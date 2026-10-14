using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

public sealed class PointsGapsTests
{
    [Fact]
    public void Point_NullId_ThrowsArgumentNull()
    {
        var point = new Point { Id = null!, Vector = Vector.Single([1f]) };
        Assert.Throws<ArgumentNullException>(point.Validate);
    }

    [Fact]
    public void Point_NullVector_ThrowsArgumentNull()
    {
        var point = new Point { Id = PointId.FromNumber(1), Vector = null! };
        Assert.Throws<ArgumentNullException>(point.Validate);
    }

    [Fact]
    public void Point_InvalidVector_Propagates()
    {
        var point = new Point { Id = PointId.FromNumber(1), Vector = Vector.Single([]) };
        Assert.Throws<EdgeInvalidArgumentException>(point.Validate);
    }

    [Fact]
    public void Point_NullPayload_Passes()
    {
        var point = new Point { Id = PointId.FromNumber(1), Vector = Vector.Single([1f]) };
        point.Validate();
    }

    [Fact]
    public void Point_Payload_PreservedWithoutValidation()
    {
        var payload = new JsonObject { ["nested"] = new JsonObject { ["list"] = new JsonArray(1, 2, 3) } };
        var point = new Point
        {
            Id = PointId.FromNumber(1),
            Vector = Vector.Single([1f]),
            Payload = payload,
        };
        point.Validate();
        Assert.Same(payload, point.Payload);
    }

    [Fact]
    public void PointVectors_NullId_ThrowsArgumentNull()
    {
        var point = new PointVectors { Id = null!, Vector = Vector.Single([1f]) };
        Assert.Throws<ArgumentNullException>(point.Validate);
    }

    [Fact]
    public void PointVectors_NullVector_ThrowsArgumentNull()
    {
        var point = new PointVectors { Id = PointId.FromNumber(1), Vector = null! };
        Assert.Throws<ArgumentNullException>(point.Validate);
    }

    [Fact]
    public void PointVectors_InvalidVector_Propagates()
    {
        var point = new PointVectors { Id = PointId.FromNumber(1), Vector = Vector.Single([]) };
        Assert.Throws<EdgeInvalidArgumentException>(point.Validate);
    }

    [Fact]
    public void OrderValue_Int_Preserved()
    {
        IntOrderValue typed = Assert.IsType<IntOrderValue>(OrderValue.Int(42));
        Assert.Equal(42, typed.Value);
    }

    [Fact]
    public void OrderValue_Float_Preserved()
    {
        FloatOrderValue typed = Assert.IsType<FloatOrderValue>(OrderValue.Float(1.5));
        Assert.Equal(1.5, typed.Value);
    }

    [Fact]
    public void OrderValue_Kinds_RoundTripThroughJson()
    {
        OrderValue intValue = OrderValue.Int(-7);
        string intJson = JsonSerializer.Serialize(intValue, EdgeJsonContext.Deep.OrderValue);
        OrderValue? intBack = JsonSerializer.Deserialize(intJson, EdgeJsonContext.Deep.OrderValue);
        Assert.Equal(-7, Assert.IsType<IntOrderValue>(intBack).Value);

        OrderValue floatValue = OrderValue.Float(2.5);
        string floatJson = JsonSerializer.Serialize(floatValue, EdgeJsonContext.Deep.OrderValue);
        OrderValue? floatBack = JsonSerializer.Deserialize(floatJson, EdgeJsonContext.Deep.OrderValue);
        Assert.Equal(2.5, Assert.IsType<FloatOrderValue>(floatBack).Value);
    }

    [Fact]
    public void ScoredPoint_AllOptionalNull_RoundTrip()
    {
        var original = new ScoredPoint
        {
            Id = PointId.FromNumber(7),
            Version = 3,
            Score = 0.5f,
            Payload = null,
            Vector = null,
            OrderValue = null,
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScoredPoint);
        ScoredPoint? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScoredPoint);
        Assert.NotNull(back);
        Assert.Equal(original, back);
        Assert.Null(back.Payload);
        Assert.Null(back.Vector);
        Assert.Null(back.OrderValue);
        Assert.Equal(3ul, back.Version);
        Assert.Equal(0.5f, back.Score);
    }

    [Fact]
    public void Record_AllOptionalNull_RoundTrip()
    {
        var original = new Record
        {
            Id = PointId.FromNumber(9),
            Payload = null,
            Vector = null,
            OrderValue = null,
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Record);
        Record? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Record);
        Assert.NotNull(back);
        Assert.Equal(original, back);
        Assert.Null(back.Payload);
        Assert.Null(back.Vector);
        Assert.Null(back.OrderValue);
    }

    [Fact]
    public void WithPayload_BoolTrue_Preserved()
    {
        WithPayload selector = WithPayload.Bool(true);
        Assert.True(Assert.IsType<BoolWithPayload>(selector).Enable);
        string json = JsonSerializer.Serialize(selector, EdgeJsonContext.Deep.WithPayload);
        WithPayload? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.WithPayload);
        Assert.Equal(selector, back);
        Assert.True(Assert.IsType<BoolWithPayload>(back).Enable);
    }

    [Fact]
    public void WithPayload_BoolFalse_Preserved()
    {
        WithPayload selector = WithPayload.Bool(false);
        Assert.False(Assert.IsType<BoolWithPayload>(selector).Enable);
        string json = JsonSerializer.Serialize(selector, EdgeJsonContext.Deep.WithPayload);
        WithPayload? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.WithPayload);
        Assert.Equal(selector, back);
        Assert.False(Assert.IsType<BoolWithPayload>(back).Enable);
    }

    [Fact]
    public void WithPayload_Fields_Preserved()
    {
        WithPayload selector = WithPayload.CreateFields(["city", "zip"]);
        Assert.Equal(["city", "zip"], Assert.IsType<FieldsWithPayload>(selector).Fields);
        string json = JsonSerializer.Serialize(selector, EdgeJsonContext.Deep.WithPayload);
        WithPayload? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.WithPayload);
        FieldsWithPayload backTyped = Assert.IsType<FieldsWithPayload>(back);
        Assert.Equal(["city", "zip"], backTyped.Fields);
    }

    [Fact]
    public void WithPayload_Exclude_Preserved()
    {
        WithPayload selector = WithPayload.Exclude(["city"]);
        Assert.Equal(["city"], Assert.IsType<ExcludeWithPayload>(selector).Fields);
        string json = JsonSerializer.Serialize(selector, EdgeJsonContext.Deep.WithPayload);
        WithPayload? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.WithPayload);
        ExcludeWithPayload backTyped = Assert.IsType<ExcludeWithPayload>(back);
        Assert.Equal(["city"], backTyped.Fields);
    }

    [Fact]
    public void WithVector_BoolTrue_Preserved()
    {
        WithVector selector = WithVector.Bool(true);
        Assert.True(Assert.IsType<BoolWithVector>(selector).Enable);
        string json = JsonSerializer.Serialize(selector, EdgeJsonContext.Deep.WithVector);
        WithVector? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.WithVector);
        Assert.Equal(selector, back);
        Assert.True(Assert.IsType<BoolWithVector>(back).Enable);
    }

    [Fact]
    public void WithVector_BoolFalse_Preserved()
    {
        WithVector selector = WithVector.Bool(false);
        Assert.False(Assert.IsType<BoolWithVector>(selector).Enable);
        string json = JsonSerializer.Serialize(selector, EdgeJsonContext.Deep.WithVector);
        WithVector? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.WithVector);
        Assert.Equal(selector, back);
        Assert.False(Assert.IsType<BoolWithVector>(back).Enable);
    }

    [Fact]
    public void WithVector_Names_Preserved()
    {
        WithVector selector = WithVector.CreateNames(["", "image"]);
        Assert.Equal(["", "image"], Assert.IsType<NamesWithVector>(selector).Names);
        string json = JsonSerializer.Serialize(selector, EdgeJsonContext.Deep.WithVector);
        WithVector? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.WithVector);
        NamesWithVector backTyped = Assert.IsType<NamesWithVector>(back);
        Assert.Equal(["", "image"], backTyped.Names);
    }

    [Fact]
    public void Retrieve_WithPayloadFields_Gating_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityShard();
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1)],
            WithPayload = WithPayload.CreateFields(["city"]),
        });
        Record single = Assert.Single(records);
        JsonObject payload = Assert.IsType<JsonObject>(JsonNode.Parse(single.Payload!));
        Assert.Equal("Oslo", payload["city"]!.GetValue<string>());
        Assert.False(payload.ContainsKey("zip"));
    }

    [Fact]
    public void Retrieve_WithPayloadExclude_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityShard();
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1)],
            WithPayload = WithPayload.Exclude(["city"]),
        });
        Record single = Assert.Single(records);
        JsonObject payload = Assert.IsType<JsonObject>(JsonNode.Parse(single.Payload!));
        Assert.False(payload.ContainsKey("city"));
        Assert.Equal("0001", payload["zip"]!.GetValue<string>());
    }

    [Fact]
    public void Retrieve_WithVectorFalse_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityShard();
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1)],
            WithVector = WithVector.Bool(false),
        });
        Record single = Assert.Single(records);
        Assert.Equal(PointId.FromNumber(1), single.Id);
        Assert.Null(single.Vector);
    }

    [Fact]
    public void Retrieve_WithVectorNames_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityShard();
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1)],
            WithVector = WithVector.CreateNames([""]),
        });
        Record single = Assert.Single(records);
        Assert.NotNull(single.Vector);
        JsonArray? values = JsonNode.Parse(single.Vector!)!["Values"] as JsonArray;
        Assert.NotNull(values);
        Assert.Equal([1f, 0f, 0f, 0f], values.Select(node => node!.GetValue<float>()));
    }

    [Fact]
    public void Search_WithPayloadExclude_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityShard();
        IReadOnlyList<ScoredPoint> hits = shard.Search(new SearchRequest
        {
            Query = Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f])),
            Limit = 10,
            WithPayload = WithPayload.Exclude(["city"]),
        });
        Assert.Equal(2, hits.Count);
        foreach (ScoredPoint hit in hits)
        {
            JsonObject payload = Assert.IsType<JsonObject>(JsonNode.Parse(hit.Payload!));
            Assert.False(payload.ContainsKey("city"));
            Assert.NotNull(payload["zip"]);
        }
    }

    [Fact]
    public void Query_WithVectorNames_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityShard();
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
            WithVector = WithVector.CreateNames([""]),
        });
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.NotNull(hit.Vector));
    }

    private static EdgeShard CityShard()
    {
        string dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["city"] = "Oslo", ["zip"] = "0001" },
                },
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["city"] = "Bergen", ["zip"] = "5003" },
                },
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
