# Issue #0004: Time accounting

**Type:** ready. **Category:** product feature; adds new components (night clock, tally, budget)
and changes DESIGN.md, so it is a system-level change.
**Review bar:** system-level change (touches DESIGN.md); two independent reviewers, both PASS;
reviewer-documentation on the DESIGN.md paragraph; architecture gate at issue time; reviewer-pr then design-philosophy gate at PR time.
**GitHub:** #9.
**Depends on:** 0002, 0003.
**Blocks:** 0005, 0006, and the status-screen issue (0007 in PLAN.md, not yet filed).
**Touches:** `src/SteamGate.Core/Accounting/**` (new; `TallyStore` sits on the `JsonFileStore<T>`
from 0002), `src/SteamGate.Core/GateSnapshot.cs` (fields added), `src/SteamGate.Service/GateWorker.cs`,
`tests/SteamGate.Tests/Accounting/**` (new), `install/Test-Install.ps1`, `DESIGN.md`, `README.md`, `WHAT-IT-CHECKS.md`,
`BUILDLOG.md`, `issues/0004-time-accounting.md`.

## User story
As Trevor, I need SteamGate to keep an accurate count of how long Steam games have been open
tonight and this week, with nights ending at 4 AM and the week resetting Monday 4 AM, and to keep
that count through restarts, so that the limits in 0005 are measured against the real time I
played and killing the service does not reset my count.

## Approved spec this issue carries
"Counts wall-clock minutes while at least one Steam game is open; two games at once don't
double-count." "Time accounting (night ends 4 AM, weeknight/weekend nights, weekly cap resets
Monday 4 AM, saved to disk every minute)." Defaults: weeknight 90 min; weekend night 3 h; weekend
nights Friday and Saturday; weekly cap 10 h. "Task Manager CAN kill the service ... Windows restarts
it and the tally survives."
Amended 2026-10-21 (issue 0008): weekend nights are Friday, Saturday, and Sunday.

## Design decisions (orchestrator)
- A "night" is named by the calendar date (`DateOnly`) of `localTime - 4 h`. So 2026-10-24 03:59
  (Saturday) is still the night of Friday 2026-10-23. A night is a weekend night when its date's day of
  week is in `weekendNights`. The "week" is the Monday (`DateOnly`) on or before the night's date.
  Monday 03:59 still belongs to the previous week; Monday 04:00 starts a new one.
- The tally (`tally.json` in the protected folder) is an immutable record: `Night` (`DateOnly`),
  `NightSeconds` (double), `Week` (`DateOnly`), `WeekSeconds` (double), `LastCountedUtc`
  (`DateTime?`). `NightClock.RollOver(Tally, DateTime localNow)` returns the tally with any part whose
  stored night or week differs from the one `localNow` names reset to 0. Both `Tick` and `Budget` call
  it, so the stale rule lives in one place.
- Counting is a pure function:
  `static Tally Accountant.Tick(Tally current, DateTime localNow, DateTime utcNow, TimeSpan monotonicStep, TimeSpan maxStep, IReadOnlyCollection<RunningGame> openGames)`
  returns a new tally. It first applies `NightClock.RollOver`. When `openGames` is not empty it adds
  `Math.Max(0, Math.Min(monotonicStep.TotalSeconds, maxStep.TotalSeconds))` with no rounding, and two games open
  together add the same single interval; it then sets `LastCountedUtc = utcNow`. When `openGames` is
  empty it sets `LastCountedUtc = null`, so a non-null `LastCountedUtc` means a game was open at the
  last count. The worker passes `maxStep` = `MaxTickStep` (15 s, named constant) on every tick, so at
  most 15 seconds of a sleep or hibernate gap is counted, except on the first tick after start (see
  Restart gap). The worker calls it exactly once per `RunTickAsync` with the matcher's full
  result. `Tick` takes the games themselves, not a flag, so the two-games-count-once rule is tested
  at the boundary the worker uses. Which step and cap apply is decided in Core by
  `Accounting/StepTimer.cs`: `Next(Tally loaded, DateTime utcNow, bool anyGameOpen)` returns
  `(RestartStep(...), MaxRestartGap)` on its first call and `(elapsed since the last call, MaxTickStep)`
  after that, using its own `Stopwatch`; the worker holds one and has no first-tick flag of its own. Counting ignores `enabled`: it counts the same whether SteamGate is
  enabled or disabled.
- Restart gap: on the first tick after the service starts, `Accountant.RestartStep` returns
  `Math.Clamp((utcNow - LastCountedUtc).TotalSeconds, 0, 60)` when a game is open now and the loaded
  `LastCountedUtc` is not null (else 0), and `StepTimer.Next` returns it as the step with
  `maxStep` = `MaxRestartGap` (60 s, named constant). Only downtime that began during play is counted,
  and repeated kills cannot freeze the count. A changed clock is the documented gap.
- `Budget.RemainingSeconds(Tally, GateSettings, DateTime localNow)` returns a `double`, unrounded: the
  smaller of (tonight's limit minus tonight's use) and (weekly cap minus the week's use), never below
  0, after `NightClock.RollOver`. Tonight's limit comes from `NightOf(localNow)`.
- Saved through the `JsonFileStore<T>` from 0002 on every tick where `Tick` changed the tally (every 5
  seconds while a game is open, so well inside the spec's "every minute"), once a minute (every `TicksPerMinute` ticks)
  otherwise, and once more after the tick loop returns because the stop token was cancelled. When
  `Save` returns false the worker logs `save failed tally.json`. `TallyStore.Load()` returns
  `LoadResult<Tally>` (`.Value`, `.Corrupt`), like `SettingsStore.Load()`; `Corrupt` is never written to
  `tally.json`. A corrupt tally is logged as `tally corrupt, reset` (the count restarts from 0). At
  start the worker logs `tally loaded night=<yyyy-MM-dd> seconds=<n>`.
- After each tick the worker sets `Tally`, `RemainingSeconds`, and `TonightLimitMinutes` on the `GateSnapshot` that 0003 defines (an object initializer on a new `GateSnapshot`), for 0005 and 0007 to read. They are
  init-only properties with defaults, not positional parameters, so `GateSnapshot.Empty` and 0003's
  constructor calls stay unchanged: `Tally = Tally.Empty`, and `RemainingSeconds` (`double?`) and
  `TonightLimitMinutes` (`int?`) are null until the first tick computes them. Remaining time is
  computed once per tick and used for both the log line and the snapshot. The tick reads the clock
  once and derives local and UTC time from that one reading.
- Numbers in log lines are written with `CultureInfo.InvariantCulture`.

## Acceptance criteria
A named section runs from its heading to the next heading of the same or higher level.

1. **Given** local time 2026-10-24 03:59, **When** `NightClock.NightOf` runs, **Then** it returns
   2026-10-23; **given** 2026-10-24 04:00, **Then** 2026-10-24. (Test `NightClockTests.NightEndsAt4AM`.)
2. **Given** default settings, **When** `Budget.NightLimitMinutes` runs for night 2026-10-22
   (Thursday), **Then** it returns 90; for 2026-10-23 (Friday) 180; for 2026-10-24 (Saturday) 180;
   for 2026-10-25 (Sunday) 180. (Test `BudgetTests.NightLimitByWeekday`.)
3. **Given** local times 2026-10-25 23:00 (Sunday), 2026-10-26 03:59 (Monday), and 2026-10-26 04:00,
   **When** `NightClock.WeekOf` runs, **Then** it returns 2026-10-19, 2026-10-19, and 2026-10-26.
   (Test `NightClockTests.WeekResetsMonday4AM`.)
4. **Given** an empty tally and `openGames` holding two `RunningGame` records (`Viewfinder` pid 20,
   `Overwatch` pid 30), **When** `Accountant.Tick` runs 12 times, each with a 5-second step, at local
   time 2026-10-22 21:00 onward, **Then** tonight's used seconds is exactly 60 (not 120). (Test
   `AccountantTests.TwoGamesCountOnce`.)
5. **Given** an empty tally, **When** `Accountant.Tick` runs with a game open, a 600-second step,
   and `maxStep` 15 seconds, **Then** tonight's used seconds is 15. (Test `AccountantTests.GapIsCapped`.)
6. **Given** no game open, **When** `Accountant.Tick` runs 10 times with 5-second steps, **Then**
   tonight's used seconds is 0. (Test `AccountantTests.NoGameNoCount`.)
7. **Given** a tally for night 2026-10-22 with 3000 seconds and week 2026-10-19 with 20000 seconds,
   **When** `Accountant.Tick` runs with a game open at 2026-10-23 04:00:05 and a 5-second step,
   **Then** the night is 2026-10-23 with 5 seconds, and the week is still 2026-10-19 with 20005
   seconds. (Test `AccountantTests.NightRollsOverWeekDoesNot`.)
8. **Given** a tally for night 2026-10-25 with 3000 seconds and week 2026-10-19 with 30000 seconds,
   **When** `Accountant.Tick` runs with a game open at 2026-10-26 04:00:05 and a 5-second step,
   **Then** the night is 2026-10-26 with 5 seconds and the week is 2026-10-26 with 5 seconds. (Test
   `AccountantTests.WeekRollsOverMonday4AM`.)
9. **Given** an empty tally, **When** `Accountant.Tick` runs 3 times with a game open and steps of
   4.999 seconds, **Then** tonight's used seconds is 14.997 within 0.001. (Test
   `AccountantTests.FractionalStepsKept`.)
10. **Given** default settings, a tally for night 2026-10-22 (Thursday) with 5000 seconds and week
   2026-10-19 with 34000 seconds, and `localNow` 2026-10-22 22:00, **When** `Budget.RemainingSeconds`
   runs, **Then** it returns 400; with 35800 week seconds it returns 200; with 6000 night seconds it
   returns 0. (Test `BudgetTests.RemainingIsSmallerOfNightAndWeek`.)
11. **Given** default settings and a tally for night 2026-10-22 with 5400 seconds and week 2026-10-19
   with 5400 seconds, **When** `Budget.RemainingSeconds` runs at 2026-10-23 17:00, **Then** it
   returns 10800; **and when** `Accountant.Tick` runs at that time with no game open, **Then** the
   tally's night is 2026-10-23 with 0 seconds. (Test `BudgetTests.StaleNightCountsAsZero`.)
12. **Given** a tally with `LastCountedUtc` 20 seconds before `utcNow` and a game open, **When**
   `Accountant.RestartStep` is computed and `Tick` runs with it as both the step and `maxStep`, **Then** night seconds rise by 20; with
   `LastCountedUtc` 600 seconds before, they rise by 60; with it 5 seconds after `utcNow`, by 0.
   **And given** a tally after one `Tick` with a game open followed by one `Tick` with no game,
   **Then** `LastCountedUtc` is null and `RestartStep` with a game open returns 0. (Test
   `AccountantTests.RestartGapCounted`.)
13. **Given** a tally saved with `TallyStore.Save`, **When** a new `TallyStore` loads it, **Then**
   every field matches and `Corrupt == false`. (Test `TallyStoreTests.SaveThenLoad_RoundTrips`.)
14. **Given** a `tally.json` containing `not json`, **When** `TallyStore.Load` runs, **Then** it
   returns `Corrupt == true` and a tally with `NightSeconds == 0` and `WeekSeconds == 0`, and a file
   starting `tally.corrupt-` exists (Test `TallyStoreTests.Load_Corrupt_ResetsToZero`); **and given**
   `src/SteamGate.Service`, **When** a reader greps it, **Then** it contains `tally corrupt, reset`.
15. **Given** the CI Windows runner, after `Reset-SteamGate @{}` then `New-FakeSteamRoot` (0003),
   **When** `Test-Install.ps1` starts `FakeGame.exe -n 600 127.0.0.1` and polls `tally.json` each
   second for up to 180 seconds until night use is at least 60, **Then** it prints `TALLY <n>`. It
   then ends the service process with `Stop-Process -Force`, waits up to 60 seconds for the restart,
   keeps the fake game running, and waits 75 seconds; **Then** it prints `TALLY KEPT <m>` only when
   `m` (night use in `tally.json`) is at least `n + 30` and the restarted service logged
   the first `tally loaded night=<yyyy-MM-dd> seconds=<s>` line written after the restarted process's
   `started pid=<new pid>` line has `s` at least `n`. It exits non-zero at
   any failed check, including a timeout of the 180-second poll or the 60-second restart wait, and
   ends with `Remove-FakeSteamRoot` and then an uninstall, both in a `finally` block.
16. **Given** `src/SteamGate.Core/GateSnapshot.cs`, **When** a reader greps it, **Then** it contains
   `RemainingSeconds` and `TonightLimitMinutes`; **and given** `src/SteamGate.Service/GateWorker.cs`,
   it contains `catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)`.
17. **Given** `DESIGN.md`, **When** a reader greps
   `## SteamGate service design (orchestrator decisions, not part of the approved spec)`, **Then** it
   contains `How time is counted`, `minus 4 h`, `15 s`, and `restart`.

## Implementation plan
1. Create `src/SteamGate.Core/Accounting/NightClock.cs` (`NightOf(DateTime local)` and
   `WeekOf(DateTime local)` return `DateOnly`; `RollOver(Tally, DateTime localNow)`; boundary hour 4 as a
   named constant).
2. Create `src/SteamGate.Core/Accounting/Tally.cs` (immutable record with the fields in Design
   decisions and a static `Empty`) and `src/SteamGate.Core/Accounting/Accountant.cs` (the static pure
   `Tick` with its `maxStep` parameter, the named constants `MaxTickStep` and `MaxRestartGap`, and a
   static `RestartStep(Tally, DateTime utcNow, bool anyGameOpen)` returning the clamped first-tick
   step).
3. Create `src/SteamGate.Core/Accounting/Budget.cs` (`NightLimitMinutes(DateOnly night,
   GateSettings)`, `RemainingSeconds(Tally, GateSettings, DateTime localNow)` returning `double`).
4. Create `src/SteamGate.Core/Accounting/TallyStore.cs` on `JsonFileStore<Tally>` (`tally.json`);
   `Load()` returns `LoadResult<Tally>`, like `SettingsStore.Load()`.
5. Create `src/SteamGate.Core/Accounting/StepTimer.cs` as in Design decisions. In
   `src/SteamGate.Core/GateSnapshot.cs`, add `Tally`, `RemainingSeconds` (`double?`), and
   `TonightLimitMinutes` (`int?`) as init-only properties with the defaults in Design decisions. In `src/SteamGate.Service/GateWorker.cs`, inside `RunTickAsync` (no new
   loop or timer): call `Accountant.Tick` once per tick with the matcher's full result (the step and `maxStep` come from
   `StepTimer.Next` on every tick); save as in Design decisions; log `remaining <seconds>`
   every `TicksPerMinute` ticks while a game is open; publish the snapshot fields. Load the tally once at start, after the
   `started pid=` line is logged (criterion 15 depends on this order); log
   `tally loaded night=<yyyy-MM-dd> seconds=<n>`, and `tally corrupt, reset` only when `Corrupt`; and make the 5-second wait
   `try { ... } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }`
   so the loop ends normally, then save the tally once after the loop (not in a `finally`).
6. Add the tests in criteria 1 to 14 under `tests/SteamGate.Tests/Accounting/`, plus any further
   value-asserting tests on the new Core code needed to keep the 80% Stryker break threshold. In
   `install/Test-Install.ps1`, add the criterion 15 group: if fewer than 15 minutes remain before the next
   local 04:00 it waits until 04:01 (the night would reset mid-test); it calls `Reset-SteamGate @{}` then
   `New-FakeSteamRoot`, starts `FakeGame.exe -n 600 127.0.0.1`, wraps each `tally.json` read in
   try/catch (a failed read counts as not yet), exits non-zero at any failed check (including a timeout of the 180-second poll or the 60-second
   restart wait), and ends with `Remove-FakeSteamRoot` and then an uninstall, both in a `finally` block.
7. In `DESIGN.md` `## SteamGate service design (orchestrator decisions, not part of the approved spec)`,
   add a "How time is counted" paragraph: night = date of local time minus 4 h; week = Monday on or
   before the night; Stopwatch step per tick capped at 15 s, no rounding; counting ignores `enabled`;
   the restart gap counts up to 60 s of downtime after a kill and restart, so repeated kills cannot
   freeze the count; `tally.json` through `JsonFileStore<T>`, saved on every changed tick and on
   shutdown; a stale night or week counts as 0 used; a corrupt tally resets to 0; a changed clock can
   fool the night and week boundaries (the approved known gap), and the time-zone case is an
   orchestrator reading of that gap, not a new approval.

## Out of scope
- Warnings and closing (0005); changing limits (0006); showing the numbers (0007). A changed system
  clock can fool the night and week boundaries (the approved known gap); a time-zone change is
  treated as a clock change under that same gap.
