namespace Qdrant.Edge;

/// <summary>Float range predicate; all bounds optional.</summary>
public sealed record RangeFloat
{
    /// <summary>Greater than or equal.</summary>
    public double? Gte { get; init; }

    /// <summary>Greater than.</summary>
    public double? Gt { get; init; }

    /// <summary>Less than or equal.</summary>
    public double? Lte { get; init; }

    /// <summary>Less than.</summary>
    public double? Lt { get; init; }
}

/// <summary>Datetime range predicate; bounds are RFC3339 strings.</summary>
public sealed record RangeDatetime
{
    /// <summary>Greater than or equal (RFC3339).</summary>
    public string? Gte { get; init; }

    /// <summary>Greater than (RFC3339).</summary>
    public string? Gt { get; init; }

    /// <summary>Less than or equal (RFC3339).</summary>
    public string? Lte { get; init; }

    /// <summary>Less than (RFC3339).</summary>
    public string? Lt { get; init; }
}

/// <summary>Payload array length predicate; all bounds optional.</summary>
public sealed record ValuesCount
{
    /// <summary>Greater than or equal.</summary>
    public ulong? Gte { get; init; }

    /// <summary>Greater than.</summary>
    public ulong? Gt { get; init; }

    /// <summary>Less than or equal.</summary>
    public ulong? Lte { get; init; }

    /// <summary>Less than.</summary>
    public ulong? Lt { get; init; }
}
