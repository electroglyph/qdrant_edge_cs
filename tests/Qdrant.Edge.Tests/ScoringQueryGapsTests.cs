using System.Text.Json;

namespace Qdrant.Edge.Tests;

/// <summary>Validation and JSON pins for every <c>ScoringQuery.cs</c> arm (§11).</summary>
public sealed class ScoringQueryGapsTests
{
    private sealed record ForgedScoringQuery : ScoringQuery;

    private static NamedVector Dense() => NamedVector.Dense([1f, 2f, 3f, 4f]);

    private static NamedVector InvalidDense() => NamedVector.Dense([]);

    [Fact]
    public void ScoringVector_NullQuery_Throws()
    {
        ScoringQuery query = new VectorScoringQuery(null!);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void ScoringVector_InvalidQuery_Propagates()
    {
        ScoringQuery query = ScoringQuery.CreateVector(Query.Nearest(InvalidDense()));
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void ScoringFusion_Null_Throws()
    {
        ScoringQuery query = new FusionScoringQuery(null!);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ScoringFusion_NonFiniteWeight_Throws(float bad)
    {
        // Direct CheckFinite throw inside the weight loop, not nested Validate propagation.
        ScoringQuery query = ScoringQuery.CreateFusion(Fusion.Rrf(2, [1f, bad]));
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void ScoringOrderBy_Null_Throws()
    {
        ScoringQuery query = new OrderByScoringQuery(null!);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void ScoringOrderBy_EmptyKey_Throws()
    {
        ScoringQuery query = ScoringQuery.CreateOrderBy(new OrderBy { Key = string.Empty });
        Assert.Throws<ArgumentException>(query.Validate);
    }

    [Fact]
    public void ScoringOrderBy_WhitespaceKey_Throws()
    {
        ScoringQuery query = ScoringQuery.CreateOrderBy(new OrderBy { Key = "  " });
        Assert.Throws<ArgumentException>(query.Validate);
    }

    [Fact]
    public void ScoringOrderBy_SyntaxInvalidKey_PassesValidate()
    {
        // Client-side only rejects null/whitespace; JSON-path syntax is engine-checked.
        ScoringQuery query = ScoringQuery.CreateOrderBy(new OrderBy { Key = "!!!" });
        query.Validate();
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ScoringOrderBy_FloatStartFromNonFinite_Throws(double bad)
    {
        ScoringQuery query = ScoringQuery.CreateOrderBy(new OrderBy
        {
            Key = "score",
            StartFrom = new FloatStartFrom(bad),
        });
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void ScoringOrderBy_IntegerStartFrom_PassesWithoutValidation()
    {
        ScoringQuery query = ScoringQuery.CreateOrderBy(new OrderBy
        {
            Key = "rank",
            StartFrom = new IntegerStartFrom(long.MinValue),
        });
        query.Validate();
    }

    [Fact]
    public void ScoringOrderBy_DatetimeStartFrom_PassesWithoutValidation()
    {
        ScoringQuery query = ScoringQuery.CreateOrderBy(new OrderBy
        {
            Key = "ts",
            StartFrom = new DatetimeStartFrom("not-a-date"),
        });
        query.Validate();
    }

    [Fact]
    public void ScoringFormula_NullExpression_Throws()
    {
        ScoringQuery query = new FormulaScoringQuery(null!, null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void ScoringFormula_InvalidExpression_Propagates()
    {
        ScoringQuery query = ScoringQuery.Formula(new ConstantExpression(float.NaN));
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void ScoringFormula_DefaultsPreserved()
    {
        ScoringQuery noDefaults = ScoringQuery.Formula(Expression.Constant(1f));
        noDefaults.Validate();
        Assert.Null(Assert.IsType<FormulaScoringQuery>(noDefaults).Defaults);
        string noDefaultsJson = JsonSerializer.Serialize(noDefaults, EdgeJsonContext.Deep.ScoringQuery);
        ScoringQuery? noDefaultsBack = JsonSerializer.Deserialize(noDefaultsJson, EdgeJsonContext.Deep.ScoringQuery);
        Assert.Null(Assert.IsType<FormulaScoringQuery>(noDefaultsBack).Defaults);
        Assert.Equal(noDefaultsJson, JsonSerializer.Serialize(noDefaultsBack, EdgeJsonContext.Deep.ScoringQuery));

        var defaults = new Dictionary<string, string> { ["p"] = "1" };
        ScoringQuery withDefaults = ScoringQuery.Formula(Expression.Variable("$score"), defaults);
        withDefaults.Validate();
        Assert.Equal(defaults, Assert.IsType<FormulaScoringQuery>(withDefaults).Defaults);
        string json = JsonSerializer.Serialize(withDefaults, EdgeJsonContext.Deep.ScoringQuery);
        ScoringQuery? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScoringQuery);
        Assert.Equal(defaults, Assert.IsType<FormulaScoringQuery>(back).Defaults);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.ScoringQuery));
    }

    [Fact]
    public void ScoringMmr_NullVector_Throws()
    {
        ScoringQuery query = new MmrScoringQuery(null!, 0.5f, 10, null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void ScoringMmr_InvalidVector_Propagates()
    {
        ScoringQuery query = ScoringQuery.Mmr(InvalidDense(), 0.5f, 10);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void ScoringMmr_LambdaBoundaries_Pass(float lambda)
    {
        ScoringQuery.Mmr(Dense(), lambda, 10).Validate();
    }

    [Theory]
    [InlineData(-0.001f)]
    [InlineData(1.001f)]
    public void ScoringMmr_LambdaOutOfRange_Throws(float lambda)
    {
        ScoringQuery query = ScoringQuery.Mmr(Dense(), lambda, 10);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ScoringMmr_LambdaNonFinite_Throws(float lambda)
    {
        // NaN compares false against both range arms, so a throw pins
        // CheckFinite running before the range check.
        ScoringQuery query = ScoringQuery.Mmr(Dense(), lambda, 10);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void ScoringMmr_CandidatesAtCap_Passes()
    {
        ScoringQuery.Mmr(Dense(), 0.5f, EdgeValidation.MaxResultCount).Validate();
    }

    [Fact]
    public void ScoringMmr_CandidatesAboveCap_Throws()
    {
        ScoringQuery query = ScoringQuery.Mmr(Dense(), 0.5f, EdgeValidation.MaxResultCount + 1);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void ScoringMmr_UsingPreserved()
    {
        ScoringQuery named = ScoringQuery.Mmr(Dense(), 0.5f, 10, "image");
        named.Validate();
        Assert.Equal("image", Assert.IsType<MmrScoringQuery>(named).Using);
        string json = JsonSerializer.Serialize(named, EdgeJsonContext.Deep.ScoringQuery);
        ScoringQuery? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScoringQuery);
        Assert.Equal("image", Assert.IsType<MmrScoringQuery>(back).Using);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.ScoringQuery));

        ScoringQuery unnamed = ScoringQuery.Mmr(Dense(), 0.5f, 10);
        unnamed.Validate();
        Assert.Null(Assert.IsType<MmrScoringQuery>(unnamed).Using);
    }

    [Fact]
    public void ScoringSample_PassesWithoutValidation()
    {
        ScoringQuery.CreateSample(Sample.Random).Validate();
    }

    [Fact]
    public void Scoring_UnknownShape_Throws()
    {
        ScoringQuery forged = new ForgedScoringQuery();
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void Scoring_All6Kinds_RoundTripThroughJson()
    {
        ScoringQuery[] cases =
        [
            ScoringQuery.CreateVector(Query.Nearest(Dense())),
            ScoringQuery.CreateFusion(Fusion.Rrf(2)),
            ScoringQuery.CreateOrderBy(new OrderBy { Key = "rank" }),
            ScoringQuery.Formula(Expression.Sum([Expression.Variable("$score"), Expression.Constant(1f)])),
            ScoringQuery.Mmr(Dense(), 0.5f, 10),
            ScoringQuery.CreateSample(Sample.Random),
        ];
        foreach (ScoringQuery original in cases)
        {
            original.Validate();
            string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScoringQuery);
            ScoringQuery? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScoringQuery);
            Assert.IsType(original.GetType(), back);
            Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.ScoringQuery));
        }
    }

    [Fact]
    public void SearchParams_AllFields_Preserved()
    {
        SearchParams defaults = new();
        Assert.Null(defaults.HnswEf);
        Assert.False(defaults.Exact);
        Assert.False(defaults.IndexedOnly);

        SearchParams original = new() { HnswEf = 16, Exact = true, IndexedOnly = true };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SearchParams);
        SearchParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SearchParams);
        Assert.NotNull(back);
        Assert.Equal(16ul, back.HnswEf);
        Assert.True(back.Exact);
        Assert.True(back.IndexedOnly);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.SearchParams));
    }

    [Fact]
    public void SearchParams_Exact_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedSmallShard(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
            Params = new SearchParams { Exact = true },
        });
        Assert.NotEmpty(hits);
    }

    [Fact]
    public void SearchParams_HnswEf_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedSmallShard(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
            Params = new SearchParams { HnswEf = 16 },
        });
        Assert.NotEmpty(hits);
    }

    [Fact]
    public void SearchParams_IndexedOnly_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedSmallShard(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Search(new SearchRequest
        {
            Query = Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f])),
            Limit = 10,
            Params = new SearchParams { IndexedOnly = true },
        });
        Assert.NotEmpty(hits);
    }

    private static EdgeShard SeedSmallShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f),
                TestHelpers.FourDimPoint(2, 0.5f, 0f, 0f, 0f),
                TestHelpers.FourDimPoint(3, 0f, 1f, 0f, 0f),
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }
}
