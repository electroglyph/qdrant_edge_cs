use segment::data_types::groups::GroupId as SegmentGroupId;
use segment::data_types::index::{StemmingAlgorithm, StopwordsInterface};
use segment::data_types::order_by::OrderValue as SegmentOrderValue;
use segment::json_path::JsonPath;
use segment::types::{
    Payload, PayloadSelector, PayloadSelectorExclude, PointIdType,
    ScoredPoint as SegmentScoredPoint, WithPayloadInterface, WithVector as SegmentWithVector,
};
use serde_json::{Map, Value, json};
use sparse::common::sparse_vector::validate_sparse_vector_impl;

use crate::error::{ApiError, Result};
use crate::json::{as_bool, as_str, as_u64, f32_array, field, kind, required};

fn parse_key(key: &str) -> Result<JsonPath> {
    key.parse().map_err(|_: ()| {
        ApiError::invalid_argument(format!(
            "invalid payload key {key:?}: not a valid JSON path"
        ))
    })
}

fn reject_non_finite(values: &[f32], ctx: &str) -> Result<()> {
    if let Some(pos) = values.iter().position(|x| !x.is_finite()) {
        return Err(ApiError::invalid_argument(format!(
            "{ctx} contains a non-finite value (NaN or infinity) at index {pos}"
        )));
    }
    Ok(())
}

/// Dense vector dimension bounds shared by the config and data paths
/// (1..=65536). The engine reports a wrong-sized vector as an `OperationError`
/// (dimension mismatch), so the data path must reject empty and oversize
/// vectors here to surface `InvalidArgument` like the C# factory does.
const MIN_DENSE_LEN: usize = 1;
const MAX_DENSE_LEN: usize = 65_536;

fn check_dense_len(values: &[f32], ctx: &str) -> Result<()> {
    if values.len() < MIN_DENSE_LEN || values.len() > MAX_DENSE_LEN {
        return Err(ApiError::invalid_argument(format!(
            "{ctx} has {} elements, expected {MIN_DENSE_LEN}..={MAX_DENSE_LEN}",
            values.len(),
        )));
    }
    Ok(())
}

fn validate_multi_dense(vectors: &[Vec<f32>], ctx: &str) -> Result<()> {
    if vectors.is_empty() {
        return Err(ApiError::invalid_argument(format!(
            "{ctx} multi-vector must contain at least one vector"
        )));
    }
    let dim = vectors[0].len();
    if dim == 0 {
        return Err(ApiError::invalid_argument(format!(
            "{ctx} multi-vector has zero dimension"
        )));
    }
    if dim > MAX_DENSE_LEN {
        return Err(ApiError::invalid_argument(format!(
            "{ctx} multi-vector dimension {dim} exceeds the maximum of {MAX_DENSE_LEN}"
        )));
    }
    for (i, v) in vectors.iter().enumerate() {
        if v.len() != dim {
            return Err(ApiError::invalid_argument(format!(
                "{ctx} multi-vector has inconsistent row length: row {i} has {} \
                 elements, expected {dim}",
                v.len()
            )));
        }
        reject_non_finite(v, ctx)?;
    }
    Ok(())
}

fn to_internal_sparse(
    indices: Vec<u32>,
    values: Vec<f32>,
    ctx: &str,
) -> Result<edge::SparseVector> {
    reject_non_finite(&values, ctx)?;
    validate_sparse_vector_impl(&indices, &values)
        .map_err(|e| ApiError::invalid_argument(format!("invalid {ctx}: {e}")))?;
    Ok(edge::SparseVector { indices, values })
}

fn parse_f32_matrix(v: &Value, ctx: &str) -> Result<Vec<Vec<f32>>> {
    let rows = v.as_array().ok_or_else(|| {
        ApiError::invalid_argument(format!("{ctx} must be an array of vectors"))
    })?;
    rows.iter()
        .enumerate()
        .map(|(i, row)| {
            let items = row.as_array().ok_or_else(|| {
                ApiError::invalid_argument(format!("{ctx} row {i} must be an array of numbers"))
            })?;
            items
                .iter()
                .enumerate()
                .map(|(j, value)| match value.as_f64() {
                    Some(raw) if raw.is_finite() && raw <= f64::from(f32::MAX) && raw >= -f64::from(f32::MAX) => {
                        Ok(raw as f32)
                    }
                    _ => Err(ApiError::invalid_argument(format!(
                        "{ctx} row {i} element {j} must be a finite number"
                    ))),
                })
                .collect()
        })
        .collect()
}

fn parse_sparse_vector(v: &Value, ctx: &str) -> Result<edge::SparseVector> {
    let indices_raw = v
        .as_object()
        .and_then(|m| m.get("Indices"))
        .ok_or_else(|| {
            ApiError::invalid_argument(format!("{ctx} is missing required field \"Indices\""))
        })?;
    let indices_items = indices_raw.as_array().ok_or_else(|| {
        ApiError::invalid_argument(format!("{ctx} field \"Indices\" must be an array of numbers"))
    })?;
    let mut indices = Vec::with_capacity(indices_items.len());
    for (i, raw) in indices_items.iter().enumerate() {
        let n = match raw {
            Value::Number(n) => n.as_u64(),
            _ => None,
        }
        .ok_or_else(|| {
            ApiError::invalid_argument(format!(
                "{ctx} field \"Indices\" element {i} must be a non-negative integer"
            ))
        })?;
        indices.push(u32::try_from(n).map_err(|_| {
            ApiError::invalid_argument(format!(
                "{ctx} field \"Indices\" element {i} ({n}) exceeds u32 range"
            ))
        })?);
    }
    let values_raw = v
        .as_object()
        .and_then(|m| m.get("Values"))
        .ok_or_else(|| {
            ApiError::invalid_argument(format!("{ctx} is missing required field \"Values\""))
        })?;
    let values = f32_array(values_raw, ctx)?;
    to_internal_sparse(indices, values, ctx)
}

fn parse_string_array(v: &Value, ctx: &str, key: &str) -> Result<Vec<String>> {
    let items = required(v, key, ctx)?.as_array().ok_or_else(|| {
        ApiError::invalid_argument(format!("{ctx} field {key:?} must be an array of strings"))
    })?;
    items
        .iter()
        .enumerate()
        .map(|(i, item)| {
            item.as_str().map(str::to_owned).ok_or_else(|| {
                ApiError::invalid_argument(format!(
                    "{ctx} field {key:?} element {i} must be a string"
                ))
            })
        })
        .collect()
}

/// Parse a C# `PointId` union (`{"kind":"num","Value":…}` /
/// `{"kind":"uuid","Value":…}`) into the engine id.
pub(crate) fn parse_point_id(v: &Value) -> Result<PointIdType> {
    let ctx = "point id";
    match kind(v, ctx)? {
        "num" => Ok(PointIdType::NumId(as_u64(required(v, "Value", ctx)?, ctx, "Value")?)),
        "uuid" => {
            let raw = as_str(required(v, "Value", ctx)?, ctx, "Value")?;
            let uuid = uuid::Uuid::parse_str(raw).map_err(|e| {
                ApiError::invalid_argument(format!("invalid point UUID {raw:?}: {e}"))
            })?;
            Ok(PointIdType::Uuid(uuid))
        }
        other => Err(ApiError::invalid_argument(format!(
            "{ctx} has unknown kind {other:?}"
        ))),
    }
}

/// Parse a C# `NamedVector` union into the engine single-named-vector type.
pub(crate) fn parse_named_vector(v: &Value) -> Result<edge::VectorPersisted> {
    let ctx = "named vector";
    match kind(v, ctx)? {
        "dense" => {
            let values = f32_array(required(v, "Values", ctx)?, "dense vector")?;
            reject_non_finite(&values, "dense vector")?;
            check_dense_len(&values, "dense vector")?;
            Ok(edge::VectorPersisted::Dense(values))
        }
        "sparse" => {
            let raw = required(v, "Vector", ctx)?;
            Ok(edge::VectorPersisted::Sparse(parse_sparse_vector(
                raw,
                "sparse vector",
            )?))
        }
        "multi_dense" => {
            let vectors = parse_f32_matrix(required(v, "Vectors", ctx)?, "named multi-vector")?;
            validate_multi_dense(&vectors, "named")?;
            Ok(edge::VectorPersisted::MultiDense(vectors))
        }
        other => Err(ApiError::invalid_argument(format!(
            "{ctx} has unknown kind {other:?}"
        ))),
    }
}

/// Parse a C# `Vector` union into the engine upsert vector type.
pub(crate) fn parse_vector(v: &Value) -> Result<edge::VectorStructPersisted> {
    let ctx = "vector";
    match kind(v, ctx)? {
        "single" => {
            let values = f32_array(required(v, "Values", ctx)?, "dense vector")?;
            reject_non_finite(&values, "dense vector")?;
            check_dense_len(&values, "dense vector")?;
            Ok(edge::VectorStructPersisted::Single(values))
        }
        "multi_dense" => {
            let vectors =
                parse_f32_matrix(required(v, "Vectors", ctx)?, "single-field multi-vector")?;
            validate_multi_dense(&vectors, "single-field")?;
            Ok(edge::VectorStructPersisted::MultiDense(vectors))
        }
        "named" => {
            let map = required(v, "Map", ctx)?.as_object().ok_or_else(|| {
                ApiError::invalid_argument("vector field \"Map\" must be an object")
            })?;
            let named: std::collections::HashMap<String, edge::VectorPersisted> = map
                .iter()
                .map(|(name, raw)| Ok((name.clone(), parse_named_vector(raw)?)))
                .collect::<Result<_>>()?;
            Ok(edge::VectorStructPersisted::Named(named))
        }
        other => Err(ApiError::invalid_argument(format!(
            "{ctx} has unknown kind {other:?}"
        ))),
    }
}

/// Parse a C# payload JSON value into the engine payload. Must be an object;
/// anything else is rejected the same way the string-based boundary does.
pub(crate) fn parse_payload(v: &Value) -> Result<Payload> {
    let map: Map<String, Value> = serde_json::from_value(v.clone())
        .map_err(|e| ApiError::invalid_argument(format!("invalid payload JSON: {e}")))?;
    Ok(Payload(map))
}

/// Parse a C# `Point` into the engine upsert type. A missing or null payload
/// stores no payload.
pub(crate) fn parse_point(v: &Value) -> Result<edge::PointStructPersisted> {
    let ctx = "point";
    let id = parse_point_id(required(v, "Id", ctx)?)?;
    let vector = parse_vector(required(v, "Vector", ctx)?)?;
    let payload = match field(v, "Payload") {
        None | Some(Value::Null) => None,
        Some(raw) => Some(parse_payload(raw)?),
    };
    Ok(edge::PointStructPersisted { id, vector, payload })
}

/// Parse a C# `PointVectors` into the engine update-vectors type.
pub(crate) fn parse_point_vectors(v: &Value) -> Result<edge::PointVectorsPersisted> {
    let ctx = "point vectors";
    Ok(edge::PointVectorsPersisted {
        id: parse_point_id(required(v, "Id", ctx)?)?,
        vector: parse_vector(required(v, "Vector", ctx)?)?,
    })
}

/// Parse a C# `WithPayload` union. A null value means the selector was not
/// supplied, so no payload preference is returned.
pub(crate) fn parse_with_payload(v: &Value) -> Result<Option<WithPayloadInterface>> {
    if v.is_null() {
        return Ok(None);
    }
    let ctx = "with payload";
    match kind(v, ctx)? {
        "bool" => Ok(Some(WithPayloadInterface::Bool(as_bool(
            required(v, "Enable", ctx)?,
            ctx,
            "Enable",
        )?))),
        "fields" => {
            let fields = parse_string_array(v, ctx, "Fields")?;
            Ok(Some(WithPayloadInterface::Fields(
                fields.iter().map(|f| parse_key(f)).collect::<Result<_>>()?,
            )))
        }
        "exclude" => {
            let fields = parse_string_array(v, ctx, "Fields")?;
            Ok(Some(WithPayloadInterface::Selector(
                PayloadSelector::Exclude(PayloadSelectorExclude::new(
                    fields.iter().map(|f| parse_key(f)).collect::<Result<_>>()?,
                )),
            )))
        }
        other => Err(ApiError::invalid_argument(format!(
            "{ctx} has unknown kind {other:?}"
        ))),
    }
}

/// Parse a C# `WithVector` union. A null value means the selector was not
/// supplied, so the engine default (no vectors) applies.
pub(crate) fn parse_with_vector(v: &Value) -> Result<SegmentWithVector> {
    if v.is_null() {
        return Ok(SegmentWithVector::default());
    }
    let ctx = "with vector";
    match kind(v, ctx)? {
        "bool" => Ok(SegmentWithVector::Bool(as_bool(
            required(v, "Enable", ctx)?,
            ctx,
            "Enable",
        )?)),
        "names" => Ok(SegmentWithVector::Selector(parse_string_array(
            v, ctx, "Names",
        )?)),
        other => Err(ApiError::invalid_argument(format!(
            "{ctx} has unknown kind {other:?}"
        ))),
    }
}

/// Serialize an engine point id to the C# `PointId` union shape.
pub(crate) fn point_id_to_json(id: &PointIdType) -> Value {
    match id {
        PointIdType::NumId(n) => json!({"kind": "num", "Value": n}),
        PointIdType::Uuid(u) => json!({"kind": "uuid", "Value": u.to_string()}),
    }
}

fn named_vector_to_json(v: &edge::VectorInternal) -> Value {
    match v {
        edge::VectorInternal::Dense(values) => json!({"kind": "dense", "Values": values}),
        edge::VectorInternal::Sparse(sparse) => json!({
            "kind": "sparse",
            "Vector": {"Indices": sparse.indices, "Values": sparse.values},
        }),
        edge::VectorInternal::MultiDense(multi) => {
            json!({"kind": "multi_dense", "Vectors": multi_dense_matrix(multi)})
        }
    }
}

fn multi_dense_matrix(multi: &segment::data_types::vectors::MultiDenseVectorInternal) -> Value {
    if multi.dim == 0 {
        return Value::Array(Vec::new());
    }
    let rows: Vec<Value> = multi
        .flattened_vectors
        .chunks(multi.dim)
        .map(|row| Value::from(row.to_vec()))
        .collect();
    Value::Array(rows)
}

fn vector_struct_to_json(v: &edge::VectorStructInternal) -> Value {
    match v {
        edge::VectorStructInternal::Single(dense) => json!({"kind": "single", "Values": dense}),
        edge::VectorStructInternal::MultiDense(multi) => {
            json!({"kind": "multi_dense", "Vectors": multi_dense_matrix(multi)})
        }
        edge::VectorStructInternal::Named(map) => {
            let entries: Map<String, Value> = map
                .iter()
                .map(|(name, vec)| (name.clone(), named_vector_to_json(vec)))
                .collect();
            json!({"kind": "named", "Map": Value::Object(entries)})
        }
    }
}

fn payload_to_string(payload: &Payload) -> String {
    serde_json::to_string(&payload.0).expect("payload JSON serialization is infallible")
}

fn vector_to_string(vector: &edge::VectorStructInternal) -> String {
    serde_json::to_string(&vector_struct_to_json(vector))
        .expect("vector JSON serialization is infallible")
}

/// Serialize an engine order value to the C# `OrderValue` union shape.
pub(crate) fn order_value_to_json(v: &SegmentOrderValue) -> Value {
    match v {
        SegmentOrderValue::Int(n) => json!({"kind": "int", "Value": n}),
        SegmentOrderValue::Float(f) => json!({"kind": "float", "Value": f}),
    }
}

/// Serialize an engine group key to the C# `GroupId` union shape.
pub(crate) fn group_id_to_json(id: &SegmentGroupId) -> Value {
    match id {
        SegmentGroupId::String(s) => json!({"kind": "string", "Value": s}),
        SegmentGroupId::NumberU64(n) => json!({"kind": "u64", "Value": n}),
        SegmentGroupId::NumberI64(n) => json!({"kind": "i64", "Value": n}),
    }
}

/// Serialize an engine scored point to the C# `ScoredPoint` shape. Payload and
/// vector travel as embedded JSON strings, null when not requested.
pub(crate) fn scored_point_to_json(p: &SegmentScoredPoint) -> Value {
    json!({
        "Id": point_id_to_json(&p.id),
        "Version": p.version,
        "Score": p.score,
        "Payload": p.payload.as_ref().map(payload_to_string).map(Value::from).unwrap_or(Value::Null),
        "Vector": p.vector.as_ref().map(vector_to_string).map(Value::from).unwrap_or(Value::Null),
        "OrderValue": p.order_value.as_ref().map(order_value_to_json).unwrap_or(Value::Null),
    })
}

/// Serialize an engine record to the C# `Record` shape. Payload and vector
/// travel as embedded JSON strings, null when not requested.
pub(crate) fn record_to_json(r: &edge::Record) -> Value {
    json!({
        "Id": point_id_to_json(&r.id),
        "Payload": r.payload.as_ref().map(payload_to_string).map(Value::from).unwrap_or(Value::Null),
        "Vector": r.vector.as_ref().map(vector_to_string).map(Value::from).unwrap_or(Value::Null),
        "OrderValue": r.order_value.as_ref().map(order_value_to_json).unwrap_or(Value::Null),
    })
}

fn memory_number(memory: edge::Memory) -> Value {
    json!(match memory {
        edge::Memory::Cold => 0,
        edge::Memory::Cached => 1,
        edge::Memory::Pinned => 2,
    })
}

fn opt_memory_number(memory: Option<edge::Memory>) -> Value {
    memory.map(memory_number).unwrap_or(Value::Null)
}

fn opt_bool_json(value: Option<bool>) -> Value {
    value.map(Value::from).unwrap_or(Value::Null)
}

fn opt_u64_json(value: Option<usize>) -> Value {
    value.map(|v| Value::from(v as u64)).unwrap_or(Value::Null)
}

fn stopwords_to_json(stopwords: &StopwordsInterface) -> Value {
    match stopwords {
        // Declaration order verified against the C# `Language` enum (30 values).
        StopwordsInterface::Language(language) => {
            json!({"kind": "language", "Language": *language as u64})
        }
        StopwordsInterface::Set(set) => json!({
            "kind": "set",
            "Languages": set.languages.as_ref().map(|langs| langs.iter().map(|l| *l as u64).collect::<Vec<_>>()).map(Value::from).unwrap_or(Value::Null),
            "Custom": set.custom.as_ref().map(|custom| custom.iter().cloned().collect::<Vec<_>>()).map(Value::from).unwrap_or(Value::Null),
        }),
    }
}

fn stemmer_to_json(stemmer: &StemmingAlgorithm) -> Value {
    match stemmer {
        // Declaration order verified against the C# `SnowballLanguage` enum (19 values).
        StemmingAlgorithm::Snowball(params) => {
            json!({"kind": "snowball", "Language": params.language as u64})
        }
        StemmingAlgorithm::Disabled(_) => json!({"kind": "disabled"}),
    }
}

fn schema_params_to_json(params: &edge::PayloadSchemaParams) -> Value {
    match params {
        edge::PayloadSchemaParams::Keyword(p) => json!({"kind": "keyword", "Config": {
            "IsTenant": opt_bool_json(p.is_tenant),
            "Memory": opt_memory_number(p.memory),
            "EnableHnsw": opt_bool_json(p.enable_hnsw),
            "Prefix": opt_bool_json(p.prefix),
        }}),
        edge::PayloadSchemaParams::Integer(p) => json!({"kind": "integer", "Config": {
            "Lookup": opt_bool_json(p.lookup),
            "Range": opt_bool_json(p.range),
            "IsPrincipal": opt_bool_json(p.is_principal),
            "Memory": opt_memory_number(p.memory),
            "EnableHnsw": opt_bool_json(p.enable_hnsw),
        }}),
        edge::PayloadSchemaParams::Float(p) => json!({"kind": "float", "Config": {
            "IsPrincipal": opt_bool_json(p.is_principal),
            "Memory": opt_memory_number(p.memory),
            "EnableHnsw": opt_bool_json(p.enable_hnsw),
        }}),
        edge::PayloadSchemaParams::Geo(p) => json!({"kind": "geo", "Config": {
            "Memory": opt_memory_number(p.memory),
            "EnableHnsw": opt_bool_json(p.enable_hnsw),
        }}),
        edge::PayloadSchemaParams::Text(p) => json!({"kind": "text", "Config": {
            // Declaration order verified against the C# `TokenizerType` enum.
            "Tokenizer": p.tokenizer as u64,
            "MinTokenLen": opt_u64_json(p.min_token_len),
            "MaxTokenLen": opt_u64_json(p.max_token_len),
            "Lowercase": opt_bool_json(p.lowercase),
            "AsciiFolding": opt_bool_json(p.ascii_folding),
            "PhraseMatching": opt_bool_json(p.phrase_matching),
            "Stopwords": p.stopwords.as_ref().map(stopwords_to_json).unwrap_or(Value::Null),
            "Memory": opt_memory_number(p.memory),
            "Stemmer": p.stemmer.as_ref().map(stemmer_to_json).unwrap_or(Value::Null),
            "EnableHnsw": opt_bool_json(p.enable_hnsw),
        }}),
        edge::PayloadSchemaParams::Bool(p) => json!({"kind": "bool", "Config": {
            "Memory": opt_memory_number(p.memory),
            "EnableHnsw": opt_bool_json(p.enable_hnsw),
        }}),
        edge::PayloadSchemaParams::Datetime(p) => json!({"kind": "datetime", "Config": {
            "IsPrincipal": opt_bool_json(p.is_principal),
            "Memory": opt_memory_number(p.memory),
            "EnableHnsw": opt_bool_json(p.enable_hnsw),
        }}),
        edge::PayloadSchemaParams::Uuid(p) => json!({"kind": "uuid", "Config": {
            "IsTenant": opt_bool_json(p.is_tenant),
            "Memory": opt_memory_number(p.memory),
            "EnableHnsw": opt_bool_json(p.enable_hnsw),
        }}),
    }
}

/// Serialize engine payload index info to the C# `PayloadIndexInfo` shape.
pub(crate) fn payload_info_to_json(info: &edge::PayloadIndexInfo) -> Value {
    let data_type = match info.data_type {
        edge::PayloadSchemaType::Keyword => 0,
        edge::PayloadSchemaType::Integer => 1,
        edge::PayloadSchemaType::Float => 2,
        edge::PayloadSchemaType::Geo => 3,
        edge::PayloadSchemaType::Text => 4,
        edge::PayloadSchemaType::Bool => 5,
        edge::PayloadSchemaType::Datetime => 6,
        edge::PayloadSchemaType::Uuid => 7,
    };
    json!({
        "DataType": data_type,
        "Params": info.params.as_ref().map(schema_params_to_json).unwrap_or(Value::Null),
        "Points": info.points as u64,
    })
}

#[cfg(test)]
mod tests {
    use serde_json::json;

    use super::*;

    #[test]
    fn parse_point_id_num_and_uuid() {
        let num = parse_point_id(&json!({"kind": "num", "Value": 42})).unwrap();
        assert_eq!(num, PointIdType::NumId(42));
        let uuid = parse_point_id(&json!({
            "kind": "uuid",
            "Value": "550e8400-e29b-41d4-a716-446655440000",
        }))
        .unwrap();
        assert!(matches!(uuid, PointIdType::Uuid(_)));
        let err = parse_point_id(&json!({"kind": "uuid", "Value": "nope"})).unwrap_err();
        assert!(err.message.contains("invalid point UUID"), "{err:?}");
    }

    #[test]
    fn parse_vector_single_rejects_non_finite() {
        let err = parse_vector(&json!({"kind": "single", "Values": [1.0, 1e40]})).unwrap_err();
        assert!(err.message.contains("must be a finite number"), "{err:?}");
    }

    #[test]
    fn parse_vector_single_rejects_empty_and_oversize() {
        let err = parse_vector(&json!({"kind": "single", "Values": []})).unwrap_err();
        assert!(err.message.contains("1..=65536"), "{err:?}");
        let err = parse_named_vector(&json!({"kind": "dense", "Values": []})).unwrap_err();
        assert!(err.message.contains("1..=65536"), "{err:?}");
        let huge = vec![1.0; 65_537];
        let err =
            parse_vector(&json!({"kind": "single", "Values": huge})).unwrap_err();
        assert!(err.message.contains("1..=65536"), "{err:?}");
    }

    #[test]
    fn parse_vector_multi_dense_rejects_ragged() {
        let err = parse_vector(&json!({"kind": "multi_dense", "Vectors": [[1.0], [1.0, 2.0]]})).unwrap_err();
        assert!(err.message.contains("inconsistent row length"), "{err:?}");
    }

    #[test]
    fn parse_named_vector_sparse_rejects_duplicates() {
        let err = parse_named_vector(&json!({
            "kind": "sparse",
            "Vector": {"Indices": [1, 1], "Values": [1.0, 2.0]},
        }))
        .unwrap_err();
        assert!(err.message.contains("invalid sparse vector"), "{err:?}");
    }

    #[test]
    fn parse_point_payload_must_be_object() {
        let err = parse_point(&json!({
            "Id": {"kind": "num", "Value": 1},
            "Vector": {"kind": "single", "Values": [1.0]},
            "Payload": [1, 2],
        }))
        .unwrap_err();
        assert!(err.message.contains("invalid payload JSON"), "{err:?}");
    }

    #[test]
    fn parse_with_payload_null_means_none() {
        assert!(parse_with_payload(&Value::Null).unwrap().is_none());
        let selected = parse_with_payload(&json!({"kind": "bool", "Enable": true})).unwrap();
        assert_eq!(selected, Some(WithPayloadInterface::Bool(true)));
    }

    #[test]
    fn parse_with_vector_null_means_default() {
        assert_eq!(parse_with_vector(&Value::Null).unwrap(), SegmentWithVector::Bool(false));
    }

    #[test]
    fn scored_point_to_json_embeds_strings() {
        let point = SegmentScoredPoint {
            id: PointIdType::NumId(7),
            version: 3,
            score: 0.5,
            payload: Some(Payload(Map::from_iter([(
                "a".to_owned(),
                json!(1),
            )]))),
            vector: Some(edge::VectorStructInternal::Single(vec![1.0, 2.0])),
            shard_key: None,
            order_value: Some(SegmentOrderValue::Int(9)),
        };
        let out = scored_point_to_json(&point);
        assert_eq!(out["Id"], json!({"kind": "num", "Value": 7}));
        assert_eq!(out["Version"], json!(3));
        let payload: Value = serde_json::from_str(out["Payload"].as_str().unwrap()).unwrap();
        assert_eq!(payload, json!({"a": 1}));
        let vector: Value = serde_json::from_str(out["Vector"].as_str().unwrap()).unwrap();
        assert_eq!(vector, json!({"kind": "single", "Values": [1.0, 2.0]}));
        assert_eq!(out["OrderValue"], json!({"kind": "int", "Value": 9}));
    }

    #[test]
    fn group_and_payload_info_shapes() {
        assert_eq!(
            group_id_to_json(&SegmentGroupId::NumberU64(5)),
            json!({"kind": "u64", "Value": 5})
        );
        let info = edge::PayloadIndexInfo {
            data_type: edge::PayloadSchemaType::Keyword,
            params: None,
            points: 12,
        };
        let out = payload_info_to_json(&info);
        assert_eq!(out["DataType"], json!(0));
        assert_eq!(out["Params"], Value::Null);
        assert_eq!(out["Points"], json!(12));
    }
}
