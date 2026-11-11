# Changelog

## 0.12.0

Managed binding targets .NET 11 only (`net11.0`, C# 15 via
`<LangVersion>latest</LangVersion>`); .NET 10 consumers must stay on
`0.11.x`. The native `qdrant-edge-cabi` bridge is unchanged — it rides
the `0.12.0` release line so the binding and the bridge stay in lockstep
(`NativeLoader.ExpectedVersion`, `Cargo.toml`, `Cargo.lock`).

- CI publishes with .NET 11 SDK (`setup-dotnet 11.0.x`); every project
  targets `net11.0` and the solution builds with zero warnings.
- Modernize to current C#: primary-constructor exceptions, constant and
  relational patterns (`is 0`, `is < 0.0`, `is -1`), list patterns
  (`is []`), `!IsFinite` checks, target-typed `new`, a
  `System.Runtime.InteropServices` global using, and interpolated
  `$"{Guid.NewGuid():N}"` temp paths. `closed`/`union` hierarchies were
  evaluated and rejected: the DTO shapes mirror the engine FFI contract
  and the suite pins open-hierarchy fallbacks.
- Remove the dead `EdgeShard.CreateCore` `configJsonRequired` parameter
  (always `true`, unused in the body).
- Docs: TFM/SDK references updated to .NET 11; snippets use the same
  interpolation and collection-expression idioms as the code.

## 0.11.0

Engine bump: `third_party/qdrant` `016542aa` (v1.19.2) → `222b2e44`
(v1.20.0). Upstream `qdrant-edge-ffi` stays `0.8.0`; the C# binding and the
`qdrant-edge-cabi` bridge bump to `0.11.0`.

- Add `EdgeConfig.WalEnabled` (null means enabled). WAL-less shards are
  durable only after `Flush()`; loading with a different value fails.
  Regression pins: WAL-less create/flush/reload round-trip and
  mismatch-on-load failure.
- Add per-element `Nested` to all eight payload index params. The field must
  sit inside the array elements (e.g. `offers[].price` with `nested: "offers"`).
  Invalid paths fail with invalid-argument without aborting the shard.
- Add BM25 text ranking: `TextIndexParams.Scoring` (`TextScoringType.Bm25`)
  plus `ScoringQuery.CreateText(field, query, scoring?)` with
  `TextQueryScoring.Bm25(Bm25Params{K, B})`. `K` must be finite and
  non-negative; `B` must be finite in `[0, 1]`.
- Add `TokenizerType.Code` for identifier splitting.
- Add `VectorStorageDatatype.Turbo8`/`Turbo16` and
  `TurboQuantBitSize.Bits8`.
- Native bridge accepts and echoes the new shapes; unknown datatypes,
  tokenizers, scoring types, and malformed `Nested`/`Bm25Params` fail with
  invalid-argument. `ScoredPoint.location` from the engine is discarded on
  conversion (payload-by-location perf change, no surface change).

## 0.10.1

- Native library lookup probes `runtimes/<rid>/native` beside `Qdrant.Edge.dll`
  before falling back to `AppContext.BaseDirectory`. The base directory is the
  host process's directory, so a library loaded into another host (a plugin)
  previously failed to find its own native engine.

## 0.10.0

- Ship prebuilt native engines for all six RIDs as NuGet `runtimes/` assets, built in CI (`build-native` matrix) — consumers no longer need a Rust toolchain.
- Publish to nuget.org via trusted publishing (`VersionPrefix` bump to release); shared build settings in `Directory.Build.props`.
- ONNX semantic-search examples run on a proper `"text"` named vector field end to end.
- Docs: every cross-page reference is a link, each guide links back to the index, snapshot-recovery semantics corrected to match the C ABI.

## 0.9.0

Validation and lifecycle fixes across the managed binding and the native
C ABI. The underlying engine (third-party Qdrant) is unchanged.

- Fix shard dispose leaking the native handle; flush errors during unload now surface instead of being swallowed.
- Align prefetch depth counting with the native parser so valid 65-deep chains validate.
- Align filter depth counting with the native parser so valid 64-deep filters validate.
- Reject empty and oversized dense vectors at the boundary and in native parsing with invalid-argument.
- Validate every request and the optimizer config on the managed side before calling native.
- Serialize requests with a high-depth JSON context and map serializer failures to invalid-argument.
- Release the handler registry lock before running engine work so shards no longer serialize on one mutex.
- Recount formula size and depth natively instead of trusting client counters.
