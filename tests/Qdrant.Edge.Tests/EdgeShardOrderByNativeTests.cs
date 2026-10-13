using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Order-by scroll and query over the native engine.</summary>
public sealed class EdgeShardOrderByNativeTests
{
    [Fact]
    public void Scroll_OrderByRank_ReturnsIdOrder()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        ScrollResponse page = shard.Scroll(new ScrollRequest
        {
            Limit = 10,
            OrderBy = new OrderBy { Key = "rank", Direction = Direction.Asc },
        });
        Assert.Equal(
            [PointId.FromNumber(2), PointId.FromNumber(3), PointId.FromNumber(1)],
            page.Records.Select(record => record.Id).ToList());
        Assert.Equal(
            [OrderValue.Int(1), OrderValue.Int(2), OrderValue.Int(3)],
            page.Records.Select(record => record.OrderValue).ToList());
    }

    [Fact]
    public void Scroll_OrderBy_ResumesViaStartFrom()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        ScrollResponse page = shard.Scroll(new ScrollRequest
        {
            Limit = 10,
            OrderBy = new OrderBy
            {
                Key = "rank",
                Direction = Direction.Asc,
                StartFrom = new IntegerStartFrom(3),
            },
        });
        Record only = Assert.Single(page.Records);
        Assert.Equal(PointId.FromNumber(1), only.Id);
    }

    [Fact]
    public void Query_OrderBy_PopulatesOrderValue()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateOrderBy(new OrderBy { Key = "rank", Direction = Direction.Asc }),
        });
        Assert.Equal(
            [PointId.FromNumber(2), PointId.FromNumber(3), PointId.FromNumber(1)],
            hits.Select(hit => hit.Id).ToList());
    }

    [Fact]
    public void Scroll_OrderByFloat_ReturnsFloatOrderValues()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        ScrollResponse page = shard.Scroll(new ScrollRequest
        {
            Limit = 10,
            OrderBy = new OrderBy { Key = "score", Direction = Direction.Asc },
        });
        Assert.Equal(
            [OrderValue.Float(1.5), OrderValue.Float(2.5), OrderValue.Float(3.5)],
            page.Records.Select(record => record.OrderValue).ToList());
    }

    private static EdgeShard Seed(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex("rank", PayloadSchemaType.Integer));
            shard.Update(UpdateOperation.CreateFieldIndex("score", PayloadSchemaType.Float));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["rank"] = 3, ["score"] = 3.5 } },
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["rank"] = 1, ["score"] = 1.5 } },
                TestHelpers.FourDimPoint(3, 0f, 0f, 1f, 0f) with { Payload = new JsonObject { ["rank"] = 2, ["score"] = 2.5 } },
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }
}
