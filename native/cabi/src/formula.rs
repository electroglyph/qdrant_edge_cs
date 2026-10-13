use serde_json::Value;

use crate::error::{ApiError, Result, check_expression_size, check_nesting_depth};
use crate::json::{as_str, as_u64, field, kind, required};

/// Parse a C# `Expression` kind-union object into the engine formula node.
///
/// The wire `Size`/`Depth` fields are required for shape compatibility but are
/// never trusted for enforcement: a forged `Size` would bypass the heap cap.
/// Instead the parse tracks the actual recursion depth (stack safety) and a
/// saturating node counter (heap safety) while building, rejecting anything
/// past the caps as it goes.
pub(crate) fn parse_expression(v: &Value) -> Result<edge::Expression> {
    let mut nodes: u64 = 0;
    // Root depth is 1 so a 64-deep tree passes and a 65-deep one fails,
    // matching the C# `Validate` measure.
    parse_expression_inner(v, 1, &mut nodes)
}

fn parse_expression_inner(v: &Value, depth: u32, nodes: &mut u64) -> Result<edge::Expression> {
    const CTX: &str = "formula expression";
    check_nesting_depth(CTX, depth)?;
    *nodes = nodes.saturating_add(1);
    check_expression_size(*nodes)?;
    // Required on the wire; the values are informational only and are not
    // used for enforcement (see above).
    let _ = field(v, "Size");
    let _ = field(v, "Depth");
    match kind(v, CTX)? {
        "constant" => {
            let value = finite(required(v, "Value", CTX)?, "formula constant must be finite")?;
            Ok(edge::Expression::Constant(value))
        }
        "variable" => {
            let name = as_str(required(v, "Name", CTX)?, CTX, "Name")?;
            Ok(edge::Expression::Variable(name.to_owned()))
        }
        "condition" => {
            let predicate = required(v, "Predicate", CTX)?;
            let condition = super::filter::parse_condition(predicate, 0)?;
            Ok(edge::Expression::Condition(Box::new(condition)))
        }
        "geo_distance" => {
            let origin_value = required(v, "Origin", CTX)?;
            let lon = geo_coord(origin_value, "Lon")?;
            let lat = geo_coord(origin_value, "Lat")?;
            let origin = edge::GeoPoint::new(lon, lat).map_err(|err| {
                ApiError::invalid_argument(format!(
                    "invalid geo point (lon={lon}, lat={lat}): {err}"
                ))
            })?;
            let key = as_str(required(v, "Key", CTX)?, CTX, "Key")?;
            Ok(edge::Expression::GeoDistance {
                origin,
                to: super::filter::parse_key(key, CTX)?,
            })
        }
        "datetime" => {
            let text = as_str(required(v, "Value", CTX)?, CTX, "Value")?;
            Ok(edge::Expression::Datetime(text.to_owned()))
        }
        "datetime_key" => {
            let key = as_str(required(v, "Key", CTX)?, CTX, "Key")?;
            Ok(edge::Expression::DatetimeKey(super::filter::parse_key(key, CTX)?))
        }
        "mult" => {
            let factors = child_array(v, "Factors", depth, nodes)?;
            Ok(edge::Expression::Mult(factors))
        }
        "sum" => {
            let terms = child_array(v, "Terms", depth, nodes)?;
            Ok(edge::Expression::Sum(terms))
        }
        "max" => {
            let operands = child_array(v, "Operands", depth, nodes)?;
            require_non_empty("max", &operands)?;
            Ok(edge::Expression::Max(operands))
        }
        "min" => {
            let operands = child_array(v, "Operands", depth, nodes)?;
            require_non_empty("min", &operands)?;
            Ok(edge::Expression::Min(operands))
        }
        "negate" => Ok(edge::Expression::Neg(Box::new(child(v, "Operand", depth, nodes)?))),
        "div" => {
            let left = child(v, "Left", depth, nodes)?;
            let right = child(v, "Right", depth, nodes)?;
            let by_zero_default = opt_finite(v, "ByZeroDefault", "div by_zero_default must be finite")?;
            Ok(edge::Expression::Div {
                left: Box::new(left),
                right: Box::new(right),
                by_zero_default,
            })
        }
        "sqrt" => Ok(edge::Expression::Sqrt(Box::new(child(v, "Operand", depth, nodes)?))),
        "pow" => {
            let basis = child(v, "Basis", depth, nodes)?;
            let exponent = child(v, "Exponent", depth, nodes)?;
            Ok(edge::Expression::Pow {
                base: Box::new(basis),
                exponent: Box::new(exponent),
            })
        }
        "exp" => Ok(edge::Expression::Exp(Box::new(child(v, "Operand", depth, nodes)?))),
        "log10" => Ok(edge::Expression::Log10(Box::new(child(v, "Operand", depth, nodes)?))),
        "ln" => Ok(edge::Expression::Ln(Box::new(child(v, "Operand", depth, nodes)?))),
        "acosh" => Ok(edge::Expression::Acosh(Box::new(child(v, "Operand", depth, nodes)?))),
        "abs" => Ok(edge::Expression::Abs(Box::new(child(v, "Operand", depth, nodes)?))),
        "decay" => {
            let decay_kind = parse_decay_kind(v)?;
            let x = child(v, "X", depth, nodes)?;
            let target = match field(v, "Target") {
                None | Some(Value::Null) => None,
                Some(raw) => Some(Box::new(parse_expression_inner(raw, depth + 1, nodes)?)),
            };
            let midpoint = opt_finite(v, "Midpoint", "decay midpoint must be finite")?;
            let scale = opt_finite(v, "Scale", "decay scale must be finite")?;
            Ok(edge::Expression::Decay {
                kind: decay_kind,
                x: Box::new(x),
                target,
                midpoint,
                scale,
            })
        }
        unknown => Err(ApiError::invalid_argument(format!(
            "{CTX} has unknown kind {unknown:?}"
        ))),
    }
}

/// Map a C# `DecayKind` ordinal onto the engine decay kind. The declaration
/// order matches on both sides (`Lin = 0`, `Gauss = 1`, `Exp = 2`), but the
/// mapping is explicit so a reordering on either side fails loudly.
fn parse_decay_kind(v: &Value) -> Result<edge::DecayKind> {
    const CTX: &str = "formula expression";
    let raw = as_u64(required(v, "DecayKind", CTX)?, CTX, "DecayKind")?;
    match raw {
        0 => Ok(edge::DecayKind::Lin),
        1 => Ok(edge::DecayKind::Gauss),
        2 => Ok(edge::DecayKind::Exp),
        _ => Err(ApiError::invalid_argument(format!(
            "unknown decay kind {raw}"
        ))),
    }
}

/// Parse a required named single-child operand.
fn child(v: &Value, key: &str, depth: u32, nodes: &mut u64) -> Result<edge::Expression> {
    parse_expression_inner(required(v, key, "formula expression")?, depth + 1, nodes)
}

/// Parse an operand list; absent and null read as empty.
fn child_array(v: &Value, key: &str, depth: u32, nodes: &mut u64) -> Result<Vec<edge::Expression>> {
    const CTX: &str = "formula expression";
    match field(v, key) {
        None | Some(Value::Null) => Ok(Vec::new()),
        Some(items) => items
            .as_array()
            .ok_or_else(|| {
                ApiError::invalid_argument(format!("{CTX} field {key:?} must be an array"))
            })?
            .iter()
            .map(|item| parse_expression_inner(item, depth + 1, nodes))
            .collect(),
    }
}

/// Read one geo coordinate as `f64`; range validation happens in
/// `GeoPoint::new` so the error names the point, mirroring the FFI conversion.
fn geo_coord(origin: &Value, key: &str) -> Result<f64> {
    const CTX: &str = "formula geo origin";
    let raw = required(origin, key, CTX)?;
    raw.as_f64().ok_or_else(|| {
        ApiError::invalid_argument(format!("{CTX} field {key:?} must be a number"))
    })
}

/// Read an optional finite `f32` with a fixed validation message.
fn opt_finite(v: &Value, key: &str, message: &str) -> Result<Option<f32>> {
    match field(v, key) {
        None | Some(Value::Null) => Ok(None),
        Some(raw) => Ok(Some(finite(raw, message)?)),
    }
}

/// Read a JSON number as a finite `f32`, rejecting NaN, infinity, and magnitudes
/// outside the `f32` range (a bare `as` cast would saturate those to infinity).
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

fn require_non_empty(operator: &str, operands: &[edge::Expression]) -> Result<()> {
    if operands.is_empty() {
        return Err(ApiError::invalid_argument(format!(
            "`{operator}` needs at least one operand"
        )));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use serde_json::Value;

    use super::*;

    fn parse(raw: &str) -> Value {
        serde_json::from_str(raw).expect("test JSON is valid")
    }

    #[test]
    fn parse_expression_all_kinds() {
        let cases = [
            r#"{"kind":"constant","Value":0.2,"Size":1,"Depth":1}"#,
            r#"{"kind":"variable","Name":"$score","Size":1,"Depth":1}"#,
            r#"{"kind":"condition","Predicate":{"kind":"is_empty","Key":"k"},"Size":2,"Depth":2}"#,
            r#"{"kind":"geo_distance","Origin":{"Lon":1.5,"Lat":2.5},"Key":"loc","Size":1,"Depth":1}"#,
            r#"{"kind":"datetime","Value":"2024-01-01T00:00:00Z","Size":1,"Depth":1}"#,
            r#"{"kind":"datetime_key","Key":"ts","Size":1,"Depth":1}"#,
            r#"{"kind":"mult","Factors":[{"kind":"variable","Name":"a","Size":1,"Depth":1}],"Size":2,"Depth":2}"#,
            r#"{"kind":"sum","Terms":[],"Size":1,"Depth":1}"#,
            r#"{"kind":"max","Operands":[{"kind":"constant","Value":1.0,"Size":1,"Depth":1}],"Size":2,"Depth":2}"#,
            r#"{"kind":"min","Operands":[{"kind":"constant","Value":1.0,"Size":1,"Depth":1}],"Size":2,"Depth":2}"#,
            r#"{"kind":"negate","Operand":{"kind":"variable","Name":"a","Size":1,"Depth":1},"Size":2,"Depth":2}"#,
            r#"{"kind":"div","Left":{"kind":"variable","Name":"a","Size":1,"Depth":1},"Right":{"kind":"variable","Name":"b","Size":1,"Depth":1},"ByZeroDefault":1.5,"Size":3,"Depth":2}"#,
            r#"{"kind":"sqrt","Operand":{"kind":"variable","Name":"a","Size":1,"Depth":1},"Size":2,"Depth":2}"#,
            r#"{"kind":"pow","Basis":{"kind":"variable","Name":"a","Size":1,"Depth":1},"Exponent":{"kind":"variable","Name":"b","Size":1,"Depth":1},"Size":3,"Depth":2}"#,
            r#"{"kind":"exp","Operand":{"kind":"variable","Name":"a","Size":1,"Depth":1},"Size":2,"Depth":2}"#,
            r#"{"kind":"log10","Operand":{"kind":"variable","Name":"a","Size":1,"Depth":1},"Size":2,"Depth":2}"#,
            r#"{"kind":"ln","Operand":{"kind":"variable","Name":"a","Size":1,"Depth":1},"Size":2,"Depth":2}"#,
            r#"{"kind":"acosh","Operand":{"kind":"variable","Name":"a","Size":1,"Depth":1},"Size":2,"Depth":2}"#,
            r#"{"kind":"abs","Operand":{"kind":"variable","Name":"a","Size":1,"Depth":1},"Size":2,"Depth":2}"#,
            r#"{"kind":"decay","DecayKind":1,"X":{"kind":"variable","Name":"x","Size":1,"Depth":1},"Target":null,"Midpoint":0.5,"Scale":2.0,"Size":2,"Depth":2}"#,
        ];
        for raw in cases {
            parse_expression(&parse(raw)).expect("expression kind parses");
        }
    }

    #[test]
    fn parse_expression_decay_kinds() {
        for (ordinal, variant) in [(0, "Lin"), (1, "Gauss"), (2, "Exp")] {
            let raw = format!(
                r#"{{"kind":"decay","DecayKind":{ordinal},"X":{{"kind":"variable","Name":"x","Size":1,"Depth":1}},"Target":null,"Midpoint":null,"Scale":null,"Size":2,"Depth":2}}"#
            );
            let expr = parse_expression(&parse(&raw)).expect("decay kind parses");
            assert!(format!("{expr:?}").contains(variant), "{expr:?}");
        }
        let bad = parse(
            r#"{"kind":"decay","DecayKind":7,"X":{"kind":"variable","Name":"x","Size":1,"Depth":1},"Target":null,"Midpoint":null,"Scale":null,"Size":2,"Depth":2}"#,
        );
        assert!(parse_expression(&bad).is_err());
    }

    #[test]
    fn parse_expression_rejects_bad_wire() {
        // Wire Size/Depth are informational only: a forged small Size on a
        // huge tree must not bypass the heap cap, and a forged large Size on
        // a tiny tree is ignored rather than rejected.
        let forged_small = parse(r#"{"kind":"constant","Value":1.0,"Size":10001,"Depth":1}"#);
        parse_expression(&forged_small).expect("forged size on a tiny tree is ignored");
        let forged_deep = parse(r#"{"kind":"constant","Value":1.0,"Size":1,"Depth":65}"#);
        parse_expression(&forged_deep).expect("forged depth on a shallow tree is ignored");
        // The caps apply to the actual shape: 65 genuinely nested negates.
        let mut deep = r#"{"kind":"constant","Value":1.0,"Size":1,"Depth":1}"#.to_owned();
        for _ in 0..65 {
            deep = format!(r#"{{"kind":"negate","Operand":{deep},"Size":1,"Depth":1}}"#);
        }
        assert!(parse_expression(&parse(&deep)).is_err());
        // And 10001 actual nodes exceed the heap cap.
        let mut terms = String::from("[");
        for i in 0..10_001 {
            if i > 0 {
                terms.push(',');
            }
            terms.push_str(r#"{"kind":"constant","Value":1.0,"Size":1,"Depth":1}"#);
        }
        terms.push(']');
        let wide = parse(&format!(
            r#"{{"kind":"sum","Terms":{terms},"Size":1,"Depth":1}}"#
        ));
        let err = parse_expression(&wide).unwrap_err();
        assert!(err.message.contains("exceeds the maximum"), "{err:?}");
        let max = parse(r#"{"kind":"max","Operands":[],"Size":1,"Depth":1}"#);
        assert!(
            parse_expression(&max)
                .unwrap_err()
                .message
                .contains("needs at least one")
        );
        let inf = parse(r#"{"kind":"constant","Value":1e40,"Size":1,"Depth":1}"#);
        assert!(parse_expression(&inf).is_err());
        let geo = parse(
            r#"{"kind":"geo_distance","Origin":{"Lon":999.0,"Lat":0.0},"Key":"k","Size":1,"Depth":1}"#,
        );
        assert!(
            parse_expression(&geo)
                .unwrap_err()
                .message
                .contains("invalid geo point")
        );
        let unknown = parse(r#"{"kind":"nope","Size":1,"Depth":1}"#);
        assert!(parse_expression(&unknown).is_err());
    }
}
