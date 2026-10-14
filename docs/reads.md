# Reads

Purpose: the read surface — `Query` (preferred) vs `Search` (deprecated),
batch, groups, scroll, retrieve, count, facet, info — plus payload/vector
projection and score/limit handling.

Prerequisites: staged native library and a 4-dim `Distance.Dot` shard with a
few points (see [`getting-started.md`](getting-started.md)). Facet needs an indexed key.

## Concepts

- Prefer `Query`; `Search` is the deprecated vector-only path kept for parity.
- `QueryBatch` runs many queries in one pass and preserves input order; it is
  cheaper than N x `Query`.
- `Scroll` pages with `Offset`/`Limit`; follow `NextOffset` until null.
  `Limit` is nullable (null means engine default); ordered scroll uses
  `OrderBy`.
- `Retrieve` takes ids directly and omits missing ids (no error).
- `Count` defaults `Exact = true`; `Facet` defaults `Limit = 10`,
  `Exact = false`. Facet requires an indexed key (`CreateFieldIndex` first).
- `WithPayload` (`Bool` / `CreateFields` / `Exclude`) and `WithVector`
  (`Bool` / `CreateNames`) gate what comes back. Queries default both off;
  retrieve defaults payload on at the engine layer (pass an explicit selector
  to pin behavior).
- `ScoredPoint.Payload`/`ScoredPoint.Vector` and `Record.Payload`/
  `Record.Vector` are JSON strings when requested; parse them with
  `JsonNode`/`JsonDocument`. `ScoredPoint` adds `Version`/`Score`; `Record`
  has neither.
- `FacetResponse` is a plain object with `Hits`, a `Count` property, and an
  indexer (`facets[0]`).
- `ScoreThreshold` is an optional finite cutoff; result limits are capped at
  1,048,576 ([`errors-limits.md`](errors-limits.md)).

## Filtered ANN with projection and threshold

```csharp
float[] queryVec = [1f, 1f, 1f, 1f];
IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
{
    Limit = 5,
    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense(queryVec))),
    Filter = new Filter
    {
        Must = [Condition.Field(new FieldCondition
        {
            Key = "color",
            Match = Match.CreateValue(ValueVariants.String("red")),
        })],
    },
    WithPayload = WithPayload.CreateFields(["color", "price"]),
    ScoreThreshold = 0.2f,
});
```

The deprecated equivalent keeps the same filter/projection shape:

```csharp
IReadOnlyList<ScoredPoint> legacy = shard.Search(new SearchRequest
{
    Query = Query.Nearest(NamedVector.Dense(queryVec)),
    Limit = 5,
    Filter = new Filter
    {
        Must = [Condition.Field(new FieldCondition
        {
            Key = "color",
            Match = Match.CreateValue(ValueVariants.String("red")),
        })],
    },
});
```

## Projection gating and defaults

```csharp
QueryRequest gated = new()
{
    Limit = 5,
    WithPayload = WithPayload.Bool(true),
    WithVector = WithVector.Bool(false),
};
QueryRequest fieldsOnly = new()
{
    Limit = 5,
    WithPayload = WithPayload.CreateFields(["city"]),
    WithVector = WithVector.CreateNames([""]),
};
QueryRequest excluded = new()
{
    Limit = 5,
    WithPayload = WithPayload.Exclude(["city"]),
};

BoolWithPayload boolPayload = new(true);
FieldsWithPayload fieldsPayload = new(["city"]);
ExcludeWithPayload excludePayload = new(["city"]);
BoolWithVector boolVector = new(true);
NamesWithVector namesVector = new([""]);
```

Both selectors default off for queries when null. On retrieve, leave the
selector null for engine defaults or pin one of the shapes above; one pin per
op (`Query`, `Search`, `Retrieve`, `Scroll`) is enough to prove gating.

## Batch preserves input order

```csharp
QueryRequest first = new() { Limit = 2 };
QueryRequest second = new()
{
    Limit = 2,
    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense(queryVec))),
};
IReadOnlyList<IReadOnlyList<ScoredPoint>> batched =
    shard.QueryBatch(new QueryBatchRequest { Queries = [first, second] });
Console.WriteLine($"Batch order preserved: {batched.Count == 2}");
```

`QueryBatchRequest.Queries` must be non-null; each element is validated in
order. Responses come back in the same order as the inputs.

## Groups with each GroupId variant

```csharp
IReadOnlyList<Group> groups = shard.QueryGroups(new GroupRequest
{
    Query = new QueryRequest { Limit = 20 },
    GroupBy = "color",
    Groups = 5,
    GroupSize = 3,
});
foreach (Group group in groups)
{
    string label = group.Key switch
    {
        StringGroupId(var value) => value,
        UInt64GroupId(var value) => value.ToString(),
        Int64GroupId(var value) => value.ToString(),
        _ => "?",
    };
    Console.WriteLine($"{label}: {group.Hits.Count}");
}
```

`GroupRequest` validates `Groups`/`GroupSize` against the 1,048,576 cap.
`Group.Key` is a `GroupId` union; handle all three record shapes.

## Scroll until NextOffset is null (plus ordered scroll)

```csharp
List<Record> seen = [];
ScrollResponse page = shard.Scroll(new ScrollRequest { Limit = 2 });
seen.AddRange(page.Records);
while (page.NextOffset is not null)
{
    page = shard.Scroll(new ScrollRequest { Limit = 2, Offset = page.NextOffset });
    seen.AddRange(page.Records);
}
Console.WriteLine($"Scrolled records: {seen.Count}");

ScrollResponse ordered = shard.Scroll(new ScrollRequest
{
    Limit = 2,
    OrderBy = new OrderBy { Key = "price", Direction = Direction.Asc },
});
Console.WriteLine($"Ordered page: {ordered.Records.Count}");
```

`ScrollRequest.Limit` is nullable; null uses the engine default. With 5
points and 2 per page the loop takes exactly 3 pages and the union is the
full set. `Scroll` does not validate `OrderBy` client-side; an invalid key
fails natively.

## Retrieve omits missing ids; count honors Exact

```csharp
IReadOnlyList<Record> found = shard.Retrieve(new RetrieveRequest
{
    PointIds = [PointId.FromNumber(1), PointId.FromNumber(999)],
});
Console.WriteLine($"Found (missing omitted): {found.Count}");

ulong exact = shard.Count(new CountRequest { Exact = true });
ulong approximate = shard.Count(new CountRequest { Exact = false });
Console.WriteLine($"Exact={exact} approximate={approximate}");
```

`RetrieveRequest.PointIds` must be non-null; empty and duplicate lists pass
validation. `CountRequest.Exact` defaults to true; the filter (when present)
is validated.

## Facet needs an indexed key (plus filtered facet and Exact)

```csharp
shard.Update(UpdateOperation.CreateFieldIndex("color", PayloadSchemaType.Keyword));
FacetResponse facets = shard.Facet(new FacetRequest { Key = "color", Limit = 10 });
foreach (FacetHit hit in facets.Hits)
{
    Console.WriteLine($"{hit.Value}: {hit.Count}");
}
Console.WriteLine($"Count={facets.Count} first={facets[0].Value}");

FacetResponse filtered = shard.Facet(new FacetRequest
{
    Key = "city",
    Limit = 10,
    Exact = true,
    Filter = new Filter
    {
        Must = [Condition.Field(new FieldCondition
        {
            Key = "color",
            Match = Match.CreateValue(ValueVariants.String("red")),
        })],
    },
});
Console.WriteLine($"Filtered facet hits: {filtered.Hits.Count}");
```

Without the keyword index the facet path cannot aggregate. `FacetRequest.Key`
must be non-empty; `Limit` is capped at 1,048,576.

## Info readback and JSON-string payload parsing

```csharp
ShardInfo info = shard.GetInfo();
Console.WriteLine($"Points={info.PointsCount} segments={info.SegmentsCount}");
foreach ((string key, PayloadIndexInfo index) in info.PayloadSchema)
{
    Console.WriteLine($"{key}: {index.DataType} points={index.Points}");
}

IReadOnlyList<ScoredPoint> withPayload = shard.Query(new QueryRequest
{
    Limit = 1,
    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense(queryVec))),
    WithPayload = WithPayload.Bool(true),
    WithVector = WithVector.Bool(true),
});
foreach (ScoredPoint hit in withPayload)
{
    if (hit.Payload is not null)
    {
        using System.Text.Json.JsonDocument payload = System.Text.Json.JsonDocument.Parse(hit.Payload);
        Console.WriteLine($"Payload keys: {string.Join(",", payload.RootElement.EnumerateObject().Select(p => p.Name))}");
    }
    if (hit.Vector is not null)
    {
        Console.WriteLine($"Vector JSON length: {hit.Vector.Length}");
    }
    if (hit.OrderValue is IntOrderValue(var intKey))
    {
        Console.WriteLine($"Int order key: {intKey}");
    }
    if (hit.OrderValue is FloatOrderValue(var floatKey))
    {
        Console.WriteLine($"Float order key: {floatKey}");
    }
}
```

`PayloadIndexInfo` carries `DataType`, optional `Params`, and `Points`.
`OrderValue.Int` / `OrderValue.Float` are the two order-key shapes.

## Common mistakes

- Expecting `Retrieve` to throw on a missing id: it omits them; check
  `found.Count` instead.
- Forgetting `CreateFieldIndex` before `Facet`: facet needs the index.
- Assuming `WithPayload` defaults on for `Query`: queries default both
  selectors off; only retrieve defaults payload on.
- Parsing `ScoredPoint.Payload` as an object without a null check: it is
  null unless requested via `WithPayload`.

Cross-links: [`writes.md`](writes.md), [`queries.md`](queries.md), [`filters.md`](filters.md), [`vectors-config.md`](vectors-config.md).

---

Back to [documentation index](README.md).
