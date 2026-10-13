namespace Qdrant.Edge.Tests;

/// <summary>Config-merge parity pins; no-op when the native engine is absent.</summary>
public sealed class EdgeShardConfigMergeNativeTests
{
    [Fact]
    public void Load_WithNoConfig_KeepsPersistedVectors()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedOne(out string dir);
        shard.Flush();
        shard.Unload();
        using EdgeShard reloaded = EdgeShard.Load(dir);
        Assert.Equal(1ul, reloaded.Count(new CountRequest()));
        EdgeConfig echoed = reloaded.GetConfig();
        Assert.True(echoed.VectorData.ContainsKey(string.Empty));
        Assert.Equal(4ul, echoed.VectorData[string.Empty].Size);
    }

    [Fact]
    public void Load_WithIncompatibleVectorSize_Fails()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedOne(out string dir);
        shard.Flush();
        shard.Unload();
        EdgeConfig bad = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [string.Empty] = new VectorDataConfig { Size = 8, Distance = Distance.Dot },
            },
        };
        Assert.Throws<EdgeOperationException>(() => EdgeShard.Load(dir, bad));
    }

    private static EdgeShard SeedOne(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard created = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            created.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f)]));
            return created;
        }
        catch
        {
            created.Dispose();
            throw;
        }
    }
}
