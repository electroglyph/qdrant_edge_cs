using System.Text.Json;

namespace Qdrant.Edge.Tests;

public sealed class UpdateOperationTests
{
    [Fact]
    public void CreateDenseVector_ZeroSize_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => UpdateOperation.CreateDenseVector("image", 0, Distance.Cosine));
    }

    [Fact]
    public void CreateFieldIndexWithParams_DisabledIntegerIndex_ThrowsInvalidArgument()
    {
        PayloadIndexParams indexParams = PayloadIndexParams.Integer(new IntegerIndexParams { Lookup = false, Range = false });
        Assert.Throws<EdgeInvalidArgumentException>(() => UpdateOperation.CreateFieldIndexWithParams("count", indexParams));
    }

    [Fact]
    public void CreateSparseVector_AnyName_ReturnsSparseKind()
    {
        UpdateOperation operation = UpdateOperation.CreateSparseVector("text");
        Assert.Equal("create_sparse_vector", operation.Kind);
    }

    [Fact]
    public void DeleteVectorName_AnyName_ReturnsDeleteKind()
    {
        UpdateOperation operation = UpdateOperation.DeleteVectorName("image");
        Assert.Equal("delete_vector_name", operation.Kind);
    }

    [Fact]
    public void UpsertPoints_NaNVector_ThrowsInvalidArgument()
    {
        Point point = new()
        {
            Id = PointId.FromNumber(1),
            Vector = Vector.Single([float.NaN, 1f]),
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => UpdateOperation.UpsertPoints([point]));
    }

    [Fact]
    public void UpsertPoints_ValidPoints_PreservesPayload()
    {
        Point point = new()
        {
            Id = PointId.FromNumber(7),
            Vector = Vector.Single([1f, 2f, 3f, 4f]),
        };
        UpdateOperation operation = UpdateOperation.UpsertPoints([point]);
        UpsertPointsOperation upsert = Assert.IsType<UpsertPointsOperation>(operation);
        Assert.Single(upsert.Points);
        Assert.Equal(point, upsert.Points[0]);
    }

    [Fact]
    public void UpsertPoints_ValidPoints_RoundTripsThroughJson()
    {
        Point point = new()
        {
            Id = PointId.FromNumber(7),
            Vector = Vector.Single([1f, 2f, 3f, 4f]),
        };
        UpdateOperation operation = UpdateOperation.UpsertPoints([point]);
        string json = JsonSerializer.Serialize(operation);
        UpdateOperation? restored = JsonSerializer.Deserialize<UpdateOperation>(json);
        UpsertPointsOperation upsert = Assert.IsType<UpsertPointsOperation>(restored);
        Assert.Single(upsert.Points);
        Assert.Equal(point.Id, upsert.Points[0].Id);
        SingleVector expectedVector = Assert.IsType<SingleVector>(point.Vector);
        SingleVector actualVector = Assert.IsType<SingleVector>(upsert.Points[0].Vector);
        Assert.Equal(expectedVector.Values, actualVector.Values);
    }

    [Fact]
    public void DeletePointsByFilter_ValidFilter_PreservesFilter()
    {
        Filter filter = new() { Must = [Condition.HasId([PointId.FromNumber(1)])] };
        UpdateOperation operation = UpdateOperation.DeletePointsByFilter(filter);
        DeletePointsByFilterOperation delete = Assert.IsType<DeletePointsByFilterOperation>(operation);
        Assert.Equal(filter, delete.Filter);
    }

    [Fact]
    public void CreateDenseVector_ValidSize_PreservesDimensions()
    {
        UpdateOperation operation = UpdateOperation.CreateDenseVector("image", 128, Distance.Cosine);
        CreateDenseVectorOperation create = Assert.IsType<CreateDenseVectorOperation>(operation);
        Assert.Equal("image", create.VectorName);
        Assert.Equal(128ul, create.Size);
        Assert.Equal(Distance.Cosine, create.Distance);
    }

    [Fact]
    public void UpsertPoints_ConditionAndMode_Preserved()
    {
        Point point = TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f);
        Filter condition = new() { Must = [Condition.HasId([PointId.FromNumber(1)])] };
        UpdateOperation operation = UpdateOperation.UpsertPoints([point], condition, UpdateMode.InsertOnly);
        UpsertPointsOperation upsert = Assert.IsType<UpsertPointsOperation>(operation);
        Assert.Same(condition, upsert.Condition);
        Assert.Equal(UpdateMode.InsertOnly, upsert.UpdateMode);
    }
}
