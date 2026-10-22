# Issue #0005: Enforcement (warnings, polite close, force close)

**Type:** ready. **Category:** product feature; adds new components (enforcement planner, the
invisible helper, the service-to-helper pipe, the helper launcher, the process killer) and changes
DESIGN.md, so it is a system-level change.
**Review bar:** system-level change; two independent reviewers, both PASS; reviewer-documentation on
the DESIGN.md, README.md, CLAUDE.md, WHAT-IT-CHECKS.md, and docs/approved-spec-2026-10-20.md changes; plus the architecture gate at issue time and the
design-philosophy gate at PR time.
**GitHub:** #10.
**Depends on:** 0002, 0003, 0004, 0008. (Trevor approved the preview page, overlay included, on
2026-10-21, passed on from the main session.)
**Blocks:** 0006, and the status-screen issue (0007 in PLAN.md, not yet filed).
**Touches:** `src/SteamGate.Core/Enforcement/**` (new), `src/SteamGate.Core/Pipe/**` (new),
`src/SteamGate.Core/GateSnapshot.cs`, `src/SteamGate.Service/PipeServer.cs` (new),
`src/SteamGate.Service/HelperLauncher.cs` (new), `src/SteamGate.Service/ProcessKiller.cs` (new),
`src/SteamGate.Service/ProcessStartTime.cs` (new, shared with `ProcessScanner`),
`src/SteamGate.Service/ProcessImage.cs`, `src/SteamGate.Service/ConsoleSession.cs`, `src/SteamGate.Core/SteamGatePaths.cs` (the trusted helper path, shared by the pipe server, launcher, killer, and scanner),
`src/SteamGate.Core/Pipe/HelloLogLimiter.cs`, `src/SteamGate.Core/Pipe/PipeContract.cs` (new), `tests/SteamGate.Tests/TempFolder.cs`,
`src/SteamGate.Service/ProcessScanner.cs`, `src/SteamGate.Service/GateWorker.cs`,
`src/SteamGate.Service/Program.cs`, `src/SteamGate.Helper/**` (new project), `SteamGate.sln`,
`tests/SteamGate.Tests/Enforcement/**` (new), `tests/SteamGate.Tests/Pipe/**` (new),
`tests/SteamGate.Tests/Service/**` (new), `tests/SteamGate.Tests/StrykerRun.cs` (new), `src/SteamGate.Core/Steam/LocalPathPolicy.cs`, `src/SteamGate.Core/Steam/PolicyRoots.cs`, `tests/SteamGate.Tests/Steam/**`,
`install/SteamGate.Common.ps1`, `BUILDLOG.md` (the 0008 line), `install/Test-Install.ps1`,
`install/PipeProbe.ps1` (new, CI only), `.github/workflows/ci.yml`, `DESIGN.md`, `README.md`, `WHAT-IT-CHECKS.md`, `CLAUDE.md` (Product
outcomes, Known gaps),
`docs/approved-spec-2026-10-20.md` (amendment appended), `issues/0001-north-star.md` (criterion 10 string and two summary lines), `docs/preview-2026-10-21.html` (new), `issues/0005-enforcement.md`.

## User story
As Trevor, I need SteamGate to warn me with an on-screen overlay at 30, 20, 10, 5, and 1 minutes left, then ask the game
to close at the limit and force it closed if it does not, and to close any game I start after the
limit, so that the limit holds even when I am deep in a game and not watching the clock.

## Approved spec this issue carries
"Enforcement (warnings at 10/5/1 min with sound, polite close then force, close any game started
after the limit)." "A small invisible helper runs in the user's own login and shows the warnings and
does the polite close, because services can't draw on the screen or talk to game windows. The
service does the counting and the force-close. Each warning plays a sound too, since Windows mutes
notifications during full-screen games." "No window, tray icon, or Alt-Tab entry" for the service.
Amendment (Trevor, 2026-10-21, passed on from the main session): "It looks good, but I think I need an
earlier warning. Like a 30-minute and 20-minute and 10-minute and 5-minute. Like, I need all of those.
Because games are played in rounds. If I get into a round and it's like, hey, there's 20 minutes left
and I would get kicked in the middle of a round, that's not good." The warning set becomes 30, 20, 10,
and 5 minutes; the original 1-minute warning stays because he did not drop it.
Amendment 2 (Trevor, 2026-10-21, passed on from the main session): "No sound, I just need an
overlay." Warnings make no sound. Each is an overlay drawn on top of the game: always on top, never
takes focus or keyboard and mouse input, and fades out on its own. Known gap: an overlay cannot draw
over a game in exclusive full-screen mode, only borderless or windowed (Trevor has been told).

## Design decisions (orchestrator)
- Planner (pure, in Core): `EnforcementPlanner.Step(PlannerState state, double remainingSeconds,
  IReadOnlyList<RunningGame> openGames, bool enabled, TimeSpan monotonicNow)` returns
  `(PlannerState Next, IReadOnlyList<EnforcementAction> Actions)`. Actions are `Warn(int Minutes)`,
  `PoliteClose(RunningGame Game)`, and `ForceClose(RunningGame Game)`, always carrying the
  `RunningGame` from the current step's `openGames`. The worker calls it once per `RunTickAsync` with
  the `RemainingSeconds` and games from the tick's `GateSnapshot` (0003, 0004), `enabled` from its
  current in-memory settings, and a `Stopwatch` time, and keeps the returned state.
  `remainingSeconds` NaN means no actions; the worker passes NaN when the snapshot's
  `RemainingSeconds` is null (not yet computed).
- Warnings: thresholds 30, 20, 10, 5, 1 minutes (1800, 1200, 600, 300, 60 seconds), one named
  constant list. When a game is open and remaining
  seconds is at or below a threshold and above 0, the planner issues one warning for the smallest
  threshold reached that has not been issued yet, and marks it and every larger threshold as issued.
  The warning carries `WarnMinutes.FromRemaining(remainingSeconds)` (whole minutes left, rounded up), which
  equals the threshold when the step lands on it, so the number never exceeds the threshold reached.
  So starting a game with 4 minutes left gives one warning saying 4 minutes, not five warnings. A threshold is
  re-armed when remaining rises above it again (a new night, or a raised limit). When a `Warn` finds no
  verified helper, the worker keeps it and, when the next helper is verified, the worker asks Core: `EnforcementPlanner.ReplayWarn(PlannerState state,
  double remainingSeconds, bool gameOpen)` returns `Warn(WarnMinutes.FromRemaining(remainingSeconds))` only
  when the state still marks a warning undelivered, a game is open, and remaining is above 0, and
  returns null otherwise. The planner clears the undelivered mark when a threshold re-arms, and the
  worker marks a `Warn` undelivered through `EnforcementPlanner.MarkUndelivered(PlannerState)` and
  clears the mark through `EnforcementPlanner.MarkDelivered(PlannerState)` whenever `PostToHelpers`
  returns more than 0 for a `Warn` or a replay. A replay shows the time actually left (whole
  minutes, rounded up).
- Closing, keyed by folder (`OrdinalIgnoreCase`):
  - At remaining 0 (at or below 0), every open game gets `PoliteClose` once; a game still open
    `PoliteGrace` (60 seconds, named constant) later gets `ForceClose`, and gets `ForceClose` again on
    every step while it stays open (so a failed or slow kill is retried).
  - A game first seen while remaining is already 0 (started after the limit) follows the same path
    from the moment it is seen.
  - Per-game close state is kept by folder until remaining rises above 0. A game that closes and
    reopens after its `PoliteClose` was issued gets `ForceClose` at once, so closing and reopening
    cannot buy another 60 seconds.
  - When remaining rises above 0, all close state is dropped (a new night, or a raised limit).
  - Disabled (`enabled == false`): no actions, and close state is dropped.
  - Restart: `PlannerState.Initial(DateTime serviceStartUtc)`. On the first step after start whose
    remaining is not NaN, a game with remaining at or below 0 gets `ForceClose` at once only when every
    one of its processes has `StartUtc` earlier than `serviceStartUtc` (it was open before the
    restart, so its polite close was owed; otherwise repeated kills could keep resetting the 60
    seconds). Any other game takes the normal polite path. A NaN step does not use up this check.
- Force close is done by the service: `ProcessKiller.Kill(int pid, DateTime startUtc)` returns bool.
  It opens the process with `OpenProcess(PROCESS_TERMINATE | PROCESS_QUERY_LIMITED_INFORMATION)`,
  compares the UTC start time from `GetProcessTimes` on that handle (one shared helper,
  `ProcessStartTime`, which `ProcessScanner` also uses; no local-time conversion anywhere) with
  `startUtc`, and only on a match calls `TerminateProcess`. It ends no other process: every process
  under the game's folder is already in the game's list, and one the scan finds later is killed on
  the next step's retry. Failures are logged and never end the service.
- Helper: `SteamGate.Helper.exe`, a WinForms app (`WinExe`) with no taskbar button
  (`ShowInTaskbar = false`), no tray icon, the `WS_EX_TOOLWINDOW`, `WS_EX_NOACTIVATE`,
  `WS_EX_LAYERED`, and `WS_EX_TRANSPARENT` styles so it is not in Alt-Tab and mouse clicks pass
  through it to the game, `ShowWithoutActivation => true`, and topmost set with
  `SetWindowPos(..., SWP_NOACTIVATE)`, so it never takes focus from a game. It is invisible except
  while showing a banner. It takes a named mutex `Local\SteamGate.Helper.<user SID>` at start (one per user, so a second
  user's helper in the same session is not blocked) and exits at once
  if another holds it. On a warn message it shows a topmost overlay with
  `BannerText.ForWarn(n)` that stays fully visible for `OverlayHold` (8 seconds) and then fades out
  over `FadeDuration` (2 seconds) by lowering its opacity, then hides; it makes no sound; on a close message it posts `WM_CLOSE` to every
  top-level window of those process ids (`EnumWindows`, `GetWindowThreadProcessId`, `PostMessage`)
  and shows `BannerText.ForClose(displayName)`. If a game's window is elevated, `PostMessage` can fail;
  the force close still happens. The one-tick rule from 0002 covers the service process; the helper is
  a separate process with its own reconnect loop and banner timer.
- Overlay: `BannerText` (Core) gives `<n> minutes of gaming left.` (`1 minute of gaming left.` when
  n is 1) and `Time is up. Closing <display name>.` The overlay's opacity over time comes from
  `OverlayFade.OpacityAt(TimeSpan sinceShown)` (Core): 1.0 until `OverlayHold`, then falling in a
  straight line to 0 at `OverlayHold + FadeDuration`. No sound is played anywhere. The overlay's look, matching the preview Trevor saw
  (copied to `docs/preview-2026-10-21.html`): a box anchored to the bottom-right of the primary screen
  with a 16 px margin, 6 px rounded corners, padding 12 px by 18 px, background `#141A20` at 94%
  opacity, a small amber dot (`#F0B357`, 10 px) before the text, text `#F2F5F8` in Segoe UI 15 px
  semibold, and a soft shadow. Trevor saw that preview and replaced
  the chime with this silent overlay ("No sound, I just need an overlay."); he approved the
  overlay look, the 8-second hold, and the 2-second fade on 2026-10-21 ("Looks good. Approved.",
  passed on from the main session; recorded here and in a comment on GitHub #10).
  Sizes are 96-DPI pixels: the helper sets fonts in `GraphicsUnit.Pixel` and scales every size by the
  monitor's DPI factor, so the box matches the preview at any display scale.
  Known gaps: a game in exclusive full-screen mode is drawn above every window, so the overlay is not
  seen there; it shows over borderless and windowed games. The overlay is on the primary screen, so a
  game on a second monitor shows it on the other monitor. The helper's drawing, polite close, and
  launch are not machine-checked in CI (the CI runner may have no signed-in user); they are checked
  on the first real install on this PC, and the hand-off note to Trevor says so. The force close still happens either way.
- Pipe `\\.\pipe\SteamGate`, created with `NamedPipeServerStreamAcl.Create` and
  `maxNumberOfServerInstances` 9: at most 8 connected clients plus one listening instance. A listening
  instance exists except while a ninth connection waits up to 10 seconds for its hello; at 9 of 9, an
  accept error disposes the failed instance, then the loop logs `pipe unavailable <error>` and returns,
  and the next tick's `EnsureListening()` starts it again and recreates the instance. An instance created while the server holds no other instance uses
  `PipeOptions.FirstPipeInstance`; instances created while one is held omit it. Connected instances keep the pipe name taken while the service runs. ACL: SYSTEM and
  Administrators full control; authenticated users `ReadData | WriteData | ReadAttributes |
  ReadExtendedAttributes | ReadPermissions | Synchronize` only (never `CreateNewInstance`,
  `WriteAttributes`, or `AppendData`, so no standard user can open a second server); network denied.
  Every SteamGate pipe client (helper, status screen, test probe) opens the pipe with exactly those
  rights through `CreateFile` and wraps the handle in a `NamedPipeClientStream` built from that
  `SafePipeHandle`; it never asks for generic read or write, which the ACL refuses for a standard
  user. The pipe is a convenience for warnings and must never be able to stop enforcement:
  - `PipeServer` exposes `EnsureListening()`, which never throws and starts at most one accept loop
    through `RunAcceptLoopAsync`. An accept error caused by one client drops that instance only after a
    new listening instance exists, except at 9 of 9 as described above. The loop logs `pipe unavailable <error>` and returns, without
    ending the process, when creating an instance fails and when an accept fails at 9 of 9; `RunTickAsync` calls
    `EnsureListening()` on every tick, which is the only retry path (no delay loop or timer of its
    own). Counting and force close keep running.
  - The whole body of `RunAcceptLoopAsync` carries the same wrapper as `GateWorker.ExecuteAsync`
    (log `unhandled pid=<pid> <message>`, then `Environment.Exit(1)`), except that create or accept
    failures take the `pipe unavailable` path above.
  - `PipeServer` exposes `int PostToHelpers(PipeMessage)` (thread-safe; returns how many verified
    helper queues accepted it), `void RecheckHelpers()`, and `bool HelperConnected { get; }`; the worker touches no connection
    object directly.
  - The tick never writes to a pipe. Every connection owns a bounded `OutboundQueue` (Core, wrapping
    `Channel.CreateBounded<PipeMessage>` with capacity 16 and `BoundedChannelFullMode.Wait`; `TryPost`
    is `TryWrite`, false when full, and the connection is then dropped); the tick posts
    to the queues and each connection's own task does the writes.
  - Each line is at most 16 KB, enforced while reading: `PipeLineReader.ReadLineAsync(Stream, int
    maxBytes)` (Core) stops once it passes `maxBytes` without a newline and returns null, and the
    connection is dropped. `PipeMessage.Parse` never throws and returns null for anything malformed or
    of unknown type, and that connection is dropped.
  - A connection whose first complete line (which must be `hello`) does not arrive within 10 seconds
    of connecting is dropped; after a valid hello there is no idle timeout. A first line that is not
    hello, or any `warn` or `close` sent by a client, drops the connection.
  - At most 8 connected clients. When 8 are already connected, a new connection is dropped unless
    its hello is a verified helper's, in which case the oldest connection that is not a verified helper
    is dropped instead. So the ninth instance waits up to 10 seconds for a hello and the listening instance
    always comes back. Verification is rechecked when the
    worker calls `RecheckHelpers()` on every 6th tick; a helper whose session is no longer the active console session loses verified
    status and its connection is dropped (so after a user switch the launcher starts one for the new
    user). For each valid hello the server logs `pipe hello role=<role> pid=<client pid>`.
  - The connection decisions (first line must be hello, the 10-second hello limit, which connection
    to evict) are pure functions in Core (`ConnectionRules`), so they sit under the Stryker gate;
    `PipeServer` only applies their answers. Only verified helpers get warn and close (a plain filter
    in `PostToHelpers`). The server logs at most 20 `pipe hello` lines per minute (then one
    `pipe hello suppressed` line per minute), so a client loop cannot push earlier events out of the
    rotated log. A `helper` hello that fails verification is dropped at once, so the helper retries
    in 5 seconds; `RecheckHelpers` also drops a verified helper that no longer qualifies. The
    helper's connect loop catches every exception except cancellation and retries after 5 seconds.
  - `HelloMessage` carries `role` (`helper` or `status`). A helper hello is verified when the client's
    image path (`GetNamedPipeClientProcessId`, then `QueryFullProcessImageName`) is
    `%ProgramFiles%\SteamGate\SteamGate.Helper.exe` and its session (`ProcessIdToSessionId` of that
    pid) is the active console session; the server derives the session itself and trusts nothing the
    client claims. Warn and close go only to verified helpers.
  - All image-path comparisons use `StringComparison.OrdinalIgnoreCase` after `Path.GetFullPath`.
  - The server changes no settings or tally, so it needs no `Channel<T>` (0006 adds one and reuses
    the per-connection queues for replies).
  - Record types live in `src/SteamGate.Core/Pipe/` so the helper and the status screen (0007) share
    them. The helper connects only when the pipe object's owner, read with `GetSecurityInfo` on its client
    handle (`OWNER_SECURITY_INFORMATION`), is LocalSystem (`S-1-5-18`) or the local Administrators
    group (`S-1-5-32-544`). A process of a standard user that creates the pipe first owns it as
    itself, so the helper refuses it. (A standard user cannot open the LocalSystem service process to
    read its path, so the path is not used on this side.)
- Helper start: on every 6th tick the service checks whether a verified helper is connected; if not
  and a user is signed in at the console, it starts `SteamGate.Helper.exe` in that user's session with
  `WTSGetActiveConsoleSessionId`, `WTSQueryUserToken`, `CreateEnvironmentBlock`, and
  `CreateProcessAsUser` (`STARTUPINFO.lpDesktop = "winsta0\default"`,
  `dwCreationFlags = CREATE_UNICODE_ENVIRONMENT | CREATE_NO_WINDOW`), freeing the block with
  `DestroyEnvironmentBlock` and closing every handle afterward. It logs `helper started session=<id>`
  or `helper not started: no console user`. A killed helper comes back within about 30 seconds, and
  no Run key is needed.
- One-shot per-connection timeouts (the 10-second hello limit) are not periodic and are allowed; no
  repeating timer or delay loop is added to the service.
- A game process that refuses termination (for example a protected anti-cheat process) stays open;
  the kill is retried every tick and logged.
- Stopping for install, upgrade, uninstall, and the test helper `Reset-SteamGate`: end the service process first and wait for it with
  `Wait-Process -Timeout 30`; only then end the remaining processes under `%ProgramFiles%\SteamGate`
  (so the service cannot relaunch a helper in between).
- `GateSnapshot` gains `HelperConnected` (bool, init-only, default false) for 0007.
- Decision logic (planner, line reading, message parsing, outbound queue, banner text, overlay fade) lives in
  Core under the Stryker gate; the service and helper only connect it to Windows.

## Acceptance criteria
A named section runs from its heading to the next heading of the same or higher level.

1. **Given** a game open and remaining seconds going 1900, 1800, 1799, 1200, 700, 600, 599, 300, 60,
   30 on successive steps, **When** the planner runs, **Then** it emits exactly `Warn(30)` at 1800,
   `Warn(20)` at 1200, `Warn(10)` at 600, `Warn(5)` at 300, `Warn(1)` at 60, and nothing at 1900,
   1799, 700, 599, or 30. (Test `PlannerTests.WarnsAt30_20_10_5_1Once`.)
2. **Given** no warnings issued and a game first seen with 240 seconds remaining, **When** the planner
   runs, **Then** it emits exactly one `Warn(4)`; **and given** a first sighting at 1201 seconds it emits exactly
   one `Warn(21)`. (Tests `PlannerTests.LateStartGetsOneWarning`, `PlannerTests.LateStartSaysTimeLeft`.)
2b. **Given** a state marked undelivered by `EnforcementPlanner.MarkUndelivered`, a game open, and remaining 1300,
   **When** `EnforcementPlanner.ReplayWarn` runs, **Then** it returns `Warn(22)`; **given** the same state with
   no game open, or remaining 0, or a state in which a threshold has re-armed since the mark, or a
   state never marked, or a state on which `MarkDelivered` was called after the mark, **Then** it
   returns null. (Test `PlannerTests.ReplayOnlyWhileUndelivered`.)
3. **Given** no game open and 300 seconds remaining, **When** the planner runs, **Then** it emits no
   actions; **given** remaining NaN and a game open, **Then** no actions. (Test
   `PlannerTests.NoGameOrUnknownNoWarning`.)
4. **Given** game `Viewfinder` (pids 20, 21) open and remaining 0 at monotonic time T, **When** the
   planner runs at T, T+30 s, T+59 s, T+60 s, and T+65 s with the game still open, **Then** it emits
   `PoliteClose(Viewfinder)` at T only, `ForceClose(Viewfinder)` at T+60 s, and `ForceClose(Viewfinder)`
   again at T+65 s. (Test `PlannerTests.PoliteThenForceAndRetry`.)
4b. **Given** `PoliteClose(Viewfinder)` at T and remaining 600 at T+30 s, **When** the planner runs at
   T+60 s with the game open, **Then** it emits no `ForceClose`. (Test
   `PlannerTests.PendingCloseDroppedWhenTimeReturns`.)
4c. **Given** `PlannerState.Initial(serviceStartUtc)`, remaining 0, and `Viewfinder` open with every
   process `StartUtc` earlier than `serviceStartUtc`, **When** the planner runs once, **Then** it emits
   `ForceClose(Viewfinder)`; **and** with `Viewfinder`'s process `StartUtc` later than
   `serviceStartUtc`, it emits `PoliteClose(Viewfinder)` and no `ForceClose`; **and** a first step with
   remaining NaN emits nothing and the next step still applies the check. (Test
   `PlannerTests.RestartDoesNotResetGrace`.)
5. **Given** remaining 0 and `Viewfinder` already closed, **When** game `Overwatch` (pid 30) is first
   seen at time U, **Then** the planner emits `PoliteClose(Overwatch)` at U and `ForceClose(Overwatch)`
   at U+60 s if it is still open; **and** if `Overwatch` closes at U+20 s and reopens at U+40 s, it gets
   `ForceClose(Overwatch)` at U+40 s. (Test `PlannerTests.GameStartedAfterLimitIsClosed`.)
6. **Given** remaining 0, a game open, and `enabled == false`, **When** the planner runs, **Then** it
   emits no actions. (Test `PlannerTests.DisabledDoesNothing`.)
7. **Given** a game open and remaining going 600, 7200, 1800, 1200, 600 on successive steps, **When**
   the planner runs, **Then** it emits `Warn(10)` at the first 600, `Warn(30)` at 1800, `Warn(20)` at
   1200, and `Warn(10)` at the second 600. (Test `PlannerTests.WarningsResetWhenTimeComesBack`.)
8. **Given** the line `{"type":"warn","minutesLeft":5}`, **When** `PipeMessage.Parse` reads it,
   **Then** it returns a `WarnMessage` with `MinutesLeft == 5`, and serializing it back gives the same
   JSON; **given** `{"type":"hello","role":"helper"}`, **Then** it round-trips the same way; **given**
   `{"type":"close","game":"Viewfinder","displayName":"Viewfinder","processIds":[20,21]}`, **Then** it
   returns a `CloseMessage` with `ProcessIds == [20, 21]` and round-trips; **given** the lines
   `not json` and `{"type":"nope"}`, **Then** it returns null each time without throwing; **given** a
   stream of 20 000 bytes with no newline, `PipeLineReader.ReadLineAsync(stream, 16384)` returns null
   after reading at most 16 385 bytes; **given** an `OutboundQueue` of capacity 16, the 17th `TryPost`
   returns false. (Tests `PipeMessageTests.WarnRoundTrips`, `PipeMessageTests.HelloRoundTrips`,
   `PipeMessageTests.CloseRoundTrips`, `PipeMessageTests.BadInputReturnsNull`,
   `PipeLineReaderTests.LineCapEnforcedWhileReading`, `OutboundQueueTests.FullQueueRejects`.)
8b. **Given** `BannerText.ForWarn(1)`, **Then** it returns `1 minute of gaming left.`; `ForWarn(5)`
   returns `5 minutes of gaming left.`; `ForWarn(30)` returns `30 minutes of gaming left.`;
   `ForClose("Viewfinder")` returns `Time is up. Closing Viewfinder.`; **and**
   `OverlayFade.OpacityAt` returns 1.0 at 0 s and at 8 s, 0.5 at 9 s, and 0.0 at 10 s and 12 s.
   **and** `OverlayStyle` holds `BackgroundHex` `#141A20`, `Opacity` 0.94, `DotHex` `#F0B357`, `DotSize`
   10, `TextHex` `#F2F5F8`, `FontSize` 15, `Margin` 16, `Radius` 6, `PaddingX` 18, `PaddingY` 12, and
   `DotGap` 12, which are the values in the preview Trevor saw and the helper reads. (Tests
   `BannerTextTests.Wording`, `OverlayFadeTests.HoldThenFade`, `OverlayStyleTests.MatchesApprovedPreview`.)
9. **Given** a child process `ping.exe -n 30 127.0.0.1` started by the test, **When**
   `ProcessKiller.Kill(pid, startUtc)` runs with a `startUtc` one `DateTime` tick (100 ns) off the true value (read with
   `ProcessStartTime`), **Then** it returns false and the child is still running after 2 seconds;
   **When** it runs with the true `startUtc`, **Then** it returns true and the child exits within 5
   seconds. (Test `ProcessKillerTests.StartTimeMismatchNeverKills`, Windows only.)
9b. **Given** `LocalPathPolicy.MovedTargetAllowed` and a target under the Users folder
   (`PolicyRoots.UsersRoot`, the Users folder on the system drive), **When** it runs, **Then** it returns false, while
   a target on another drive still returns true. (Test `LocalPathPolicyTests.UsersFolderTargetRefused`.)
   This stops a junction from making the SYSTEM service end another user's programs. DESIGN.md's
   known gaps say a junction to a folder outside the Users folder and the protected folders makes
   that folder count as a game.
10. **Given** `src/SteamGate.Helper`, **When** a reader greps it, **Then** it contains
   `ShowInTaskbar = false`, `WS_EX_TOOLWINDOW`, `WS_EX_NOACTIVATE`, `ShowWithoutActivation => true`,
   `SWP_NOACTIVATE`, `WS_EX_LAYERED`, `WS_EX_TRANSPARENT`, and `Local\SteamGate.Helper` or
   `Local\\SteamGate.Helper`, and contains none of `NotifyIcon`, `SoundPlayer`, `SystemSounds`, or
   `PlaySound`.
11. **Given** the CI Windows runner, after
   `Reset-SteamGate @{ weeknightMinutes = 0; weekendNightMinutes = 0 }` then `New-FakeSteamRoot`
   (0003), **When** `Test-Install.ps1` waits up to 30 seconds for the first `libraries <count>` line
   written after the restarted service's `started pid=` line (failing on timeout) and then starts `FakeGame.exe -n 600 127.0.0.1`, **Then** within 90 seconds the fake game process has exited,
   `service.log` contains `polite close FakeGame` and, unless the fake game exited on the polite close,
   then `force close FakeGame` (the script prints which of the two ended it), the script prints
   which of `helper started session=` or `helper not started: no console user` appeared, and it prints
   `ENFORCED FakeGame`. It exits non-zero at any failed check and ends with `Remove-FakeSteamRoot`.
12. **Given** the CI Windows runner with the service installed, **When** `Test-Install.ps1` opens
   `\\.\pipe\SteamGate` and sends `not json` on one connection, sends 10 MB with no newline on a
   second, and then connects a third and sends `{"type":"hello","role":"status"}`, **Then**
   `service.log` contains `pipe hello role=status pid=<third client's pid>` within 2 seconds of its
   hello, the service process id is
   unchanged, at least one new `alive` line appears in `service.log` within 70 seconds after the third hello, and the script prints `PIPE OK`.
12b. **Given** the CI Windows runner with the service installed, **When** `Test-Install.ps1` creates a
   temporary local standard user and runs `install/PipeProbe.ps1` as that user (`Start-Process
   -Credential -LoadUserProfile`, writing to a folder the script creates and grants to that user), which opens `\\.\pipe\SteamGate` with the same rights as the helper, sends
   `{"type":"hello","role":"status"}`, and reads the pipe owner through `GetSecurityInfo`, **Then** the probe writes
   `CONNECTED <owner SID>` to a file, the SID is `S-1-5-18` or `S-1-5-32-544`, and the script prints
   `PIPE USER OK` (the user is kept for 12c).
12c. **Given** the temporary standard user from 12b, **When** `Test-Install.ps1` starts the published
   `%ProgramFiles%\SteamGate\SteamGate.Helper.exe` as that user (`Start-Process -Credential
   -LoadUserProfile`) and waits up to 15 seconds, **Then** `service.log` contains
   `pipe hello role=helper pid=<that process's pid>`, and the script prints `HELPER USER OK` and then deletes
   the user. (This proves the real helper's pipe rights work without admin; the helper
   itself does not need to show anything.)
13. **Given** the CI Windows runner, **When** `Test-Install.ps1` ends the service process and,
   retrying every 100 ms within the 5-second recovery delay, creates `\\.\pipe\SteamGate` with one
   allowed instance as the admin runner (acting as a squatter), then holds it through the restart,
   **Then** the new service pid stays Running for 30 seconds, `service.log` contains
   `pipe unavailable`, and the script prints `PIPE SQUAT OK`; it then releases the pipe, and within 15
   seconds `PipeProbe.ps1` run as the admin runner writes `CONNECTED <owner SID>` with the SID `S-1-5-18` or `S-1-5-32-544` and `service.log` shows its `pipe hello` line,
   and the script prints `PIPE RECOVERED`.
14. **Given** the CI Windows runner with the service installed, **When** `Test-Install.ps1` starts
   `%ProgramFiles%\SteamGate\SteamGate.Helper.exe` as the runner and then runs
   `Uninstall-SteamGate.ps1`, **Then** `UNINSTALLED` still prints and `%ProgramFiles%\SteamGate` is
   gone.
15. **Given** `DESIGN.md`, **When** a reader greps
   `## SteamGate service design (orchestrator decisions, not part of the approved spec)`, **Then** it
   contains `How closing works`, `PoliteGrace`, `\\.\pipe\SteamGate`, and `pipe unavailable`, and its
   crash-restart bullet does not contain `exits 1 only when the loop itself fails`; **and**
   `## Repo structure` contains `SteamGate.Helper` and `PipeProbe`, `## CI` contains `SteamGate.Helper`,
   and `WHAT-IT-CHECKS.md` contains `overlay`.
15b. **Given** `ConnectionRules` and `WarnMinutes`, **When** a connection's first line is `warn`,
   **Then** the rule says drop; **when** a first line has not arrived by `ConnectionRules.HelloTimeout` (10 seconds, the value the server's read limit uses), the constant is 10 seconds; **when** 8 clients
   are connected (oldest unverified first) and a verified helper's hello arrives, it picks the oldest
   unverified one to evict; **when** 8 are connected and a ninth sends a `status` hello, the rule says
   drop the ninth; **and** `WarnMinutes.FromRemaining(1300)` returns 22. (Tests
   `ConnectionRulesTests.NonHelloFirstLineDrops`, `ConnectionRulesTests.HelloTimeoutIsTenSeconds`,
   `ConnectionRulesTests.EvictsOldestUnverified`, `ConnectionRulesTests.NinthNonHelperDropped`, `WarnMinutesTests.RoundsUp`.)
16. **Given** `docs/approved-spec-2026-10-20.md`, `DESIGN.md`, and `CLAUDE.md`, **When** a reader
   greps them, **Then** the docs file contains `## Amendment 2026-10-21 (warnings)`, `30, 20, 10, 5, and 1
   minutes`, `No sound`, and `exclusive full-screen`; DESIGN.md's `## SteamGate product spec`
   contains `30, 20, 10, 5, and 1 minutes`, `No sound`, and `exclusive full-screen`; DESIGN.md and
   CLAUDE.md each contain `with dated amendments appended below the original`; CLAUDE.md's `### Product outcomes` contains `30, 20, 10, 5, and 1 minutes`
   and `silent overlay` and contains neither `plays a sound` nor `with a sound`; CLAUDE.md's Known gaps
   section contains `exclusive full-screen`; `README.md` does not contain `arrive in later issues`; `issues/0001-north-star.md` criterion 10 contains `silent overlay` and not `plays a sound`; and `docs/preview-2026-10-21.html` contains `Warning overlay` and `gaming left.`.

## Implementation plan
1. Create `src/SteamGate.Core/Enforcement/EnforcementPlanner.cs`, `PlannerState.cs` (with
   `Initial(DateTime serviceStartUtc)`), and the action records, implementing the rules in Design
   decisions; the thresholds and `PoliteGrace` are named constants.
2. Create in `src/SteamGate.Core/Pipe/`: `PipeMessage.cs` (`HelloMessage` with `role`, `WarnMessage`
   with `minutesLeft`, `CloseMessage` with `game`, `displayName`, `processIds`; `Parse` never throws
   and returns null for malformed or unknown type; `Serialize`), `PipeLineReader.cs`
   (`ReadLineAsync(Stream, int maxBytes)`), `OutboundQueue.cs` (capacity 16, `TryPost`),
   `BannerText.cs` (`ForWarn`, `ForClose`), and `OverlayFade.cs` (`OverlayHold`, `FadeDuration`,
   `OpacityAt`), `OverlayStyle.cs` (the constants in criterion 8b), `ConnectionRules.cs`, `PipeContract.cs` (the pipe name and the client access rights, used by both the server and the helper), and `WarnMinutes.cs`. `PipeMessage` gains `ToLine()` (the serialized message plus the newline, UTF-8), which both sides use to write.
3. Register one `GateLog` instance as a singleton in `Program.cs`; `GateWorker` and `PipeServer` both
   take it from there, so two loggers never append to and rotate `service.log` at once. Create
   `src/SteamGate.Service/ProcessStartTime.cs` (the `GetProcessTimes` read, moved out of
   `ProcessScanner`, which now calls it), `src/SteamGate.Service/ProcessKiller.cs`
   (`Kill(int pid, DateTime startUtc)`, start-time check then `TerminateProcess`, no child-process
   walk), `src/SteamGate.Service/PipeServer.cs` (the pipe rules in Design decisions, including
   `EnsureListening()` and the exit-1 wrapper; registered with `AddSingleton<PipeServer>()` and started
   through `AddHostedService(sp => sp.GetRequiredService<PipeServer>())` in `Program.cs`; `GateWorker`
   receives it by constructor), and `src/SteamGate.Service/HelperLauncher.cs` (the session-launch
   path).
4. In `src/SteamGate.Service/GateWorker.cs`, inside `RunTickAsync` (no new loop or timer): call
   `PipeServer.EnsureListening()`; run the planner (starting from
   `PlannerState.Initial(serviceStartUtc)`); post `Warn` and `PoliteClose` to verified helpers' queues
   (and replay a missed `Warn` as in Design decisions); perform `ForceClose` with `ProcessKiller` for
   each process of the game; on every 6th tick call `PipeServer.RecheckHelpers()`, then run the helper launcher check; set `HelperConnected` on
   the snapshot; log `warn <n>`, `polite close <folder>`, `force close <folder>`, and the launcher lines
   to `service.log`.
5. Create `src/SteamGate.Helper`
   (`net10.0-windows`, WinForms, `WinExe`, self-contained single file in publish): the mutex; a pipe
   client that opens the pipe with the rights in Design decisions, checks the pipe owner, says
   hello with `role` `helper`, and reconnects every 5 seconds; the overlay form with the styles in
   Design decisions, the approved look, and the fade driven by `OverlayFade.OpacityAt` (the helper's
   own timer); `BannerText` from Core; no sound; and the window-close
   code. Add it to `SteamGate.sln`.
6. Publish the helper next to the service in `.github/workflows/ci.yml` (same `out` folder; the
   install script already copies every non-script file). Change the one shared function `Stop-SteamGateProcesses` in
   `install/SteamGate.Common.ps1` (called by `Install-SteamGate.ps1`, `Uninstall-SteamGate.ps1`, and
   `Reset-SteamGate`) to the stop order in Design decisions: end the service process first and wait,
   then end the remaining processes under `%ProgramFiles%\SteamGate` (the helper in every session)
   and wait with `Wait-Process -Timeout 30`. The two scripts then need no change of their own.
7. Add the tests in criteria 1 to 9 and 15b under `tests/SteamGate.Tests/Enforcement/`,
   `tests/SteamGate.Tests/Pipe/`, and `tests/SteamGate.Tests/Service/`, plus further value-asserting
   tests on the new Core code as needed to keep the 80% Stryker break threshold. Create `install/PipeProbe.ps1` (CI
   only; not copied into the artifact). In `install/Test-Install.ps1`, add
   the criterion 11 to 14 groups using `Reset-SteamGate`, `New-FakeSteamRoot`, and
   `Remove-FakeSteamRoot` from 0003; each exits non-zero at any failed check.
8. In `issues/0001-north-star.md`, change criterion 10's required string `plays a sound` to `silent
   overlay`, and its summary lines about warnings and sound to the new times and the silent overlay,
   each followed by "Amended 2026-10-21 (issue 0005)." on the same line or the next, as 0008 did.
   Record the warning amendment: append to `docs/approved-spec-2026-10-20.md` a section
   `## Amendment 2026-10-21 (warnings)` quoting Trevor's words from this issue and stating the new set (30, 20,
   10, 5, and 1 minutes), Amendment 2 (no sound; a silent overlay that stays on top, takes no focus or
   input, and fades out), and the exclusive full-screen known gap, leaving every earlier line unchanged; (0008 already changed the three "pinned unedited" phrases to say "with dated
   amendments appended below the original"; append this amendment below 0008's); in `DESIGN.md`
   `## SteamGate product spec`, add the same amendment text as `### Amendment 2026-10-21 (warnings)` immediately after 0008's
   `### Amendment 2026-10-21 (weekend nights)` (still inside `## SteamGate product spec`), both parts: the new
   times and Amendment 2 (no sound; a silent overlay; the exclusive full-screen gap); copy the
   preview Trevor saw, from
   `C:\Users\thumb\AppData\Roaming\Claude\side-session-notes\local_9d1a5e4a-29cd-4cb2-ae67-ba26dbd1d900\preview-2026-10-21.html`,
   unchanged to `docs/preview-2026-10-21.html`; in `CLAUDE.md`
   `### Product outcomes`, change `10, 5, and 1 minutes` to `30, 20, 10, 5, and 1 minutes`, replace the
   two sound phrases ("Each warning also plays a sound." and ", each with a sound") with "Each warning is
   a silent overlay on top of the game that takes no focus or input and fades out." (one sentence,
   after the helper sentence, not repeated in the warnings bullet), and add the exclusive full-screen gap to
   `### Known gaps (documented, not solved)`.
9. In `DESIGN.md`:
   - In `## SteamGate service design (orchestrator decisions, not part of the approved spec)`, add a
     "How closing works" paragraph: the planner, its thresholds, `PoliteGrace` (60 s), the retry,
     reopen, and restart rules, the start-time check before a kill, the helper (invisible, no Alt-Tab,
     silent overlay that fades out, single instance, the exclusive full-screen gap) and how the service relaunches it, the pipe `\\.\pipe\SteamGate` with its
     rights and limits, and that `pipe unavailable` never ends the service.
   - Rewrite that section's crash-restart bullet to: "Any unhandled error in the worker, or any error
     that escapes a server's own handling, logs and ends the process with exit code 1, so the Windows
     recovery setting restarts it. A server loop isolates per-connection and per-message errors. The
     pipe server logs `pipe unavailable` and the worker retries it each tick; it never exits for that."
   - Change the opening sentence of that section to say `SteamGate.Service` and `SteamGate.Helper`
     only connect Core to Windows, and note that the one-tick rule covers the service process. Also
     change the "One tick" bullet's last sentence to: "The service starts no other periodic loop or
     timer; the helper runs its own reconnect and fade timers, and each pipe connection has a
     one-shot hello timeout."
   - In `## CI` step 5 (the publish step) say both `SteamGate.Service` and `SteamGate.Helper` are
     published to `out`; in step 6 (the install test) add that a game started after the limit is closed and that a bad pipe client cannot stop
     the service. In `## Evals` say Trevor approved the warning overlay look on a preview.
   - In `## Testing`, add `ProcessKiller` to the service classes the tests cover, and say the pipe server is checked by
     the install test (`## CI`), not by xUnit.
   - Record in "How closing works" that while another process holds the pipe name, warnings, the
     polite close, change requests, and the status screen are unavailable; force close and counting
     continue. The orchestrator also reports this gap to Trevor in the run's hand-off note.
   - In `WHAT-IT-CHECKS.md`, add one plain-language bullet that CI proves a game started after the
     limit is closed, add a bullet that the helper's overlay, polite close, and launch are checked on the
     real install and not by CI, and change the not-machine-checked line to say Trevor approves the status
     screen and the warning overlay on previews.
   - In `## Repo structure`, add "and the approved warning overlay preview" to the `docs/` line, add
     `PipeProbe.ps1 (CI-only)` to the `install/` line (and the same in `README.md`'s `install/` row), and list
     `SteamGate.Helper` (the invisible helper in the user's session) on
     the `src/` line. In `README.md`, add a layout row for `src/SteamGate.Helper` and change line 3's "Warnings and
     closing games arrive in later issues" to say warnings and closing games work; criterion 16 checks
     that `README.md` no longer contains `arrive in later issues`.

## Out of scope
- Changing settings and the cooldown (0006); the status screen (0007). The overlay does not show over a game in
  exclusive full-screen mode (documented known gap); force close works regardless.
