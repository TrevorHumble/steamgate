# Issue #0002: Service skeleton, install and uninstall, restart after being killed

**Type:** ready. **Category:** product feature that adds new components (the .NET solution, the
service, the install scripts) and changes DESIGN.md, CLAUDE.md, and three agent specs and deletes a skill
(`skills/visual-confirm.md`), so it is a system-level change.
**Review bar:** system-level change; two independent reviewers, both PASS; reviewer-agent on the
`agents/implementation-agent.md`, `agents/reviewer-doc-currency.md`, and `agents/orchestrator.md` changes against `standards/agent-standards.md`; reviewer-skill on
the removal of `skills/visual-confirm.md` (confirming no remaining file references it); reviewer-documentation
on the DESIGN.md, CLAUDE.md, README.md, and WHAT-IT-CHECKS.md changes; plus the architecture gate at
issue time and the design-philosophy gate at PR time.
**GitHub:** #7.
**Depends on:** 0001.
**Blocks:** 0003, 0004, 0005, 0006, and the status-screen issue (0007 in PLAN.md, not yet filed).
**Touches:**
- Removed (the template's Blender example and its tooling): `addons/`, `tests/` (old contents),
  `evals/`, `tools/render_preview.py`, `tools/retire-example.ps1`,
  `.github/workflows/blender-canary.yml`, `skills/visual-confirm.md`.
- New: `SteamGate.sln`, `global.json`, `Directory.Build.props`, `.config/dotnet-tools.json`,
  `stryker-config.json`, `src/SteamGate.Core/**`, `src/SteamGate.Service/**`,
  `tests/SteamGate.Tests/**`, `install/Install-SteamGate.ps1`, `install/Uninstall-SteamGate.ps1`, `install/SteamGate.Common.ps1`,
  `install/Test-Install.ps1`, `issues/0002-service-skeleton.md`.
- Changed: `.github/workflows/ci.yml`, `.github/workflows/codeql.yml`, `.github/dependabot.yml`,
  `.gitignore`, `DESIGN.md`, `CLAUDE.md`, `README.md`, `WHAT-IT-CHECKS.md`,
  `agents/implementation-agent.md`, `agents/reviewer-doc-currency.md`, `agents/orchestrator.md`.

## User story
As Trevor, who wants a self-imposed limit on Steam gaming, I need SteamGate installed as a Windows
service with no window, tray icon, or Alt-Tab entry, keeping its settings in an admin-only
ProgramData folder and restarted by Windows if killed, so that the later counting and closing
features run all the time without me starting anything, and ending the process in Task Manager does
not switch it off.

## Approved spec this issue carries (DESIGN.md `## SteamGate product spec`, from GitHub #6)
"Service skeleton, install/uninstall, auto-restart recovery" "Windows service in C# / .NET. No
window, tray icon, or Alt-Tab entry. Windows restarts it if killed. Settings and tally in a protected
system folder (ProgramData, admin-only writes)." "Defaults: weeknight 90 min; weekend night 3 h;
weekend nights = Friday + Saturday; weekly cap 10 h; cooldown 48 h." Not-counted list pre-filled with
Blender, OBS Studio, Wallpaper Engine, and Steamworks Shared.
Amended 2026-10-21 (issue 0008): weekend nights are Friday, Saturday, and Sunday.

## Design decisions (orchestrator)
- No .NET SDK was installed on this PC when this issue was drafted (Trevor approved installing SDK
  10.0.402 on 2026-10-20, and it is now present); the implementer must never install one. The install test is verified only by the CI
  `build-test` job. Build, tests, and Stryker may run locally now that SDK 10.0.402 is present; CI
  `build-test` stays the proof of record.
- If no local SDK is present: criteria 1, 1b, 2, 2b, 3, 3b, 3c, 3d, 3e, 3f (xUnit tests) and 4 (the
  install test) cannot be executed before the commit gate. reviewer-pr
  verifies each of them before commit the way `agents/reviewer-pr.md` allows: by tracing the changed
  code on a concrete input to a concrete output and citing `file:line` (for criterion 4, the
  `Test-Install.ps1` lines that print each marker and the script lines they exercise). After the
  push to `main`, the orchestrator watches CI to green under the existing rule in
  `agents/orchestrator.md`; the green `build-test` run is the recorded output of those tests. If it is
  red, the cause is fixed or the commit reverted before anything else proceeds. No new commit path is added. Criterion 11b (green CI on `main`) is checked by the orchestrator's CI watch after the push, not by reviewer-pr.
- `docs/AUTONOMOUS-RUN.md` is a dated run log of the template and is left as history.
- Frameworks: `net10.0` for `SteamGate.Core` (all decision logic and file-store logic; Stryker covers it); `net10.0-windows` for
  `SteamGate.Service` and for `SteamGate.Tests` (so tests can reference both). The service is
  published self-contained, single file, `win-x64`, so the PC needs no .NET runtime.
- Service name `SteamGate`, display name `SteamGate`, LocalSystem, start type automatic.
- The service does not accept the Stop control; it accepts Shutdown so later issues can save state on shutdown.
  Stopping from the Services window is greyed out; removal goes through the uninstall script. This
  is not hiding: it is listed under its real name in the Services window and Task Manager, and an
  admin can still end it (Windows restarts it) or remove it with the script.
- Recovery: `sc.exe failure SteamGate reset= 86400 actions= restart/5000/restart/5000/restart/5000`
  and `sc.exe failureflag SteamGate 1`. Any unhandled exception in the worker is logged and ends the
  process with `Environment.Exit(1)`, because a clean host stop with exit code 0 would not trigger
  the Windows restart.
- Folder `%ProgramData%\SteamGate`: inheritance removed; owner `BUILTIN\Administrators`; SYSTEM and
  Administrators full control; no other identity has any entry (standard users talk to the service
  through the 0005 pipe, never the files, so they cannot lock a file to crash the service). Files: `settings.json`,
  `service.log` (rolled to `service.log.1` past 1 MB).
- `settings.json` (camelCase keys, day names as strings): `weeknightMinutes` 90,
  `weekendNightMinutes` 180, `weekendNights` `["Friday","Saturday","Sunday"]`, `weeklyCapMinutes` 600,
  `cooldownHours` 48, `notCounted` `["Blender","OBS Studio","wallpaper_engine","Steamworks Shared"]`
  (Steam install folder names under `steamapps\common`), `enabled` true. The status screen (0007)
  displays them through `GateSettings.ApprovedNames`; the stored values are Steam folder names used
  only for matching.
- One shared `JsonFileStore<T>` does every JSON load and save. `Load` has three outcomes: file
  missing (`FileNotFoundException`, checked before the general `IOException` path) (return the default and write it, `Corrupt` false); file read but not valid JSON (keep it as
  `<file name without extension>.corrupt-<UTC yyyyMMddTHHmmssfffZ>.json`, return and write the default, `Corrupt` true); `IOException` (retry 3
  times 200 ms apart, then rethrow so the worker's unhandled path restarts the process; never
  overwrite). A file whose JSON is `null`, or that fails to deserialize to the type
  (`JsonException`), counts as not valid JSON. So does a file whose values the type rejects (for settings: a null list item or a negative limit); the move to the corrupt name is retried like reads (3 times 200 ms apart), then the `IOException` is rethrown. Any property absent from the file takes its value from the type's defaults; unknown
  properties are ignored. `Save` and `Load` on one instance hold a private lock so they never
  overlap. `Save` writes `<path>.tmp` in the same folder, flushes it with `Flush(true)`, then
  `File.Move(<path>.tmp, <path>, overwrite: true)`. An `IOException` or `UnauthorizedAccessException` during `Save` is retried 3 times 200 ms
  apart; if it still fails, `Save` deletes `<path>.tmp` if it can, leaves `<path>` unchanged, and
  returns `false`; `Save` of a value the type rejects also returns `false` and leaves the file unchanged (the worker logs `save failed <file>` and saves again on a later tick); `Save` never
  throws either. If the folder does not exist, `Load` and `Save` never create it; the
  `DirectoryNotFoundException` (an `IOException`) takes the `IOException` path. Only the install
  script creates the folder. `SettingsStore` is built on it;
  0004's tally store reuses it.
- The file removals in plan step 1 are run by the orchestrator. Basis: `addons/`, `tests/`, `evals/`,
  and `tools/render_preview.py` are what CLAUDE.md `## First run` step 2 assigns the orchestrator to
  retire with `tools/retire-example.ps1`. The other three leftovers (`.github/workflows/blender-canary.yml`,
  `skills/visual-confirm.md`, `tools/retire-example.ps1`) are removed as an orchestrator code-structure
  decision under Trevor's global working rules ("Claude owns the code"; "Decide from the goals; don't
  punt"), passed on from the main session on 2026-10-20: the removal is reversible through git history
  and is not Trevor's approval. The
  implementer deletes nothing and checks criterion 8 with its Grep tool. The new Bash-scope text for
  `agents/implementation-agent.md` is an artifact of this issue, reviewed by reviewer-agent at PR time,
  and governs later issues only.
- Install doubles as upgrade: if the service already exists, the install script clears recovery
  actions, ends the service process and any other process whose image path is under
  `%ProgramFiles%\SteamGate`, waits for them with `Wait-Process -Timeout 30`, replaces the program
  files, leaves `%ProgramData%\SteamGate` untouched, re-applies recovery, and starts the service.
  Re-applying recovery and starting the service run even when the file copy fails (try/finally);
  the script then exits non-zero and prints `INSTALL: failed: upgrade failed: <reason>`. This needs admin, so it sits inside the
  documented admin-teardown gap.
- The service is registered with `New-Service` and a quoted `-BinaryPathName`, so the path under
  `C:\Program Files` is never an unquoted service path.
- Code analysis: `Directory.Build.props` sets `<AnalysisLevel>latest-recommended</AnalysisLevel>` and
  `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`, replacing the template's ruff and bandit.
- Scripts are run with Windows PowerShell 5.1 (`powershell.exe`), the shell a fresh Windows 11 admin
  prompt opens, and CI runs them the same way.
- Tests: xUnit v3 tests asserting output values. `dotnet test` runs them through VSTest; Stryker.NET
  runs them through the Microsoft Testing Platform (`"test-runner": "mtp"` in `stryker-config.json`,
  marked preview by Stryker), because its VSTest runner cannot capture coverage with xUnit v3 and
  then reports every mutant as surviving (found on the first local run, 2026-10-21). Test strength: Stryker.NET mutation testing on `SteamGate.Core`, pinned in
  `.config/dotnet-tools.json`; the build breaks below an 80% mutation score, and surviving mutants are listed in the CI log so
  a reviewer can tell an equivalent mutant from a missing test. This replaces the
  template's Python mutation harness.

## Acceptance criteria
A named section runs from its heading to the next heading of the same or higher level.

1. **Given** a fresh `SettingsStore` on an empty temp folder, **When** `Load()` runs, **Then** it
   returns `WeeknightMinutes == 90`, `WeekendNightMinutes == 180`, `WeekendNights == [Friday,
   Saturday, Sunday]`, `WeeklyCapMinutes == 600`, `CooldownHours == 48`, `NotCounted == ["Blender","OBS
   Studio","wallpaper_engine","Steamworks Shared"]`, `Enabled == true`, `Corrupt == false`;
   `settings.json` now exists;
   and its text, with whitespace removed, contains `"weeknightMinutes":90` and
   `"weekendNights":["Friday","Saturday","Sunday"]`. (Test `SettingsStoreTests.Load_EmptyFolder_WritesDefaults`.)
1b. **Given** `GateSettings.ApprovedNames`, **When** a test reads it, **Then** it has exactly 4
   entries and `ApprovedNames["wallpaper_engine"] == "Wallpaper Engine"`,
   `ApprovedNames["Blender"] == "Blender"`, `ApprovedNames["OBS Studio"] == "OBS Studio"`,
   `ApprovedNames["Steamworks Shared"] == "Steamworks Shared"`. (Test
   `GateSettingsTests.ApprovedNames_MapFolderToSpecName`.)
2. **Given** a folder where `Load()` has already written the default `settings.json`, **When**
   `SettingsStore.Save` runs with `WeeknightMinutes = 75` and then again with `WeeknightMinutes = 80`,
   and a new `SettingsStore` on the same folder runs `Load()`, **Then** `WeeknightMinutes == 80`, every
   other field equals what was saved, and no file named `settings.json.tmp` exists. (Test
   `SettingsStoreTests.SaveThenLoad_RoundTrips`.)
2b. **Given** a `settings.json` containing only `{"weeknightMinutes":75}`, **When** `Load()` runs,
   **Then** `WeeknightMinutes == 75`, `Enabled == true`, `CooldownHours == 48`,
   `WeeklyCapMinutes == 600`, `WeekendNights == [Friday, Saturday, Sunday]`, `NotCounted ==
   ["Blender","OBS Studio","wallpaper_engine","Steamworks Shared"]`, and `Corrupt == false`.
   (Test `SettingsStoreTests.Load_MissingFields_TakeDefaults`.)
3. **Given** a `settings.json` containing `not json`, **When** `Load()` runs, **Then** it returns the
   defaults with `Corrupt == true`, rewrites `settings.json` with the defaults, and leaves the bad
   file beside it under a name starting `settings.corrupt-`. (Test
   `SettingsStoreTests.Load_Corrupt_FallsBackToDefaults`.) The same holds for a file containing
   `null` (Test `SettingsStoreTests.Load_Null_FallsBackToDefaults`) for
   `{"weekendNights":null}` (Test `SettingsStoreTests.Load_NullField_FallsBackToDefaults`), and for
   `{"notCounted":[null]}` (Test `SettingsStoreTests.Load_NullItem_FallsBackToDefaults`).
3e. **Given** a `settings.json` containing `{"cooldownHours":-1}`, **When** `Load()` runs, **Then**
   `Corrupt == true` and `CooldownHours == 48`. (Test
   `SettingsStoreTests.Load_NegativeNumber_FallsBackToDefaults`, case `cooldownHours`.)
3f. **Given** the default `settings.json`, **When** `Save` runs with `WeeknightMinutes = -5`, **Then**
   it returns `false` and `settings.json` still contains `"weeknightMinutes":90`. (Test
   `SettingsStoreTests.Save_Rejected_ReturnsFalse`.)
3b. **Given** a `settings.json` containing `{"weeknightMinutes":75}` held open by the test with
   `FileShare.None`, **When** `Load()` runs, **Then** it throws `IOException`, `settings.json` still
   contains exactly `{"weeknightMinutes":75}`, and no file starting `settings.corrupt-` exists.
   (Test `SettingsStoreTests.Load_Locked_RethrowsWithoutOverwrite`.)
3c. **Given** a length of 1048577, **When** `LogRotation.ShouldRotate` runs, **Then** it returns
   true; given 1048576 it returns false. (Test `LogRotationTests.RotatesPast1MB`.)
3d. **Given** `settings.json` held open with `FileShare.None`, **When** `Save` runs with
   `WeeknightMinutes = 80`, **Then** it returns `false`, does not throw, and `settings.json` is
   unchanged. (Test `SettingsStoreTests.Save_Locked_ReturnsFalseWithoutThrow`.)
4. **Given** the CI Windows runner, **When** `powershell.exe -NoProfile -ExecutionPolicy Bypass -File
   install/Test-Install.ps1 -PublishDir out` runs, **Then** it prints these lines in order and exits 0
   only if all print:
   - `SERVICE RUNNING` after it runs `out\Install-SteamGate.ps1`, the service reports Running, and
     its registry `ImagePath` starts with a double quote.
   - `START AUTO` after `sc.exe qc SteamGate` shows `AUTO_START`.
   - `RESTARTED <old pid> -> <new pid>` (two different numbers) within 60 seconds of ending the
     service process with `Stop-Process -Force`.
   - `STOP REFUSED` after `sc.exe stop SteamGate` reports error 1052 and the service is still Running
     with the same pid.
   - `ACL OK` after checking that `%ProgramData%\SteamGate` has inheritance disabled and owner
     `BUILTIN\Administrators`, and that the folder, `settings.json`, and `service.log` (after the
     service has written them) have no access entry for any identity other than
     `NT AUTHORITY\SYSTEM` and `BUILTIN\Administrators`.
   - `RESTARTED-AFTER-FAULT <fault pid> -> <new pid>` after the test sets the service's registry
     `Environment` value (REG_MULTI_SZ) to `STEAMGATE_FAULT_TEST=1` and ends the process, where
     `<fault pid>` is the pid in the first `started pid=` line written after that kill,
     `service.log` contains `unhandled pid=<fault pid>`, and `<new pid>` is the pid of a later
     `started pid=` line, different from `<fault pid>`, written within 60 seconds after the
     `unhandled pid=<fault pid>` line. The test then removes the value, waits until the service is
     Running, ends it if running, and requires the next pid to stay Running for 10 seconds before
     going on.
   - `UPGRADED` after the test sets `weeknightMinutes` to 75 with
     `$j = Get-Content -Raw $p | ConvertFrom-Json; $j.weeknightMinutes = 75;
     [IO.File]::WriteAllText($p, ($j | ConvertTo-Json -Depth 5))`, ends the process,
     waits for the restart, runs `out\Install-SteamGate.ps1` again, and finds the service Running with
     a new pid and `settings.json` (read with `ConvertFrom-Json`) still holding 75.
   - `UNINSTALLED` after `out\Uninstall-SteamGate.ps1` runs and `Get-Service SteamGate` finds nothing
     within 30 seconds (polled each second), and `%ProgramData%\SteamGate` and
     `%ProgramFiles%\SteamGate` do not exist.
5. **Given** `.github/workflows/ci.yml`, **When** a reader greps it, **Then** it contains
   `runs-on: windows-latest`, `dotnet test`, `dotnet tool restore`, `dotnet stryker`,
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File install/Test-Install.ps1`, and
   `upload-artifact`; still contains
   the step `Commit-gate integrity (server-side backstop)`; and contains no `setup-python`.
6. **Given** `.github/workflows/codeql.yml`, **When** a reader greps it, **Then** it contains
   `codeql-action/init@v4`, `csharp`, and `build-mode: none`, and contains no `python`.
7. **Given** the repo, **When** a reader lists it, **Then** `addons/`, `evals/`,
   `tools/render_preview.py`, `tools/retire-example.ps1`, `skills/visual-confirm.md`, and
   `.github/workflows/blender-canary.yml` do not exist, and `tests/` holds only `tests/SteamGate.Tests/`.
8. **Given** the working tree, **When** a reader runs
   `git grep -n -E "run_pure|mutation_harness|render_preview|retire-example|visual-confirm|hashFiles\('addons|issue 0002 rewrites them" -- . ':!issues/' ':!BUILDLOG.md' ':!docs/AUTONOMOUS-RUN.md'`,
   **Then** there are no matches.
9. **Given** `DESIGN.md`, **When** a reader greps the sections `## Repo structure`, `## CI`,
   `## Testing`, `## Evals`, `### Test strength`, `### Provided by the template`, and
   `## Governance: in-license constraint`, **Then** none contains
   `Blender`, `no pytest`, or `evals/`, and `## CI` contains `windows-latest` and `Stryker`.
10. **Given** `src/SteamGate.Service`, **When** a reader greps it with the regex
   `\bNotifyIcon\b|System\.Windows\.Forms|\bnew Form\(|<UseWindowsForms>|<UseWPF>`, **Then** there
   are no matches; **and** it contains `CanStop = false`, `CanShutdown = true`, and
   `settings corrupt, defaults restored`.
11a. **Given** `.github/workflows/ci.yml`, **When** a reader reads the `build-test` job, **Then** its
   `upload-artifact` step uploads `out` as `steamgate-win-x64`, and the steps before it copy only
   `Install-SteamGate.ps1`, `Uninstall-SteamGate.ps1`, and the shared `SteamGate.Common.ps1` they load
   into `out`.
11b. **Given** CI on the commit to `main`, **When** the `build-test` job finishes, **Then** it is green and the
   run has an artifact named `steamgate-win-x64` containing `SteamGate.Service.exe`,
   `Install-SteamGate.ps1`, `Uninstall-SteamGate.ps1`, and `SteamGate.Common.ps1`, and not
   `Test-Install.ps1`.
12. **Given** every path in Touches, **When** a reader runs `git diff HEAD -- <Touches paths>`
   plus `git ls-files --others --exclude-standard -- <Touches paths>` for new files (before commit),
   or `git diff HEAD~1 HEAD -- <Touches paths>` (after commit), **Then** no added line contains U+2014 (em dash) or the em dash HTML entity, and
   `WHAT-IT-CHECKS.md` contains neither `ruff` nor `bandit`, and its "## What is NOT machine-checked"
   section does not contain `Test-Install`.
13. **Given** `agents/implementation-agent.md`, **When** a reader greps its Bash scope paragraph,
   **Then** it contains `CI-only` and `never install`.
14. **Given** `DESIGN.md`, **When** a reader greps the section
   `## SteamGate service design (orchestrator decisions, not part of the approved spec)`, **Then** it
   contains `RunTickAsync`, `JsonFileStore`, and `Channel<T>`, and `## SteamGate product spec` contains neither.
15. **Given** `DESIGN.md`, `README.md`, and `agents/reviewer-doc-currency.md`, **When** a reader
   greps them, **Then** DESIGN.md `## Deferred items` contains no `0002`; DESIGN.md
   `## Repo structure` contains `src/`, `install/`, and `stryker-config.json`; README.md contains
   `## Install` and `steamgate-win-x64`; `agents/reviewer-doc-currency.md` contains neither
   `addons/` nor `evals/`.
16. **Given** `DESIGN.md`, **When** a reader greps `### Implementation lifecycle`, **Then** it contains
   `no .NET SDK is installed locally` and `post-push CI build-test run`.

## Implementation plan
1. (Orchestrator, before hand-off.) Run `git rm -r -q addons tests evals tools/render_preview.py tools/retire-example.ps1
   .github/workflows/blender-canary.yml skills/visual-confirm.md`.
2. In `.gitignore`, remove the lines that only served the example (the `__pycache__/`, `*.pyc`,
   and `renders/` entries and their comments) and add `bin/`, `obj/`, `out/`, `StrykerOutput/`,
   `TestResults/`, `.vs/`, `*.user`. Create `global.json` (SDK `10.0.100`, `rollForward: latestFeature`), `Directory.Build.props`
   (`Nullable` enable, `TreatWarningsAsErrors` true, `ImplicitUsings` enable, and the two analysis
   settings in Design decisions),
   `.config/dotnet-tools.json` pinning `dotnet-stryker` to `5.0.0`, and `SteamGate.sln`
   with the three projects below.
3. Create `src/SteamGate.Core` (`net10.0`): `SteamGatePaths.DataFolder` (read-only,
   `CommonApplicationData\SteamGate`); `SettingsStore` and `JsonFileStore<T>` take the folder as a
   constructor argument, and the service passes `SteamGatePaths.DataFolder` (no static setter); `JsonFileStore<T>` as in Design
   decisions; `GateSettings` record with the defaults, a static `Defaults`, and a static `ApprovedNames` map
   `{"Blender":"Blender","OBS Studio":"OBS Studio","wallpaper_engine":"Wallpaper Engine","Steamworks Shared":"Steamworks Shared"}`
   for display; `SettingsStore` built on
   `JsonFileStore<GateSettings>` with `Load()` (returns settings plus a `Corrupt` flag) and `Save`. Use
   `System.Text.Json` with camelCase naming, `JsonStringEnumConverter` for days, and
   `RespectNullableAnnotations = true` so a null value for a non-nullable property throws
   `JsonException` and takes the corrupt path; after loading, any null item inside `NotCounted` or
   `WeekendNights` also counts as not valid JSON, as does a negative limit.
4. Create `src/SteamGate.Service` (`net10.0-windows`, SDK `Microsoft.NET.Sdk.Worker`, package
   `Microsoft.Extensions.Hosting.WindowsServices`):
   - `Program.cs`: `Host.CreateApplicationBuilder`, `AddWindowsService(o => o.ServiceName =
     "SteamGate")`, then, only when `WindowsServiceHelpers.IsWindowsService()`,
     `builder.Services.AddSingleton<IHostLifetime, GateServiceLifetime>()`, where
     `GateServiceLifetime` subclasses `WindowsServiceLifetime` and sets `CanStop = false` and
     `CanShutdown = true` in its constructor.
   - `GateLog`: appends one line per call, opening `service.log` with `FileShare.ReadWrite` and
     closing it; calls are serialized with a private lock; an `IOException` or `UnauthorizedAccessException` while appending or
     rotating is swallowed (the line is dropped), never propagated; it renames it to `service.log.1` (replacing the old one) when
     `LogRotation.ShouldRotate(length)` (in Core) says so: true when the length passes 1 MB, where
     length is the file's size before the line is appended.
   - `GateWorker : BackgroundService`: loads settings (logs `settings corrupt, defaults restored` when
     `Corrupt`), logs `started pid=<pid>`, throws `InvalidOperationException("fault test")` right
     after that line when environment variable `STEAMGATE_FAULT_TEST` is `1`, then runs a tick every
     5 seconds (named constant `ScanInterval`) by calling a private `RunTickAsync(CancellationToken)`,
     logging `alive` on every 12th tick. Every log line is prefixed with a UTC ISO time (`<ISO time> started pid=<pid>`, `<ISO time> alive`). Later issues add their periodic steps inside
     `RunTickAsync` and start no other periodic loop or timer; a server that answers clients (the 0005
     pipe) may run its own accept loop but hands every settings or tally change to the worker through a
     `Channel<T>` that `RunTickAsync` drains, so only the worker calls `Load` or `Save` on any store; other components read an immutable snapshot
     the worker publishes after each tick. Its body is wrapped in
     `catch (Exception ex) when (!(ex is OperationCanceledException && stoppingToken.IsCancellationRequested))`.
     Inside the catch, a `try` logs `unhandled pid=<pid> <message>` (a failure of that write is
     swallowed) and its `finally` calls `Environment.Exit(1)`. A normal return or a cancellation never
     reaches `Environment.Exit`. Any other long-running loop (a server accept loop, added later) catches and
     logs every exception raised while serving one connection or one message, drops that connection,
     and keeps accepting; only a failure of the loop itself (it cannot create or accept on its
     endpoint) takes the same catch, log, and `Environment.Exit(1)` path. 
5. Create `tests/SteamGate.Tests` (`net10.0-windows`; packages `xunit.v3`,
   `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) referencing both projects, with the
   `SettingsStoreTests` named in criteria 1, 2, 2b, 3, 3b, 3d, 3e, and 3f, `GateSettingsTests` in criterion 1b,
   and `LogRotationTests` in criterion 3c, each on a fresh temp folder, plus any further value-asserting tests on `SteamGate.Core` needed to
   reach the 80% Stryker break threshold. In `tests/SteamGate.Tests/SteamGate.Tests.csproj` set
   `<NoWarn>$(NoWarn);CA1707;CA1515</NoWarn>` (CA1515: xUnit needs public test classes) so the underscore test names required by the criteria build
   under `TreatWarningsAsErrors`.
6. Create `install/SteamGate.Common.ps1` (shared constants and the helpers the three scripts use, dot-sourced from each; the installer skips every `.ps1` when copying into Program Files). Create `install/Install-SteamGate.ps1` (Windows PowerShell 5.1 compatible; requires admin; on a
   fresh install, copies `SteamGate.Service.exe` and any other non-script files from `$PSScriptRoot` to
   `%ProgramFiles%\SteamGate`; then, if `%ProgramData%\SteamGate` exists, rename it
   to `SteamGate.old-<stamp>` (whatever its owner); create the folder with `New-Item -ItemType Directory` without `-Force` (exit non-zero if it
   already exists after the rename); run
   `icacls "<dir>" /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F"`,
   then a second call `icacls "<dir>" /setowner *S-1-5-32-544`; only then exit non-zero if
   `Get-ChildItem -LiteralPath <dir> -Force` returns anything (hidden and system entries included) (once its permissions are tight no standard user can add to it, so this check is
   conclusive); then run the folder part of the `ACL OK` check (inheritance, owner, entries) and exit
   non-zero if it fails. Register with `New-Service -Name SteamGate -DisplayName SteamGate
   -StartupType Automatic -BinaryPathName ('"' + "$dest\SteamGate.Service.exe" + '"')`, set the
   description, run the two recovery commands, start the service, and print `INSTALL: fresh`. When
   the service already exists, run the upgrade path in Design decisions and print `INSTALL: upgrade`.) Create
   `install/Uninstall-SteamGate.ps1` (requires admin; clears recovery with
   `cmd.exe /c 'sc.exe failure SteamGate reset= 0 actions= ""'`; ends the service process and waits
   for it with `Wait-Process -Timeout 30`; `sc.exe delete SteamGate`; removes
   `%ProgramFiles%\SteamGate` and `%ProgramData%\SteamGate`; its lines start `UNINSTALL:`). Both print
   one plain line per step and exit non-zero on any failed step.
7. Create `install/Test-Install.ps1` (it sets the service's `Environment` value as REG_MULTI_SZ). The script takes `param([Parameter(Mandatory)][string]$PublishDir)`; the criterion 4 sequence starts from a plain `"$PublishDir\Install-SteamGate.ps1"`, runs the checks in
   criterion 4 in order (ending with `"$PublishDir\Uninstall-SteamGate.ps1"`), printing the marker lines, and exits non-zero at the first failed check.
8. Create `stryker-config.json` with a root key `"stryker-config"` holding: `"project":
   "SteamGate.Core.csproj"`, `"test-projects": ["tests/SteamGate.Tests/SteamGate.Tests.csproj"]`,
   `"thresholds": {"high": 80, "low": 80, "break": 80}`, `"reporters": ["progress","cleartexttree"]`, `"test-runner": "mtp"`, and `"coverage-analysis": "off"` (each mutant runs the full suite; with coverage mapping on, scores varied between runs).
   Stryker requires high >= low >= break, so all three are set.
9. Rewrite `.github/workflows/ci.yml`: rename the `lint` job to `gate-integrity`; it keeps only checkout and the commit-gate integrity
   step (drop `setup-python` and the Python steps); delete the old `test` job; add a `build-test` job on
   `windows-latest` that runs `actions/checkout@v4`, then `actions/setup-dotnet@v6` with `global-json-file: global.json`,
   `dotnet build -c Release`, `dotnet test -c Release`, `dotnet tool restore`, `dotnet stryker`,
   `dotnet publish src/SteamGate.Service -c Release -r win-x64 --self-contained
   -p:PublishSingleFile=true -o out`, copies only `install/Install-SteamGate.ps1`,
   `install/Uninstall-SteamGate.ps1`, and `install/SteamGate.Common.ps1` into `out`, runs
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File install/Test-Install.ps1 -PublishDir out`,
   and uploads `out` with
   `actions/upload-artifact@v4` as `steamgate-win-x64`. Add a `workflow_dispatch:` trigger.
10. Rewrite `.github/workflows/codeql.yml` on `github/codeql-action` v4 with a language matrix of
   exactly `csharp` and `actions`, both `build-mode: none` (a configured language with no source files
   fails the job, so no `python`). Add a `nuget` entry to `.github/dependabot.yml`.
11. Fix every document this change makes false:
   - `DESIGN.md`: rewrite `## Repo structure` (add `src/`, `tests/SteamGate.Tests/`, `install/`, `.config/` (pinned dotnet tools), `tools/` (gate and run scripts), `docs/`
     (pinned approved spec), and the root build files `SteamGate.sln`, `global.json`, `Directory.Build.props`, `stryker-config.json`; drop
     `addons/`, `evals/`), the test and runtime lines in `## Governance: in-license constraint` (and
     remove both Blender sentences there), `## CI`, `### Test strength` (xUnit value assertions plus
     the Stryker.NET score: the build breaks below 80% on `SteamGate.Core`, and surviving mutants are
     listed in the CI log so a reviewer can tell an equivalent mutant from a missing test), `## Testing`, and the "Provided by the template" CI line. Keep the heading
     `## Evals` and replace its body with: there is no separate eval harness; the end-to-end check is
     `install/Test-Install.ps1` on the Windows runner. The status screen's look is approved by Trevor
     on a preview before it is built (see `## SteamGate product spec`). Add a new section `## SteamGate service design (orchestrator decisions, not part of the approved spec)`
     directly after `## SteamGate product spec`, holding: one
     5-second tick (`RunTickAsync`) carries every periodic step; every JSON file goes through
     `JsonFileStore<T>` (missing, corrupt, and locked outcomes as in this issue) and only the worker
     calls `Load` or `Save` on any store (other components read an immutable snapshot the worker
     publishes after each tick); the service crashes on start when its registry `Environment` holds
     `STEAMGATE_FAULT_TEST=1`, which exists only for `install/Test-Install.ps1`, needs admin, and sits
     inside the admin-teardown gap; a server that answers clients (the pipe) may run its own accept loop but hands every
     settings or tally change to the worker through a `Channel<T>` that `RunTickAsync` drains (0002
     builds no channel; the first issue whose server hands a settings or tally change to the worker
     creates it); the `%ProgramData%\SteamGate` ACL (SYSTEM and Administrators full, no other entries); the
     service refuses Stop, accepts Shutdown, and exits 1 on any unhandled error in the worker or in any other long-running loop (a server loop
     isolates per-connection and per-message errors and exits 1 only when the loop itself fails) so recovery restarts it; install doubles as upgrade; an upgrade never touches ProgramData, and only a fresh install creates it. Decision logic lives in
     `SteamGate.Core`; `SteamGate.Service` only connects it to Windows (service lifetime, process and
     registry access, the tick). The missing or corrupt fallback is a loosening reachable only by an
     admin edit (the documented admin-teardown gap) or disk failure; settings and tally schema changes
     are additive only (never rename a field or change its type), so an upgrade never takes the
     corrupt path. In `### Implementation lifecycle` step 3, append (as one unwrapped line): `When no .NET SDK is installed
     locally, reviewer-pr verifies test, mutation, and install criteria by tracing the changed code on a
     concrete input to a concrete output with file:line; the executed proof is the post-push CI
     build-test run, and a red run is fixed or the commit reverted before any other work.` In `## System-level change
     definition`, replace "Ordinary add-on code" with "Product code under `src/`, `tests/`, and
     `install/`". Replace the `## SteamGate product spec` closing line with `The Blender line in the Issue
     lifecycle describes the template's own machinery and stays.`, in `### Implementation lifecycle` step 1
     replace "consulting `blender-rag` for any bpy code" with "confirming APIs per its build rule 2", and remove the
     `## Deferred items`
     bullet for 0002 that 0001 added.
   - `CLAUDE.md` `## Pipeline`: replace "add-on features" with "product features".
   - `CLAUDE.md` `## First run` step 2: say the example was retired in 0002 and CI runs the .NET
     build, tests, Stryker, and the install test.
   - `agents/implementation-agent.md`:
     - Bash scope as a bulleted list, allowed and never items marked: allowed: `dotnet --list-sdks`;
       only when an SDK is listed, `dotnet build`, `dotnet test`, `dotnet tool restore` (pinned tools
       only), `dotnet stryker`, and never install an SDK; `git rm` of exact (non-glob) paths the acting
       issue's Touches marks for removal; the read-only git commands (`git grep`, `git diff`,
       `git ls-files`) named in its acceptance criteria. Never: run any script under `install/` locally (`Test-Install.ps1` is CI-only, and the
       install scripts install and kill a system service); commit; self-approve. Keep the literal
       strings `CI-only` and `never install`.
     - Output contract: either the artifact written plus `"Artifact written to <path>. Ready for
       review."` followed by one line naming any skipped dotnet commands and, when `dotnet test` or
       `dotnet stryker` ran, their summary lines (if no dotnet command applies, as for a doc, skill, or
       agent artifact, or none was skipped, the line reads "dotnet: none skipped."); or a halt message `"Halted: <reason>."` naming an
       unconfirmed API (build rule 2) or a scope ambiguity (build rule 5), with no artifact claimed.
     - Inputs: change "(all required)" to "(all required unless marked conditional)" and add
       "Path to the researcher's findings doc, when research ran (conditional)."
     - Build rule 2: "**Confirm APIs, don't recall them.** Before using a .NET or Windows API you are
       unsure of, confirm its signature from local prior art or the researcher's findings doc. If
       neither confirms it, halt and name the API in the halt message instead of writing it from
       memory." (This replaces the Blender RAG rule.)
     - Build rule 5: replace 'stop and surface the ambiguity' with 'Halt with the halt message naming the ambiguity'.
   - `agents/orchestrator.md`:
     - Research step: "**Research:** Delegate to `agents/researcher.md` using `skills/research-prior-art.md`, which
       sets the order of sources. Do not research what prior art already answers." Research-first rule:
       same pointer, with no Blender RAG wording of its own. Hand-off to the implementer: "the passing
       issue + the researcher's findings doc + all prior-art file paths."
     - Replace the "during a Blender-development run" system-defect sentence with a general one.
     - Constraints: an "owner confirms the look" checkpoint on visible output is sanctioned; for
       SteamGate that is the status screen, whose look Trevor approves on a preview before it is built.
     - Add pipeline step 2a, "Owner look gate": if the issue builds visible output whose look the
       owner approves, do not start Research or Implementation until the issue records that approval;
       add to the timed-run stop list "an issue blocked on the owner look gate, which is parked and
       skipped, never built without approval". Write the stop-list item as ", an issue blocked on the
       owner look gate (that issue is parked and skipped, never built without approval; the run
       continues), or a scope decision" with commas separating it from its neighbors.
     - After the implementation step, add: if the implementer returns `Halted: <reason>.`, do not spawn
       artifact review; an unconfirmed API goes back to the Research step naming that API, and a scope
       ambiguity goes back to the Issue step to revise the issue.
   - `CLAUDE.md` `## Pipeline` step 2 (review): append the look-gate rule (nothing is researched or
     implemented until the issue records the owner's look approval; orchestrator step 2a). `DESIGN.md`
     `### Issue lifecycle`: add the same rule as a step right after the issue-review step.
   - `WHAT-IT-CHECKS.md`: add a bullet under "## What does a green checkmark actually prove?" saying
     CI installs SteamGate on a Windows test machine, kills it, and confirms Windows restarts it, that
     Stop is refused, that the settings folder is admin-only, and that upgrade keeps settings and
     uninstall removes it. Replace the preview paragraph under "## What is NOT machine-checked" with:
     the status screen's look, which Trevor approves on a preview before it is built (issue 0007).
     Replace the ruff/bandit bullet with: "**The code is auto-checked for cleanliness.** The .NET code
     analyzers run on every CI build and fail it on any warning, so sloppy or unsafe patterns block a
     green checkmark."
   - `agents/reviewer-doc-currency.md`: replace `(`addons/`, `tests/`, `evals/`, `.github/`, or
     any other)` with `(`src/`, `tests/`, `install/`, `.github/`, or any other)`.
   - `README.md`: a one-paragraph SteamGate introduction at the top above the template material;
     replace the Python 3 prerequisite line with `.NET 10 SDK: only to build locally; CI builds without
     it.`, the "A worked example" paragraph under `## What's already wired in` (it names
     `tools/retire-example.ps1`, which this issue removes), and the `tests/ · evals/`
     layout row; add layout rows for `src/`, `tests/SteamGate.Tests/`, `install/`, `.config/`, and the root
     build files; add
     `## Install`: download and extract the `steamgate-win-x64` artifact, then from an admin
     PowerShell in that folder run `powershell -ExecutionPolicy Bypass -File .\Install-SteamGate.ps1`.
     Artifacts expire after 90 days; if none is listed, run the CI workflow from the Actions tab (Run workflow).
     Also demote the template title to a `##` heading under a one-line pointer, and add the uninstall sentence to `## Install`.
   - `CLAUDE.md` `.NET SDK` convention: state that 10.0.402 is installed (approved 2026-10-20) and CI
     is the proof of record; never install another SDK without asking Trevor.
   Then run the search in criterion 8 with the Grep tool and fix any match, and remove any em dash on a line you
   edited.

## Out of scope
- Detecting games, counting, closing, the cooldown lock, the helper, and the status screen
  (0003 to 0007). The uninstall script's cooldown check is added in 0006.