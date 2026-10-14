# Lifecycle

Purpose: shard creation/loading, config readback and tuning entry points,
flush discipline, `Unload` vs `Dispose`, `Path`/`IsLoaded`, threading, and the
two async overloads. Write/read/query shapes live in `writes.md`, `reads.md`,
and `queries.md`.

Prerequisites: staged native library (see `getting-started.md`). Examples use
a temp dir plus a 4-dim `Distance.Dot` fixture unless stated.

## Concepts

- `EdgeShard.Create(path, config, cancellationToken?)` validates `config`,
  creates the directory when missing, and returns a loaded shard.
- `EdgeShard.Load(path, config? = null, cancellationToken?)`: with `config`
  it creates when missing; without it opens an existing directory. Loading a
  missing directory without `config` fails and does not create the directory.
- `Flush()` persists WAL + segments. `Optimize()` runs optimizers to
  completion and returns `true` when work was done.
- `GetConfig()` echoes the loaded config. `SetHnswConfig`,
  `SetVectorHnswConfig(vectorName, hnsw)`, and `SetOptimizersConfig` persist
  tuning changes (`""` addresses the default unnamed vector).
- `Path` returns the shard directory and throws `ShardClosedException` after
  unload. `IsLoaded` tracks loaded state.
- `Unload()` flushes then releases (idempotent; releases even when the flush
  fails, then rethrows the flush error). `Dispose()` calls `Unload`, swallows
  the flush error (the handle is already released), marks disposed, and is
  idempotent. After either, every operation throws `ShardClosedException`.
- Threading: one `Lock` per shard guards instance calls; concurrent calls
  from many threads are safe and run one at a time per shard, in parallel
  across shards. The per-shard `Lock` is held across the native call,
  so `Unload` and `UpdateFromSnapshot` serialize against reads and updates
  on the same shard instead of racing them.
- Cancellation tokens are checked before dispatch only; an in-flight native
  call cannot be cancelled.
- Missing native library: `Create`/`Load` (and static `UnpackSnapshot`)
  throw `EdgeOperationException`.

## Examples

### Create creates a missing directory; load without config does not

```csharp
string missing = Path.Combine(Path.GetTempPath(), "qdrant-edge-missing-" + Guid.NewGuid().ToString("N"));
EdgeConfig fourDim = new()
{
    VectorData = new Dictionary<string, VectorDataConfig>
    {
        [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
    },
};

using EdgeShard created = EdgeShard.Create(missing, fourDim);
Console.WriteLine($"Created at {created.Path}: loaded={created.IsLoaded}");

string absent = Path.Combine(Path.GetTempPath(), "qdrant-edge-absent-" + Guid.NewGuid().ToString("N"));
try
{
    using EdgeShard noCreate = EdgeShard.Load(absent);
}
catch (EdgeException ex)
{
    Console.WriteLine($"Missing load failed as expected: {ex.GetType().Name}");
    Console.WriteLine($"No directory created: {!Directory.Exists(absent)}");
}
```

Expected: first block prints the created path; the load throws
(`EdgeOperationException` from the engine for a missing path) and leaves no
directory behind.

### Load with config creates when missing

```csharp
// fourDim is the 4-dim Distance.Dot EdgeConfig from the previous example.
string fresh = Path.Combine(Path.GetTempPath(), "qdrant-edge-fresh-" + Guid.NewGuid().ToString("N"));
using EdgeShard viaLoad = EdgeShard.Load(fresh, fourDim);
Console.WriteLine($"Load-with-config created: {viaLoad.IsLoaded}");
```

### Flush, unload, reload round-trip

```csharp
// fourDim is the 4-dim Distance.Dot EdgeConfig from the first example.
string dir = Path.Combine(Path.GetTempPath(), "qdrant-edge-roundtrip-" + Guid.NewGuid().ToString("N"));
using EdgeShard shard = EdgeShard.Create(dir, fourDim);
shard.Update(UpdateOperation.UpsertPoints(
[
    new Point { Id = PointId.FromNumber(1), Vector = Vector.Single([1f, 2f, 3f, 4f]) },
]));
shard.Flush();
shard.Unload();
Console.WriteLine($"After unload: loaded={shard.IsLoaded}");
try
{
    _ = shard.Path;
}
catch (ShardClosedException)
{
    Console.WriteLine("Path after unload throws ShardClosedException.");
}
using EdgeShard reopened = EdgeShard.Load(dir);
Console.WriteLine($"Points after reopen: {reopened.Count(new CountRequest { Exact = true })}");
```

Expected: `After unload: loaded=False`, the `Path` message, and count 1.

### Operations after unload throw ShardClosedException

```csharp
// fourDim is the 4-dim Distance.Dot EdgeConfig from the first example.
// dir is a fresh temp dir; shard is a loaded shard on that dir.
string opDir = Path.Combine(Path.GetTempPath(), "qdrant-edge-ops-" + Guid.NewGuid().ToString("N"));
using EdgeShard shard = EdgeShard.Create(opDir, fourDim);
shard.Unload();
try
{
    shard.Count(new CountRequest { Exact = true });
}
catch (ShardClosedException)
{
    Console.WriteLine("Ops after unload throw ShardClosedException.");
}
```

### Config readback and tuning setters

```csharp
// fourDim is the 4-dim Distance.Dot EdgeConfig from the first example.
using EdgeShard tuned = EdgeShard.Create(
    Path.Combine(Path.GetTempPath(), "qdrant-edge-tuned-" + Guid.NewGuid().ToString("N")), fourDim);
EdgeConfig echoed = tuned.GetConfig();
Console.WriteLine($"Echoed size: {echoed.VectorData[""].Size}");
tuned.SetHnswConfig(new HnswIndexConfig { M = 32, EfConstruct = 200 });
tuned.SetVectorHnswConfig("", new HnswIndexConfig { M = 16 });
tuned.SetOptimizersConfig(new OptimizersConfig { DefaultSegmentNumber = 2 });
Console.WriteLine($"Optimize did work: {tuned.Optimize()}");
tuned.Flush();
```

`GetConfig()` returns the configured fields; `Optimize()` returns a bool.
`SetHnswConfig` validates `M`/`PayloadM`, `EfConstruct`, and thread bounds
before the native call; `SetOptimizersConfig` validates optimizer bounds.

### Missing native library surfaces EdgeOperationException

```csharp
// fourDim is the 4-dim Distance.Dot EdgeConfig from the first example.
string missingNativeDir = Path.Combine(Path.GetTempPath(), "qdrant-edge-missing-native-" + Guid.NewGuid().ToString("N"));
if (!NativeLoader.IsAvailable)
{
    try
    {
        using EdgeShard doomed = EdgeShard.Create(missingNativeDir, fourDim);
    }
    catch (EdgeOperationException ex)
    {
        Console.WriteLine($"Missing native: {ex.Reason}");
    }
}
```

### Only QueryAsync and SearchAsync exist; token fires before dispatch

```csharp
// fourDim is the 4-dim Distance.Dot EdgeConfig from the first example.
using EdgeShard asyncShard = EdgeShard.Create(
    Path.Combine(Path.GetTempPath(), "qdrant-edge-async-" + Guid.NewGuid().ToString("N")), fourDim);
IReadOnlyList<ScoredPoint> asyncHits = await asyncShard.QueryAsync(new QueryRequest { Limit = 5 });
IReadOnlyList<ScoredPoint> asyncSearch = await asyncShard.SearchAsync(new SearchRequest
{
    Query = Query.Nearest(NamedVector.Dense([1f, 1f, 1f, 1f])),
    Limit = 5,
});
Console.WriteLine($"Async hits: {asyncHits.Count + asyncSearch.Count}");

using CancellationTokenSource preCancelled = new();
preCancelled.Cancel();
try
{
    await asyncShard.QueryAsync(new QueryRequest { Limit = 1 }, preCancelled.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Pre-cancelled token throws before dispatch.");
}
```

There are no other async overloads; async offloads the sync engine call to
the thread pool and cannot cancel an in-flight call.

### Concurrent counts stay safe; unload races surface only Ok or ShardClosed

```csharp
// fourDim is the 4-dim Distance.Dot EdgeConfig from the first example.
// asyncShard is a loaded shard from the previous example; to run standalone,
// create it first with the two lines below.
using EdgeShard asyncShard = EdgeShard.Create(
    Path.Combine(Path.GetTempPath(), "qdrant-edge-concurrent-" + Guid.NewGuid().ToString("N")), fourDim);
IReadOnlyList<Task> readers = Enumerable.Range(0, 4)
    .Select(_ => Task.Run(() => asyncShard.Count(new CountRequest { Exact = true })))
    .ToArray();
await Task.WhenAll(readers);
Console.WriteLine("Concurrent counts completed without corruption.");
```

A racing `Unload` may make a concurrent call throw `ShardClosedException`;
no other outcome is possible.

## Common mistakes

- Calling `Path` or any op after `Unload`/`Dispose` and catching the wrong
  type: the throw is always `ShardClosedException`, derived from
  `EdgeException` with a `Reason` string.
- Expecting `Dispose` to rethrow a flush failure: `Unload` rethrows after
  releasing, but `Dispose` swallows it because the handle is already
  released. Check `Flush()` explicitly when flush errors matter.
- Passing `Load(missingPath)` without `config` and expecting creation: only
  `Create` and `Load(path, config)` create; plain `Load` opens.

Cross-links: `getting-started.md`, `writes.md`, `snapshots-tuning.md`,
`errors-limits.md`.
