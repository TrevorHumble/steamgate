# Issue #0006: The lock (cooldown rules for every change)

**Type:** ready. **Category:** product feature; adds new components (the change engine, the request
channel from the pipe to the worker) and changes the uninstall script and DESIGN.md, README.md, and
WHAT-IT-CHECKS.md, so it is a system-level change.
**Review bar:** system-level change; two independent reviewers, both PASS; reviewer-documentation on
the DESIGN.md, README.md, and WHAT-IT-CHECKS.md changes; plus the architecture gate at issue time and
the design-philosophy gate at PR time.
**GitHub:** #11.
**Depends on:** 0002, 0003, 0004, 0005, 0008.
**Blocks:** 0007, GitHub #17.
**Touches:** `BUILDLOG.md`, `src/SteamGate.Service/BootMarker.cs` (new), `src/SteamGate.Service/RateLimitedLog.cs` (new), `src/SteamGate.Core/Pipe/ChangeMessages.cs` (new), `src/SteamGate.Core/Pipe/LogLimiter.cs` (renamed from `HelloLogLimiter.cs`), `install/SteamGate.Common.ps1`, `tests/SteamGate.Tests/SettingsStoreRuleTests.cs`, `src/SteamGate.Core/Rules/**` (new), `src/SteamGate.Core/GateSettings.cs`,
`src/SteamGate.Core/SettingsStore.cs`, `src/SteamGate.Core/GateSnapshot.cs`,
`src/SteamGate.Core/Pipe/PipeMessage.cs`, `src/SteamGate.Core/Pipe/ConnectionRules.cs`,
`src/SteamGate.Core/Pipe/OutboundQueue.cs`, `src/SteamGate.Service/PipeServer.cs`,
`src/SteamGate.Service/GateWorker.cs`, `src/SteamGate.Service/Program.cs`,
`tests/SteamGate.Tests/Rules/**` (new), `tests/SteamGate.Tests/Pipe/**`,
`tests/SteamGate.Tests/SettingsStoreTests.cs`, `install/Uninstall-SteamGate.ps1`,
`install/Test-Install.ps1`, `install/PipeProbe.ps1`, `DESIGN.md`, `README.md`, `WHAT-IT-CHECKS.md`,
`issues/0006-the-lock.md`.

## User story
As Trevor, I need every change that loosens a SteamGate rule to wait out the cooldown before it
takes effect, and every change that tightens a rule to take effect at once, so that a late-night
decision to play more cannot undo a limit I set earlier, while making the rules stricter is never
held up.

## Approved spec this issue carries
"The lock (protected settings folder, cooldown rules below)." "Cooldown rules: any change that
loosens a rule (raise a limit, add a weekend night, shorten the cooldown, add a program to the
not-counted list, disable, uninstall) waits the full cooldown. Any change that tightens applies right
away. The cooldown has a hard maximum of 7 days, so it can't be set to a year by mistake."
"Not-counted list (whitelist) ... adding waits the cooldown, removing applies now." (The protected
folder itself was built in 0002.)

## Design decisions (orchestrator)
- A change request names one field and a new value. Request fields: `weeknightMinutes`,
  `weekendNightMinutes`, `weeklyCapMinutes`, `cooldownHours` (numbers), `weekendNights` (the full
  new set of days), `notCountedAdd` and `notCountedRemove` (one folder), `enabled` and
  `allowUninstall` (true or false).
- `ChangeRequest(string Field, int? Number, bool? Flag, string? Folder, IReadOnlyList<DayOfWeek>?
  Days)`: exactly one value member is set, matching `Field`, else `Not allowed.`; `PipeServer` builds
  it from `RequestMessage` field by field. `RequestMessage.days` stays text; `PipeServer` turns it
  into days with the Core function `DayNames.TryParse(IReadOnlyList<string>, out
  IReadOnlyList<DayOfWeek>)`, which accepts a day only when it equals a name from
  `Enum.GetNames<DayOfWeek>()` compared `OrdinalIgnoreCase` (so `"6"` and `"7"` are refused, never
  parsed as numbers). When it fails, `PipeServer` replies `Not allowed.` without calling the
  engine.
- Classification by effect on the rules, with one exception: adding a weekend night always waits,
  because the approved spec names it a loosening and the preview page (`docs/preview-2026-10-21.html`) made adds wait:
  - A higher limit loosens; a lower one tightens. A shorter cooldown loosens; a longer one tightens.
  - `notCountedAdd` loosens; `notCountedRemove` tightens. `enabled = false` and
    `allowUninstall = true` loosen; their opposites tighten.
  - A weekend-nights change is judged per day. Adding a weekend night always loosens and waits (the
    spec lists "add a weekend night" as a loosening, and the preview page (`docs/preview-2026-10-21.html`) made adds wait).
    Removing a weekend night applies now only when it cannot raise that night's limit under any mix
    of current and pending values: for every pairing of {current `weeknightMinutes`, each pending
    `weeknightMinutes`} with {current `weekendNightMinutes`, each pending `weekendNightMinutes`}, the
    weekend value must be at least the weeknight value. Otherwise the removal loosens and waits. A
    change with both kinds of day is split: removals that apply now apply, the rest wait.
  - A request equal to the current value changes nothing, leaves pending entries alone, and replies
    `Already set.`
- A loosening becomes a pending entry, a typed record:
  `PendingChange(Guid Id, string Field, int? Number, bool? Flag, string? Folder,
  IReadOnlyList<DayOfWeek>? Days, DateTime RequestedUtc, double RemainingWaitSeconds)`. Exactly one
  value member is set, chosen by `Field`. Pending fields are the request fields except that a
  weekend-nights change is stored as `weekendNightsAdd` or `weekendNightsRemove`, whose `Days` hold
  only the days that wait. `RemainingWaitSeconds` starts at the cooldown in force at request time, so
  shortening the cooldown waits the current cooldown and lengthening it does not move existing
  entries. With cooldown 0 a loosening applies now and replies `Applied now.`
- The cooldown counts real calendar time, so a 48 h wait means 48 hours on the clock, but a clock
  change while the PC is on gains almost nothing. The pure function
  `ChangeEngine.StepSeconds(bool firstTick, bool sameBoot, DateTime utcNow, long prevUptimeMs,
  long uptimeMs, DateTime? lastSavedUtc, long? lastSavedUptimeMs)` returns the seconds to subtract
  from every entry each tick:
  - While the service runs: `max(0, (uptimeMs - prevUptimeMs) / 1000.0)`, the system-uptime change
    (`Environment.TickCount64`, which keeps counting through sleep and ignores the clock). The wall
    clock plays no part while running, so a clock change gains nothing and sleep still counts.
  - Same boot: at start the service opens the volatile registry key `HKLM\SOFTWARE\SteamGate\Boot`
    (`RegistryOptions.Volatile`, which Windows deletes at every restart), creating it with a new GUID
    value `Id` when absent. When the saved `BootId` equals that `Id`, it is the same Windows boot
    whatever the clock says, and the first-tick step is the uptime change since
    `lastSavedUptimeMs`, so a kill and restart cannot be stretched by a clock change. If
    `lastSavedUptimeMs` is null it returns 0.
  - After a Windows restart (no match): `utcNow - lastSavedUtc`, never below 0, so time the PC is
    off still counts toward the wait. A clock moved forward before Windows restarts can still shorten
    the wait across that restart; this is an orchestrator reading of the approved clock gap, not a
    new approval, and the orchestrator reports it to Trevor in the run's hand-off note.
  - An entry applies when `RemainingWaitSeconds` reaches 0 or below. The status screen (0007) shows
    the remaining wait and an "about" end time computed from it.
- As on the preview page, a pending entry leaves the list only by applying or by `Cancel`; a new
  request never removes or changes pending entries, a tightening included. Each loosening adds its
  own entry. When several entries come due in one tick, `ApplyDue` applies them in `RequestedUtc`
  order (then `Id`), so the later request wins. Folders compare `OrdinalIgnoreCase`; day sets
  compare as sets, order ignored. `notCountedRemove` of a folder that is not on the list replies
  `Not in the list.` (a pending add of it stays); `notCountedAdd` of a folder already on the list
  replies `Already set.` Cancelling a pending entry always applies now.
- Allowed values: night limits 0 to 1440 minutes, weekly cap 0 to 10080 minutes, cooldown 0 to 168
  hours (the 7-day maximum); a folder name is 1 to 260 characters with no `\`, `/`, or `:`; a
  weekend-nights set holds distinct valid day names; at most 50 pending entries; `notCountedAdd` is
  refused when current folders plus pending additions reach 50, and an add that would exceed 50 when it comes due
  (after a hand edit, or when an add applied at cooldown 0 filled the list while an older add
  still waited) is removed from `Pending`, returned in `ApplyDue`'s `Skipped`, and
  logged once as `skipped notCountedAdd` (never the folder name). A weekend change whose waiting days need more
  entries than the free pending slots creates none. If none of its days apply now, it replies
  `Not allowed.` (result `Refused`). If some days apply now, they apply and the reply is
  `Applied now: <days>. Not allowed: <days>.` with result `Applied`, `Changed` true, `ok` true, and
  `pending` null. The pending-full and list-full refusals apply only
  to the part of a request that would create a new entry, except that `notCountedAdd` is always
  refused when the list would exceed 50 folders, including with cooldown 0; tightenings, cancels, same-value requests,
  and the applied-now days of a split weekend change still apply, so a full list never blocks a
  tightening.
- Replies (named constants, `ChangeReply`): `Applied now.`; `Waits <h> h <m> min.` (total minutes =
  ceiling(seconds / 60); h = total / 60; m = total % 60; at request time the minutes part is always
  0, and the rule is kept so 0007 can format remaining waits); for a split weekend change
  `Applied now: <days>. Waits <h> h <m> min: <days>.`; for a partly refused weekend change
  `Applied now: <days>. Not allowed: <days>.`; `Already set.`; `Not in the list.`;
  `No pending change with that id.`; `Out of range: <min> to <max>.`; `Not allowed.` (unknown field,
  wrong kind of value, bad folder name, bad day set, list full, or pending full); and
  `Busy, try again.` A successful `Cancel` replies `Applied now.` `ReplyMessage.ok` is false only for
  `Not allowed.`, `Out of range`, `No pending change with that id.`, and `Busy, try again.`;
  `ReplyMessage.pending` is the new entry's `Id` when one was created; when a weekend change
  creates two entries it is the `weekendNightsAdd` entry's `Id`. `<days>` are English `DayOfWeek` names in Monday-first order
  joined with `, `. A weekend change whose days all apply now replies `Applied now.` with result
  `Applied`; one whose days all wait (one or two entries) replies `Waits <h> h <m> min.` with result
  `Pending`; only a change with both is `Split`.
- The engine is pure, in Core:
  - `ChangeEngine.Request(GateSettings s, ChangeRequest r, DateTime utcNow, Func<Guid> newId)` and
    `ChangeEngine.Cancel(GateSettings s, Guid id)` return `ChangeOutcome(GateSettings Updated,
    ChangeResult Result, string Text, Guid? PendingId, bool Changed, bool Loosens)`, where
    `Loosens` is true when the request would loosen a rule (whether it waits or, with cooldown 0,
    applies now) and false for a tightening, a `Cancel`, a partly refused change (result `Applied`
    with a `Not allowed:` part), and a `Same`, `NotInList`, or `Refused` result; `ChangeResult` is
    `Applied | Pending | Split | Same | NotInList | Refused` and `Text` is one of the
    `ChangeReply` constants. The caller passes `newId`, so the engine makes no random values; the
    engine calls it once per entry it creates, so two entries from one weekend change get different
    `Id`s.
  - `Changed` is true whenever `Updated` differs from the input in any rule value or in `Pending`
    (a new entry, or an entry removed by `Cancel`); it is false for `Same`, `NotInList`, and
    `Refused`. So a cancelled loosening is saved on that tick and cannot come back after a restart.
  - A successful `Cancel` returns `Applied`; an unknown id returns `Refused`.
  - `ChangeEngine.Countdown(GateSettings s, double seconds)` returns `GateSettings`.
  - `ChangeEngine.ApplyDue(GateSettings s)` returns `(GateSettings Updated,
    IReadOnlyList<PendingChange> Applied, IReadOnlyList<PendingChange> Skipped)`; the worker logs both
    lists. `ApplyDue` adds `weekendNightsAdd` days to the set and takes `weekendNightsRemove` days out
    of it. A `notCountedAdd` whose folder is already on the list (`OrdinalIgnoreCase`) applies as a
    no-op. Neither list ever holds a duplicate.
  - 0007 calls `Request` on its own copy of the snapshot's settings as a dry run and chooses the
    verdict line in the Rules editor from the direction of the change, as the preview
    does (the not-counted, turn-off, and removal panels keep their own lines from the
    preview; Trevor approves all status-screen text on 0007's preview): a fully applied
    tightening (`Applied` with `Loosens` false and no `Not allowed:` part) shows "This tightens the
    rule. It applies now."; a loosening (`Loosens` true, `Pending` or `Applied`) shows "This loosens
    the rule. It applies <time> (<cooldown> h)." A `Same` result shows "No change yet." and a cooldown above
    168 shows "The cooldown can be at most 168 h (7 days).", both as on the preview page.
    0007 maps `Split`, a partly refused change, and every other result
    to verdict text Trevor approves on a preview in 0007. This issue adds no screen text of its own;
    it only records which preview line each result maps to.
  - `GateSettings` gains `Pending` (default empty), `AllowUninstall` (default false), and `Clock`
    (`LockClock?`, default null), all additive (0002's schema rule); `LockClock(DateTime?
    LastSavedUtc, long? LastSavedUptimeMs, Guid? BootId)` keeps the countdown bookkeeping apart from
    Trevor's limits. `SettingsStore`'s check also refuses any top-level value outside the allowed
    ranges in "Allowed values" (for example `cooldownHours` above 168, a night limit above 1440, a weekly cap above
    10080, or more than 50 not-counted folders), so a hand edit cannot set a year-long cooldown. It
    also covers `Pending`: it refuses a null entry, an
    entry whose `Field` is not one of the pending field names, whose value member does not match
    its field, or whose value is out of range, so such a file takes the corrupt path; `ApplyDue`
    never throws.
- Requests arrive over the 0005 pipe as `request` and `cancel` messages from any connection whose
  first line was a valid hello (role `helper` or `status`). `ConnectionRules` allows these two types
  after hello; a client `reply`, a second `hello`, or a `requestId` over 64 characters drops the
  connection; a connection that is not a verified helper (0005), from which the server reads no
  complete line for 10 minutes, is dropped (a one-shot `CancelAfter` on its read, restarted on each line), so idle clients
  cannot hold every slot.
- Every pipe instance is created with `PipeOptions.Asynchronous`, so a pending read or write can
  be cancelled. One bounded `Channel<SettingsCommand>` (capacity 32) is registered as a singleton in `Program.cs`;
  `PipeServer` takes its writer and `GateWorker` its reader by constructor. A full channel answers
  `Busy, try again.` at once. A `SettingsCommand` (`src/SteamGate.Core/Rules/`) is
  `SettingsCommand(string RequestId, ChangeRequest? Request, Guid? CancelId, OutboundQueue Replies,
  int ClientPid, int SessionId)`, with exactly one of `Request` or `CancelId` set; the pid comes from
  `GetNamedPipeClientProcessId` and the session from `GetNamedPipeClientSessionId`; the server never impersonates the client and never
  hands the worker a connection. `OutboundQueue` takes the connection's existing cancel source (the one `Connection.Drop()` in
  `PipeServer` already cancels, so there is one way to end a connection, not two); when `TryPost`
  fails it cancels that source (ignoring `ObjectDisposedException`, because the worker can post a reply after the connection ended and disposed it; a value test: `TryPost` on a closed queue whose source is disposed returns false and does not throw), and the connection already passes its token to every pipe read and
  write, so even a stuck write ends at once.
- The worker holds one in-memory `GateSettings`, loaded at start (settings are loaded once and not re-read from
  disk while running, as today; a test that needs different settings uses `Reset-SteamGate`, which restarts the
  service). Inside `RunTickAsync`, in this order: `Countdown` existing entries by `StepSeconds`; drain
  the channel and call the engine for each command; `ApplyDue`. The matcher (0003), `Budget` (0004),
  and the planner's `enabled` (0005) all read that same object later in the same tick, so a change
  that applies takes effect in that tick.
- Saving: save on any tick where an outcome's `Changed` was true or `ApplyDue` applied or skipped anything, and on every
  12th tick while anything is pending (to record the countdown). Updating `Clock` alone is not a change. A failed save logs `save failed
  settings.json` and is retried every tick; replies for a tick are posted after that tick's save
  attempt, and if it failed the text of each reply whose outcome had `Changed` true ends with
  ` Saved in memory; will retry.`
- Logging: `request <field> <result> pid=<client pid> session=<session id>` and
  `cancel <result> pid=<client pid> session=<session id>`, where `<result>` is the lower-case
  `ChangeResult` or `busy`; and `applied <field>` when a pending
  entry applies (the value is logged only for the numeric, boolean, and day-name fields, never for a folder name). `<field>` is always one of the fixed field names the engine knows (anything else
  is logged as `unknown`), and no client-supplied text (folder names included) is logged, so a
  client cannot forge a line. Request, cancel, and `busy` lines go through the `LogLimiter`
  pattern from 0005 (a shared per-minute cap of 20 lines, then one `request log suppressed` line
  a minute), so a client loop cannot push earlier events out of the rotated log. `PipeServer`
  writes the `busy` lines itself when the channel is full, through the same limiter.
- Any signed-in account can send requests. A loosening waits the cooldown whoever asks and a
  tightening only makes rules stricter, so no request can loosen a rule early. Another local account
  can tighten every rule at once, fill the pending list, or cancel Trevor's pending entries; that
  annoys Trevor but loosens nothing, and is recorded as accepted.
- The snapshot (`GateSnapshot`) gains `Settings` (`GateSettings?`, init-only, default null; readers
  treat null as not yet loaded). 0007 adds the pipe message that sends it to the status screen.
- Uninstall: `Uninstall-SteamGate.ps1`, right after its admin check and before it clears recovery,
  checks whether the SteamGate service is installed. With no service it cleans up without the check
  below (an admin can remove the service anyway, so this opens no new gap). With the service, it
  reads `%ProgramData%\SteamGate\settings.json` inside its own try/catch and refuses unless the
  file parses and `$s.PSObject.Properties['allowUninstall']` exists with a `.Value` that
  `-is [bool]` and is `$true` (the script runs under `Set-StrictMode`, so the property is never read
  directly). A missing folder, missing file, unreadable file,
  invalid JSON, or a missing `allowUninstall` property all refuse. It prints
  `Removal is not allowed yet. Request it in SteamGate; it applies after the cooldown (until its status screen ships, an admin sets allowUninstall to true in settings.json and runs the uninstall right away).` and exits 1.
  There is no `-Force` switch. This script is the one shipped reader of `settings.json` outside
  `JsonFileStore<T>` (the CI-only `Test-Install.ps1` also edits it); it sees the last saved state, so a change held only in memory after a failed
  save does not count. Removing SteamGate by hand as an admin remains the documented admin-teardown
  gap. The test helper `Reset-SteamGate` (0003) does not call this script. After `sc delete`, the
  uninstall removes `HKLM:\SOFTWARE\SteamGate` (the parent of the volatile boot key) with
  `Remove-Item -Recurse -Force`, and `Reset-SteamGate` does the same; a missing key is not an
  error.
- Two differences from the preview page, both forced by the written spec text (the preview is not a record of Trevor's approval of these behaviors) and both reported in the
  run's hand-off note: (1) the preview made a mixed weekend-day change wait as a whole, but the spec
  says a tightening applies right away, so safe removals apply now and the rest waits; (2) the
  preview applied every removal now, but a removal that would raise a night's limit (weekend limit
  below the weeknight limit, now or after a pending change) is a loosening and waits. Also reported:
  an added weekend night waits even when it lowers that night's limit (weekend limit below the
  weeknight limit), because the spec names adding a weekend night a loosening.
- Before 0007 ships, no shipped tool can send a request, so an admin unlocks removal by setting
  `"allowUninstall": true` in `settings.json` by hand (inside the admin-teardown gap) and running the
  uninstall right after; the service does not re-read the file, so its next save (any change, or every
  minute while something is pending) writes its own copy back over the edit.
- Decision logic (classification, splitting, ranges, the step and countdown, pending handling, reply
  texts, connection rules) lives in Core under the Stryker gate.

## Acceptance criteria
A named section runs from its heading to the next heading of the same or higher level. Unless stated,
tests use `now = 2026-10-20T21:00:00Z` and default settings (except weekend nights, where a criterion states them; cooldown 48 h = 172800 s).

1. **Given** weeknight 90, **When** a request sets `weeknightMinutes` to 60, **Then** settings show 60
   at once, nothing is pending, and the reply is `Applied now.` (Test
   `ChangeEngineTests.LowerLimitAppliesNow`.)
2. **Given** weeknight 90, **When** a request sets `weeknightMinutes` to 120, **Then** settings still
   show 90, one pending entry exists with `RemainingWaitSeconds` 172800 and `Number` 120, and the
   reply is `Waits 48 h 0 min.`; `Countdown(172799)` then `ApplyDue` leaves 90; one more
   `Countdown(1)` then `ApplyDue` gives 120 and no pending entries. (Test
   `ChangeEngineTests.RaiseWaitsCooldown`.)
3. **Given** cooldown 48, **When** a request sets `cooldownHours` to 24, **Then** it is pending with
   `RemainingWaitSeconds` 172800; **when** a request sets it to 72, **Then** it applies now; **when** a
   request sets it to 169, **Then** it is refused, settings are unchanged, and the reply is
   `Out of range: 0 to 168.`; **with cooldown 0**, a request setting `weeknightMinutes` to 120
   applies now with `Applied now.`, `Result` `Applied`, and `Loosens` true; and, among the earlier requests made at cooldown 48, the one setting the
   cooldown to 72 has `Loosens` false and the one setting it to 24 has `Loosens` true. (Test
   `ChangeEngineTests.CooldownRules`.)
4. **Given** weeknight 90, weekend 180, and weekend nights Friday and Saturday, **When** a request
   sets them to Saturday and Sunday, **Then** Friday is removed at once (weekend nights now Saturday
   only), one pending entry has `Field` `weekendNightsAdd` and `Days` [Sunday], and the reply is
   `Applied now: Friday. Waits 48 h 0 min: Sunday.`; after `Countdown(172800)` and `ApplyDue`, weekend
   nights are Saturday and Sunday. (Test `ChangeEngineTests.WeekendNightsSplit`.)
4b. **Given** weeknight 120, weekend 60, and weekend nights Friday and Saturday, **When** a request
   sets them to Saturday, **Then** weekend nights still contain Friday and one pending entry has
   `Field` `weekendNightsRemove` and `Days` [Friday]; **given** weeknight 90 and weekend 90 and
   nothing pending, the same request applies now; **and given** weeknight 90, weekend 90, and a
   pending `weekendNightMinutes` 180, **When** a request adds Wednesday, **Then** Wednesday is
   pending, not applied; **and given** weeknight 90, weekend 180, a pending `weekendNightMinutes` 150,
   and a pending `weeknightMinutes` 200, **When** a request removes Friday, **Then** the removal is
   pending (weekend 150 or 180 is below weeknight 200); **and given** weeknight 90 and weekend 60,
   **When** a request adds Wednesday, **Then** it is pending; **and given** weeknight 120, weekend 60,
   and weekend nights Friday and Saturday, **When** a request sets them to Saturday and Sunday,
   **Then** a `weekendNightsAdd` [Sunday] and a `weekendNightsRemove` [Friday] entry exist with
   different `Id`s, and `Cancel` with the remove entry's `Id` leaves the add entry. (Test
   `ChangeEngineTests.WeekendChangesJudgedByEffect`.)
5. **Given** the default not-counted list, **When** `notCountedAdd` `Viewfinder` is requested,
   **Then** it is pending; **when** `notCountedRemove` `viewfinder` is then requested, **Then** the
   reply is `Not in the list.` and the pending add stays; **when** `notCountedRemove`
   `blender` (lower case) is requested, **Then** `Blender` is gone at once; **when**
   `notCountedRemove` `Nope` is requested, **Then** the reply is `Not in the list.`; **when**
   `notCountedAdd` `OBS Studio` is requested, **Then** the reply is `Already set.`; **when**
   `notCountedAdd` `a\b` is requested, **Then** the reply is `Not allowed.`; **and given** two
   pending `notCountedAdd` `Viewfinder` entries, after `Countdown(172800)` and `ApplyDue`,
   `Viewfinder` is on the list exactly once. (Test `ChangeEngineTests.NotCountedRules`.)
6. **Given** `enabled` true, **When** `enabled = false` is requested, **Then** it is pending; with
   `enabled` false, `enabled = true` applies now. **Given** `allowUninstall` false, **When**
   `allowUninstall = true` is requested, **Then** it is pending; after `Countdown(172800)` and
   `ApplyDue`, `AllowUninstall` is true, and likewise a pending `enabled = false` leaves `Enabled`
   false. (Test
   `ChangeEngineTests.DisableAndUninstallWait`.)
7. **Given** weeknight 90 and a pending raise of `weeknightMinutes` to 120, **When** a request sets
   `weeknightMinutes` to 60, **Then** 60 applies now and the pending raise is still there; **when** a
   request sets it to 60 again, **Then** the reply is `Already set.`, `Changed` is false, and the
   raise is still there; **and given** weeknight 90, a raise to 120 requested at `now` and a raise to
   100 requested one minute later, **When** `Countdown(172800)` and `ApplyDue` run, **Then** weeknight
   is 100 and nothing is pending. (Test `ChangeEngineTests.PendingEntriesStayUntilCancelled`.)
7b. **Given** 50 pending entries and weeknight 90, **When** a request sets `weeknightMinutes` to 60,
   **Then** the reply is `Applied now.`; **when** a request sets it to 120, **Then** the reply is
   `Not allowed.`; **and given** 49 pending entries, weeknight 120, weekend 60, and weekend nights
   Friday and Saturday, **When** a request sets them to Saturday and Sunday, **Then** nothing new is
   pending and the reply is `Not allowed.`; **and given** 50 pending entries, weeknight 90, weekend
   180, and weekend nights Friday and Saturday, **When** a request sets them to Saturday and Sunday,
   **Then** Friday is removed at once, nothing new is pending, the reply is
   `Applied now: Friday. Not allowed: Sunday.`, `Result` is `Applied`, `Changed` is true, and
   `Loosens` is false. (Test `ChangeEngineTests.FullPendingNeverBlocksTightening`.)
7c. **Given** 49 folders on the list and one pending `notCountedAdd`, **When** another
   `notCountedAdd` is requested, **Then** the reply is `Not allowed.`; **given** 50 folders and a
   pending add (as after a hand edit), after `Countdown(172800)` and `ApplyDue`, `Skipped` holds that
   entry, `Pending` is empty, and the list still has 50; **and given** cooldown 0 and 50 folders,
   **When** a `notCountedAdd` is requested, **Then** the reply is `Not allowed.` and the list still
   has 50. (Test `ChangeEngineTests.NotCountedListFull`.)
8. **Given** a pending entry, **When** `Cancel` runs with its id, **Then** it is removed at once, the
   reply is `Applied now.`, `Result` is `Applied`, `Changed` is true, and settings are otherwise
   unchanged; **with** an unknown id, **Then** nothing changes, `Result` is `Refused`, `Changed` is
   false, and the reply is `No pending change with that id.` (Test
   `ChangeEngineTests.CancelAppliesNow`.)
9. **Given** cooldown 48 and a pending `weeknightMinutes` 120 entry, **When** the cooldown is
   lengthened to 72 (applies now), **Then** the existing entry keeps its `RemainingWaitSeconds`.
   (Test `ChangeEngineTests.LengtheningDoesNotMoveExistingEntries`.)
10. **Given** weeknight 90 and nothing pending, **When** a request sets `weeknightMinutes` to 90 or to
   1441, **Then** nothing changes; the replies are `Already set.` and `Out of range: 0 to 1440.`
   (Test `ChangeEngineTests.SameValueAndRange`.)
10b. **Given** settings with a pending `weekendNightsAdd` entry (`Days` [Sunday]) and a pending
   `allowUninstall` entry, **When** `SettingsStore.Save` then `Load` run, **Then** each loaded entry's
   `Id`, `Field`, `Number`, `Flag`, `Folder`, `RequestedUtc`, and `RemainingWaitSeconds` equal the
   saved one's, its `Days` sequence-equals the saved `Days`, and `Clock`'s `LastSavedUtc`,
   `LastSavedUptimeMs`, and `BootId` equal the saved ones; **given** `{"weeknightMinutes":75}`,
   `Pending` is empty and `AllowUninstall` is false; **given** `{"pending":[null]}`,
   `Corrupt == true`, and likewise for a pending entry whose `field` is `bogus`; **given**
   `{"cooldownHours":169}`, `Corrupt == true`; **given** `{"cooldownHours":168}`,
   `Corrupt == false`. (Test
   `SettingsStoreTests.PendingRoundTrips`.)
10c. **Given** `StepSeconds` while running, **When** uptime moved 5 s, **Then** it returns 5 whether
   the clock moved 5 s, 3 days forward, or 1 h back; **when** uptime moved backward, **Then** 0;
   **given** the first tick on the same boot (`sameBoot` true) with uptime 3600 s
   past `lastSavedUptimeMs` and `utcNow` 3 days after `lastSavedUtc`, **Then** 3600; **given** the first tick after a Windows restart (`sameBoot` false) with
   `lastSavedUtc` 1 h before `utcNow`, **Then** 3600; with `lastSavedUtc` after `utcNow`, **Then** 0;
   with `lastSavedUtc` null, **Then** 0; on the same boot with `lastSavedUptimeMs` null, **Then** 0.
   (Test `ChangeEngineTests.StepSeconds`.)
11. **Given** the lines
   `{"type":"request","requestId":"r1","field":"weeknightMinutes","number":120}`,
   `{"type":"request","requestId":"r3","field":"weekendNights","days":["saturday","Sunday"]}` (`days`
   is a JSON array of `DayOfWeek` names compared `OrdinalIgnoreCase`),
   `{"type":"cancel","requestId":"r2","id":"<a guid>"}`, and
   `{"type":"reply","requestId":"r1","ok":true,"message":"Waits 48 h 0 min.","pending":"<a guid>"}`,
   **When** `PipeMessage.Parse` reads them, **Then** it returns `RequestMessage`, `CancelMessage`, and
   `ReplyMessage` with those values and round-trips them; `DayNames.TryParse` accepts
   `["saturday","Sunday"]` and refuses `["6"]` and `["7"]`; **and given** a connection whose hello was
   `status`, `ConnectionRules` allows `request` and `cancel` after hello, drops `warn`, `close`,
   `reply`, a second `hello`, and a `requestId` of 65 characters, and drops a connection that is not a verified helper (an unverified `helper`
   role included) idle for 10 minutes. (Tests `PipeMessageTests.RequestCancelReplyRoundTrip`,
   `DayNamesTests.NamesOnly`,
   `ConnectionRulesTests.StatusMaySendRequests`, `ConnectionRulesTests.IdleStatusDropped`, which uses `ConnectionRules.IdleLimit`, 10 minutes, next to `HelloTimeout`.)
12. **Given** the CI Windows runner, after `Reset-SteamGate @{}`, **When** `PipeProbe.ps1` sends a
   `weeknightMinutes` 120 request with `requestId` `r1` and the default 48 h cooldown, **Then** within
   10 seconds it receives a `reply` echoing `r1` whose message starts `Waits 48 h`, `settings.json`
   (read after up to 70 seconds, the save interval) holds 90 and one pending entry, and `Test-Install.ps1`
   prints `PENDING OK`; **when** it then sends `weeknightMinutes` 60, **Then** within 10 seconds the
   reply is `Applied now.`, `settings.json` holds 60 and still the one pending raise, and
   `Test-Install.ps1` prints `TIGHTEN OK`. `PipeProbe.ps1` retries its connection for up to 10
   seconds.
12b. **Given** the CI Windows runner, after `Reset-SteamGate @{}` then `New-FakeSteamRoot` (0003),
   **When** the script waits up to 30 seconds for the first `libraries <count>` line written after the
   restarted service's `started pid=` line, `PipeProbe.ps1` requests `weeknightMinutes` 0 and
   `weekendNightMinutes` 0, both replies are `Applied now.`, and the script then starts
   `FakeGame.exe -n 600 127.0.0.1`, **Then** within 90 seconds `service.log` contains
   `polite close FakeGame` and `Test-Install.ps1` prints `TIGHTEN ENFORCED`; it ends with
   `Remove-FakeSteamRoot`.
13. **Given** the CI Windows runner, after `Reset-SteamGate @{}` (so `allowUninstall` is false),
   **When** `Test-Install.ps1` runs `Uninstall-SteamGate.ps1`, **Then** it exits 1, prints the refusal
   message, the service is still running, and the script prints `UNINSTALL REFUSED`; **and** after the
   test removes the `allowUninstall` property from `settings.json` (writing a temp file and moving it
   over), the uninstall again prints the refusal, and with `"allowUninstall": "true"` (a string) it
   prints the refusal again, and with `settings.json` replaced by `{` it prints the refusal again.
   Then the test runs
   `Reset-SteamGate @{ cooldownHours = 0 }`, sends a pipe request for `allowUninstall = true`, waits up
   to 15 seconds for the reply `Applied now.`,
   runs the uninstall again, and prints `UNINSTALL UNLOCKED` once it succeeds and
   `Test-Path HKLM:\SOFTWARE\SteamGate` is false. It exits non-zero at any
   failed check.
14. **Given** `Test-Install.ps1`, **When** a reader greps it, **Then** it defines a function
   `Invoke-TestUninstall` that runs `Clear-Recovery`, ends only the `SteamGate.Service` process (not
   `Stop-SteamGateProcesses`, which would also end the helper) and waits for it, sets `allowUninstall` to `true` in `settings.json` when that file
   exists (so the service cannot save over the edit), and then runs `Uninstall-SteamGate.ps1`, and every uninstall the script runs outside the criterion 13 group (the 0002 uninstall group, the
   `finally` blocks of the 0003, 0004, and 0005 groups, the 0005 criterion 14 group, and groups 15
   and 16) goes through it, so no `Invoke-Script ... Uninstall-SteamGate.ps1` call outside that
   function and the criterion 13 group remains; the criterion 13 group runs the script directly with
   `&` and checks the exit code itself, because it needs refusals (exit 1) and a final success.
   The 0005 criterion 14 group still starts the helper before it uninstalls.
15. **Given** `DESIGN.md`, `README.md`, and `WHAT-IT-CHECKS.md`, **When** a reader greps them, **Then**
   DESIGN.md's `## SteamGate service design (orchestrator decisions, not part of the approved spec)`
   contains `How the lock works`, `ChangeEngine`, `168`, `calendar time`, `Channel<SettingsCommand>`,
   `allowUninstall has waited out the cooldown`, and `10 minutes`; its `One file store` bullet
   contains `Uninstall-SteamGate.ps1` and `a bad pending entry`; its `Additive settings only` bullet
   contains `out-of-range values`; and DESIGN.md no longer contains
   `and the uninstall script removes it.` or `Every JSON file goes through`; README.md's `## Install` contains `requested in SteamGate` and `"allowUninstall": true`
   and no longer contains `To remove SteamGate, run`; and WHAT-IT-CHECKS.md contains
   `uninstall is refused until`; DESIGN.md contains neither `Issue 0002 builds no channel` nor
   `there is no idle timeout`, and its `## CI` contains `uninstall is refused`.

## Implementation plan
1. Create in `src/SteamGate.Core/Rules/`: `ChangeRequest.cs`, `DayNames.cs`, `PendingChange.cs` (the typed record in
   Design decisions), `ChangeReply.cs` (with the reply-text constants), `SettingsCommand.cs`, and
   `ChangeOutcome.cs`, `ChangeResult.cs`, `LockClock.cs`, and `ChangeEngine.cs` (`Request`, `Cancel`,
   `StepSeconds`, `Countdown`, `ApplyDue`) implementing the
   rules in Design decisions; ranges and the 168-hour maximum are named constants.
2. In `src/SteamGate.Core/GateSettings.cs`, add `Pending`, `AllowUninstall`, and `Clock`. In `src/SteamGate.Core/SettingsStore.cs`, extend the check
   to the top-level ranges and to `Pending` as in Design decisions, and update its comment to
   "(a null list item, a value outside its allowed range, or a bad pending entry)". In `src/SteamGate.Core/GateSnapshot.cs`, add
   `Settings` (`GateSettings?`, init-only, default null).
3. In `src/SteamGate.Core/Pipe/PipeMessage.cs`, add `RequestMessage` (`requestId`, `field`, and the
   value members `number`, `flag`, `folder`, `days`), `CancelMessage` (`requestId`, `id`), and
   `ReplyMessage` (`requestId`, `ok`, `message`, optional `pending` Guid). In
   `src/SteamGate.Core/Pipe/ConnectionRules.cs`, apply the connection rules in Design decisions. In
   `src/SteamGate.Core/Pipe/OutboundQueue.cs`, make `OutboundQueue` take the connection's cancel source and cancel it (ignoring `ObjectDisposedException`, as `Connection.Drop` does) when `TryPost` fails; update the `new OutboundQueue()` calls in `OutboundQueueTests`.
4. In `src/SteamGate.Service/Program.cs`, register the `Channel<SettingsCommand>` singleton and one shared request-log limiter (used by the worker and the pipe server). In
   `src/SteamGate.Service/PipeServer.cs`, write each request into the channel with that connection's
   `OutboundQueue`, client pid, and session id; answer `Busy, try again.` when full; end a connection
   whose queue cancelled its source.
5. In `src/SteamGate.Service/GateWorker.cs`, inside `RunTickAsync` (no new loop or timer), follow the
   tick order and save rules in Design decisions, open or create the volatile boot key at start, record `Clock` on each save, pass the one in-memory settings object to the matcher, `Budget`, and the
   planner, write the log lines, and set `Settings` on the snapshot.
6. In `install/Uninstall-SteamGate.ps1`, add the service-present check, the refusal check, and the
   registry cleanup in Design decisions; add the same registry cleanup to `Reset-SteamGate` in
   `install/Test-Install.ps1`. Extend `install/PipeProbe.ps1` with two optional parameters, `-Line` (one extra line to send after
   its `status` hello) and `-ReplyFile` (where to write the first `reply` line it receives); it already
   retries for `-WaitSeconds`, and without the new parameters it behaves exactly as today (writing
   `CONNECTED <sid>` to `-OutFile`).
7. Add the tests in criteria 1 to 11 (7b and 7c included) under `tests/SteamGate.Tests/Rules/`,
   `tests/SteamGate.Tests/Pipe/`, and `tests/SteamGate.Tests/SettingsStoreTests.cs`, plus further
   value-asserting tests on the new Core code as needed to keep the 80% Stryker break threshold. In
   `install/Test-Install.ps1`, add `Invoke-TestUninstall` (criterion 14) and use it in every place the
   script uninstalls (the uninstall group, the `finally` blocks of the earlier groups, and group 14), and add the
   criterion 12, 12b, and 13 groups as groups 15, 16, and 17 after the existing group 14; groups 15 and
   16 end with `Invoke-TestUninstall` (group 17 ends with its own successful uninstall, after which
   `Test-Path` on the data folder is false); each exits non-zero at any failed check.
8. Update the documents this change makes false or that must describe it. Also: in `DESIGN.md`'s
   "One tick" bullet, change the sentence listing the per-connection timers to name both the one-shot
   hello timeout and the 10-minute idle limit restarted on each line; update the comment in
   `PipeServer.cs` that says there is no idle limit after the hello; update `ConnectionRulesTests`
   `ClientsMaySendOnlyHello` to the new rule (a second `hello` drops the connection); update the
   "a later issue makes them follow changes" comment in `GateWorker`:
   - `DESIGN.md` `## SteamGate service design (orchestrator decisions, not part of the approved spec)`:
     add a "How the lock works" paragraph (classification by effect including the weekend-night rule
     against every mix of current and pending limits, the split rule, ranges and the 168-hour maximum, the calendar time
     countdown with its clock-jump guard and the reboot note, pending entries and cancelling,
     `Channel<SettingsCommand>` and replies, `ChangeEngine` in Core, why any account may send
     requests, and the uninstall refusal). In the "Stop is refused, Shutdown is accepted" bullet,
     replace "and the uninstall script removes it." with "and the uninstall script removes it once
     allowUninstall has waited out the cooldown." Replace "Issue 0002 builds no channel; the first
     issue whose server hands a settings or tally change to the worker creates it." with a sentence
     saying 0006 created
     `Channel<SettingsCommand>` (capacity 32), which carries only a Core `OutboundQueue`, never a
     connection. In the `One file store` bullet, replace "Every JSON file goes through
     `JsonFileStore<T>`." with "Inside the service and Core, every JSON file goes through
     `JsonFileStore<T>`. The one other shipped reader is install/Uninstall-SteamGate.ps1, which reads
     allowUninstall from the last saved settings.json and refuses on any read or parse failure." In
     the same bullet, replace "a null list item or a negative limit;" with "a null list item, a value
     outside its allowed range, or a bad pending entry;" (keeping the tally clause). In
     "How closing works", replace the sentence that describes the idle rule after hello (whatever
     its wording) with one saying that after a valid hello, a connection that is not a verified
     helper is dropped after 10 minutes with no complete line; if no such sentence exists, add it, and note that `request` and `cancel` are allowed
     after hello. Add that before 0007 an admin unlocks removal by hand. In `## CI`, add that
     uninstall is refused until allowed. In the `Additive settings only` bullet, after "so an
     upgrade never takes the corrupt path", add: "except for out-of-range values, which only a hand
     edit can produce; those take the corrupt path on the upgrade that adds the range check (0006)".
   - `README.md` `## Install`: replace "To remove SteamGate, run `Uninstall-SteamGate.ps1` the same
     way." with "Removal must first be requested in SteamGate's status screen; it is allowed after the
     cooldown. Then run `Uninstall-SteamGate.ps1` the same way. Until the status screen ships, an admin can
     allow removal by editing `%ProgramData%\SteamGate\settings.json` to set `"allowUninstall": true`
     and then running `Uninstall-SteamGate.ps1` right away (SteamGate rewrites the file on its next
     save)."
   - `WHAT-IT-CHECKS.md`: one plain bullet: "CI proves an uninstall is refused until removal is
     allowed, and unit tests prove allowing removal waits the full cooldown, a loosening change waits,
     and a tightening change applies at once." 

## Out of scope
- The screen that sends these requests (0007). Admin hand-edits of `settings.json` are the documented
  admin-teardown gap; a clock moved forward before a Windows restart is covered by the clock note
  above.
