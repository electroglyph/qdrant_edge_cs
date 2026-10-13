namespace Qdrant.Edge;

/// <summary>Field condition holding exactly one predicate.</summary>
public sealed record FieldCondition
{
    /// <summary>Payload key (JSON path).</summary>
    public required string Key { get; init; }

    /// <summary>Match predicate.</summary>
    public Match? Match { get; init; }

    /// <summary>Float range predicate.</summary>
    public RangeFloat? Range { get; init; }

    /// <summary>Datetime range predicate; mutually exclusive with <see cref="Range"/>.</summary>
    public RangeDatetime? DatetimeRange { get; init; }

    /// <summary>Geo bounding-box predicate.</summary>
    public GeoBoundingBox? GeoBoundingBox { get; init; }

    /// <summary>Geo radius predicate.</summary>
    public GeoRadius? GeoRadius { get; init; }

    /// <summary>Geo polygon predicate.</summary>
    public GeoPolygon? GeoPolygon { get; init; }

    /// <summary>Values-count predicate.</summary>
    public ValuesCount? ValuesCount { get; init; }

    /// <summary>Validates that exactly one predicate is set.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When zero or several predicates are set.</exception>
    public void Validate()
    {
        EdgeValidation.CheckPayloadKey(Key);
        int count = 0;
        count += Match is not null ? 1 : 0;
        count += Range is not null || DatetimeRange is not null ? 1 : 0;
        count += GeoBoundingBox is not null ? 1 : 0;
        count += GeoRadius is not null ? 1 : 0;
        count += GeoPolygon is not null ? 1 : 0;
        count += ValuesCount is not null ? 1 : 0;
        if (count == 0)
        {
            throw new EdgeInvalidArgumentException(
                "field condition has no predicate set: specify exactly one of match, range, datetime_range, geo_bounding_box, geo_radius, geo_polygon, or values_count");
        }

        if (count > 1)
        {
            throw new EdgeInvalidArgumentException(
                "field condition has more than one predicate set: a field condition tests exactly one predicate. To AND several predicates on the same key, add one field condition per predicate to the filter's must clause");
        }

        if (Range is not null && DatetimeRange is not null)
        {
            throw new EdgeInvalidArgumentException("field condition: set either range or datetime_range, not both");
        }

        GeoRadius?.Validate();
        GeoPolygon?.Validate();
        if (GeoBoundingBox is not null)
        {
            GeoBoundingBox.TopLeft.Validate();
            GeoBoundingBox.BottomRight.Validate();
        }
    }
}

/// <summary>Minimum-should clause.</summary>
public sealed record MinShould
{
    /// <summary>Candidate conditions.</summary>
    public required IReadOnlyList<Condition> Conditions { get; init; }

    /// <summary>Minimum number that must match.</summary>
    public required ulong MinCount { get; init; }
}

/// <summary>Filter condition: Field/IsEmpty/IsNull/HasId/HasVector/Slice/Nested/Filter.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(FieldConditionClause), "field")]
[JsonDerivedType(typeof(IsEmptyCondition), "is_empty")]
[JsonDerivedType(typeof(IsNullCondition), "is_null")]
[JsonDerivedType(typeof(HasIdCondition), "has_id")]
[JsonDerivedType(typeof(HasVectorCondition), "has_vector")]
[JsonDerivedType(typeof(SliceCondition), "slice")]
[JsonDerivedType(typeof(NestedCondition), "nested")]
[JsonDerivedType(typeof(FilterCondition), "filter")]
public abstract record Condition
{
    /// <summary>Creates a field condition.</summary>
    /// <param name="condition">Field condition.</param>
    public static Condition Field(FieldCondition condition) => new FieldConditionClause(condition);

    /// <summary>Creates an is-empty condition.</summary>
    /// <param name="key">Payload key.</param>
    public static Condition IsEmpty(string key) => new IsEmptyCondition(key);

    /// <summary>Creates an is-null condition.</summary>
    /// <param name="key">Payload key.</param>
    public static Condition IsNull(string key) => new IsNullCondition(key);

    /// <summary>Creates a has-id condition.</summary>
    /// <param name="ids">Point ids.</param>
    public static Condition HasId(IReadOnlyList<PointId> ids) => new HasIdCondition(ids);

    /// <summary>Creates a has-vector condition.</summary>
    /// <param name="vectorName">Vector name.</param>
    public static Condition HasVector(string vectorName) => new HasVectorCondition(vectorName);

    /// <summary>Creates a slice condition.</summary>
    /// <param name="total">Total slices.</param>
    /// <param name="index">This slice index.</param>
    public static Condition Slice(uint total, uint index) => new SliceCondition(total, index);

    /// <summary>Creates a nested condition.</summary>
    /// <param name="key">Nested key.</param>
    /// <param name="filter">Nested filter.</param>
    public static Condition Nested(string key, Filter filter) => new NestedCondition(key, filter);

    /// <summary>Creates a nested filter condition.</summary>
    /// <param name="filter">Nested filter.</param>
    public static Condition NestedFilter(Filter filter) => new FilterCondition(filter);

    /// <summary>Measures nesting depth for the 64-deep cap.</summary>
    /// <returns>Nesting depth of this branch.</returns>
    public uint Depth() => this switch
    {
        FieldConditionClause => 0,
        IsEmptyCondition => 0,
        IsNullCondition => 0,
        HasIdCondition => 0,
        HasVectorCondition => 0,
        SliceCondition => 0,
        NestedCondition(var key, var filter) => 1 + filter.Depth(),
        FilterCondition(var filter) => 1 + filter.Depth(),
        _ => 0,
    };

    /// <summary>Validates shape (slice bounds, key presence).</summary>
    public void Validate()
    {
        switch (this)
        {
            case FieldConditionClause(var condition):
                ArgumentNullException.ThrowIfNull(condition, nameof(condition));
                condition.Validate();
                break;
            case IsEmptyCondition(var key):
                EdgeValidation.CheckPayloadKey(key);
                break;
            case IsNullCondition(var key):
                EdgeValidation.CheckPayloadKey(key);
                break;
            case HasIdCondition(var ids):
                ArgumentNullException.ThrowIfNull(ids, nameof(ids));
                break;
            case HasVectorCondition(var vectorName):
                ArgumentException.ThrowIfNullOrWhiteSpace(vectorName, nameof(vectorName));
                break;
            case SliceCondition(var total, var index):
                if (total == 0)
                {
                    throw new EdgeInvalidArgumentException("slice total must be > 0");
                }

                if (index >= total)
                {
                    throw new EdgeInvalidArgumentException($"slice index {index} must be < total {total}");
                }

                break;
            case NestedCondition(var key, var filter):
                EdgeValidation.CheckPayloadKey(key);
                ArgumentNullException.ThrowIfNull(filter, nameof(filter));
                filter.Validate();
                break;
            case FilterCondition(var filter):
                ArgumentNullException.ThrowIfNull(filter, nameof(filter));
                filter.Validate();
                break;
            default:
                throw new EdgeInvalidArgumentException("unknown condition shape");
        }
    }
}

/// <summary>Field condition clause.</summary>
/// <param name="Condition">Field condition.</param>
public sealed record FieldConditionClause(FieldCondition Condition) : Condition;

/// <summary>Is-empty clause.</summary>
/// <param name="Key">Payload key.</param>
public sealed record IsEmptyCondition(string Key) : Condition;

/// <summary>Is-null clause.</summary>
/// <param name="Key">Payload key.</param>
public sealed record IsNullCondition(string Key) : Condition;

/// <summary>Has-id clause.</summary>
/// <param name="Ids">Point ids.</param>
public sealed record HasIdCondition(IReadOnlyList<PointId> Ids) : Condition;

/// <summary>Has-vector clause.</summary>
/// <param name="VectorName">Vector name.</param>
public sealed record HasVectorCondition(string VectorName) : Condition;

/// <summary>Slice clause.</summary>
/// <param name="Total">Total slices.</param>
/// <param name="Index">This slice index.</param>
public sealed record SliceCondition(uint Total, uint Index) : Condition;

/// <summary>Nested clause.</summary>
/// <param name="Key">Nested key.</param>
/// <param name="Filter">Nested filter.</param>
public sealed record NestedCondition(string Key, Filter Filter) : Condition;

/// <summary>Nested filter clause.</summary>
/// <param name="Filter">Nested filter.</param>
public sealed record FilterCondition(Filter Filter) : Condition;

/// <summary>Boolean filter over conditions.</summary>
public sealed record Filter
{
    /// <summary>Must clauses.</summary>
    public IReadOnlyList<Condition>? Must { get; init; }

    /// <summary>Should clauses.</summary>
    public IReadOnlyList<Condition>? Should { get; init; }

    /// <summary>Must-not clauses.</summary>
    public IReadOnlyList<Condition>? MustNot { get; init; }

    /// <summary>Minimum-should clause.</summary>
    public MinShould? MinShould { get; init; }

    /// <summary>Measures nesting depth for the 64-deep cap.</summary>
    /// <returns>Maximum branch depth.</returns>
    public uint Depth()
    {
        uint deepest = 0;
        foreach (Condition condition in AllConditions())
        {
            deepest = Math.Max(deepest, condition.Depth());
        }

        return deepest;
    }

    /// <summary>Validates depth and every branch.</summary>
    public void Validate()
    {
        EdgeValidation.CheckNestingDepth("filter", Depth());
        foreach (Condition condition in AllConditions())
        {
            condition.Validate();
        }
    }

    private IEnumerable<Condition> AllConditions()
    {
        foreach (Condition condition in Must ?? []) yield return condition;
        foreach (Condition condition in Should ?? []) yield return condition;
        foreach (Condition condition in MustNot ?? []) yield return condition;
        if (MinShould is not null)
        {
            ArgumentNullException.ThrowIfNull(MinShould.Conditions, nameof(MinShould.Conditions));
            foreach (Condition condition in MinShould.Conditions) yield return condition;
        }
    }
}
