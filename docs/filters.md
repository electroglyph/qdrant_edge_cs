# Filters

Purpose: `Filter` boolean structure, all 8 `Condition` kinds (including both
nested forms), the one-predicate `FieldCondition` rule, all 8 `Match` kinds,
and range/values-count/slice guards.

Prerequisites: 4-dim `Distance.Dot` shard. Payload examples assume points
carry `color`, `price`, and `city` fields.

## Concepts

- `Filter` holds `Must`, `Should`, `MustNot` (each a condition list) plus one
  `MinShould { Conditions, MinCount }`.
- `Condition` has 8 kinds via factories: `Field`, `IsEmpty`, `IsNull`,
  `HasId`, `HasVector`, `Slice`, `Nested(key, filter)`, `NestedFilter(filter)`.
  `Nested` and `NestedFilter` differ only in whether a payload key scopes the
  nested filter.
- `FieldCondition` holds exactly one predicate (`Match`, `Range`,
  `DatetimeRange`, `GeoBoundingBox`, `GeoRadius`, `GeoPolygon`,
  `ValuesCount`). `Range` + `DatetimeRange` share one slot and are mutually
  exclusive. To AND several predicates on one key, add one condition per
  predicate to `Must`.
- `Match` is one enum with 8 factories: `CreateValue`, `CreateText`,
  `CreateTextAny`, `CreatePhrase`, `CreatePrefix`, `CreateSubstring`,
  `CreateAny`, `CreateExcept`. `ValueVariants` offers `String`/`Integer`/
  `Bool`; `AnyVariants` offers `Strings`/`Integers` (homogeneous lists; empty
  `Any` is valid).
- `RangeFloat` and `ValuesCount` bounds are all optional. `RangeDatetime`
  bounds are RFC3339 strings (see `geo-datetime.md`).
- `Slice(total, index)` requires `total > 0` and `index < total`.
- Nesting depth is capped at 64; leaves have depth 0, each
  `Nested`/`Filter` wrapper adds 1.

## Must / MustNot / Should / MinShould nesting

```csharp
Filter filter = new()
{
    Must =
    [
        Condition.Field(new FieldCondition
        {
            Key = "price",
            Range = new RangeFloat { Gte = 10, Lt = 100 },
        }),
    ],
    MustNot = [Condition.IsNull("deleted")],
    Should = [Condition.HasId([PointId.FromNumber(7)])],
    MinShould = new MinShould
    {
        Conditions = [Condition.HasVector("image")],
        MinCount = 1,
    },
};
Console.WriteLine($"Filtered count: {shard.Count(new CountRequest { Filter = filter, Exact = true })}");
```

Every branch is validated (`Depth()` pre-pass, then each condition).
`MinCount: 0` passes validation untouched.

## Each Condition kind (including both nested forms) and HasId/HasVector/Slice

```csharp
Condition field = Condition.Field(new FieldCondition
{
    Key = "color",
    Match = Match.CreateValue(ValueVariants.String("red")),
});
Condition isEmpty = Condition.IsEmpty("color");
Condition isNull = Condition.IsNull("deleted");
Condition hasId = Condition.HasId([PointId.FromNumber(1)]);
Condition hasVector = Condition.HasVector("image");
Condition slice = Condition.Slice(2, 1);

FieldConditionClause fieldRecord = new(new FieldCondition
{
    Key = "color",
    Match = Match.CreateValue(ValueVariants.String("red")),
});
IsEmptyCondition isEmptyRecord = new("color");
HasIdCondition hasIdRecord = new([PointId.FromNumber(1)]);
SliceCondition sliceRecord = new(2, 1);

Condition nested = Condition.Nested("arrays", new Filter { Must = [field] });
Condition nestedFilter = Condition.NestedFilter(new Filter { Must = [field] });

foreach (Condition each in (Condition[])[field, isEmpty, isNull, hasId, hasVector, slice, nested, nestedFilter])
{
    Filter probe = new() { Must = [each] };
    Console.WriteLine($"{each.GetType().Name}: {shard.Count(new CountRequest { Filter = probe })}");
}
```

`HasId([])` passes validation (engine decides); `Slice(0, 0)` and
`Slice(2, 2)` throw `EdgeInvalidArgumentException`.

## One predicate per FieldCondition, and the AND-same-key pattern

```csharp
FieldCondition priceBand = new() { Key = "price", Range = new RangeFloat { Gte = 10, Lt = 100 } };
priceBand.Validate();

Filter andSameKey = new()
{
    Must =
    [
        Condition.Field(new FieldCondition { Key = "price", Range = new RangeFloat { Gte = 10 } }),
        Condition.Field(new FieldCondition { Key = "price", Range = new RangeFloat { Lt = 100 } }),
    ],
};
Console.WriteLine($"AND-same-key count: {shard.Count(new CountRequest { Filter = andSameKey })}");

try
{
    new FieldCondition
    {
        Key = "price",
        Range = new RangeFloat { Gte = 1 },
        DatetimeRange = new RangeDatetime { Gte = "2024-01-01T00:00:00Z" },
    }.Validate();
}
catch (EdgeInvalidArgumentException ex)
{
    Console.WriteLine($"Range+DatetimeRange rejected: {ex.Reason}");
}
```

Zero predicates throws; two or more throws with the "exactly one predicate"
message. `Range` alone and `DatetimeRange` alone each count as one.

## Each Match kind, AnyVariants, and empty-Any

```csharp
Match byValue = Match.CreateValue(ValueVariants.String("red"));
Match byInt = Match.CreateValue(ValueVariants.Integer(42));
Match byBool = Match.CreateValue(ValueVariants.Bool(true));
Match byText = Match.CreateText("hello world");
Match byTextAny = Match.CreateTextAny("hello world");
Match byPhrase = Match.CreatePhrase("hello world");
Match byPrefix = Match.CreatePrefix("hel");
Match bySubstring = Match.CreateSubstring("ell");
Match byAny = Match.CreateAny(AnyVariants.Strings(["red", "blue"]));
Match byAnyInt = Match.CreateAny(AnyVariants.Integers([1, 2]));
Match byExcept = Match.CreateExcept(AnyVariants.Strings(["green"]));

ValueMatch valueRecord = new(ValueVariants.String("red"));
AnyMatch anyRecord = new(AnyVariants.Strings(["red"]));
ExceptMatch exceptRecord = new(AnyVariants.Strings(["green"]));

Match emptyAnyStrings = Match.CreateAny(AnyVariants.Strings([]));
Match emptyAnyInts = Match.CreateAny(AnyVariants.Integers([]));
Console.WriteLine($"Empty Any lists construct: {emptyAnyStrings is not null && emptyAnyInts is not null}");
```

`ExceptMatch.Except` keeps the C# name (`except_` in Python). Any/Except
lists must be homogeneous (`Strings` or `Integers`); empty lists are valid.

## RangeFloat and ValuesCount bounds

```csharp
FieldCondition rangeAll = new()
{
    Key = "price",
    Range = new RangeFloat { Gte = 1, Gt = 0, Lte = 10, Lt = 11 },
};
FieldCondition rangeOpen = new() { Key = "price", Range = new RangeFloat() };
FieldCondition countBand = new()
{
    Key = "tags",
    ValuesCount = new ValuesCount { Gte = 1, Lt = 5 },
};
foreach (FieldCondition each in (FieldCondition[])[rangeAll, rangeOpen, countBand])
{
    each.Validate();
}
Console.WriteLine("Range and ValuesCount bounds pass through unvalidated.");
```

No cross-bound check (`Gte > Lt` passes client-side); the engine decides.

## Common mistakes

- Setting two predicates on one `FieldCondition` (for example `Match` plus
  `Range`): split into two `Must` entries instead.
- Using `except_` (Python spelling): C# is `ExceptMatch.Except`.
- Calling `Condition.Slice(0, 0)`: `total` must be > 0 and `index` < total.
- Expecting `HasId` with an empty list to throw: it passes validation.

Cross-links: `reads.md`, `queries.md`, `geo-datetime.md`,
`errors-limits.md`.
