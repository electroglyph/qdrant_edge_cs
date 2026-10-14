namespace Qdrant.Edge;

/// <summary>HNSW index configuration.</summary>
public sealed record HnswIndexConfig
{
    /// <summary>Number of edges per node.</summary>
    public ulong M { get; init; } = 16;

    /// <summary>Search scope during construction.</summary>
    public ulong EfConstruct { get; init; } = 100;

    /// <summary>Full-scan threshold.</summary>
    public ulong FullScanThreshold { get; init; } = 10_000;

    /// <summary>Maximum indexing threads (0 = automatic).</summary>
    public ulong MaxIndexingThreads { get; init; }

    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Payload-specific edge count.</summary>
    public ulong? PayloadM { get; init; }

    /// <summary>Validates m/payload_m, ef_construct, and thread bounds.</summary>
    /// <param name="scope">Scope name for error messages.</param>
    /// <exception cref="EdgeInvalidArgumentException">When a bound is violated.</exception>
    public void Validate(string scope)
    {
        ArgumentException.ThrowIfNullOrEmpty(scope, nameof(scope));
        if (M > EdgeValidation.MaxHnswM)
        {
            throw new EdgeInvalidArgumentException($"{scope}: m {M} exceeds maximum {EdgeValidation.MaxHnswM}");
        }

        if (PayloadM is ulong payloadM && payloadM > EdgeValidation.MaxHnswM)
        {
            throw new EdgeInvalidArgumentException($"{scope}: payload_m {payloadM} exceeds maximum {EdgeValidation.MaxHnswM}");
        }

        if (EfConstruct is < 4 or > 100_000)
        {
            throw new EdgeInvalidArgumentException($"{scope}: ef_construct {EfConstruct} is out of range 4..=100000");
        }

        if (MaxIndexingThreads > EdgeValidation.MaxHnswIndexingThreads)
        {
            throw new EdgeInvalidArgumentException($"{scope}: max_indexing_threads {MaxIndexingThreads} exceeds maximum {EdgeValidation.MaxHnswIndexingThreads}");
        }
    }
}

/// <summary>Dense vector field configuration.</summary>
public sealed record VectorDataConfig
{
    /// <summary>Vector size (1..=65536).</summary>
    public required ulong Size { get; init; }

    /// <summary>Distance metric.</summary>
    public required Distance Distance { get; init; }

    /// <summary>Quantization configuration.</summary>
    public QuantizationConfig? QuantizationConfig { get; init; }

    /// <summary>Multi-vector configuration.</summary>
    public MultiVectorConfig? MultivectorConfig { get; init; }

    /// <summary>Storage datatype.</summary>
    public VectorStorageDatatype? Datatype { get; init; }

    /// <summary>HNSW configuration.</summary>
    public HnswIndexConfig? HnswConfig { get; init; }

    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Validates size and HNSW bounds.</summary>
    /// <param name="field">Field name for error messages.</param>
    public void Validate(string field)
    {
        EdgeValidation.CheckVectorSize(field, Size);
        HnswConfig?.Validate(field);
    }
}

/// <summary>Sparse vector modifier.</summary>
public enum Modifier
{
    /// <summary>No modifier.</summary>
    None,

    /// <summary>Inverse document frequency.</summary>
    Idf,
}

/// <summary>Sparse vector field configuration.</summary>
public sealed record SparseVectorDataConfig
{
    /// <summary>Full-scan threshold.</summary>
    public ulong? FullScanThreshold { get; init; }

    /// <summary>Storage datatype.</summary>
    public VectorStorageDatatype? Datatype { get; init; }

    /// <summary>Score modifier.</summary>
    public Modifier? Modifier { get; init; }

    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }
}

/// <summary>Optimizer configuration; every field optional.</summary>
public sealed record OptimizersConfig
{
    /// <summary>Deleted-vector threshold.</summary>
    public double? DeletedThreshold { get; init; }

    /// <summary>Minimum vectors before vacuuming.</summary>
    public ulong? VacuumMinVectorNumber { get; init; }

    /// <summary>Default segment number.</summary>
    public ulong? DefaultSegmentNumber { get; init; }

    /// <summary>Maximum segment size in kilobytes.</summary>
    public ulong? MaxSegmentSizeKb { get; init; }

    /// <summary>Indexing threshold in kilobytes.</summary>
    public ulong? IndexingThresholdKb { get; init; }

    /// <summary>Whether to prevent unoptimized segments.</summary>
    public bool? PreventUnoptimized { get; init; }

    /// <summary>Validates deleted threshold and kb bounds.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When a bound is violated.</exception>
    public void Validate()
    {
        if (DeletedThreshold is double threshold)
        {
            if (double.IsNaN(threshold) || double.IsInfinity(threshold) || threshold is < 0.0 or > 1.0)
            {
                throw new EdgeInvalidArgumentException($"optimizers deleted_threshold {threshold} must be finite and in [0, 1]");
            }
        }

        if (VacuumMinVectorNumber is ulong vacuum && vacuum > EdgeValidation.MaxResultCount)
        {
            throw new EdgeInvalidArgumentException($"optimizers vacuum_min_vector_number {vacuum} exceeds maximum {EdgeValidation.MaxResultCount}");
        }

        if (DefaultSegmentNumber is ulong segments && (segments == 0 || segments > EdgeValidation.MaxResultCount))
        {
            throw new EdgeInvalidArgumentException($"optimizers default_segment_number {segments} is out of range 1..={EdgeValidation.MaxResultCount}");
        }

        if (MaxSegmentSizeKb is ulong maxSegment && maxSegment == 0)
        {
            throw new EdgeInvalidArgumentException("optimizers max_segment_size_kb must be > 0");
        }

        if (IndexingThresholdKb is ulong indexing && indexing == 0)
        {
            throw new EdgeInvalidArgumentException("optimizers indexing_threshold_kb must be > 0");
        }
    }
}

/// <summary>Shard configuration: at least one of dense or sparse vectors is required.</summary>
public sealed record EdgeConfig
{
    /// <summary>Dense vector fields keyed by name ("" = default).</summary>
    public IReadOnlyDictionary<string, VectorDataConfig> VectorData { get; init; } = new Dictionary<string, VectorDataConfig>();

    /// <summary>Sparse vector fields keyed by name.</summary>
    public IReadOnlyDictionary<string, SparseVectorDataConfig> SparseVectorData { get; init; } = new Dictionary<string, SparseVectorDataConfig>();

    /// <summary>Payload memory placement.</summary>
    public Memory? PayloadMemory { get; init; }

    /// <summary>Id-tracker memory placement.</summary>
    public Memory? IdTrackerMemory { get; init; }

    /// <summary>Validates that at least one vector family exists and every field is in range.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When the config is empty or invalid.</exception>
    public void Validate()
    {
        if (VectorData.Count == 0 && SparseVectorData.Count == 0)
        {
            throw new EdgeInvalidArgumentException("config must define at least one of vectors or sparse_vectors");
        }

        foreach ((string name, VectorDataConfig field) in VectorData)
        {
            // Empty string is the default unnamed vector; only null is invalid.
            ArgumentNullException.ThrowIfNull(name, nameof(name));
            ArgumentNullException.ThrowIfNull(field, nameof(field));
            field.Validate($"vector field {name}");
        }
    }
}
