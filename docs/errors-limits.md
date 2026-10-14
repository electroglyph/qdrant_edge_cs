# Errors and limits

Purpose: the exception hierarchy, every enforced cap with a failing example,
keyword renames, FFI-vs-Python shape divergences, and v2 gaps. Per-type
bounds also appear on their primary pages; this page is the checklist.

Prerequisites: 4-dim `Distance.Dot` shard. `EdgeValidation` constants are the
client-side mirrors; the engine remains authoritative.

## Hierarchy and catch sample

All failures derive from `EdgeException` with a `Reason` string (never a
`Message` field — the name avoids a Kotlin `Throwable.message` collision
carried over from the FFI design).

| Exception | Meaning |
|---|---|
| `EdgeInvalidArgumentException` | Bad request — client validation or native rejection |
| `EdgeOperationException` | Engine failure (I/O, missing native lib, dimension mismatch) |
| `ShardClosedException` | Operation after `Unload`/`Dispose` |

```csharp
try
{
    shard.Count(new CountRequest
    {
        Filter = new Filter
        {
            Must = [Condition.Field(new FieldCondition { Key = "city", Match = Match.CreateValue(ValueVariants.String("x")) })],
        },
    });
}
catch (ShardClosedException)
{
    Console.WriteLine("Shard is unloaded; reopen first.");
}
catch (EdgeInvalidArgumentException ex)
{
    Console.WriteLine($"Bad request: {ex.Reason}");
}
catch (EdgeOperationException ex)
{
    Console.WriteLine($"Engine failure: {ex.Reason}");
}
catch (EdgeException ex)
{
    Console.WriteLine($"Other edge failure: {ex.Reason}");
}
```

`EdgeException.Reason` carries the detail; `Message` echoes it via the base
`Exception` constructor. Catch order matters: `ShardClosedException` first,
then invalid-argument, then operation.

## Caps table

| Cap | Value | Enforced by |
|---|---|---|
| Result counts (`limit`, `offset`, `groups`, `group_size`, `sample_size`, `limit_per_sample`, MMR `candidates_limit`, prefetch `limit`, facet `limit`) | <= 1,048,576 (`EdgeValidation.MaxResultCount`) | `CheckResultCount`; `count`/`retrieve` have no limit |
| Filter nesting depth | <= 64 (`MaxQueryNestingDepth`) | `Filter.Depth` + `CheckNestingDepth`; leaves 0, wrappers +1 |
| Prefetch nesting | depth <= 64 (65 nodes pass, 66 fail) | `Prefetch.Validate(depth = 0)` + `CheckNestingDepth` |
| Formula nodes | <= 10,000 (`MaxFormulaNodes`) | `Expression` measure + `CheckExpressionSize` on the actual tree |
| Formula depth | <= 64 | `Expression.Validate` recomputed depth |
| Dense vector dims | 1..=65536 | `CheckVectorSize` |
| HNSW `M` / `PayloadM` | <= 2048 (`MaxHnswM`) | `HnswIndexConfig.Validate` |
| HNSW `EfConstruct` | 4..=100000 | `HnswIndexConfig.Validate` |
| HNSW `MaxIndexingThreads` | <= 1024 | `HnswIndexConfig.Validate` |
| Optimizer `DeletedThreshold` | finite, [0, 1] | `OptimizersConfig.Validate` |
| Geo lat / lon | [-90, 90] / [-180, 180], finite | `CheckGeoPoint` |
| Geo radius | finite, >= 0 | `GeoRadius.Validate` |
| Polygon rings | closed, >= 4 points | `GeoPolygon.Validate` |
| Slice | `total` > 0, `index` < `total` | `Condition.Validate` |
| `FieldCondition` | exactly one predicate | `FieldCondition.Validate` |
| `IntegerIndexParams` | not (`Lookup: false` + `Range: false`) | `IntegerIndexParams.Validate` |
| Sparse lengths | `Indices.Count == Values.Count` | `SparseVector.Validate` |
| Floats (dense, multi-dense, sparse, and named vector values; feedback scores and coefficients; RRF weights; MMR lambda; decay scale/midpoint; div fallback; float order-by start-from; geo point/radius; score thresholds) | finite (no NaN/±Inf) | `CheckFinite` |

## One failing example per row (with exception type)

```csharp
static void ExpectInvalid(Action action, string label)
{
    try
    {
        action();
        Console.WriteLine($"{label}: unexpectedly passed");
    }
    catch (EdgeInvalidArgumentException ex)
    {
        Console.WriteLine($"{label}: EdgeInvalidArgument: {ex.Reason}");
    }
}

// Limit overflow.
ExpectInvalid(() => shard.Query(new QueryRequest { Limit = 1_048_577 }), "limit overflow");

// Filter depth 65 fails (64 wrappers pass, 65th fails).
Filter DeepFilter(int wrappers)
{
    Filter inner = new();
    for (int i = 0; i < wrappers; i++)
    {
        inner = new Filter { Must = [Condition.Nested("a", inner)] };
    }
    return inner;
}
ExpectInvalid(() => DeepFilter(65).Validate(), "filter depth 65");
DeepFilter(64).Validate();

// Prefetch 65-deep passes, 66 fails.
Prefetch DeepPrefetch(int depth)
{
    Prefetch leaf = new() { Limit = 1 };
    for (int i = 0; i < depth; i++)
    {
        leaf = new Prefetch { Limit = 1, Prefetches = [leaf] };
    }
    return leaf;
}
new QueryRequest { Limit = 1, Prefetches = [DeepPrefetch(64)] }.Validate();
ExpectInvalid(() => new QueryRequest { Limit = 1, Prefetches = [DeepPrefetch(65)] }.Validate(), "prefetch 66");

// Formula 10,002 nodes (10,001 terms plus the Sum node) fails.
ExpectInvalid(() =>
{
    List<Expression> terms = Enumerable.Range(0, 10_001).Select(_ => Expression.Constant(1f)).ToList();
    Expression.Sum(terms);
}, "formula 10k+1");

// NaN vector.
ExpectInvalid(() => Vector.Single([float.NaN]).Validate(), "NaN vector");

// Bad UUID string.
ExpectInvalid(() => PointId.FromString("not-a-uuid"), "bad UUID");

// size=0 vector config.
ExpectInvalid(() => new VectorDataConfig { Size = 0, Distance = Distance.Dot }.Validate("v"), "size=0");

// Bad HNSW m / ef.
ExpectInvalid(() => new HnswIndexConfig { M = 2049 }.Validate("hnsw"), "bad HNSW m");
ExpectInvalid(() => new HnswIndexConfig { EfConstruct = 3 }.Validate("hnsw"), "bad HNSW ef");

// Unclosed / too-few-point polygon.
ExpectInvalid(() => new GeoPolygon
{
    Exterior = new GeoLineString
    {
        Points =
        [
            new GeoPoint { Lon = 0, Lat = 0 },
            new GeoPoint { Lon = 1, Lat = 0 },
            new GeoPoint { Lon = 1, Lat = 1 },
            new GeoPoint { Lon = 2, Lat = 2 },
        ],
    },
}.Validate(), "unclosed polygon");

// Bad lat/lon.
ExpectInvalid(() => new GeoPoint { Lon = 0, Lat = 999 }.Validate(), "bad lat");
ExpectInvalid(() => new GeoPoint { Lon = 181, Lat = 0 }.Validate(), "bad lon");

// Negative radius.
ExpectInvalid(() => new GeoRadius
{
    Center = new GeoPoint { Lon = 0, Lat = 0 },
    Radius = -1,
}.Validate(), "negative radius");

// Slice bounds.
ExpectInvalid(() => Condition.Slice(0, 0).Validate(), "slice total=0");
ExpectInvalid(() => Condition.Slice(2, 2).Validate(), "slice index>=total");

// Multi-predicate FieldCondition.
ExpectInvalid(() => new FieldCondition
{
    Key = "price",
    Range = new RangeFloat { Gte = 1 },
    ValuesCount = new ValuesCount { Gte = 1 },
}.Validate(), "multi-predicate field");

// Integer Lookup=false + Range=false.
ExpectInvalid(() => UpdateOperation.CreateFieldIndexWithParams(
    "bad", PayloadIndexParams.Integer(new IntegerIndexParams { Lookup = false, Range = false })),
    "integer both-false");

// Sparse Indices/Values length mismatch.
ExpectInvalid(() => new SparseVector { Indices = [1u, 2u], Values = [0.5f] }.Validate(), "sparse mismatch");

// Optimizer DeletedThreshold out of [0, 1].
ExpectInvalid(() => new OptimizersConfig { DeletedThreshold = 1.5 }.Validate(), "deleted threshold range");
ExpectInvalid(() => new OptimizersConfig { DeletedThreshold = double.NaN }.Validate(), "deleted threshold finite");

// Group and sample caps share the same 1,048,576 ceiling as limit.
ExpectInvalid(() => new GroupRequest
{
    Query = new QueryRequest { Limit = 1 },
    GroupBy = "city",
    Groups = 1_048_577,
    GroupSize = 1,
}.Validate(), "groups overflow");
ExpectInvalid(() => new SearchMatrixRequest { SampleSize = 1_048_577, LimitPerSample = 1 }.Validate(), "sample size overflow");
```

Non-abort note: over-cap requests fail without aborting the process and
leave the shard usable — a `limit = ulong.MaxValue` query throws
`EdgeInvalidArgumentException` and the next valid query still succeeds. Every
request is validated before dispatch, and serialization depth failures also
surface as `EdgeInvalidArgumentException` (never raw `JsonException`).

## Keyword renames (FFI/Python parity)

- `MmrScoringQuery.Lambda` (Python `lambda_`).
- `ExceptMatch.Except` (Python `except_`).
- `TurboQuantBitSize.Bits1Point5` (Python `Bits1_5` member spelling; the wire
  value is numeric).

## FFI-vs-Python shapes (documented once; pointers elsewhere)

- Field index: C# keeps the FFI split (`CreateFieldIndex` schema form vs
  `CreateFieldIndexWithParams` params form); Python merges both into one
  `create_field_index(field_name, schema | params)` union ([`writes.md`](writes.md)).
- `WithPayload` is `Bool`/`Fields`/`Exclude` (`BoolWithPayload`,
  `FieldsWithPayload`, `ExcludeWithPayload`); Python uses
  `bool | string[] | PayloadSelector`. `WithVector` is `Bool`/`Names`;
  Python uses `bool | string[]` ([`reads.md`](reads.md)).
- `SearchParams` is exactly `{ HnswEf, Exact, IndexedOnly }`; Python adds
  `quantization`/`acorn`/`idf` ([`queries.md`](queries.md)).
- `SnapshotManifest()` returns a JSON string; Python returns a dict
  ([`snapshots-tuning.md`](snapshots-tuning.md)).
- C# uses `RetrieveRequest` / `ScrollResponse` records; Python uses
  `retrieve(point_ids, ...)` args and a `(records, next_offset)` tuple.
- Python has no `query_groups` / `search_matrix`; C# has both ([`reads.md`](reads.md),
  [`snapshots-tuning.md`](snapshots-tuning.md)).
- `SearchMatrix` is `[Experimental("QDRANT_EDGE_MATRIX")]` with O(n^2) cost —
  usable, but opt in knowingly.
- `RangeDatetime` (not `RangeDateTime`); `OrderValue` covers datetime sort
  keys as `Int`/`Float`.

## v2 gaps

Read-only follower (`live_reload`), batch writer (`apply_batch` /
`preview_batch`), and the `Bm25` embed helper are engine-only surfaces with
no FFI binding. v1 ships sparse search via `sparse_vector_data` +
`Modifier.Idf` only (see `src/Qdrant.Edge/README.md` "v2 gaps").

## Common mistakes

- Catching `Exception` and reading `Message`: catch `EdgeException` arms and
  read `Reason`.
- Asserting `Scalar`/`Product` quantization survives optimize: only
  `Binary`/`Turbo` survive on appendable segments.
- Counting nesting from 1: both filter and prefetch count from depth 0.
  Filter depth 64 passes while depth 65 fails (64 wrappers pass, 65 fail);
  prefetch depth 64 passes while depth 65 fails (65 nodes pass, 66 fail).
  Test the depth boundary rather than assuming.
- Treating `SearchMatrix` as stable: keep the experimental opt-in and small
  samples.

Related: [`getting-started.md`](getting-started.md), [`lifecycle.md`](lifecycle.md), [`writes.md`](writes.md), [`reads.md`](reads.md), [`queries.md`](queries.md), [`filters.md`](filters.md), [`geo-datetime.md`](geo-datetime.md), [`vectors-config.md`](vectors-config.md), [`snapshots-tuning.md`](snapshots-tuning.md). This page is the canonical caps-and-exceptions reference: the other guides link here for limits, the exception hierarchy, and FFI-vs-Python differences instead of repeating them.

---

Back to [documentation index](README.md).
