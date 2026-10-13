use serde_json::Value;

use crate::error::{ApiError, Result};

/// Look up `key` in a JSON object, returning `None` when `v` is not an object
/// or the key is absent.
pub(crate) fn field<'a>(v: &'a Value, key: &str) -> Option<&'a Value> {
    v.as_object()?.get(key)
}

/// Look up a mandatory field, rejecting absent and explicit-null values so
/// callers never silently fall back to a default for a required input.
pub(crate) fn required<'a>(v: &'a Value, key: &str, ctx: &str) -> Result<&'a Value> {
    match field(v, key) {
        Some(value) if !value.is_null() => Ok(value),
        _ => Err(ApiError::invalid_argument(format!(
            "{ctx} is missing required field {key:?}"
        ))),
    }
}

pub(crate) fn as_str<'a>(v: &'a Value, ctx: &str, key: &str) -> Result<&'a str> {
    v.as_str().ok_or_else(|| {
        ApiError::invalid_argument(format!("{ctx} field {key:?} must be a string"))
    })
}

/// Read a non-negative integer. `Number::as_u64` rejects negatives and
/// fractions, so both are reported as invalid input rather than truncated.
pub(crate) fn as_u64(v: &Value, ctx: &str, key: &str) -> Result<u64> {
    match v {
        Value::Number(n) => n.as_u64(),
        _ => None,
    }
    .ok_or_else(|| {
        ApiError::invalid_argument(format!(
            "{ctx} field {key:?} must be a non-negative integer"
        ))
    })
}

pub(crate) fn as_f32(v: &Value, ctx: &str, key: &str) -> Result<f32> {
    match v.as_f64() {
        Some(raw) if is_f32_range(raw) => Ok(raw as f32),
        _ => Err(ApiError::invalid_argument(format!(
            "{ctx} field {key:?} must be a finite number"
        ))),
    }
}

pub(crate) fn as_bool(v: &Value, ctx: &str, key: &str) -> Result<bool> {
    v.as_bool().ok_or_else(|| {
        ApiError::invalid_argument(format!("{ctx} field {key:?} must be a boolean"))
    })
}

pub(crate) fn opt_str<'a>(v: &'a Value, ctx: &str, key: &str) -> Result<Option<&'a str>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(None),
        Some(value) => Ok(Some(as_str(value, ctx, key)?)),
    }
}

pub(crate) fn opt_u64(v: &Value, ctx: &str, key: &str) -> Result<Option<u64>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(None),
        Some(value) => Ok(Some(as_u64(value, ctx, key)?)),
    }
}

pub(crate) fn opt_bool(v: &Value, ctx: &str, key: &str) -> Result<Option<bool>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(None),
        Some(value) => Ok(Some(as_bool(value, ctx, key)?)),
    }
}

/// Read a dense vector. Each element must be finite: JSON has no canonical NaN
/// or infinity literal, and a non-finite coordinate would poison scoring.
pub(crate) fn f32_array(v: &Value, ctx: &str) -> Result<Vec<f32>> {
    let items = v.as_array().ok_or_else(|| {
        ApiError::invalid_argument(format!("{ctx} must be an array of numbers"))
    })?;
    items
        .iter()
        .enumerate()
        .map(|(index, value)| match value.as_f64() {
            Some(raw) if is_f32_range(raw) => Ok(raw as f32),
            _ => Err(ApiError::invalid_argument(format!(
                "{ctx} element {index} must be a finite number"
            ))),
        })
        .collect()
}

/// Read the lowercase `"kind"` discriminator every C# union carries.
pub(crate) fn kind<'a>(v: &'a Value, ctx: &str) -> Result<&'a str> {
    as_str(required(v, "kind", ctx)?, ctx, "kind")
}

/// Finite and representable as `f32`: the `as` cast saturates out-of-range
/// `f64` values to infinity, so the range check must come first.
fn is_f32_range(raw: f64) -> bool {
    raw.is_finite() && raw <= f64::from(f32::MAX) && raw >= -f64::from(f32::MAX)
}
