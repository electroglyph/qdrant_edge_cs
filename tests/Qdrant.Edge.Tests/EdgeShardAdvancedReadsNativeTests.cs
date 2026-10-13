using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Advanced read-path pins (pagination, batch, groups, vectors) over the native engine.</summary>
public sealed class EdgeShardAdvancedReadsNativeTests
{
    [Fact]
    public void Scroll_PagesOfTwo_CoversAllPointsOnce()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = FiveShard(out _);
        List<ulong> ids = [];
        PointId? offset = null;
        int pages = 0;
        do
        {
            ScrollResponse page = shard.Scroll(new ScrollRequest { Limit = 2, Offset = offset });
            pages++;
            ids.AddRange(page.Records.Select(record => ((NumPointId)record.Id).Value));
            offset = page.NextOffset;
        }
        while (offset is not null);
        Assert.Equal(3, pages);
        Assert.Equal([1ul, 2ul, 3ul, 4ul, 5ul], ids.OrderBy(id => id));
    }

    [Fact]
    public void QueryBatch_MatchesSoloQueries()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.RankedShard(out _);
        QueryRequest first = RankedQuery(1);
        QueryRequest second = RankedQuery(1);
        QueryRequest third = RankedQuery(2);
        IReadOnlyList<IReadOnlyList<ScoredPoint>> batched = shard.QueryBatch(new QueryBatchRequest
        {
            Queries = [first, second, third],
        });
        Assert.Equal(3, batched.Count);
        Assert.Equal([1u, 1u, 2u], batched.Select(hits => (uint)hits.Count));
        Assert.Equal(SoloIds(shard, first), BatchIds(batched[0]));
        Assert.Equal(SoloIds(shard, second), BatchIds(batched[1]));
        Assert.Equal(SoloIds(shard, third), BatchIds(batched[2]));
    }

    [Fact]
    public void QueryBatch_Empty_ReturnsEmpty()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.RankedShard(out _);
        IReadOnlyList<IReadOnlyList<ScoredPoint>> batched = shard.QueryBatch(new QueryBatchRequest
        {
            Queries = [],
        });
        Assert.Empty(batched);
    }

    [Fact]
    public void QueryGroups_ByCategory_ReturnsTwoGroups()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        IReadOnlyList<Group> groups = shard.QueryGroups(new GroupRequest
        {
            Query = RankedQuery(10),
            GroupBy = "category",
            Groups = 10,
            GroupSize = 1,
        });
        Assert.Equal(2, groups.Count);
        Assert.Equal(["a", "b"], groups.Select(group => ((StringGroupId)group.Key).Value).OrderBy(key => key));
        Assert.All(groups, group => Assert.Single(group.Hits));
    }

    [Fact]
    public void Retrieve_WithVectors_ReturnsStoredValues()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.RankedShard(out _);
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1)],
            WithVector = WithVector.Bool(true),
        });
        Record single = Assert.Single(records);
        JsonArray? values = JsonNode.Parse(single.Vector!)!["Values"] as JsonArray;
        Assert.NotNull(values);
        Assert.Equal([1f, 0f, 0f, 0f], values.Select(node => node!.GetValue<float>()));
    }

    private static EdgeShard FiveShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f),
                TestHelpers.FourDimPoint(2, 2f, 0f, 0f, 0f),
                TestHelpers.FourDimPoint(3, 3f, 0f, 0f, 0f),
                TestHelpers.FourDimPoint(4, 4f, 0f, 0f, 0f),
                TestHelpers.FourDimPoint(5, 5f, 0f, 0f, 0f),
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static QueryRequest RankedQuery(ulong limit) => new()
    {
        Limit = limit,
        Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
    };

    private static List<ulong> SoloIds(EdgeShard shard, QueryRequest request) =>
        shard.Query(request).Select(hit => ((NumPointId)hit.Id).Value).ToList();

    private static List<ulong> BatchIds(IReadOnlyList<ScoredPoint> hits) =>
        hits.Select(hit => ((NumPointId)hit.Id).Value).ToList();
}
