namespace Qdrant.Edge.Tests;

/// <summary>Snapshot error-path parity pins; no-op when the native engine is absent.</summary>
public sealed class EdgeShardSnapshotNativeTests
{
    [Fact]
    public void UpdateFromSnapshot_MissingPath_ErrorsAndKeepsPoints()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Throws<EdgeOperationException>(() =>
            shard.UpdateFromSnapshot(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void UpdateFromSnapshot_CorruptArchive_ErrorsAndKeepsPoints()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        string garbage = Path.Combine(Path.GetTempPath(), "qdrant-edge-garbage-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(garbage, "not a snapshot");
        try
        {
            Assert.Throws<EdgeOperationException>(() => shard.UpdateFromSnapshot(garbage));
            Assert.Equal(3ul, shard.Count(new CountRequest()));
        }
        finally
        {
            File.Delete(garbage);
        }
    }

    [Fact]
    public void UnpackSnapshot_MissingPath_Errors()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        Assert.Throws<EdgeOperationException>(() => EdgeShard.UnpackSnapshot(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            TestHelpers.TempDir()));
    }

    private static EdgeShard SeedThree(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f),
                TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f),
                TestHelpers.FourDimPoint(3, 3f, 4f, 5f, 6f),
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
