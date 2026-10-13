namespace Qdrant.Edge.Tests;

/// <summary>Empty dense vectors are rejected at the boundary with
/// invalid-argument, matching the native parser.</summary>
public sealed class AuditVectorTests
{
    [Fact]
    public void UpsertPoints_EmptyDenseVector_ThrowsInvalidArgument()
    {
        Point point = new()
        {
            Id = PointId.FromNumber(1),
            Vector = Vector.Single([]),
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => UpdateOperation.UpsertPoints([point]));
    }
}
