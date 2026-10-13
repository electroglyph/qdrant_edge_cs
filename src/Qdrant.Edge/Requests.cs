namespace Qdrant.Edge;

/// <summary>Prefetch clause for multi-stage queries.</summary>
public sealed record Prefetch
{
    /// <summary>Prefetch limit.</summary>
    public required ulong Limit { get; init; }

    /// <summary>Optional sub-query.</summary>
    public ScoringQuery? Query { get; init; }

    /// <summary>Nested prefetches.</summary>
    public IReadOnlyList<Prefetch> Prefetches { get; init; } = [];

    /// <summary>Optional filter.</summary>
    public Filter? Filter { get; init; }

    /// <summary>Optional score threshold.</summary>
    public float? ScoreThreshold { get; init; }

    /// <summary>Optional search params.</summary>
    public SearchParams? Params { get; init; }

    /// <summary>Validates limit, depth, and nested branches.</summary>
    public void Validate(uint depth = 0)
    {
        EdgeValidation.CheckResultCount("prefetch limit", Limit);
        EdgeValidation.CheckNestingDepth("prefetch", depth);
        Filter?.Validate();
        foreach (Prefetch nested in Prefetches)
        {
            ArgumentNullException.ThrowIfNull(nested, nameof(nested));
            nested.Validate(depth + 1);
        }
    }
}

/// <summary>Vector-only search request. Deprecated in edge-core; prefer <see cref="QueryRequest"/>.</summary>
public sealed record SearchRequest
{
    /// <summary>Vector query.</summary>
    public required Query Query { get; init; }

    /// <summary>Result limit.</summary>
    public required ulong Limit { get; init; }

    /// <summary>Result offset.</summary>
    public ulong? Offset { get; init; }

    /// <summary>Optional filter.</summary>
    public Filter? Filter { get; init; }

    /// <summary>Optional search params.</summary>
    public SearchParams? Params { get; init; }

    /// <summary>Optional vector selector.</summary>
    public WithVector? WithVector { get; init; }

    /// <summary>Optional payload selector.</summary>
    public WithPayload? WithPayload { get; init; }

    /// <summary>Optional score threshold.</summary>
    public float? ScoreThreshold { get; init; }

    /// <summary>Validates limit/offset caps and nested shapes.</summary>
    public void Validate()
    {
        EdgeValidation.CheckResultCount("limit", Limit);
        if (Offset is ulong offset)
        {
            EdgeValidation.CheckResultCount("offset", offset);
        }

        Filter?.Validate();
        if (ScoreThreshold is float threshold)
        {
            EdgeValidation.CheckFinite(nameof(ScoreThreshold), threshold);
        }
    }
}

/// <summary>General query request.</summary>
public sealed record QueryRequest
{
    /// <summary>Result limit.</summary>
    public required ulong Limit { get; init; }

    /// <summary>Result offset.</summary>
    public ulong? Offset { get; init; }

    /// <summary>Optional scoring query.</summary>
    public ScoringQuery? Query { get; init; }

    /// <summary>Prefetch stages.</summary>
    public IReadOnlyList<Prefetch> Prefetches { get; init; } = [];

    /// <summary>Optional vector selector.</summary>
    public WithVector? WithVector { get; init; }

    /// <summary>Optional payload selector.</summary>
    public WithPayload? WithPayload { get; init; }

    /// <summary>Optional filter.</summary>
    public Filter? Filter { get; init; }

    /// <summary>Optional score threshold.</summary>
    public float? ScoreThreshold { get; init; }

    /// <summary>Optional search params.</summary>
    public SearchParams? Params { get; init; }

    /// <summary>Validates limit/offset caps and nested shapes.</summary>
    public void Validate()
    {
        EdgeValidation.CheckResultCount("limit", Limit);
        if (Offset is ulong offset)
        {
            EdgeValidation.CheckResultCount("offset", offset);
        }

        Filter?.Validate();
        foreach (Prefetch prefetch in Prefetches)
        {
            ArgumentNullException.ThrowIfNull(prefetch, nameof(prefetch));
            prefetch.Validate();
        }

        if (Query is MmrScoringQuery mmr)
        {
            mmr.Validate();
        }

        if (ScoreThreshold is float threshold)
        {
            EdgeValidation.CheckFinite(nameof(ScoreThreshold), threshold);
        }
    }
}

/// <summary>Batch query request; responses preserve input order.</summary>
public sealed record QueryBatchRequest
{
    /// <summary>Queries to run.</summary>
    public required IReadOnlyList<QueryRequest> Queries { get; init; }

    /// <summary>Validates every query.</summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Queries, nameof(Queries));
        foreach (QueryRequest query in Queries)
        {
            ArgumentNullException.ThrowIfNull(query, nameof(query));
            query.Validate();
        }
    }
}

/// <summary>Scroll request; follow <see cref="ScrollResponse.NextOffset"/> until null.</summary>
public sealed record ScrollRequest
{
    /// <summary>Start offset; null starts from the beginning.</summary>
    public PointId? Offset { get; init; }

    /// <summary>Page size.</summary>
    public ulong? Limit { get; init; }

    /// <summary>Optional filter.</summary>
    public Filter? Filter { get; init; }

    /// <summary>Optional payload selector.</summary>
    public WithPayload? WithPayload { get; init; }

    /// <summary>Optional vector selector.</summary>
    public WithVector? WithVector { get; init; }

    /// <summary>Optional order-by.</summary>
    public OrderBy? OrderBy { get; init; }

    /// <summary>Validates the limit cap and filter.</summary>
    public void Validate()
    {
        if (Limit is ulong limit)
        {
            EdgeValidation.CheckResultCount("limit", limit);
        }

        Filter?.Validate();
    }
}

/// <summary>Scroll response: one page plus the next offset.</summary>
public sealed record ScrollResponse
{
    /// <summary>Page records.</summary>
    public required IReadOnlyList<Record> Records { get; init; }

    /// <summary>Next offset; null ends pagination.</summary>
    public PointId? NextOffset { get; init; }
}

/// <summary>Retrieve-by-id request; missing ids are omitted.</summary>
public sealed record RetrieveRequest
{
    /// <summary>Point ids.</summary>
    public required IReadOnlyList<PointId> PointIds { get; init; }

    /// <summary>Optional payload selector.</summary>
    public WithPayload? WithPayload { get; init; }

    /// <summary>Optional vector selector.</summary>
    public WithVector? WithVector { get; init; }
}

/// <summary>Count request.</summary>
public sealed record CountRequest
{
    /// <summary>Optional filter.</summary>
    public Filter? Filter { get; init; }

    /// <summary>Whether to count exactly.</summary>
    public bool Exact { get; init; } = true;
}

/// <summary>Facet request over an indexed payload key.</summary>
public sealed record FacetRequest
{
    /// <summary>Payload key.</summary>
    public required string Key { get; init; }

    /// <summary>Maximum hits.</summary>
    public ulong Limit { get; init; } = 10;

    /// <summary>Whether to compute exactly.</summary>
    public bool Exact { get; init; }

    /// <summary>Optional filter.</summary>
    public Filter? Filter { get; init; }

    /// <summary>Validates the key and limit cap.</summary>
    public void Validate()
    {
        EdgeValidation.CheckPayloadKey(Key);
        EdgeValidation.CheckResultCount("limit", Limit);
        Filter?.Validate();
    }
}

/// <summary>Single facet hit.</summary>
public sealed record FacetHit
{
    /// <summary>Facet value.</summary>
    public required string Value { get; init; }

    /// <summary>Match count.</summary>
    public required ulong Count { get; init; }
}

/// <summary>Facet response.</summary>
/// <remarks>
/// Plain object shape (<c>{"Hits": [...]}</c>): a collection interface would
/// force System.Text.Json into collection deserialization, which has no
/// constructible path for this type.
/// </remarks>
public sealed record FacetResponse
{
    /// <summary>Facet hits.</summary>
    public required IReadOnlyList<FacetHit> Hits { get; init; }

    /// <summary>Hit count.</summary>
    public int Count => Hits.Count;

    /// <summary>Gets the hit at the given index.</summary>
    /// <param name="index">Zero-based index.</param>
    public FacetHit this[int index] => Hits[index];
}

/// <summary>Grouped query request.</summary>
public sealed record GroupRequest
{
    /// <summary>Inner query.</summary>
    public required QueryRequest Query { get; init; }

    /// <summary>Payload key to group by.</summary>
    public required string GroupBy { get; init; }

    /// <summary>Maximum groups.</summary>
    public required ulong Groups { get; init; }

    /// <summary>Maximum hits per group.</summary>
    public required ulong GroupSize { get; init; }

    /// <summary>Validates caps and nested shapes.</summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Query, nameof(Query));
        Query.Validate();
        EdgeValidation.CheckPayloadKey(GroupBy);
        EdgeValidation.CheckResultCount("groups", Groups);
        EdgeValidation.CheckResultCount("group_size", GroupSize);
    }
}

/// <summary>Group key.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StringGroupId), "string")]
[JsonDerivedType(typeof(UInt64GroupId), "u64")]
[JsonDerivedType(typeof(Int64GroupId), "i64")]
public abstract record GroupId;

/// <summary>String group key.</summary>
/// <param name="Value">Key value.</param>
public sealed record StringGroupId(string Value) : GroupId;

/// <summary>Unsigned group key.</summary>
/// <param name="Value">Key value.</param>
public sealed record UInt64GroupId(ulong Value) : GroupId;

/// <summary>Signed group key.</summary>
/// <param name="Value">Key value.</param>
public sealed record Int64GroupId(long Value) : GroupId;

/// <summary>One result group.</summary>
public sealed record Group
{
    /// <summary>Group key.</summary>
    public required GroupId Key { get; init; }

    /// <summary>Group hits.</summary>
    public required IReadOnlyList<ScoredPoint> Hits { get; init; }
}

/// <summary>Search-matrix request.</summary>
public sealed record SearchMatrixRequest
{
    /// <summary>Sample size.</summary>
    public required ulong SampleSize { get; init; }

    /// <summary>Limit per sample.</summary>
    public required ulong LimitPerSample { get; init; }

    /// <summary>Optional filter.</summary>
    public Filter? Filter { get; init; }

    /// <summary>Optional vector name.</summary>
    public string? Using { get; init; }

    /// <summary>Validates caps and filter.</summary>
    public void Validate()
    {
        EdgeValidation.CheckResultCount("sample_size", SampleSize);
        EdgeValidation.CheckResultCount("limit_per_sample", LimitPerSample);
        Filter?.Validate();
    }
}

/// <summary>Search-matrix response.</summary>
public sealed record SearchMatrixResponse
{
    /// <summary>Sampled point ids.</summary>
    public required IReadOnlyList<PointId> SampleIds { get; init; }

    /// <summary>Nearest lists per sample.</summary>
    public required IReadOnlyList<IReadOnlyList<ScoredPoint>> Nearests { get; init; }
}

/// <summary>Shard summary statistics.</summary>
public sealed record ShardInfo
{
    /// <summary>Number of segments.</summary>
    public required ulong SegmentsCount { get; init; }

    /// <summary>Total points.</summary>
    public required ulong PointsCount { get; init; }

    /// <summary>Indexed vector count.</summary>
    public required ulong IndexedVectorsCount { get; init; }

    /// <summary>Payload indexes keyed by field.</summary>
    public required IReadOnlyDictionary<string, PayloadIndexInfo> PayloadSchema { get; init; }
}
