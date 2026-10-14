using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Factory-guard vs Validate() bypass pins for all 20 update operations, plus native round-trips.</summary>
public sealed class UpdateOperationGapsTests
{
    private sealed record ForgedOperation : UpdateOperation
    {
        public override string Kind => "forged";
    }

    private static Point ValidPoint(ulong id = 1) => TestHelpers.FourDimPoint(id, 1f, 2f, 3f, 4f);

    private static Point InvalidPoint() => new()
    {
        Id = PointId.FromNumber(1),
        Vector = Vector.Single([float.NaN]),
    };

    private static PointVectors ValidPointVectors(ulong id = 1) => new()
    {
        Id = PointId.FromNumber(id),
        Vector = Vector.Single([1f, 2f, 3f, 4f]),
    };

    private static PointVectors InvalidPointVectors() => new()
    {
        Id = PointId.FromNumber(1),
        Vector = Vector.Single([float.NaN]),
    };

    private static Filter ValidFilter() => new()
    {
        Must = [Condition.HasId([PointId.FromNumber(1)])],
    };

    private static Filter InvalidFilter() => new()
    {
        Must = [Condition.Field(new FieldCondition { Key = "", Match = Match.CreateText("x") })],
    };

    // UpsertPoints (UpdateOperation.cs:85-93 vs :309-317).

    [Fact]
    public void UpsertPoints_NullPoints_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.UpsertPoints(null!));
        Assert.Throws<ArgumentNullException>(() => new UpsertPointsOperation(null!, null, null).Validate());
    }

    [Fact]
    public void UpsertPoints_NullElement_ThrowsArgumentNull()
    {
        List<Point> points = [null!];
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.UpsertPoints(points));
        Assert.Throws<ArgumentNullException>(() => new UpsertPointsOperation(points, null, null).Validate());
    }

    [Fact]
    public void UpsertPoints_InvalidPoint_ThrowsInvalidArgument()
    {
        List<Point> points = [InvalidPoint()];
        Assert.Throws<EdgeInvalidArgumentException>(() => UpdateOperation.UpsertPoints(points));
        Assert.Throws<EdgeInvalidArgumentException>(() => new UpsertPointsOperation(points, null, null).Validate());
    }

    [Fact]
    public void UpsertPoints_InvalidCondition_ThrowsArgument()
    {
        Filter filter = InvalidFilter();
        Assert.Throws<ArgumentException>(() => UpdateOperation.UpsertPoints([ValidPoint()], filter));
        Assert.Throws<ArgumentException>(() => new UpsertPointsOperation([ValidPoint()], filter, null).Validate());
    }

    [Fact]
    public void UpsertPoints_EmptyList_PassesValidation()
    {
        UpdateOperation operation = UpdateOperation.UpsertPoints([]);
        operation.Validate();
        Assert.IsType<UpsertPointsOperation>(operation);
    }

    [Fact]
    public void UpsertPoints_NullCondition_PassesValidation()
    {
        UpdateOperation operation = UpdateOperation.UpsertPoints([ValidPoint()], null, null);
        operation.Validate();
        UpsertPointsOperation upsert = Assert.IsType<UpsertPointsOperation>(operation);
        Assert.Null(upsert.Condition);
        Assert.Null(upsert.UpdateMode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(UpdateMode.Upsert)]
    [InlineData(UpdateMode.InsertOnly)]
    [InlineData(UpdateMode.UpdateOnly)]
    public void UpsertPoints_EachUpdateMode_Preserved(UpdateMode? mode)
    {
        UpdateOperation operation = UpdateOperation.UpsertPoints([ValidPoint()], null, mode);
        UpsertPointsOperation upsert = Assert.IsType<UpsertPointsOperation>(operation);
        Assert.Equal(mode, upsert.UpdateMode);
        string json = JsonSerializer.Serialize(operation);
        UpsertPointsOperation? restored = (UpsertPointsOperation?)JsonSerializer.Deserialize<UpdateOperation>(json);
        Assert.NotNull(restored);
        Assert.Equal(mode, restored.UpdateMode);
    }

    // DeletePoints (factory :99-101 skips element checks; Validate :319-326 checks).

    [Fact]
    public void DeletePoints_NullIds_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeletePoints(null!));
        Assert.Throws<ArgumentNullException>(() => new DeletePointsOperation(null!).Validate());
    }

    [Fact]
    public void DeletePoints_NullElement_PassesFactory()
    {
        UpdateOperation operation = UpdateOperation.DeletePoints([null!]);
        Assert.IsType<DeletePointsOperation>(operation);
    }

    [Fact]
    public void DeletePoints_NullElement_ThrowsArgumentNull_Validate()
    {
        Assert.Throws<ArgumentNullException>(() => new DeletePointsOperation([null!]).Validate());
    }

    [Fact]
    public void DeletePoints_EmptyList_PassesValidation()
    {
        UpdateOperation operation = UpdateOperation.DeletePoints([]);
        operation.Validate();
        Assert.IsType<DeletePointsOperation>(operation);
    }

    // DeletePointsByFilter (:108-109 vs :327-330).

    [Fact]
    public void DeletePointsByFilter_NullFilter_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeletePointsByFilter(null!));
        Assert.Throws<ArgumentNullException>(() => new DeletePointsByFilterOperation(null!).Validate());
    }

    [Fact]
    public void DeletePointsByFilter_InvalidFilter_ThrowsArgument()
    {
        Filter filter = InvalidFilter();
        Assert.Throws<ArgumentException>(() => UpdateOperation.DeletePointsByFilter(filter));
        Assert.Throws<ArgumentException>(() => new DeletePointsByFilterOperation(filter).Validate());
    }

    // UpdateVectors (factory :120-122 skips element checks; Validate :331-339 checks).

    [Fact]
    public void UpdateVectors_NullList_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.UpdateVectors(null!));
        Assert.Throws<ArgumentNullException>(() => new UpdateVectorsOperation(null!, null).Validate());
    }

    [Fact]
    public void UpdateVectors_NullElement_PassesFactory()
    {
        UpdateOperation operation = UpdateOperation.UpdateVectors([null!]);
        Assert.IsType<UpdateVectorsOperation>(operation);
    }

    [Fact]
    public void UpdateVectors_NullElement_ThrowsArgumentNull_Validate()
    {
        Assert.Throws<ArgumentNullException>(() => new UpdateVectorsOperation([null!], null).Validate());
    }

    [Fact]
    public void UpdateVectors_InvalidElement_PassesFactory()
    {
        UpdateOperation operation = UpdateOperation.UpdateVectors([InvalidPointVectors()]);
        Assert.IsType<UpdateVectorsOperation>(operation);
    }

    [Fact]
    public void UpdateVectors_InvalidElement_ThrowsInvalidArgument_Validate()
    {
        List<PointVectors> vectors = [InvalidPointVectors()];
        Assert.Throws<EdgeInvalidArgumentException>(() => new UpdateVectorsOperation(vectors, null).Validate());
    }

    [Fact]
    public void UpdateVectors_InvalidCondition_ThrowsArgument()
    {
        Filter filter = InvalidFilter();
        List<PointVectors> vectors = [ValidPointVectors()];
        Assert.Throws<ArgumentException>(() => UpdateOperation.UpdateVectors(vectors, filter));
        Assert.Throws<ArgumentException>(() => new UpdateVectorsOperation(vectors, filter).Validate());
    }

    [Fact]
    public void UpdateVectors_EmptyList_PassesValidation()
    {
        UpdateOperation operation = UpdateOperation.UpdateVectors([]);
        operation.Validate();
        Assert.IsType<UpdateVectorsOperation>(operation);
    }

    // DeleteVectorsByFilter (:130-132 vs :341-345).

    [Fact]
    public void DeleteVectorsByFilter_NullFilter_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeleteVectorsByFilter(null!, [""]));
        Assert.Throws<ArgumentNullException>(() => new DeleteVectorsByFilterOperation(null!, [""]).Validate());
    }

    [Fact]
    public void DeleteVectorsByFilter_NullNames_ThrowsArgumentNull()
    {
        Filter filter = ValidFilter();
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeleteVectorsByFilter(filter, null!));
        Assert.Throws<ArgumentNullException>(() => new DeleteVectorsByFilterOperation(filter, null!).Validate());
    }

    [Fact]
    public void DeleteVectorsByFilter_InvalidFilter_ThrowsArgument()
    {
        Filter filter = InvalidFilter();
        Assert.Throws<ArgumentException>(() => UpdateOperation.DeleteVectorsByFilter(filter, [""]));
        Assert.Throws<ArgumentException>(() => new DeleteVectorsByFilterOperation(filter, [""]).Validate());
    }

    // DeleteVectors (:141-142 vs :346-354).

    [Fact]
    public void DeleteVectors_NullIds_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeleteVectors(null!, [""]));
        Assert.Throws<ArgumentNullException>(() => new DeleteVectorsOperation(null!, [""]).Validate());
    }

    [Fact]
    public void DeleteVectors_NullNames_ThrowsArgumentNull()
    {
        List<PointId> ids = [PointId.FromNumber(1)];
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeleteVectors(ids, null!));
        Assert.Throws<ArgumentNullException>(() => new DeleteVectorsOperation(ids, null!).Validate());
    }

    [Fact]
    public void DeleteVectors_NullIdElement_PassesFactory()
    {
        UpdateOperation operation = UpdateOperation.DeleteVectors([null!], [""]);
        Assert.IsType<DeleteVectorsOperation>(operation);
    }

    [Fact]
    public void DeleteVectors_NullIdElement_ThrowsArgumentNull_Validate()
    {
        Assert.Throws<ArgumentNullException>(() => new DeleteVectorsOperation([null!], [""]).Validate());
    }

    [Fact]
    public void DeleteVectors_EmptyLists_PassValidation()
    {
        UpdateOperation operation = UpdateOperation.DeleteVectors([], []);
        operation.Validate();
        Assert.IsType<DeleteVectorsOperation>(operation);
    }

    // SetPayload (:152-153 vs :355-358).

    [Fact]
    public void SetPayload_NullIds_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.SetPayload(null!, """{"a":1}"""));
        Assert.Throws<ArgumentNullException>(() => new SetPayloadOperation(null!, """{"a":1}""", null).Validate());
    }

    [Fact]
    public void SetPayload_NullJson_ThrowsArgumentNull()
    {
        List<PointId> ids = [PointId.FromNumber(1)];
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.SetPayload(ids, null!));
        Assert.Throws<ArgumentNullException>(() => new SetPayloadOperation(ids, null!, null).Validate());
    }

    [Fact]
    public void SetPayload_EmptyJson_ThrowsArgument()
    {
        List<PointId> ids = [PointId.FromNumber(1)];
        Assert.Throws<ArgumentException>(() => UpdateOperation.SetPayload(ids, ""));
        Assert.Throws<ArgumentException>(() => new SetPayloadOperation(ids, "", null).Validate());
    }

    [Fact]
    public void SetPayload_WhitespaceJson_ThrowsArgument()
    {
        List<PointId> ids = [PointId.FromNumber(1)];
        Assert.Throws<ArgumentException>(() => UpdateOperation.SetPayload(ids, "  "));
        Assert.Throws<ArgumentException>(() => new SetPayloadOperation(ids, "  ", null).Validate());
    }

    [Fact]
    public void SetPayload_NullKey_PassesValidation()
    {
        List<PointId> ids = [PointId.FromNumber(1)];
        UpdateOperation operation = UpdateOperation.SetPayload(ids, """{"a":1}""", null);
        operation.Validate();
        Assert.Null(Assert.IsType<SetPayloadOperation>(operation).Key);
    }

    [Fact]
    public void SetPayload_Key_Preserved()
    {
        UpdateOperation operation = UpdateOperation.SetPayload([PointId.FromNumber(1)], """{"a":1}""", "k");
        SetPayloadOperation set = Assert.IsType<SetPayloadOperation>(operation);
        Assert.Equal("k", set.Key);
        Assert.Equal("""{"a":1}""", set.PayloadJson);
    }

    // SetPayloadByFilter (:163-165 vs :359-363).

    [Fact]
    public void SetPayloadByFilter_NullFilter_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.SetPayloadByFilter(null!, """{"a":1}"""));
        Assert.Throws<ArgumentNullException>(() => new SetPayloadByFilterOperation(null!, """{"a":1}""", null).Validate());
    }

    [Fact]
    public void SetPayloadByFilter_NullJson_ThrowsArgumentNull()
    {
        Filter filter = ValidFilter();
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.SetPayloadByFilter(filter, null!));
        Assert.Throws<ArgumentNullException>(() => new SetPayloadByFilterOperation(filter, null!, null).Validate());
    }

    [Fact]
    public void SetPayloadByFilter_EmptyJson_ThrowsArgument()
    {
        Filter filter = ValidFilter();
        Assert.Throws<ArgumentException>(() => UpdateOperation.SetPayloadByFilter(filter, ""));
        Assert.Throws<ArgumentException>(() => new SetPayloadByFilterOperation(filter, "", null).Validate());
    }

    [Fact]
    public void SetPayloadByFilter_InvalidFilter_ThrowsArgument()
    {
        Filter filter = InvalidFilter();
        Assert.Throws<ArgumentException>(() => UpdateOperation.SetPayloadByFilter(filter, """{"a":1}"""));
        Assert.Throws<ArgumentException>(() => new SetPayloadByFilterOperation(filter, """{"a":1}""", null).Validate());
    }

    // OverwritePayload (:174-175 vs :364-367).

    [Fact]
    public void OverwritePayload_NullIds_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.OverwritePayload(null!, """{"a":1}"""));
        Assert.Throws<ArgumentNullException>(() => new OverwritePayloadOperation(null!, """{"a":1}""").Validate());
    }

    [Fact]
    public void OverwritePayload_NullJson_ThrowsArgumentNull()
    {
        List<PointId> ids = [PointId.FromNumber(1)];
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.OverwritePayload(ids, null!));
        Assert.Throws<ArgumentNullException>(() => new OverwritePayloadOperation(ids, null!).Validate());
    }

    [Fact]
    public void OverwritePayload_EmptyJson_ThrowsArgument()
    {
        List<PointId> ids = [PointId.FromNumber(1)];
        Assert.Throws<ArgumentException>(() => UpdateOperation.OverwritePayload(ids, ""));
        Assert.Throws<ArgumentException>(() => new OverwritePayloadOperation(ids, "").Validate());
    }

    [Fact]
    public void OverwritePayload_WhitespaceJson_ThrowsArgument()
    {
        List<PointId> ids = [PointId.FromNumber(1)];
        Assert.Throws<ArgumentException>(() => UpdateOperation.OverwritePayload(ids, "  "));
        Assert.Throws<ArgumentException>(() => new OverwritePayloadOperation(ids, "  ").Validate());
    }

    // OverwritePayloadByFilter (:184-186 vs :368-372).

    [Fact]
    public void OverwritePayloadByFilter_NullFilter_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.OverwritePayloadByFilter(null!, """{"a":1}"""));
        Assert.Throws<ArgumentNullException>(() => new OverwritePayloadByFilterOperation(null!, """{"a":1}""").Validate());
    }

    [Fact]
    public void OverwritePayloadByFilter_NullJson_ThrowsArgumentNull()
    {
        Filter filter = ValidFilter();
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.OverwritePayloadByFilter(filter, null!));
        Assert.Throws<ArgumentNullException>(() => new OverwritePayloadByFilterOperation(filter, null!).Validate());
    }

    [Fact]
    public void OverwritePayloadByFilter_EmptyJson_ThrowsArgument()
    {
        Filter filter = ValidFilter();
        Assert.Throws<ArgumentException>(() => UpdateOperation.OverwritePayloadByFilter(filter, ""));
        Assert.Throws<ArgumentException>(() => new OverwritePayloadByFilterOperation(filter, "").Validate());
    }

    [Fact]
    public void OverwritePayloadByFilter_InvalidFilter_ThrowsArgument()
    {
        Filter filter = InvalidFilter();
        Assert.Throws<ArgumentException>(() => UpdateOperation.OverwritePayloadByFilter(filter, """{"a":1}"""));
        Assert.Throws<ArgumentException>(() => new OverwritePayloadByFilterOperation(filter, """{"a":1}""").Validate());
    }

    // DeletePayload (:195-196 vs :373-376).

    [Fact]
    public void DeletePayload_NullIds_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeletePayload(null!, ["a"]));
        Assert.Throws<ArgumentNullException>(() => new DeletePayloadOperation(null!, ["a"]).Validate());
    }

    [Fact]
    public void DeletePayload_NullKeys_ThrowsArgumentNull()
    {
        List<PointId> ids = [PointId.FromNumber(1)];
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeletePayload(ids, null!));
        Assert.Throws<ArgumentNullException>(() => new DeletePayloadOperation(ids, null!).Validate());
    }

    [Fact]
    public void DeletePayload_EmptyKeys_PassValidation()
    {
        UpdateOperation operation = UpdateOperation.DeletePayload([PointId.FromNumber(1)], []);
        operation.Validate();
        Assert.IsType<DeletePayloadOperation>(operation);
    }

    // DeletePayloadByFilter (:204-207 vs :377-381).

    [Fact]
    public void DeletePayloadByFilter_NullFilter_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeletePayloadByFilter(null!, ["a"]));
        Assert.Throws<ArgumentNullException>(() => new DeletePayloadByFilterOperation(null!, ["a"]).Validate());
    }

    [Fact]
    public void DeletePayloadByFilter_NullKeys_ThrowsArgumentNull()
    {
        Filter filter = ValidFilter();
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeletePayloadByFilter(filter, null!));
        Assert.Throws<ArgumentNullException>(() => new DeletePayloadByFilterOperation(filter, null!).Validate());
    }

    [Fact]
    public void DeletePayloadByFilter_InvalidFilter_ThrowsArgument()
    {
        Filter filter = InvalidFilter();
        Assert.Throws<ArgumentException>(() => UpdateOperation.DeletePayloadByFilter(filter, ["a"]));
        Assert.Throws<ArgumentException>(() => new DeletePayloadByFilterOperation(filter, ["a"]).Validate());
    }

    // ClearPayload (:214-215 vs :382-384).

    [Fact]
    public void ClearPayload_NullIds_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.ClearPayload(null!));
        Assert.Throws<ArgumentNullException>(() => new ClearPayloadOperation(null!).Validate());
    }

    [Fact]
    public void ClearPayload_EmptyIds_PassValidation()
    {
        UpdateOperation operation = UpdateOperation.ClearPayload([]);
        operation.Validate();
        Assert.IsType<ClearPayloadOperation>(operation);
    }

    // ClearPayloadByFilter (:223-224 vs :385-388).

    [Fact]
    public void ClearPayloadByFilter_NullFilter_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.ClearPayloadByFilter(null!));
        Assert.Throws<ArgumentNullException>(() => new ClearPayloadByFilterOperation(null!).Validate());
    }

    [Fact]
    public void ClearPayloadByFilter_InvalidFilter_ThrowsArgument()
    {
        Filter filter = InvalidFilter();
        Assert.Throws<ArgumentException>(() => UpdateOperation.ClearPayloadByFilter(filter));
        Assert.Throws<ArgumentException>(() => new ClearPayloadByFilterOperation(filter).Validate());
    }

    // CreateFieldIndex (:233 vs :389-391).

    [Fact]
    public void CreateFieldIndex_NullName_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.CreateFieldIndex(null!, PayloadSchemaType.Keyword));
        Assert.Throws<ArgumentNullException>(() => new CreateFieldIndexOperation(null!, PayloadSchemaType.Keyword).Validate());
    }

    [Fact]
    public void CreateFieldIndex_EmptyName_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => UpdateOperation.CreateFieldIndex("", PayloadSchemaType.Keyword));
        Assert.Throws<ArgumentException>(() => new CreateFieldIndexOperation("", PayloadSchemaType.Keyword).Validate());
    }

    [Fact]
    public void CreateFieldIndex_WhitespaceName_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => UpdateOperation.CreateFieldIndex("  ", PayloadSchemaType.Keyword));
        Assert.Throws<ArgumentException>(() => new CreateFieldIndexOperation("  ", PayloadSchemaType.Keyword).Validate());
    }

    [Theory]
    [InlineData(PayloadSchemaType.Keyword)]
    [InlineData(PayloadSchemaType.Integer)]
    [InlineData(PayloadSchemaType.Float)]
    [InlineData(PayloadSchemaType.Geo)]
    [InlineData(PayloadSchemaType.Text)]
    [InlineData(PayloadSchemaType.Bool)]
    [InlineData(PayloadSchemaType.Datetime)]
    [InlineData(PayloadSchemaType.Uuid)]
    public void CreateFieldIndex_EachSchemaType_Preserved(PayloadSchemaType schema)
    {
        UpdateOperation operation = UpdateOperation.CreateFieldIndex("field", schema);
        CreateFieldIndexOperation create = Assert.IsType<CreateFieldIndexOperation>(operation);
        Assert.Equal("field", create.FieldName);
        Assert.Equal(schema, create.Schema);
        operation.Validate();
    }

    // CreateFieldIndexWithParams (:242-247 vs :392-400).

    [Fact]
    public void CreateFieldIndexWithParams_NullName_ThrowsArgumentNull()
    {
        PayloadIndexParams indexParams = PayloadIndexParams.Keyword(new KeywordIndexParams());
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.CreateFieldIndexWithParams(null!, indexParams));
        Assert.Throws<ArgumentNullException>(() => new CreateFieldIndexWithParamsOperation(null!, indexParams).Validate());
    }

    [Fact]
    public void CreateFieldIndexWithParams_NullParams_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.CreateFieldIndexWithParams("field", null!));
        Assert.Throws<ArgumentNullException>(() => new CreateFieldIndexWithParamsOperation("field", null!).Validate());
    }

    [Fact]
    public void CreateFieldIndexWithParams_DisabledInteger_ThrowsInvalidArgument()
    {
        PayloadIndexParams indexParams = PayloadIndexParams.Integer(new IntegerIndexParams { Lookup = false, Range = false });
        Assert.Throws<EdgeInvalidArgumentException>(() => UpdateOperation.CreateFieldIndexWithParams("count", indexParams));
        Assert.Throws<EdgeInvalidArgumentException>(() => new CreateFieldIndexWithParamsOperation("count", indexParams).Validate());
    }

    public static TheoryData<bool?, bool?, bool> IntegerLookupRangeCases() => new()
    {
        { true, false, false },
        { false, true, false },
        { true, true, false },
        { null, null, false },
        { false, false, true },
    };

    [Theory]
    [MemberData(nameof(IntegerLookupRangeCases))]
    public void CreateFieldIndexWithParams_IntegerLookupRange_Preserved(bool? lookup, bool? range, bool throws)
    {
        PayloadIndexParams indexParams = PayloadIndexParams.Integer(new IntegerIndexParams { Lookup = lookup, Range = range });
        if (throws)
        {
            Assert.Throws<EdgeInvalidArgumentException>(() => UpdateOperation.CreateFieldIndexWithParams("count", indexParams));
            Assert.Throws<EdgeInvalidArgumentException>(() => new CreateFieldIndexWithParamsOperation("count", indexParams).Validate());
        }
        else
        {
            UpdateOperation operation = UpdateOperation.CreateFieldIndexWithParams("count", indexParams);
            operation.Validate();
            CreateFieldIndexWithParamsOperation create = Assert.IsType<CreateFieldIndexWithParamsOperation>(operation);
            IntegerPayloadIndex integer = Assert.IsType<IntegerPayloadIndex>(create.IndexParams);
            Assert.Equal(lookup, integer.Config.Lookup);
            Assert.Equal(range, integer.Config.Range);
        }
    }

    public static TheoryData<PayloadIndexParams> NonIntegerParamsCases() => new()
    {
        { PayloadIndexParams.Keyword(new KeywordIndexParams()) },
        { PayloadIndexParams.Float(new FloatIndexParams()) },
        { PayloadIndexParams.Geo(new GeoIndexParams()) },
        { PayloadIndexParams.Text(new TextIndexParams()) },
        { PayloadIndexParams.Bool(new BoolIndexParams()) },
        { PayloadIndexParams.Datetime(new DatetimeIndexParams()) },
        { PayloadIndexParams.Uuid(new UuidIndexParams()) },
    };

    [Theory]
    [MemberData(nameof(NonIntegerParamsCases))]
    public void CreateFieldIndexWithParams_NonIntegerSkipsIntegerValidate(PayloadIndexParams indexParams)
    {
        UpdateOperation operation = UpdateOperation.CreateFieldIndexWithParams("field", indexParams);
        operation.Validate();
        Assert.IsType<CreateFieldIndexWithParamsOperation>(operation);
    }

    // DeleteFieldIndex (:256 vs :401-403).

    [Fact]
    public void DeleteFieldIndex_NullName_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeleteFieldIndex(null!));
        Assert.Throws<ArgumentNullException>(() => new DeleteFieldIndexOperation(null!).Validate());
    }

    [Fact]
    public void DeleteFieldIndex_EmptyName_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => UpdateOperation.DeleteFieldIndex(""));
        Assert.Throws<ArgumentException>(() => new DeleteFieldIndexOperation("").Validate());
    }

    [Fact]
    public void DeleteFieldIndex_WhitespaceName_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => UpdateOperation.DeleteFieldIndex("  "));
        Assert.Throws<ArgumentException>(() => new DeleteFieldIndexOperation("  ").Validate());
    }

    // CreateDenseVector (:273-274 vs :404-407).

    [Fact]
    public void CreateDenseVector_NullName_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.CreateDenseVector(null!, 4, Distance.Dot));
        Assert.Throws<ArgumentNullException>(() => new CreateDenseVectorOperation(null!, 4, Distance.Dot, null, null).Validate());
    }

    [Fact]
    public void CreateDenseVector_EmptyName_PassesValidation()
    {
        UpdateOperation operation = UpdateOperation.CreateDenseVector("", 4, Distance.Dot);
        operation.Validate();
        Assert.Equal("", Assert.IsType<CreateDenseVectorOperation>(operation).VectorName);
    }

    [Theory]
    [InlineData(1ul, false)]
    [InlineData(65536ul, false)]
    [InlineData(65537ul, true)]
    public void CreateDenseVector_SizeBoundaries_PassOrThrow(ulong size, bool throws)
    {
        if (throws)
        {
            Assert.Throws<EdgeInvalidArgumentException>(() => UpdateOperation.CreateDenseVector("vec", size, Distance.Dot));
            Assert.Throws<EdgeInvalidArgumentException>(() => new CreateDenseVectorOperation("vec", size, Distance.Dot, null, null).Validate());
        }
        else
        {
            UpdateOperation operation = UpdateOperation.CreateDenseVector("vec", size, Distance.Dot);
            operation.Validate();
            Assert.Equal(size, Assert.IsType<CreateDenseVectorOperation>(operation).Size);
        }
    }

    [Fact]
    public void CreateDenseVector_MultivectorAndDatatype_Preserved()
    {
        MultiVectorConfig multivector = new() { Comparator = MultiVectorComparator.MaxSim };
        UpdateOperation operation = UpdateOperation.CreateDenseVector("vec", 4, Distance.Cosine, multivector, VectorStorageDatatype.Float32);
        operation.Validate();
        CreateDenseVectorOperation create = Assert.IsType<CreateDenseVectorOperation>(operation);
        Assert.Equal(MultiVectorComparator.MaxSim, create.MultivectorConfig!.Comparator);
        Assert.Equal(VectorStorageDatatype.Float32, create.Datatype);
        Assert.Equal(Distance.Cosine, create.Distance);
        string json = JsonSerializer.Serialize(operation);
        CreateDenseVectorOperation? restored = (CreateDenseVectorOperation?)JsonSerializer.Deserialize<UpdateOperation>(json);
        Assert.NotNull(restored);
        Assert.Equal(VectorStorageDatatype.Float32, restored.Datatype);
        Assert.Equal(MultiVectorComparator.MaxSim, restored.MultivectorConfig!.Comparator);
    }

    // CreateSparseVector (:287 vs :408-409).

    [Fact]
    public void CreateSparseVector_NullName_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.CreateSparseVector(null!));
        Assert.Throws<ArgumentNullException>(() => new CreateSparseVectorOperation(null!, null, null).Validate());
    }

    [Fact]
    public void CreateSparseVector_EmptyName_PassesValidation()
    {
        UpdateOperation operation = UpdateOperation.CreateSparseVector("");
        operation.Validate();
        Assert.Equal("", Assert.IsType<CreateSparseVectorOperation>(operation).VectorName);
    }

    [Fact]
    public void CreateSparseVector_ModifierAndDatatype_Preserved()
    {
        UpdateOperation operation = UpdateOperation.CreateSparseVector("text", Modifier.Idf, VectorStorageDatatype.Uint8);
        operation.Validate();
        CreateSparseVectorOperation create = Assert.IsType<CreateSparseVectorOperation>(operation);
        Assert.Equal(Modifier.Idf, create.Modifier);
        Assert.Equal(VectorStorageDatatype.Uint8, create.Datatype);
        string json = JsonSerializer.Serialize(operation);
        CreateSparseVectorOperation? restored = (CreateSparseVectorOperation?)JsonSerializer.Deserialize<UpdateOperation>(json);
        Assert.NotNull(restored);
        Assert.Equal(Modifier.Idf, restored.Modifier);
        Assert.Equal(VectorStorageDatatype.Uint8, restored.Datatype);
    }

    // DeleteVectorName (:295 vs :411-412).

    [Fact]
    public void DeleteVectorName_NullName_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateOperation.DeleteVectorName(null!));
        Assert.Throws<ArgumentNullException>(() => new DeleteVectorNameOperation(null!).Validate());
    }

    [Fact]
    public void DeleteVectorName_EmptyName_PassesValidation()
    {
        UpdateOperation operation = UpdateOperation.DeleteVectorName("");
        operation.Validate();
        Assert.Equal("", Assert.IsType<DeleteVectorNameOperation>(operation).VectorName);
    }

    // Unknown shape (:415), Kind strings, ToString, JSON round-trips.

    [Fact]
    public void UpdateOperation_UnknownShape_ThrowsInvalidArgument()
    {
        UpdateOperation forged = new ForgedOperation();
        Assert.Throws<EdgeInvalidArgumentException>(() => forged.Validate());
    }

    public static TheoryData<UpdateOperation, string> AllTwentyOperations() => new()
    {
        { UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f)]), "upsert_points" },
        { UpdateOperation.DeletePoints([PointId.FromNumber(1)]), "delete_points" },
        { UpdateOperation.DeletePointsByFilter(new Filter { Must = [Condition.HasId([PointId.FromNumber(1)])] }), "delete_points_by_filter" },
        { UpdateOperation.UpdateVectors([new PointVectors { Id = PointId.FromNumber(1), Vector = Vector.Single([1f, 2f, 3f, 4f]) }]), "update_vectors" },
        { UpdateOperation.DeleteVectorsByFilter(new Filter { Must = [Condition.HasId([PointId.FromNumber(1)])] }, [""]), "delete_vectors_by_filter" },
        { UpdateOperation.DeleteVectors([PointId.FromNumber(1)], [""]), "delete_vectors" },
        { UpdateOperation.SetPayload([PointId.FromNumber(1)], """{"a":1}"""), "set_payload" },
        { UpdateOperation.SetPayloadByFilter(new Filter { Must = [Condition.HasId([PointId.FromNumber(1)])] }, """{"a":1}"""), "set_payload_by_filter" },
        { UpdateOperation.OverwritePayload([PointId.FromNumber(1)], """{"a":1}"""), "overwrite_payload" },
        { UpdateOperation.OverwritePayloadByFilter(new Filter { Must = [Condition.HasId([PointId.FromNumber(1)])] }, """{"a":1}"""), "overwrite_payload_by_filter" },
        { UpdateOperation.DeletePayload([PointId.FromNumber(1)], ["a"]), "delete_payload" },
        { UpdateOperation.DeletePayloadByFilter(new Filter { Must = [Condition.HasId([PointId.FromNumber(1)])] }, ["a"]), "delete_payload_by_filter" },
        { UpdateOperation.ClearPayload([PointId.FromNumber(1)]), "clear_payload" },
        { UpdateOperation.ClearPayloadByFilter(new Filter { Must = [Condition.HasId([PointId.FromNumber(1)])] }), "clear_payload_by_filter" },
        { UpdateOperation.CreateFieldIndex("field", PayloadSchemaType.Keyword), "create_field_index" },
        { UpdateOperation.CreateFieldIndexWithParams("field", PayloadIndexParams.Keyword(new KeywordIndexParams())), "create_field_index_with_params" },
        { UpdateOperation.DeleteFieldIndex("field"), "delete_field_index" },
        { UpdateOperation.CreateDenseVector("extra", 4, Distance.Dot), "create_dense_vector" },
        { UpdateOperation.CreateSparseVector("text"), "create_sparse_vector" },
        { UpdateOperation.DeleteVectorName("extra"), "delete_vector_name" },
    };

    [Theory]
    [MemberData(nameof(AllTwentyOperations))]
    public void UpdateOperation_All20Kinds_Preserved(UpdateOperation operation, string kind)
    {
        Assert.Equal(kind, operation.Kind);
        operation.Validate();
    }

    [Theory]
    [MemberData(nameof(AllTwentyOperations))]
    public void UpdateOperation_ToString_ContainsKind(UpdateOperation operation, string kind)
    {
        Assert.Equal(kind, operation.Kind);
        Assert.Contains(operation.Kind, operation.ToString());
    }

    [Theory]
    [MemberData(nameof(AllTwentyOperations))]
    public void UpdateOperation_All20Kinds_RoundTripThroughJson(UpdateOperation operation, string kind)
    {
        string json = JsonSerializer.Serialize(operation);
        Assert.Contains(kind, json);
        UpdateOperation? restored = JsonSerializer.Deserialize<UpdateOperation>(json);
        Assert.NotNull(restored);
        Assert.Equal(kind, restored.Kind);
        Assert.Equal(operation.GetType(), restored.GetType());
        restored.Validate();
    }

    // Native pins.

    private static EdgeShard TitledShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["title"] = "one" },
                },
                TestHelpers.FourDimPoint(2, 2f, 0f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["title"] = "two" },
                },
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static EdgeShard LetteredShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["a"] = 1, ["b"] = 2 },
                },
                TestHelpers.FourDimPoint(2, 2f, 0f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["a"] = 1, ["b"] = 2 },
                },
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static EdgeShard TwoVectorShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
                ["vec2"] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
            },
        };
        EdgeShard shard = EdgeShard.Create(dir, config);
        try
        {
            Point first = TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
            {
                Vector = Vector.Named(new Dictionary<string, NamedVector>
                {
                    [""] = NamedVector.Dense([1f, 2f, 3f, 4f]),
                    ["vec2"] = NamedVector.Dense([5f, 6f, 7f, 8f]),
                }),
            };
            Point second = TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f) with
            {
                Vector = Vector.Named(new Dictionary<string, NamedVector>
                {
                    [""] = NamedVector.Dense([2f, 3f, 4f, 5f]),
                    ["vec2"] = NamedVector.Dense([6f, 7f, 8f, 9f]),
                }),
            };
            shard.Update(UpdateOperation.UpsertPoints([first, second]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static JsonObject RetrievePayload(EdgeShard shard, ulong id)
    {
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(id)],
        });
        Record single = Assert.Single(records);
        return single.Payload is null ? new JsonObject() : (JsonObject)JsonNode.Parse(single.Payload)!;
    }

    private static string RetrieveVector(EdgeShard shard, ulong id)
    {
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(id)],
            WithVector = WithVector.Bool(true),
        });
        Record single = Assert.Single(records);
        Assert.NotNull(single.Vector);
        return single.Vector;
    }

    private static Filter HasIdFilter(ulong id) => new()
    {
        Must = [Condition.HasId([PointId.FromNumber(id)])],
    };

    [Fact]
    public void DeleteVectorsByFilter_RemovesNamedFieldFromMatches_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TwoVectorShard(out _);
        shard.Update(UpdateOperation.DeleteVectorsByFilter(HasIdFilter(1), ["vec2"]));
        Assert.DoesNotContain("vec2", RetrieveVector(shard, 1));
        Assert.Contains("vec2", RetrieveVector(shard, 2));
    }

    [Fact]
    public void SetPayloadByFilter_MergesKeysOnMatches_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TitledShard(out _);
        shard.Update(UpdateOperation.SetPayloadByFilter(HasIdFilter(1), """{"tag":"hot"}"""));
        JsonObject first = RetrievePayload(shard, 1);
        Assert.Equal("one", first["title"]!.GetValue<string>());
        Assert.Equal("hot", first["tag"]!.GetValue<string>());
        JsonObject second = RetrievePayload(shard, 2);
        Assert.Equal("two", second["title"]!.GetValue<string>());
        Assert.Null(second["tag"]);
    }

    [Fact]
    public void OverwritePayloadByFilter_ReplacesWholesaleOnMatches_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TitledShard(out _);
        shard.Update(UpdateOperation.OverwritePayloadByFilter(HasIdFilter(1), """{"only":"yes"}"""));
        JsonObject first = RetrievePayload(shard, 1);
        Assert.Equal("yes", first["only"]!.GetValue<string>());
        Assert.Null(first["title"]);
        JsonObject second = RetrievePayload(shard, 2);
        Assert.Equal("two", second["title"]!.GetValue<string>());
    }

    [Fact]
    public void DeletePayloadByFilter_RemovesKeysFromMatches_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = LetteredShard(out _);
        shard.Update(UpdateOperation.DeletePayloadByFilter(HasIdFilter(1), ["a"]));
        JsonObject first = RetrievePayload(shard, 1);
        Assert.Null(first["a"]);
        Assert.Equal(2, first["b"]!.GetValue<int>());
        JsonObject second = RetrievePayload(shard, 2);
        Assert.Equal(1, second["a"]!.GetValue<int>());
    }

    [Fact]
    public void UpdateVectors_WithCondition_AppliesToMatches_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TitledShard(out _);
        shard.Update(UpdateOperation.UpdateVectors(
        [
            new PointVectors { Id = PointId.FromNumber(1), Vector = Vector.Single([9f, 8f, 7f, 6f]) },
        ],
        HasIdFilter(1)));
        JsonNode vector = JsonNode.Parse(RetrieveVector(shard, 1))!;
        Assert.Equal([9f, 8f, 7f, 6f], ((JsonArray)vector["Values"]!).Select(node => node!.GetValue<float>()));
    }

    [Fact]
    public void UpsertPoints_WithCondition_AppliesToMatches_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TitledShard(out _);
        Point replacement = TestHelpers.FourDimPoint(1, 9f, 8f, 7f, 6f);
        shard.Update(UpdateOperation.UpsertPoints([replacement], HasIdFilter(1)));
        JsonNode vector = JsonNode.Parse(RetrieveVector(shard, 1))!;
        Assert.Equal([9f, 8f, 7f, 6f], ((JsonArray)vector["Values"]!).Select(node => node!.GetValue<float>()));
    }

    [Fact]
    public void UpsertPoints_UpdateModeUpsert_ReplacesExisting_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TitledShard(out _);
        Point replacement = TestHelpers.FourDimPoint(1, 9f, 9f, 9f, 9f) with
        {
            Payload = new JsonObject { ["title"] = "two" },
        };
        shard.Update(UpdateOperation.UpsertPoints([replacement], null, UpdateMode.Upsert));
        Assert.Equal("two", RetrievePayload(shard, 1)["title"]!.GetValue<string>());
    }

    [Fact]
    public void UpsertPoints_UpdateModeInsertOnly_PreservesExisting_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TitledShard(out _);
        Point overwrite = TestHelpers.FourDimPoint(1, 9f, 9f, 9f, 9f) with
        {
            Payload = new JsonObject { ["title"] = "two" },
        };
        shard.Update(UpdateOperation.UpsertPoints([overwrite], null, UpdateMode.InsertOnly));
        Assert.Equal("one", RetrievePayload(shard, 1)["title"]!.GetValue<string>());
    }

    [Fact]
    public void UpsertPoints_UpdateModeUpdateOnly_UpdatesExisting_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TitledShard(out _);
        Point replacement = TestHelpers.FourDimPoint(1, 9f, 9f, 9f, 9f) with
        {
            Payload = new JsonObject { ["title"] = "two" },
        };
        shard.Update(UpdateOperation.UpsertPoints([replacement], null, UpdateMode.UpdateOnly));
        Assert.Equal("two", RetrievePayload(shard, 1)["title"]!.GetValue<string>());
    }

    [Fact]
    public void UpsertPoints_UpdateModeUpdateOnly_SkipsMissing_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TitledShard(out _);
        shard.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(99, 1f, 2f, 3f, 4f)], null, UpdateMode.UpdateOnly));
        Assert.Equal(2ul, shard.Count(new CountRequest()));
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(99)],
        });
        Assert.Empty(records);
    }

    [Fact]
    public void CreateDenseVector_AllowsWrites_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateDenseVector("extra", 4, Distance.Dot));
        Point extended = TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
        {
            Vector = Vector.Named(new Dictionary<string, NamedVector>
            {
                [""] = NamedVector.Dense([1f, 2f, 3f, 4f]),
                ["extra"] = NamedVector.Dense([5f, 6f, 7f, 8f]),
            }),
        };
        shard.Update(UpdateOperation.UpsertPoints([extended]));
        JsonObject map = (JsonObject)JsonNode.Parse(RetrieveVector(shard, 1))!["Map"]!;
        Assert.True(map.ContainsKey("extra"));
    }

    [Fact]
    public void CreateSparseVector_AllowsWrites_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateSparseVector("stext"));
        Point point = TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
        {
            Vector = Vector.Named(new Dictionary<string, NamedVector>
            {
                [""] = NamedVector.Dense([1f, 2f, 3f, 4f]),
                ["stext"] = NamedVector.Sparse(new SparseVector
                {
                    Indices = [1, 5, 9],
                    Values = [0.5f, 1.5f, 2.5f],
                }),
            }),
        };
        shard.Update(UpdateOperation.UpsertPoints([point]));
        Vector? roundTripped = JsonSerializer.Deserialize<Vector>(RetrieveVector(shard, 1));
        NamedVectorMap map = Assert.IsType<NamedVectorMap>(roundTripped);
        SparseNamedVector sparse = Assert.IsType<SparseNamedVector>(map.Map["stext"]);
        Assert.Equal([1u, 5u, 9u], sparse.Vector.Indices);
        Assert.Equal([0.5f, 1.5f, 2.5f], sparse.Vector.Values);
    }

    [Fact]
    public void DeleteVectorName_RemovesField_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateDenseVector("extra", 4, Distance.Dot));
        shard.Update(UpdateOperation.DeleteVectorName("extra"));
        shard.Update(UpdateOperation.CreateDenseVector("extra", 8, Distance.Cosine));
        Assert.Equal(0ul, shard.Count(new CountRequest()));
    }

    [Theory]
    [InlineData(PayloadSchemaType.Geo, "loc")]
    [InlineData(PayloadSchemaType.Bool, "active")]
    [InlineData(PayloadSchemaType.Datetime, "stamp")]
    [InlineData(PayloadSchemaType.Uuid, "uid")]
    public void CreateFieldIndex_EachType_AppearsInSchema_Native(PayloadSchemaType schema, string field)
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndex(field, schema));
        Assert.Equal(schema, shard.GetInfo().PayloadSchema[field].DataType);
    }
}
