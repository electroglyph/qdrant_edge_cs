namespace Qdrant.Edge.Tests;

/// <summary>Quantization load-acceptance parity pins; no-op when native is absent.</summary>
public sealed class EdgeShardQuantizationNativeTests
{
    [Fact]
    public void Load_ScalarQuantization_AcceptedAndEchoed()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateWith(QuantizationConfig.Scalar(new ScalarQuantizationParams
        {
            Type = ScalarType.Int8,
        }), out _);
        EdgeConfig echoed = shard.GetConfig();
        Assert.IsType<ScalarQuantization>(echoed.VectorData[""].QuantizationConfig);
    }

    [Fact]
    public void Load_ProductQuantization_Accepted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateWith(QuantizationConfig.Product(new ProductQuantizationParams
        {
            Compression = CompressionRatio.X16,
        }), out _);
        Assert.Equal(0ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Load_TurboQuantization_Accepted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateWith(QuantizationConfig.Turbo(new TurboQuantizationParams
        {
            Bits = TurboQuantBitSize.Bits4,
        }), out _);
        Assert.Equal(0ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Load_BinaryQuantization_AcceptedAndEchoed()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateWith(QuantizationConfig.Binary(new BinaryQuantizationParams()), out _);
        EdgeConfig echoed = shard.GetConfig();
        Assert.IsType<BinaryQuantization>(echoed.VectorData[""].QuantizationConfig);
    }

    [Fact]
    public void Create_ZeroVectorSize_Rejected()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 0, Distance = Distance.Dot },
            },
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeShard.Create(TestHelpers.TempDir(), config));
    }

    private static EdgeShard CreateWith(QuantizationConfig quantization, out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig
                {
                    Size = 4,
                    Distance = Distance.Dot,
                    QuantizationConfig = quantization,
                },
            },
        };
        return EdgeShard.Create(dir, config);
    }
}
