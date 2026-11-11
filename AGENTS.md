# AGENTS

## Commands

- Build: `dotnet build *.sln -c Release` (or Debug). Verify with `--no-incremental` for 0 warnings.
- Run tests: `dotnet test <TestProject>` — use `--filter` for focused tests.
- Log test results in full: NEVER summarize a test run through `tail -N` (a truncated tail can drop the failing test's name/exception and makes later diagnosis impossible). Pass `--logger "trx;LogFileName=..."` (and/or `--logger "console;verbosity=normal"`) and keep the full log; when output must be previewed, grep for `Failed|error` on the full log file instead of tailing it. Every reported result must include the exact failing test names and assertion messages.

## Conventions

- Give tests standalone names that describe the feature under test.
- Batch the work, then test the batch: when working a list of items, implement 5–6 fixes (code + regression pins) before building and running their targeted suites — do NOT build/test after every single change. Full-suite verification still happens once at the end of the run, not per item.
- Following [csharp.md](./csharp.md) is mandatory for all new and rewritten code. Modernize surrounding lines you touch; do not reformat untouched code.

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
- NEVER run git commands on your own. No `add`, `commit`, `push`, `submodule`, `rm --cached`, or any other git operation unless the user explicitly asked for it. Staging and committing are the user's call.
- NEVER answer with excuses. Answer to the best of your ability: do the work, report the result plainly, and state what is unknown only with evidence — never as a substitute for trying.
- If the user's command doesn't make sense, challenge them on it. Do not blindly execute instructions that contradict the repo's own rules, established precedents, or plain technical reality — push back with evidence (file paths, test results) and ask for clarification.
- No invented AI bullshit jargon: method/variable names, log messages, and comments must use plain words a cold reader understands. A name says what the thing does (`RestoreRoomContents`, never `RebuildContainment`); a log line says what happened and to what (`could not return object 734 to its room`, never `seat`). If a name needs explaining, rename it instead of documenting it.

## C# best practices

[csharp.md](./csharp.md) holds the C# best practices. Following it is mandatory.
