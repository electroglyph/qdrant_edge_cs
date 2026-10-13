namespace Qdrant.Edge.Tests;

/// <summary>Prefetch depth starts at zero like the native parser, so a
/// 65-deep chain passes here exactly when the engine accepts it.</summary>
public sealed class AuditPrefetchTests
{
    private static Prefetch Chain(int count)
    {
        Prefetch inner = new() { Limit = 10 };
        for (int i = 1; i < count; i++)
        {
            inner = new Prefetch { Limit = 10, Prefetches = [inner] };
        }

        return inner;
    }

    [Fact]
    public void PrefetchValidate_SixtyFiveChain_PassesMatchingNative()
    {
        QueryRequest request = new() { Limit = 10, Prefetches = [Chain(65)] };
        request.Validate();
    }

    [Fact]
    public void PrefetchValidate_SixtySixChain_ThrowsInvalidArgument()
    {
        QueryRequest request = new() { Limit = 10, Prefetches = [Chain(66)] };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }
}
