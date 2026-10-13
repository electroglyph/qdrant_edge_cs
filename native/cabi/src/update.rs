use std::collections::BTreeSet;

use segment::data_types::index as segment_index;
use serde_json::{Map, Value};

use crate::error::{ApiError, Result};
use crate::json::{as_str, as_u64, field, kind, opt_bool, opt_str, opt_u64, required};

/// Parse a C# `UpdateOperation` kind-union into the engine operation.
///
/// The wire form carries a lowercase `"kind"` discriminator with the full
/// payload; numeric fields hold C# enum ordinals, mapped explicitly below so a
/// host/enumeration reorder fails loudly instead of silently reinterpreting.
pub(crate) fn parse_update_operation(v: &Value) -> Result<edge::UpdateOperation> {
    match kind(v, "update operation")? {
        "upsert_points" => parse_upsert_points(v),
        "delete_points" => parse_delete_points(v),
        "delete_points_by_filter" => parse_delete_points_by_filter(v),
        "update_vectors" => parse_update_vectors(v),
        "delete_vectors_by_filter" => parse_delete_vectors_by_filter(v),
        "delete_vectors" => parse_delete_vectors(v),
        "set_payload" => parse_set_payload(v),
        "set_payload_by_filter" => parse_set_payload_by_filter(v),
        "overwrite_payload" => parse_overwrite_payload(v),
        "overwrite_payload_by_filter" => parse_overwrite_payload_by_filter(v),
        "delete_payload" => parse_delete_payload(v),
        "delete_payload_by_filter" => parse_delete_payload_by_filter(v),
        "clear_payload" => parse_clear_payload(v),
        "clear_payload_by_filter" => parse_clear_payload_by_filter(v),
        "create_field_index" => parse_create_field_index(v),
        "create_field_index_with_params" => parse_create_field_index_with_params(v),
        "delete_field_index" => parse_delete_field_index(v),
        "create_dense_vector" => parse_create_dense_vector(v),
        "create_sparse_vector" => parse_create_sparse_vector(v),
        "delete_vector_name" => parse_delete_vector_name(v),
        other => Err(ApiError::invalid_argument(format!(
            "unknown update operation kind {other:?}"
        ))),
    }
}

fn parse_upsert_points(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "upsert_points";
    let items = required(v, "Points", ctx)?
        .as_array()
        .ok_or_else(|| ApiError::invalid_argument(format!("{ctx} field \"Points\" must be an array")))?;
    let points = items
        .iter()
        .map(super::types::parse_point)
        .collect::<Result<Vec<_>>>()?;
    let points_op = edge::PointInsertOperations::PointsList(points);
    let condition = opt_condition(v)?;
    let update_mode = opt_update_mode(v, ctx)?;
    let operation = match (condition, update_mode) {
        (Some(condition), update_mode) => edge::PointOperations::UpsertPointsConditional(
            edge::ConditionalInsertOperation {
                points_op,
                condition,
                update_mode,
            },
        ),
        (None, Some(mode)) => edge::PointOperations::UpsertPointsConditional(
            edge::ConditionalInsertOperation {
                points_op,
                condition: edge::Filter::default(),
                update_mode: Some(mode),
            },
        ),
        (None, None) => edge::PointOperations::UpsertPoints(points_op),
    };
    Ok(edge::UpdateOperation::PointOperation(operation))
}

fn parse_delete_points(v: &Value) -> Result<edge::UpdateOperation> {
    let ids = point_ids(v, "delete_points")?;
    Ok(edge::UpdateOperation::PointOperation(
        edge::PointOperations::DeletePoints { ids },
    ))
}

fn parse_delete_points_by_filter(v: &Value) -> Result<edge::UpdateOperation> {
    let filter = req_filter(v, "delete_points_by_filter")?;
    Ok(edge::UpdateOperation::PointOperation(
        edge::PointOperations::DeletePointsByFilter(filter),
    ))
}

fn parse_update_vectors(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "update_vectors";
    let items = required(v, "PointVectors", ctx)?
        .as_array()
        .ok_or_else(|| {
            ApiError::invalid_argument(format!("{ctx} field \"PointVectors\" must be an array"))
        })?;
    let points = items
        .iter()
        .map(super::types::parse_point_vectors)
        .collect::<Result<Vec<_>>>()?;
    let operation = edge::VectorOperations::UpdateVectors(edge::UpdateVectorsOp {
        points,
        update_filter: opt_condition(v)?,
    });
    Ok(edge::UpdateOperation::VectorOperation(operation))
}

fn parse_delete_vectors_by_filter(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "delete_vectors_by_filter";
    let operation = edge::VectorOperations::DeleteVectorsByFilter(
        req_filter(v, ctx)?,
        vector_names(v, ctx)?,
    );
    Ok(edge::UpdateOperation::VectorOperation(operation))
}

fn parse_delete_vectors(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "delete_vectors";
    let operation = edge::VectorOperations::DeleteVectors(
        edge::PointIdsList::from(point_ids(v, ctx)?),
        vector_names(v, ctx)?,
    );
    Ok(edge::UpdateOperation::VectorOperation(operation))
}

fn parse_set_payload(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "set_payload";
    let operation = edge::PayloadOps::SetPayload(edge::SetPayloadOp {
        payload: payload_text(v, ctx)?,
        points: Some(point_ids(v, ctx)?),
        filter: None,
        key: opt_key(v, ctx)?,
    });
    Ok(edge::UpdateOperation::PayloadOperation(operation))
}

fn parse_set_payload_by_filter(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "set_payload_by_filter";
    let operation = edge::PayloadOps::SetPayload(edge::SetPayloadOp {
        payload: payload_text(v, ctx)?,
        points: None,
        filter: Some(req_filter(v, ctx)?),
        key: opt_key(v, ctx)?,
    });
    Ok(edge::UpdateOperation::PayloadOperation(operation))
}

fn parse_overwrite_payload(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "overwrite_payload";
    let operation = edge::PayloadOps::OverwritePayload(edge::SetPayloadOp {
        payload: payload_text(v, ctx)?,
        points: Some(point_ids(v, ctx)?),
        filter: None,
        key: None,
    });
    Ok(edge::UpdateOperation::PayloadOperation(operation))
}

fn parse_overwrite_payload_by_filter(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "overwrite_payload_by_filter";
    let operation = edge::PayloadOps::OverwritePayload(edge::SetPayloadOp {
        payload: payload_text(v, ctx)?,
        points: None,
        filter: Some(req_filter(v, ctx)?),
        key: None,
    });
    Ok(edge::UpdateOperation::PayloadOperation(operation))
}

fn parse_delete_payload(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "delete_payload";
    let operation = edge::PayloadOps::DeletePayload(edge::DeletePayloadOp {
        keys: payload_keys(v, ctx)?,
        points: Some(point_ids(v, ctx)?),
        filter: None,
    });
    Ok(edge::UpdateOperation::PayloadOperation(operation))
}

fn parse_delete_payload_by_filter(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "delete_payload_by_filter";
    let operation = edge::PayloadOps::DeletePayload(edge::DeletePayloadOp {
        keys: payload_keys(v, ctx)?,
        points: None,
        filter: Some(req_filter(v, ctx)?),
    });
    Ok(edge::UpdateOperation::PayloadOperation(operation))
}

fn parse_clear_payload(v: &Value) -> Result<edge::UpdateOperation> {
    let points = point_ids(v, "clear_payload")?;
    Ok(edge::UpdateOperation::PayloadOperation(
        edge::PayloadOps::ClearPayload { points },
    ))
}

fn parse_clear_payload_by_filter(v: &Value) -> Result<edge::UpdateOperation> {
    let filter = req_filter(v, "clear_payload_by_filter")?;
    Ok(edge::UpdateOperation::PayloadOperation(
        edge::PayloadOps::ClearPayloadByFilter(filter),
    ))
}

fn parse_create_field_index(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "create_field_index";
    let operation = edge::FieldIndexOperations::CreateIndex(edge::CreateIndex {
        field_name: field_path(v, ctx)?,
        field_schema: Some(edge::PayloadFieldSchema::FieldType(parse_schema(
            as_u64(required(v, "Schema", ctx)?, ctx, "Schema")?,
            ctx,
        )?)),
    });
    Ok(edge::UpdateOperation::FieldIndexOperation(operation))
}

fn parse_create_field_index_with_params(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "create_field_index_with_params";
    let operation = edge::FieldIndexOperations::CreateIndex(edge::CreateIndex {
        field_name: field_path(v, ctx)?,
        field_schema: Some(edge::PayloadFieldSchema::FieldParams(parse_index_params(
            required(v, "IndexParams", ctx)?,
        )?)),
    });
    Ok(edge::UpdateOperation::FieldIndexOperation(operation))
}

fn parse_delete_field_index(v: &Value) -> Result<edge::UpdateOperation> {
    let operation =
        edge::FieldIndexOperations::DeleteIndex(field_path(v, "delete_field_index")?);
    Ok(edge::UpdateOperation::FieldIndexOperation(operation))
}

fn parse_create_dense_vector(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "create_dense_vector";
    let vector_name = as_str(required(v, "VectorName", ctx)?, ctx, "VectorName")?.to_owned();
    let size = as_u64(required(v, "Size", ctx)?, ctx, "Size")?;
    if !(1..=65_536).contains(&size) {
        return Err(ApiError::invalid_argument(format!(
            "vector field {vector_name:?}: size {size} is out of range 1..=65536"
        )));
    }
    let distance = parse_distance(
        as_u64(required(v, "Distance", ctx)?, ctx, "Distance")?,
        ctx,
    )?;
    let multivector_config = match field(v, "MultivectorConfig") {
        None | Some(Value::Null) => None,
        Some(config) => {
            let comparator = match as_u64(required(config, "Comparator", ctx)?, ctx, "Comparator")?
            {
                0 => edge::MultiVectorComparator::MaxSim,
                other => {
                    return Err(ApiError::invalid_argument(format!(
                        "{ctx} has unknown multivector comparator {other}"
                    )));
                }
            };
            Some(edge::MultiVectorConfig { comparator })
        }
    };
    let config = edge::VectorNameConfig::dense(edge::DenseVectorConfig {
        size: size as usize,
        distance,
        multivector_config,
        datatype: opt_datatype(v, ctx, "Datatype")?,
    });
    let operation = edge::VectorNameOperations::CreateVectorName(edge::CreateVectorName {
        vector_name,
        config,
    });
    Ok(edge::UpdateOperation::VectorNameOperation(operation))
}

fn parse_create_sparse_vector(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "create_sparse_vector";
    let vector_name = as_str(required(v, "VectorName", ctx)?, ctx, "VectorName")?.to_owned();
    let config = edge::VectorNameConfig::sparse(edge::SparseVectorConfig {
        modifier: opt_modifier(v, ctx, "Modifier")?,
        datatype: opt_datatype(v, ctx, "Datatype")?,
    });
    let operation = edge::VectorNameOperations::CreateVectorName(edge::CreateVectorName {
        vector_name,
        config,
    });
    Ok(edge::UpdateOperation::VectorNameOperation(operation))
}

fn parse_delete_vector_name(v: &Value) -> Result<edge::UpdateOperation> {
    let ctx = "delete_vector_name";
    let vector_name = as_str(required(v, "VectorName", ctx)?, ctx, "VectorName")?.to_owned();
    let operation = edge::VectorNameOperations::DeleteVectorName(edge::DeleteVectorName {
        vector_name,
    });
    Ok(edge::UpdateOperation::VectorNameOperation(operation))
}

/// Read the `"PointIds"` array through the shared point-id conversion.
fn point_ids(v: &Value, ctx: &str) -> Result<Vec<edge::PointId>> {
    let items = required(v, "PointIds", ctx)?
        .as_array()
        .ok_or_else(|| {
            ApiError::invalid_argument(format!("{ctx} field \"PointIds\" must be an array"))
        })?;
    items
        .iter()
        .map(super::types::parse_point_id)
        .collect()
}

/// Read the `"VectorNames"` string array.
fn vector_names(v: &Value, ctx: &str) -> Result<Vec<String>> {
    string_array(v, ctx, "VectorNames")
}

/// Read the `"Keys"` string array through the shared payload-key conversion.
fn payload_keys(v: &Value, ctx: &str) -> Result<Vec<edge::JsonPath>> {
    string_array(v, ctx, "Keys")?
        .iter()
        .map(|key| super::filter::parse_key(key, ctx))
        .collect()
}

/// Read the `"FieldName"` string through the shared payload-key conversion.
fn field_path(v: &Value, ctx: &str) -> Result<edge::JsonPath> {
    let name = as_str(required(v, "FieldName", ctx)?, ctx, "FieldName")?;
    super::filter::parse_key(name, ctx)
}

fn string_array(v: &Value, ctx: &str, key: &str) -> Result<Vec<String>> {
    let items = required(v, key, ctx)?.as_array().ok_or_else(|| {
        ApiError::invalid_argument(format!("{ctx} field {key:?} must be an array"))
    })?;
    items
        .iter()
        .enumerate()
        .map(|(index, item)| match item.as_str() {
            Some(text) => Ok(text.to_owned()),
            None => Err(ApiError::invalid_argument(format!(
                "{ctx} field {key:?} element {index} must be a string"
            ))),
        })
        .collect()
}

/// Read the optional `"Condition"` filter; absent and explicit null both mean
/// no condition.
fn opt_condition(v: &Value) -> Result<Option<edge::Filter>> {
    match field(v, "Condition") {
        None | Some(Value::Null) => Ok(None),
        Some(filter) => Ok(Some(super::filter::parse_filter(filter)?)),
    }
}

/// Read the required `"Filter"` object through the shared filter conversion.
fn req_filter(v: &Value, ctx: &str) -> Result<edge::Filter> {
    super::filter::parse_filter(required(v, "Filter", ctx)?)
}

/// Parse the `"PayloadJson"` string: it must itself be JSON text holding an
/// object, mirroring the engine-facing conversion.
fn payload_text(v: &Value, ctx: &str) -> Result<edge::Payload> {
    let text = as_str(required(v, "PayloadJson", ctx)?, ctx, "PayloadJson")?;
    let map: Map<String, Value> = serde_json::from_str(text)
        .map_err(|err| ApiError::invalid_argument(format!("invalid payload JSON: {err}")))?;
    Ok(edge::Payload(map))
}

/// Read the optional `"Key"`; a present key scopes the payload write at that
/// path, an absent one targets the payload root.
fn opt_key(v: &Value, ctx: &str) -> Result<Option<edge::JsonPath>> {
    match opt_str(v, ctx, "Key")? {
        None => Ok(None),
        Some(key) => Ok(Some(super::filter::parse_key(key, ctx)?)),
    }
}

/// Map the C# `UpdateMode` ordinal (Upsert/InsertOnly/UpdateOnly).
fn opt_update_mode(v: &Value, ctx: &str) -> Result<Option<edge::UpdateMode>> {
    match opt_u64(v, ctx, "UpdateMode")? {
        None => Ok(None),
        Some(0) => Ok(Some(edge::UpdateMode::Upsert)),
        Some(1) => Ok(Some(edge::UpdateMode::InsertOnly)),
        Some(2) => Ok(Some(edge::UpdateMode::UpdateOnly)),
        Some(other) => Err(ApiError::invalid_argument(format!(
            "{ctx} field \"UpdateMode\" has unknown mode {other}"
        ))),
    }
}

/// Map the C# `Distance` ordinal (Cosine/Euclid/Dot/Manhattan).
fn parse_distance(raw: u64, ctx: &str) -> Result<edge::Distance> {
    match raw {
        0 => Ok(edge::Distance::Cosine),
        1 => Ok(edge::Distance::Euclid),
        2 => Ok(edge::Distance::Dot),
        3 => Ok(edge::Distance::Manhattan),
        _ => Err(ApiError::invalid_argument(format!(
            "{ctx} field \"Distance\" has unknown distance {raw}"
        ))),
    }
}

/// Map the C# `PayloadSchemaType` ordinal
/// (Keyword/Integer/Float/Geo/Text/Bool/Datetime/Uuid).
fn parse_schema(raw: u64, ctx: &str) -> Result<edge::PayloadSchemaType> {
    match raw {
        0 => Ok(edge::PayloadSchemaType::Keyword),
        1 => Ok(edge::PayloadSchemaType::Integer),
        2 => Ok(edge::PayloadSchemaType::Float),
        3 => Ok(edge::PayloadSchemaType::Geo),
        4 => Ok(edge::PayloadSchemaType::Text),
        5 => Ok(edge::PayloadSchemaType::Bool),
        6 => Ok(edge::PayloadSchemaType::Datetime),
        7 => Ok(edge::PayloadSchemaType::Uuid),
        _ => Err(ApiError::invalid_argument(format!(
            "{ctx} field \"Schema\" has unknown schema {raw}"
        ))),
    }
}

/// Map the C# `Modifier` ordinal (None/Idf); absent and null mean unset.
fn opt_modifier(v: &Value, ctx: &str, key: &str) -> Result<Option<edge::Modifier>> {
    match opt_u64(v, ctx, key)? {
        None => Ok(None),
        Some(0) => Ok(Some(edge::Modifier::None)),
        Some(1) => Ok(Some(edge::Modifier::Idf)),
        Some(other) => Err(ApiError::invalid_argument(format!(
            "{ctx} field {key:?} has unknown modifier {other}"
        ))),
    }
}

/// Map the C# `VectorStorageDatatype` ordinal (Float32/Float16/Uint8/Turbo4);
/// absent and null mean the engine default.
fn opt_datatype(
    v: &Value,
    ctx: &str,
    key: &str,
) -> Result<Option<edge::VectorStorageDatatype>> {
    match opt_u64(v, ctx, key)? {
        None => Ok(None),
        Some(0) => Ok(Some(edge::VectorStorageDatatype::Float32)),
        Some(1) => Ok(Some(edge::VectorStorageDatatype::Float16)),
        Some(2) => Ok(Some(edge::VectorStorageDatatype::Uint8)),
        Some(3) => Ok(Some(edge::VectorStorageDatatype::Turbo4)),
        Some(other) => Err(ApiError::invalid_argument(format!(
            "{ctx} field {key:?} has unknown datatype {other}"
        ))),
    }
}

/// Map the C# `Memory` ordinal (Cold/Cached/Pinned); absent and null mean the
/// engine default.
fn opt_memory(v: &Value, ctx: &str, key: &str) -> Result<Option<edge::Memory>> {
    match opt_u64(v, ctx, key)? {
        None => Ok(None),
        Some(0) => Ok(Some(edge::Memory::Cold)),
        Some(1) => Ok(Some(edge::Memory::Cached)),
        Some(2) => Ok(Some(edge::Memory::Pinned)),
        Some(other) => Err(ApiError::invalid_argument(format!(
            "{ctx} field {key:?} has unknown memory {other}"
        ))),
    }
}

/// Map the C# `TokenizerType` ordinal (Prefix/Whitespace/Word/Multilingual).
fn parse_tokenizer(raw: u64, ctx: &str) -> Result<segment_index::TokenizerType> {
    match raw {
        0 => Ok(segment_index::TokenizerType::Prefix),
        1 => Ok(segment_index::TokenizerType::Whitespace),
        2 => Ok(segment_index::TokenizerType::Word),
        3 => Ok(segment_index::TokenizerType::Multilingual),
        _ => Err(ApiError::invalid_argument(format!(
            "{ctx} field \"Tokenizer\" has unknown tokenizer {raw}"
        ))),
    }
}

/// Map the C# `Language` ordinal onto the engine stopword language.
fn parse_language(raw: u64, ctx: &str) -> Result<edge::Language> {
    match raw {
        0 => Ok(edge::Language::Arabic),
        1 => Ok(edge::Language::Azerbaijani),
        2 => Ok(edge::Language::Basque),
        3 => Ok(edge::Language::Bengali),
        4 => Ok(edge::Language::Catalan),
        5 => Ok(edge::Language::Chinese),
        6 => Ok(edge::Language::Danish),
        7 => Ok(edge::Language::Dutch),
        8 => Ok(edge::Language::English),
        9 => Ok(edge::Language::Finnish),
        10 => Ok(edge::Language::French),
        11 => Ok(edge::Language::German),
        12 => Ok(edge::Language::Greek),
        13 => Ok(edge::Language::Hebrew),
        14 => Ok(edge::Language::Hinglish),
        15 => Ok(edge::Language::Hungarian),
        16 => Ok(edge::Language::Indonesian),
        17 => Ok(edge::Language::Italian),
        18 => Ok(edge::Language::Japanese),
        19 => Ok(edge::Language::Kazakh),
        20 => Ok(edge::Language::Nepali),
        21 => Ok(edge::Language::Norwegian),
        22 => Ok(edge::Language::Portuguese),
        23 => Ok(edge::Language::Romanian),
        24 => Ok(edge::Language::Russian),
        25 => Ok(edge::Language::Slovene),
        26 => Ok(edge::Language::Spanish),
        27 => Ok(edge::Language::Swedish),
        28 => Ok(edge::Language::Tajik),
        29 => Ok(edge::Language::Turkish),
        _ => Err(ApiError::invalid_argument(format!(
            "{ctx} has unknown language {raw}"
        ))),
    }
}

/// Map the C# `SnowballLanguage` ordinal onto the engine stemmer language.
fn parse_snowball_language(raw: u64, ctx: &str) -> Result<edge::SnowballLanguage> {
    match raw {
        0 => Ok(edge::SnowballLanguage::Arabic),
        1 => Ok(edge::SnowballLanguage::Armenian),
        2 => Ok(edge::SnowballLanguage::Danish),
        3 => Ok(edge::SnowballLanguage::Dutch),
        4 => Ok(edge::SnowballLanguage::English),
        5 => Ok(edge::SnowballLanguage::Finnish),
        6 => Ok(edge::SnowballLanguage::French),
        7 => Ok(edge::SnowballLanguage::German),
        8 => Ok(edge::SnowballLanguage::Greek),
        9 => Ok(edge::SnowballLanguage::Hungarian),
        10 => Ok(edge::SnowballLanguage::Italian),
        11 => Ok(edge::SnowballLanguage::Norwegian),
        12 => Ok(edge::SnowballLanguage::Portuguese),
        13 => Ok(edge::SnowballLanguage::Romanian),
        14 => Ok(edge::SnowballLanguage::Russian),
        15 => Ok(edge::SnowballLanguage::Spanish),
        16 => Ok(edge::SnowballLanguage::Swedish),
        17 => Ok(edge::SnowballLanguage::Tamil),
        18 => Ok(edge::SnowballLanguage::Turkish),
        _ => Err(ApiError::invalid_argument(format!(
            "{ctx} has unknown snowball language {raw}"
        ))),
    }
}

/// Parse the 8-variant `PayloadIndexParams` kind-union. The variant selects the
/// index type; its `"Config"` carries the per-type tuning options.
#[allow(deprecated)]
fn parse_index_params(v: &Value) -> Result<edge::PayloadSchemaParams> {
    let ctx = "payload index params";
    match kind(v, ctx)? {
        "keyword" => {
            let config = required(v, "Config", ctx)?;
            Ok(edge::PayloadSchemaParams::Keyword(
                segment_index::KeywordIndexParams {
                    r#type: segment_index::KeywordIndexType::Keyword,
                    is_tenant: opt_bool(config, ctx, "IsTenant")?,
                    on_disk: None,
                    memory: opt_memory(config, ctx, "Memory")?,
                    enable_hnsw: opt_bool(config, ctx, "EnableHnsw")?,
                    prefix: opt_bool(config, ctx, "Prefix")?,
                },
            ))
        }
        "integer" => {
            let config = required(v, "Config", ctx)?;
            let lookup = opt_bool(config, ctx, "Lookup")?;
            let range = opt_bool(config, ctx, "Range")?;
            segment_index::validate_integer_index_params(&lookup, &range).map_err(|_| {
                ApiError::invalid_argument(
                    "integer index: the 'lookup' and 'range' capabilities can't be both disabled",
                )
            })?;
            Ok(edge::PayloadSchemaParams::Integer(
                segment_index::IntegerIndexParams {
                    r#type: segment_index::IntegerIndexType::Integer,
                    lookup,
                    range,
                    is_principal: opt_bool(config, ctx, "IsPrincipal")?,
                    on_disk: None,
                    memory: opt_memory(config, ctx, "Memory")?,
                    enable_hnsw: opt_bool(config, ctx, "EnableHnsw")?,
                },
            ))
        }
        "float" => {
            let config = required(v, "Config", ctx)?;
            Ok(edge::PayloadSchemaParams::Float(
                segment_index::FloatIndexParams {
                    r#type: segment_index::FloatIndexType::Float,
                    is_principal: opt_bool(config, ctx, "IsPrincipal")?,
                    on_disk: None,
                    memory: opt_memory(config, ctx, "Memory")?,
                    enable_hnsw: opt_bool(config, ctx, "EnableHnsw")?,
                },
            ))
        }
        "geo" => {
            let config = required(v, "Config", ctx)?;
            Ok(edge::PayloadSchemaParams::Geo(
                segment_index::GeoIndexParams {
                    r#type: segment_index::GeoIndexType::Geo,
                    on_disk: None,
                    memory: opt_memory(config, ctx, "Memory")?,
                    enable_hnsw: opt_bool(config, ctx, "EnableHnsw")?,
                },
            ))
        }
        "text" => Ok(edge::PayloadSchemaParams::Text(parse_text_config(
            required(v, "Config", ctx)?,
            ctx,
        )?)),
        "bool" => {
            let config = required(v, "Config", ctx)?;
            Ok(edge::PayloadSchemaParams::Bool(
                segment_index::BoolIndexParams {
                    r#type: segment_index::BoolIndexType::Bool,
                    on_disk: None,
                    memory: opt_memory(config, ctx, "Memory")?,
                    enable_hnsw: opt_bool(config, ctx, "EnableHnsw")?,
                },
            ))
        }
        "datetime" => {
            let config = required(v, "Config", ctx)?;
            Ok(edge::PayloadSchemaParams::Datetime(
                segment_index::DatetimeIndexParams {
                    r#type: segment_index::DatetimeIndexType::Datetime,
                    is_principal: opt_bool(config, ctx, "IsPrincipal")?,
                    on_disk: None,
                    memory: opt_memory(config, ctx, "Memory")?,
                    enable_hnsw: opt_bool(config, ctx, "EnableHnsw")?,
                },
            ))
        }
        "uuid" => {
            let config = required(v, "Config", ctx)?;
            Ok(edge::PayloadSchemaParams::Uuid(
                segment_index::UuidIndexParams {
                    r#type: segment_index::UuidIndexType::Uuid,
                    is_tenant: opt_bool(config, ctx, "IsTenant")?,
                    on_disk: None,
                    memory: opt_memory(config, ctx, "Memory")?,
                    enable_hnsw: opt_bool(config, ctx, "EnableHnsw")?,
                },
            ))
        }
        other => Err(ApiError::invalid_argument(format!(
            "unknown payload index kind {other:?}"
        ))),
    }
}

#[allow(deprecated)]
fn parse_text_config(v: &Value, ctx: &str) -> Result<segment_index::TextIndexParams> {
    let tokenizer = match opt_u64(v, ctx, "Tokenizer")? {
        None => segment_index::TokenizerType::Word,
        Some(raw) => parse_tokenizer(raw, ctx)?,
    };
    let min_token_len = token_len("min_token_len", opt_u64(v, ctx, "MinTokenLen")?)?;
    let max_token_len = token_len("max_token_len", opt_u64(v, ctx, "MaxTokenLen")?)?;
    segment_index::validate_text_index_params(&min_token_len, &max_token_len).map_err(|_| {
        ApiError::invalid_argument(
            "text index: 'min_token_len' can't be greater than 'max_token_len'",
        )
    })?;
    let stopwords = match field(v, "Stopwords") {
        None | Some(Value::Null) => None,
        Some(stopwords) => Some(parse_stopwords(stopwords, ctx)?),
    };
    let stemmer = match field(v, "Stemmer") {
        None | Some(Value::Null) => None,
        Some(stemmer) => Some(parse_stemmer(stemmer, ctx)?),
    };
    Ok(segment_index::TextIndexParams {
        r#type: segment_index::TextIndexType::Text,
        tokenizer,
        min_token_len,
        max_token_len,
        lowercase: opt_bool(v, ctx, "Lowercase")?,
        ascii_folding: opt_bool(v, ctx, "AsciiFolding")?,
        phrase_matching: opt_bool(v, ctx, "PhraseMatching")?,
        stopwords,
        on_disk: None,
        memory: opt_memory(v, ctx, "Memory")?,
        stemmer,
        enable_hnsw: opt_bool(v, ctx, "EnableHnsw")?,
    })
}

/// Parse the `Stopwords` kind-union (`"language"` with an ordinal, or `"set"`
/// with optional language ordinals and custom tokens).
fn parse_stopwords(v: &Value, ctx: &str) -> Result<segment_index::StopwordsInterface> {
    match kind(v, ctx)? {
        "language" => {
            let language = parse_language(
                as_u64(required(v, "Language", ctx)?, ctx, "Language")?,
                ctx,
            )?;
            Ok(segment_index::StopwordsInterface::Language(language))
        }
        "set" => {
            let languages = match field(v, "Languages") {
                None | Some(Value::Null) => None,
                Some(list) => {
                    let items = list.as_array().ok_or_else(|| {
                        ApiError::invalid_argument(format!(
                            "{ctx} field \"Languages\" must be an array"
                        ))
                    })?;
                    let mut set = BTreeSet::new();
                    for item in items {
                        set.insert(parse_language(as_u64(item, ctx, "Languages")?, ctx)?);
                    }
                    Some(set)
                }
            };
            let custom = match field(v, "Custom") {
                None | Some(Value::Null) => None,
                Some(list) => {
                    let items = list.as_array().ok_or_else(|| {
                        ApiError::invalid_argument(format!(
                            "{ctx} field \"Custom\" must be an array"
                        ))
                    })?;
                    let mut set = BTreeSet::new();
                    for item in items {
                        set.insert(as_str(item, ctx, "Custom")?.to_owned());
                    }
                    Some(set)
                }
            };
            Ok(segment_index::StopwordsInterface::Set(
                segment_index::StopwordsSet { languages, custom },
            ))
        }
        other => Err(ApiError::invalid_argument(format!(
            "{ctx} has unknown stopwords kind {other:?}"
        ))),
    }
}

/// Parse the `Stemmer` kind-union (`"snowball"` with an ordinal, or
/// `"disabled"` which carries no payload).
fn parse_stemmer(v: &Value, ctx: &str) -> Result<segment_index::StemmingAlgorithm> {
    match kind(v, ctx)? {
        "snowball" => {
            let language = parse_snowball_language(
                as_u64(required(v, "Language", ctx)?, ctx, "Language")?,
                ctx,
            )?;
            Ok(segment_index::StemmingAlgorithm::Snowball(
                segment_index::SnowballParams {
                    r#type: segment_index::Snowball::Snowball,
                    language,
                },
            ))
        }
        "disabled" => Ok(segment_index::StemmingAlgorithm::Disabled(
            segment_index::DisabledStemmerParams {
                r#type: segment_index::NoStemmer::None,
            },
        )),
        other => Err(ApiError::invalid_argument(format!(
            "{ctx} has unknown stemmer kind {other:?}"
        ))),
    }
}

/// Convert a host token length to the engine width, rejecting values that do
/// not fit instead of truncating.
fn token_len(name: &str, len: Option<u64>) -> Result<Option<usize>> {
    len.map(|len| {
        usize::try_from(len)
            .map_err(|_| ApiError::invalid_argument(format!("{name} {len} out of range")))
    })
    .transpose()
}
