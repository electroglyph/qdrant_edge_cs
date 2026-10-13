namespace Qdrant.Edge;

/// <summary>
/// Client-side validation mirroring <c>ffi/src/error.rs</c> and
/// <c>ffi/src/config.rs</c>. The engine remains authoritative; these
/// pre-checks fail fast without a native call.
/// </summary>
public static class EdgeValidation
{
    /// <summary>Maximum result count for limit/offset-style fields (1 Mi).</summary>
    public const ulong MaxResultCount = 1_048_576;

    /// <summary>Maximum filter/prefetch/formula nesting depth.</summary>
    public const uint MaxQueryNestingDepth = 64;

    /// <summary>Maximum formula expression node count.</summary>
    public const ulong MaxFormulaNodes = 10_000;

    /// <summary>Minimum dense vector size.</summary>
    public const ulong MinVectorSize = 1;

    /// <summary>Maximum dense vector size.</summary>
    public const ulong MaxVectorSize = 65_536;

    /// <summary>Maximum HNSW m / payload_m.</summary>
    public const ulong MaxHnswM = 2_048;

    /// <summary>Minimum HNSW ef_construct.</summary>
    public const ulong MinHnswEfConstruct = 4;

    /// <summary>Maximum HNSW ef_construct.</summary>
    public const ulong MaxHnswEfConstruct = 100_000;

    /// <summary>Maximum HNSW indexing threads.</summary>
    public const ulong MaxHnswIndexingThreads = 1_024;

    /// <summary>Rejects allocation-sized fields above <see cref="MaxResultCount"/>.</summary>
    /// <param name="field">Field name for the error message.</param>
    /// <param name="value">Value to check.</param>
    /// <exception cref="EdgeInvalidArgumentException">When the value exceeds the cap.</exception>
    public static void CheckResultCount(string field, ulong value)
    {
        ArgumentException.ThrowIfNullOrEmpty(field, nameof(field));
        if (value > MaxResultCount)
        {
            throw new EdgeInvalidArgumentException($"{field} {value} exceeds maximum result count {MaxResultCount}");
        }
    }

    /// <summary>Rejects nesting deeper than <see cref="MaxQueryNestingDepth"/>.</summary>
    /// <param name="kind">What is nested (for the error message).</param>
    /// <param name="depth">Measured depth.</param>
    /// <exception cref="EdgeInvalidArgumentException">When the depth exceeds the cap.</exception>
    public static void CheckNestingDepth(string kind, uint depth)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind, nameof(kind));
        if (depth > MaxQueryNestingDepth)
        {
            throw new EdgeInvalidArgumentException($"{kind} nesting depth {depth} exceeds maximum {MaxQueryNestingDepth}");
        }
    }

    /// <summary>Rejects formula trees larger than <see cref="MaxFormulaNodes"/>.</summary>
    /// <param name="size">Saturating node count.</param>
    /// <exception cref="EdgeInvalidArgumentException">When the size exceeds the cap.</exception>
    public static void CheckExpressionSize(ulong size)
    {
        if (size > MaxFormulaNodes)
        {
            throw new EdgeInvalidArgumentException($"formula node count {size} exceeds maximum {MaxFormulaNodes}");
        }
    }

    /// <summary>Rejects dense vector sizes outside 1..=65536.</summary>
    /// <param name="field">Field name for the error message.</param>
    /// <param name="size">Vector size.</param>
    /// <exception cref="EdgeInvalidArgumentException">When the size is out of range.</exception>
    public static void CheckVectorSize(string field, ulong size)
    {
        // "" names the default vector; only null is rejected client-side.
        ArgumentNullException.ThrowIfNull(field, nameof(field));
        if (size is < 1 or > 65_536)
        {
            throw new EdgeInvalidArgumentException($"{field} size {size} is out of range 1..=65536");
        }
    }

    /// <summary>Rejects non-finite float values (NaN and infinities).</summary>
    /// <param name="field">Field name for the error message.</param>
    /// <param name="value">Value to check.</param>
    /// <exception cref="EdgeInvalidArgumentException">When the value is non-finite.</exception>
    public static void CheckFinite(string field, double value)
    {
        ArgumentException.ThrowIfNullOrEmpty(field, nameof(field));
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new EdgeInvalidArgumentException($"{field} must be finite, got {value}");
        }
    }

    /// <summary>Rejects non-finite float values (NaN and infinities).</summary>
    /// <param name="field">Field name for the error message.</param>
    /// <param name="value">Value to check.</param>
    /// <exception cref="EdgeInvalidArgumentException">When the value is non-finite.</exception>
    public static void CheckFinite(string field, float value)
    {
        ArgumentException.ThrowIfNullOrEmpty(field, nameof(field));
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new EdgeInvalidArgumentException($"{field} must be finite, got {value}");
        }
    }

    /// <summary>Rejects empty payload keys; the engine validates JSON-path syntax.</summary>
    /// <param name="key">Payload key.</param>
    /// <exception cref="EdgeInvalidArgumentException">When the key is empty.</exception>
    public static void CheckPayloadKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
    }

    /// <summary>Validates a geographic point.</summary>
    /// <param name="lon">Longitude in [-180, 180].</param>
    /// <param name="lat">Latitude in [-90, 90].</param>
    /// <exception cref="EdgeInvalidArgumentException">When the coordinates are out of range or non-finite.</exception>
    public static void CheckGeoPoint(double lon, double lat)
    {
        CheckFinite(nameof(lon), lon);
        CheckFinite(nameof(lat), lat);
        if (lat is < -90.0 or > 90.0)
        {
            throw new EdgeInvalidArgumentException($"latitude {lat} is out of range [-90, 90]");
        }

        if (lon is < -180.0 or > 180.0)
        {
            throw new EdgeInvalidArgumentException($"longitude {lon} is out of range [-180, 180]");
        }
    }
}
