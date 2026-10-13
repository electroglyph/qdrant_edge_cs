using System.Text.Json.Nodes;

namespace Qdrant.Edge;

/// <summary>Point to upsert: id, vector, and optional JSON payload object.</summary>
public sealed record Point
{
    /// <summary>Point id.</summary>
    public required PointId Id { get; init; }

    /// <summary>Point vector.</summary>
    public required Vector Vector { get; init; }

    /// <summary>Optional payload object.</summary>
    public JsonObject? Payload { get; init; }

    /// <summary>Validates id, vector, and payload shape.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When the shape is invalid.</exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Id, nameof(Id));
        ArgumentNullException.ThrowIfNull(Vector, nameof(Vector));
        Vector.Validate();
    }
}

/// <summary>Point id plus replacement vectors.</summary>
public sealed record PointVectors
{
    /// <summary>Point id.</summary>
    public required PointId Id { get; init; }

    /// <summary>Replacement vector.</summary>
    public required Vector Vector { get; init; }

    /// <summary>Validates id and vector shape.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When the shape is invalid.</exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Id, nameof(Id));
        ArgumentNullException.ThrowIfNull(Vector, nameof(Vector));
        Vector.Validate();
    }
}

/// <summary>Sort key attached to a point: integer or float (datetimes as timestamps).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(IntOrderValue), "int")]
[JsonDerivedType(typeof(FloatOrderValue), "float")]
public abstract record OrderValue
{
    /// <summary>Creates an integer order value.</summary>
    /// <param name="value">Integer key.</param>
    public static OrderValue Int(long value) => new IntOrderValue(value);

    /// <summary>Creates a float order value.</summary>
    /// <param name="value">Float key.</param>
    public static OrderValue Float(double value) => new FloatOrderValue(value);
}

/// <summary>Integer order value.</summary>
/// <param name="Value">Integer key.</param>
public sealed record IntOrderValue(long Value) : OrderValue;

/// <summary>Float order value.</summary>
/// <param name="Value">Float key.</param>
public sealed record FloatOrderValue(double Value) : OrderValue;

/// <summary>Scored query hit.</summary>
public sealed record ScoredPoint
{
    /// <summary>Point id.</summary>
    public required PointId Id { get; init; }

    /// <summary>Point version.</summary>
    public required ulong Version { get; init; }

    /// <summary>Match score.</summary>
    public required float Score { get; init; }

    /// <summary>Payload as a JSON string, when requested.</summary>
    public string? Payload { get; init; }

    /// <summary>Vector as a JSON string, when requested.</summary>
    public string? Vector { get; init; }

    /// <summary>Sort key, when ordered.</summary>
    public OrderValue? OrderValue { get; init; }
}

/// <summary>Stored record without score or version.</summary>
public sealed record Record
{
    /// <summary>Point id.</summary>
    public required PointId Id { get; init; }

    /// <summary>Payload as a JSON string, when requested.</summary>
    public string? Payload { get; init; }

    /// <summary>Vector as a JSON string, when requested.</summary>
    public string? Vector { get; init; }

    /// <summary>Sort key, when ordered.</summary>
    public OrderValue? OrderValue { get; init; }
}
