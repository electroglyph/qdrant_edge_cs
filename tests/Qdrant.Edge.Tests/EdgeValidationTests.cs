namespace Qdrant.Edge.Tests;

public sealed class EdgeValidationTests
{
    [Fact]
    public void CheckResultCount_ValueAboveCap_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckResultCount("limit", EdgeValidation.MaxResultCount + 1));
    }

    [Fact]
    public void CheckResultCount_ValueAtCap_Passes()
    {
        EdgeValidation.CheckResultCount("limit", EdgeValidation.MaxResultCount);
    }

    [Fact]
    public void CheckNestingDepth_ValueAboveCap_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckNestingDepth("filter", EdgeValidation.MaxQueryNestingDepth + 1));
    }

    [Fact]
    public void CheckExpressionSize_ValueAboveCap_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckExpressionSize(EdgeValidation.MaxFormulaNodes + 1));
    }

    [Fact]
    public void CheckVectorSize_ZeroSize_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckVectorSize("vector", 0));
    }

    [Fact]
    public void CheckVectorSize_Oversized_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckVectorSize("vector", 65_537));
    }

    [Fact]
    public void CheckFinite_NaN_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckFinite("score", float.NaN));
    }

    [Fact]
    public void CheckGeoPoint_LatitudeOutOfRange_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckGeoPoint(10.0, 999.0));
    }
}
