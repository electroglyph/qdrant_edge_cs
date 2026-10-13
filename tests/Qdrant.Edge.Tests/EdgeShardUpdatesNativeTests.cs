using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Update-path pins (delete, payload ops) over the native engine.</summary>
public sealed class EdgeShardUpdatesNativeTests
{
    [Fact]
    public void DeleteThenReload_ReducesCount()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.RankedShard(out string dir);
        shard.Update(UpdateOperation.DeletePoints([PointId.FromNumber(2)]));
        shard.Flush();
        shard.Unload();
        using EdgeShard reloaded = EdgeShard.Load(dir);
        Assert.Equal(2ul, reloaded.Count(new CountRequest()));
        IReadOnlyList<Record> records = reloaded.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1), PointId.FromNumber(2), PointId.FromNumber(3)],
        });
        Assert.Equal([1ul, 3ul], records.Select(record => ((NumPointId)record.Id).Value).OrderBy(id => id));
    }

    [Fact]
    public void DeletePointsByFilter_PersistsAfterReload()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out string dir);
        shard.Update(UpdateOperation.DeletePointsByFilter(EdgeShardReadsNativeTests.CategoryIs("a")));
        shard.Flush();
        shard.Unload();
        using EdgeShard reloaded = EdgeShard.Load(dir);
        Assert.Equal(1ul, reloaded.Count(new CountRequest()));
        Record single = Assert.Single(reloaded.Scroll(new ScrollRequest { Limit = 10 }).Records);
        Assert.Equal(PointId.FromNumber(3), single.Id);
    }

    [Fact]
    public void SetPayload_MergesKeys()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TitledShard(out _);
        shard.Update(UpdateOperation.SetPayload([PointId.FromNumber(1)], """{"tag":"hot"}"""));
        JsonObject payload = RetrievePayload(shard, 1);
        Assert.Equal("one", payload["title"]!.GetValue<string>());
        Assert.Equal("hot", payload["tag"]!.GetValue<string>());
    }

    [Fact]
    public void OverwritePayload_ReplacesWholesale()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TitledShard(out _);
        shard.Update(UpdateOperation.OverwritePayload([PointId.FromNumber(1)], """{"only":"yes"}"""));
        JsonObject payload = RetrievePayload(shard, 1);
        Assert.Equal("yes", payload["only"]!.GetValue<string>());
        Assert.Null(payload["title"]);
    }

    [Fact]
    public void DeletePayload_RemovesOnlyNamedKeys()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = LetteredShard(out _);
        shard.Update(UpdateOperation.DeletePayload([PointId.FromNumber(1)], ["a"]));
        JsonObject payload = RetrievePayload(shard, 1);
        Assert.Null(payload["a"]);
        Assert.Equal(2, payload["b"]!.GetValue<int>());
    }

    [Fact]
    public void ClearPayload_RemovesAllKeys()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = LetteredShard(out _);
        shard.Update(UpdateOperation.ClearPayload([PointId.FromNumber(1)]));
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1)],
        });
        Record single = Assert.Single(records);
        Assert.True(single.Payload is null || single.Payload == "{}");
    }

    [Fact]
    public void ClearPayloadByFilter_AppliesToMatches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = LetteredShard(out _);
        shard.Update(UpdateOperation.ClearPayloadByFilter(new Filter
        {
            Must = [Condition.HasId([PointId.FromNumber(1)])],
        }));
        JsonObject first = RetrievePayload(shard, 1);
        Assert.Empty(first);
        JsonObject second = RetrievePayload(shard, 2);
        Assert.Equal(2, second["b"]!.GetValue<int>());
    }

    private static EdgeShard TitledShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["title"] = "one" },
                },
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static EdgeShard LetteredShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["a"] = 1, ["b"] = 2 },
                },
                TestHelpers.FourDimPoint(2, 2f, 0f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["a"] = 1, ["b"] = 2 },
                },
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static JsonObject RetrievePayload(EdgeShard shard, ulong id)
    {
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(id)],
        });
        Record single = Assert.Single(records);
        return single.Payload is null ? new JsonObject() : (JsonObject)JsonNode.Parse(single.Payload)!;
    }
}
