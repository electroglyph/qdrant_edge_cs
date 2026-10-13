using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Lifecycle pins over the native engine; no-op when it is absent.</summary>
public sealed class EdgeShardLifecycleNativeTests
{
    [Fact]
    public void Create_NestedMissingDirectories_CreatesThem()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = Path.Combine(TestHelpers.TempDir(), "nested", "missing", "shard");
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        Assert.True(Directory.Exists(dir));
        Assert.Equal(dir, shard.Path);
    }

    [Fact]
    public void Load_MissingDirectory_NamesTheDirectory()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = Path.Combine(TestHelpers.TempDir(), "nope");
        EdgeOperationException exception = Assert.Throws<EdgeOperationException>(() => EdgeShard.Load(dir));
        Assert.Contains(dir, exception.Reason);
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Create_OverOccupiedDirectory_ThrowsOperationError()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeShard first = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        first.Unload();
        first.Dispose();
        Assert.Throws<EdgeOperationException>(() => EdgeShard.Create(dir, TestHelpers.FourDimDotConfig()));
    }

    [Fact]
    public void Count_AfterUnload_ThrowsShardClosed()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        try
        {
            shard.Unload();
            Assert.Throws<ShardClosedException>(() => shard.Count(new CountRequest()));
        }
        finally
        {
            shard.Dispose();
        }
    }

    [Fact]
    public void Unload_Twice_DoesNotThrow()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Unload();
        shard.Unload();
    }

    [Fact]
    public void GetConfig_EchoesVectorSizeAndDistance()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        EdgeConfig config = shard.GetConfig();
        Assert.Equal(4ul, config.VectorData[""].Size);
        Assert.Equal(Distance.Dot, config.VectorData[""].Distance);
    }

    [Fact]
    public void SetHnswConfig_Valid_AppliesWithoutThrow()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.SetHnswConfig(new HnswIndexConfig { M = 16, EfConstruct = 100 });
        Assert.Equal(0ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void SetVectorHnswConfig_Valid_AppliesWithoutThrow()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.SetVectorHnswConfig("", new HnswIndexConfig { M = 16, EfConstruct = 100 });
        Assert.Equal(0ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void SetOptimizersConfig_Valid_AppliesWithoutThrow()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.SetOptimizersConfig(new OptimizersConfig { MaxSegmentSizeKb = 1024 });
        Assert.Equal(0ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void SnapshotManifest_ReturnsJsonObject()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        Assert.IsType<JsonObject>(JsonNode.Parse(shard.SnapshotManifest()));
    }

    [Fact]
    public void FlushUnloadLoad_RoundTripsPoints()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f)]));
        shard.Flush();
        shard.Unload();
        shard.Dispose();
        using EdgeShard reloaded = EdgeShard.Load(dir);
        Assert.Equal(1ul, reloaded.Count(new CountRequest()));
    }
}
