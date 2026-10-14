using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Coverage gaps for <c>Expression.cs</c>: factories, accounting, forgery, JSON, native.</summary>
public sealed class ExpressionGapsTests
{
    [Fact]
    public void Constant_Zero_Passes()
    {
        ConstantExpression constant = Assert.IsType<ConstantExpression>(Expression.Constant(0f));
        Assert.Equal(0f, constant.Value);
    }

    [Fact]
    public void Constant_NegZero_Passes()
    {
        ConstantExpression constant = Assert.IsType<ConstantExpression>(Expression.Constant(-0f));
        Assert.Equal(-0f, constant.Value);
    }

    [Fact]
    public void Constant_MaxValue_Passes()
    {
        ConstantExpression constant = Assert.IsType<ConstantExpression>(Expression.Constant(float.MaxValue));
        Assert.Equal(float.MaxValue, constant.Value);
    }

    [Fact]
    public void Constant_NaN_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => Expression.Constant(float.NaN));
    }

    [Fact]
    public void Constant_NegInf_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => Expression.Constant(float.NegativeInfinity));
    }

    [Fact]
    public void Variable_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Variable(null!));
    }

    [Fact]
    public void Variable_Empty_Throws()
    {
        Exception thrown = Assert.Throws<ArgumentException>(() => Expression.Variable(string.Empty));
        Assert.Equal(typeof(ArgumentException), thrown.GetType());
    }

    [Fact]
    public void Variable_Whitespace_Throws()
    {
        Exception thrown = Assert.Throws<ArgumentException>(() => Expression.Variable("  "));
        Assert.Equal(typeof(ArgumentException), thrown.GetType());
    }

    [Fact]
    public void Variable_WhitespaceName_ThrowsValidate()
    {
        Expression forged = new VariableExpression("  ");
        Exception thrown = Assert.Throws<ArgumentException>(forged.Validate);
        Assert.Equal(typeof(ArgumentException), thrown.GetType());
    }

    [Fact]
    public void Condition_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Condition(null!));
    }

    [Fact]
    public void Condition_InvalidFilter_Propagates()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Condition(Condition.HasId(null!)));
    }

    [Fact]
    public void Condition_InvalidNestedFilter_Propagates()
    {
        var bad = Condition.Field(new FieldCondition
        {
            Key = "k",
            Match = Match.CreateText("a"),
            Range = new RangeFloat { Gte = 1 },
        });
        Assert.Throws<EdgeInvalidArgumentException>(() => Expression.Condition(bad));
    }

    [Fact]
    public void Condition_SizeAndDepthAccounted()
    {
        Expression expression = Expression.Condition(Condition.IsEmpty("seen"));
        Assert.Equal(1ul, expression.Size);
        Assert.Equal(1u, expression.Depth);
    }

    [Fact]
    public void GeoDistance_NullOrigin_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.GeoDistance(null!, "location"));
    }

    [Fact]
    public void GeoDistance_NullKey_Throws()
    {
        GeoPoint origin = new() { Lon = 0, Lat = 0 };
        Assert.Throws<ArgumentNullException>(() => Expression.GeoDistance(origin, null!));
    }

    [Fact]
    public void GeoDistance_EmptyKey_Throws()
    {
        GeoPoint origin = new() { Lon = 0, Lat = 0 };
        Exception thrown = Assert.Throws<ArgumentException>(() => Expression.GeoDistance(origin, string.Empty));
        Assert.Equal(typeof(ArgumentException), thrown.GetType());
    }

    [Fact]
    public void GeoDistance_WhitespaceKey_Throws()
    {
        GeoPoint origin = new() { Lon = 0, Lat = 0 };
        Exception thrown = Assert.Throws<ArgumentException>(() => Expression.GeoDistance(origin, "  "));
        Assert.Equal(typeof(ArgumentException), thrown.GetType());
    }

    [Fact]
    public void GeoDistance_InvalidOrigin_Propagates()
    {
        GeoPoint origin = new() { Lon = 0, Lat = 999 };
        Assert.Throws<EdgeInvalidArgumentException>(() => Expression.GeoDistance(origin, "location"));
    }

    [Fact]
    public void Datetime_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Datetime(null!));
    }

    [Fact]
    public void Datetime_Empty_Throws()
    {
        Exception thrown = Assert.Throws<ArgumentException>(() => Expression.Datetime(string.Empty));
        Assert.Equal(typeof(ArgumentException), thrown.GetType());
    }

    [Fact]
    public void Datetime_Whitespace_Throws()
    {
        Exception thrown = Assert.Throws<ArgumentException>(() => Expression.Datetime("  "));
        Assert.Equal(typeof(ArgumentException), thrown.GetType());
    }

    [Fact]
    public void Datetime_InvalidString_PassesFactory()
    {
        DatetimeExpression parsed = Assert.IsType<DatetimeExpression>(Expression.Datetime("not-a-date"));
        Assert.Equal("not-a-date", parsed.Value);
    }

    [Fact]
    public void Datetime_BadValue_NativeErrorShardUsable()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        Assert.ThrowsAny<EdgeException>(() => RunFormula(shard, Expression.Datetime("not-a-date")));
        Assert.Equal(2, RunFormula(shard, Expression.Constant(1f)).Count);
    }

    [Fact]
    public void DatetimeKey_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.DatetimeKey(null!));
    }

    [Fact]
    public void DatetimeKey_Empty_Throws()
    {
        Exception thrown = Assert.Throws<ArgumentException>(() => Expression.DatetimeKey(string.Empty));
        Assert.Equal(typeof(ArgumentException), thrown.GetType());
    }

    [Fact]
    public void DatetimeKey_Whitespace_Throws()
    {
        Exception thrown = Assert.Throws<ArgumentException>(() => Expression.DatetimeKey("  "));
        Assert.Equal(typeof(ArgumentException), thrown.GetType());
    }

    [Fact]
    public void Mult_Empty_PassesWithSizeOne()
    {
        Expression empty = Expression.Mult([]);
        Assert.Equal(1ul, empty.Size);
        Assert.Equal(1u, empty.Depth);
    }

    [Fact]
    public void Sum_Empty_PassesWithSizeOne()
    {
        Expression empty = Expression.Sum([]);
        Assert.Equal(1ul, empty.Size);
        Assert.Equal(1u, empty.Depth);
    }

    [Fact]
    public void Mult_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Mult(null!));
    }

    [Fact]
    public void Mult_NullElement_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Mult([null!]));
    }

    [Fact]
    public void Mult_AccountingIsThreeNodesDeepTwo()
    {
        Expression mult = Expression.Mult([Expression.Variable("a"), Expression.Constant(1f)]);
        MultExpression factors = Assert.IsType<MultExpression>(mult);
        Assert.Equal(2, factors.Factors.Count);
        Assert.Equal(3ul, mult.Size);
        Assert.Equal(2u, mult.Depth);
    }

    [Fact]
    public void Max_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Max(null!));
    }

    [Fact]
    public void Max_NullElement_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Max([null!]));
    }

    [Fact]
    public void Max_EmptyOperands_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => Expression.Max([]));
    }

    [Fact]
    public void Min_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Min(null!));
    }

    [Fact]
    public void Min_NullElement_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Min([null!]));
    }

    [Fact]
    public void Min_EmptyOperands_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => Expression.Min([]));
    }

    [Fact]
    public void Sum_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Sum(null!));
    }

    [Fact]
    public void Sum_NullElement_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Sum([null!]));
    }

    [Fact]
    public void Validate_EmptyMaxViaCtor_Throws()
    {
        Expression forged = new MaxExpression([]);
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void Validate_EmptyMinViaCtor_Throws()
    {
        Expression forged = new MinExpression([]);
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void Negate_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Negate(null!));
    }

    [Fact]
    public void Div_NullLeft_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Div(null!, Expression.Constant(1f)));
    }

    [Fact]
    public void Div_NullRight_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Div(Expression.Constant(1f), null!));
    }

    [Fact]
    public void Div_NullFallback_Passes()
    {
        DivExpression div = Assert.IsType<DivExpression>(
            Expression.Div(Expression.Variable("a"), Expression.Constant(2f)));
        Assert.Null(div.ByZeroDefault);
    }

    [Fact]
    public void Div_NaNFallback_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() =>
            Expression.Div(Expression.Variable("a"), Expression.Constant(1f), float.NaN));
    }

    [Fact]
    public void Div_NegInfFallback_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() =>
            Expression.Div(Expression.Variable("a"), Expression.Constant(1f), float.NegativeInfinity));
    }

    [Fact]
    public void Sqrt_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Sqrt(null!));
    }

    [Fact]
    public void Pow_NullBasis_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Pow(null!, Expression.Constant(2f)));
    }

    [Fact]
    public void Pow_NullExponent_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Pow(Expression.Constant(2f), null!));
    }

    [Fact]
    public void Exp_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Exp(null!));
    }

    [Fact]
    public void Log10_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Log10(null!));
    }

    [Fact]
    public void Ln_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Ln(null!));
    }

    [Fact]
    public void Acosh_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Acosh(null!));
    }

    [Fact]
    public void Abs_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Abs(null!));
    }

    [Fact]
    public void Decay_NullX_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Expression.Decay(DecayKind.Gauss, null!));
    }

    [Fact]
    public void Decay_NaNScale_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() =>
            Expression.Decay(DecayKind.Gauss, Expression.Variable("$score"), null, null, float.NaN));
    }

    [Fact]
    public void Decay_PosInfScale_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() =>
            Expression.Decay(DecayKind.Gauss, Expression.Variable("$score"), null, null, float.PositiveInfinity));
    }

    [Fact]
    public void Decay_NegInfScale_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() =>
            Expression.Decay(DecayKind.Gauss, Expression.Variable("$score"), null, null, float.NegativeInfinity));
    }

    [Fact]
    public void Decay_NaNMidpoint_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() =>
            Expression.Decay(DecayKind.Gauss, Expression.Variable("$score"), null, float.NaN, null));
    }

    [Fact]
    public void Decay_PosInfMidpoint_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() =>
            Expression.Decay(DecayKind.Gauss, Expression.Variable("$score"), null, float.PositiveInfinity, null));
    }

    [Fact]
    public void Decay_NegInfMidpoint_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() =>
            Expression.Decay(DecayKind.Gauss, Expression.Variable("$score"), null, float.NegativeInfinity, null));
    }

    [Fact]
    public void Decay_NullTarget_Passes()
    {
        DecayExpression decay = Assert.IsType<DecayExpression>(
            Expression.Decay(DecayKind.Gauss, Expression.Variable("$score")));
        Assert.Null(decay.Target);
        Assert.Equal(2ul, decay.Size);
        Assert.Equal(2u, decay.Depth);
    }

    [Fact]
    public void Decay_WithTarget_AccountsBoth()
    {
        Expression decay = Expression.Decay(
            DecayKind.Lin, Expression.Variable("$score"), Expression.Constant(1f));
        Assert.Equal(3ul, decay.Size);
        Assert.Equal(2u, decay.Depth);
    }

    [Fact]
    public void Decay_AllKinds_Preserved()
    {
        foreach (DecayKind kind in new[] { DecayKind.Lin, DecayKind.Gauss, DecayKind.Exp })
        {
            DecayExpression decay = Assert.IsType<DecayExpression>(
                Expression.Decay(kind, Expression.Variable("$score")));
            Assert.Equal(kind, decay.DecayKind);
            decay.Validate();
        }
    }

    [Fact]
    public void Decay_AllKinds_RoundTripThroughJson()
    {
        foreach (DecayKind kind in new[] { DecayKind.Lin, DecayKind.Gauss, DecayKind.Exp })
        {
            Expression decay = Expression.Decay(kind, Expression.Variable("$score"));
            string json = JsonSerializer.Serialize(decay, EdgeJsonContext.Deep.Expression);
            Expression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Expression);
            DecayExpression restored = Assert.IsType<DecayExpression>(back);
            Assert.Equal(kind, restored.DecayKind);
            Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.Expression));
        }
    }

    [Fact]
    public void Combine_SizeAtCap_Passes()
    {
        List<Expression> terms = new(9999);
        for (int i = 0; i < 9999; i++)
        {
            terms.Add(Expression.Constant(1f));
        }

        Expression sum = Expression.Sum(terms);
        Assert.Equal(10_000ul, sum.Size);
    }

    [Fact]
    public void Combine_SizeAboveCap_Throws()
    {
        List<Expression> terms = new(10_000);
        for (int i = 0; i < 10_000; i++)
        {
            terms.Add(Expression.Constant(1f));
        }

        Assert.Throws<EdgeInvalidArgumentException>(() => Expression.Sum(terms));
    }

    [Fact]
    public void Combine_DepthAtCap_Passes()
    {
        Expression current = Expression.Variable("x");
        for (int i = 0; i < 63; i++)
        {
            current = Expression.Negate(current);
        }

        Assert.Equal(64u, current.Depth);
    }

    [Fact]
    public void Combine_DepthAboveCap_Throws()
    {
        Expression current = Expression.Variable("x");
        for (int i = 0; i < 63; i++)
        {
            current = Expression.Negate(current);
        }

        Assert.Throws<EdgeInvalidArgumentException>(() => Expression.Negate(current));
    }

    [Fact]
    public void Validate_ForgedSizeOnSum_Throws()
    {
        Expression forged = Expression.Sum([ForgedDeepChain()]) with { Size = 1, Depth = 1 };
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void Validate_ForgedDepthOnMult_Throws()
    {
        Expression forged = Expression.Mult([ForgedDeepChain()]) with { Size = 1, Depth = 1 };
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void Validate_ForgedSizeOnDecay_Throws()
    {
        Expression forged = Expression.Decay(DecayKind.Gauss, ForgedDeepChain()) with { Size = 1, Depth = 1 };
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void Validate_NonFiniteConstantViaCtor_Throws()
    {
        Expression forged = new ConstantExpression(float.NaN);
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void Validate_UnknownShape_Throws()
    {
        Expression forged = new ForgedExpression();
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void Expression_All20Kinds_RoundTripThroughJson()
    {
        GeoPoint origin = new() { Lon = 1, Lat = 2 };
        (Expression Expression, Type Shape)[] cases =
        [
            (Expression.Constant(1.5f), typeof(ConstantExpression)),
            (Expression.Variable("x"), typeof(VariableExpression)),
            (Expression.Condition(Condition.IsEmpty("seen")), typeof(ConditionExpression)),
            (Expression.GeoDistance(origin, "location"), typeof(GeoDistanceExpression)),
            (Expression.Datetime("2024-01-02T03:04:05Z"), typeof(DatetimeExpression)),
            (Expression.DatetimeKey("created"), typeof(DatetimeKeyExpression)),
            (Expression.Mult([Expression.Variable("a"), Expression.Constant(2f)]), typeof(MultExpression)),
            (Expression.Sum([Expression.Variable("a"), Expression.Constant(2f)]), typeof(SumExpression)),
            (Expression.Max([Expression.Constant(1f)]), typeof(MaxExpression)),
            (Expression.Min([Expression.Constant(1f)]), typeof(MinExpression)),
            (Expression.Negate(Expression.Variable("a")), typeof(NegateExpression)),
            (Expression.Div(Expression.Variable("a"), Expression.Constant(2f), 0f), typeof(DivExpression)),
            (Expression.Sqrt(Expression.Variable("a")), typeof(SqrtExpression)),
            (Expression.Pow(Expression.Variable("a"), Expression.Constant(2f)), typeof(PowExpression)),
            (Expression.Exp(Expression.Variable("a")), typeof(ExpExpression)),
            (Expression.Log10(Expression.Variable("a")), typeof(Log10Expression)),
            (Expression.Ln(Expression.Variable("a")), typeof(LnExpression)),
            (Expression.Acosh(Expression.Constant(2f)), typeof(AcoshExpression)),
            (Expression.Abs(Expression.Variable("a")), typeof(AbsExpression)),
            (Expression.Decay(DecayKind.Gauss, Expression.Variable("$score")), typeof(DecayExpression)),
        ];

        Assert.Equal(20, cases.Length);
        foreach ((Expression expression, Type shape) in cases)
        {
            string json = JsonSerializer.Serialize(expression);
            Expression? restored = JsonSerializer.Deserialize<Expression>(json);
            Assert.IsType(shape, restored);
            Assert.Equal(json, JsonSerializer.Serialize(restored));
        }
    }

    [Fact]
    public void Expression_KindProperty_MatchesDiscriminator()
    {
        GeoPoint origin = new() { Lon = 1, Lat = 2 };
        (Expression Expression, string Kind)[] cases =
        [
            (Expression.Constant(1f), "constant"),
            (Expression.Variable("x"), "variable"),
            (Expression.Condition(Condition.IsEmpty("seen")), "condition"),
            (Expression.GeoDistance(origin, "location"), "geo_distance"),
            (Expression.Datetime("2024-01-02T03:04:05Z"), "datetime"),
            (Expression.DatetimeKey("created"), "datetime_key"),
            (Expression.Mult([Expression.Variable("a")]), "mult"),
            (Expression.Sum([Expression.Variable("a")]), "sum"),
            (Expression.Max([Expression.Variable("a")]), "max"),
            (Expression.Min([Expression.Variable("a")]), "min"),
            (Expression.Negate(Expression.Variable("a")), "negate"),
            (Expression.Div(Expression.Variable("a"), Expression.Variable("b")), "div"),
            (Expression.Sqrt(Expression.Variable("a")), "sqrt"),
            (Expression.Pow(Expression.Variable("a"), Expression.Variable("b")), "pow"),
            (Expression.Exp(Expression.Variable("a")), "exp"),
            (Expression.Log10(Expression.Variable("a")), "log10"),
            (Expression.Ln(Expression.Variable("a")), "ln"),
            (Expression.Acosh(Expression.Variable("a")), "acosh"),
            (Expression.Abs(Expression.Variable("a")), "abs"),
            (Expression.Decay(DecayKind.Gauss, Expression.Variable("$score")), "decay"),
        ];

        Assert.Equal(20, cases.Length);
        foreach ((Expression expression, string kind) in cases)
        {
            Assert.Equal(kind, expression.Kind);
        }
    }

    [Fact]
    public void FormulaMult_Native_MultipliesScore()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(
            shard, Expression.Mult([Expression.Variable("$score"), Expression.Constant(2f)]));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(2f, hit.Score));
    }

    [Fact]
    public void FormulaNegate_Native_NegatesScore()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(shard, Expression.Negate(Expression.Variable("$score")));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(-1f, hit.Score));
    }

    [Fact]
    public void FormulaDiv_Native_DividesScore()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(
            shard, Expression.Div(Expression.Variable("$score"), Expression.Constant(2f)));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(0.5f, hit.Score));
    }

    [Fact]
    public void FormulaDiv_ByZeroDefault_Native_FallsBack()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(
            shard, Expression.Div(Expression.Variable("$score"), Expression.Constant(0f), 0f));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(0f, hit.Score));
    }

    [Fact]
    public void FormulaSqrt_Native_ComputesRoot()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(shard, Expression.Sqrt(Expression.Variable("$score")));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(1f, hit.Score));
    }

    [Fact]
    public void FormulaPow_Native_ComputesPower()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(
            shard, Expression.Pow(Expression.Variable("$score"), Expression.Constant(2f)));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(1f, hit.Score));
    }

    [Fact]
    public void FormulaExp_Native_ComputesExponential()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(shard, Expression.Exp(Expression.Variable("$score")));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.True(float.IsFinite(hit.Score)));
    }

    [Fact]
    public void FormulaLog10_Native_ComputesLog()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(shard, Expression.Log10(Expression.Variable("$score")));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(0f, hit.Score));
    }

    [Fact]
    public void FormulaLn_Native_ComputesNaturalLog()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(shard, Expression.Ln(Expression.Variable("$score")));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(0f, hit.Score));
    }

    [Fact]
    public void FormulaAcosh_Native_ComputesInverseHyperbolic()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(shard, Expression.Acosh(Expression.Variable("$score")));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(0f, hit.Score));
    }

    [Fact]
    public void FormulaAbs_Native_ComputesAbsolute()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(
            shard, Expression.Abs(Expression.Negate(Expression.Variable("$score"))));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(1f, hit.Score));
    }

    [Fact]
    public void FormulaConstant_Native_ScoresConstant()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(shard, Expression.Constant(7f));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(7f, hit.Score));
    }

    [Fact]
    public void FormulaVariableScore_Native_ReturnsRawScore()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(shard, Expression.Variable("$score"));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(1f, hit.Score));
    }

    [Fact]
    public void FormulaCondition_Native_ScoresMatches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(
            shard, Expression.Condition(Condition.IsEmpty("missing_key")));
        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public void FormulaGeoDistance_Native_MeasuresDistance()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        GeoPoint origin = new() { Lon = 0, Lat = 0 };
        IReadOnlyList<ScoredPoint> hits = RunFormula(shard, Expression.GeoDistance(origin, "location"));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.True(hit.Score >= 0f));
    }

    [Fact]
    public void FormulaDatetime_Native_AcceptsLiteral()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(shard, Expression.Datetime("2024-01-01T00:00:00Z"));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.True(float.IsFinite(hit.Score)));
    }

    [Fact]
    public void FormulaDatetimeKey_Native_ReadsPayload()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = RunFormula(shard, Expression.DatetimeKey("created"));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.True(float.IsFinite(hit.Score)));
    }

    private static Expression ForgedDeepChain()
    {
        Expression current = Expression.Variable("x");
        for (int i = 0; i < 70; i++)
        {
            current = Expression.Negate(current) with { Size = 1, Depth = 1 };
        }

        return current;
    }

    private sealed record ForgedExpression : Expression
    {
        public override string Kind => "forged";

        public override ulong Size { get; init; } = 1;

        public override uint Depth { get; init; } = 1;
    }

    private static IReadOnlyList<ScoredPoint> RunFormula(EdgeShard shard, Expression expression) =>
        shard.Query(new QueryRequest
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 1f, 0f, 0f]))) },
            ],
            Query = ScoringQuery.Formula(expression),
        });

    private static EdgeShard Seed(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with { Payload = Payload(0.0, 0.0, "2024-01-01T00:00:00Z") },
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f) with { Payload = Payload(1.0, 1.0, "2024-06-01T00:00:00Z") },
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static JsonObject Payload(double lon, double lat, string created) => new()
    {
        ["popularity"] = 7,
        ["location"] = new JsonObject { ["lon"] = lon, ["lat"] = lat },
        ["created"] = created,
    };
}
