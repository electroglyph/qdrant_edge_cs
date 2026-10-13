namespace Qdrant.Edge;

/// <summary>Named vector value: dense, sparse, or multi-dense.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(DenseNamedVector), "dense")]
[JsonDerivedType(typeof(SparseNamedVector), "sparse")]
[JsonDerivedType(typeof(MultiDenseNamedVector), "multi_dense")]
public abstract record NamedVector
{
    /// <summary>Creates a dense named vector.</summary>
    /// <param name="values">Dense values.</param>
    public static NamedVector Dense(IReadOnlyList<float> values) => new DenseNamedVector(values);

    /// <summary>Creates a sparse named vector.</summary>
    /// <param name="vector">Sparse vector.</param>
    public static NamedVector Sparse(SparseVector vector) => new SparseNamedVector(vector);

    /// <summary>Creates a multi-dense named vector.</summary>
    /// <param name="vectors">Dense matrix rows.</param>
    public static NamedVector MultiDense(IReadOnlyList<IReadOnlyList<float>> vectors) => new MultiDenseNamedVector(vectors);
}

/// <summary>Dense named vector.</summary>
/// <param name="Values">Dense values.</param>
public sealed record DenseNamedVector(IReadOnlyList<float> Values) : NamedVector;

/// <summary>Sparse named vector.</summary>
/// <param name="Vector">Sparse vector.</param>
public sealed record SparseNamedVector(SparseVector Vector) : NamedVector;

/// <summary>Multi-dense named vector.</summary>
/// <param name="Vectors">Dense matrix rows.</param>
public sealed record MultiDenseNamedVector(IReadOnlyList<IReadOnlyList<float>> Vectors) : NamedVector;
