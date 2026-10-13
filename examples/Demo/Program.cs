using Qdrant.Edge;

EdgeConfig config = new()
{
    VectorData = new Dictionary<string, VectorDataConfig>
    {
        [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
    },
};

string dir = Path.Combine(Path.GetTempPath(), "qdrant-edge-demo-" + Guid.NewGuid().ToString("N"));
Console.WriteLine($"Demo dir: {dir}");
Console.WriteLine($"Native available: {NativeLoader.IsAvailable}");

if (!NativeLoader.IsAvailable)
{
    Console.WriteLine("Native library is missing; build the cabi crate into runtimes/<rid>/native/ first.");
    return 0;
}

using EdgeShard shard = EdgeShard.Create(dir, config);
shard.Update(UpdateOperation.UpsertPoints(
[
    new Point
    {
        Id = PointId.FromNumber(1),
        Vector = Vector.Single([6f, 9f, 4f, 2f]),
        Payload = new System.Text.Json.Nodes.JsonObject { ["hello"] = "world" },
    },
]));
IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
{
    Limit = 10,
    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 1f, 1f, 1f]))),
});
Console.WriteLine($"Hits: {hits.Count}");
shard.Flush();
return 0;
