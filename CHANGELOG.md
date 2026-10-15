# Changelog

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
