namespace Qdrant.Edge.Tests;

public sealed class ConfigValidationTests
{
    [Fact]
    public void Validate_EmptyConfig_ThrowsInvalidArgument()
    {
        EdgeConfig config = new();
        Assert.Throws<EdgeInvalidArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_ZeroSizeVector_ThrowsInvalidArgument()
    {
        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 0, Distance = Distance.Dot },
            },
        };
        Assert.Throws<EdgeInvalidArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_OversizedHnsw_ThrowsInvalidArgument()
    {
        HnswIndexConfig hnsw = new() { M = 4096 };
        Assert.Throws<EdgeInvalidArgumentException>(() => hnsw.Validate("hnsw config"));
    }

    [Fact]
    public void Validate_DisabledIntegerIndex_ThrowsInvalidArgument()
    {
        IntegerIndexParams index = new() { Lookup = false, Range = false };
        Assert.Throws<EdgeInvalidArgumentException>(index.Validate);
    }

    [Fact]
    public void Validate_FourDimDotConfig_Passes()
    {
        TestHelpers.FourDimDotConfig().Validate();
    }
}
