namespace Qdrant.Edge.Tests;

public sealed class VectorValidationTests
{
    [Fact]
    public void Validate_EmptyDenseVector_ThrowsInvalidArgument()
    {
        Vector vector = Vector.Single([]);
        Assert.Throws<EdgeInvalidArgumentException>(vector.Validate);
    }

    [Fact]
    public void Validate_RaggedMultiDense_ThrowsInvalidArgument()
    {
        Vector vector = Vector.MultiDense([[1f, 2f], [1f, 2f, 3f]]);
        Assert.Throws<EdgeInvalidArgumentException>(vector.Validate);
    }

    [Fact]
    public void Validate_EmptyMultiDense_ThrowsInvalidArgument()
    {
        Vector vector = Vector.MultiDense([]);
        Assert.Throws<EdgeInvalidArgumentException>(vector.Validate);
    }

    [Fact]
    public void Validate_NaNValue_ThrowsInvalidArgument()
    {
        Vector vector = Vector.Single([1f, float.NaN]);
        Assert.Throws<EdgeInvalidArgumentException>(vector.Validate);
    }

    [Fact]
    public void Validate_WellFormedDense_Passes()
    {
        TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f).Validate();
    }
}
