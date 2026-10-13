namespace Qdrant.Edge.Tests;

/// <summary>Filter depth starts at zero like the native parser, so 64 nested
/// filters pass here exactly when the engine accepts them.</summary>
public sealed class AuditFilterTests
{
    private static Filter Nest(int count)
    {
        Filter inner = new();
        for (int i = 0; i < count; i++)
        {
            inner = new Filter { Must = [Condition.Nested("k", inner)] };
        }

        return inner;
    }

    [Fact]
    public void FilterValidate_SixtyFourNested_PassesMatchingNative()
    {
        Nest(64).Validate();
    }

    [Fact]
    public void FilterValidate_SixtyFiveNested_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(Nest(65).Validate);
    }
}
