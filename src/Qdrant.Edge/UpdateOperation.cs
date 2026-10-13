namespace Qdrant.Edge;

/// <summary>Upsert mode.</summary>
public enum UpdateMode
{
    /// <summary>Insert or replace.</summary>
    Upsert,

    /// <summary>Insert only.</summary>
    InsertOnly,

    /// <summary>Update only.</summary>
    UpdateOnly,
}

/// <summary>Payload index schema type.</summary>
public enum PayloadSchemaType
{
    /// <summary>Keyword index.</summary>
    Keyword,

    /// <summary>Integer index.</summary>
    Integer,

    /// <summary>Float index.</summary>
    Float,

    /// <summary>Geo index.</summary>
    Geo,

    /// <summary>Text index.</summary>
    Text,

    /// <summary>Boolean index.</summary>
    Bool,

    /// <summary>Datetime index.</summary>
    Datetime,

    /// <summary>UUID index.</summary>
    Uuid,
}

/// <summary>
/// Immutable update operation built via static factories mirroring all 20
/// FFI constructors. CreateSparseVector and DeleteVectorName are infallible.
/// Serializes as a <c>kind</c>-discriminated union carrying the full payload.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(UpsertPointsOperation), "upsert_points")]
[JsonDerivedType(typeof(DeletePointsOperation), "delete_points")]
[JsonDerivedType(typeof(DeletePointsByFilterOperation), "delete_points_by_filter")]
[JsonDerivedType(typeof(UpdateVectorsOperation), "update_vectors")]
[JsonDerivedType(typeof(DeleteVectorsByFilterOperation), "delete_vectors_by_filter")]
[JsonDerivedType(typeof(DeleteVectorsOperation), "delete_vectors")]
[JsonDerivedType(typeof(SetPayloadOperation), "set_payload")]
[JsonDerivedType(typeof(SetPayloadByFilterOperation), "set_payload_by_filter")]
[JsonDerivedType(typeof(OverwritePayloadOperation), "overwrite_payload")]
[JsonDerivedType(typeof(OverwritePayloadByFilterOperation), "overwrite_payload_by_filter")]
[JsonDerivedType(typeof(DeletePayloadOperation), "delete_payload")]
[JsonDerivedType(typeof(DeletePayloadByFilterOperation), "delete_payload_by_filter")]
[JsonDerivedType(typeof(ClearPayloadOperation), "clear_payload")]
[JsonDerivedType(typeof(ClearPayloadByFilterOperation), "clear_payload_by_filter")]
[JsonDerivedType(typeof(CreateFieldIndexOperation), "create_field_index")]
[JsonDerivedType(typeof(CreateFieldIndexWithParamsOperation), "create_field_index_with_params")]
[JsonDerivedType(typeof(DeleteFieldIndexOperation), "delete_field_index")]
[JsonDerivedType(typeof(CreateDenseVectorOperation), "create_dense_vector")]
[JsonDerivedType(typeof(CreateSparseVectorOperation), "create_sparse_vector")]
[JsonDerivedType(typeof(DeleteVectorNameOperation), "delete_vector_name")]
public abstract record UpdateOperation
{
    /// <summary>Operation kind (matches the <c>kind</c> discriminator).</summary>
    [JsonIgnore]
    public abstract string Kind { get; }

    /// <summary>Upserts points, optionally filtered and moded.</summary>
    /// <param name="points">Points to upsert.</param>
    /// <param name="condition">Optional filter.</param>
    /// <param name="updateMode">Optional upsert mode.</param>
    public static UpdateOperation UpsertPoints(
        IReadOnlyList<Point> points,
        Filter? condition = null,
        UpdateMode? updateMode = null)
    {
        ArgumentNullException.ThrowIfNull(points, nameof(points));
        foreach (Point point in points)
        {
            ArgumentNullException.ThrowIfNull(point, nameof(point));
            point.Validate();
        }

        condition?.Validate();
        return new UpsertPointsOperation(points, condition, updateMode);
    }

    /// <summary>Deletes points by id.</summary>
    /// <param name="pointIds">Point ids.</param>
    public static UpdateOperation DeletePoints(IReadOnlyList<PointId> pointIds)
    {
        ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
        return new DeletePointsOperation(pointIds);
    }

    /// <summary>Deletes points by filter.</summary>
    /// <param name="filter">Filter.</param>
    public static UpdateOperation DeletePointsByFilter(Filter filter)
    {
        ArgumentNullException.ThrowIfNull(filter, nameof(filter));
        filter.Validate();
        return new DeletePointsByFilterOperation(filter);
    }

    /// <summary>Replaces vectors, optionally filtered.</summary>
    /// <param name="pointVectors">Replacement vectors.</param>
    /// <param name="condition">Optional filter.</param>
    public static UpdateOperation UpdateVectors(
        IReadOnlyList<PointVectors> pointVectors,
        Filter? condition = null)
    {
        ArgumentNullException.ThrowIfNull(pointVectors, nameof(pointVectors));
        condition?.Validate();
        return new UpdateVectorsOperation(pointVectors, condition);
    }

    /// <summary>Deletes vectors by filter.</summary>
    /// <param name="filter">Filter.</param>
    /// <param name="vectorNames">Vector names.</param>
    public static UpdateOperation DeleteVectorsByFilter(Filter filter, IReadOnlyList<string> vectorNames)
    {
        ArgumentNullException.ThrowIfNull(filter, nameof(filter));
        ArgumentNullException.ThrowIfNull(vectorNames, nameof(vectorNames));
        filter.Validate();
        return new DeleteVectorsByFilterOperation(filter, vectorNames);
    }

    /// <summary>Deletes vectors by id.</summary>
    /// <param name="pointIds">Point ids.</param>
    /// <param name="vectorNames">Vector names.</param>
    public static UpdateOperation DeleteVectors(IReadOnlyList<PointId> pointIds, IReadOnlyList<string> vectorNames)
    {
        ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
        ArgumentNullException.ThrowIfNull(vectorNames, nameof(vectorNames));
        return new DeleteVectorsOperation(pointIds, vectorNames);
    }

    /// <summary>Sets payload on points, optionally under a key.</summary>
    /// <param name="pointIds">Point ids.</param>
    /// <param name="payloadJson">Payload JSON object string.</param>
    /// <param name="key">Optional key.</param>
    public static UpdateOperation SetPayload(IReadOnlyList<PointId> pointIds, string payloadJson, string? key = null)
    {
        ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson, nameof(payloadJson));
        return new SetPayloadOperation(pointIds, payloadJson, key);
    }

    /// <summary>Sets payload by filter, optionally under a key.</summary>
    /// <param name="filter">Filter.</param>
    /// <param name="payloadJson">Payload JSON object string.</param>
    /// <param name="key">Optional key.</param>
    public static UpdateOperation SetPayloadByFilter(Filter filter, string payloadJson, string? key = null)
    {
        ArgumentNullException.ThrowIfNull(filter, nameof(filter));
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson, nameof(payloadJson));
        filter.Validate();
        return new SetPayloadByFilterOperation(filter, payloadJson, key);
    }

    /// <summary>Overwrites payload on points.</summary>
    /// <param name="pointIds">Point ids.</param>
    /// <param name="payloadJson">Payload JSON object string.</param>
    public static UpdateOperation OverwritePayload(IReadOnlyList<PointId> pointIds, string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson, nameof(payloadJson));
        return new OverwritePayloadOperation(pointIds, payloadJson);
    }

    /// <summary>Overwrites payload by filter.</summary>
    /// <param name="filter">Filter.</param>
    /// <param name="payloadJson">Payload JSON object string.</param>
    public static UpdateOperation OverwritePayloadByFilter(Filter filter, string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(filter, nameof(filter));
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson, nameof(payloadJson));
        filter.Validate();
        return new OverwritePayloadByFilterOperation(filter, payloadJson);
    }

    /// <summary>Deletes payload keys on points.</summary>
    /// <param name="pointIds">Point ids.</param>
    /// <param name="keys">Payload keys.</param>
    public static UpdateOperation DeletePayload(IReadOnlyList<PointId> pointIds, IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
        ArgumentNullException.ThrowIfNull(keys, nameof(keys));
        return new DeletePayloadOperation(pointIds, keys);
    }

    /// <summary>Deletes payload keys by filter.</summary>
    /// <param name="filter">Filter.</param>
    /// <param name="keys">Payload keys.</param>
    public static UpdateOperation DeletePayloadByFilter(Filter filter, IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(filter, nameof(filter));
        ArgumentNullException.ThrowIfNull(keys, nameof(keys));
        filter.Validate();
        return new DeletePayloadByFilterOperation(filter, keys);
    }

    /// <summary>Clears all payload on points.</summary>
    /// <param name="pointIds">Point ids.</param>
    public static UpdateOperation ClearPayload(IReadOnlyList<PointId> pointIds)
    {
        ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
        return new ClearPayloadOperation(pointIds);
    }

    /// <summary>Clears all payload by filter.</summary>
    /// <param name="filter">Filter.</param>
    public static UpdateOperation ClearPayloadByFilter(Filter filter)
    {
        ArgumentNullException.ThrowIfNull(filter, nameof(filter));
        filter.Validate();
        return new ClearPayloadByFilterOperation(filter);
    }

    /// <summary>Creates a payload field index with a schema type.</summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="schema">Schema type.</param>
    public static UpdateOperation CreateFieldIndex(string fieldName, PayloadSchemaType schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName, nameof(fieldName));
        return new CreateFieldIndexOperation(fieldName, schema);
    }

    /// <summary>Creates a payload field index with explicit params.</summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="indexParams">Index parameters.</param>
    public static UpdateOperation CreateFieldIndexWithParams(string fieldName, PayloadIndexParams indexParams)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName, nameof(fieldName));
        ArgumentNullException.ThrowIfNull(indexParams, nameof(indexParams));
        if (indexParams is IntegerPayloadIndex(var integer))
        {
            integer.Validate();
        }

        return new CreateFieldIndexWithParamsOperation(fieldName, indexParams);
    }

    /// <summary>Deletes a payload field index.</summary>
    /// <param name="fieldName">Field name.</param>
    public static UpdateOperation DeleteFieldIndex(string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName, nameof(fieldName));
        return new DeleteFieldIndexOperation(fieldName);
    }

    /// <summary>Creates a dense vector field.</summary>
    /// <param name="vectorName">Vector name; "" addresses the default unnamed vector.</param>
    /// <param name="size">Vector size (1..=65536).</param>
    /// <param name="distance">Distance metric.</param>
    /// <param name="multivectorConfig">Optional multi-vector config.</param>
    /// <param name="datatype">Optional storage datatype.</param>
    public static UpdateOperation CreateDenseVector(
        string vectorName,
        ulong size,
        Distance distance,
        MultiVectorConfig? multivectorConfig = null,
        VectorStorageDatatype? datatype = null)
    {
        ArgumentNullException.ThrowIfNull(vectorName, nameof(vectorName));
        EdgeValidation.CheckVectorSize(vectorName, size);
        return new CreateDenseVectorOperation(vectorName, size, distance, multivectorConfig, datatype);
    }

    /// <summary>Creates a sparse vector field (infallible).</summary>
    /// <param name="vectorName">Vector name; "" addresses the default unnamed vector.</param>
    /// <param name="modifier">Optional modifier.</param>
    /// <param name="datatype">Optional storage datatype.</param>
    public static UpdateOperation CreateSparseVector(
        string vectorName,
        Modifier? modifier = null,
        VectorStorageDatatype? datatype = null)
    {
        ArgumentNullException.ThrowIfNull(vectorName, nameof(vectorName));
        return new CreateSparseVectorOperation(vectorName, modifier, datatype);
    }

    /// <summary>Deletes a vector field (infallible).</summary>
    /// <param name="vectorName">Vector name; "" addresses the default unnamed vector.</param>
    public static UpdateOperation DeleteVectorName(string vectorName)
    {
        ArgumentNullException.ThrowIfNull(vectorName, nameof(vectorName));
        return new DeleteVectorNameOperation(vectorName);
    }

    /// <summary>Validates points, vectors, and filters without trusting factory paths.</summary>
    /// <remarks>
    /// Factories validate, but record constructors are public so direct
    /// construction can bypass them. EdgeShard calls this before dispatch.
    /// </remarks>
    /// <exception cref="EdgeInvalidArgumentException">When the shape is invalid.</exception>
    public void Validate()
    {
        switch (this)
        {
            case UpsertPointsOperation(var points, var condition, _):
                ArgumentNullException.ThrowIfNull(points, nameof(points));
                foreach (Point point in points)
                {
                    ArgumentNullException.ThrowIfNull(point, nameof(point));
                    point.Validate();
                }

                condition?.Validate();
                break;
            case DeletePointsOperation(var pointIds):
                ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
                foreach (PointId id in pointIds)
                {
                    ArgumentNullException.ThrowIfNull(id, nameof(id));
                }

                break;
            case DeletePointsByFilterOperation(var filter):
                ArgumentNullException.ThrowIfNull(filter, nameof(filter));
                filter.Validate();
                break;
            case UpdateVectorsOperation(var vectors, var condition):
                ArgumentNullException.ThrowIfNull(vectors, nameof(vectors));
                foreach (PointVectors pointVectors in vectors)
                {
                    ArgumentNullException.ThrowIfNull(pointVectors, nameof(pointVectors));
                    pointVectors.Validate();
                }

                condition?.Validate();
                break;
            case DeleteVectorsByFilterOperation(var filter, var names):
                ArgumentNullException.ThrowIfNull(filter, nameof(filter));
                ArgumentNullException.ThrowIfNull(names, nameof(names));
                filter.Validate();
                break;
            case DeleteVectorsOperation(var pointIds, var names):
                ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
                ArgumentNullException.ThrowIfNull(names, nameof(names));
                foreach (PointId id in pointIds)
                {
                    ArgumentNullException.ThrowIfNull(id, nameof(id));
                }

                break;
            case SetPayloadOperation(var pointIds, var payloadJson, _):
                ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
                ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson, nameof(payloadJson));
                break;
            case SetPayloadByFilterOperation(var filter, var payloadJson, _):
                ArgumentNullException.ThrowIfNull(filter, nameof(filter));
                ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson, nameof(payloadJson));
                filter.Validate();
                break;
            case OverwritePayloadOperation(var pointIds, var payloadJson):
                ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
                ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson, nameof(payloadJson));
                break;
            case OverwritePayloadByFilterOperation(var filter, var payloadJson):
                ArgumentNullException.ThrowIfNull(filter, nameof(filter));
                ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson, nameof(payloadJson));
                filter.Validate();
                break;
            case DeletePayloadOperation(var pointIds, var keys):
                ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
                ArgumentNullException.ThrowIfNull(keys, nameof(keys));
                break;
            case DeletePayloadByFilterOperation(var filter, var keys):
                ArgumentNullException.ThrowIfNull(filter, nameof(filter));
                ArgumentNullException.ThrowIfNull(keys, nameof(keys));
                filter.Validate();
                break;
            case ClearPayloadOperation(var pointIds):
                ArgumentNullException.ThrowIfNull(pointIds, nameof(pointIds));
                break;
            case ClearPayloadByFilterOperation(var filter):
                ArgumentNullException.ThrowIfNull(filter, nameof(filter));
                filter.Validate();
                break;
            case CreateFieldIndexOperation(var fieldName, _):
                ArgumentException.ThrowIfNullOrWhiteSpace(fieldName, nameof(fieldName));
                break;
            case CreateFieldIndexWithParamsOperation(var fieldName, var indexParams):
                ArgumentException.ThrowIfNullOrWhiteSpace(fieldName, nameof(fieldName));
                ArgumentNullException.ThrowIfNull(indexParams, nameof(indexParams));
                if (indexParams is IntegerPayloadIndex(var integer))
                {
                    integer.Validate();
                }

                break;
            case DeleteFieldIndexOperation(var fieldName):
                ArgumentException.ThrowIfNullOrWhiteSpace(fieldName, nameof(fieldName));
                break;
            case CreateDenseVectorOperation(var vectorName, var size, _, _, _):
                ArgumentNullException.ThrowIfNull(vectorName, nameof(vectorName));
                EdgeValidation.CheckVectorSize(vectorName, size);
                break;
            case CreateSparseVectorOperation(var vectorName, _, _):
                ArgumentNullException.ThrowIfNull(vectorName, nameof(vectorName));
                break;
            case DeleteVectorNameOperation(var vectorName):
                ArgumentNullException.ThrowIfNull(vectorName, nameof(vectorName));
                break;
            default:
                throw new EdgeInvalidArgumentException("unknown update operation shape");
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"UpdateOperation({Kind})";
}

/// <summary>Upsert-points operation.</summary>
/// <param name="Points">Points to upsert.</param>
/// <param name="Condition">Optional filter.</param>
/// <param name="UpdateMode">Optional upsert mode.</param>
public sealed record UpsertPointsOperation(
    IReadOnlyList<Point> Points,
    Filter? Condition,
    UpdateMode? UpdateMode) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "upsert_points";
}

/// <summary>Delete-points-by-id operation.</summary>
/// <param name="PointIds">Point ids.</param>
public sealed record DeletePointsOperation(IReadOnlyList<PointId> PointIds) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "delete_points";
}

/// <summary>Delete-points-by-filter operation.</summary>
/// <param name="Filter">Filter.</param>
public sealed record DeletePointsByFilterOperation(Filter Filter) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "delete_points_by_filter";
}

/// <summary>Replace-vectors operation.</summary>
/// <param name="PointVectors">Replacement vectors.</param>
/// <param name="Condition">Optional filter.</param>
public sealed record UpdateVectorsOperation(
    IReadOnlyList<PointVectors> PointVectors,
    Filter? Condition) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "update_vectors";
}

/// <summary>Delete-vectors-by-filter operation.</summary>
/// <param name="Filter">Filter.</param>
/// <param name="VectorNames">Vector names.</param>
public sealed record DeleteVectorsByFilterOperation(
    Filter Filter,
    IReadOnlyList<string> VectorNames) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "delete_vectors_by_filter";
}

/// <summary>Delete-vectors-by-id operation.</summary>
/// <param name="PointIds">Point ids.</param>
/// <param name="VectorNames">Vector names.</param>
public sealed record DeleteVectorsOperation(
    IReadOnlyList<PointId> PointIds,
    IReadOnlyList<string> VectorNames) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "delete_vectors";
}

/// <summary>Set-payload operation.</summary>
/// <param name="PointIds">Point ids.</param>
/// <param name="PayloadJson">Payload JSON object string.</param>
/// <param name="Key">Optional key.</param>
public sealed record SetPayloadOperation(
    IReadOnlyList<PointId> PointIds,
    string PayloadJson,
    string? Key) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "set_payload";
}

/// <summary>Set-payload-by-filter operation.</summary>
/// <param name="Filter">Filter.</param>
/// <param name="PayloadJson">Payload JSON object string.</param>
/// <param name="Key">Optional key.</param>
public sealed record SetPayloadByFilterOperation(
    Filter Filter,
    string PayloadJson,
    string? Key) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "set_payload_by_filter";
}

/// <summary>Overwrite-payload operation.</summary>
/// <param name="PointIds">Point ids.</param>
/// <param name="PayloadJson">Payload JSON object string.</param>
public sealed record OverwritePayloadOperation(
    IReadOnlyList<PointId> PointIds,
    string PayloadJson) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "overwrite_payload";
}

/// <summary>Overwrite-payload-by-filter operation.</summary>
/// <param name="Filter">Filter.</param>
/// <param name="PayloadJson">Payload JSON object string.</param>
public sealed record OverwritePayloadByFilterOperation(
    Filter Filter,
    string PayloadJson) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "overwrite_payload_by_filter";
}

/// <summary>Delete-payload-keys operation.</summary>
/// <param name="PointIds">Point ids.</param>
/// <param name="Keys">Payload keys.</param>
public sealed record DeletePayloadOperation(
    IReadOnlyList<PointId> PointIds,
    IReadOnlyList<string> Keys) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "delete_payload";
}

/// <summary>Delete-payload-keys-by-filter operation.</summary>
/// <param name="Filter">Filter.</param>
/// <param name="Keys">Payload keys.</param>
public sealed record DeletePayloadByFilterOperation(
    Filter Filter,
    IReadOnlyList<string> Keys) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "delete_payload_by_filter";
}

/// <summary>Clear-payload operation.</summary>
/// <param name="PointIds">Point ids.</param>
public sealed record ClearPayloadOperation(IReadOnlyList<PointId> PointIds) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "clear_payload";
}

/// <summary>Clear-payload-by-filter operation.</summary>
/// <param name="Filter">Filter.</param>
public sealed record ClearPayloadByFilterOperation(Filter Filter) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "clear_payload_by_filter";
}

/// <summary>Create field index with schema operation.</summary>
/// <param name="FieldName">Field name.</param>
/// <param name="Schema">Schema type.</param>
public sealed record CreateFieldIndexOperation(string FieldName, PayloadSchemaType Schema) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "create_field_index";
}

/// <summary>Create field index with params operation.</summary>
/// <param name="FieldName">Field name.</param>
/// <param name="IndexParams">Index parameters.</param>
public sealed record CreateFieldIndexWithParamsOperation(
    string FieldName,
    PayloadIndexParams IndexParams) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "create_field_index_with_params";
}

/// <summary>Delete field index operation.</summary>
/// <param name="FieldName">Field name.</param>
public sealed record DeleteFieldIndexOperation(string FieldName) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "delete_field_index";
}

/// <summary>Create dense vector field operation.</summary>
/// <param name="VectorName">Vector name.</param>
/// <param name="Size">Vector size.</param>
/// <param name="Distance">Distance metric.</param>
/// <param name="MultivectorConfig">Optional multi-vector config.</param>
/// <param name="Datatype">Optional storage datatype.</param>
public sealed record CreateDenseVectorOperation(
    string VectorName,
    ulong Size,
    Distance Distance,
    MultiVectorConfig? MultivectorConfig,
    VectorStorageDatatype? Datatype) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "create_dense_vector";
}

/// <summary>Create sparse vector field operation.</summary>
/// <param name="VectorName">Vector name.</param>
/// <param name="Modifier">Optional modifier.</param>
/// <param name="Datatype">Optional storage datatype.</param>
public sealed record CreateSparseVectorOperation(
    string VectorName,
    Modifier? Modifier,
    VectorStorageDatatype? Datatype) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "create_sparse_vector";
}

/// <summary>Delete vector field operation.</summary>
/// <param name="VectorName">Vector name.</param>
public sealed record DeleteVectorNameOperation(string VectorName) : UpdateOperation
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => "delete_vector_name";
}
