use std::collections::HashMap;

use serde_json::{Value, json};

use crate::error::{ApiError, Result, bounded_limit, check_nesting_depth, clamp_usize};
use crate::json::{
    as_f32, as_str, as_u64, field, kind, opt_bool, opt_str, opt_u64, required,
};
use edge::external::ordered_float::OrderedFloat;

// ── Search ──────────────────────────────────────────────────────────────────

pub(crate) fn parse_search_request(v: &Value) -> Result<edge::SearchRequest> {
    const CTX: &str = "search request";
    Ok(edge::SearchRequest {
        query: parse_vector_query(required(v, "Query", CTX)?)?,
        limit: bounded_limit("limit", as_u64(required(v, "Limit", CTX)?, CTX, "Limit")?)?,
        offset: bounded_limit("offset", opt_u64(v, CTX, "Offset")?.unwrap_or(0))?,
        filter: opt_filter(v)?,
        params: opt_params(v)?,
        with_vector: opt_with_vector(v)?,
        with_payload: opt_with_payload(v)?,
        score_threshold: opt_f32(v, CTX, "ScoreThreshold")?,
    })
}

// ── Query ───────────────────────────────────────────────────────────────────

pub(crate) fn parse_query_request(v: &Value) -> Result<edge::QueryRequest> {
    const CTX: &str = "query request";
    Ok(edge::QueryRequest {
        prefetches: opt_array(v, "Prefetches", CTX)?
            .into_iter()
            .map(|item| parse_prefetch(item, 0))
            .collect::<Result<Vec<_>>>()?,
        query: opt_scoring_query(v)?,
        filter: opt_filter(v)?,
        score_threshold: opt_f32(v, CTX, "ScoreThreshold")?,
        limit: bounded_limit("limit", as_u64(required(v, "Limit", CTX)?, CTX, "Limit")?)?,
        offset: bounded_limit("offset", opt_u64(v, CTX, "Offset")?.unwrap_or(0))?,
        params: opt_params(v)?,
        with_vector: with_vector_or_default(v)?,
        with_payload: with_payload_or_default(v)?,
    })
}

pub(crate) fn parse_query_batch_request(v: &Value) -> Result<edge::QueryBatchRequest> {
    const CTX: &str = "query batch request";
    let queries = opt_array(v, "Queries", CTX)?
        .into_iter()
        .map(parse_query_request)
        .collect::<Result<Vec<_>>>()?;
    Ok(edge::QueryBatchRequest::new(queries))
}

pub(crate) fn parse_prefetch(v: &Value, depth: u32) -> Result<edge::Prefetch> {
    check_nesting_depth("prefetch", depth)?;
    const CTX: &str = "prefetch";
    Ok(edge::Prefetch {
        prefetches: opt_array(v, "Prefetches", CTX)?
            .into_iter()
            .map(|item| parse_prefetch(item, depth + 1))
            .collect::<Result<Vec<_>>>()?,
        query: opt_scoring_query(v)?,
        limit: bounded_limit(
            "prefetch limit",
            as_u64(required(v, "Limit", CTX)?, CTX, "Limit")?,
        )?,
        params: opt_params(v)?,
        filter: opt_filter(v)?,
        score_threshold: opt_f32(v, CTX, "ScoreThreshold")?,
    })
}

// ── Scroll / retrieve / count / facet / group / matrix ──────────────────────

pub(crate) fn parse_scroll_request(v: &Value) -> Result<edge::ScrollRequest> {
    const CTX: &str = "scroll request";
    Ok(edge::ScrollRequest {
        offset: match field(v, "Offset") {
            None | Some(Value::Null) => None,
            Some(raw) => Some(super::types::parse_point_id(raw)?),
        },
        limit: match field(v, "Limit") {
            None | Some(Value::Null) => None,
            Some(raw) => Some(bounded_limit("limit", as_u64(raw, CTX, "Limit")?)?),
        },
        filter: opt_filter(v)?,
        with_payload: opt_with_payload(v)?,
        with_vector: with_vector_or_default(v)?,
        order_by: match field(v, "OrderBy") {
            None | Some(Value::Null) => None,
            Some(raw) => Some(parse_order_by(raw)?),
        },
    })
}

pub(crate) fn parse_retrieve_request(v: &Value) -> Result<edge::RetrieveRequest> {
    const CTX: &str = "retrieve request";
    let point_ids = opt_array(v, "PointIds", CTX)?
        .into_iter()
        .map(super::types::parse_point_id)
        .collect::<Result<Vec<_>>>()?;
    Ok(edge::RetrieveRequest {
        point_ids,
        with_payload: opt_with_payload(v)?,
        with_vector: opt_with_vector(v)?,
    })
}

pub(crate) fn parse_count_request(v: &Value) -> Result<edge::CountRequest> {
    const CTX: &str = "count request";
    Ok(edge::CountRequest {
        filter: opt_filter(v)?,
        exact: opt_bool(v, CTX, "Exact")?.unwrap_or(true),
    })
}

pub(crate) fn parse_facet_request(v: &Value) -> Result<edge::FacetRequest> {
    const CTX: &str = "facet request";
    let key = as_str(required(v, "Key", CTX)?, CTX, "Key")?;
    Ok(edge::FacetRequest {
        key: super::filter::parse_key(key, CTX)?,
        limit: bounded_limit("limit", opt_u64(v, CTX, "Limit")?.unwrap_or(10))?,
        filter: opt_filter(v)?,
        exact: opt_bool(v, CTX, "Exact")?.unwrap_or(false),
    })
}

pub(crate) fn parse_group_request(v: &Value) -> Result<edge::GroupRequest> {
    const CTX: &str = "group request";
    let group_by = as_str(required(v, "GroupBy", CTX)?, CTX, "GroupBy")?;
    Ok(edge::GroupRequest {
        query: parse_query_request(required(v, "Query", CTX)?)?,
        group_by: super::filter::parse_key(group_by, CTX)?,
        groups: bounded_limit(
            "groups",
            as_u64(required(v, "Groups", CTX)?, CTX, "Groups")?,
        )?,
        group_size: bounded_limit(
            "group_size",
            as_u64(required(v, "GroupSize", CTX)?, CTX, "GroupSize")?,
        )?,
    })
}

pub(crate) fn parse_matrix_request(v: &Value) -> Result<edge::SearchMatrixRequest> {
    const CTX: &str = "search matrix request";
    Ok(edge::SearchMatrixRequest {
        sample_size: bounded_limit(
            "sample_size",
            as_u64(required(v, "SampleSize", CTX)?, CTX, "SampleSize")?,
        )?,
        limit_per_sample: bounded_limit(
            "limit_per_sample",
            as_u64(required(v, "LimitPerSample", CTX)?, CTX, "LimitPerSample")?,
        )?,
        filter: opt_filter(v)?,
        using: opt_using(v, CTX)?,
    })
}

// ── Vector queries ──────────────────────────────────────────────────────────

fn parse_vector_query(v: &Value) -> Result<edge::QueryEnum> {
    const CTX: &str = "query";
    match kind(v, CTX)? {
        "nearest" => {
            let vector = parse_query_vector(required(v, "Vector", CTX)?)?;
            Ok(edge::QueryEnum::Nearest(edge::NamedQuery {
                query: vector,
                using: Some(opt_using(v, CTX)?),
            }))
        }
        "recommend" => {
            let positives = opt_array(v, "Positives", CTX)?
                .into_iter()
                .map(parse_query_vector)
                .collect::<Result<Vec<_>>>()?;
            if positives.is_empty() {
                return Err(ApiError::invalid_argument(
                    "recommend query requires at least one positive example",
                ));
            }
            let negatives = opt_array(v, "Negatives", CTX)?
                .into_iter()
                .map(parse_query_vector)
                .collect::<Result<Vec<_>>>()?;
            let strategy = match field(v, "Strategy") {
                None | Some(Value::Null) => RecommendStrategy::BestScore,
                Some(raw) => match as_u64(raw, CTX, "Strategy")? {
                    0 => RecommendStrategy::BestScore,
                    1 => RecommendStrategy::SumScores,
                    other => {
                        return Err(ApiError::invalid_argument(format!(
                            "unknown recommend strategy {other}"
                        )));
                    }
                },
            };
            let named = edge::NamedQuery {
                query: segment::vector_storage::query::RecoQuery {
                    positives,
                    negatives,
                },
                using: Some(opt_using(v, CTX)?),
            };
            Ok(match strategy {
                RecommendStrategy::BestScore => edge::QueryEnum::RecommendBestScore(named),
                RecommendStrategy::SumScores => edge::QueryEnum::RecommendSumScores(named),
            })
        }
        "discover" => {
            let target = parse_query_vector(required(v, "Target", CTX)?)?;
            let context = opt_array(v, "Context", CTX)?
                .into_iter()
                .map(parse_context_pair)
                .collect::<Result<Vec<_>>>()?;
            Ok(edge::QueryEnum::Discover(edge::NamedQuery {
                query: segment::vector_storage::query::DiscoverQuery::new(target, context),
                using: Some(opt_using(v, CTX)?),
            }))
        }
        "context" => {
            let context = opt_array(v, "Context", CTX)?
                .into_iter()
                .map(parse_context_pair)
                .collect::<Result<Vec<_>>>()?;
            Ok(edge::QueryEnum::Context(edge::NamedQuery {
                query: segment::vector_storage::query::ContextQuery::new(context),
                using: Some(opt_using(v, CTX)?),
            }))
        }
        "feedback" => {
            let target = parse_query_vector(required(v, "Target", CTX)?)?;
            let feedback = opt_array(v, "Feedback", CTX)?
                .into_iter()
                .map(|item| {
                    const ITEM: &str = "feedback item";
                    let vector =
                        parse_query_vector(required(item, "Vector", ITEM)?)?;
                    let score = finite(required(item, "Score", ITEM)?, "feedback score must be finite")?;
                    Ok(segment::vector_storage::query::FeedbackItem {
                        vector,
                        score: OrderedFloat(score),
                    })
                })
                .collect::<Result<Vec<_>>>()?;
            let coefficients = required(v, "Coefficients", CTX)?;
            let a = finite(
                required(coefficients, "A", "feedback coefficients")?,
                "feedback coefficient a must be finite",
            )?;
            let b = finite(
                required(coefficients, "B", "feedback coefficients")?,
                "feedback coefficient b must be finite",
            )?;
            let c = finite(
                required(coefficients, "C", "feedback coefficients")?,
                "feedback coefficient c must be finite",
            )?;
            Ok(edge::QueryEnum::FeedbackNaive(edge::NamedQuery {
                query: segment::vector_storage::query::NaiveFeedbackQuery {
                    target,
                    feedback,
                    coefficients:
                        segment::vector_storage::query::NaiveFeedbackCoefficients {
                            a: OrderedFloat(a),
                            b: OrderedFloat(b),
                            c: OrderedFloat(c),
                        },
                },
                using: Some(opt_using(v, CTX)?),
            }))
        }
        unknown => Err(ApiError::invalid_argument(format!(
            "{CTX} has unknown kind {unknown:?}"
        ))),
    }
}

/// Placeholder ordinals mapped explicitly onto the two engine
/// recommend variants. Declaration order matches (`BestScore = 0`,
/// `SumScores = 1`); the match stays explicit so a reordering fails loudly.
enum RecommendStrategy {
    BestScore,
    SumScores,
}

fn parse_context_pair(
    v: &Value,
) -> Result<segment::vector_storage::query::ContextPair<edge::VectorInternal>> {
    const CTX: &str = "context pair";
    Ok(segment::vector_storage::query::ContextPair {
        positive: parse_query_vector(required(v, "Positive", CTX)?)?,
        negative: parse_query_vector(required(v, "Negative", CTX)?)?,
    })
}

// ── Scoring queries ─────────────────────────────────────────────────────────

pub(crate) fn parse_scoring_query(v: &Value) -> Result<edge::ScoringQuery> {
    const CTX: &str = "scoring query";
    match kind(v, CTX)? {
        "vector" => {
            let query = parse_vector_query(required(v, "Query", CTX)?)?;
            Ok(edge::ScoringQuery::Vector(query))
        }
        "fusion" => {
            let fusion = parse_fusion(required(v, "Fusion", CTX)?)?;
            Ok(edge::ScoringQuery::Fusion(fusion))
        }
        "order_by" => {
            let order_by = parse_order_by_struct(required(v, "OrderBy", CTX)?)?;
            Ok(edge::ScoringQuery::OrderBy(order_by))
        }
        "formula" => {
            let expression = crate::formula::parse_expression(required(v, "Expression", CTX)?)?;
            let mut defaults = HashMap::new();
            match field(v, "Defaults") {
                None | Some(Value::Null) => {}
                Some(raw) => {
                    let entries = raw.as_object().ok_or_else(|| {
                        ApiError::invalid_argument(format!(
                            "{CTX} field \"Defaults\" must be an object"
                        ))
                    })?;
                    for (name, encoded) in entries {
                        let text = encoded.as_str().ok_or_else(|| {
                            ApiError::invalid_argument(format!(
                                "formula default {name:?} must be a JSON string"
                            ))
                        })?;
                        let value: Value = serde_json::from_str(text).map_err(|err| {
                            ApiError::invalid_argument(format!(
                                "formula default {name:?} is not valid JSON: {err}"
                            ))
                        })?;
                        defaults.insert(name.clone(), value);
                    }
                }
            }
            let formula = edge::Formula {
                formula: expression,
                defaults,
            };
            let parsed = segment::index::query_optimization::rescore_formula::parsed_formula::ParsedFormula::try_from(formula).map_err(|err| {
                ApiError::invalid_argument(format!("invalid formula: {err}"))
            })?;
            Ok(edge::ScoringQuery::Formula(parsed))
        }
        "mmr" => {
            let vector = parse_query_vector(required(v, "Vector", CTX)?)?;
            let raw_lambda = required(v, "Lambda", CTX)?;
            let number = raw_lambda.as_f64().ok_or_else(|| {
                ApiError::invalid_argument(format!("{CTX} field \"Lambda\" must be a number"))
            })?;
            if !number.is_finite() || !(0.0..=1.0).contains(&number) {
                return Err(ApiError::invalid_argument(format!(
                    "MMR lambda ({number}) must be within [0, 1]"
                )));
            }
            let candidates_limit = bounded_limit(
                "MMR candidates_limit",
                as_u64(required(v, "CandidatesLimit", CTX)?, CTX, "CandidatesLimit")?,
            )?;
            Ok(edge::ScoringQuery::Mmr(edge::Mmr {
                vector,
                using: opt_using(v, CTX)?,
                lambda: OrderedFloat(number as f32),
                candidates_limit,
            }))
        }
        "sample" => {
            let raw = as_u64(required(v, "Sample", CTX)?, CTX, "Sample")?;
            match raw {
                0 => Ok(edge::ScoringQuery::Sample(edge::Sample::Random)),
                _ => Err(ApiError::invalid_argument(format!(
                    "unknown sample strategy {raw}"
                ))),
            }
        }
        unknown => Err(ApiError::invalid_argument(format!(
            "{CTX} has unknown kind {unknown:?}"
        ))),
    }
}

fn parse_fusion(v: &Value) -> Result<edge::Fusion> {
    const CTX: &str = "fusion";
    match kind(v, CTX)? {
        "rrf" => {
            let k = clamp_usize(as_u64(required(v, "K", CTX)?, CTX, "K")?);
            let weights = match field(v, "Weights") {
                None | Some(Value::Null) => None,
                Some(raw) => {
                    let items = raw.as_array().ok_or_else(|| {
                        ApiError::invalid_argument(format!(
                            "{CTX} field \"Weights\" must be an array"
                        ))
                    })?;
                    let mut out = Vec::with_capacity(items.len());
                    for (pos, item) in items.iter().enumerate() {
                        let number = item.as_f64().ok_or_else(|| {
                            ApiError::invalid_argument(format!(
                                "{CTX} weight at index {pos} must be a number"
                            ))
                        })?;
                        if !number.is_finite()
                            || number > f64::from(f32::MAX)
                            || number < -f64::from(f32::MAX)
                        {
                            return Err(ApiError::invalid_argument(format!(
                                "RRF weight at index {pos} must be finite"
                            )));
                        }
                        out.push(OrderedFloat(number as f32));
                    }
                    Some(out)
                }
            };
            Ok(edge::Fusion::Rrf { k, weights })
        }
        "dbsf" => Ok(edge::Fusion::Dbsf),
        unknown => Err(ApiError::invalid_argument(format!(
            "{CTX} has unknown kind {unknown:?}"
        ))),
    }
}

// ── Order-by ────────────────────────────────────────────────────────────────

pub(crate) fn parse_order_by(v: &Value) -> Result<edge::OrderByInterface> {
    Ok(edge::OrderByInterface::Struct(parse_order_by_struct(v)?))
}

fn parse_order_by_struct(v: &Value) -> Result<edge::OrderBy> {
    const CTX: &str = "order-by";
    let key = as_str(required(v, "Key", CTX)?, CTX, "Key")?;
    Ok(edge::OrderBy {
        key: super::filter::parse_key(key, CTX)?,
        direction: parse_direction(v)?,
        start_from: parse_start_from(v)?,
    })
}

/// C# `Direction` ordinals mapped explicitly onto the engine directions.
/// Declaration order matches (`Asc = 0`, `Desc = 1`).
fn parse_direction(v: &Value) -> Result<Option<edge::Direction>> {
    const CTX: &str = "order-by";
    match field(v, "Direction") {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => match as_u64(raw, CTX, "Direction")? {
            0 => Ok(Some(edge::Direction::Asc)),
            1 => Ok(Some(edge::Direction::Desc)),
            other => Err(ApiError::invalid_argument(format!(
                "unknown order-by direction {other}"
            ))),
        },
    }
}

fn parse_start_from(v: &Value) -> Result<Option<edge::StartFrom>> {
    match field(v, "StartFrom") {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => {
            const CTX: &str = "order-by start_from";
            match kind(raw, CTX)? {
                "integer" => {
                    let value = required(raw, "Value", CTX)?;
                    let number = value.as_i64().ok_or_else(|| {
                        ApiError::invalid_argument(format!(
                            "{CTX} field \"Value\" must be an integer"
                        ))
                    })?;
                    Ok(Some(edge::StartFrom::Integer(number)))
                }
                "float" => {
                    let value = required(raw, "Value", CTX)?;
                    let number = value.as_f64().ok_or_else(|| {
                        ApiError::invalid_argument(format!(
                            "{CTX} field \"Value\" must be a number"
                        ))
                    })?;
                    if !number.is_finite() {
                        return Err(ApiError::invalid_argument(format!(
                            "order-by start_from float ({number}) must be finite"
                        )));
                    }
                    Ok(Some(edge::StartFrom::Float(number)))
                }
                "datetime" => {
                    let text = as_str(required(raw, "Value", CTX)?, CTX, "Value")?;
                    let moment = text.parse().map_err(|err| {
                        ApiError::invalid_argument(format!(
                            "invalid order-by start_from datetime ({text:?}): {err}"
                        ))
                    })?;
                    Ok(Some(edge::StartFrom::Datetime(moment)))
                }
                unknown => Err(ApiError::invalid_argument(format!(
                    "{CTX} has unknown kind {unknown:?}"
                ))),
            }
        }
    }
}

// ── Shared optionals ────────────────────────────────────────────────────────

fn parse_search_params(v: &Value) -> Result<edge::SearchParams> {
    const CTX: &str = "search params";
    Ok(edge::SearchParams {
        hnsw_ef: opt_u64(v, CTX, "HnswEf")?.map(clamp_usize),
        exact: opt_bool(v, CTX, "Exact")?.unwrap_or(false),
        quantization: None,
        indexed_only: opt_bool(v, CTX, "IndexedOnly")?.unwrap_or(false),
        acorn: None,
        idf: None,
    })
}

fn opt_filter(v: &Value) -> Result<Option<edge::Filter>> {
    match field(v, "Filter") {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => Ok(Some(super::filter::parse_filter(raw)?)),
    }
}

fn opt_params(v: &Value) -> Result<Option<edge::SearchParams>> {
    match field(v, "Params") {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => Ok(Some(parse_search_params(raw)?)),
    }
}

fn opt_scoring_query(v: &Value) -> Result<Option<edge::ScoringQuery>> {
    match field(v, "Query") {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => Ok(Some(parse_scoring_query(raw)?)),
    }
}

fn opt_with_vector(v: &Value) -> Result<Option<edge::WithVector>> {
    match field(v, "WithVector") {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => Ok(Some(super::types::parse_with_vector(raw)?)),
    }
}

fn with_vector_or_default(v: &Value) -> Result<edge::WithVector> {
    match field(v, "WithVector") {
        None => Ok(edge::WithVector::default()),
        Some(raw) => super::types::parse_with_vector(raw),
    }
}

fn opt_with_payload(v: &Value) -> Result<Option<edge::WithPayloadInterface>> {
    match field(v, "WithPayload") {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => super::types::parse_with_payload(raw),
    }
}

fn with_payload_or_default(v: &Value) -> Result<edge::WithPayloadInterface> {
    match field(v, "WithPayload") {
        None => Ok(edge::WithPayloadInterface::default()),
        Some(raw) => Ok(super::types::parse_with_payload(raw)?.unwrap_or_default()),
    }
}

/// Parse a C# `NamedVector` into the engine query vector. The shared parser
/// yields the persisted form; the uniform-shape conversion into the query
/// form is infallible here because the parser already enforced the 1..=65536
/// length bounds and non-empty, uniform, finite vectors.
fn parse_query_vector(v: &Value) -> Result<edge::VectorInternal> {
    Ok(super::types::parse_named_vector(v)?.into())
}

/// Vector field name; absent and null target the default (unnamed) field,
/// mirroring the FFI `using` default.
fn opt_using(v: &Value, ctx: &str) -> Result<String> {
    match opt_str(v, ctx, "Using")? {
        Some(name) => Ok(name.to_owned()),
        None => Ok(edge::DEFAULT_VECTOR_NAME.to_owned()),
    }
}

/// Array field; absent and null read as empty.
fn opt_array<'a>(v: &'a Value, key: &str, ctx: &str) -> Result<Vec<&'a Value>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(Vec::new()),
        Some(raw) => raw
            .as_array()
            .ok_or_else(|| {
                ApiError::invalid_argument(format!("{ctx} field {key:?} must be an array"))
            })
            .map(|items| items.iter().collect()),
    }
}

fn opt_f32(v: &Value, ctx: &str, key: &str) -> Result<Option<f32>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => Ok(Some(as_f32(raw, ctx, key)?)),
    }
}

/// Read a JSON number as a finite `f32` with a fixed validation message.
fn finite(raw: &Value, message: &str) -> Result<f32> {
    match raw.as_f64() {
        Some(value)
            if value.is_finite()
                && value <= f64::from(f32::MAX)
                && value >= -f64::from(f32::MAX) =>
        {
            Ok(value as f32)
        }
        _ => Err(ApiError::invalid_argument(message.to_owned())),
    }
}

// ── Responses ───────────────────────────────────────────────────────────────

pub(crate) fn scored_list_to_json(points: Vec<edge::ScoredPoint>) -> Value {
    Value::Array(
        points
            .iter()
            .map(super::types::scored_point_to_json)
            .collect(),
    )
}

pub(crate) fn batch_to_json(batches: Vec<Vec<edge::ScoredPoint>>) -> Value {
    Value::Array(
        batches
            .iter()
            .map(|row| {
                Value::Array(row.iter().map(super::types::scored_point_to_json).collect())
            })
            .collect(),
    )
}

pub(crate) fn groups_to_json(groups: Vec<edge::Group>) -> Value {
    Value::Array(
        groups
            .iter()
            .map(|group| {
                json!({
                    "Key": super::types::group_id_to_json(&group.key),
                    "Hits": Value::Array(
                        group.hits.iter().map(super::types::scored_point_to_json).collect::<Vec<_>>()
                    ),
                })
            })
            .collect(),
    )
}

pub(crate) fn scroll_to_json(records: Vec<edge::Record>, next: Option<edge::PointId>) -> Value {
    json!({
        "Records": Value::Array(
            records.iter().map(super::types::record_to_json).collect::<Vec<_>>()
        ),
        "NextOffset": next.as_ref().map(super::types::point_id_to_json).unwrap_or(Value::Null),
    })
}

pub(crate) fn retrieve_to_json(records: Vec<edge::Record>) -> Value {
    Value::Array(
        records
            .iter()
            .map(super::types::record_to_json)
            .collect(),
    )
}

/// Facet hits as `{"Hits": [{Value, Count}]}` with engine-canonical
/// stringified values, mirroring the FFI facet conversion. The object shape
/// (not a bare array) matches the C# `FacetResponse` record: a collection
/// interface would leave System.Text.Json with no constructible path.
pub(crate) fn facet_to_json(response: edge::FacetResponse) -> Value {
    json!({
        "Hits": Value::Array(
            response
                .hits
                .iter()
                .map(|hit| {
                    let value = match &hit.value {
                        edge::FacetValue::Keyword(text) => text.clone(),
                        edge::FacetValue::Int(number) => number.to_string(),
                        edge::FacetValue::Uuid(id) => uuid::Uuid::from_u128(*id).to_string(),
                        edge::FacetValue::Bool(flag) => flag.to_string(),
                    };
                    json!({
                        "Value": value,
                        "Count": hit.count as u64,
                    })
                })
                .collect::<Vec<_>>(),
        ),
    })
}

pub(crate) fn matrix_to_json(response: edge::SearchMatrixResponse) -> Value {
    json!({
        "SampleIds": Value::Array(
            response.sample_ids.iter().map(super::types::point_id_to_json).collect::<Vec<_>>()
        ),
        "Nearests": Value::Array(
            response.nearests.iter().map(|row| {
                Value::Array(row.iter().map(super::types::scored_point_to_json).collect::<Vec<_>>())
            }).collect::<Vec<_>>()
        ),
    })
}

pub(crate) fn shard_info_to_json(info: edge::ShardInfo) -> Value {
    let schema: serde_json::Map<String, Value> = info
        .payload_schema
        .iter()
        .map(|(name, index)| (name.to_string(), super::types::payload_info_to_json(index)))
        .collect();
    json!({
        "SegmentsCount": info.segments_count as u64,
        "PointsCount": info.points_count as u64,
        "IndexedVectorsCount": info.indexed_vectors_count as u64,
        "PayloadSchema": Value::Object(schema),
    })
}

#[cfg(test)]
mod tests {
    use serde_json::json;

    use super::*;

    fn parse(raw: &str) -> Value {
        serde_json::from_str(raw).expect("test JSON is valid")
    }

    #[test]
    fn parse_search_request_shape() {
        let raw = r#"{"Query":{"kind":"nearest","Vector":{"kind":"dense","Values":[1.0,2.0]},"Using":null},"Limit":10,"Offset":3,"Filter":null,"Params":{"HnswEf":16,"Exact":true,"IndexedOnly":true},"WithVector":null,"WithPayload":null,"ScoreThreshold":0.5}"#;
        let req = parse_search_request(&parse(raw)).expect("search parses");
        assert_eq!((req.limit, req.offset), (10, 3));
        assert!(req.filter.is_none());
        let params = req.params.expect("params set");
        assert_eq!((params.hnsw_ef, params.exact, params.indexed_only), (Some(16), true, true));
        assert!(req.with_vector.is_none() && req.with_payload.is_none());
        assert_eq!(req.score_threshold, Some(0.5));
        assert!(matches!(req.query, edge::QueryEnum::Nearest(_)));
    }

    #[test]
    fn parse_query_request_prefetch_and_defaults() {
        let raw = r#"{"Limit":5,"Offset":1,"Query":{"kind":"sample","Sample":0},"Prefetches":[{"Limit":7,"Query":{"kind":"sample","Sample":0},"Prefetches":[],"Filter":null,"ScoreThreshold":null,"Params":null}],"WithVector":null,"WithPayload":null,"Filter":null,"ScoreThreshold":null,"Params":null}"#;
        let req = parse_query_request(&parse(raw)).expect("query parses");
        assert_eq!((req.limit, req.offset), (5, 1));
        assert_eq!(req.prefetches.len(), 1);
        assert_eq!(req.prefetches[0].limit, 7);
        assert!(matches!(
            req.query,
            Some(edge::ScoringQuery::Sample(edge::Sample::Random))
        ));
    }

    #[test]
    fn parse_scoring_query_shapes() {
        let rrf = parse(r#"{"kind":"fusion","Fusion":{"kind":"rrf","K":60,"Weights":[1.0,2.0]}}"#);
        assert!(matches!(
            parse_scoring_query(&rrf).expect("rrf parses"),
            edge::ScoringQuery::Fusion(edge::Fusion::Rrf { .. })
        ));
        let order = parse(
            r#"{"kind":"order_by","OrderBy":{"Key":"price","Direction":1,"StartFrom":{"kind":"integer","Value":5}}}"#,
        );
        match parse_scoring_query(&order).expect("order-by parses") {
            edge::ScoringQuery::OrderBy(order) => {
                assert_eq!(order.direction, Some(edge::Direction::Desc));
                assert!(matches!(order.start_from, Some(edge::StartFrom::Integer(5))));
            }
            other => panic!("unexpected {other:?}"),
        }
        let mmr = parse(
            r#"{"kind":"mmr","Vector":{"kind":"dense","Values":[1.0]},"Lambda":0.5,"CandidatesLimit":10,"Using":"v"}"#,
        );
        match parse_scoring_query(&mmr).expect("mmr parses") {
            edge::ScoringQuery::Mmr(mmr) => {
                assert_eq!((mmr.using.as_str(), mmr.candidates_limit), ("v", 10));
            }
            other => panic!("unexpected {other:?}"),
        }
        let bad_lambda = parse(
            r#"{"kind":"mmr","Vector":{"kind":"dense","Values":[1.0]},"Lambda":1.5,"CandidatesLimit":10,"Using":null}"#,
        );
        assert!(
            parse_scoring_query(&bad_lambda)
                .unwrap_err()
                .message
                .contains("MMR lambda")
        );
        let recommend = parse(
            r#"{"kind":"vector","Query":{"kind":"recommend","Positives":[{"kind":"dense","Values":[1.0]}],"Negatives":[],"Strategy":null,"Using":null}}"#,
        );
        assert!(matches!(
            parse_scoring_query(&recommend).expect("recommend parses"),
            edge::ScoringQuery::Vector(edge::QueryEnum::RecommendBestScore(_))
        ));
        let empty = parse(
            r#"{"kind":"vector","Query":{"kind":"recommend","Positives":[],"Negatives":[],"Strategy":null,"Using":null}}"#,
        );
        assert!(parse_scoring_query(&empty).is_err());
        let formula = parse(
            r#"{"kind":"formula","Expression":{"kind":"variable","Name":"$score","Size":1,"Depth":1},"Defaults":{"x":"1"}}"#,
        );
        assert!(matches!(
            parse_scoring_query(&formula).expect("formula parses"),
            edge::ScoringQuery::Formula(_)
        ));
    }

    #[test]
    fn parse_prefetch_depth_guard() {
        let mut inner = json!({"Limit":1,"Query":null,"Prefetches":[],"Filter":null,"ScoreThreshold":null,"Params":null});
        for _ in 0..70 {
            inner =
                json!({"Limit":1,"Query":null,"Prefetches":[inner],"Filter":null,"ScoreThreshold":null,"Params":null});
        }
        assert!(parse_prefetch(&inner, 0).is_err());
    }

    #[test]
    fn parse_limit_guard() {
        let mut request = parse(
            r#"{"Limit":5,"Offset":0,"Query":null,"Prefetches":[],"WithVector":null,"WithPayload":null,"Filter":null,"ScoreThreshold":null,"Params":null}"#,
        );
        request["Limit"] = json!(u64::MAX);
        assert!(
            parse_query_request(&request)
                .unwrap_err()
                .message
                .contains("exceeds the maximum")
        );
    }

    #[test]
    fn parse_scroll_retrieve_count_facet_group_matrix() {
        let scroll = parse(
            r#"{"Offset":{"kind":"num","Value":42},"Limit":10,"Filter":null,"WithPayload":null,"WithVector":null,"OrderBy":{"Key":"k","Direction":null,"StartFrom":null}}"#,
        );
        let req = parse_scroll_request(&scroll).expect("scroll parses");
        assert_eq!(req.offset, Some(edge::PointId::NumId(42)));
        assert_eq!(req.limit, Some(10));
        assert!(matches!(req.order_by, Some(edge::OrderByInterface::Struct(_))));

        let retrieve = parse(
            r#"{"PointIds":[{"kind":"num","Value":1}],"WithPayload":null,"WithVector":null}"#,
        );
        assert_eq!(parse_retrieve_request(&retrieve).expect("retrieve parses").point_ids.len(), 1);

        let count = parse(r#"{"Filter":null}"#);
        assert!(parse_count_request(&count).expect("count parses").exact);

        let facet = parse(r#"{"Key":"color","Limit":5,"Exact":true,"Filter":null}"#);
        let req = parse_facet_request(&facet).expect("facet parses");
        assert_eq!((req.limit, req.exact), (5, true));

        let group = parse(
            r#"{"Query":{"Limit":5,"Offset":null,"Query":null,"Prefetches":[],"WithVector":null,"WithPayload":null,"Filter":null,"ScoreThreshold":null,"Params":null},"GroupBy":"g","Groups":2,"GroupSize":3}"#,
        );
        let req = parse_group_request(&group).expect("group parses");
        assert_eq!((req.groups, req.group_size), (2, 3));

        let matrix = parse(r#"{"SampleSize":4,"LimitPerSample":2,"Filter":null,"Using":"img"}"#);
        assert_eq!(parse_matrix_request(&matrix).expect("matrix parses").using, "img");
    }

    #[test]
    fn response_shapes_match_csharp() {
        let hit = edge::ScoredPoint {
            id: edge::PointId::NumId(7),
            version: 3,
            score: 0.9,
            payload: None,
            vector: None,
            shard_key: None,
            order_value: None,
        };
        let list = scored_list_to_json(vec![hit.clone()]);
        assert_eq!(list[0]["Id"], json!({"kind":"num","Value":7}));
        assert_eq!(list[0]["Payload"], Value::Null);

        let groups = groups_to_json(vec![edge::Group {
            hits: vec![hit],
            key: segment::data_types::groups::GroupId::String("a".to_owned()),
        }]);
        assert_eq!(groups[0]["Key"], json!({"kind":"string","Value":"a"}));

        let scroll = scroll_to_json(vec![], Some(edge::PointId::NumId(9)));
        assert_eq!(scroll, json!({"Records":[],"NextOffset":{"kind":"num","Value":9}}));

        let facet = facet_to_json(edge::FacetResponse {
            hits: vec![edge::FacetHit {
                value: edge::FacetValue::Keyword("red".to_owned()),
                count: 3,
            }],
        });
        assert_eq!(facet, json!({"Hits":[{"Value":"red","Count":3}]}));

        let matrix = matrix_to_json(edge::SearchMatrixResponse {
            sample_ids: vec![edge::PointId::NumId(1)],
            nearests: vec![vec![]],
        });
        assert_eq!(matrix["SampleIds"], json!([{"kind":"num","Value":1}]));

        let info = shard_info_to_json(edge::ShardInfo {
            segments_count: 1,
            points_count: 2,
            indexed_vectors_count: 3,
            payload_schema: [(
                "f".parse().expect("key parses"),
                edge::PayloadIndexInfo {
                    data_type: edge::PayloadSchemaType::Keyword,
                    params: None,
                    points: 4,
                },
            )]
            .into_iter()
            .collect(),
        });
        assert_eq!(
            info,
            json!({"SegmentsCount":1,"PointsCount":2,"IndexedVectorsCount":3,"PayloadSchema":{"f":{"DataType":0,"Params":null,"Points":4}}})
        );
    }
}
