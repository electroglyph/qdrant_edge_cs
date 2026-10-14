using System.Text.Json;

namespace Qdrant.Edge.Tests;

public sealed class ConfigGapsTests
{
    [Fact]
    public void HnswValidate_NullScope_ThrowsArgumentNull()
    {
        HnswIndexConfig hnsw = new();
        Assert.Throws<ArgumentNullException>(() => hnsw.Validate(null!));
    }

    [Fact]
    public void HnswValidate_EmptyScope_ThrowsArgument()
    {
        HnswIndexConfig hnsw = new();
        ArgumentException ex = Assert.Throws<ArgumentException>(() => hnsw.Validate(string.Empty));
        Assert.Equal(typeof(ArgumentException), ex.GetType());
    }

    [Fact]
    public void HnswValidate_MAtCap_Passes()
    {
        new HnswIndexConfig { M = EdgeValidation.MaxHnswM }.Validate("hnsw");
    }

    [Fact]
    public void HnswValidate_MZero_Passes()
    {
        new HnswIndexConfig { M = 0 }.Validate("hnsw");
    }

    [Fact]
    public void HnswValidate_PayloadMNull_Passes()
    {
        new HnswIndexConfig { PayloadM = null }.Validate("hnsw");
    }

    [Fact]
    public void HnswValidate_PayloadMAtCap_Passes()
    {
        new HnswIndexConfig { PayloadM = EdgeValidation.MaxHnswM }.Validate("hnsw");
    }

    [Fact]
    public void HnswValidate_PayloadMAboveCap_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(
            () => new HnswIndexConfig { PayloadM = EdgeValidation.MaxHnswM + 1 }.Validate("hnsw"));
    }

    [Fact]
    public void HnswValidate_EfConstructBoundaries_Pass()
    {
        new HnswIndexConfig { EfConstruct = EdgeValidation.MinHnswEfConstruct }.Validate("hnsw");
        new HnswIndexConfig { EfConstruct = EdgeValidation.MaxHnswEfConstruct }.Validate("hnsw");
    }

    [Fact]
    public void EfConstruct_Ef3_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(
            () => new HnswIndexConfig { EfConstruct = EdgeValidation.MinHnswEfConstruct - 1 }.Validate("hnsw"));
    }

    [Fact]
    public void EfConstruct_Ef100001_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(
            () => new HnswIndexConfig { EfConstruct = EdgeValidation.MaxHnswEfConstruct + 1 }.Validate("hnsw"));
    }

    [Fact]
    public void HnswValidate_MaxIndexingThreadsAtCap_Passes()
    {
        new HnswIndexConfig { MaxIndexingThreads = EdgeValidation.MaxHnswIndexingThreads }.Validate("hnsw");
    }

    [Fact]
    public void HnswValidate_MaxIndexingThreadsAboveCap_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(
            () => new HnswIndexConfig { MaxIndexingThreads = EdgeValidation.MaxHnswIndexingThreads + 1 }.Validate("hnsw"));
    }

    [Fact]
    public void HnswValidate_MaxIndexingThreadsZero_Passes()
    {
        new HnswIndexConfig { MaxIndexingThreads = 0 }.Validate("hnsw");
    }

    [Fact]
    public void VectorDataValidate_NullHnsw_Passes()
    {
        new VectorDataConfig { Size = 4, Distance = Distance.Dot, HnswConfig = null }.Validate("vector");
    }

    [Fact]
    public void VectorDataValidate_InvalidHnsw_Throws()
    {
        VectorDataConfig field = new()
        {
            Size = 4,
            Distance = Distance.Dot,
            HnswConfig = new HnswIndexConfig { M = EdgeValidation.MaxHnswM * 2 },
        };
        EdgeInvalidArgumentException ex = Assert.Throws<EdgeInvalidArgumentException>(() => field.Validate("my-scope"));
        Assert.Contains("my-scope", ex.Message);
    }

    [Fact]
    public void VectorDataValidate_SizeBoundaries()
    {
        new VectorDataConfig { Size = EdgeValidation.MinVectorSize, Distance = Distance.Dot }.Validate("vector");
        new VectorDataConfig { Size = EdgeValidation.MaxVectorSize, Distance = Distance.Dot }.Validate("vector");
        Assert.Throws<EdgeInvalidArgumentException>(
            () => new VectorDataConfig { Size = EdgeValidation.MaxVectorSize + 1, Distance = Distance.Dot }.Validate("vector"));
    }

    [Fact]
    public void OptimizersValidate_AllNull_Passes()
    {
        new OptimizersConfig().Validate();
    }

    [Fact]
    public void OptimizersValidate_DeletedThresholdBoundaries()
    {
        new OptimizersConfig { DeletedThreshold = 0.0 }.Validate();
        new OptimizersConfig { DeletedThreshold = 1.0 }.Validate();
        foreach (double bad in new[] { -0.1, 1.0001, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<EdgeInvalidArgumentException>(() => new OptimizersConfig { DeletedThreshold = bad }.Validate());
        }
    }

    [Fact]
    public void OptimizersValidate_VacuumAtCap_Passes()
    {
        new OptimizersConfig { VacuumMinVectorNumber = EdgeValidation.MaxResultCount }.Validate();
    }

    [Fact]
    public void OptimizersValidate_VacuumAboveCap_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(
            () => new OptimizersConfig { VacuumMinVectorNumber = EdgeValidation.MaxResultCount + 1 }.Validate());
    }

    [Fact]
    public void OptimizersValidate_VacuumZero_Passes()
    {
        new OptimizersConfig { VacuumMinVectorNumber = 0 }.Validate();
    }

    [Fact]
    public void OptimizersValidate_DefaultSegmentOne_Passes()
    {
        new OptimizersConfig { DefaultSegmentNumber = 1 }.Validate();
    }

    [Fact]
    public void OptimizersValidate_DefaultSegmentAtCap_Passes()
    {
        new OptimizersConfig { DefaultSegmentNumber = EdgeValidation.MaxResultCount }.Validate();
    }

    [Fact]
    public void OptimizersValidate_DefaultSegmentAboveCap_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(
            () => new OptimizersConfig { DefaultSegmentNumber = EdgeValidation.MaxResultCount + 1 }.Validate());
    }

    [Fact]
    public void OptimizersValidate_MaxSegmentSizeOne_Passes()
    {
        new OptimizersConfig { MaxSegmentSizeKb = 1 }.Validate();
    }

    [Fact]
    public void OptimizersValidate_MaxSegmentSizeZero_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(
            () => new OptimizersConfig { MaxSegmentSizeKb = 0 }.Validate());
    }

    [Fact]
    public void OptimizersValidate_IndexingThresholdOne_Passes()
    {
        new OptimizersConfig { IndexingThresholdKb = 1 }.Validate();
    }

    [Fact]
    public void OptimizersValidate_IndexingThresholdZero_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(
            () => new OptimizersConfig { IndexingThresholdKb = 0 }.Validate());
    }

    [Fact]
    public void OptimizersValidate_PreventUnoptimizedEitherValue_Passes()
    {
        new OptimizersConfig { PreventUnoptimized = true }.Validate();
        new OptimizersConfig { PreventUnoptimized = false }.Validate();
        new OptimizersConfig { PreventUnoptimized = null }.Validate();
    }

    [Fact]
    public void EdgeConfigValidate_SparseOnly_Passes()
    {
        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>(),
            SparseVectorData = new Dictionary<string, SparseVectorDataConfig>
            {
                ["text"] = new SparseVectorDataConfig(),
            },
        };
        config.Validate();
    }

    [Fact]
    public void EdgeConfigValidate_BothFamilies_Passes()
    {
        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
            },
            SparseVectorData = new Dictionary<string, SparseVectorDataConfig>
            {
                ["text"] = new SparseVectorDataConfig(),
            },
        };
        config.Validate();
    }

    [Fact]
    public void EdgeConfigValidate_NullFieldValue_ThrowsArgumentNull()
    {
        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = null!,
            },
        };
        Assert.Throws<ArgumentNullException>(config.Validate);
    }

    [Fact]
    public void EdgeConfigValidate_InvalidFieldMessage_ContainsScope()
    {
        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 0, Distance = Distance.Dot },
            },
        };
        EdgeInvalidArgumentException ex = Assert.Throws<EdgeInvalidArgumentException>(config.Validate);
        Assert.Contains("vector field ", ex.Message);
    }

    [Fact]
    public void EdgeConfigValidate_EmptyStringKey_Allowed()
    {
        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
            },
        };
        config.Validate();
    }

    [Fact]
    public void SparseVectorDataConfig_NoValidate_Serializes()
    {
        SparseVectorDataConfig config = new()
        {
            FullScanThreshold = 1_000,
            Datatype = VectorStorageDatatype.Float32,
            Modifier = Modifier.Idf,
            Memory = Memory.Pinned,
        };
        string json = JsonSerializer.Serialize(config, EdgeJsonContext.Deep.SparseVectorDataConfig);
        SparseVectorDataConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SparseVectorDataConfig);
        Assert.Equal(config, back);
    }
}
