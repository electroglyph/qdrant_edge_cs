# Vectors and config

Purpose: `EdgeConfig` families, dense/sparse/multi-dense shapes, distances,
memory, datatypes, HNSW/optimizer bounds, all four quantization configs, all
eight payload index types, text analysis, and config-merge basics.

Prerequisites: staged native library. `dirFor` below is a helper that mints a
fresh temp dir per config so incompatible configs do not collide.

## Concepts

- `EdgeConfig` requires at least one family: non-empty `VectorData` or
  non-empty `SparseVectorData`. `""` is the default unnamed dense field.
- Dense fields (`VectorDataConfig`) require `Size` (1..=65536) and `Distance`;
  optional `QuantizationConfig`, `MultivectorConfig`, `Datatype`, `HnswConfig`,
  `Memory`.
- Sparse fields (`SparseVectorDataConfig`) carry optional
  `FullScanThreshold`, `Datatype`, `Modifier` (v1 search uses `Modifier.Idf`),
  `Memory`.
- `Distance` has 4 values (`Cosine`, `Euclid`, `Dot`, `Manhattan`).
  `Memory` has 3 (`Cold`, `Cached`, `Pinned`). `VectorStorageDatatype` has 4
  (`Float32`, `Float16`, `Uint8`, `Turbo4`). `MultiVectorConfig` has one
  comparator (`MaxSim`).
- `HnswIndexConfig` bounds: `M`/`PayloadM` <= 2048, `EfConstruct` 4..=100000,
  `MaxIndexingThreads` <= 1024. `OptimizersConfig` bounds: finite
  `DeletedThreshold` in [0, 1] plus `_Kb` fields (`MaxSegmentSizeKb` /
  `IndexingThresholdKb` must be > 0 when set; segment counts capped at
  1,048,576).
- Quantization exposes all four strategies (`Scalar`, `Product`, `Binary`,
  `Turbo`) for parity, but the engine keeps only `Binary`/`Turbo` for
  appendable segments: do not assert `Scalar`/`Product` survive optimize.
- Payload indexes expose 8 types; `IntegerIndexParams` with
  `Lookup: false, Range: false` is rejected.
- Text analysis: `TokenizerType` 4 values, `Stemmer` (`Snowball` 19
  languages / `Disabled`), `Stopwords` (`CreateLanguage` / `Set`),
  `Language` 30 values.
- Config merge: `Load(path)` with null keeps the stored config; reloads apply
  tunables; a missing config file derives from the directory; an
  incompatible size change (for example `size: 1` to `size: 2`) fails.

## Default vector, named vectors, and the create-plus-add flow

```csharp
static string FreshDir(string tag) =>
    Path.Combine(Path.GetTempPath(), $"qdrant-edge-{tag}-" + Guid.NewGuid().ToString("N"));

EdgeConfig single = new()
{
    VectorData = new Dictionary<string, VectorDataConfig>
    {
        [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
    },
};
using EdgeShard shard = EdgeShard.Create(FreshDir("single"), single);

shard.Update(UpdateOperation.CreateDenseVector("image", 4, Distance.Cosine));
shard.Update(UpdateOperation.UpsertPoints(
[
    new Point
    {
        Id = PointId.FromNumber(1),
        Vector = Vector.Named(new Dictionary<string, NamedVector>
        {
            [""] = NamedVector.Dense([1f, 2f, 3f, 4f]),
            ["image"] = NamedVector.Dense([0f, 1f, 0f, 1f]),
        }),
    },
]));
Console.WriteLine($"Named-vector points: {shard.Count(new CountRequest { Exact = true })}");
```

## Sparse plus Idf, and multi-dense shapes with rejections

```csharp
EdgeConfig sparse = new()
{
    SparseVectorData = new Dictionary<string, SparseVectorDataConfig>
    {
        ["splade"] = new SparseVectorDataConfig { Modifier = Modifier.Idf },
    },
};
using EdgeShard sparseShard = EdgeShard.Create(FreshDir("sparse"), sparse);
sparseShard.Update(UpdateOperation.UpsertPoints(
[
    new Point
    {
        Id = PointId.FromNumber(1),
        Vector = Vector.Named(new Dictionary<string, NamedVector>
        {
            ["splade"] = NamedVector.Sparse(new SparseVector
            {
                Indices = [3u, 17u, 420u],
                Values = [0.9f, 0.4f, 0.1f],
            }),
        }),
    },
]));

MultiVectorConfig maxSim = new() { Comparator = MultiVectorComparator.MaxSim };
EdgeConfig multi = new()
{
    VectorData = new Dictionary<string, VectorDataConfig>
    {
        ["colbert"] = new VectorDataConfig
        {
            Size = 2,
            Distance = Distance.Dot,
            MultivectorConfig = maxSim,
        },
    },
};
using EdgeShard multiShard = EdgeShard.Create(FreshDir("multi"), multi);
multiShard.Update(UpdateOperation.UpsertPoints(
[
    new Point { Id = PointId.FromNumber(1), Vector = Vector.MultiDense([[1f, 2f], [3f, 4f]]) },
]));

foreach (Vector bad in (Vector[])
    [
        Vector.MultiDense([]),
        Vector.MultiDense([[]]),
        Vector.MultiDense([[1f, 2f], [3f]]),
    ])
{
    try
    {
        bad.Validate();
    }
    catch (EdgeInvalidArgumentException ex)
    {
        Console.WriteLine($"Ragged/empty multi-dense rejected: {ex.Reason}");
    }
}
```

Sparse `Indices`/`Values` lengths must match; multi-dense rows must share one
width. `SparseVectorDataConfig.Modifier` is `null` when unset (the engine
treats an unset modifier as `Modifier.None`).

## All four quantization configs (with Binary/Turbo-survive note)

```csharp
QuantizationConfig scalar = QuantizationConfig.Scalar(new ScalarQuantizationParams
{
    Type = ScalarType.Int8,
    Quantile = 0.99f,
    Memory = Memory.Cached,
});
QuantizationConfig product = QuantizationConfig.Product(new ProductQuantizationParams
{
    Compression = CompressionRatio.X16,
    Memory = Memory.Cached,
});
QuantizationConfig binary = QuantizationConfig.Binary(new BinaryQuantizationParams
{
    Memory = Memory.Cached,
    Encoding = BinaryQuantizationEncoding.OneBit,
    QueryEncoding = BinaryQuantizationQueryEncoding.Default,
});
QuantizationConfig turbo = QuantizationConfig.Turbo(new TurboQuantizationParams
{
    Memory = Memory.Cached,
    Bits = TurboQuantBitSize.Bits1Point5,
});

ScalarQuantization scalarRecord = new(new ScalarQuantizationParams { Type = ScalarType.Int8 });
ProductQuantization productRecord = new(new ProductQuantizationParams { Compression = CompressionRatio.X16 });
BinaryQuantization binaryRecord = new(new BinaryQuantizationParams { });
TurboQuantization turboRecord = new(new TurboQuantizationParams { Bits = TurboQuantBitSize.Bits1Point5 });

foreach (QuantizationConfig each in (QuantizationConfig[])[scalar, product, binary, turbo])
{
    EdgeConfig quantized = new()
    {
        VectorData = new Dictionary<string, VectorDataConfig>
        {
            [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot, QuantizationConfig = each },
        },
    };
    using EdgeShard eachShard = EdgeShard.Create(FreshDir("quant"), quantized);
    Console.WriteLine($"Quantized load ok: {eachShard.GetConfig().VectorData[""].QuantizationConfig is not null}");
}
```

All four are accepted at load and echo via `GetConfig()`. After `Optimize`,
only `Binary`/`Turbo` are guaranteed to survive on appendable segments;
never assert `Scalar`/`Product` round-trip through optimize.

`VectorStorageDatatype` (`Float32`, `Float16`, `Uint8`, `Turbo4`) rides on
the same field:

```csharp
EdgeConfig typed = new()
{
    VectorData = new Dictionary<string, VectorDataConfig>
    {
        [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot, Datatype = VectorStorageDatatype.Float32 },
    },
};
using EdgeShard typedShard = EdgeShard.Create(FreshDir("datatype"), typed);
Console.WriteLine($"Datatype kept: {typedShard.GetConfig().VectorData[""].Datatype}");
```

## All eight payload index types, Text options, and the Integer guard

```csharp
PayloadIndexParams[] allEight =
[
    PayloadIndexParams.Keyword(new KeywordIndexParams { }),
    PayloadIndexParams.Integer(new IntegerIndexParams { Lookup = true, Range = true }),
    PayloadIndexParams.Float(new FloatIndexParams { }),
    PayloadIndexParams.Geo(new GeoIndexParams { }),
    PayloadIndexParams.Text(new TextIndexParams
    {
        Tokenizer = TokenizerType.Word,
        MinTokenLen = 2,
        MaxTokenLen = 20,
        Lowercase = true,
        AsciiFolding = true,
        PhraseMatching = true,
        Stopwords = Stopwords.CreateLanguage(Language.English),
        Stemmer = Stemmer.Snowball(SnowballLanguage.English),
        Memory = Memory.Cached,
        EnableHnsw = false,
    }),
    PayloadIndexParams.Bool(new BoolIndexParams { }),
    PayloadIndexParams.Datetime(new DatetimeIndexParams { }),
    PayloadIndexParams.Uuid(new UuidIndexParams { }),
];
KeywordPayloadIndex keywordRecord = new(new KeywordIndexParams { });
IntegerPayloadIndex integerRecord = new(new IntegerIndexParams { Lookup = true });

foreach (PayloadIndexParams each in allEight)
{
    shard.Update(UpdateOperation.CreateFieldIndexWithParams($"field_{each.GetType().Name}", each));
}
shard.Update(UpdateOperation.CreateFieldIndexWithParams(
    "custom_stop",
    PayloadIndexParams.Text(new TextIndexParams
    {
        Tokenizer = TokenizerType.Multilingual,
        Stopwords = Stopwords.Set([Language.English], ["custom"]),
        Stemmer = Stemmer.Disabled(),
    })));

try
{
    UpdateOperation.CreateFieldIndexWithParams(
        "bad_int", PayloadIndexParams.Integer(new IntegerIndexParams { Lookup = false, Range = false }));
}
catch (EdgeInvalidArgumentException ex)
{
    Console.WriteLine($"Integer both-false rejected: {ex.Reason}");
}
```

`TokenizerType` has `Prefix`/`Whitespace`/`Word`/`Multilingual`.
`Language` covers 30 stopword languages; `SnowballLanguage` covers 19
stemmer languages. Custom stopwords take optional language plus custom
token lists.

## HNSW/optimizer bounds, memory placements, and config-merge basics

```csharp
new HnswIndexConfig { M = 2048, EfConstruct = 100_000, MaxIndexingThreads = 1024 }.Validate("ok");
new OptimizersConfig { DeletedThreshold = 0.5, MaxSegmentSizeKb = 100, IndexingThresholdKb = 100 }.Validate();

try
{
    new HnswIndexConfig { M = 2049 }.Validate("hnsw");
}
catch (EdgeInvalidArgumentException ex)
{
    Console.WriteLine($"Oversized HNSW rejected: {ex.Reason}");
}

EdgeConfig withMemory = new()
{
    VectorData = new Dictionary<string, VectorDataConfig>
    {
        [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot, Memory = Memory.Cached },
    },
    PayloadMemory = Memory.Cached,
    IdTrackerMemory = Memory.Cached,
};
withMemory.Validate();

string mergeDir = FreshDir("merge");
using EdgeShard first = EdgeShard.Create(mergeDir, single);
first.Flush();
first.Unload();
using EdgeShard kept = EdgeShard.Load(mergeDir);
Console.WriteLine($"Null-load keeps stored size: {kept.GetConfig().VectorData[""].Size}");

try
{
    using EdgeShard badSize = EdgeShard.Load(mergeDir, new EdgeConfig
    {
        VectorData = new Dictionary<string, VectorDataConfig>
        {
            [""] = new VectorDataConfig { Size = 2, Distance = Distance.Dot },
        },
    });
}
catch (EdgeException ex)
{
    Console.WriteLine($"Incompatible size fails: {ex.GetType().Name}");
}
```

An empty `EdgeConfig` (no families) throws; `""` keys pass as the default
vector. Reload with `null` keeps stored settings; only tunables reload;
changing the vector size fails.

## Common mistakes

- Asserting `Scalar`/`Product` quantization survives `Optimize`: only
  `Binary`/`Turbo` are kept for appendable segments.
- Using `IntegerIndexParams` with both flags false: enable lookup, range, or
  both.
- Requiring a lower bound on `HnswIndexConfig.M`: zero passes client-side;
  only the upper bound is enforced.
- Changing vector size on reload and expecting a migration: the engine
  rejects it; create a new directory instead.

Cross-links: [`writes.md`](writes.md), [`lifecycle.md`](lifecycle.md), [`snapshots-tuning.md`](snapshots-tuning.md).

---

Back to [documentation index](README.md).
