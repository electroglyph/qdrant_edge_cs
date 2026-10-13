namespace Qdrant.Edge;

/// <summary>
/// Formula expression: immutable handle built via static factories.
/// Variable and Datetime are infallible; the other 18 factories validate.
/// Serializes as a <c>kind</c>-discriminated union carrying the full operands.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ConstantExpression), "constant")]
[JsonDerivedType(typeof(VariableExpression), "variable")]
[JsonDerivedType(typeof(ConditionExpression), "condition")]
[JsonDerivedType(typeof(GeoDistanceExpression), "geo_distance")]
[JsonDerivedType(typeof(DatetimeExpression), "datetime")]
[JsonDerivedType(typeof(DatetimeKeyExpression), "datetime_key")]
[JsonDerivedType(typeof(MultExpression), "mult")]
[JsonDerivedType(typeof(SumExpression), "sum")]
[JsonDerivedType(typeof(MaxExpression), "max")]
[JsonDerivedType(typeof(MinExpression), "min")]
[JsonDerivedType(typeof(NegateExpression), "negate")]
[JsonDerivedType(typeof(DivExpression), "div")]
[JsonDerivedType(typeof(SqrtExpression), "sqrt")]
[JsonDerivedType(typeof(PowExpression), "pow")]
[JsonDerivedType(typeof(ExpExpression), "exp")]
[JsonDerivedType(typeof(Log10Expression), "log10")]
[JsonDerivedType(typeof(LnExpression), "ln")]
[JsonDerivedType(typeof(AcoshExpression), "acosh")]
[JsonDerivedType(typeof(AbsExpression), "abs")]
[JsonDerivedType(typeof(DecayExpression), "decay")]
public abstract record Expression
{
    /// <summary>Expression kind (constant, variable, sum, ...).</summary>
    [JsonIgnore]
    public abstract string Kind { get; }

    /// <summary>Saturating node count for the 10k cap.</summary>
    public abstract ulong Size { get; init; }

    /// <summary>Nesting depth for the 64-deep cap.</summary>
    public abstract uint Depth { get; init; }

    /// <summary>Constant expression.</summary>
    /// <param name="value">Constant value; must be finite.</param>
    public static Expression Constant(float value)
    {
        EdgeValidation.CheckFinite(nameof(value), value);
        return new ConstantExpression(value);
    }

    /// <summary>Variable expression (infallible).</summary>
    /// <param name="name">Variable name.</param>
    public static Expression Variable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        return new VariableExpression(name);
    }

    /// <summary>Condition expression.</summary>
    /// <param name="condition">Filter condition.</param>
    public static Expression Condition(Condition condition)
    {
        ArgumentNullException.ThrowIfNull(condition, nameof(condition));
        condition.Validate();
        ulong size = 1 + condition.Depth();
        uint depth = 1 + condition.Depth();
        EdgeValidation.CheckExpressionSize(size);
        EdgeValidation.CheckNestingDepth("formula", depth);
        return new ConditionExpression(condition) { Size = size, Depth = depth };
    }

    /// <summary>Geo-distance expression.</summary>
    /// <param name="origin">Origin point.</param>
    /// <param name="key">Payload key holding the target.</param>
    public static Expression GeoDistance(GeoPoint origin, string key)
    {
        ArgumentNullException.ThrowIfNull(origin, nameof(origin));
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        origin.Validate();
        EdgeValidation.CheckPayloadKey(key);
        return new GeoDistanceExpression(origin, key);
    }

    /// <summary>Datetime literal (infallible; validated at query execution).</summary>
    /// <param name="value">RFC3339 datetime.</param>
    public static Expression Datetime(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        return new DatetimeExpression(value);
    }

    /// <summary>Datetime-key expression.</summary>
    /// <param name="key">Payload key.</param>
    public static Expression DatetimeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        EdgeValidation.CheckPayloadKey(key);
        return new DatetimeKeyExpression(key);
    }

    /// <summary>Product expression.</summary>
    /// <param name="factors">Factors.</param>
    public static Expression Mult(IReadOnlyList<Expression> factors) => Combine<MultExpression>(factors, static (parts, size, depth) => new MultExpression(parts) { Size = size, Depth = depth });

    /// <summary>Sum expression.</summary>
    /// <param name="terms">Terms.</param>
    public static Expression Sum(IReadOnlyList<Expression> terms) => Combine<SumExpression>(terms, static (parts, size, depth) => new SumExpression(parts) { Size = size, Depth = depth });

    /// <summary>Maximum expression.</summary>
    /// <param name="operands">Operands (non-empty).</param>
    public static Expression Max(IReadOnlyList<Expression> operands)
    {
        ArgumentNullException.ThrowIfNull(operands, nameof(operands));
        if (operands.Count == 0)
        {
            throw new EdgeInvalidArgumentException("max requires at least one operand");
        }

        return Combine<MaxExpression>(operands, static (parts, size, depth) => new MaxExpression(parts) { Size = size, Depth = depth });
    }

    /// <summary>Minimum expression.</summary>
    /// <param name="operands">Operands (non-empty).</param>
    public static Expression Min(IReadOnlyList<Expression> operands)
    {
        ArgumentNullException.ThrowIfNull(operands, nameof(operands));
        if (operands.Count == 0)
        {
            throw new EdgeInvalidArgumentException("min requires at least one operand");
        }

        return Combine<MinExpression>(operands, static (parts, size, depth) => new MinExpression(parts) { Size = size, Depth = depth });
    }

    /// <summary>Negation expression.</summary>
    /// <param name="expression">Operand.</param>
    public static Expression Negate(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression, nameof(expression));
        return Combine<NegateExpression>([expression], static (parts, size, depth) => new NegateExpression(parts[0]) { Size = size, Depth = depth });
    }

    /// <summary>Division expression.</summary>
    /// <param name="left">Numerator.</param>
    /// <param name="right">Denominator.</param>
    /// <param name="byZeroDefault">Finite fallback for division by zero.</param>
    public static Expression Div(Expression left, Expression right, float? byZeroDefault = null)
    {
        ArgumentNullException.ThrowIfNull(left, nameof(left));
        ArgumentNullException.ThrowIfNull(right, nameof(right));
        if (byZeroDefault is float fallback)
        {
            EdgeValidation.CheckFinite(nameof(byZeroDefault), fallback);
        }

        IReadOnlyList<Expression> parts = [left, right];
        (ulong size, uint depth) = Measure(parts);
        return new DivExpression(left, right, byZeroDefault) { Size = size, Depth = depth };
    }

    /// <summary>Square-root expression.</summary>
    /// <param name="expression">Operand.</param>
    public static Expression Sqrt(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression, nameof(expression));
        return Combine<SqrtExpression>([expression], static (parts, size, depth) => new SqrtExpression(parts[0]) { Size = size, Depth = depth });
    }

    /// <summary>Power expression.</summary>
    /// <param name="basis">Base.</param>
    /// <param name="exponent">Exponent.</param>
    public static Expression Pow(Expression basis, Expression exponent)
    {
        ArgumentNullException.ThrowIfNull(basis, nameof(basis));
        ArgumentNullException.ThrowIfNull(exponent, nameof(exponent));
        IReadOnlyList<Expression> parts = [basis, exponent];
        (ulong size, uint depth) = Measure(parts);
        return new PowExpression(basis, exponent) { Size = size, Depth = depth };
    }

    /// <summary>Exponential expression.</summary>
    /// <param name="expression">Operand.</param>
    public static Expression Exp(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression, nameof(expression));
        return Combine<ExpExpression>([expression], static (parts, size, depth) => new ExpExpression(parts[0]) { Size = size, Depth = depth });
    }

    /// <summary>Base-10 logarithm expression.</summary>
    /// <param name="expression">Operand.</param>
    public static Expression Log10(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression, nameof(expression));
        return Combine<Log10Expression>([expression], static (parts, size, depth) => new Log10Expression(parts[0]) { Size = size, Depth = depth });
    }

    /// <summary>Natural logarithm expression.</summary>
    /// <param name="expression">Operand.</param>
    public static Expression Ln(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression, nameof(expression));
        return Combine<LnExpression>([expression], static (parts, size, depth) => new LnExpression(parts[0]) { Size = size, Depth = depth });
    }

    /// <summary>Inverse hyperbolic cosine expression.</summary>
    /// <param name="expression">Operand.</param>
    public static Expression Acosh(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression, nameof(expression));
        return Combine<AcoshExpression>([expression], static (parts, size, depth) => new AcoshExpression(parts[0]) { Size = size, Depth = depth });
    }

    /// <summary>Absolute-value expression.</summary>
    /// <param name="expression">Operand.</param>
    public static Expression Abs(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression, nameof(expression));
        return Combine<AbsExpression>([expression], static (parts, size, depth) => new AbsExpression(parts[0]) { Size = size, Depth = depth });
    }

    /// <summary>Decay expression.</summary>
    /// <param name="kind">Decay kind.</param>
    /// <param name="x">Input.</param>
    /// <param name="target">Optional target.</param>
    /// <param name="midpoint">Optional midpoint; must be finite.</param>
    /// <param name="scale">Optional scale; must be finite.</param>
    public static Expression Decay(
        DecayKind kind,
        Expression x,
        Expression? target = null,
        float? midpoint = null,
        float? scale = null)
    {
        ArgumentNullException.ThrowIfNull(x, nameof(x));
        if (midpoint is float mid)
        {
            EdgeValidation.CheckFinite(nameof(midpoint), mid);
        }

        if (scale is float sc)
        {
            EdgeValidation.CheckFinite(nameof(scale), sc);
        }

        List<Expression> parts = [x];
        if (target is not null)
        {
            parts.Add(target);
        }

        (ulong size, uint depth) = Measure(parts);
        return new DecayExpression(kind, x, target, midpoint, scale) { Size = size, Depth = depth };
    }

    private static T Combine<T>(IReadOnlyList<Expression> parts, Func<IReadOnlyList<Expression>, ulong, uint, T> build)
    {
        ArgumentNullException.ThrowIfNull(parts, nameof(parts));
        ArgumentNullException.ThrowIfNull(build, nameof(build));
        (ulong size, uint depth) = Measure(parts);
        return build(parts, size, depth);
    }

    private static (ulong Size, uint Depth) Measure(IReadOnlyList<Expression> parts)
    {
        ulong size = 1;
        uint depth = 1;
        foreach (Expression part in parts)
        {
            ArgumentNullException.ThrowIfNull(part, nameof(part));
            size = size >= ulong.MaxValue - part.Size ? ulong.MaxValue : size + part.Size;
            depth = Math.Max(depth, part.Depth + 1);
        }

        EdgeValidation.CheckExpressionSize(size);
        EdgeValidation.CheckNestingDepth("formula", depth);
        return (size, depth);
    }
}

/// <summary>Constant expression.</summary>
/// <param name="Value">Constant value.</param>
public sealed record ConstantExpression(float Value) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "constant";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Variable expression.</summary>
/// <param name="Name">Variable name.</param>
public sealed record VariableExpression(string Name) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "variable";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Condition expression.</summary>
public sealed record ConditionExpression : Expression
{
    /// <summary>Filter condition predicate.</summary>
    public Condition Predicate { get; init; }

    /// <summary>Builds a condition expression.</summary>
    /// <param name="predicate">Filter condition.</param>
    public ConditionExpression(Condition predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
        Predicate = predicate;
    }

    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "condition";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Geo-distance expression.</summary>
/// <param name="Origin">Origin point.</param>
/// <param name="Key">Payload key.</param>
public sealed record GeoDistanceExpression(GeoPoint Origin, string Key) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "geo_distance";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Datetime literal expression.</summary>
/// <param name="Value">RFC3339 datetime.</param>
public sealed record DatetimeExpression(string Value) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "datetime";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Datetime-key expression.</summary>
/// <param name="Key">Payload key.</param>
public sealed record DatetimeKeyExpression(string Key) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "datetime_key";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Product expression.</summary>
/// <param name="Factors">Factors.</param>
public sealed record MultExpression(IReadOnlyList<Expression> Factors) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "mult";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Sum expression.</summary>
/// <param name="Terms">Terms.</param>
public sealed record SumExpression(IReadOnlyList<Expression> Terms) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "sum";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Maximum expression.</summary>
/// <param name="Operands">Operands.</param>
public sealed record MaxExpression(IReadOnlyList<Expression> Operands) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "max";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Minimum expression.</summary>
/// <param name="Operands">Operands.</param>
public sealed record MinExpression(IReadOnlyList<Expression> Operands) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "min";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Negation expression.</summary>
/// <param name="Operand">Operand.</param>
public sealed record NegateExpression(Expression Operand) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "negate";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Division expression.</summary>
/// <param name="Left">Numerator.</param>
/// <param name="Right">Denominator.</param>
/// <param name="ByZeroDefault">Fallback for division by zero.</param>
public sealed record DivExpression(Expression Left, Expression Right, float? ByZeroDefault) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "div";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Square-root expression.</summary>
/// <param name="Operand">Operand.</param>
public sealed record SqrtExpression(Expression Operand) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "sqrt";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Power expression.</summary>
/// <param name="Basis">Base.</param>
/// <param name="Exponent">Exponent.</param>
public sealed record PowExpression(Expression Basis, Expression Exponent) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "pow";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Exponential expression.</summary>
/// <param name="Operand">Operand.</param>
public sealed record ExpExpression(Expression Operand) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "exp";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Base-10 logarithm expression.</summary>
/// <param name="Operand">Operand.</param>
public sealed record Log10Expression(Expression Operand) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "log10";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Natural logarithm expression.</summary>
/// <param name="Operand">Operand.</param>
public sealed record LnExpression(Expression Operand) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "ln";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Inverse hyperbolic cosine expression.</summary>
/// <param name="Operand">Operand.</param>
public sealed record AcoshExpression(Expression Operand) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "acosh";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Absolute-value expression.</summary>
/// <param name="Operand">Operand.</param>
public sealed record AbsExpression(Expression Operand) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "abs";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}

/// <summary>Decay expression.</summary>
/// <param name="DecayKind">Decay kind.</param>
/// <param name="X">Input.</param>
/// <param name="Target">Optional target.</param>
/// <param name="Midpoint">Optional midpoint.</param>
/// <param name="Scale">Optional scale.</param>
public sealed record DecayExpression(
    DecayKind DecayKind,
    Expression X,
    Expression? Target,
    float? Midpoint,
    float? Scale) : Expression
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "decay";

    /// <inheritdoc />
    public override ulong Size { get; init; } = 1;

    /// <inheritdoc />
    public override uint Depth { get; init; } = 1;
}
