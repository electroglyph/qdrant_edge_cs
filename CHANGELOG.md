# Changelog

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
