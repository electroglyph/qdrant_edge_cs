using System.Text.Json;

namespace Qdrant.Edge.Tests;

public sealed class PointIdGapsTests
{
    [Fact]
    public void FromUuid_Value_RoundTrips()
    {
        Guid value = Guid.NewGuid();
        PointId id = PointId.FromUuid(value);
        Assert.Equal(new UuidPointId(value), id);
        Guid extracted = id switch
        {
            UuidPointId(var v) => v,
            _ => throw new Xunit.Sdk.XunitException("wrong variant"),
        };
        Assert.Equal(value, extracted);
    }

    [Fact]
    public void FromNumber_Zero_Passes()
    {
        PointId id = PointId.FromNumber(0UL);
        Assert.Equal(new NumPointId(0UL), id);
    }

    [Fact]
    public void FromNumber_MaxValue_Passes()
    {
        PointId id = PointId.FromNumber(ulong.MaxValue);
        Assert.Equal(new NumPointId(ulong.MaxValue), id);
    }

    [Fact]
    public void FromString_Null_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => PointId.FromString(null!));
    }

    [Fact]
    public void FromString_Whitespace_ThrowsArgument()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => PointId.FromString("  "));
        Assert.Equal(typeof(ArgumentException), ex.GetType());
    }

    [Fact]
    public void FromString_InvalidUuid_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => PointId.FromString("not-a-uuid"));
    }

    [Fact]
    public void FromString_UppercaseUuid_Passes()
    {
        Guid value = Guid.NewGuid();
        string upper = value.ToString("D").ToUpperInvariant();
        PointId id = PointId.FromString(upper);
        Assert.Equal(new UuidPointId(value), id);
    }

    [Fact]
    public void PointId_NumAndUuid_RoundTripThroughJson()
    {
        PointId num = PointId.FromNumber(42UL);
        string numJson = JsonSerializer.Serialize(num, EdgeJsonContext.Deep.PointId);
        PointId? numBack = JsonSerializer.Deserialize(numJson, EdgeJsonContext.Deep.PointId);
        Assert.Equal(num, numBack);

        Guid value = Guid.NewGuid();
        PointId uuid = PointId.FromUuid(value);
        string uuidJson = JsonSerializer.Serialize(uuid, EdgeJsonContext.Deep.PointId);
        PointId? uuidBack = JsonSerializer.Deserialize(uuidJson, EdgeJsonContext.Deep.PointId);
        Assert.Equal(uuid, uuidBack);
    }
}
