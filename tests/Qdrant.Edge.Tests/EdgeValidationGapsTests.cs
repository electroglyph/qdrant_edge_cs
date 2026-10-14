namespace Qdrant.Edge.Tests;

public sealed class EdgeValidationGapsTests
{
    [Fact]
    public void CheckResultCount_NullOrEmptyField_ThrowsArgument()
    {
        Assert.Throws<ArgumentNullException>(() => EdgeValidation.CheckResultCount(null!, 1));
        ArgumentException empty = Assert.Throws<ArgumentException>(() => EdgeValidation.CheckResultCount(string.Empty, 1));
        Assert.Equal("field", empty.ParamName);
    }

    [Fact]
    public void CheckResultCount_Zero_Passes()
    {
        EdgeValidation.CheckResultCount("limit", 0);
    }

    [Fact]
    public void CheckNestingDepth_NullOrEmptyKind_ThrowsArgument()
    {
        Assert.Throws<ArgumentNullException>(() => EdgeValidation.CheckNestingDepth(null!, 1));
        ArgumentException empty = Assert.Throws<ArgumentException>(() => EdgeValidation.CheckNestingDepth(string.Empty, 1));
        Assert.Equal("kind", empty.ParamName);
    }

    [Fact]
    public void CheckNestingDepth_ZeroAndAtCap_Pass()
    {
        EdgeValidation.CheckNestingDepth("filter", 0);
        EdgeValidation.CheckNestingDepth("filter", EdgeValidation.MaxQueryNestingDepth);
    }

    [Fact]
    public void CheckExpressionSize_AtCapAndZero_Pass()
    {
        EdgeValidation.CheckExpressionSize(EdgeValidation.MaxFormulaNodes);
        EdgeValidation.CheckExpressionSize(0);
    }

    [Fact]
    public void CheckVectorSize_NullField_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => EdgeValidation.CheckVectorSize(null!, 4));
    }

    [Fact]
    public void CheckVectorSize_Boundaries_Pass()
    {
        EdgeValidation.CheckVectorSize("vector", 1);
        EdgeValidation.CheckVectorSize("vector", 65_536);
    }

    [Fact]
    public void CheckFiniteDouble_NaN_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckFinite("score", double.NaN));
    }

    [Fact]
    public void CheckFiniteDouble_PosInf_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckFinite("score", double.PositiveInfinity));
    }

    [Fact]
    public void CheckFiniteDouble_NegInf_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckFinite("score", double.NegativeInfinity));
    }

    [Fact]
    public void CheckFiniteFloat_PosInf_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckFinite("score", float.PositiveInfinity));
    }

    [Fact]
    public void CheckFiniteFloat_NegInf_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckFinite("score", float.NegativeInfinity));
    }

    [Fact]
    public void CheckFinite_ZeroAndMaxValue_Pass()
    {
        EdgeValidation.CheckFinite("score", 0.0f);
        EdgeValidation.CheckFinite("score", float.MaxValue);
        EdgeValidation.CheckFinite("score", double.MaxValue);
    }

    [Fact]
    public void CheckFinite_NullOrEmptyField_ThrowsArgument()
    {
        Assert.Throws<ArgumentNullException>(() => EdgeValidation.CheckFinite(null!, 1.0));
        Assert.Throws<ArgumentException>(() => EdgeValidation.CheckFinite(string.Empty, 1.0));
        Assert.Throws<ArgumentNullException>(() => EdgeValidation.CheckFinite(null!, 1.0f));
        Assert.Throws<ArgumentException>(() => EdgeValidation.CheckFinite(string.Empty, 1.0f));
    }

    [Fact]
    public void CheckPayloadKey_Null_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => EdgeValidation.CheckPayloadKey(null!));
    }

    [Fact]
    public void CheckPayloadKey_Empty_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => EdgeValidation.CheckPayloadKey(string.Empty));
    }

    [Fact]
    public void CheckPayloadKey_Whitespace_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => EdgeValidation.CheckPayloadKey("  "));
    }

    [Fact]
    public void CheckGeoPoint_LonOutOfRange_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckGeoPoint(181.0, 0.0));
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckGeoPoint(-181.0, 0.0));
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckGeoPoint(0.0, -91.0));
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckGeoPoint(0.0, 90.0001));
    }

    [Fact]
    public void CheckGeoPoint_Boundaries_Pass()
    {
        EdgeValidation.CheckGeoPoint(-180.0, -90.0);
        EdgeValidation.CheckGeoPoint(180.0, 90.0);
        EdgeValidation.CheckGeoPoint(0.0, 0.0);
    }

    [Fact]
    public void CheckGeoPoint_NaN_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckGeoPoint(double.NaN, 0.0));
    }

    [Fact]
    public void CheckGeoPoint_PosInf_Throws()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckGeoPoint(0.0, double.PositiveInfinity));
    }

    [Fact]
    public void CheckGeoPoint_NaNLon_ThrowsWithLonFieldName()
    {
        EdgeInvalidArgumentException ex = Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckGeoPoint(double.NaN, 0.0));
        Assert.Contains("lon", ex.Message);
    }

    [Fact]
    public void CheckGeoPoint_NaNLat_ThrowsWithLatFieldName()
    {
        EdgeInvalidArgumentException ex = Assert.Throws<EdgeInvalidArgumentException>(() => EdgeValidation.CheckGeoPoint(0.0, double.NaN));
        Assert.Contains("lat", ex.Message);
    }
}
