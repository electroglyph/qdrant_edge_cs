namespace Qdrant.Edge.Tests;

/// <summary>Fusion and advanced vector queries over the native engine.</summary>
public sealed class EdgeShardFusionNativeTests
{
    [Fact]
    public void Rrf_OverTwoPrefetches_ReturnsFusedSet()
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
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))) },
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([0f, 1f, 0f, 0f]))) },
            ],
            Query = ScoringQuery.CreateFusion(Fusion.Rrf(2)),
        });
        Assert.Equal([1ul, 2ul, 3ul], hits.Select(hit => ((NumPointId)hit.Id).Value).Order());
    }

    [Fact]
    public void Mmr_ReturnsResults()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.Mmr(NamedVector.Dense([1f, 1f, 1f, 1f]), 0.5f, 10),
        });
        Assert.NotEmpty(hits);
    }

    [Fact]
    public void Recommend_BestScore_ReturnsResults()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Recommend([NamedVector.Dense([1f, 0f, 0f, 0f])], [])),
        });
        Assert.NotEmpty(hits);
    }

    [Fact]
    public void Discover_ReturnsResults()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Discover(
                NamedVector.Dense([1f, 0f, 0f, 0f]),
                [new ContextPair { Positive = NamedVector.Dense([1f, 0f, 0f, 0f]), Negative = NamedVector.Dense([0f, 1f, 0f, 0f]) }])),
        });
        Assert.NotEmpty(hits);
    }

    [Fact]
    public void Context_ReturnsResults()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.CreateContext(
                [new ContextPair { Positive = NamedVector.Dense([1f, 0f, 0f, 0f]), Negative = NamedVector.Dense([0f, 1f, 0f, 0f]) }])),
        });
        Assert.NotEmpty(hits);
    }

    [Fact]
    public void Recommend_EmptyPositives_ThrowsInvalidArgument()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Recommend([], [])),
        }));
    }

    private static EdgeShard Seed(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f),
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f),
                TestHelpers.FourDimPoint(3, 0f, 0f, 1f, 1f),
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
