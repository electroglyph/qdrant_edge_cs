using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Read-path pins (ranking, filtering, retrieve) over the native engine.</summary>
public sealed class EdgeShardReadsNativeTests
{
    [Fact]
    public void Search_DotRanking_ReturnsIdsInScoreOrder()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = RankedShard(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Search(NearestSearch([1f, 0f, 0f, 0f], 10));
        Assert.Equal([PointId.FromNumber(1), PointId.FromNumber(2), PointId.FromNumber(3)], [hits[0].Id, hits[1].Id, hits[2].Id]);
    }

    [Fact]
    public void Query_DotRanking_ReturnsIdsInScoreOrder()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = RankedShard(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
        });
        Assert.Equal([PointId.FromNumber(1), PointId.FromNumber(2), PointId.FromNumber(3)], [hits[0].Id, hits[1].Id, hits[2].Id]);
    }

    [Fact]
    public void Query_CosineRanking_RanksByDirection()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Cosine },
            },
        };
        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, config);
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f),
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f),
                TestHelpers.FourDimPoint(3, 1f, 1f, 0f, 0f),
            ]));
            IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
            {
                Limit = 10,
                Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
            });
            Assert.Equal([PointId.FromNumber(1), PointId.FromNumber(3), PointId.FromNumber(2)], [hits[0].Id, hits[1].Id, hits[2].Id]);
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    [Fact]
    public void Count_CategoryFilter_ReturnsTwo()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CategorizedShard(out _);
        Assert.Equal(2ul, shard.Count(new CountRequest { Filter = CategoryIs("a") }));
    }

    [Fact]
    public void Scroll_CategoryFilter_ReturnsMatchingIds()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CategorizedShard(out _);
        ScrollResponse page = shard.Scroll(new ScrollRequest { Limit = 10, Filter = CategoryIs("a") });
        Assert.Equal([1ul, 2ul], page.Records.Select(record => ((NumPointId)record.Id).Value).OrderBy(value => value));
    }

    [Fact]
    public void Search_CategoryFilter_ReturnsOnlyMatches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CategorizedShard(out _);
        SearchRequest request = NearestSearch([1f, 0f, 0f, 0f], 10);
        IReadOnlyList<ScoredPoint> hits = shard.Search(request with { Filter = CategoryIs("a") });
        Assert.Equal([1ul, 2ul], hits.Select(hit => ((NumPointId)hit.Id).Value).OrderBy(value => value));
    }

    [Fact]
    public void Retrieve_MissingId_Omitted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CategorizedShard(out _);
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1), PointId.FromNumber(999)],
        });
        Record single = Assert.Single(records);
        Assert.Equal(PointId.FromNumber(1), single.Id);
    }

    internal static EdgeShard RankedShard(out string dir, EdgeConfig? config = null)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, config ?? TestHelpers.FourDimDotConfig());
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

    internal static EdgeShard CategorizedShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                CategorizedPoint(1, "a"),
                CategorizedPoint(2, "a"),
                CategorizedPoint(3, "b"),
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    internal static Filter CategoryIs(string category) => new()
    {
        Must = [Condition.Field(new FieldCondition { Key = "category", Match = Match.CreateText(category) })],
    };

    private static Point CategorizedPoint(ulong id, string category) =>
        TestHelpers.FourDimPoint(id, (float)id, 0f, 0f, 0f) with
        {
            Payload = new JsonObject { ["category"] = category },
        };

    private static SearchRequest NearestSearch(float[] values, ulong limit) => new()
    {
        Query = Query.Nearest(NamedVector.Dense(values)),
        Limit = limit,
    };
}
