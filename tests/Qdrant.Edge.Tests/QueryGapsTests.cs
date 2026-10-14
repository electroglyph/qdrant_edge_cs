using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Validation and JSON pins for every <c>Query.cs</c> arm (§10).</summary>
public sealed class QueryGapsTests
{
    private sealed record ForgedQuery : Query;

    private static NamedVector Dense() => NamedVector.Dense([1f, 2f, 3f, 4f]);

    private static NamedVector InvalidDense() => NamedVector.Dense([]);

    private static ContextPair Pair() => new()
    {
        Positive = Dense(),
        Negative = NamedVector.Dense([5f, 6f, 7f, 8f]),
    };

    private static FeedbackCoefficients Coeffs() => new() { A = 1f, B = 2f, C = 3f };

    private static FeedbackItem ScoredItem(float score) => new() { Vector = Dense(), Score = score };

    [Fact]
    public void QueryNearest_NullVector_Throws()
    {
        Query query = new NearestQuery(null!, null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryNearest_InvalidVector_Throws()
    {
        Query query = Query.Nearest(InvalidDense());
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryNearest_UsingPreserved()
    {
        Query query = Query.Nearest(Dense(), "image");
        var nearest = Assert.IsType<NearestQuery>(query);
        Assert.Equal("image", nearest.Using);
        query.Validate();
        string json = JsonSerializer.Serialize(query, EdgeJsonContext.Deep.Query);
        Query? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Query);
        var backNearest = Assert.IsType<NearestQuery>(back);
        Assert.Equal("image", backNearest.Using);
        Assert.Equal([1f, 2f, 3f, 4f], Assert.IsType<DenseNamedVector>(backNearest.Vector).Values);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.Query));
    }

    [Fact]
    public void QueryRecommend_NullPositives_Throws()
    {
        Query query = new RecommendQuery(null!, [], null, null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryRecommend_NullNegatives_Throws()
    {
        Query query = new RecommendQuery([Dense()], null!, null, null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryRecommend_EmptyNegatives_PassesValidate()
    {
        Query query = Query.Recommend([Dense()], []);
        query.Validate();
    }

    [Fact]
    public void QueryRecommend_NullElementInPositives_Throws()
    {
        Query query = new RecommendQuery([null!], [], null, null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryRecommend_NullElementInNegatives_Throws()
    {
        Query query = new RecommendQuery([Dense()], [null!], null, null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryRecommend_InvalidElement_Propagates()
    {
        Query query = Query.Recommend([InvalidDense()], []);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryRecommend_InvalidElementInNegatives_Propagates()
    {
        Query query = Query.Recommend([Dense()], [InvalidDense()]);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    public static IEnumerable<object?[]> AllStrategies()
    {
        yield return [RecommendStrategy.BestScore];
        yield return [RecommendStrategy.SumScores];
        yield return [null];
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void QueryRecommend_StrategyPreserved(RecommendStrategy? strategy)
    {
        Query query = Query.Recommend([Dense()], [], strategy);
        Assert.Equal(strategy, Assert.IsType<RecommendQuery>(query).Strategy);
        query.Validate();
        string json = JsonSerializer.Serialize(query, EdgeJsonContext.Deep.Query);
        Query? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Query);
        var backRecommend = Assert.IsType<RecommendQuery>(back);
        Assert.Equal(strategy, backRecommend.Strategy);
        Assert.Equal([1f, 2f, 3f, 4f], Assert.IsType<DenseNamedVector>(Assert.Single(backRecommend.Positives)).Values);
        Assert.Empty(backRecommend.Negatives);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.Query));
    }

    [Fact]
    public void QueryDiscover_NullTarget_Throws()
    {
        Query query = new DiscoverQuery(null!, [], null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryDiscover_NullContext_Throws()
    {
        Query query = new DiscoverQuery(Dense(), null!, null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryDiscover_NullPair_Throws()
    {
        Query query = new DiscoverQuery(Dense(), [null!], null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryDiscover_NullPositive_Throws()
    {
        var pair = new ContextPair { Positive = null!, Negative = Dense() };
        Query query = new DiscoverQuery(Dense(), [pair], null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryDiscover_NullNegative_Throws()
    {
        var pair = new ContextPair { Positive = Dense(), Negative = null! };
        Query query = new DiscoverQuery(Dense(), [pair], null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryDiscover_InvalidTarget_Propagates()
    {
        Query query = new DiscoverQuery(InvalidDense(), [], null);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryDiscover_InvalidPairElement_Propagates()
    {
        var pair = new ContextPair { Positive = InvalidDense(), Negative = Dense() };
        Query query = Query.Discover(Dense(), [pair]);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryDiscover_InvalidPairNegativeElement_Propagates()
    {
        var pair = new ContextPair { Positive = Dense(), Negative = InvalidDense() };
        Query query = Query.Discover(Dense(), [pair]);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryDiscover_EmptyContext_Passes()
    {
        Query query = Query.Discover(Dense(), []);
        query.Validate();
    }

    [Fact]
    public void QueryContext_NullContext_Throws()
    {
        Query query = new ContextQuery(null!, null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryContext_NullPair_Throws()
    {
        Query query = new ContextQuery([null!], null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryContext_NullPositive_Throws()
    {
        var pair = new ContextPair { Positive = null!, Negative = Dense() };
        Query query = new ContextQuery([pair], null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryContext_NullNegative_Throws()
    {
        var pair = new ContextPair { Positive = Dense(), Negative = null! };
        Query query = new ContextQuery([pair], null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryContext_InvalidElement_Propagates()
    {
        var pair = new ContextPair { Positive = Dense(), Negative = InvalidDense() };
        Query query = Query.CreateContext([pair]);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryContext_InvalidPositiveElement_Propagates()
    {
        var pair = new ContextPair { Positive = InvalidDense(), Negative = Dense() };
        Query query = Query.CreateContext([pair]);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_NullTarget_Throws()
    {
        Query query = new FeedbackQuery(null!, [], Coeffs(), null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_NullFeedback_Throws()
    {
        Query query = new FeedbackQuery(Dense(), null!, Coeffs(), null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_NullCoeffs_Throws()
    {
        Query query = new FeedbackQuery(Dense(), [], null!, null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_InvalidTarget_Propagates()
    {
        Query query = new FeedbackQuery(InvalidDense(), [], Coeffs(), null);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_NullItem_Throws()
    {
        Query query = new FeedbackQuery(Dense(), [null!], Coeffs(), null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_NullItemVector_Throws()
    {
        var item = new FeedbackItem { Vector = null!, Score = 1f };
        Query query = new FeedbackQuery(Dense(), [item], Coeffs(), null);
        Assert.Throws<ArgumentNullException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_InvalidItemVector_Propagates()
    {
        var item = new FeedbackItem { Vector = InvalidDense(), Score = 1f };
        Query query = Query.CreateFeedback(Dense(), [item], Coeffs());
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_NaNScore_Throws()
    {
        Query query = Query.CreateFeedback(Dense(), [ScoredItem(float.NaN)], Coeffs());
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_PosInfScore_Throws()
    {
        Query query = Query.CreateFeedback(Dense(), [ScoredItem(float.PositiveInfinity)], Coeffs());
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_NegInfScore_Throws()
    {
        Query query = Query.CreateFeedback(Dense(), [ScoredItem(float.NegativeInfinity)], Coeffs());
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_NaNCoeffA_Throws()
    {
        var coeffs = Coeffs() with { A = float.NaN };
        Query query = Query.CreateFeedback(Dense(), [], coeffs);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_NaNCoeffB_Throws()
    {
        var coeffs = Coeffs() with { B = float.NaN };
        Query query = Query.CreateFeedback(Dense(), [], coeffs);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_NaNCoeffC_Throws()
    {
        var coeffs = Coeffs() with { C = float.NaN };
        Query query = Query.CreateFeedback(Dense(), [], coeffs);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void QueryFeedback_InfCoeff_Throws()
    {
        var coeffs = Coeffs() with { A = float.PositiveInfinity };
        Query query = Query.CreateFeedback(Dense(), [], coeffs);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    public static IEnumerable<object?[]> InfCoeffVariants()
    {
        yield return [new FeedbackCoefficients { A = float.NegativeInfinity, B = 2f, C = 3f }];
        yield return [new FeedbackCoefficients { A = 1f, B = float.PositiveInfinity, C = 3f }];
        yield return [new FeedbackCoefficients { A = 1f, B = float.NegativeInfinity, C = 3f }];
        yield return [new FeedbackCoefficients { A = 1f, B = 2f, C = float.PositiveInfinity }];
        yield return [new FeedbackCoefficients { A = 1f, B = 2f, C = float.NegativeInfinity }];
    }

    [Theory]
    [MemberData(nameof(InfCoeffVariants))]
    public void QueryFeedback_InfCoeffVariants_Throws(FeedbackCoefficients coeffs)
    {
        Query query = Query.CreateFeedback(Dense(), [], coeffs);
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void Query_UnknownShape_Throws()
    {
        Query forged = new ForgedQuery();
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void Query_All5Kinds_RoundTripThroughJson()
    {
        Query[] cases =
        [
            Query.Nearest(Dense()),
            Query.Recommend([Dense()], [NamedVector.Dense([5f])], RecommendStrategy.SumScores),
            Query.Discover(Dense(), [Pair()]),
            Query.CreateContext([Pair()]),
            Query.CreateFeedback(Dense(), [ScoredItem(1.5f)], Coeffs()),
        ];
        foreach (Query original in cases)
        {
            string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Query);
            Query? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Query);
            Assert.IsType(original.GetType(), back);
            Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.Query));
        }
    }

    [Fact]
    public void FusionRrf_NullWeights_Passes()
    {
        ScoringQuery.CreateFusion(Fusion.Rrf(2)).Validate();
    }

    [Fact]
    public void FusionRrf_EmptyWeights_Passes()
    {
        ScoringQuery.CreateFusion(Fusion.Rrf(2, [])).Validate();
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void FusionRrf_NonFiniteWeight_Throws(float bad)
    {
        ScoringQuery query = ScoringQuery.CreateFusion(Fusion.Rrf(2, [1f, bad]));
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void FusionDbsf_Passes()
    {
        ScoringQuery.CreateFusion(Fusion.Dbsf()).Validate();
    }

    [Fact]
    public void OrderBy_NullKey_Throws()
    {
        ScoringQuery query = ScoringQuery.CreateOrderBy(new OrderBy { Key = null! });
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(query.Validate);
        Assert.Equal("Key", ex.ParamName);
    }

    [Fact]
    public void OrderBy_Desc_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedRankScore(out _);
        ScrollResponse page = shard.Scroll(new ScrollRequest
        {
            Limit = 10,
            OrderBy = new OrderBy { Key = "rank", Direction = Direction.Desc },
        });
        Assert.Equal(
            [PointId.FromNumber(1), PointId.FromNumber(3), PointId.FromNumber(2)],
            page.Records.Select(record => record.Id).ToList());
        Assert.Equal(
            [OrderValue.Int(3), OrderValue.Int(2), OrderValue.Int(1)],
            page.Records.Select(record => record.OrderValue).ToList());
    }

    [Fact]
    public void OrderBy_FloatStartFrom_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedRankScore(out _);
        ScrollResponse page = shard.Scroll(new ScrollRequest
        {
            Limit = 10,
            OrderBy = new OrderBy
            {
                Key = "score",
                Direction = Direction.Asc,
                StartFrom = new FloatStartFrom(2.5),
            },
        });
        Assert.Equal(
            [PointId.FromNumber(3), PointId.FromNumber(1)],
            page.Records.Select(record => record.Id).ToList());
        Assert.Equal(
            [OrderValue.Float(2.5), OrderValue.Float(3.5)],
            page.Records.Select(record => record.OrderValue).ToList());
    }

    [Fact]
    public void OrderBy_DatetimeStartFrom_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedDatetime(out _);
        ScrollResponse full = shard.Scroll(new ScrollRequest
        {
            Limit = 10,
            OrderBy = new OrderBy { Key = "ts", Direction = Direction.Asc },
        });
        Assert.Equal(
            [PointId.FromNumber(2), PointId.FromNumber(3), PointId.FromNumber(1)],
            full.Records.Select(record => record.Id).ToList());
        ScrollResponse page = shard.Scroll(new ScrollRequest
        {
            Limit = 10,
            OrderBy = new OrderBy
            {
                Key = "ts",
                Direction = Direction.Asc,
                StartFrom = new DatetimeStartFrom("2024-01-02T00:00:00Z"),
            },
        });
        Assert.Equal(
            [PointId.FromNumber(3), PointId.FromNumber(1)],
            page.Records.Select(record => record.Id).ToList());
    }

    private static EdgeShard SeedRankScore(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex("rank", PayloadSchemaType.Integer));
            shard.Update(UpdateOperation.CreateFieldIndex("score", PayloadSchemaType.Float));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["rank"] = 3, ["score"] = 3.5 } },
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["rank"] = 1, ["score"] = 1.5 } },
                TestHelpers.FourDimPoint(3, 0f, 0f, 1f, 0f) with { Payload = new JsonObject { ["rank"] = 2, ["score"] = 2.5 } },
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static EdgeShard SeedDatetime(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex("ts", PayloadSchemaType.Datetime));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["ts"] = "2024-01-03T00:00:00Z" } },
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["ts"] = "2024-01-01T00:00:00Z" } },
                TestHelpers.FourDimPoint(3, 0f, 0f, 1f, 0f) with { Payload = new JsonObject { ["ts"] = "2024-01-02T00:00:00Z" } },
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
