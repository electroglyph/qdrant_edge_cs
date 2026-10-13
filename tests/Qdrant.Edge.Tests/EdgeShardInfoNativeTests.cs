namespace Qdrant.Edge.Tests;

/// <summary>Info-schema parity pins; no-op when the native engine is absent.</summary>
public sealed class EdgeShardInfoNativeTests
{
    [Fact]
    public void GetInfo_BareKeywordIndex_ReportsSchemaEntry()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        ShardInfo info = shard.GetInfo();
        Assert.Equal(3ul, info.PointsCount);
        Assert.True(info.SegmentsCount >= 1);
        Assert.True(info.PayloadSchema.TryGetValue("title", out PayloadIndexInfo? entry));
        Assert.Equal(PayloadSchemaType.Keyword, entry.DataType);
        Assert.Equal(3ul, entry.Points);
        Assert.Null(entry.Params);
    }

    [Fact]
    public void GetInfo_IndexedField_SurvivesReload()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out string dir);
        shard.Flush();
        shard.Unload();
        using EdgeShard reloaded = EdgeShard.Load(dir);
        ShardInfo info = reloaded.GetInfo();
        Assert.Equal(3ul, info.PointsCount);
        Assert.True(info.PayloadSchema.TryGetValue("title", out PayloadIndexInfo? entry));
        Assert.Equal(PayloadSchemaType.Keyword, entry.DataType);
        Assert.Equal(3ul, entry.Points);
    }

    private static EdgeShard SeedThree(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex("title", PayloadSchemaType.Keyword));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
                {
                    Payload = new System.Text.Json.Nodes.JsonObject { ["title"] = "one" },
                },
                TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f) with
                {
                    Payload = new System.Text.Json.Nodes.JsonObject { ["title"] = "two" },
                },
                TestHelpers.FourDimPoint(3, 3f, 4f, 5f, 6f) with
                {
                    Payload = new System.Text.Json.Nodes.JsonObject { ["title"] = "three" },
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
}
