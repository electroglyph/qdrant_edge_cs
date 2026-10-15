# Getting started

Purpose: install the package, build and stage the native engine, and run a
first create-upsert-query-flush-reopen round-trip. For the full lifecycle
surface (`Load` variants, `Unload` vs `Dispose`, config setters) see
[`lifecycle.md`](lifecycle.md).

Prerequisites: .NET 10 SDK, Rust toolchain (native build only),
`git clone --recurse-submodules` (the engine pin in
`third_party/qdrant` is load-bearing).

## 1. Install

```sh
git clone --recurse-submodules https://github.com/electroglyph/qdrant_edge_cs.git
cd qdrant_edge_cs
dotnet add reference src/Qdrant.Edge/Qdrant.Edge.csproj
```

```csharp
using Qdrant.Edge;
```

## 2. Build the native library and stage per-RID assets

NuGet consumers skip this section: release packages ship all six prebuilt
engines as `runtimes/` assets. What follows is for source builds.

```sh
cargo build -p qdrant-edge-cabi --release --manifest-path native/cabi/Cargo.toml
```

Copy the build output next to the consuming app under
`runtimes/<rid>/native/`. The test and Demo bins each need their own copy.
Binaries are git-ignored CI artifacts.

| RID | File name |
|---|---|
| `linux-x64` | `libqdrant_edge.so` |
| `linux-arm64` | `libqdrant_edge.so` |
| `osx-x64` | `libqdrant_edge.dylib` |
| `osx-arm64` | `libqdrant_edge.dylib` |
| `win-x64` | `qdrant_edge.dll` |
| `win-arm64` | `qdrant_edge.dll` |

Example layout for a `linux-x64` app rooted at `bin/Release/net10.0/`:

```text
bin/Release/net10.0/runtimes/linux-x64/native/libqdrant_edge.so
```

`NativeLoader` resolves only this layout (per-RID directory, OS-specific file
name above); there is no system-library fallback. Probe before use:

```csharp
if (!NativeLoader.IsAvailable)
{
    Console.WriteLine("Build the cabi crate into runtimes/<rid>/native/ first.");
    return;
}
```

`NativeLoader.ExpectedVersion` is `"0.10.0"` and matches
`src/Qdrant.Edge/Qdrant.Edge.csproj` (`VersionPrefix 0.10.0`) and
`native/cabi/Cargo.toml`. Without a staged library, `EdgeShard.Create` and
`EdgeShard.Load` throw `EdgeOperationException`.

## 3. First shard: create, upsert, query, flush, reopen

```csharp
using Qdrant.Edge;

string dir = Path.Combine(Path.GetTempPath(), "qdrant-edge-first-" + Guid.NewGuid().ToString("N"));

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
Console.WriteLine($"Points after reopen: {reopened.Count(new CountRequest { Exact = true })}");
```

Expected output note: the query returns 1 hit (the single upserted point);
the count after reopen is 1. Scores are engine-computed; pin counts and ids,
not score literals. `Dispose` flushes then releases (idempotent); see
[`lifecycle.md`](lifecycle.md) for `Unload` vs `Dispose`.

## 4. Semantic search with an ONNX embedding model (condensed)

The top-level [`README.md`](../README.md) carries the full ONNX end-to-end sample
(`Microsoft.ML.OnnxRuntime` 1.31.0 + `Tokenizers.HuggingFace` 3.23.1 against
`jina-embeddings-v5-text-nano-retrieval-onnx/model_int8.onnx`, 768-dim
cosine). The condensed shape is:

```csharp
// Embed documents with a shared InferenceSession + Tokenizer, then:
EdgeConfig textConfig = new()
{
    VectorData = new Dictionary<string, VectorDataConfig>
    {
        // "text" holds the document embeddings; the query below addresses it with usingVector: "text".
        ["text"] = new VectorDataConfig { Size = 768, Distance = Distance.Cosine },
    },
};
using EdgeShard textShard = EdgeShard.Create(dir, textConfig);
// Upsert one Point per document with a "text" NamedVector holding Embed(prefixedText)
// and a JsonObject payload holding text + category.
IReadOnlyList<ScoredPoint> textHits = textShard.Query(new QueryRequest
{
    Limit = 2,
    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense(queryEmbedding), usingVector: "text")),
    WithPayload = WithPayload.CreateFields(["text", "category"]),
});
```

Keep one shared `InferenceSession`/`Tokenizer`/`EdgeShard`; dispose each;
`Flush()` before process exit when `Dispose` is skipped.

## Common mistakes

- Cloning without `--recurse-submodules`: the native build fails because the
  pinned engine sources are missing. Re-clone with submodules.
- Copying the native library to the wrong path or file name: `IsAvailable`
  stays false and `Create`/`Load` throw `EdgeOperationException`. Check the
  per-RID table above.
- Hardcoding `/tmp` or `C:\` paths: use `Path.GetTempPath()` and
  `Path.Combine` so samples run cross-platform.

Cross-links: [`lifecycle.md`](lifecycle.md), [`vectors-config.md`](vectors-config.md), [`errors-limits.md`](errors-limits.md).

---

Back to [documentation index](README.md).
