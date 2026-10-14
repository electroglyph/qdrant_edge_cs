# Queries

Purpose: `Query` variants, `ScoringQuery` modes, prefetch chains, and the
3-field `SearchParams`. Filter syntax lives in [`filters.md`](filters.md); formula operand
details that touch geo/datetime live in [`geo-datetime.md`](geo-datetime.md).

Prerequisites: 4-dim `Distance.Dot` shard. `queryVec`, `vecA`, and `vecB`
below are 4-dim float arrays.

## Concepts

- `Query` is the vector-side clause: `Nearest`, `Recommend`, `Discover`,
  `CreateContext`, `CreateFeedback`. `Recommend` defaults to
  `RecommendStrategy.BestScore` when strategy is null.
- `ScoringQuery` wraps it for `QueryRequest`: `CreateVector`,
  `CreateFusion`, `CreateOrderBy`, `Formula`, `Mmr` (a variant, not a
  standalone class), `CreateSample`.
- `Fusion.Rrf(k, weights?)` takes an optional per-source weight list;
  `Fusion.Dbsf()` takes none. RRF weights must be finite when present.
- `OrderBy` sorts by a payload key with optional `Direction` and `StartFrom`
  (`IntegerStartFrom(long)`, `FloatStartFrom(double, finite only)`,
  `DatetimeStartFrom(string RFC3339)`). Ordered hits expose `OrderValue`
  (`OrderValue.Int` / `OrderValue.Float`) on readback.
- `MmrScoringQuery` carries `Lambda` in [0, 1] plus `CandidatesLimit` capped
  at 1,048,576. `Sample` has only `Random`.
- `Formula` takes an `Expression` plus optional `Defaults` variables (never
  validated; `$score` is the standard variable).
- `Prefetch` chains nest with `Prefetches`; depth is capped at 64 (base 0).
  `SearchParams` is exactly `{ HnswEf, Exact, IndexedOnly }`; quantization,
  acorn, and idf knobs are Python-only.

## Nearest, Recommend (both strategies + default), Discover, Context, Feedback

```csharp
Query nearest = Query.Nearest(NamedVector.Dense(queryVec));
Query recommendDefault = Query.Recommend([NamedVector.Dense(vecA)], [NamedVector.Dense(vecB)]);
Query recommendBest = Query.Recommend(
    [NamedVector.Dense(vecA)], [NamedVector.Dense(vecB)], RecommendStrategy.BestScore);
Query recommendSum = Query.Recommend(
    [NamedVector.Dense(vecA)], [NamedVector.Dense(vecB)], RecommendStrategy.SumScores);

NearestQuery nearestRecord = new(NamedVector.Dense(queryVec), null);
RecommendQuery recommendRecord = new([NamedVector.Dense(vecA)], [], RecommendStrategy.BestScore, null);

Query discover = Query.Discover(
    NamedVector.Dense(queryVec),
    [new ContextPair { Positive = NamedVector.Dense(vecA), Negative = NamedVector.Dense(vecB) }]);
Query context = Query.CreateContext(
    [new ContextPair { Positive = NamedVector.Dense(vecA), Negative = NamedVector.Dense(vecB) }]);
Query feedback = Query.CreateFeedback(
    NamedVector.Dense(queryVec),
    [new FeedbackItem { Vector = NamedVector.Dense(vecA), Score = 1.0f }],
    new FeedbackCoefficients { A = 1.0f, B = 0.5f, C = 0.25f });

foreach (Query each in (Query[])[nearest, recommendDefault, recommendBest, recommendSum, discover, context, feedback])
{
    IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
    {
        Limit = 3,
        Query = ScoringQuery.CreateVector(each),
    });
    Console.WriteLine($"{each.GetType().Name}: {hits.Count}");
}
```

`Recommend` requires at least one positive; empty negatives pass.
`FeedbackItem.Score` and all three coefficients must be finite.

## Fusion: RRF plain, RRF weighted 3:1, DBSF

```csharp
ScoringQuery rrf = ScoringQuery.CreateFusion(Fusion.Rrf(60));
ScoringQuery weightedRrf = ScoringQuery.CreateFusion(Fusion.Rrf(2, [3.0f, 1.0f]));
ScoringQuery dbsf = ScoringQuery.CreateFusion(Fusion.Dbsf());

RrfFusion rrfRecord = new(60, null);
DbsfFusion dbsfRecord = new();

QueryRequest fusionBase = new()
{
    Limit = 5,
    Query = rrf,
    Prefetches =
    [
        new Prefetch
        {
            Limit = 5,
            Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense(vecA))),
        },
        new Prefetch
        {
            Limit = 5,
            Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense(vecB))),
        },
    ],
};
Console.WriteLine($"Fusion base hits: {shard.Query(fusionBase).Count}");
Console.WriteLine($"Weighted fusion hits: {shard.Query(fusionBase with { Query = weightedRrf }).Count}");
Console.WriteLine($"DBSF hits: {shard.Query(fusionBase with { Query = dbsf }).Count}");
```

Fusion needs prefetches to fuse; the 3:1 weight list biases toward the first
source.

## OrderBy with each StartFrom kind and Direction, plus OrderValue readback

```csharp
OrderBy byInt = new() { Key = "price", Direction = Direction.Asc, StartFrom = new IntegerStartFrom(10) };
OrderBy byFloat = new() { Key = "price", Direction = Direction.Desc, StartFrom = new FloatStartFrom(1.5) };
OrderBy byDatetime = new()
{
    Key = "created",
    Direction = Direction.Asc,
    StartFrom = new DatetimeStartFrom("2024-01-01T00:00:00Z"),
};
foreach (OrderBy order in (OrderBy[])[byInt, byFloat, byDatetime])
{
    IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
    {
        Limit = 5,
        Query = ScoringQuery.CreateOrderBy(order),
    });
    Console.WriteLine($"{order.StartFrom?.GetType().Name}: {hits.Count}");
}
```

Only `FloatStartFrom` is finiteness-checked client-side; integer/datetime
values pass through to the engine. Non-whitespace keys pass client
validation; bad JSON-path keys fail natively.

## MMR (lambda bounds + candidates cap) and Sample

```csharp
ScoringQuery mmr = ScoringQuery.Mmr(NamedVector.Dense(queryVec), 0.5f, 100);
MmrScoringQuery mmrRecord = new(NamedVector.Dense(queryVec), 0.0f, 1_048_576, null);
ScoringQuery sample = ScoringQuery.CreateSample(Sample.Random);

Console.WriteLine($"MMR hits: {shard.Query(new QueryRequest { Limit = 5, Query = mmr }).Count}");
Console.WriteLine($"Sample hits: {shard.Query(new QueryRequest { Limit = 5, Query = sample }).Count}");

try
{
    new MmrScoringQuery(NamedVector.Dense(queryVec), 2.0f, 10, null).ValidateMmr();
}
catch (EdgeInvalidArgumentException ex)
{
    Console.WriteLine($"MMR lambda bound enforced: {ex.Reason}");
}
```

`Lambda` 0 and 1 pass; outside [0, 1] or non-finite throws.
`CandidatesLimit` is capped at 1,048,576.

## Formula: Sum baseline, $score variable, math op, Div fallback, Decay kinds

```csharp
ScoringQuery sumBaseline = ScoringQuery.Formula(
    Expression.Sum([Expression.Variable("$score"), Expression.Constant(1f)]));
ScoringQuery withDefaults = ScoringQuery.Formula(
    Expression.Sum([Expression.Variable("p"), Expression.Constant(1f)]),
    new Dictionary<string, string> { ["p"] = "1" });
ScoringQuery decayLin = ScoringQuery.Formula(
    Expression.Decay(DecayKind.Lin, Expression.Variable("$score"), scale: 1.0f));
ScoringQuery decayGauss = ScoringQuery.Formula(
    Expression.Decay(DecayKind.Gauss, Expression.Variable("$score"), scale: 1.0f));
ScoringQuery decayExp = ScoringQuery.Formula(
    Expression.Decay(DecayKind.Exp, Expression.Variable("$score"), scale: 1.0f));
ScoringQuery divGuarded = ScoringQuery.Formula(
    Expression.Div(Expression.Variable("$score"), Expression.Constant(0f), byZeroDefault: 0f));

ConstantExpression constantRecord = new(1f);
VariableExpression variableRecord = new("$score");

foreach (ScoringQuery formula in (ScoringQuery[])[sumBaseline, withDefaults, decayLin, decayGauss, decayExp, divGuarded])
{
    FormulaScoringQuery asRecord = (FormulaScoringQuery)formula;
    Console.WriteLine($"Formula {asRecord.Expression.Kind}: size={asRecord.Expression.Size}");
}
```

The remaining factories (`Mult`, `Max`, `Min`, `Negate`, `Sqrt`, `Pow`,
`Exp`, `Log10`, `Ln`, `Acosh`, `Abs`, `Condition`, `GeoDistance`,
`Datetime`, `DatetimeKey`) share the same `Formula(...)` wrapper; `Div`
`byZeroDefault` must be finite when set. Formula trees are capped at 10,000
nodes and 64 deep; `Validate()` recomputes size/depth from the actual tree.

## Prefetch chains and SearchParams

```csharp
QueryRequest chained = new()
{
    Limit = 5,
    Prefetches =
    [
        new Prefetch
        {
            Limit = 5,
            Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense(queryVec))),
            Prefetches =
            [
                new Prefetch
                {
                    Limit = 5,
                    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense(vecA))),
                },
            ],
        },
    ],
    Params = new SearchParams { HnswEf = 64, Exact = false, IndexedOnly = false },
};
Console.WriteLine($"Prefetch chain hits: {shard.Query(chained).Count}");
```

A 65-deep chain validates (depths 0..64); 66 fails client-side with
`EdgeInvalidArgumentException`. `SearchParams` carries only the three FFI
fields; `HnswEf` has no client-side range check.

## Common mistakes

- Calling `ScoringQuery.Mmr(...)` and expecting a standalone class: MMR is a
  `ScoringQuery` variant (`MmrScoringQuery` record).
- Using Python names (`RecommendBestScore`, `lambda_`, standalone `Mmr`):
  C# uses `Recommend` + `RecommendStrategy`, `Lambda`, and the variant
  factory (see [`errors-limits.md`](errors-limits.md)).
- Putting quantization/acorn/idf knobs on `SearchParams`: they are
  Python-only; C# accepts only `HnswEf`/`Exact`/`IndexedOnly`.
- Building a formula larger than 10,000 nodes: compose fails fast with
  `EdgeInvalidArgumentException` before any native call.

Cross-links: [`reads.md`](reads.md), [`filters.md`](filters.md), [`geo-datetime.md`](geo-datetime.md),
[`errors-limits.md`](errors-limits.md).

---

Back to [documentation index](README.md).
