use std::collections::HashMap;

use segment::data_types::modifier::Modifier as SegmentModifier;
use segment::types::{
    BinaryQuantization, BinaryQuantizationConfig,
    BinaryQuantizationEncoding as SegmentBinaryEncoding,
    BinaryQuantizationQueryEncoding as SegmentBinaryQueryEncoding,
    CompressionRatio as SegmentCompressionRatio, Distance as SegmentDistance,
    HnswConfig as SegmentHnswConfig, Memory as SegmentMemory,
    MultiVectorComparator as SegmentComparator, MultiVectorConfig as SegmentMultiVectorConfig,
    ProductQuantization, ProductQuantizationConfig,
    QuantizationConfig as SegmentQuantizationConfig, ScalarQuantization,
    ScalarQuantizationConfig, ScalarType as SegmentScalarType,
    VectorStorageDatatype as SegmentDatatype,
};
use serde_json::{Map, Value, json};

use crate::error::{ApiError, Result, clamp_usize};
use crate::json::{as_f32, as_u64, field, kind, opt_bool, opt_u64, required};

const MIN_VECTOR_SIZE: u64 = 1;
const MAX_VECTOR_SIZE: u64 = 65_536;

const MAX_HNSW_M: u64 = 2_048;
const MIN_HNSW_EF_CONSTRUCT: u64 = 4;
const MAX_HNSW_EF_CONSTRUCT: u64 = 100_000;
const MAX_HNSW_INDEXING_THREADS: u64 = 1_024;

fn check_vector_size(name: &str, size: u64) -> Result<()> {
    if size < MIN_VECTOR_SIZE || size > MAX_VECTOR_SIZE {
        return Err(ApiError::invalid_argument(format!(
            "vector field {name:?}: size {size} is out of range \
             {MIN_VECTOR_SIZE}..={MAX_VECTOR_SIZE}"
        )));
    }
    Ok(())
}

fn check_hnsw(
    scope: &str,
    m: u64,
    payload_m: Option<u64>,
    ef_construct: u64,
    max_indexing_threads: u64,
) -> Result<()> {
    if m > MAX_HNSW_M {
        return Err(ApiError::invalid_argument(format!(
            "{scope}: hnsw m ({m}) exceeds the maximum of {MAX_HNSW_M}"
        )));
    }
    if let Some(payload_m) = payload_m {
        if payload_m > MAX_HNSW_M {
            return Err(ApiError::invalid_argument(format!(
                "{scope}: hnsw payload_m ({payload_m}) exceeds the maximum of {MAX_HNSW_M}"
            )));
        }
    }
    if ef_construct < MIN_HNSW_EF_CONSTRUCT || ef_construct > MAX_HNSW_EF_CONSTRUCT {
        return Err(ApiError::invalid_argument(format!(
            "{scope}: hnsw ef_construct ({ef_construct}) is out of range \
             {MIN_HNSW_EF_CONSTRUCT}..={MAX_HNSW_EF_CONSTRUCT}"
        )));
    }
    if max_indexing_threads > MAX_HNSW_INDEXING_THREADS {
        return Err(ApiError::invalid_argument(format!(
            "{scope}: hnsw max_indexing_threads ({max_indexing_threads}) exceeds the maximum of \
             {MAX_HNSW_INDEXING_THREADS}"
        )));
    }
    Ok(())
}

/// Parse and validate a C# `EdgeConfig` JSON document into the engine config.
pub(crate) fn parse_edge_config(v: &Value) -> Result<edge::EdgeConfig> {
    let vectors = match field(v, "VectorData") {
        None | Some(Value::Null) => HashMap::new(),
        Some(map) => parse_vector_data_map(map)?,
    };
    let sparse_vectors = match field(v, "SparseVectorData") {
        None | Some(Value::Null) => HashMap::new(),
        Some(map) => parse_sparse_data_map(map)?,
    };
    if vectors.is_empty() && sparse_vectors.is_empty() {
        return Err(ApiError::invalid_argument(
            "config must define at least one of vectors or sparse_vectors",
        ));
    }
    Ok(edge::EdgeConfig {
        vectors,
        sparse_vectors,
        payload_memory: opt_memory(v, "config", "PayloadMemory")?,
        id_tracker_memory: opt_memory(v, "config", "IdTrackerMemory")?,
        ..Default::default()
    })
}

fn parse_vector_data_map(v: &Value) -> Result<HashMap<String, edge::EdgeVectorParams>> {
    let map = v.as_object().ok_or_else(|| {
        ApiError::invalid_argument("config field \"VectorData\" must be an object")
    })?;
    map.iter()
        .map(|(name, cfg)| Ok((name.clone(), parse_vector_field(name, cfg)?)))
        .collect()
}

#[allow(deprecated)]
fn parse_vector_field(name: &str, v: &Value) -> Result<edge::EdgeVectorParams> {
    let ctx = format!("vector field {name:?}");
    let size = as_u64(required(v, "Size", &ctx)?, &ctx, "Size")?;
    check_vector_size(name, size)?;
    let distance = parse_distance(required(v, "Distance", &ctx)?, &ctx)?;
    let quantization = match field(v, "QuantizationConfig") {
        None | Some(Value::Null) => None,
        Some(q) => Some(parse_quantization(q, &ctx)?),
    };
    let multivector_config = match field(v, "MultivectorConfig") {
        None | Some(Value::Null) => None,
        Some(m) => Some(parse_multivector(m, &ctx)?),
    };
    let datatype = opt_datatype(v, &ctx, "Datatype")?;
    let hnsw_config = match field(v, "HnswConfig") {
        None | Some(Value::Null) => None,
        Some(h) => Some(parse_hnsw_value(h, &ctx)?),
    };
    Ok(edge::EdgeVectorParams {
        size: clamp_usize(size),
        distance,
        on_disk: None,
        memory: opt_memory(v, &ctx, "Memory")?,
        multivector_config,
        datatype,
        quantization_config: quantization,
        hnsw_config,
    })
}

fn parse_sparse_data_map(v: &Value) -> Result<HashMap<String, edge::EdgeSparseVectorParams>> {
    let map = v.as_object().ok_or_else(|| {
        ApiError::invalid_argument("config field \"SparseVectorData\" must be an object")
    })?;
    map.iter()
        .map(|(name, cfg)| Ok((name.clone(), parse_sparse_field(name, cfg)?)))
        .collect()
}

#[allow(deprecated)]
fn parse_sparse_field(name: &str, v: &Value) -> Result<edge::EdgeSparseVectorParams> {
    let ctx = format!("sparse vector field {name:?}");
    Ok(edge::EdgeSparseVectorParams {
        full_scan_threshold: opt_u64(v, &ctx, "FullScanThreshold")?.map(clamp_usize),
        on_disk: None,
        memory: opt_memory(v, &ctx, "Memory")?,
        modifier: opt_modifier(v, &ctx)?,
        datatype: opt_datatype(v, &ctx, "Datatype")?,
    })
}

fn parse_distance(v: &Value, ctx: &str) -> Result<SegmentDistance> {
    match as_u64(v, ctx, "Distance")? {
        0 => Ok(SegmentDistance::Cosine),
        1 => Ok(SegmentDistance::Euclid),
        2 => Ok(SegmentDistance::Dot),
        3 => Ok(SegmentDistance::Manhattan),
        other => Err(ApiError::invalid_argument(format!(
            "{ctx} field \"Distance\" has unknown distance {other}"
        ))),
    }
}

fn parse_memory_number(n: u64, ctx: &str, key: &str) -> Result<SegmentMemory> {
    match n {
        0 => Ok(SegmentMemory::Cold),
        1 => Ok(SegmentMemory::Cached),
        2 => Ok(SegmentMemory::Pinned),
        other => Err(ApiError::invalid_argument(format!(
            "{ctx} field {key:?} has unknown memory placement {other}"
        ))),
    }
}

fn opt_memory(v: &Value, ctx: &str, key: &str) -> Result<Option<SegmentMemory>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => Ok(Some(parse_memory_number(as_u64(raw, ctx, key)?, ctx, key)?)),
    }
}

fn opt_datatype(v: &Value, ctx: &str, key: &str) -> Result<Option<SegmentDatatype>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => match as_u64(raw, ctx, key)? {
            0 => Ok(Some(SegmentDatatype::Float32)),
            1 => Ok(Some(SegmentDatatype::Float16)),
            2 => Ok(Some(SegmentDatatype::Uint8)),
            3 => Ok(Some(SegmentDatatype::Turbo4)),
            other => Err(ApiError::invalid_argument(format!(
                "{ctx} field {key:?} has unknown datatype {other}"
            ))),
        },
    }
}

fn opt_modifier(v: &Value, ctx: &str) -> Result<Option<SegmentModifier>> {
    match field(v, "Modifier") {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => match as_u64(raw, ctx, "Modifier")? {
            0 => Ok(Some(SegmentModifier::None)),
            1 => Ok(Some(SegmentModifier::Idf)),
            other => Err(ApiError::invalid_argument(format!(
                "{ctx} field \"Modifier\" has unknown modifier {other}"
            ))),
        },
    }
}

fn parse_multivector(v: &Value, ctx: &str) -> Result<SegmentMultiVectorConfig> {
    let raw = as_u64(required(v, "Comparator", ctx)?, ctx, "Comparator")?;
    match raw {
        0 => Ok(SegmentMultiVectorConfig {
            comparator: SegmentComparator::MaxSim,
        }),
        other => Err(ApiError::invalid_argument(format!(
            "{ctx} field \"Comparator\" has unknown comparator {other}"
        ))),
    }
}

fn parse_quantization(v: &Value, ctx: &str) -> Result<SegmentQuantizationConfig> {
    let ctx = format!("{ctx} quantization");
    let ctx = ctx.as_str();
    let config = required(v, "Config", ctx)?;
    match kind(v, ctx)? {
        "scalar" => {
            let raw = as_u64(required(config, "Type", ctx)?, ctx, "Type")?;
            let scalar_type = match raw {
                0 => SegmentScalarType::Int8,
                other => {
                    return Err(ApiError::invalid_argument(format!(
                        "{ctx} field \"Type\" has unknown scalar type {other}"
                    )));
                }
            };
            Ok(SegmentQuantizationConfig::Scalar(ScalarQuantization {
                scalar: ScalarQuantizationConfig {
                    r#type: scalar_type,
                    quantile: opt_f32(config, ctx, "Quantile")?,
                    #[allow(deprecated)]
                    always_ram: None,
                    memory: opt_memory(config, ctx, "Memory")?,
                },
            }))
        }
        "product" => {
            let raw = as_u64(required(config, "Compression", ctx)?, ctx, "Compression")?;
            let compression = match raw {
                0 => SegmentCompressionRatio::X4,
                1 => SegmentCompressionRatio::X8,
                2 => SegmentCompressionRatio::X16,
                3 => SegmentCompressionRatio::X32,
                4 => SegmentCompressionRatio::X64,
                other => {
                    return Err(ApiError::invalid_argument(format!(
                        "{ctx} field \"Compression\" has unknown compression ratio {other}"
                    )));
                }
            };
            Ok(SegmentQuantizationConfig::Product(ProductQuantization {
                product: ProductQuantizationConfig {
                    compression,
                    #[allow(deprecated)]
                    always_ram: None,
                    memory: opt_memory(config, ctx, "Memory")?,
                },
            }))
        }
        "binary" => Ok(SegmentQuantizationConfig::Binary(BinaryQuantization {
            binary: BinaryQuantizationConfig {
                #[allow(deprecated)]
                always_ram: None,
                encoding: opt_binary_encoding(config, ctx)?,
                query_encoding: opt_binary_query_encoding(config, ctx)?,
                memory: opt_memory(config, ctx, "Memory")?,
            },
        })),
        "turbo" => Ok(SegmentQuantizationConfig::Turbo(
            segment::types::TurboQuantization {
                turbo: segment::types::TurboQuantQuantizationConfig {
                    #[allow(deprecated)]
                    always_ram: None,
                    bits: opt_turbo_bits(config, ctx)?,
                    memory: opt_memory(config, ctx, "Memory")?,
                },
            },
        )),
        other => Err(ApiError::invalid_argument(format!(
            "{ctx} has unknown kind {other:?}"
        ))),
    }
}

fn opt_binary_encoding(v: &Value, ctx: &str) -> Result<Option<SegmentBinaryEncoding>> {
    match field(v, "Encoding") {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => match as_u64(raw, ctx, "Encoding")? {
            0 => Ok(Some(SegmentBinaryEncoding::OneBit)),
            1 => Ok(Some(SegmentBinaryEncoding::TwoBits)),
            2 => Ok(Some(SegmentBinaryEncoding::OneAndHalfBits)),
            other => Err(ApiError::invalid_argument(format!(
                "{ctx} field \"Encoding\" has unknown binary encoding {other}"
            ))),
        },
    }
}

fn opt_binary_query_encoding(
    v: &Value,
    ctx: &str,
) -> Result<Option<SegmentBinaryQueryEncoding>> {
    match field(v, "QueryEncoding") {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => match as_u64(raw, ctx, "QueryEncoding")? {
            0 => Ok(Some(SegmentBinaryQueryEncoding::Default)),
            1 => Ok(Some(SegmentBinaryQueryEncoding::Binary)),
            2 => Ok(Some(SegmentBinaryQueryEncoding::Scalar4Bits)),
            3 => Ok(Some(SegmentBinaryQueryEncoding::Scalar8Bits)),
            other => Err(ApiError::invalid_argument(format!(
                "{ctx} field \"QueryEncoding\" has unknown binary query encoding {other}"
            ))),
        },
    }
}

fn opt_turbo_bits(
    v: &Value,
    ctx: &str,
) -> Result<Option<segment::types::TurboQuantBitSize>> {
    use segment::types::TurboQuantBitSize as SegmentBits;
    match field(v, "Bits") {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => match as_u64(raw, ctx, "Bits")? {
            0 => Ok(Some(SegmentBits::Bits1)),
            1 => Ok(Some(SegmentBits::Bits1_5)),
            2 => Ok(Some(SegmentBits::Bits2)),
            3 => Ok(Some(SegmentBits::Bits4)),
            other => Err(ApiError::invalid_argument(format!(
                "{ctx} field \"Bits\" has unknown turbo bit size {other}"
            ))),
        },
    }
}

fn opt_f32(v: &Value, ctx: &str, key: &str) -> Result<Option<f32>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => Ok(Some(as_f32(raw, ctx, key)?)),
    }
}

fn opt_f64(v: &Value, ctx: &str, key: &str) -> Result<Option<f64>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => match raw.as_f64() {
            Some(f) if f.is_finite() => Ok(Some(f)),
            _ => Err(ApiError::invalid_argument(format!(
                "{ctx} field {key:?} must be a finite number"
            ))),
        },
    }
}

/// Parse a C# `HnswIndexConfig` JSON document into the engine HNSW config.
/// Shared by the global and per-vector setters; both take the same DTO shape.
pub(crate) fn parse_hnsw_config(v: &Value) -> Result<edge::HnswIndexConfig> {
    parse_hnsw_value(v, "hnsw config")
}

#[allow(deprecated)]
fn parse_hnsw_value(v: &Value, scope: &str) -> Result<SegmentHnswConfig> {
    let m = opt_u64(v, scope, "M")?.unwrap_or(16);
    let ef_construct = opt_u64(v, scope, "EfConstruct")?.unwrap_or(100);
    let full_scan_threshold = opt_u64(v, scope, "FullScanThreshold")?.unwrap_or(10_000);
    let max_indexing_threads = opt_u64(v, scope, "MaxIndexingThreads")?.unwrap_or(0);
    let payload_m = opt_u64(v, scope, "PayloadM")?;
    let memory = opt_memory(v, scope, "Memory")?;
    check_hnsw(scope, m, payload_m, ef_construct, max_indexing_threads)?;
    Ok(SegmentHnswConfig {
        m: clamp_usize(m),
        ef_construct: clamp_usize(ef_construct),
        full_scan_threshold: clamp_usize(full_scan_threshold),
        max_indexing_threads: clamp_usize(max_indexing_threads),
        on_disk: None,
        memory,
        payload_m: payload_m.map(clamp_usize),
        inline_storage: None,
    })
}

/// Parse a C# `OptimizersConfig` JSON document into the engine optimizers config.
pub(crate) fn parse_optimizers_config(v: &Value) -> Result<edge::EdgeOptimizersConfig> {
    let ctx = "optimizers config";
    Ok(edge::EdgeOptimizersConfig {
        deleted_threshold: opt_f64(v, ctx, "DeletedThreshold")?,
        vacuum_min_vector_number: opt_u64(v, ctx, "VacuumMinVectorNumber")?.map(clamp_usize),
        default_segment_number: opt_u64(v, ctx, "DefaultSegmentNumber")?.map(clamp_usize),
        max_segment_size: opt_u64(v, ctx, "MaxSegmentSizeKb")?.map(clamp_usize),
        indexing_threshold: opt_u64(v, ctx, "IndexingThresholdKb")?.map(clamp_usize),
        prevent_unoptimized: opt_bool(v, ctx, "PreventUnoptimized")?,
    })
}

fn memory_to_json(memory: SegmentMemory) -> Value {
    json!(match memory {
        SegmentMemory::Cold => 0,
        SegmentMemory::Cached => 1,
        SegmentMemory::Pinned => 2,
    })
}

fn opt_memory_to_json(memory: Option<SegmentMemory>) -> Value {
    memory.map(memory_to_json).unwrap_or(Value::Null)
}

fn distance_to_json(distance: SegmentDistance) -> Value {
    json!(match distance {
        SegmentDistance::Cosine => 0,
        SegmentDistance::Euclid => 1,
        SegmentDistance::Dot => 2,
        SegmentDistance::Manhattan => 3,
    })
}

fn datatype_to_json(datatype: SegmentDatatype) -> Value {
    json!(match datatype {
        SegmentDatatype::Float32 => 0,
        SegmentDatatype::Float16 => 1,
        SegmentDatatype::Uint8 => 2,
        SegmentDatatype::Turbo4 => 3,
    })
}

fn quantization_to_json(q: &SegmentQuantizationConfig) -> Value {
    match q {
        SegmentQuantizationConfig::Scalar(ScalarQuantization { scalar }) => {
            let scalar_type = match scalar.r#type {
                SegmentScalarType::Int8 => 0,
            };
            let mut config = Map::new();
            config.insert("Type".to_owned(), json!(scalar_type));
            config.insert(
                "Quantile".to_owned(),
                scalar.quantile.map(Value::from).unwrap_or(Value::Null),
            );
            config.insert(
                "Memory".to_owned(),
                opt_memory_to_json(scalar.memory_placement()),
            );
            json!({"kind": "scalar", "Config": Value::Object(config)})
        }
        SegmentQuantizationConfig::Product(ProductQuantization { product }) => {
            let compression = match product.compression {
                SegmentCompressionRatio::X4 => 0,
                SegmentCompressionRatio::X8 => 1,
                SegmentCompressionRatio::X16 => 2,
                SegmentCompressionRatio::X32 => 3,
                SegmentCompressionRatio::X64 => 4,
            };
            let mut config = Map::new();
            config.insert("Compression".to_owned(), json!(compression));
            config.insert(
                "Memory".to_owned(),
                opt_memory_to_json(product.memory_placement()),
            );
            json!({"kind": "product", "Config": Value::Object(config)})
        }
        SegmentQuantizationConfig::Binary(BinaryQuantization { binary }) => {
            let mut config = Map::new();
            config.insert(
                "Memory".to_owned(),
                opt_memory_to_json(binary.memory_placement()),
            );
            config.insert(
                "Encoding".to_owned(),
                binary
                    .encoding
                    .map(|e| match e {
                        SegmentBinaryEncoding::OneBit => json!(0),
                        SegmentBinaryEncoding::TwoBits => json!(1),
                        SegmentBinaryEncoding::OneAndHalfBits => json!(2),
                    })
                    .unwrap_or(Value::Null),
            );
            config.insert(
                "QueryEncoding".to_owned(),
                binary
                    .query_encoding
                    .map(|e| match e {
                        SegmentBinaryQueryEncoding::Default => json!(0),
                        SegmentBinaryQueryEncoding::Binary => json!(1),
                        SegmentBinaryQueryEncoding::Scalar4Bits => json!(2),
                        SegmentBinaryQueryEncoding::Scalar8Bits => json!(3),
                    })
                    .unwrap_or(Value::Null),
            );
            json!({"kind": "binary", "Config": Value::Object(config)})
        }
        SegmentQuantizationConfig::Turbo(segment::types::TurboQuantization { turbo }) => {
            let mut config = Map::new();
            config.insert(
                "Memory".to_owned(),
                opt_memory_to_json(turbo.memory_placement()),
            );
            config.insert(
                "Bits".to_owned(),
                turbo
                    .bits
                    .map(|b| {
                        use segment::types::TurboQuantBitSize as SegmentBits;
                        match b {
                            SegmentBits::Bits1 => json!(0),
                            SegmentBits::Bits1_5 => json!(1),
                            SegmentBits::Bits2 => json!(2),
                            SegmentBits::Bits4 => json!(3),
                        }
                    })
                    .unwrap_or(Value::Null),
            );
            json!({"kind": "turbo", "Config": Value::Object(config)})
        }
    }
}

#[allow(deprecated)]
fn hnsw_to_json(h: &SegmentHnswConfig) -> Value {
    let SegmentHnswConfig {
        m,
        ef_construct,
        full_scan_threshold,
        max_indexing_threads,
        on_disk,
        memory,
        payload_m,
        inline_storage: _,
    } = *h;
    let memory = SegmentMemory::resolve(memory, on_disk.map(SegmentMemory::from_on_disk));
    json!({
        "M": m as u64,
        "EfConstruct": ef_construct as u64,
        "FullScanThreshold": full_scan_threshold as u64,
        "MaxIndexingThreads": max_indexing_threads as u64,
        "Memory": opt_memory_to_json(memory),
        "PayloadM": payload_m.map(|v| v as u64).map(Value::from).unwrap_or(Value::Null),
    })
}

fn vector_params_to_json(
    p: &edge::EdgeVectorParams,
    global_quantization: Option<&SegmentQuantizationConfig>,
) -> Value {
    let quantization = p
        .quantization_config
        .as_ref()
        .or(global_quantization)
        .map(quantization_to_json)
        .unwrap_or(Value::Null);
    json!({
        "Size": p.size as u64,
        "Distance": distance_to_json(p.distance),
        "QuantizationConfig": quantization,
        "MultivectorConfig": p.multivector_config.map(|m| {
            let comparator = match m.comparator {
                SegmentComparator::MaxSim => 0,
            };
            json!({"Comparator": comparator})
        }).unwrap_or(Value::Null),
        "Datatype": p.datatype.map(datatype_to_json).unwrap_or(Value::Null),
        "HnswConfig": p.hnsw_config.as_ref().map(hnsw_to_json).unwrap_or(Value::Null),
        "Memory": opt_memory_to_json(p.memory_placement()),
    })
}

fn sparse_params_to_json(p: &edge::EdgeSparseVectorParams) -> Value {
    json!({
        "FullScanThreshold": p.full_scan_threshold.map(|v| v as u64).map(Value::from).unwrap_or(Value::Null),
        "Datatype": p.datatype.map(datatype_to_json).unwrap_or(Value::Null),
        "Modifier": p.modifier.map(|m| match m {
            SegmentModifier::None => json!(0),
            SegmentModifier::Idf => json!(1),
        }).unwrap_or(Value::Null),
        "Memory": opt_memory_to_json(p.memory_placement()),
    })
}

/// Serialize the engine config back to a C# `EdgeConfig` JSON document.
/// As-requested read-back: each field reflects what was configured, falling
/// back to the shard-global quantization the engine applies, and never
/// substituting an engine default for an unset value.
pub(crate) fn edge_config_to_json(cfg: &edge::EdgeConfig) -> Value {
    let vectors: Map<String, Value> = cfg
        .vectors
        .iter()
        .map(|(name, p)| {
            (
                name.clone(),
                vector_params_to_json(p, cfg.quantization_config.as_ref()),
            )
        })
        .collect();
    let sparse_vectors: Map<String, Value> = cfg
        .sparse_vectors
        .iter()
        .map(|(name, p)| (name.clone(), sparse_params_to_json(p)))
        .collect();
    json!({
        "VectorData": Value::Object(vectors),
        "SparseVectorData": Value::Object(sparse_vectors),
        "PayloadMemory": opt_memory_to_json(cfg.requested_payload_memory()),
        "IdTrackerMemory": opt_memory_to_json(cfg.id_tracker_memory),
    })
}

#[cfg(test)]
mod tests {
    use serde_json::json;

    use super::*;

    fn dense_config() -> Value {
        json!({
            "VectorData": {
                "": {"Size": 4, "Distance": 0},
                "text": {
                    "Size": 8,
                    "Distance": 2,
                    "QuantizationConfig": {"kind": "scalar", "Config": {"Type": 0, "Quantile": 0.99, "Memory": 2}},
                    "MultivectorConfig": {"Comparator": 0},
                    "Datatype": 1,
                    "HnswConfig": {"M": 16, "EfConstruct": 100, "FullScanThreshold": 10000, "MaxIndexingThreads": 0, "Memory": 1, "PayloadM": null},
                    "Memory": 1,
                },
            },
            "SparseVectorData": {
                "splade": {"FullScanThreshold": 100, "Datatype": 0, "Modifier": 1, "Memory": 0},
            },
            "PayloadMemory": 0,
            "IdTrackerMemory": null,
        })
    }

    #[test]
    fn parse_edge_config_dense_and_sparse() {
        let cfg = parse_edge_config(&dense_config()).unwrap();
        assert_eq!(cfg.vectors.len(), 2);
        assert_eq!(cfg.sparse_vectors.len(), 1);
        let text = &cfg.vectors["text"];
        assert_eq!(text.size, 8);
        assert!(matches!(text.distance, SegmentDistance::Dot));
        assert!(text.quantization_config.is_some());
        assert!(text.multivector_config.is_some());
        assert!(text.hnsw_config.is_some());
        assert_eq!(cfg.payload_memory, Some(SegmentMemory::Cold));
        assert_eq!(cfg.id_tracker_memory, None);
    }

    #[test]
    fn parse_edge_config_empty_is_rejected() {
        let err = parse_edge_config(&json!({})).unwrap_err();
        assert_eq!(
            err.message,
            "config must define at least one of vectors or sparse_vectors"
        );
    }

    #[test]
    fn parse_edge_config_bad_size_is_rejected() {
        let v = json!({"VectorData": {"": {"Size": 0, "Distance": 0}}});
        let err = parse_edge_config(&v).unwrap_err();
        assert!(err.message.contains("size 0 is out of range 1..=65536"), "{err:?}");
    }

    #[test]
    fn parse_edge_config_bad_hnsw_is_rejected() {
        let v = json!({"VectorData": {"": {"Size": 4, "Distance": 0,
            "HnswConfig": {"M": 4096, "EfConstruct": 100}}}});
        let err = parse_edge_config(&v).unwrap_err();
        assert!(err.message.contains("hnsw m (4096) exceeds the maximum of 2048"), "{err:?}");
    }

    #[test]
    fn parse_edge_config_bad_distance_is_rejected() {
        let v = json!({"VectorData": {"": {"Size": 4, "Distance": 9}}});
        let err = parse_edge_config(&v).unwrap_err();
        assert!(err.message.contains("unknown distance 9"), "{err:?}");
    }

    #[test]
    fn parse_optimizers_config_kb_mapping() {
        let cfg = parse_optimizers_config(&json!({
            "DeletedThreshold": 0.5,
            "VacuumMinVectorNumber": 100,
            "DefaultSegmentNumber": 2,
            "MaxSegmentSizeKb": 1024,
            "IndexingThresholdKb": 512,
            "PreventUnoptimized": true,
        }))
        .unwrap();
        assert_eq!(cfg.deleted_threshold, Some(0.5));
        assert_eq!(cfg.max_segment_size, Some(1024));
        assert_eq!(cfg.indexing_threshold, Some(512));
        assert_eq!(cfg.prevent_unoptimized, Some(true));
    }

    #[test]
    fn edge_config_to_json_round_trip_subset() {
        let cfg = parse_edge_config(&dense_config()).unwrap();
        let out = edge_config_to_json(&cfg);
        assert_eq!(out["VectorData"]["text"]["Size"], json!(8));
        assert_eq!(out["VectorData"]["text"]["Distance"], json!(2));
        assert_eq!(out["VectorData"]["text"]["QuantizationConfig"]["kind"], json!("scalar"));
        assert_eq!(
            out["VectorData"]["text"]["QuantizationConfig"]["Config"]["Type"],
            json!(0)
        );
        assert_eq!(out["VectorData"]["text"]["MultivectorConfig"]["Comparator"], json!(0));
        assert_eq!(out["VectorData"]["text"]["HnswConfig"]["M"], json!(16));
        assert_eq!(out["SparseVectorData"]["splade"]["Modifier"], json!(1));
        assert_eq!(out["PayloadMemory"], json!(0));
        assert_eq!(out["IdTrackerMemory"], Value::Null);
        let back = parse_edge_config(&out).unwrap();
        assert_eq!(back.vectors.len(), 2);
        assert_eq!(back.sparse_vectors.len(), 1);
    }
}
