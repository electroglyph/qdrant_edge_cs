namespace Qdrant.Edge.Tests;

public sealed class EdgeShardLifecycleTests
{
    [Fact]
    public void Create_WithoutNativeLibrary_ThrowsOperationError()
    {
        if (NativeLoader.IsAvailable)
        {
            return;
        }

        Assert.Throws<EdgeOperationException>(() => EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig()));
    }

    [Fact]
    public void Load_WithoutNativeLibrary_ThrowsOperationError()
    {
        if (NativeLoader.IsAvailable)
        {
            return;
        }

        Assert.Throws<EdgeOperationException>(() => EdgeShard.Load(TestHelpers.TempDir()));
    }

    [Fact]
    public void Create_NullConfig_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => EdgeShard.Create(TestHelpers.TempDir(), null!));
    }

    [Fact]
    public void Create_EmptyPath_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => EdgeShard.Create("  ", TestHelpers.FourDimDotConfig()));
    }
}
