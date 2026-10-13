namespace Qdrant.Edge;

/// <summary>Vector distance metric.</summary>
public enum Distance
{
    /// <summary>Cosine distance.</summary>
    Cosine,

    /// <summary>Euclidean distance.</summary>
    Euclid,

    /// <summary>Dot-product distance.</summary>
    Dot,

    /// <summary>Manhattan distance.</summary>
    Manhattan,
}

/// <summary>Vector storage datatype.</summary>
public enum VectorStorageDatatype
{
    /// <summary>32-bit float.</summary>
    Float32,

    /// <summary>16-bit float.</summary>
    Float16,

    /// <summary>8-bit unsigned integer.</summary>
    Uint8,

    /// <summary>Turbo 4-bit.</summary>
    Turbo4,
}

/// <summary>Multi-vector comparator.</summary>
public enum MultiVectorComparator
{
    /// <summary>Maximum similarity.</summary>
    MaxSim,
}

/// <summary>Multi-vector configuration.</summary>
public sealed record MultiVectorConfig
{
    /// <summary>Comparator.</summary>
    public required MultiVectorComparator Comparator { get; init; }
}

/// <summary>Scalar quantization type.</summary>
public enum ScalarType
{
    /// <summary>8-bit integer.</summary>
    Int8,
}

/// <summary>Product quantization compression ratio.</summary>
public enum CompressionRatio
{
    /// <summary>4x compression.</summary>
    X4,

    /// <summary>8x compression.</summary>
    X8,

    /// <summary>16x compression.</summary>
    X16,

    /// <summary>32x compression.</summary>
    X32,

    /// <summary>64x compression.</summary>
    X64,
}

/// <summary>Binary quantization encoding.</summary>
public enum BinaryQuantizationEncoding
{
    /// <summary>One bit.</summary>
    OneBit,

    /// <summary>Two bits.</summary>
    TwoBits,

    /// <summary>One and a half bits.</summary>
    OneAndHalfBits,
}

/// <summary>Binary quantization query encoding.</summary>
public enum BinaryQuantizationQueryEncoding
{
    /// <summary>Default encoding.</summary>
    Default,

    /// <summary>Binary encoding.</summary>
    Binary,

    /// <summary>4-bit scalar encoding.</summary>
    Scalar4Bits,

    /// <summary>8-bit scalar encoding.</summary>
    Scalar8Bits,
}

/// <summary>Memory placement.</summary>
public enum Memory
{
    /// <summary>Cold storage.</summary>
    Cold,

    /// <summary>Cached in memory.</summary>
    Cached,

    /// <summary>Pinned in memory.</summary>
    Pinned,
}

/// <summary>Scalar quantization parameters.</summary>
public sealed record ScalarQuantizationParams
{
    /// <summary>Scalar type.</summary>
    public required ScalarType Type { get; init; }

    /// <summary>Quantile.</summary>
    public float? Quantile { get; init; }

    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }
}

/// <summary>Product quantization parameters.</summary>
public sealed record ProductQuantizationParams
{
    /// <summary>Compression ratio.</summary>
    public required CompressionRatio Compression { get; init; }

    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }
}

/// <summary>Binary quantization parameters.</summary>
public sealed record BinaryQuantizationParams
{
    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Storage encoding.</summary>
    public BinaryQuantizationEncoding? Encoding { get; init; }

    /// <summary>Query encoding.</summary>
    public BinaryQuantizationQueryEncoding? QueryEncoding { get; init; }
}

/// <summary>Turbo quantization bit size.</summary>
public enum TurboQuantBitSize
{
    /// <summary>1 bit.</summary>
    Bits1,

    /// <summary>1.5 bits.</summary>
    Bits1Point5,

    /// <summary>2 bits.</summary>
    Bits2,

    /// <summary>4 bits.</summary>
    Bits4,
}

/// <summary>Turbo quantization parameters.</summary>
public sealed record TurboQuantizationParams
{
    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Bit size.</summary>
    public TurboQuantBitSize? Bits { get; init; }
}

/// <summary>Quantization configuration union.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ScalarQuantization), "scalar")]
[JsonDerivedType(typeof(ProductQuantization), "product")]
[JsonDerivedType(typeof(BinaryQuantization), "binary")]
[JsonDerivedType(typeof(TurboQuantization), "turbo")]
public abstract record QuantizationConfig
{
    /// <summary>Creates scalar quantization.</summary>
    /// <param name="config">Scalar params.</param>
    public static QuantizationConfig Scalar(ScalarQuantizationParams config) => new ScalarQuantization(config);

    /// <summary>Creates product quantization.</summary>
    /// <param name="config">Product params.</param>
    public static QuantizationConfig Product(ProductQuantizationParams config) => new ProductQuantization(config);

    /// <summary>Creates binary quantization.</summary>
    /// <param name="config">Binary params.</param>
    public static QuantizationConfig Binary(BinaryQuantizationParams config) => new BinaryQuantization(config);

    /// <summary>Creates turbo quantization.</summary>
    /// <param name="config">Turbo params.</param>
    public static QuantizationConfig Turbo(TurboQuantizationParams config) => new TurboQuantization(config);
}

/// <summary>Scalar quantization.</summary>
/// <param name="Config">Scalar params.</param>
public sealed record ScalarQuantization(ScalarQuantizationParams Config) : QuantizationConfig;

/// <summary>Product quantization.</summary>
/// <param name="Config">Product params.</param>
public sealed record ProductQuantization(ProductQuantizationParams Config) : QuantizationConfig;

/// <summary>Binary quantization.</summary>
/// <param name="Config">Binary params.</param>
public sealed record BinaryQuantization(BinaryQuantizationParams Config) : QuantizationConfig;

/// <summary>Turbo quantization.</summary>
/// <param name="Config">Turbo params.</param>
public sealed record TurboQuantization(TurboQuantizationParams Config) : QuantizationConfig;
