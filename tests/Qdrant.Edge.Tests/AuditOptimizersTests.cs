namespace Qdrant.Edge.Tests;

/// <summary>Optimizer config bounds are validated before reaching native.</summary>
public sealed class AuditOptimizersTests
{
    [Fact]
    public void OptimizersConfigValidate_ZeroSegments_ThrowsInvalidArgument()
    {
        OptimizersConfig config = new() { DefaultSegmentNumber = 0 };
        Assert.Throws<EdgeInvalidArgumentException>(config.Validate);
    }

    [Fact]
    public void OptimizersConfigValidate_ThresholdAboveOne_ThrowsInvalidArgument()
    {
        OptimizersConfig config = new() { DeletedThreshold = 1.5 };
        Assert.Throws<EdgeInvalidArgumentException>(config.Validate);
    }
}
