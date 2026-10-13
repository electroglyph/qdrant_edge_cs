using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>End-to-end pins over the native engine; no-op when it is absent.</summary>
public sealed class EdgeShardNativeTests
{
    [Fact]
    public void Query_SeededShard_ReturnsBothPoints()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 1f, 1f, 1f]))),
        });
        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public void Count_SeededShard_ReturnsTwo()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        Assert.Equal(2ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Scroll_SeededShard_ReturnsRecordsWithPayload()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        ScrollResponse page = shard.Scroll(new ScrollRequest { Limit = 10 });
        Assert.Equal(2, page.Records.Count);
        Record first = page.Records.Single(record => record.Id.Equals(PointId.FromNumber(1)));
        Assert.Equal("red", PayloadCity(first));
    }

    [Fact]
    public void Facet_SeededShard_GroupsByCity()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        FacetResponse response = shard.Facet(new FacetRequest { Key = "city" });
        Assert.Equal(["blue", "red"], response.Hits.OrderBy(hit => hit.Value).Select(hit => hit.Value));
        Assert.All(response.Hits, hit => Assert.Equal(1ul, hit.Count));
    }

    private static EdgeShard Seed(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex("city", PayloadSchemaType.Keyword));
            shard.Update(UpdateOperation.UpsertPoints([CityPoint(1, "red"), CityPoint(2, "blue")]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static Point CityPoint(ulong id, string city) =>
        TestHelpers.FourDimPoint(id, 6f, 9f, 4f, 2f) with
        {
            Payload = new JsonObject { ["city"] = city },
        };

    private static string? PayloadCity(Record record)
    {
        if (record.Payload is null)
        {
            return null;
        }

        return (string?)JsonNode.Parse(record.Payload)?["city"]?.GetValue<string>();
    }
}
