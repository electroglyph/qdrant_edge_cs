using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Formula rescoring over the native engine.</summary>
public sealed class EdgeShardFormulaNativeTests
{
    [Fact]
    public void Formula_ScorePlusPopularity_BoostsPopularPoint()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 1f, 0f, 0f]))) },
            ],
            Query = ScoringQuery.Formula(Expression.Sum([Expression.Variable("$score"), Expression.Variable("popularity")])),
        });
        Assert.Equal(2, hits.Count);
        Assert.Equal(PointId.FromNumber(2), hits[0].Id);
    }

    [Fact]
    public void Formula_Max_ClampsScoresToFloor()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 1f, 0f, 0f]))) },
            ],
            Query = ScoringQuery.Formula(Expression.Max([Expression.Variable("$score"), Expression.Constant(42f)])),
        });
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(42f, hit.Score));
    }

    [Fact]
    public void Formula_Min_ClampsScoresToCeiling()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 1f, 0f, 0f]))) },
            ],
            Query = ScoringQuery.Formula(Expression.Min([Expression.Variable("$score"), Expression.Constant(-42f)])),
        });
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(-42f, hit.Score));
    }

    [Fact]
    public void Formula_Decay_Accepted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 1f, 0f, 0f]))) },
            ],
            Query = ScoringQuery.Formula(Expression.Decay(DecayKind.Gauss, Expression.Variable("$score"))),
        });
        Assert.Equal(2, hits.Count);
    }

    private static EdgeShard Seed(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["popularity"] = 1 } },
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["popularity"] = 10 } },
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
