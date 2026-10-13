# Qdrant.Edge (qdrant_edge_cs)

Idiomatic C# interface for `qdrant_edge` (embedded single-shard vector search).
Version `0.8.0`, synced with `ffi` / `python` / `amalgamate.py`.

## Layout

- `src/Qdrant.Edge/` — managed DTOs, validation, `EdgeShard`, native interop stubs.
- `tests/Qdrant.Edge.Tests/` — xunit validation suite (no native needed).
- `examples/Demo/` — port of `demo.py` + `load-existing.py`.
- `.github/workflows/ci.yml` — build + test.

## Native library

The engine stays in Rust. `native/cabi/` (`qdrant-edge-cabi`, `cdylib` named
`qdrant_edge`) exposes the JSON-in/JSON-out C ABI over the engine sources in
the pinned `third_party/qdrant` submodule (qdrant @ `016542aa` — the ABI
depends on its engine types, so the pin is load-bearing). Clone with
`git clone --recurse-submodules`, then:

```sh
cargo build -p qdrant-edge-cabi --release --manifest-path native/cabi/Cargo.toml
```

and copy the output per RID under
`runtimes/{linux-x64,linux-arm64,osx-x64,osx-arm64,win-x64,win-arm64}/native/`
(e.g. `libqdrant_edge.so` for `linux-x64`), next to the consuming app —
the test and Demo bins each need their own copy. The `.so`/`.dll` files are
CI artifacts and are git-ignored; without one, `EdgeShard.Create/Load`
throws `EdgeOperationException` and the lifecycle tests assert that behavior.

## Lifecycle

```csharp
EdgeConfig config = new()
{
    VectorData = new Dictionary<string, VectorDataConfig>
    {
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
IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest { Limit = 10 });
shard.Flush();
// Reopen: using EdgeShard reopened = EdgeShard.Load(dir);
```

`Dispose` flushes then releases (idempotent). After `Unload`/`Dispose`,
operations throw `ShardClosedException`.

## Keyword renames (FFI/Python parity)

- `Mmr.Lambda` (Python `lambda_`), `ExceptMatch.Except` (Python `except_`).
- FFI `create_field_index` + `create_field_index_with_params` split is kept;
  Python merges them into one union.
- FFI `WithPayload = Bool/Fields/Exclude`, `WithVector = Bool/Names`;
  Python uses `bool | string[]` shapes.
- `search_matrix` is behind `QDRANT_EDGE_MATRIX` (mirrors FFI `matrix` feature).

## v2 gaps

Read-only follower (`live_reload`), batch writer (`apply_batch`/`preview_batch`),
and the `Bm25` embed helper are not in v1 (not on the FFI surface, except BM25
which is Python-only). v1 ships sparse search via `sparse_vector_data` +
`Modifier.Idf` only.
