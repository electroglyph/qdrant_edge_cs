# Snapshots and tuning

Purpose: snapshot manifest/replace/merge/unpack, `Optimize`, config setters,
flush discipline, and the experimental `SearchMatrix`.

Prerequisites: 4-dim `Distance.Dot` shard. Snapshot archives are GNU-tar
files of a flushed shard directory in these examples.

## Concepts

- `SnapshotManifest()` returns an opaque manifest as a JSON string (FFI
  shape; Python returns a dict).
- `UpdateFromSnapshot(snapshotPath, tmpDir? = null)` applies an archive to
  the live shard. A full recovery replaces data entirely; a partial recovery
  merges in place and is not atomic (a failed partial merge may leave a
  half-merged directory — recover from a full snapshot to return to a known
  state). `tmpDir` defaults to the snapshot parent when null.
- `EdgeShard.UnpackSnapshot(snapshotPath, targetPath)` is static and unpacks
  without a live shard.
- If a full recovery fails after the data directory was cleared, the shard
  is left unloaded and later ops throw `ShardClosedException`.
- `Optimize()` runs segment optimizers and returns `true` when work was
  done. `SetHnswConfig` / `SetVectorHnswConfig` / `SetOptimizersConfig`
  persist tuning changes; `Flush()` persists WAL + segments.
- `SearchMatrix` is `[Experimental("QDRANT_EDGE_MATRIX")]` with O(n^2) cost:
  opt in knowingly with `#pragma warning disable` or an explicit opt-in
  note. It samples `SampleSize` points and returns `LimitPerSample` nearest
  lists per sample; `Using` selects the vector field.

## Manifest, full replace, and static unpack

```csharp
string dir = Path.Combine(Path.GetTempPath(), "qdrant-edge-snap-" + Guid.NewGuid().ToString("N"));
EdgeConfig fourDim = new()
{
    VectorData = new Dictionary<string, VectorDataConfig>
    {
        [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
    },
};
using EdgeShard shard = EdgeShard.Create(dir, fourDim);
shard.Update(UpdateOperation.UpsertPoints(
[
    new Point { Id = PointId.FromNumber(1), Vector = Vector.Single([1f, 2f, 3f, 4f]) },
]));
shard.Flush();

string manifestJson = shard.SnapshotManifest();
using System.Text.Json.JsonDocument manifest = System.Text.Json.JsonDocument.Parse(manifestJson);
Console.WriteLine($"Manifest is JSON: {manifest.RootElement.ValueKind}");

string archive = Path.Combine(Path.GetTempPath(), "snap-" + Guid.NewGuid().ToString("N") + ".tar");
// Archive must be a valid snapshot tar (for example a GNU-tar of the flushed
// shard directory). The two calls below require that file to exist.
try
{
    shard.UpdateFromSnapshot(archive, tmpDir: Path.GetTempPath());
}
catch (EdgeException ex)
{
    Console.WriteLine($"Snapshot apply needs a valid archive: {ex.GetType().Name}");
}

string restoreDir = Path.Combine(Path.GetTempPath(), "qdrant-edge-restore-" + Guid.NewGuid().ToString("N"));
try
{
    EdgeShard.UnpackSnapshot(archive, restoreDir);
    Console.WriteLine($"Unpacked to {restoreDir}");
}
catch (EdgeException ex)
{
    Console.WriteLine($"Unpack needs a valid archive: {ex.GetType().Name}");
}
```

The manifest string is opaque: parse it as JSON but do not depend on its
fields. `tmpDir` null would default to the archive parent.

## Partial merge is not atomic; full failure leaves the shard unloaded

```csharp
try
{
    shard.UpdateFromSnapshot("/nonexistent/snap.tar");
}
catch (EdgeException ex)
{
    Console.WriteLine($"Bad snapshot fails: {ex.GetType().Name}");
}

try
{
    shard.UpdateFromSnapshot(archive);
    Console.WriteLine("Snapshot applied; partial merges may still leave half-merged state on failure.");
}
catch (EdgeException)
{
    Console.WriteLine("Snapshot failed; recover from a full snapshot for a known-good state.");
}```

A failed full recovery after the directory was cleared leaves the shard
unloaded:

```csharp
// After such a failure, later ops throw:
try
{
    shard.Count(new CountRequest { Exact = true });
}
catch (ShardClosedException)
{
    Console.WriteLine("Unloaded shard throws ShardClosedException.");
}
```

## Optimize, setters, and flush discipline

```csharp
shard.SetHnswConfig(new HnswIndexConfig { M = 32, EfConstruct = 200 });
shard.SetVectorHnswConfig("image", new HnswIndexConfig { M = 16 });
shard.SetOptimizersConfig(new OptimizersConfig { DefaultSegmentNumber = 2 });
bool didWork = shard.Optimize();
shard.Flush();
Console.WriteLine($"Optimize did work: {didWork}");
```

Call `Flush()` before process exit when skipping `Dispose`; `GetConfig()`
echoes the persisted tuning. Heavy deletes (for example 1000 upserts then
900 deletes plus flush) are the shape that observably returns `true`.

## SearchMatrix (experimental, O(n^2))

```csharp
#pragma warning disable QDRANT_EDGE_MATRIX
SearchMatrixResponse matrix = shard.SearchMatrix(new SearchMatrixRequest
{
    SampleSize = 2,
    LimitPerSample = 2,
    Using = null,
});
#pragma warning restore QDRANT_EDGE_MATRIX
Console.WriteLine($"Samples: {matrix.SampleIds.Count}, lists: {matrix.Nearests.Count}");

#pragma warning disable QDRANT_EDGE_MATRIX
SearchMatrixResponse zeroSample = shard.SearchMatrix(new SearchMatrixRequest
{
    SampleSize = 0,
    LimitPerSample = 2,
});
#pragma warning restore QDRANT_EDGE_MATRIX
Console.WriteLine($"Zero sample returns empty: {zeroSample.SampleIds.Count == 0 && zeroSample.Nearests.Count == 0}");
```

Zero `SampleSize` returns fully empty collections; zero `LimitPerSample`
returns empty nearest lists. Cost grows quadratically; keep samples small.

## Common mistakes

- Treating the manifest as stable: it is opaque JSON; store it, do not parse
  fields for logic.
- Assuming a partial merge is atomic: it merges in place; on failure,
  restore from a full snapshot.
- Calling other ops after a failed full recovery and expecting data: the
  shard is unloaded; reopen or restore first.
- Using `SearchMatrix` without the experimental opt-in: the call site must
  disable `QDRANT_EDGE_MATRIX` or document the O(n^2) opt-in.

Cross-links: `lifecycle.md`, `reads.md`, `errors-limits.md`.
