namespace Qdrant.Edge.Tests;

public sealed class PointIdTests
{
    [Fact]
    public void FromString_ValidUuid_ReturnsUuidId()
    {
        Guid value = Guid.NewGuid();
        PointId id = PointId.FromString(value.ToString());
        Assert.Equal(new UuidPointId(value), id);
    }

    [Fact]
    public void FromString_InvalidText_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => PointId.FromString("not-a-uuid"));
    }

    [Fact]
    public void FromNumber_Value_RoundTripsThroughPatternMatch()
    {
        PointId id = PointId.FromNumber(7);
        ulong value = id switch
        {
            NumPointId(var v) => v,
            UuidPointId => throw new Xunit.Sdk.XunitException("wrong variant"),
            _ => throw new Xunit.Sdk.XunitException("unknown variant"),
        };
        Assert.Equal(7UL, value);
    }
}
