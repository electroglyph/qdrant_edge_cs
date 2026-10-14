# Geo and datetime

Purpose: geo predicates, geo/datetime expressions, datetime ranges, and
datetime ordering. General filter structure lives in [`filters.md`](filters.md); formula
wrapping lives in [`queries.md`](queries.md).

Prerequisites: 4-dim `Distance.Dot` shard. Geo points assume a `location`
payload field holding `{ "lon": ..., "lat": ... }`; datetime examples assume
a `created` field holding RFC3339 strings.

## Concepts

- `GeoPoint` requires `Lon` in [-180, 180] and `Lat` in [-90, 90], both
  finite. Guards run in `Validate()` via `EdgeValidation.CheckGeoPoint`.
- `GeoBoundingBox` holds `TopLeft` + `BottomRight` corners (no `Validate` of
  its own; `FieldCondition.Validate` validates each corner, so a null corner
  throws `NullReferenceException` rather than an argument exception).
- `GeoRadius` holds `Center` + finite `Radius >= 0`.
- `GeoPolygon` holds an `Exterior` ring plus optional `Interiors` holes built
  from `GeoLineString` rings. Every ring needs >= 4 points and must be closed
  (first equals last); each point is validated.
- `Expression.GeoDistance(origin, key)` orders by distance from origin; the
  key holds the target point. Argument order is origin first, key second.
- `Expression.Datetime(value)` is an infallible RFC3339 literal (validated at
  execution, not at the factory); `Expression.DatetimeKey(key)` reads a
  payload datetime field.
- `RangeDatetime` bounds (`Gte`/`Gt`/`Lte`/`Lt`) are RFC3339 strings with no
  client-side ordering check.
- `DatetimeStartFrom(string)` positions an `OrderBy` at a timestamp; ordered
  hits expose `OrderValue` ([`reads.md`](reads.md)).

## Each geo predicate, closed-ring rule, and interior holes

```csharp
GeoPoint origin = new() { Lon = 13.4, Lat = 52.5 };
GeoBoundingBox bbox = new()
{
    TopLeft = new GeoPoint { Lon = 13.0, Lat = 53.0 },
    BottomRight = new GeoPoint { Lon = 14.0, Lat = 52.0 },
};
GeoRadius radius = new() { Center = origin, Radius = 1000.0 };
GeoLineString ring = new()
{
    Points =
    [
        new GeoPoint { Lon = 0, Lat = 0 },
        new GeoPoint { Lon = 1, Lat = 0 },
        new GeoPoint { Lon = 1, Lat = 1 },
        new GeoPoint { Lon = 0, Lat = 0 },
    ],
};
GeoPolygon square = new() { Exterior = ring };
GeoPolygon withHole = new()
{
    Exterior = ring,
    Interiors =
    [
        new GeoLineString
        {
            Points =
            [
                new GeoPoint { Lon = 0.2, Lat = 0.2 },
                new GeoPoint { Lon = 0.4, Lat = 0.2 },
                new GeoPoint { Lon = 0.4, Lat = 0.4 },
                new GeoPoint { Lon = 0.2, Lat = 0.2 },
            ],
        },
    ],
};

Filter geoFilter = new()
{
    Should =
    [
        Condition.Field(new FieldCondition { Key = "location", GeoBoundingBox = bbox }),
        Condition.Field(new FieldCondition { Key = "location", GeoRadius = radius }),
        Condition.Field(new FieldCondition { Key = "location", GeoPolygon = square }),
        Condition.Field(new FieldCondition { Key = "location", GeoPolygon = withHole }),
    ],
};
Console.WriteLine($"Geo count: {shard.Count(new CountRequest { Filter = geoFilter })}");

try
{
    new GeoPolygon
    {
        Exterior = new GeoLineString
        {
            Points =
            [
                new GeoPoint { Lon = 0, Lat = 0 },
                new GeoPoint { Lon = 1, Lat = 0 },
                new GeoPoint { Lon = 1, Lat = 1 },
            ],
        },
    }.Validate();
}
catch (EdgeInvalidArgumentException ex)
{
    Console.WriteLine($"Too-few-points rejected: {ex.Reason}");
}

try
{
    new GeoPolygon
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
    }.Validate();
}
catch (EdgeInvalidArgumentException ex)
{
    Console.WriteLine($"Unclosed ring rejected: {ex.Reason}");
}
```

Rings close by repeating the first point as the last point. Holes follow the
same closed >= 4-point rule as the exterior.

## DatetimeRange bounds

```csharp
FieldCondition since2024 = new()
{
    Key = "created",
    DatetimeRange = new RangeDatetime { Gte = "2024-01-01T00:00:00Z" },
};
FieldCondition bounded = new()
{
    Key = "created",
    DatetimeRange = new RangeDatetime { Gte = "2024-01-01T00:00:00Z", Lt = "2025-01-01T00:00:00Z" },
};
since2024.Validate();
bounded.Validate();
Filter dtFilter = new() { Must = [Condition.Field(bounded)] };
Console.WriteLine($"Datetime count: {shard.Count(new CountRequest { Filter = dtFilter })}");
```

Bounds pass through unvalidated (no ordering check); the engine parses
RFC3339 at execution.

## GeoDistance and datetime-key ordering

```csharp
Expression distance = Expression.GeoDistance(origin, "location");
Expression datetimeKey = Expression.DatetimeKey("created");
Expression datetimeLiteral = Expression.Datetime("2024-06-01T00:00:00Z");

GeoDistanceExpression distanceRecord = new(origin, "location");
DatetimeKeyExpression datetimeKeyRecord = new("created");
DatetimeExpression datetimeRecord = new("2024-06-01T00:00:00Z");

Console.WriteLine($"kinds: {distance.Kind}, {datetimeKey.Kind}, {datetimeLiteral.Kind}");

IReadOnlyList<ScoredPoint> nearest = shard.Query(new QueryRequest
{
    Limit = 5,
    Query = ScoringQuery.Formula(Expression.Sum([distance, Expression.Constant(0f)])),
});
Console.WriteLine($"GeoDistance formula hits: {nearest.Count}");
```

`Datetime(...)` accepts any non-whitespace string at the factory (for
example `"not-a-date"` constructs fine) and fails at execution with the
shard left usable. `DatetimeKey` validates the key shape only.

## StartFrom datetime in OrderBy

```csharp
OrderBy fromDate = new()
{
    Key = "created",
    Direction = Direction.Asc,
    StartFrom = new DatetimeStartFrom("2024-01-01T00:00:00Z"),
};
DatetimeStartFrom startRecord = new("2024-01-01T00:00:00Z");
ScrollResponse page = shard.Scroll(new ScrollRequest { Limit = 5, OrderBy = fromDate });
Console.WriteLine($"Datetime-ordered page: {page.Records.Count}, start={startRecord.Value}");
```

## Common mistakes

- Swapping `GeoDistance` arguments: origin first, payload key second.
- Leaving a ring open or under 4 points: repeat the first point last; holes
  included.
- Passing `lat=999` or `lon=181`: latitude must stay in [-90, 90],
  longitude in [-180, 180]; both must be finite.
- Passing a negative radius: `Radius` must be >= 0 (zero passes).
- Expecting `Datetime("not-a-date")` to throw at the factory: it is
  infallible by design; the failure surfaces at query execution.

Cross-links: [`filters.md`](filters.md), [`queries.md`](queries.md), [`reads.md`](reads.md).

---

Back to [documentation index](README.md).
