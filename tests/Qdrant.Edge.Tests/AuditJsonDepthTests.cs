using System.Text.Json;

namespace Qdrant.Edge.Tests;

/// <summary>The high-depth JSON context writes a valid 65-deep prefetch tree
/// that the default 64-deep cap would reject.</summary>
public sealed class AuditJsonDepthTests
{
    [Fact]
    public void QueryRequestSerialize_SixtyFiveDeepPrefetch_Succeeds()
    {
        Prefetch inner = new() { Limit = 10 };
        for (int i = 1; i < 65; i++)
        {
            inner = new Prefetch { Limit = 10, Prefetches = [inner] };
        }

        QueryRequest request = new() { Limit = 10, Prefetches = [inner] };
        request.Validate();
        string json = JsonSerializer.Serialize(request, EdgeJsonContext.Deep.QueryRequest);
        Assert.NotEmpty(json);
    }
}
