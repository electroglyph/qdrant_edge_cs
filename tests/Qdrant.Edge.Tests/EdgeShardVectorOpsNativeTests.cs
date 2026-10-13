using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Vector-field and upsert-mode pins over the native engine.</summary>
public sealed class EdgeShardVectorOpsNativeTests
{
    [Fact]
    public void ConditionalUpsert_InsertOnly_PreservesExisting()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SingleShard(out _, """{"title":"one"}""");
        Point overwrite = TestHelpers.FourDimPoint(1, 9f, 9f, 9f, 9f) with
        {
            Payload = new JsonObject { ["title"] = "two" },
        };
        shard.Update(UpdateOperation.UpsertPoints([overwrite], null, UpdateMode.InsertOnly));
        JsonObject payload = RetrievePayload(shard, 1);
        Assert.Equal("one", payload["title"]!.GetValue<string>());
    }

    [Fact]
    public void UpdateVectors_ReplacesStoredVector()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SingleShard(out _, """{"title":"one"}""");
        shard.Update(UpdateOperation.UpdateVectors(
        [
            new PointVectors { Id = PointId.FromNumber(1), Vector = Vector.Single([9f, 8f, 7f, 6f]) },
        ]));
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1)],
            WithVector = WithVector.Bool(true),
        });
        Record single = Assert.Single(records);
        JsonArray? values = JsonNode.Parse(single.Vector!)!["Values"] as JsonArray;
        Assert.NotNull(values);
        Assert.Equal([9f, 8f, 7f, 6f], values.Select(node => node!.GetValue<float>()));
    }

    [Fact]
    public void DeleteVectors_RemovesNamedFieldOnly()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = TwoFieldShard(out _);
        shard.Update(UpdateOperation.DeleteVectors([PointId.FromNumber(1)], ["vec2"]));
        // Only the default vector remains, so the engine reads it back as a single vector.
        JsonNode vector = JsonNode.Parse(RetrieveVector(shard, 1))!;
        Assert.Equal("single", vector["kind"]!.GetValue<string>());
        Assert.Equal([1f, 2f, 3f, 4f], ((JsonArray)vector["Values"]!).Select(node => node!.GetValue<float>()));
    }

    [Fact]
    public void DeleteFieldIndex_RemovesSchemaEntry()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        shard.Update(UpdateOperation.CreateFieldIndex("category", PayloadSchemaType.Keyword));
        shard.Update(UpdateOperation.DeleteFieldIndex("category"));
        Assert.False(shard.GetInfo().PayloadSchema.ContainsKey("category"));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void CreateNamedVectorField_AllowsWritesThenDelete()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SingleShard(out _, """{"title":"one"}""");
        shard.Update(UpdateOperation.CreateDenseVector("extra", 4, Distance.Dot));
        Point extended = TestHelpers.FourDimPoint(2, 1f, 2f, 3f, 4f) with
        {
            Vector = Vector.Named(new Dictionary<string, NamedVector>
            {
                [""] = NamedVector.Dense([1f, 2f, 3f, 4f]),
                ["extra"] = NamedVector.Dense([5f, 6f, 7f, 8f]),
            }),
        };
        shard.Update(UpdateOperation.UpsertPoints([extended]));
        Assert.True(RetrieveVectorMap(shard, 2).ContainsKey("extra"));
        shard.Update(UpdateOperation.DeleteVectorName("extra"));
        // Only the default vector remains, so the engine reads it back as a single vector.
        JsonNode vector = JsonNode.Parse(RetrieveVector(shard, 2))!;
        Assert.Equal("single", vector["kind"]!.GetValue<string>());
        Assert.Equal([1f, 2f, 3f, 4f], ((JsonArray)vector["Values"]!).Select(node => node!.GetValue<float>()));
    }

    [Fact]
    public void CreateVectorName_ConflictingRecreate_Throws()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SingleShard(out _, """{"title":"one"}""");
        shard.Update(UpdateOperation.CreateDenseVector("extra", 4, Distance.Dot));
        shard.Update(UpdateOperation.CreateDenseVector("extra", 4, Distance.Dot));
        Assert.Throws<EdgeOperationException>(() =>
            shard.Update(UpdateOperation.CreateDenseVector("extra", 8, Distance.Cosine)));
        shard.Update(UpdateOperation.CreateDenseVector("", 4, Distance.Dot));
    }

    private static EdgeShard SingleShard(out string dir, string payloadJson)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
                {
                    Payload = (JsonObject)JsonNode.Parse(payloadJson)!,
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

    private static EdgeShard TwoFieldShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
                ["vec2"] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
            },
        };
        EdgeShard shard = EdgeShard.Create(dir, config);
        try
        {
            Point point = TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
            {
                Vector = Vector.Named(new Dictionary<string, NamedVector>
                {
                    [""] = NamedVector.Dense([1f, 2f, 3f, 4f]),
                    ["vec2"] = NamedVector.Dense([5f, 6f, 7f, 8f]),
                }),
            };
            shard.Update(UpdateOperation.UpsertPoints([point]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static JsonObject RetrievePayload(EdgeShard shard, ulong id)
    {
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(id)],
        });
        Record single = Assert.Single(records);
        return single.Payload is null ? new JsonObject() : (JsonObject)JsonNode.Parse(single.Payload)!;
    }

    private static string RetrieveVector(EdgeShard shard, ulong id)
    {
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(id)],
            WithVector = WithVector.Bool(true),
        });
        Record single = Assert.Single(records);
        Assert.NotNull(single.Vector);
        return single.Vector;
    }

    private static JsonObject RetrieveVectorMap(EdgeShard shard, ulong id) =>
        (JsonObject)JsonNode.Parse(RetrieveVector(shard, id))!["Map"]!;
}
