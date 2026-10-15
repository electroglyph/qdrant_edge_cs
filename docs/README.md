# Qdrant.Edge documentation (v0.10.0, `net10.0`)

Purpose: task-oriented guides for the `Qdrant.Edge` managed binding with
copy-pasteable C# examples. The XML doc comments on the public API remain the
authoritative reference; these pages explain how the pieces fit together.

Prerequisites: .NET 10 SDK, a local clone with submodules, and one native
`qdrant_edge` library per target RID (see [`getting-started.md`](getting-started.md)). All examples
assume `using Qdrant.Edge;`, a temp directory from `Path.GetTempPath()`, and a
4-dimension `Distance.Dot` fixture unless stated otherwise.

## Reading order

1. [`getting-started.md`](getting-started.md) — install, native build, first shard round-trip.
2. [`lifecycle.md`](lifecycle.md) — create/load/flush/optimize/config/unload/dispose/threading.
3. [`writes.md`](writes.md) — all 20 `UpdateOperation` factories.
4. [`reads.md`](reads.md) — query/search/batch/groups/scroll/retrieve/count/facet/info.
5. [`queries.md`](queries.md) — `Query` and `ScoringQuery` modes, prefetch, `SearchParams`.
6. [`filters.md`](filters.md) — `Filter`, `Condition`, `FieldCondition`, `Match`, ranges.
7. [`geo-datetime.md`](geo-datetime.md) — geo predicates and datetime expressions/ranges.
8. [`vectors-config.md`](vectors-config.md) — `EdgeConfig`, vector families, quantization, payload indexes.
9. [`snapshots-tuning.md`](snapshots-tuning.md) — snapshots, `Optimize`, tuning, `SearchMatrix`.
10. [`errors-limits.md`](errors-limits.md) — exceptions, caps, FFI-vs-Python divergences, v2 gaps.

Version note: managed package and C ABI are both `0.10.0`
(`NativeLoader.ExpectedVersion`, `src/Qdrant.Edge/Qdrant.Edge.csproj`). The
upstream `ffi`/`python` crates are `0.8.0` at the pinned `third_party/qdrant`
submodule commit; resync when `lib/edge/VERSION` lands.

## Primary-page map (every public type has a primary page)

No orphans: each row names the page that owns the runnable example for the type.
Most types have exactly one primary page; a few cross-cutting types list two
(the second page holds bounds or a second shape, with a pointer back).

| Type(s) | Primary page |
|---|---|
| `EdgeShard` (`Create`, `Load`, `Flush`, `Optimize`, `GetConfig`, `SetHnswConfig`, `SetVectorHnswConfig`, `SetOptimizersConfig`, `SnapshotManifest`, `UpdateFromSnapshot`, `UnpackSnapshot`, `Unload`, `Dispose`, `Path`, `IsLoaded`, `Update`, `Search`, `Query`, `QueryBatch`, `QueryGroups`, `Scroll`, `Retrieve`, `Count`, `Facet`, `GetInfo`, `SearchMatrix`, `QueryAsync`, `SearchAsync`) | [`lifecycle.md`](lifecycle.md) for lifecycle/config/snapshot entry points; read/write behavior in [`reads.md`](reads.md), [`writes.md`](writes.md), [`snapshots-tuning.md`](snapshots-tuning.md) |
| `NativeLoader` (`IsAvailable`, `ExpectedVersion`), RID layout | [`getting-started.md`](getting-started.md) |
| `EdgeValidation` (`MaxResultCount`, `MaxQueryNestingDepth`, `MaxFormulaNodes`, `MinVectorSize`, `MaxVectorSize`, `MaxHnswM`, `MinHnswEfConstruct`, `MaxHnswEfConstruct`, `MaxHnswIndexingThreads`) | [`errors-limits.md`](errors-limits.md) (caps table); applied throughout |
| `EdgeException` (`Reason`), `EdgeInvalidArgumentException`, `EdgeOperationException`, `ShardClosedException` | [`errors-limits.md`](errors-limits.md) |
| `EdgeConfig`, `VectorDataConfig`, `SparseVectorDataConfig`, `Distance`, `VectorStorageDatatype`, `MultiVectorConfig`, `MultiVectorComparator`, `Memory`, `Modifier` | [`vectors-config.md`](vectors-config.md) |
| `HnswIndexConfig`, `OptimizersConfig` | [`lifecycle.md`](lifecycle.md) (setters) + [`vectors-config.md`](vectors-config.md) (bounds) |
| `QuantizationConfig` (`Scalar`, `Product`, `Binary`, `Turbo`), `ScalarQuantizationParams`, `ProductQuantizationParams`, `BinaryQuantizationParams`, `TurboQuantizationParams`, `ScalarType`, `CompressionRatio`, `BinaryQuantizationEncoding`, `BinaryQuantizationQueryEncoding`, `TurboQuantBitSize` | [`vectors-config.md`](vectors-config.md) |
| `PayloadSchemaType`, `PayloadIndexParams` (`Keyword`, `Integer`, `Float`, `Geo`, `Text`, `Bool`, `Datetime`, `Uuid`), `KeywordIndexParams`, `IntegerIndexParams`, `FloatIndexParams`, `GeoIndexParams`, `TextIndexParams`, `BoolIndexParams`, `DatetimeIndexParams`, `UuidIndexParams`, `PayloadIndexInfo` | [`vectors-config.md`](vectors-config.md) (params) + [`writes.md`](writes.md) (create/delete index ops) + [`reads.md`](reads.md) (info readback) |
| `TokenizerType`, `Language` (30), `SnowballLanguage` (19), `Stopwords` (`CreateLanguage`, `Set`), `LanguageStopwords`, `SetStopwords`, `Stemmer` (`Snowball`, `Disabled`), `SnowballStemmer`, `DisabledStemmer` | [`vectors-config.md`](vectors-config.md) |
| `Point`, `PointVectors`, `PointId` (`FromNumber`, `FromUuid`, `FromString`), `NumPointId`, `UuidPointId` | [`writes.md`](writes.md) |
| `Vector` (`Single`, `MultiDense`, `Named`), `SingleVector`, `MultiDenseVector`, `NamedVectorMap`, `NamedVector` (`Dense`, `Sparse`, `MultiDense`), `DenseNamedVector`, `SparseNamedVector`, `MultiDenseNamedVector`, `SparseVector` | [`writes.md`](writes.md) + [`vectors-config.md`](vectors-config.md) (family shapes) |
| `UpdateOperation` (all 20 factories) + operation records (`UpsertPointsOperation`, `DeletePointsOperation`, `DeletePointsByFilterOperation`, `UpdateVectorsOperation`, `DeleteVectorsByFilterOperation`, `DeleteVectorsOperation`, `SetPayloadOperation`, `SetPayloadByFilterOperation`, `OverwritePayloadOperation`, `OverwritePayloadByFilterOperation`, `DeletePayloadOperation`, `DeletePayloadByFilterOperation`, `ClearPayloadOperation`, `ClearPayloadByFilterOperation`, `CreateFieldIndexOperation`, `CreateFieldIndexWithParamsOperation`, `DeleteFieldIndexOperation`, `CreateDenseVectorOperation`, `CreateSparseVectorOperation`, `DeleteVectorNameOperation`), `UpdateMode` (`Upsert`, `InsertOnly`, `UpdateOnly`) | [`writes.md`](writes.md) |
| `ScoredPoint`, `Record`, `OrderValue` (`Int`, `Float`), `IntOrderValue`, `FloatOrderValue` | [`reads.md`](reads.md) |
| `WithPayload` (`Bool`, `CreateFields`, `Exclude`), `BoolWithPayload`, `FieldsWithPayload`, `ExcludeWithPayload`, `WithVector` (`Bool`, `CreateNames`), `BoolWithVector`, `NamesWithVector` | [`reads.md`](reads.md) |
| `SearchRequest`, `QueryRequest`, `Prefetch`, `QueryBatchRequest`, `ScrollRequest`, `ScrollResponse`, `RetrieveRequest`, `CountRequest`, `FacetRequest`, `FacetHit`, `FacetResponse`, `GroupRequest`, `Group`, `GroupId` (`StringGroupId`, `UInt64GroupId`, `Int64GroupId`), `ShardInfo`, `SearchMatrixRequest`, `SearchMatrixResponse`, `SearchParams` | [`reads.md`](reads.md) (shapes) + [`queries.md`](queries.md) (prefetch/params) + [`snapshots-tuning.md`](snapshots-tuning.md) (`SearchMatrix`) |
| `Query` (`Nearest`, `Recommend`, `Discover`, `CreateContext`, `CreateFeedback`), `NearestQuery`, `RecommendQuery`, `DiscoverQuery`, `ContextQuery`, `FeedbackQuery`, `RecommendStrategy` (`BestScore`, `SumScores`), `ContextPair`, `FeedbackItem`, `FeedbackCoefficients` | [`queries.md`](queries.md) |
| `ScoringQuery` (`CreateVector`, `CreateFusion`, `CreateOrderBy`, `Formula`, `Mmr`, `CreateSample`), `VectorScoringQuery`, `FusionScoringQuery`, `OrderByScoringQuery`, `FormulaScoringQuery`, `MmrScoringQuery`, `SampleScoringQuery`, `Fusion` (`Rrf`, `Dbsf`), `RrfFusion`, `DbsfFusion`, `Sample` (`Random`), `OrderBy`, `StartFrom` (`IntegerStartFrom`, `FloatStartFrom`, `DatetimeStartFrom`), `Direction` (`Asc`, `Desc`), `DecayKind` (`Lin`, `Gauss`, `Exp`) | [`queries.md`](queries.md) |
| `Expression` (all 20 factories) + expression records (`ConstantExpression`, `VariableExpression`, `ConditionExpression`, `GeoDistanceExpression`, `DatetimeExpression`, `DatetimeKeyExpression`, `MultExpression`, `SumExpression`, `MaxExpression`, `MinExpression`, `NegateExpression`, `DivExpression`, `SqrtExpression`, `PowExpression`, `ExpExpression`, `Log10Expression`, `LnExpression`, `AcoshExpression`, `AbsExpression`, `DecayExpression`) | [`queries.md`](queries.md) (formula) + [`geo-datetime.md`](geo-datetime.md) (`GeoDistance`, `Datetime`, `DatetimeKey`) |
| `Filter` (`Must`, `Should`, `MustNot`, `MinShould`), `MinShould`, `Condition` (`Field`, `IsEmpty`, `IsNull`, `HasId`, `HasVector`, `Slice`, `Nested`, `NestedFilter`), `FieldConditionClause`, `IsEmptyCondition`, `IsNullCondition`, `HasIdCondition`, `HasVectorCondition`, `SliceCondition`, `NestedCondition`, `FilterCondition`, `FieldCondition` | [`filters.md`](filters.md) |
| `Match` (`CreateValue`, `CreateText`, `CreateTextAny`, `CreatePhrase`, `CreatePrefix`, `CreateSubstring`, `CreateAny`, `CreateExcept`), `ValueMatch`, `TextMatch`, `TextAnyMatch`, `PhraseMatch`, `PrefixMatch`, `SubstringMatch`, `AnyMatch`, `ExceptMatch`, `ValueVariants` (`String`, `Integer`, `Bool`), `StringValueVariant`, `IntegerValueVariant`, `BoolValueVariant`, `AnyVariants` (`Strings`, `Integers`), `StringsAnyVariant`, `IntegersAnyVariant` | [`filters.md`](filters.md) |
| `RangeFloat`, `RangeDatetime`, `ValuesCount` | [`filters.md`](filters.md) (ranges) + [`geo-datetime.md`](geo-datetime.md) (`RangeDatetime`) |
| `GeoPoint`, `GeoBoundingBox`, `GeoRadius`, `GeoPolygon`, `GeoLineString` | [`geo-datetime.md`](geo-datetime.md) |

Conventions for every page: Markdown only, `csharp`/`sh`/`text` fences, no
emojis, plain-word naming, `is null` checks, `Path.GetTempPath()` for temp
dirs, one behavior per example, observable behavior (counts/order/page unions)
rather than hand-computed scores. Single-vector examples use the default
unnamed field (`""`); named fields need `Using` on every query — see
[`vectors-config.md`](vectors-config.md).
