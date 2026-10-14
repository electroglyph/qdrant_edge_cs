# Qdrant.Edge for C#

Full guides: [`docs/README.md`](docs/README.md) (start with [`docs/getting-started.md`](docs/getting-started.md)).

Embedded single-shard vector search for .NET: idiomatic C# over the Qdrant edge engine, which stays in Rust behind a small JSON C ABI. No server, no gRPC — a shard is a directory on disk plus a native library.

- Package `Qdrant.Edge`, version `0.9.0`, targets `net10.0` (C# 14, nullable enabled).
- Read-write `EdgeShard`: create/load, point/vector/payload updates, ANN query, scroll, facet, groups, snapshots.
- Every request is validated client-side before touching native; failures throw typed `EdgeException`s.

## Prerequisites

- .NET 10 SDK.
- Rust toolchain (only to build the native engine once).

Get the code with submodules (the engine lives in pinned `third_party/qdrant`; the pin is load-bearing):

```sh
git clone --recurse-submodules https://github.com/electroglyph/qdrant_edge_cs.git
cd qdrant_edge_cs
```

## Build the native library

```sh
cargo build -p qdrant-edge-cabi --release --manifest-path native/cabi/Cargo.toml
```

Copy the output per RID next to the consuming app under `runtimes/<rid>/native/` (e.g. `libqdrant_edge.so` for `linux-x64`, `qdrant_edge.dll` for `win-x64`). The test and Demo bins each need their own copy. The binaries are git-ignored CI artifacts. Without one, `EdgeShard.Create`/`Load` throw `EdgeOperationException` — check `NativeLoader.IsAvailable` to probe.

```csharp
if (!NativeLoader.IsAvailable)
{
    Console.WriteLine("Build the cabi crate into runtimes/<rid>/native/ first.");
    return;
}
```

Reference the project and import the namespace:

```sh
dotnet add reference src/Qdrant.Edge/Qdrant.Edge.csproj
```

```csharp
using Qdrant.Edge;
```

## Quickstart

```csharp
using Qdrant.Edge;

EdgeConfig config = new()
{
    VectorData = new Dictionary<string, VectorDataConfig>
    {
        // "" is the default unnamed vector field.
        [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
    },
};

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

foreach (ScoredPoint hit in hits)
{
    Console.WriteLine($"{hit.Id} score={hit.Score}");
}

shard.Flush();

// Later: reopen the same directory.
using EdgeShard reopened = EdgeShard.Load(dir);
```

`Dispose` flushes then releases (idempotent). After `Unload`/`Dispose`, every operation throws `ShardClosedException`.

## End to end: semantic search with an ONNX embedding model

A realistic retrieval pipeline: embed documents with a local ONNX model
(`Microsoft.ML.OnnxRuntime` for inference, `Tokenizers.HuggingFace` — .NET
bindings for the real Rust `tokenizers` crate — for tokenization), store the
vectors in an `EdgeShard`, and query by embedding similarity.
Tested against `jina-embeddings-v5-text-nano-retrieval-onnx/model_int8.onnx`
(int64 `input_ids` + `attention_mask` in, 768-dim L2-normalized `sentence_embedding` out).

```sh
dotnet add package Microsoft.ML.OnnxRuntime --version 1.31.0
dotnet add package Tokenizers.HuggingFace --version 3.23.1
```

```csharp
using Microsoft.ML.OnnxRuntime;
using Qdrant.Edge;
using Tokenizers.HuggingFace.Tokenizer;

// Loads the model's own tokenizer.json directly — full HF pipeline
// (normalizer, pre-tokenizer, BPE, template) with exact Python parity.
// Build once, reuse. Dispose when done (finalizer backs it up if you forget).
using Tokenizer tk = Tokenizer.FromFile("tokenizer.json");
using var session = new InferenceSession("model_int8.onnx");

float[] Embed(string text)
{
    var enc = tk.Encode(text, true, includeTypeIds: true, includeAttentionMask: true).First();
    long[] inputIds = enc.Ids.Select(t => (long)t).Take(512).ToArray(); // demo cap; model takes up to 8192
    long[] mask = enc.AttentionMask.Select(t => (long)t).Take(512).ToArray();
    long[] shape = [1, inputIds.Length];
    using var idsValue = OrtValue.CreateTensorValueFromMemory(inputIds, shape);
    using var maskValue = OrtValue.CreateTensorValueFromMemory(mask, shape);
    var inputs = new Dictionary<string, OrtValue>
    {
        { "input_ids", idsValue },
        { "attention_mask", maskValue },
    };
    using var runOptions = new RunOptions();
    using var output = session.Run(runOptions, inputs, session.OutputNames);
    return output[0].GetTensorDataAsSpan<float>().ToArray();
}

// Prefix queries/documents per the model's prompts config
// (config_sentence_transformers.json: "Query: " / "Document: ").
(string Text, string Category)[] docs =
[
    ("Document: Quantization reduces model size for fast CPU inference.", "ml"),
    ("Document: HNSW graphs index dense vectors for nearest-neighbor search.", "ml"),
    ("Document: Simmer the sauce for twenty minutes before serving.", "cooking"),
];

EdgeConfig config = new()
{
    VectorData = new Dictionary<string, VectorDataConfig>
    {
        [""] = new VectorDataConfig { Size = 768, Distance = Distance.Cosine },
    },
};
using EdgeShard shard = EdgeShard.Create(dir, config);

for (int i = 0; i < docs.Length; i++)
{
    shard.Update(UpdateOperation.UpsertPoints(
    [
        new Point
        {
            Id = PointId.FromNumber((ulong)(i + 1)),
            Vector = Vector.Single(Embed(docs[i].Text)),
            Payload = new System.Text.Json.Nodes.JsonObject
            {
                ["text"] = docs[i].Text,
                ["category"] = docs[i].Category,
            },
        },
    ]));
}

// Retrieve: embedding similarity is a dot product (vectors are normalized).
float[] queryVec = Embed("Query: What is quantization?");
IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
{
    Limit = 2,
    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense(queryVec))),
    WithPayload = WithPayload.CreateFields(["text", "category"]),
});

// Same search narrowed to ML docs via a payload filter.
IReadOnlyList<ScoredPoint> filtered = shard.Query(new QueryRequest
{
    Limit = 2,
    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense(queryVec))),
    Filter = new Filter
    {
        Must =
        [
            Condition.Field(new FieldCondition
            {
                Key = "category",
                Match = Match.CreateValue(ValueVariants.String("ml")),
            }),
        ],
    },
    WithPayload = WithPayload.CreateFields(["text"]),
});
```

Real output from the run above:

```text
1 score=0.5246 payload={"text":"Document: Quantization reduces model size for fast CPU inference.","category":"ml"}
2 score=0.1141 payload={"text":"Document: HNSW graphs index dense vectors for nearest-neighbor search.","category":"ml"}
```

Notes: keep one shared `InferenceSession`/`Tokenizer`/`EdgeShard` (session
construction is expensive); everything disposable gets a `using`; `Flush()`
before process exit if you skip `Dispose`.

## Writes

`shard.Update(...)` takes one of 20 `UpdateOperation`s:

```csharp
// Upsert (optional condition + InsertOnly/UpdateOnly variants).
shard.Update(UpdateOperation.UpsertPoints(points));

// Targeted deletes.
shard.Update(UpdateOperation.DeletePoints([PointId.FromNumber(1)]));
shard.Update(UpdateOperation.DeletePointsByFilter(filter));

// Payload: set (merge), overwrite (replace), delete keys, or clear.
shard.Update(UpdateOperation.SetPayload([PointId.FromNumber(1)], """{"color":"red"}"""));
shard.Update(UpdateOperation.DeletePayloadByFilter(filter, ["color"]));

// Schema: payload indexes and vector fields.
shard.Update(UpdateOperation.CreateFieldIndex("color", PayloadSchemaType.Keyword));
shard.Update(UpdateOperation.CreateDenseVector("image", 128, Distance.Cosine));
shard.Update(UpdateOperation.DeleteVectorName("legacy"));
```

Point ids are `PointId.FromNumber(ulong)`, `PointId.FromUuid(Guid)`, or `PointId.FromString(uuid-string)`. Vectors are `Vector.Single(floats)`, `Vector.MultiDense(nested)`, or `Vector.Named(map)` for multi-field shards; query-time vectors are `NamedVector.Dense/Sparse/MultiDense`. Payload on write is a `JsonObject`; on read, `ScoredPoint.Payload`/`Vector` come back as JSON strings — request them explicitly (see below).

## Reads

Prefer `Query`; `Search` is kept for the deprecated vector-only path.

```csharp
// Filtered ANN with a payload projection and score cutoff.
IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
{
    Limit = 5,
    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense(queryVec))),
    Filter = new Filter
    {
        Must = [Condition.Field(new FieldCondition { Key = "color", Match = Match.CreateValue(ValueVariants.String("red")) })],
    },
    WithPayload = WithPayload.CreateFields(["color", "price"]),
    ScoreThreshold = 0.2f,
});

// Batch, groups, scroll (follow NextOffset until null), point lookup, count.
IReadOnlyList<IReadOnlyList<ScoredPoint>> batched =
    shard.QueryBatch(new QueryBatchRequest { Queries = [q1, q2] });

IReadOnlyList<Group> groups = shard.QueryGroups(new GroupRequest
{
    Query = new QueryRequest { Limit = 20 },
    GroupBy = "color",
    Groups = 5,
    GroupSize = 3,
});

ScrollResponse page = shard.Scroll(new ScrollRequest { Limit = 100 });
while (page.NextOffset is not null)
{
    page = shard.Scroll(new ScrollRequest { Limit = 100, Offset = page.NextOffset });
}

IReadOnlyList<Record> found =
    shard.Retrieve(new RetrieveRequest { PointIds = [PointId.FromNumber(1)] });

ulong n = shard.Count(new CountRequest { Exact = true });
ShardInfo info = shard.GetInfo();
```

Other query modes use the same `QueryRequest` with a different `ScoringQuery`:

```csharp
ScoringQuery.CreateVector(Query.Recommend([NamedVector.Dense(a)], [NamedVector.Dense(b)]));
ScoringQuery.CreateFusion(Fusion.Rrf(60));
ScoringQuery.Mmr(NamedVector.Dense(a), 0.5f, 100);
ScoringQuery.CreateSample(Sample.Random);
ScoringQuery.Formula(Expression.Sum([Expression.Variable("$score"), Expression.Constant(1f)]));
```

`WithPayload` is `Bool / CreateFields / Exclude`; `WithVector` is `Bool / CreateNames`. Both default off for queries (retrieve defaults payload on).

## Filters

```csharp
Filter filter = new()
{
    Must = [Condition.Field(new FieldCondition { Key = "price", Range = new RangeFloat { Gte = 10, Lt = 100 } })],
    MustNot = [Condition.IsNull("deleted")],
    Should = [Condition.HasId([PointId.FromNumber(7)])],
    MinShould = new MinShould { Conditions = [Condition.HasVector("image")], MinCount = 1 },
};
```

`FieldCondition` holds exactly one predicate (`Match`, `Range`, `DatetimeRange`, `GeoBoundingBox`, `GeoRadius`, `GeoPolygon`, `ValuesCount`); to AND several predicates on one key, add one condition per predicate. Nesting is capped at 64 deep.

## Facet (needs an indexed key)

```csharp
shard.Update(UpdateOperation.CreateFieldIndex("color", PayloadSchemaType.Keyword));
FacetResponse facets = shard.Facet(new FacetRequest { Key = "color", Limit = 10 });
foreach (FacetHit hit in facets.Hits)
{
    Console.WriteLine($"{hit.Value}: {hit.Count}");
}
```

## Sparse vectors

```csharp
EdgeConfig config = new()
{
    SparseVectorData = new Dictionary<string, SparseVectorDataConfig>
    {
        ["splade"] = new SparseVectorDataConfig { Modifier = Modifier.Idf },
    },
};

// ...upsert with a named sparse vector...
Vector.Named(new Dictionary<string, NamedVector>
{
    ["splade"] = NamedVector.Sparse(new SparseVector
    {
        Indices = [3u, 17u, 420u],
        Values = [0.9f, 0.4f, 0.1f],
    }),
});
```

## Snapshots

```csharp
string manifestJson = shard.SnapshotManifest();          // opaque manifest JSON
shard.UpdateFromSnapshot("/backups/snap.tar");           // full replace or partial merge
EdgeShard.UnpackSnapshot("/backups/snap.tar", dir);      // static: unpack without a shard
```

## Tuning

```csharp
shard.SetHnswConfig(new HnswIndexConfig { M = 32, EfConstruct = 200 });
shard.SetVectorHnswConfig("image", new HnswIndexConfig { M = 16 });
shard.SetOptimizersConfig(new OptimizersConfig { DefaultSegmentNumber = 2 });
shard.Optimize();   // true when work was done
shard.Flush();
```

`EdgeConfig.GetConfig()` echoes the configured fields. Async `QueryAsync`/`SearchAsync` offload to the thread pool; tokens are honored before dispatch but cannot cancel an in-flight native call.

## Errors and limits

All failures derive from `EdgeException` (`Reason` carries the detail):

| Exception | Meaning |
|---|---|
| `EdgeInvalidArgumentException` | Bad request — client validation or native rejection |
| `EdgeOperationException` | Engine failure (I/O, missing native lib, dimension mismatch) |
| `ShardClosedException` | Operation after `Unload`/`Dispose` |

Enforced caps: result counts ≤ 1,048,576; filter/prefetch nesting ≤ 64; formula ≤ 10,000 nodes; dense vectors 1–65,536 dims, finite values; HNSW `m`/`payload_m` ≤ 2,048, `ef_construct` 4–100,000.

`SearchMatrix` is `[Experimental("QDRANT_EDGE_MATRIX")]` with O(n²) cost — usable, but opt in knowingly.

## Tests and layout

```sh
dotnet build *.sln -c Release
dotnet test tests/Qdrant.Edge.Tests -c Release
```

Native-guarded tests execute for real when the library is staged (see above); otherwise they assert the missing-library behavior.

- `src/Qdrant.Edge/` — DTOs, validation, `EdgeShard`, interop (`README.md` there has architecture notes).
- `native/cabi/` — the Rust C ABI bridge.
- `tests/Qdrant.Edge.Tests/` — xunit suite exercising real paths (no mirror assertions).
- `examples/Demo/` — the quickstart above as a runnable app.
- `interface.md`, `test.md`, `audit.md`, `csharp.md` — interface plan, test plan, audit journal, C# reference.

## Not in v1

Read-only follower (`live_reload`), batch writer (`apply_batch`/`preview_batch`), and the `Bm25` embed helper are engine-only surfaces with no FFI binding — see `src/Qdrant.Edge/README.md` §"v2 gaps".
