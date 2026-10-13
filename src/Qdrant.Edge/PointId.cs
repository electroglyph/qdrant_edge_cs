namespace Qdrant.Edge;

/// <summary>Point id: numeric or UUID. Strings parse as UUID or throw.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(NumPointId), "num")]
[JsonDerivedType(typeof(UuidPointId), "uuid")]
public abstract record PointId
{
    /// <summary>Creates a numeric point id.</summary>
    /// <param name="value">Numeric id.</param>
    public static PointId FromNumber(ulong value) => new NumPointId(value);

    /// <summary>Creates a UUID point id.</summary>
    /// <param name="value">UUID value.</param>
    public static PointId FromUuid(Guid value) => new UuidPointId(value);

    /// <summary>Parses a string as UUID; throws on failure.</summary>
    /// <param name="value">UUID string.</param>
    /// <exception cref="EdgeInvalidArgumentException">When the string is not a UUID.</exception>
    public static PointId FromString(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        if (Guid.TryParse(value, out Guid parsed))
        {
            return new UuidPointId(parsed);
        }

        throw new EdgeInvalidArgumentException($"point id string {value} is not a valid UUID");
    }
}

/// <summary>Numeric point id.</summary>
/// <param name="Value">Numeric id.</param>
public sealed record NumPointId(ulong Value) : PointId;

/// <summary>UUID point id.</summary>
/// <param name="Value">UUID value.</param>
public sealed record UuidPointId(Guid Value) : PointId;
