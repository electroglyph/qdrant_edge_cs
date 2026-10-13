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

    /// <summary>Validates size, finiteness, and multi-dense shape.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When the shape is invalid.</exception>
    public void Validate()
    {
        switch (this)
        {
            case DenseNamedVector(var values):
                ArgumentNullException.ThrowIfNull(values, nameof(values));
                EdgeValidation.CheckVectorSize("named vector", (ulong)values.Count);
                foreach (float value in values)
                {
                    EdgeValidation.CheckFinite("named vector value", value);
                }

                break;
            case SparseNamedVector(var sparse):
                ArgumentNullException.ThrowIfNull(sparse, nameof(sparse));
                sparse.Validate();
                break;
            case MultiDenseNamedVector(var rows):
                ArgumentNullException.ThrowIfNull(rows, nameof(rows));
                if (rows.Count == 0)
                {
                    throw new EdgeInvalidArgumentException("multi-dense vector must not be empty");
                }

                int width = -1;
                foreach (IReadOnlyList<float> row in rows)
                {
                    ArgumentNullException.ThrowIfNull(row, nameof(row));
                    if (row.Count == 0)
                    {
                        throw new EdgeInvalidArgumentException("multi-dense vector rows must not be empty");
                    }

                    EdgeValidation.CheckVectorSize("multi-dense row", (ulong)row.Count);
                    width = width == -1 ? row.Count : width;
                    if (row.Count != width)
                    {
                        throw new EdgeInvalidArgumentException("multi-dense vector rows must share one width");
                    }

                    foreach (float value in row)
                    {
                        EdgeValidation.CheckFinite("multi-dense value", value);
                    }
                }

                break;
            default:
                throw new EdgeInvalidArgumentException("unknown named vector shape");
        }
    }
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
