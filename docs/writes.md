# Writes

Purpose: every `UpdateOperation` factory, upsert modes, point/vector/id
shapes, and payload merge-vs-overwrite semantics. For reads see `reads.md`;
for index/field config bounds see `vectors-config.md`.

Prerequisites: staged native library (`getting-started.md`). Examples share a
4-dim `Distance.Dot` shard at a temp dir; each snippet shows its own updates
against that shape unless stated.

## Concepts

- `shard.Update(operation)` validates then dispatches. Factories validate
  eagerly, but the operation `record` constructors are public, so `Update`
  revalidates before dispatch.
- `Point` takes `required Id`, `required Vector`, and an optional
  `JsonObject` payload. `PointVectors` takes `required Id` plus a replacement
  `Vector`. Write payload is always a `JsonObject`; read payload
  (`ScoredPoint.Payload`, `Record.Payload`) comes back as a JSON string that
  must be parsed (see `reads.md`).
- `payloadJson` on the set/overwrite factories must be a JSON-object string
  (for example `"""{"color":"red"}"""`); the client checks null/whitespace,
  the engine checks object shape.
- `""` addresses the default unnamed vector everywhere a vector name appears.

## Point ids, vectors, and payload asymmetry

```csharp
Point byNumber = new()
{
    Id = PointId.FromNumber(1),
    Vector = Vector.Single([1f, 2f, 3f, 4f]),
    Payload = new System.Text.Json.Nodes.JsonObject { ["color"] = "red" },
};
Point byUuid = new()
{
    Id = PointId.FromUuid(Guid.NewGuid()),
    Vector = Vector.Single([1f, 1f, 1f, 1f]),
};
Point byUuidString = new()
{
    Id = PointId.FromString("550e8400-e29b-41d4-a716-446655440000"),
    Vector = Vector.Single([2f, 2f, 2f, 2f]),
};
try
{
    PointId bad = PointId.FromString("not-a-uuid");
}
catch (EdgeInvalidArgumentException ex)
{
    Console.WriteLine($"Bad UUID string rejected: {ex.Reason}");
}

// Named, multi-dense, and sparse point vectors.
Vector named = Vector.Named(new Dictionary<string, NamedVector>
{
    [""] = NamedVector.Dense([1f, 2f, 3f, 4f]),
});
Vector multi = Vector.MultiDense([[1f, 2f], [3f, 4f]]);
Vector sparseNamed = Vector.Named(new Dictionary<string, NamedVector>
{
    ["splade"] = NamedVector.Sparse(new SparseVector { Indices = [3u, 17u], Values = [0.9f, 0.4f] }),
});
try
{
    Vector badSparse = Vector.Named(new Dictionary<string, NamedVector>
    {
        ["splade"] = NamedVector.Sparse(new SparseVector { Indices = [1u, 2u], Values = [0.5f] }),
    });
    badSparse.Validate();
}
catch (EdgeInvalidArgumentException ex)
{
    Console.WriteLine($"Sparse length mismatch rejected: {ex.Reason}");
}
```

`SingleVector`, `MultiDenseVector`, and `NamedVectorMap` are the underlying
records; prefer the `Vector.Single` / `Vector.MultiDense` / `Vector.Named`
factories. `Vector.Named` validates eagerly; `Single`/`MultiDense` validate
at `Update`. Sparse `Indices`/`Values` lengths must match; multi-dense rows
must be non-empty, uniform width, finite, and within 1..=65536 per row.
`PointId.FromString` accepts only UUID strings.

## Upsert (plus condition filter and InsertOnly/UpdateOnly)

```csharp
shard.Update(UpdateOperation.UpsertPoints(
[
    new Point { Id = PointId.FromNumber(1), Vector = Vector.Single([1f, 0f, 0f, 0f]) },
    new Point { Id = PointId.FromNumber(2), Vector = Vector.Single([0f, 1f, 0f, 0f]) },
]));

Filter onlyOdd = new()
{
    Must = [Condition.HasId([PointId.FromNumber(1)])],
};
shard.Update(UpdateOperation.UpsertPoints(
[
    new Point { Id = PointId.FromNumber(1), Vector = Vector.Single([9f, 9f, 9f, 9f]) },
], condition: onlyOdd, updateMode: UpdateMode.Upsert));

shard.Update(UpdateOperation.UpsertPoints(
[
    new Point { Id = PointId.FromNumber(9), Vector = Vector.Single([1f, 1f, 1f, 1f]) },
], updateMode: UpdateMode.InsertOnly));

shard.Update(UpdateOperation.UpsertPoints(
[
    new Point { Id = PointId.FromNumber(1), Vector = Vector.Single([2f, 2f, 2f, 2f]) },
], updateMode: UpdateMode.UpdateOnly));
```

`UpsertPointsOperation` carries `Points`, `Condition`, and `UpdateMode`.
`Upsert` replaces or inserts; `InsertOnly` skips existing points;
`UpdateOnly` skips missing points.

## Replace vectors (UpdateVectors with PointVectors plus condition)

```csharp
shard.Update(UpdateOperation.UpdateVectors(
[
    new PointVectors { Id = PointId.FromNumber(1), Vector = Vector.Single([5f, 5f, 5f, 5f]) },
]));
shard.Update(UpdateOperation.UpdateVectors(
[
    new PointVectors { Id = PointId.FromNumber(2), Vector = Vector.Single([6f, 6f, 6f, 6f]) },
], condition: onlyOdd));
```

`UpdateVectorsOperation` carries `PointVectors` plus an optional `Condition`.

## Delete points and vectors

```csharp
shard.Update(UpdateOperation.DeletePoints([PointId.FromNumber(2)]));
shard.Update(UpdateOperation.DeletePointsByFilter(onlyOdd));
shard.Update(UpdateOperation.DeleteVectors([PointId.FromNumber(1)], ["image"]));
shard.Update(UpdateOperation.DeleteVectorsByFilter(onlyOdd, ["image"]));
```

`DeletePointsOperation` takes ids; `DeletePointsByFilterOperation` takes a
filter; `DeleteVectorsOperation` takes ids plus vector names;
`DeleteVectorsByFilterOperation` takes a filter plus vector names.

## Payload: merge (set) vs replace (overwrite) vs key-scoped set

```csharp
shard.Update(UpdateOperation.SetPayload([PointId.FromNumber(1)], """{"color":"red","price":10}"""));
shard.Update(UpdateOperation.SetPayload([PointId.FromNumber(1)], """{"red":true}""", key: "flags"));
shard.Update(UpdateOperation.SetPayloadByFilter(onlyOdd, """{"color":"blue"}"""));
shard.Update(UpdateOperation.OverwritePayload([PointId.FromNumber(1)], """{"color":"green"}"""));
shard.Update(UpdateOperation.OverwritePayloadByFilter(onlyOdd, """{"color":"yellow"}"""));
```

`SetPayloadOperation` merges the object into existing payload; the optional
`Key` scopes the merge under one key. `OverwritePayloadOperation` replaces
payload wholesale. The `*ByFilter` records carry a `Filter` instead of ids.

## Delete keys and clear payload

```csharp
shard.Update(UpdateOperation.DeletePayload([PointId.FromNumber(1)], ["color"]));
shard.Update(UpdateOperation.DeletePayloadByFilter(onlyOdd, ["color", "price"]));
shard.Update(UpdateOperation.ClearPayload([PointId.FromNumber(1)]));
shard.Update(UpdateOperation.ClearPayloadByFilter(onlyOdd));
```

`DeletePayloadOperation` removes listed keys; `ClearPayloadOperation` removes
all payload. Filter variants match by filter instead of id list.

## Field indexes: schema form vs params form, then delete

```csharp
shard.Update(UpdateOperation.CreateFieldIndex("color", PayloadSchemaType.Keyword));
shard.Update(UpdateOperation.CreateFieldIndexWithParams(
    "price", PayloadIndexParams.Integer(new IntegerIndexParams { Lookup = true, Range = true })));
shard.Update(UpdateOperation.DeleteFieldIndex("color"));
```

Keep the split: `CreateFieldIndexOperation` takes a `PayloadSchemaType`;
`CreateFieldIndexWithParamsOperation` takes a `PayloadIndexParams` union
(`PayloadIndexParams.Keyword`, `.Integer`, `.Float`, `.Geo`, `.Text`,
`.Bool`, `.Datetime`, `.Uuid`). Python merges both into one union; C# keeps
the FFI split. `IntegerIndexParams` with `Lookup: false, Range: false` is
rejected. Facet and filtered reads need the index first (see `reads.md`).

## Vector fields: create dense, create sparse, delete name

```csharp
shard.Update(UpdateOperation.CreateDenseVector("image", 128, Distance.Cosine));
shard.Update(UpdateOperation.CreateSparseVector("splade", Modifier.Idf));
shard.Update(UpdateOperation.DeleteVectorName("legacy"));
```

`CreateDenseVectorOperation` validates size 1..=65536 eagerly.
`CreateSparseVectorOperation` and `DeleteVectorNameOperation` are infallible
factories (null names still throw `ArgumentNullException`).

## Common mistakes

- Passing a JSON array or scalar as `payloadJson`: it must be a JSON-object
  string; the engine rejects non-objects with `EdgeInvalidArgumentException`.
- Using `new UpsertPointsOperation(...)` with a bad vector and expecting the
  factory guard: the record constructor skips factory validation, but
  `Update` revalidates and throws `EdgeInvalidArgumentException`.
- Creating a `Match` filter on `color` before `CreateFieldIndex`: the write
  succeeds but filtered query/facet falls back or fails; create the keyword
  index first.
- Setting both `Range` and `DatetimeRange` on one `FieldCondition`: they
  share one predicate slot and are mutually exclusive (see `filters.md`).

Cross-links: `reads.md`, `filters.md`, `vectors-config.md`,
`errors-limits.md`.
