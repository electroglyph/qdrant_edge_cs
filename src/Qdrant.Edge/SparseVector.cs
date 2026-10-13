namespace Qdrant.Edge;

/// <summary>Sparse vector: parallel indices and values.</summary>
public sealed record SparseVector
{
    /// <summary>Dimension indices.</summary>
    public required IReadOnlyList<uint> Indices { get; init; }

    /// <summary>Non-zero values.</summary>
    public required IReadOnlyList<float> Values { get; init; }

    /// <summary>Validates index/value shape and finiteness.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When the shape is invalid.</exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Indices, nameof(Indices));
        ArgumentNullException.ThrowIfNull(Values, nameof(Values));
        if (Indices.Count != Values.Count)
        {
            throw new EdgeInvalidArgumentException($"sparse vector indices ({Indices.Count}) and values ({Values.Count}) must match");
        }

        foreach (float value in Values)
        {
            EdgeValidation.CheckFinite("sparse value", value);
        }
    }
}
