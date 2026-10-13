# AGENTS

## Commands

- Build: `dotnet build *.sln -c Release` (or Debug). Verify with `--no-incremental` for 0 warnings.
- Run tests: `dotnet test <TestProject>` — use `--filter` for focused tests.
- Log test results in full: NEVER summarize a test run through `tail -N` (a truncated tail can drop the failing test's name/exception and makes later diagnosis impossible). Pass `--logger "trx;LogFileName=..."` (and/or `--logger "console;verbosity=normal"`) and keep the full log; when output must be previewed, grep for `Failed|error` on the full log file instead of tailing it. Every reported result must include the exact failing test names and assertion messages.

## Conventions

- Give tests standalone names that describe the feature under test.
- Batch the work, then test the batch: when working a list of items, implement 5–6 fixes (code + regression pins) before building and running their targeted suites — do NOT build/test after every single change. Full-suite verification still happens once at the end of the run, not per item.
- All new and rewritten code must follow [Best practices (C# 14)](#best-practices-c-14) below (per-feature details in [csharp.md](./csharp.md)). Modernize surrounding lines you touch; do not reformat untouched code.

## Hard rules

- Tests: only mock when absolutely necessary. Prefer exercising real paths over stub-only tests. Cover each feature from many angles: happy path, edge cases, error/permission branches, and integration. Avoid `Mock`-only unit tests that don't touch actual logic.
- Tests: no mirror assertions. NEVER assert a literal that appears verbatim in the production code under test: it parrots the source, fails only when someone edits both, and proves nothing. Pin the observable behavior instead. One behavior per test — don't bundle unrelated assertions into a single combined name.
- ALWAYS use a timeout for every shell command to avoid hangs. Set an explicit timeout on every call (e.g. `timeout 15 dotnet build`, `curl --max-time 5`), with a longer timeout for full-suite `dotnet test -c Release`. Never run unbounded `bash`/`dotnet`/`curl`/`nc`/`ss`/`lsof`/`pkill` without a timeout.
- Debugging through the browser (interactive inspection, screenshots, dev-server loops) is a last resort. Diagnose from the code first, pin with tests, and only reach for a live browser when code-level evidence cannot settle it.
- This is a cross-platform project: production code must build and run on Windows, macOS, and Linux. Where behavior differs by OS, prefer managed APIs with `OperatingSystem.Is...()` guards plus a working fallback.
- Fix production code, not tests — except stale tests. If a test fails, first assume the production code is broken and do not edit the test to make it pass. But a test that asserts an OLD structural shape after an approved refactor intentionally moved that code into a shared helper — with behavior preserved and pinned by new tests — is itself broken: update it to assert the preserved behavior at its new location. Never weaken or delete a behavioral assertion to hide a regression.
- Keep test code separate from production code. NEVER add test-only hacks (e.g. `time.sleep`, `Thread.Sleep`, artificial delays, polling loops, `if "test" in name` branches) to production code to make tests pass. Fix tests to handle async timing and keep production paths delay-free.
- NEVER use reflection in production code (`System.Reflection`: `GetType`/`GetProperty`/`GetField`/`GetMethod`/`Activator.CreateInstance`/`Assembly.GetTypes`/`dynamic` where it hides reflection). Reflection is freely permitted inside `tests/` (fixtures/assertions) — the ban applies to production code only. Production must use explicit typed properties/fields, `interface`/`abstract` overrides, `switch`/`is` pattern matching, and source-generated or code-gen explicit lists rather than `Assembly` scans. If a code path needs dynamic attribute access, add a real property/method to the target type instead of reflecting. If there is a valid use of reflection which would be the best way to accomplish something, ask the user for permission first.
- NEVER show the user menus / question-tool prompts. Present information and recommendations as plain text and let the user reply in free text.
- NEVER reference yourself in code or test comments — no "my fix", "I changed", diary entries, or decision logs. Comments explain what the code does and why it must be that way (technical reason), never who ordered it or when. If a decision needs provenance, it belongs in the commit message, not the source.
- NEVER create or delete folders in the home folder (`~`, `/home/anon/`). No `mkdir ~/...`, no `rm -rf ~/...`, no tar/extract into `~`. Scratch space lives under `/tmp/` — the default; the repo workdir only if `/tmp` is full. Scratch you create for a task is evidence, not litter: keep it until the user says the task is finished, because a harness, transcript or scratch database is often the only thing that lets anyone re-check a claim. Deleting it at the end of a run is not tidiness, it is destroying the work.
- If the user's command doesn't make sense, challenge them on it. Do not blindly execute instructions that contradict the repo's own rules, established precedents, or plain technical reality — push back with evidence (file paths, test results) and ask for clarification.
- No invented AI bullshit jargon: method/variable names, log messages, and comments must use plain words a cold reader understands. A name says what the thing does (`RestoreRoomContents`, never `RebuildContainment`); a log line says what happened and to what (`could not return object 734 to its room`, never `seat`). If a name needs explaining, rename it instead of documenting it.

## Best practices (C# 14)

Target the latest stable language: `<LangVersion>latest</LangVersion>` (C# 14 on .NET 10 SDK).
For what each feature is and how to use it, see **[csharp.md](./csharp.md)** — the version-by-version
reference (C# 9 → C# 14) with examples. This file states which practices to apply when writing code.

## Nullability and validation

- Keep `<Nullable>enable</Nullable>` on. Never silence nullability with `!` to work around a design
  problem — fix the types or add a real guard instead. `!` is acceptable only for cases the compiler
  cannot see (e.g. initialized via reflection/serialization), with a comment.
- Validate public API arguments with `ArgumentNullException.ThrowIfNull`,
  `ArgumentOutOfRangeException.ThrowIf...`, `ArgumentException.ThrowIfNullOrEmpty/WhiteSpace`.
- Prefer `required` members or constructor parameters over settable-then-validated state.
- Use `is null` / `is not null` instead of `== null` (avoids overloaded-operator surprises).

## Types and object design

- Model immutable data as `record` / `readonly record struct` (value equality, `with` support).
  Use a `class` when identity matters or the type has significant behavior/lifetime.
- Prefer `init` accessors for construction-time-only state; `required` for mandatory members.
- Use primary constructors for simple dependency capture (services, DTOs). If the constructor needs
  validation or logic, write an explicit constructor body instead of clever parameter tricks.
- One type per file; file name matches type name. File-scoped namespaces; `global using` for
  ubiquitous imports only.

## Properties

- Use the `field` keyword for validated/transformed/lazy properties instead of hand-written backing
  fields (see csharp.md — C# 14 `field`-backed properties).
- Expression-bodied members for single-expression getters/methods.
- No redundant backing fields, no `GetX()`/`SetX()` method pairs where a property fits.

## Control flow and expressions

- Prefer `switch` expressions and pattern matching (`and`/`or`/`not`, relational, list patterns)
  over `if`/`else` chains and manual type tests + casts.
- Use target-typed `new()`, collection expressions (`[..]` with spread), and null-coalescing /
  null-conditional assignment (`?.`, `customer?.Order = ...`) to cut ceremony.
- Use raw string literals (`"""`) for embedded JSON/SQL/regex/XML; UTF-8 literals (`"..."u8`) for
  byte-oriented protocols.
- Prefer `foreach` + LINQ over hand-rolled index loops for in-memory collections; prefer
  `for` over `foreach` only in measured hot paths.

## Strings, collections, spans

- String building: interpolation with handlers over `string.Format`/`+` in loops; `StringBuilder`
  for loops; `const` interpolation for compile-time constants.
- Accept the most general usable collection parameter type; offer `params ReadOnlySpan<T>` /
  `params IEnumerable<T>` overloads for allocation-free variadic APIs.
- Use `Span<T>` / `ReadOnlySpan<T>` for parsing and buffer manipulation (first-class conversions in
  C# 14 make them cheap to pass around). Use `stackalloc` for small short-lived buffers.
- `ref struct` only when pinning stack-only semantics; remember it can't box to interfaces —
  expose functionality via `allows ref struct` generics where needed.

## Concurrency

- `async`/`await` end-to-end; never block on async code (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`
  outside `Main`). Always flow a `CancellationToken`.
- Prefer `ValueTask` over `Task` only for genuinely hot, often-synchronous paths.
- Synchronize with `System.Threading.Lock` + `lock` statement — never `lock (this)`, strings, or `Type`s.
- Prefer immutable messages / channels (`System.Threading.Channels`) over shared mutable state.
- Dispose with `using` declarations; implement `IAsyncDisposable` when teardown is async.

## Extension and generated code

- New `extension(...)` blocks (C# 14) for extension properties and static extensions; classic
  `this`-parameter syntax remains fine for plain extension methods. Don't use extensions to fake
  missing encapsulation — if it needs privates, it belongs in the type.
- Source-generated code (regex, JSON, logging, interceptors): prefer `[GeneratedRegex]`,
  `JsonSerializerContext`, `LoggerMessage.Define` over runtime reflection/emitted code.

## Errors and diagnostics

- Throw specific exception types with actionable messages; include parameter names via `nameof`.
  Don't catch `Exception` except at a true boundary (log + translate there).
- Use `[Experimental("ID")]` for in-progress APIs instead of ad-hoc "DO NOT USE" comments.
- Annotate with `[MemberNotNull]`, `[NotNullWhen]`, `[DoesNotReturn]` etc. where contracts help analysis.

## Quality gates

- Treat warnings as errors in CI (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`) with a modern
  `<AnalysisLevel>`; enable relevant warning waves. No `#pragma` suppressions without a justification comment.
- Public APIs get XML doc comments. Tests live next to the code they cover; name tests
  `MethodUnderTest_Scenario_ExpectedResult`.
- Follow .NET naming conventions (PascalCase types/members, camelCase locals/params, `_camelCase`
  private fields only when a hand-written backing field is genuinely needed).
