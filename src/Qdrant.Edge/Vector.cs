namespace Qdrant.Edge;

/// <summary>Point vector: single dense, multi-dense, or named map. Empty string is the default unnamed vector.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SingleVector), "single")]
[JsonDerivedType(typeof(MultiDenseVector), "multi_dense")]
[JsonDerivedType(typeof(NamedVectorMap), "named")]
public abstract record Vector
{
    /// <summary>Creates a single dense vector.</summary>
    /// <param name="values">Dense values.</param>
    public static Vector Single(IReadOnlyList<float> values) => new SingleVector(values);

    /// <summary>Creates a multi-dense vector.</summary>
    /// <param name="vectors">Dense matrix rows.</param>
    public static Vector MultiDense(IReadOnlyList<IReadOnlyList<float>> vectors) => new MultiDenseVector(vectors);

    /// <summary>Creates a named vector map.</summary>
    /// <param name="map">Vector name to value.</param>
    public static Vector Named(IReadOnlyDictionary<string, NamedVector> map) => new NamedVectorMap(map);

    /// <summary>Validates sizes, ragged shapes, and finiteness.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When the shape is invalid.</exception>
    public void Validate()
    {
        switch (this)
        {
            case SingleVector(var values):
                ArgumentNullException.ThrowIfNull(values, nameof(values));
                EdgeValidation.CheckVectorSize("vector", (ulong)values.Count);
                foreach (float value in values)
                {
                    EdgeValidation.CheckFinite("vector value", value);
                }

                break;
            case MultiDenseVector(var vectors):
                ValidateMultiDense(vectors);
                break;
            case NamedVectorMap(var map):
                ArgumentNullException.ThrowIfNull(map, nameof(map));
                foreach ((string name, NamedVector named) in map)
                {
                    // "" is the default unnamed vector; only null is rejected client-side.
                    ArgumentNullException.ThrowIfNull(name, nameof(name));
                    ArgumentNullException.ThrowIfNull(named, nameof(named));
                    switch (named)
                    {
                        case DenseNamedVector(var values):
                            ValidateDense(values);
                            break;
                        case SparseNamedVector(var sparse):
                            sparse.Validate();
                            break;
                        case MultiDenseNamedVector(var vectors):
                            ValidateMultiDense(vectors);
                            break;
                        default:
                            throw new EdgeInvalidArgumentException($"unknown named vector for {name}");
                    }
                }

                break;
            default:
                throw new EdgeInvalidArgumentException("unknown vector shape");
        }

        static void ValidateDense(IReadOnlyList<float> values)
        {
            ArgumentNullException.ThrowIfNull(values, nameof(values));
            EdgeValidation.CheckVectorSize("named vector", (ulong)values.Count);
            foreach (float value in values)
            {
                EdgeValidation.CheckFinite("named vector value", value);
            }
        }

        static void ValidateMultiDense(IReadOnlyList<IReadOnlyList<float>> vectors)
        {
            ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
            if (vectors.Count == 0)
            {
                throw new EdgeInvalidArgumentException("multi-dense vector must not be empty");
            }

            int width = -1;
            foreach (IReadOnlyList<float> row in vectors)
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
        }
    }
}

/// <summary>Single dense vector.</summary>
/// <param name="Values">Dense values.</param>
public sealed record SingleVector(IReadOnlyList<float> Values) : Vector;

/// <summary>Multi-dense vector.</summary>
/// <param name="Vectors">Dense matrix rows.</param>
public sealed record MultiDenseVector(IReadOnlyList<IReadOnlyList<float>> Vectors) : Vector;

/// <summary>Named vector map.</summary>
/// <param name="Map">Vector name to value.</param>
public sealed record NamedVectorMap(IReadOnlyDictionary<string, NamedVector> Map) : Vector;
