use std::num::NonZeroU32;

use segment::json_path::JsonPath;
use segment::types::{
    AnyVariants as SegmentAnyVariants, Condition as SegmentCondition,
    DateTimePayloadType as SegmentDateTime, FieldCondition as SegmentFieldCondition,
    Filter as SegmentFilter, GeoBoundingBox as SegmentGeoBoundingBox,
    GeoLineString as SegmentGeoLineString, GeoPoint as SegmentGeoPoint,
    GeoPolygon as SegmentGeoPolygon, GeoRadius as SegmentGeoRadius,
    HasVectorCondition as SegmentHasVector,
    IsEmptyCondition as SegmentIsEmpty, IsNullCondition as SegmentIsNull,
    Match as SegmentMatch, MatchAny as SegmentMatchAny, MatchExcept as SegmentMatchExcept,
    MatchPhrase as SegmentMatchPhrase, MatchPrefix as SegmentMatchPrefix,
    MatchSubstring as SegmentMatchSubstring, MatchText as SegmentMatchText,
    MatchTextAny as SegmentMatchTextAny, MatchValue as SegmentMatchValue,
    MinShould as SegmentMinShould, Nested as SegmentNested,
    NestedCondition as SegmentNestedCondition, PayloadField as SegmentPayloadField,
    PointIdType as SegmentPointId, Range as SegmentRange, RangeInterface as SegmentRangeInterface,
    Slice as SegmentSlice, SliceCondition as SegmentSliceCondition,
    ValuesCount as SegmentValuesCount, ValueVariants as SegmentValueVariants,
};
use serde_json::Value;

use crate::error::{ApiError, Result, check_nesting_depth, clamp_usize};
use crate::json::{as_bool, as_str, as_u64, field, kind, opt_str, opt_u64, required};

/// Parse a payload key into the engine's JSON-path type.
///
/// Replicates the acceptance and error message of the FFI boundary's
/// `parse_json_path`: the path grammar lives in the engine, whose error
/// carries no detail, so the message names the rejected key instead.
pub(crate) fn parse_key(s: &str, ctx: &str) -> Result<JsonPath> {
    let _ = ctx;
    s.parse().map_err(|_: ()| {
        ApiError::invalid_argument(format!("invalid payload key {s:?}: not a valid JSON path"))
    })
}

/// Parse a C# `Filter` JSON document into the engine filter.
///
/// Starts the nesting check at depth 0, exactly like the FFI conversion:
/// each `nested` / `filter` condition adds one level.
pub(crate) fn parse_filter(v: &Value) -> Result<SegmentFilter> {
    parse_filter_at(v, 0)
}

/// Parse one C# `Condition` (lowercase `"kind"` discriminator) at nesting
/// `depth`. Only `nested` and `filter` recurse, each bumping the depth by
/// one for the inner filter — mirroring the FFI conversion.
pub(crate) fn parse_condition(v: &Value, depth: u32) -> Result<SegmentCondition> {
    const CTX: &str = "filter condition";
    match kind(v, CTX)? {
        "field" => Ok(SegmentCondition::Field(parse_field_condition(
            required(v, "Condition", CTX)?,
        )?)),
        "is_empty" => {
            let key = parse_key(as_str(required(v, "Key", CTX)?, CTX, "Key")?, CTX)?;
            Ok(SegmentCondition::IsEmpty(SegmentIsEmpty {
                is_empty: SegmentPayloadField { key },
            }))
        }
        "is_null" => {
            let key = parse_key(as_str(required(v, "Key", CTX)?, CTX, "Key")?, CTX)?;
            Ok(SegmentCondition::IsNull(SegmentIsNull {
                is_null: SegmentPayloadField { key },
            }))
        }
        "has_id" => {
            let items = required_array(v, "Ids", CTX)?;
            let ids = items
                .iter()
                .map(parse_point_id)
                .collect::<Result<Vec<_>>>()?;
            Ok(SegmentCondition::HasId(ids.into_iter().collect()))
        }
        "has_vector" => {
            let name = as_str(required(v, "VectorName", CTX)?, CTX, "VectorName")?;
            Ok(SegmentCondition::HasVector(SegmentHasVector {
                has_vector: name.to_owned(),
            }))
        }
        "slice" => {
            // Mirrors the engine's `validate_slice_condition`, which only runs
            // on the serde path: `total` must be non-zero and `index < total`.
            let total = as_u32(required(v, "Total", CTX)?, CTX, "Total")?;
            let index = as_u32(required(v, "Index", CTX)?, CTX, "Index")?;
            let total = NonZeroU32::new(total).ok_or_else(|| {
                ApiError::invalid_argument("slice condition: total must be non-zero")
            })?;
            if index >= total.get() {
                return Err(ApiError::invalid_argument(format!(
                    "slice condition: index ({index}) must be less than total ({total})"
                )));
            }
            Ok(SegmentCondition::Slice(SegmentSliceCondition {
                slice: SegmentSlice { total, index },
            }))
        }
        "nested" => {
            let key = parse_key(as_str(required(v, "Key", CTX)?, CTX, "Key")?, CTX)?;
            let filter = parse_filter_at(required(v, "Filter", CTX)?, depth + 1)?;
            Ok(SegmentCondition::Nested(SegmentNestedCondition::new(
                SegmentNested { key, filter },
            )))
        }
        "filter" => {
            let filter = parse_filter_at(required(v, "Filter", CTX)?, depth + 1)?;
            Ok(SegmentCondition::Filter(filter))
        }
        other => Err(ApiError::invalid_argument(format!(
            "{CTX}: unknown condition kind {other:?}"
        ))),
    }
}

/// Parse a `Filter` at nesting `depth`, rejecting trees deeper than the
/// shared cap before the recursion can overflow the stack. Conditions convert
/// at the same depth; only `nested` / `filter` bump it by one.
fn parse_filter_at(v: &Value, depth: u32) -> Result<SegmentFilter> {
    const CTX: &str = "filter";
    check_nesting_depth("filter", depth)?;
    if v.as_object().is_none() {
        return Err(ApiError::invalid_argument(format!("{CTX} must be an object")));
    }
    Ok(SegmentFilter {
        must: opt_conditions(v, "Must", depth)?,
        should: opt_conditions(v, "Should", depth)?,
        must_not: opt_conditions(v, "MustNot", depth)?,
        min_should: opt_min_should(v, depth)?,
    })
}

/// Parse one optional condition list. Absent and explicit-null mean unset;
/// a present value must be an array.
fn opt_conditions(v: &Value, key: &str, depth: u32) -> Result<Option<Vec<SegmentCondition>>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(None),
        Some(items) => {
            let list = items.as_array().ok_or_else(|| {
                ApiError::invalid_argument(format!(
                    "filter field {key:?} must be an array of conditions"
                ))
            })?;
            list.iter()
                .map(|item| parse_condition(item, depth))
                .collect::<Result<Vec<_>>>()
                .map(Some)
        }
    }
}

/// Parse the optional `MinShould` clause: required `Conditions` plus a
/// required `MinCount` clamped to `usize` without truncation.
fn opt_min_should(v: &Value, depth: u32) -> Result<Option<SegmentMinShould>> {
    match field(v, "MinShould") {
        None | Some(Value::Null) => Ok(None),
        Some(inner) => {
            const CTX: &str = "min should";
            let items = required_array(inner, "Conditions", CTX)?;
            let conditions = items
                .iter()
                .map(|item| parse_condition(item, depth))
                .collect::<Result<Vec<_>>>()?;
            let min_count = as_u64(required(inner, "MinCount", CTX)?, CTX, "MinCount")?;
            Ok(Some(SegmentMinShould {
                conditions,
                min_count: clamp_usize(min_count),
            }))
        }
    }
}

/// Parse a `FieldCondition`: required `Key` plus exactly one predicate.
/// `Range` and `DatetimeRange` share the engine's single range slot, so they
/// count as one predicate; setting both is rejected separately.
fn parse_field_condition(v: &Value) -> Result<SegmentFieldCondition> {
    const CTX: &str = "field condition";
    let key = parse_key(as_str(required(v, "Key", CTX)?, CTX, "Key")?, CTX)?;
    let match_pred = match field(v, "Match") {
        None | Some(Value::Null) => None,
        Some(inner) => Some(parse_match(inner)?),
    };
    let range_float = match field(v, "Range") {
        None | Some(Value::Null) => None,
        Some(inner) => Some(parse_float_range(inner)?),
    };
    let range_datetime = match field(v, "DatetimeRange") {
        None | Some(Value::Null) => None,
        Some(inner) => Some(parse_datetime_range(inner)?),
    };
    let geo_bounding_box = match field(v, "GeoBoundingBox") {
        None | Some(Value::Null) => None,
        Some(inner) => Some(parse_geo_bounding_box(inner)?),
    };
    let geo_radius = match field(v, "GeoRadius") {
        None | Some(Value::Null) => None,
        Some(inner) => Some(parse_geo_radius(inner)?),
    };
    let geo_polygon = match field(v, "GeoPolygon") {
        None | Some(Value::Null) => None,
        Some(inner) => Some(parse_geo_polygon(inner)?),
    };
    let values_count = match field(v, "ValuesCount") {
        None | Some(Value::Null) => None,
        Some(inner) => Some(parse_values_count(inner)?),
    };

    // Exactly one predicate per field condition. The engine has no
    // well-defined semantics for several predicates in one condition — it
    // evaluates only one, and which one depends on the field's indexes — so
    // passing several through would silently diverge from a Qdrant server.
    // None set matches every point (a silent no-op); reject both and AND
    // predicates via separate `must` conditions instead.
    let predicate_count = [
        match_pred.is_some(),
        range_float.is_some() || range_datetime.is_some(),
        geo_bounding_box.is_some(),
        geo_radius.is_some(),
        geo_polygon.is_some(),
        values_count.is_some(),
    ]
    .into_iter()
    .filter(|&set| set)
    .count();
    if predicate_count == 0 {
        return Err(ApiError::invalid_argument(
            "field condition has no predicate set: specify exactly one of \
             match, range, datetime_range, geo_bounding_box, geo_radius, geo_polygon, \
             or values_count",
        ));
    }
    if predicate_count > 1 {
        return Err(ApiError::invalid_argument(
            "field condition has more than one predicate set: a field condition tests \
             exactly one predicate. To AND several predicates on the same key, add one \
             field condition per predicate to the filter's `must` clause",
        ));
    }

    // The engine's `range` slot holds one interface, float or datetime.
    let range = match (range_float, range_datetime) {
        (Some(_), Some(_)) => {
            return Err(ApiError::invalid_argument(
                "field condition: set either `range` or `datetime_range`, not both",
            ));
        }
        (Some(range), None) | (None, Some(range)) => Some(range),
        (None, None) => None,
    };

    Ok(SegmentFieldCondition {
        key,
        r#match: match_pred,
        range,
        geo_bounding_box,
        geo_radius,
        geo_polygon,
        values_count,
        is_empty: None,
        is_null: None,
    })
}

/// Parse a `Match` predicate: single enum with
/// value/text/text_any/phrase/prefix/substring/any/except.
fn parse_match(v: &Value) -> Result<SegmentMatch> {
    const CTX: &str = "match";
    match kind(v, CTX)? {
        "value" => Ok(SegmentMatch::Value(SegmentMatchValue {
            value: parse_value_variant(required(v, "Value", CTX)?)?,
        })),
        "text" => Ok(SegmentMatch::Text(SegmentMatchText {
            text: as_str(required(v, "Text", CTX)?, CTX, "Text")?.to_owned(),
        })),
        "text_any" => Ok(SegmentMatch::TextAny(SegmentMatchTextAny {
            text_any: as_str(required(v, "TextAny", CTX)?, CTX, "TextAny")?.to_owned(),
        })),
        "phrase" => Ok(SegmentMatch::Phrase(SegmentMatchPhrase {
            phrase: as_str(required(v, "Phrase", CTX)?, CTX, "Phrase")?.to_owned(),
        })),
        "prefix" => Ok(SegmentMatch::Prefix(SegmentMatchPrefix {
            prefix: as_str(required(v, "Prefix", CTX)?, CTX, "Prefix")?.to_owned(),
        })),
        "substring" => Ok(SegmentMatch::Substring(SegmentMatchSubstring {
            substring: as_str(required(v, "Substring", CTX)?, CTX, "Substring")?.to_owned(),
        })),
        "any" => Ok(SegmentMatch::Any(SegmentMatchAny {
            any: parse_any_variants(required(v, "Any", CTX)?)?,
        })),
        "except" => Ok(SegmentMatch::Except(SegmentMatchExcept {
            except: parse_any_variants(required(v, "Except", CTX)?)?,
        })),
        other => Err(ApiError::invalid_argument(format!(
            "{CTX}: unknown match kind {other:?}"
        ))),
    }
}

/// Parse a scalar match value: string, 64-bit integer, or boolean.
fn parse_value_variant(v: &Value) -> Result<SegmentValueVariants> {
    const CTX: &str = "match value";
    match kind(v, CTX)? {
        "string" => Ok(SegmentValueVariants::String(
            as_str(required(v, "Value", CTX)?, CTX, "Value")?.to_owned(),
        )),
        "integer" => Ok(SegmentValueVariants::Integer(as_i64(
            required(v, "Value", CTX)?,
            CTX,
            "Value",
        )?)),
        "bool" => Ok(SegmentValueVariants::Bool(as_bool(
            required(v, "Value", CTX)?,
            CTX,
            "Value",
        )?)),
        other => Err(ApiError::invalid_argument(format!(
            "{CTX}: unknown value kind {other:?}"
        ))),
    }
}

/// Parse the homogeneous value set behind `any` / `except`: strings or
/// integers. An empty set is valid: `any` then matches nothing and `except`
/// matches everything.
fn parse_any_variants(v: &Value) -> Result<SegmentAnyVariants> {
    const CTX: &str = "match any";
    match kind(v, CTX)? {
        "strings" => {
            let items = required_array(v, "Values", CTX)?;
            let values = items
                .iter()
                .map(|item| Ok(as_str(item, CTX, "Values")?.to_owned()))
                .collect::<Result<Vec<_>>>()?;
            Ok(SegmentAnyVariants::Strings(values.into_iter().collect()))
        }
        "integers" => {
            let items = required_array(v, "Values", CTX)?;
            let values = items
                .iter()
                .map(|item| as_i64(item, CTX, "Values"))
                .collect::<Result<Vec<_>>>()?;
            Ok(SegmentAnyVariants::Integers(values.into_iter().collect()))
        }
        other => Err(ApiError::invalid_argument(format!(
            "{CTX}: unknown any kind {other:?}"
        ))),
    }
}

/// Parse a float range into the engine's float range slot. Any combination of
/// bounds may be set; absent and explicit-null bounds are unbounded.
fn parse_float_range(v: &Value) -> Result<SegmentRangeInterface> {
    const CTX: &str = "range";
    require_object(v, CTX)?;
    Ok(SegmentRangeInterface::Float(SegmentRange {
        gte: opt_f64(v, CTX, "Gte")?.map(Into::into),
        gt: opt_f64(v, CTX, "Gt")?.map(Into::into),
        lte: opt_f64(v, CTX, "Lte")?.map(Into::into),
        lt: opt_f64(v, CTX, "Lt")?.map(Into::into),
    }))
}

/// Parse a datetime range (RFC 3339 bounds) into the engine's datetime range
/// slot. Absent and explicit-null bounds are unbounded.
fn parse_datetime_range(v: &Value) -> Result<SegmentRangeInterface> {
    const CTX: &str = "datetime range";
    require_object(v, CTX)?;
    Ok(SegmentRangeInterface::DateTime(SegmentRange {
        gte: parse_datetime_bound("gte", opt_str(v, CTX, "Gte")?)?,
        gt: parse_datetime_bound("gt", opt_str(v, CTX, "Gt")?)?,
        lte: parse_datetime_bound("lte", opt_str(v, CTX, "Lte")?)?,
        lt: parse_datetime_bound("lt", opt_str(v, CTX, "Lt")?)?,
    }))
}

/// Parse one datetime range bound, naming the bound in the error.
fn parse_datetime_bound(
    bound: &str,
    value: Option<&str>,
) -> Result<Option<SegmentDateTime>> {
    value
        .map(|raw| {
            raw.parse::<SegmentDateTime>().map_err(|e| {
                ApiError::invalid_argument(format!(
                    "invalid datetime range bound {bound} ({raw:?}): {e}"
                ))
            })
        })
        .transpose()
}

/// Parse a values-count range, clamping each bound to `usize` without
/// truncation. Absent and explicit-null bounds are unbounded.
fn parse_values_count(v: &Value) -> Result<SegmentValuesCount> {
    const CTX: &str = "values count";
    require_object(v, CTX)?;
    Ok(SegmentValuesCount {
        lt: opt_u64(v, CTX, "Lt")?.map(clamp_usize),
        gt: opt_u64(v, CTX, "Gt")?.map(clamp_usize),
        gte: opt_u64(v, CTX, "Gte")?.map(clamp_usize),
        lte: opt_u64(v, CTX, "Lte")?.map(clamp_usize),
    })
}

/// Parse a geographic point. Range validation stays with the engine's
/// `GeoPoint::new`, whose error is wrapped with the rejected coordinates.
fn parse_geo_point(v: &Value) -> Result<SegmentGeoPoint> {
    const CTX: &str = "geo point";
    let lon = as_f64(required(v, "Lon", CTX)?, CTX, "Lon")?;
    let lat = as_f64(required(v, "Lat", CTX)?, CTX, "Lat")?;
    SegmentGeoPoint::new(lon, lat).map_err(|e| {
        ApiError::invalid_argument(format!("invalid geo point (lon={lon}, lat={lat}): {e}"))
    })
}

/// Parse a geographic bounding box from its two corner points.
fn parse_geo_bounding_box(v: &Value) -> Result<SegmentGeoBoundingBox> {
    const CTX: &str = "geo bounding box";
    Ok(SegmentGeoBoundingBox {
        top_left: parse_geo_point(required(v, "TopLeft", CTX)?)?,
        bottom_right: parse_geo_point(required(v, "BottomRight", CTX)?)?,
    })
}

/// Parse a geographic radius. The radius reaches the geo index unvalidated
/// otherwise: a negative or non-finite value yields nonsensical distance
/// comparisons (and feeds NaN into the geohash math), so reject it here.
fn parse_geo_radius(v: &Value) -> Result<SegmentGeoRadius> {
    const CTX: &str = "geo radius";
    let center = parse_geo_point(required(v, "Center", CTX)?)?;
    let radius = as_f64(required(v, "Radius", CTX)?, CTX, "Radius")?;
    if !radius.is_finite() || radius < 0.0 {
        return Err(ApiError::invalid_argument(format!(
            "invalid geo radius: must be a finite, non-negative number of meters, got {radius}"
        )));
    }
    Ok(SegmentGeoRadius {
        center,
        radius: radius.into(),
    })
}

/// Parse one polygon ring, mirroring the engine's `validate_line_string`,
/// which only runs on its serde path: a ring needs at least 4 points and the
/// first and last points must be equal to close the ring. Building the
/// segment type directly would otherwise let a malformed ring through to the
/// geo index, where it can panic on indexed payloads.
fn parse_geo_ring(v: &Value) -> Result<SegmentGeoLineString> {
    const CTX: &str = "geo ring";
    let items = required_array(v, "Points", CTX)?;
    let points = items
        .iter()
        .map(parse_geo_point)
        .collect::<Result<Vec<_>>>()?;
    if points.len() <= 3 {
        return Err(ApiError::invalid_argument(format!(
            "invalid geo ring: a closed ring needs at least 4 points, got {}",
            points.len()
        )));
    }
    // `points` is non-empty here (len > 3), so first/last always exist.
    let (first, last) = (&points[0], &points[points.len() - 1]);
    let closed = (first.lat.into_inner() - last.lat.into_inner()).abs() <= f64::EPSILON
        && (first.lon.into_inner() - last.lon.into_inner()).abs() <= f64::EPSILON;
    if !closed {
        return Err(ApiError::invalid_argument(
            "invalid geo ring: the first and last points must be equal to close the ring",
        ));
    }
    Ok(SegmentGeoLineString { points })
}

/// Parse a geographic polygon: an exterior ring with optional interior holes.
fn parse_geo_polygon(v: &Value) -> Result<SegmentGeoPolygon> {
    const CTX: &str = "geo polygon";
    let exterior = parse_geo_ring(required(v, "Exterior", CTX)?)?;
    let interiors = match field(v, "Interiors") {
        None | Some(Value::Null) => None,
        Some(inner) => {
            let rings = inner.as_array().ok_or_else(|| {
                ApiError::invalid_argument(format!(
                    "{CTX} field \"Interiors\" must be an array"
                ))
            })?;
            Some(
                rings
                    .iter()
                    .map(parse_geo_ring)
                    .collect::<Result<Vec<_>>>()?,
            )
        }
    };
    Ok(SegmentGeoPolygon {
        exterior,
        interiors,
    })
}

/// Parse a `has_id` point id: numeric or UUID string.
fn parse_point_id(v: &Value) -> Result<SegmentPointId> {
    const CTX: &str = "point id";
    match kind(v, CTX)? {
        "num" => Ok(SegmentPointId::NumId(as_u64(
            required(v, "Value", CTX)?,
            CTX,
            "Value",
        )?)),
        "uuid" => {
            let raw = as_str(required(v, "Value", CTX)?, CTX, "Value")?;
            let id = uuid::Uuid::parse_str(raw).map_err(|e| {
                ApiError::invalid_argument(format!("invalid point UUID {raw:?}: {e}"))
            })?;
            Ok(SegmentPointId::Uuid(id))
        }
        other => Err(ApiError::invalid_argument(format!(
            "{CTX}: unknown point kind {other:?}"
        ))),
    }
}

/// Read a JSON number as `f64`. JSON has no NaN or infinity literal, so any
/// decoded number is usable; downstream conversions (geo point, radius)
/// apply their own finiteness checks where the engine needs them.
fn as_f64(v: &Value, ctx: &str, key: &str) -> Result<f64> {
    v.as_f64().ok_or_else(|| {
        ApiError::invalid_argument(format!("{ctx} field {key:?} must be a number"))
    })
}

/// Read an optional JSON number: absent and explicit-null mean unset.
fn opt_f64(v: &Value, ctx: &str, key: &str) -> Result<Option<f64>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(None),
        Some(value) => Ok(Some(as_f64(value, ctx, key)?)),
    }
}

/// Read a JSON integer that fits in 64 bits, signed. `Number::as_i64`
/// rejects negatives-out-of-range, fractions, and non-numbers alike.
fn as_i64(v: &Value, ctx: &str, key: &str) -> Result<i64> {
    v.as_i64().ok_or_else(|| {
        ApiError::invalid_argument(format!("{ctx} field {key:?} must be an integer"))
    })
}

/// Read a JSON integer that fits in 32 bits, for slice bounds.
fn as_u32(v: &Value, ctx: &str, key: &str) -> Result<u32> {
    let raw = as_u64(v, ctx, key)?;
    u32::try_from(raw).map_err(|_| {
        ApiError::invalid_argument(format!(
            "{ctx} field {key:?} must be a 32-bit unsigned integer"
        ))
    })
}

/// Look up a mandatory array field.
fn required_array<'a>(v: &'a Value, key: &str, ctx: &str) -> Result<&'a [Value]> {
    required(v, key, ctx)?
        .as_array()
        .ok_or_else(|| ApiError::invalid_argument(format!("{ctx} field {key:?} must be an array")))
        .map(Vec::as_slice)
}

/// Reject non-object input for shapes whose fields are all optional. Without
/// this a mistyped value (number, string, array) would silently become an
/// unbounded predicate that matches every point.
fn require_object(v: &Value, ctx: &str) -> Result<()> {
    if v.as_object().is_none() {
        return Err(ApiError::invalid_argument(format!(
            "{ctx} must be an object"
        )));
    }
    Ok(())
}
