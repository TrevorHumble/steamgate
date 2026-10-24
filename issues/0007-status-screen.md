# Issue #0007: Status screen (see where you stand, request changes, edit the not-counted list)

**Type:** ready. **Category:** product feature; adds new components (the status screen program
`SteamGate.Status`, the `state` pipe message, the status text, view, verdict, and mapping logic in
Core, the Start Menu shortcut) and changes DESIGN.md, so it is a system-level change.
**Review bar:** system-level change; two independent reviewers, both PASS; reviewer-documentation on
the DESIGN.md, README.md, WHAT-IT-CHECKS.md, and PLAN.md changes; plus the architecture gate at issue
time and the design-philosophy gate at PR time. Visual-approval note: this issue builds visible output
whose look Trevor approves (orchestrator step 2a). Trevor approved the look and every extras card
in chat on 2026-10-23, pinned as the `(status screen look)` amendment; see "Owner look gate". The
screen's drawing is not machine-checked in CI.
**GitHub:** #17.
**Depends on:** 0004, 0005, 0006, and the owner look gate below. (0006 is committed to `main`
(e6a7824 or later). Every 0006 name this issue cites was checked against e6a7824: `ChangeEngine.Request`,
`ChangeOutcome` (`Loosens`), `ChangeResult` (`Applied`, `Pending`, `Split`, `Same`, `NotInList`,
`Refused`), `ChangeReply`, `ChangeMessages.ToRequest` (returns `ChangeRequest?`), `ChangeFields`,
`DayNames.TryParse`, `SettingsCommand`, `GateSnapshot.Settings`, `GateSettings.Pending` and
`AllowUninstall`, `PendingChange.RemainingWaitSeconds`, `ConnectionRules.DropsOnClientMessage` and
`IdleLimit`, `Invoke-TestUninstall` in `Test-Install.ps1`, and `PipeProbe.ps1`'s `-Line` and
`-ReplyFile`.)
**Blocks:** none.
**Touches:** `src/SteamGate.Core/Pipe/PipeMessage.cs`, `src/SteamGate.Core/Pipe/ConnectionRules.cs`, `src/SteamGate.Core/Rules/ChangeReply.cs` (one shared constant, `NotAllowedPart`),
`src/SteamGate.Core/Status/**` (new), `src/SteamGate.Service/PipeServer.cs`,
`src/SteamGate.Service/GateWorker.cs`, `src/SteamGate.Status/**` (new project),
`src/SteamGate.Helper/PipeClient.cs` (comment only), `src/SteamGate.Core/Pipe/PipeLineReader.cs`
(comment only), `SteamGate.sln`,
`tests/SteamGate.Tests/Status/**` (new), `tests/SteamGate.Tests/Pipe/**`,
`install/SteamGate.Common.ps1`, `install/Install-SteamGate.ps1`, `install/Uninstall-SteamGate.ps1`,
`install/Test-Install.ps1`, `install/PipeProbe.ps1`, `.github/workflows/ci.yml`, `DESIGN.md`,
`README.md`, `WHAT-IT-CHECKS.md`, `PLAN.md`, `BUILDLOG.md` (the 0006 line, carried by this commit), `docs/preview-2026-10-22-extras.html` (new),
`docs/approved-spec-2026-10-20.md` (amendment appended by the orchestrator at the gate),
`issues/0007-status-screen.md`.

## User story
As Trevor, I need a SteamGate window I open from the Start Menu that shows how much gaming time I
have left tonight and this week, which game is being counted, my rules, the changes waiting out the
cooldown, and the not-counted list, and that lets me request rule changes, cancel waiting changes,
add or remove not-counted programs, and request turning SteamGate off or allowing its removal, so
that I can see where I stand and change my limits without editing files as an admin.

## Approved spec this issue carries
"Status screen (see where you stand, request changes, edit the not-counted list)." "Not-counted list
(whitelist), pre-filled with Blender, OBS Studio, Wallpaper Engine, and Steamworks Shared. Editable
from the status screen; adding waits the cooldown, removing applies now." "The status screen is
visual: Trevor approves its look on a preview before it is built." (DESIGN.md
`## SteamGate product spec`, pinned in `docs/approved-spec-2026-10-20.md`.)

## Owner look gate (met 2026-10-23)
History: the main session's build brief said Trevor had approved the status screen look on
`docs/preview-2026-10-21.html` (the status-screen half; the overlay half was approved in 0005), but
at the time the repo held no record of it in his own words, so this issue did not treat the brief as
an approval. It blocked Research and Implementation until Trevor, in chat, did the five things
below. He did so on 2026-10-23 (see "Gate result"), and his words are pinned as the
`(status screen look)` amendment in `docs/approved-spec-2026-10-20.md` and DESIGN.md
`## SteamGate product spec`. The gate items, as they were put to him:

- (a) confirms the status screen look on `docs/preview-2026-10-21.html`,
- (b) approves the extra states and texts on `docs/preview-2026-10-22-extras.html`, which shows every
  screen text or state this issue builds that the 2026-10-21 preview does not show (cards 1 to 11,
  listed under "Extras" below), and
- (c) decides card 12, a workflow change the orchestrator proposes because the 2026-10-21 preview
  never shows that SteamGate is off or that removal is allowed and has no way back from either:
  accept it, change it, or drop it. Building stays blocked until he has decided. If he drops it, the
  two lines and two buttons of card 12 are removed from this issue (Design decisions and criterion
  29) before the build, and the screen keeps the 2026-10-21 preview's controls only; and
- (d) decides card 13, how the status screen is opened, which is his workflow: the proposal is a
  Start Menu shortcut only (no desktop shortcut, no startup entry, no tray icon); the alternatives on
  the card are adding a desktop shortcut, or also opening the screen at sign-in (a startup entry).
  Building stays blocked until he has decided. If he picks anything other than the proposal, the
  "Opening the screen" decision, the install and uninstall steps, and criterion 26 are revised to
  match before the build; and
- (e) decides card 14, the program icon, which is visible on his screen: the proposal is the default
  Windows program icon in the title bar, the taskbar, and the Start Menu shortcut; the alternative is
  a custom SteamGate icon (whose look he would then approve on its own preview). Building stays
  blocked until he has decided. If he picks a custom icon, this issue is revised to add it (the icon
  file, the `ApplicationIcon` in the project, and the shortcut's `IconLocation`) and re-reviewed
  before the build.

**Gate result (2026-10-23).** Trevor, in chat: "looks good my man", after the build session listed the status screen look and the cards and asked him to say what he approves or wants changed. He changed no card, so (a) to (e) are accepted as proposed: Start Menu shortcut only, the default Windows program icon, and the "SteamGate is off" and "Removal is allowed" lines with their buttons. The amendment `(status screen look)` is pinned in `docs/approved-spec-2026-10-20.md` and DESIGN.md `## SteamGate product spec` and commented on GitHub #17.

The amendment and the GitHub #17 comment were made on 2026-10-23 (see Gate result). The preview shows weekend nights "Fri, Sat" because it was made before issue 0008; the shipped default is Friday, Saturday, and Sunday and the screen shows real data, so the preview needs no edit.

### Extras (each is one card on `docs/preview-2026-10-22-extras.html`)
1. The Tonight meter label on a weekend night: `Tonight (weekend night, ends 4 AM)`.
2. The two forms of the "used" line (plain minutes when the limit is not whole hours).
3. Each meter shows its own time left (the week can run out before the night).
4. Not connected: `Can't reach SteamGate.` and every button greyed out after 10 seconds without a
   connection.
5. The reply line: SteamGate's answer at a fixed place in the section that sent the request (below
   the Rules note; below the waiting rows; below the add box; below the turn-off buttons; below the
   card 12 lines); green for `Applied now.` and `Applied now. Saved in memory; will retry.`, amber
   for every other text, as card 5 shows.
6. Refused and partly refused verdicts: the dry run's own text (`Not allowed.`,
   `Applied now: Friday. Not allowed: Sunday.`).
7. Waiting-change row text, written with the Rules list's time format (`1 h 30 min to 2 h`, not the
   first preview's sample `90 min to 120 min`), and the weekend-nights row.
8. A waiting change only a hand edit can make: `<field> <value>`, for example `enabled true`.
9. Day order: Monday first in the Rules list and waiting rows; check boxes Sunday first as the
   preview.
10. A second start: exits silently; the open window is not brought forward.
11. The cooldown filled into the preview's "48 h" lines, and the `Counting now` line hidden when no
    game is counted.
12. (Proposed, gate item (c).) When SteamGate is off, the line `SteamGate is off.` under the heading
    with a `Request turn on` button; when removal is allowed, the line `Removal is allowed.` with a
    `Stop allowing removal` button. Per 0006, `enabled` true and `allowUninstall` false are
    tightenings and apply at once.
13. (Proposed, gate item (d).) How the screen is opened: a `SteamGate` shortcut in the Start Menu
    only. Alternatives: also a desktop shortcut; also open at sign-in.
14. (Proposed, gate item (e).) The program icon: the default Windows program icon in the title bar,
    the taskbar, and the Start Menu shortcut. Alternative: a custom icon.

No other text appears on the screen than the 2026-10-21 preview's status-screen half and these
cards. The reply line shows SteamGate's reply text verbatim, and card 5 lists every text it can be.

## Design decisions (orchestrator)
Decided before this issue was drafted and not reopened here, except where a bullet names a gate item:
how the screen is opened (gate item (d), card 13), the program icon (gate item (e), card 14), and
card 12 (gate item (c)) are Trevor's to decide.

- **Program.** A WinForms app project `src/SteamGate.Status` (`net10.0-windows`, `WinExe`,
  `UseWindowsForms`, `ApplicationHighDpiMode` `PerMonitorV2`, self-contained single file in publish,
  like `SteamGate.Helper`), published by `ci.yml` into the same `out` folder as the service, so
  `Install-SteamGate.ps1` copies `SteamGate.Status.exe` to `%ProgramFiles%\SteamGate` (it already
  copies every non-script file). It runs in the user's own session, started by the user (see
  "Opening the screen"). It has no tray icon. Its icon is the default Windows program icon (gate
  item (e), card 14): the window sets `Icon = SystemIcons.Application`, the project sets no
  `ApplicationIcon`, and the shortcut sets no `IconLocation`, so the title bar, the taskbar, and the
  shortcut all show that icon. It is a normal window
  (taskbar button, Alt-Tab entry, title bar caption `SteamGate`). One instance per user per session:
  it takes the named mutex `Local\SteamGate.Status.<user SID>` at start (`Local\` is per session; the
  SID from `WindowsIdentity.GetCurrent().User`, as the helper does) and a second start exits at once
  with exit code 0 and shows nothing (extras card 10; bringing the open window forward would need a
  cross-process signal and foreground-window rights, so it is not built). Closing the window ends the
  program with exit code 0. It plays no sound.
- **Opening the screen (gate item (d), extras card 13; Trevor's call, built as proposed only if he
  accepts it).** Proposal: the only way in is a Start Menu shortcut named `SteamGate`; no desktop
  shortcut, no startup entry, no Run key. Alternatives on the card: also a desktop shortcut, or also
  a startup entry so the screen opens at sign-in. The shortcut below is the proposal's build.
- **Start Menu shortcut.** `install/SteamGate.Common.ps1` defines `$ShortcutPath` as
  `Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'SteamGate.lnk'` (the All Users
  Programs folder). `Install-SteamGate.ps1` creates it after copying the program files, on both a
  fresh install and an upgrade: it first deletes any existing file at `$ShortcutPath`, then creates
  it through the `WScript.Shell` COM object with `TargetPath`
  `%ProgramFiles%\SteamGate\SteamGate.Status.exe`, `WorkingDirectory` `%ProgramFiles%\SteamGate`,
  and no description (so the shortcut has no tooltip text of its own), and prints
  `INSTALL: Start Menu shortcut created`. `Uninstall-SteamGate.ps1` removes it after it removes the
  program folder (so a removal that 0006 refuses leaves the shortcut in place); a missing shortcut is
  not an error. It prints `UNINSTALL: removed <path>` or `UNINSTALL: <path> not present`, matching
  `Remove-Folder`.
- **Stopping.** `Stop-SteamGateProcesses` already ends every process running from
  `%ProgramFiles%\SteamGate` after the service, so an upgrade or removal also closes an open status
  screen. No change there.
- **The `state` message.** A new record `StateMessage` in `src/SteamGate.Core/Pipe/PipeMessage.cs`,
  type name `state`, parsed by `PipeMessage.Parse` (never throws; null for anything malformed) and
  written with `Serialize` and `ToLine` like the other messages, with value equality over its lists
  (as `CloseMessage` does). JSON members:
  - `remainingSeconds` (number or null): the snapshot's `RemainingSeconds` (the smaller of tonight
    and this week). Carried for the probe and for completeness; neither meter shows it.
  - `tonightLimitMinutes` (int or null): the snapshot's `TonightLimitMinutes`.
  - `tonightWeekend` (bool): whether tonight (`NightClock.NightOf(localNow)`) falls on a day in the
    settings' `WeekendNights`.
  - `nightSeconds` and `weekSeconds` (numbers): the snapshot's tally after
    `NightClock.RollOver(tally, localNow)`, so a tally from an earlier night or week shows 0 used.
  - `counting` (string or null): the display name of the first game in the snapshot's `Games`, or
    null when no game is counted.
  - `helperConnected` (bool): the snapshot's `HelperConnected`. The screen does not draw it; the
    previews have no place for it.
  - `settings`: `weeknightMinutes`, `weekendNightMinutes`, `weeklyCapMinutes`, `cooldownHours`,
    `enabled`, `allowUninstall`, `weekendNights` (English `DayOfWeek` names), `notCounted` (a list of
    `{folder, displayName}`), and `pending` (a list of `{id, field, number, flag, folder, days,
    requestedUtc, remainingWaitSeconds}`, the `PendingChange` members). `LockClock` is not sent.
  - `addable`: a list of `{folder, displayName}`, the installed Steam games (the snapshot's
    `Installed`) whose folder is not on the not-counted list (`OrdinalIgnoreCase`), sorted by display
    name (`OrdinalIgnoreCase`, then folder), at most `StatusState.MaxAddable` (512) entries before
    the byte cap below.
  - One Core function gives every display name on the screen, in the state and in pending rows:
    `StatusState.DisplayName(string folder, IReadOnlyList<InstalledApp> installed)` returns
    `GateSettings.ApprovedNames[folder]` when the folder is one of the four approved programs
    (installed or not), else the installed app's `DisplayName` (`OrdinalIgnoreCase` folder match),
    else the folder name. The screen side uses the same function with the state's `addable` and
    `notCounted` entries as the installed list. Every display name in the message is cut to at most
    `StatusState.MaxNameLength` (128) UTF-16 units on a whole-character boundary: when unit 128 would
    be the first half of a surrogate pair, the cut is at 127, so a pair is never split.
- **Line size, in bytes.** Lines a client sends keep the 16 KB cap (`PipeLineReader.MaxLineBytes`). A
  `state` line can be longer: `StateMessage.MaxLineBytes` is 524288 bytes, not counting the newline, and the
  status screen and the probe read state lines with that cap. `StateMessage.Serialize` writes with
  `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (the other message types keep the default encoder).
  Checked with the .NET 10 SDK on 2026-10-22: with that encoder, `Café Ω 日本` is written as raw
  UTF-8; a character outside the Basic Multilingual Plane is still escaped as a surrogate pair
  (U+1F600, the grinning face emoji, becomes the 12 characters `\uD83D\uDE00`); control characters (`\u0001`) and U+2028 are escaped as 6
  bytes; and a lone surrogate is written as the 6 characters `\uFFFD`. So the safe bound used for every size
  argument is 6 bytes per UTF-16 unit of text. The cap is enforced on the serialized bytes, not on
  entry counts, in one pass: `StatusState.From` serializes the state with an empty `addable`, then
  takes the sorted `addable` entries in order, adding each entry's own serialized byte length (plus
  1 for the separating comma after the first) to the running total, and keeps the longest prefix
  whose total stays at or under `MaxLineBytes`; it serializes once more with that prefix. Without
  `addable` the largest possible state (50 not-counted folders and 50 pending entries per 0006's
  limits, folders of at most 260 units and display names of at most 128 units, all at 6 bytes per
  unit, about 0.2 MB) is under the cap, so the result always fits. The helper never receives
  `state`.
- **Building the state.** `StatusState.From(GateSnapshot snapshot, DateTime localNow)` (Core) returns
  the `StateMessage`, or null while `snapshot.Settings` or `snapshot.RemainingSeconds` is null (not
  yet computed). `StatusState.ToSettings(StateMessage state)` (Core) rebuilds the `GateSettings`
  (with `Pending`, without `Clock`) the screen uses for its dry run.
- **Sending the state, never throwing.** Inside `RunTickAsync` (no new loop or timer), after it
  publishes the snapshot, the worker checks `PipeServer.StatusConnected` (true while at least one
  `status` connection is registered) and only then calls `StatusState.From(published snapshot,
  localNow)` and, when it is not null, `PipeServer.PublishState(StateMessage)`; with no status
  client open no state is built. Both calls sit in one `try`/`catch` in the
  worker that catches every exception except `OutOfMemoryException`, logs
  `state unavailable <message>` once (not again until a tick has published a state successfully), and
  lets the tick continue, so a state failure never stops counting or enforcement and never takes the
  exit-1 path. `PipeServer.PublishState` itself catches and ignores `ObjectDisposedException` during
  shutdown. `PipeServer` holds one `StatusFeed` (Core), which owns the stored latest state:
  `StatusFeed.Offer(StateMessage state)` returns true when the state should be posted: it returns true when the state's serialized text differs from the last one it accepted, and then stores the state and
  remembers its text; `StatusFeed.Latest` is the stored state or null; `StatusFeed.Forget()` drops
  both. `PublishState` calls `Offer` under the server's existing `_lock`, and skips the call when no `status` connection is registered, as read under that same lock (the worker's own `StatusConnected` check, made outside the lock, only saves building a state no one will read; `Forget()` on the last disconnect is what keeps an old state from being sent). On true the server posts the state to the `OutboundQueue` of every
  connection whose hello role was `status`; it never posts it to a helper. When a `status`
  connection is registered, the server posts `StatusFeed.Latest` (if any) to that connection's
  queue, under `_lock`. When the last `status` connection ends, the server calls
  `StatusFeed.Forget()` under `_lock`. So a client that connects after a gap never gets an old
  state: it gets its first state on the next tick, within 5 seconds. Because registering, posting
  `Latest`, forgetting, and `PublishState` all run under `_lock`, a connection registered while a
  state is published gets either that state from `PublishState` or the same state from
  registration, never an older one after a newer one. So a status client
  gets the state right after its hello (or within 5 seconds when it is the only one) and then at
  most once per 5-second tick, only when something changed. The tick still never writes to a pipe, and a
  status client whose queue is full is dropped by the existing rule. `PipeServer` records each
  connection's hello role so it can choose the `status` connections.
- **Who can read it.** Any signed-in account that can open the pipe (0005's rights) can say hello as
  `status` and read the state: the limits, the tally, the counted game's name, the not-counted list,
  pending changes, and the installed game names. On Trevor's PC that reveals nothing he does not
  already see, and it is recorded here as accepted, like 0006's "any signed-in account can send
  requests".
- **A client never sends `state`.** `ConnectionRules.DropsOnClientMessage` drops a connection that
  sends a `state` line after hello (0006's rule already drops everything except `request` and
  `cancel`; criterion 15 pins it).
- **Connecting.** The status screen opens the pipe exactly as the helper does: it compiles the
  helper's `src/SteamGate.Helper/PipeClient.cs` into its own project through a linked source file
  (`<Compile Include="..\SteamGate.Helper\PipeClient.cs" Link="PipeClient.cs" />`), so there is one
  copy of the open-with-these-rights and pipe-owner check (SYSTEM or Administrators only). It sends
  `{"type":"hello","role":"status"}` and reads lines with `PipeLineReader.ReadLineAsync(stream,
  StateMessage.MaxLineBytes, token)` on a background task, handing each parsed message to the window
  thread with `BeginInvoke`. When the connection ends (the service restarted, the pipe was busy, or
  0006's 10-minute idle limit dropped this client because it sent nothing), it reconnects every 5
  seconds, as the helper does, and keeps the last state on screen. Because the screen gets a fresh
  state within 5 seconds of every hello, a reconnect needs no other handling.
- **Not connected (extras card 4).** When the screen has had no open connection for 10 seconds in a
  row (so a quick reconnect never shows it), it shows `Can't reach SteamGate.` under the title in the
  verdict wait style and disables every button on the screen: each `Change` and `Close` button in
  the Rules rows, `Request change`, each `Cancel`, each `Remove`, the add box's `Request`,
  `Request turn off`, `Request removal`, and the card 12 buttons `Request turn on` and
  `Stop allowing removal`. It keeps the last numbers on screen and leaves an open editor open. Before
  the first state arrives only the title and that line show.
- **Request while not connected.** A request made while the screen has no open connection (before
  the 10-second not-connected rule greys the buttons out) is not sent, and nothing new is shown; the
  section's reply line keeps what it showed. The reply line only ever shows SteamGate's own reply
  text.
- **Requests.** The screen sends 0006's `request` and `cancel` messages with request ids `s1`, `s2`,
  and so on (always under 64 characters). It builds each `RequestMessage` through `StatusRequests`
  (Core) and builds the dry-run `ChangeRequest` from that same `RequestMessage` through 0006's
  `ChangeMessages.ToRequest(RequestMessage)` (Core, `src/SteamGate.Core/Pipe/ChangeMessages.cs`,
  the same call `PipeServer` makes; it returns `ChangeRequest?`, and null is the case the server
  answers `Not allowed.`, which the screen shows as that verdict). So the screen judges exactly what the service
  will judge. Day names are sent
  as English `DayOfWeek` names. Number boxes are limited to 0 to 1440 for the two night limits and 0
  to 10080 for the weekly cap (0006's allowed ranges), and to 0 to 9999 for the cooldown, so the
  preview's over-168 cooldown line can still show.
- **Reply line (extras card 5).** The `message` of the `reply` with the matching `requestId` shows on
  one line in the verdict style, until the same section sends its next request. Its color comes from
  the text, by `StatusText.ReplyIsNow(string text)` (Core): the text with a trailing
  ` Saved in memory; will retry.` removed (once, if present) is compared with `Applied now.`; equal
  gives the "now" colors, anything else the "wait" colors. That reproduces every sample on card 5:
  `Applied now.` and `Applied now. Saved in memory; will retry.` are green; `Waits 48 h 0 min.`,
  `Waits 48 h 0 min. Saved in memory; will retry.`, `Applied now: Friday. Waits 48 h 0 min: Sunday.`,
  `Applied now: Friday. Not allowed: Sunday.`, `Already set.`, `Not in the list.`,
  `No pending change with that id.`, `Out of range: 0 to 1440.`, `Not allowed.`, and
  `Busy, try again.` are amber (criterion 12b). Its place is fixed per section:
  - `RULES` (a `Request change`): below the rules note, the last thing in the section.
  - `WAITING TO APPLY` (a `Cancel`): below the waiting rows (or below `Nothing waiting.`).
  - `NOT COUNTED` (a `Remove` or the add box's `Request`): below the add box, after its panel line.
  - `TURN OFF OR REMOVE` (`Request turn off` or `Request removal`): below the row of buttons and
    their note.
  - Card 12 (`Request turn on` or `Stop allowing removal`): below the card 12 lines, above the meters.
  The screen shows the reply's `message` verbatim and adds no words of its own. From 0006's
  `ChangeReply` (read on `main` at e6a7824) and the engine that uses it, the texts a
  reply can carry are exactly: `Applied now.`; `Waits <h> h <m> min.`; `Applied now: <days>. Waits
  <h> h <m> min: <days>.`; `Applied now: <days>. Not allowed: <days>.`; `Already set.`;
  `Not in the list.`; `No pending change with that id.`; `Out of range: <min> to <max>.`;
  `Not allowed.`; `Busy, try again.`; and any of the first four with ` Saved in memory; will retry.`
  added at the end (when the service's save failed). `<days>` are full English day names, Monday
  first, joined with `, ` (for example `Friday, Sunday`). Extras card 5 shows every one of these. If
  a later change to `ChangeReply` adds or changes a text, card 5 is updated and re-approved before
  the build.
- **SteamGate off, removal allowed (extras card 12, gate item (c); built only if Trevor accepts it).**
  When the state's `settings.enabled` is false, the line `SteamGate is off.` shows under the heading
  in the verdict wait style with a `Request turn on` button on the same line, which sends
  `enabled` true. When `settings.allowUninstall` is true, the line `Removal is allowed.` shows the
  same way with a `Stop allowing removal` button, which sends `allowUninstall` false. Per 0006 both
  requests are tightenings and apply at once (reply `Applied now.`). When both hold, both lines show,
  off first. Nothing else changes; the `TURN OFF OR REMOVE` section keeps its preview buttons.
- **Verdict line in the Rules editor (0006's mapping).** `VerdictLine.For(GateSettings current,
  ChangeRequest request, DateTime utcNow, TimeZoneInfo zone)` (Core) runs
  `ChangeEngine.Request(current, request, utcNow, () => Guid.Empty)` as a dry run (its result is
  thrown away; nothing is sent) and returns `(string Text, VerdictKind Kind, bool CanRequest)`, where
  `VerdictKind` is `Now` or `Wait` (the preview's two verdict colors):
  - A cooldown request above 168, checked before the dry run: `The cooldown can be at most 168 h (7
    days).`, `Wait`, false (preview line).
  - `Same`: `No change yet.`, `Wait`, false (preview line, shown in the wait colors as the preview
    does).
  - `Applied` with `Loosens` false and no `Not allowed:` part: `This tightens the rule. It applies
    now.`, `Now`, true (preview line).
  - `Loosens` true (`Pending` or `Applied`), and `Split` (the preview showed any weekend-nights change
    that adds a day in the wait line): `This loosens the rule. It applies <time> (<cooldown> h).`,
    `Wait`, true (preview line), where `<time>` is `utcNow` plus the cooldown, converted to `zone`,
    formatted as the preview does (`ddd, MMM d, h:mm tt`, en-US) and `<cooldown>` is the current
    `CooldownHours`.
  - `Refused` and a partly refused weekend change (`Applied` with a `Not allowed:` part), extras card
    6: the dry run's own `Text`, `Wait`, and `CanRequest` false for `Refused`, true for the partly
    refused change.
- **Panel lines (preview text, cooldown filled in, extras card 11).** The not-counted panel shows
  `Adding a program loosens the rules. It waits <cooldown> h.` and the turn-off panel
  `Both wait <cooldown> h.` (the preview's lines with its 48 replaced by the current
  `CooldownHours`). Each not-counted row shows the note `Removing applies now`. The rules note is
  `Loosening a rule waits the cooldown. Tightening a rule applies now.` An empty pending list shows
  `Nothing waiting.`
- **Text and formatting, all in Core** (`src/SteamGate.Core/Status/StatusText.cs`, en-US):
  - `Minutes(int m)`: the preview's `fmtMin`: under 60 is `<m> min`; otherwise `<h> h` when
    `m % 60 == 0`, else `<h> h <r> min`.
  - The two big numbers: Tonight is `Left(max(0, tonightLimitMinutes * 60 - nightSeconds))` and This
    week is `Left(max(0, weeklyCapMinutes * 60 - weekSeconds))`. Neither is the snapshot's
    `RemainingSeconds` (extras card 3). `Left(double seconds)` is `Minutes` of the whole minutes left
    rounded up (`WarnMinutes.FromRemaining`, so the screen and the overlay agree), shown with a
    smaller `left` after it as the preview does. When `tonightLimitMinutes` is null the Tonight meter
    shows the weeknight or weekend-night limit for `tonightWeekend`.
  - `Used(double usedSeconds, int limitMinutes)` (extras card 2): used minutes rounded down. When
    `limitMinutes % 60` is not 0, `<used> of <limit> min used` in plain minutes; otherwise
    `<Minutes(used)> of <Minutes(limit)> used`. This reproduces both preview samples (`47 of 90 min
    used`, `6 h 10 min of 10 h used`).
  - `FillPercent(double usedSeconds, int limitMinutes)`: `round(100 * used / limit)` clamped to
    0..100 (midpoint away from zero), and 100 when the limit is 0.
  - `TonightLabel(bool weekend)`: `Tonight (weeknight, ends 4 AM)` (preview) or `Tonight (weekend
    night, ends 4 AM)` (extras card 1). `WeekLabel` is `This week (resets Monday 4 AM)`.
  - `CountingNow` is `Counting now: ` followed by the game's name in bold, after the preview's green
    dot. When `counting` is null the whole line is hidden (extras card 11).
  - `Days(IEnumerable<DayOfWeek>)` (extras card 9): three-letter names (`Mon` to `Sun`) joined with
    `, ` in Monday-first order. The weekend-nights editor's check boxes keep the preview's order,
    `Sun Mon Tue Wed Thu Fri Sat`.
  - `AppliesAt(DateTime local)`: `Applies <ddd, MMM d, h:mm tt>`; `In(double seconds)`: the preview's
    `left()`: hours = `round(seconds / 3600)` (midpoint away from zero); 24 or more is
    `in <h / 24> d <h % 24> h`, else `in <h> h`. A pending row's end time is `DateTime.UtcNow` plus its
    `RemainingWaitSeconds`, converted to local time.
  - Rule row values: `Minutes` for the three limits, `Days` for weekend nights, `<n> h` for the
    cooldown.
- **Pending row text** (`PendingText.For(PendingChange entry, StateMessage state)`, Core, extras
  cards 7 and 8): `Weeknight limit: <current> to <new>`, `Weekend night limit: ...`,
  `Weekly cap: ...` (both values with `Minutes`), `Cooldown: <current> h to <new> h`,
  `Weekend nights: <current days> to <days after>` (the current set with the entry's days added for
  `weekendNightsAdd` or taken out for `weekendNightsRemove`, both with `Days`),
  `Stop counting <display name>` (`notCountedAdd`; preview text; the name from
  `StatusState.DisplayName` with the state's `addable` and `notCounted` entries, so an approved folder
  such as `wallpaper_engine` reads `Wallpaper Engine` even when it is not installed),
  `Turn SteamGate off` (`enabled` false; preview text), and `Allow SteamGate to be removed`
  (`allowUninstall` true; preview text). `<current>` is the setting's value in the same state. A
  pending entry the engine never creates (`enabled` true or `allowUninstall` false; per 0006 these are
  tightenings and apply at once, so one exists only after an admin hand edit; a pending
  `notCountedRemove` cannot exist at all, because 0006 marks that field not pendable and refuses such
  an entry when it loads the settings)
  shows `<field> <value>` in the JSON spelling (`enabled true`), with its `Cancel` button.
- **An incoming state never disturbs editing.** The window keeps an `EditorState` (Core record in
  `src/SteamGate.Core/Status/StatusView.cs`): the open Rules editor's field (or null), the typed
  number, the checked days, the chosen Add folder, and each section's reply line.
  `StatusView.Merge(EditorState editor, StateMessage next)` returns the editor state to use with the
  new state: it never closes an open editor and never changes the typed number, the checked days, or
  a reply line; it keeps the chosen Add folder while that folder is still in `next.addable`, and
  otherwise picks the first `addable` folder (null when the list is empty). After a state arrives the
  window recomputes only the shown numbers, lists, and the open editor's verdict (against the new
  settings). It updates existing controls' text in place and never recreates the focused control or
  resets the scroll position; it rebuilds a list's rows only when
  `StatusView.RowsChanged(previousKeys, nextKeys)` is true (keys: pending entry ids, not-counted
  folders, `addable` folders, compared in order), and keeps the scroll position when it does.
- **The window, frozen to the preview.** It builds the status-screen half of
  `docs/preview-2026-10-21.html` exactly, plus the extras cards, and adds nothing else. It does not
  ship the preview's `Sample data. Clicking works on this preview; nothing is saved.` note, its fake
  title bar (the real Windows title bar, caption `SteamGate`, takes its place), its page background
  and window frame, or anything from the overlay half. Top to bottom:
  - Heading `SteamGate` and, on the same line at the right, the `Counting now:` line with its dot.
    Under them, when they apply, the card 12 lines and their reply line.
  - Two meters side by side (one column when the window is narrower than 560 px): the label, the big
    remaining text with a small `left`, the bar filled to `FillPercent`, and the `Used` line.
    Tonight uses `tonightLimitMinutes` and `nightSeconds`; this week uses `weeklyCapMinutes` and
    `weekSeconds`.
  - `RULES` (section headings upper case): rows `Weeknight limit`, `Weekend night limit`,
    `Weekend nights`, `Weekly cap`, `Cooldown`, each with its value and a `Change` button that opens
    the row's editor and reads `Close` while it is open (one editor open at a time). A number editor
    has a number box, the unit note `min`, or for the cooldown `h (most 168 h, which is 7 days)`, a
    primary `Request change` button, and the verdict line, updated on every edit, with
    `Request change` enabled only when `CanRequest` is true. The weekend-nights editor has the check
    boxes. Pressing `Request change` sends the request and closes the editor, as the preview does.
    Under the rows the rules note.
  - `WAITING TO APPLY`: one row per pending entry: the `PendingText` line with the `AppliesAt` note
    under it, the `In` pill, and a `Cancel` button that sends `cancel` with the entry's `id`; or
    `Nothing waiting.`
  - `NOT COUNTED`: one row per not-counted folder (display name, the note `Removing applies now`, and
    a `Remove` button that sends `notCountedRemove` with the folder); under the rows the add box:
    `Add a Steam program:`, a drop-down of `addable` display names, a `Request` button that sends
    `notCountedAdd` with the chosen folder (disabled when the list is empty), and the not-counted
    panel line.
  - `TURN OFF OR REMOVE`: `Request turn off` (sends `enabled` false), `Request removal` (sends
    `allowUninstall` true), and the note `Both wait <cooldown> h.`
  - Turning SteamGate back on and withdrawing allowed removal are only the card 12 buttons; if
    Trevor drops card 12 there is no such control, and a waiting request can still be cancelled.
- **Style values.** `src/SteamGate.Core/Status/StatusStyle.cs` (Core) holds every value the
  2026-10-21 preview's CSS (and, for disabled buttons, the extras page's `button[disabled]`) gives
  the status-screen parts the window draws, so one value-asserting
  test pins all of them as `OverlayStyleTests` does for the overlay. The window reads every color,
  size, padding, gap, and radius from `StatusStyle` and has no number of its own for these. The full
  list (96-DPI pixels; the window's own layout limits that the preview CSS does not give are also held there: minimum window 420 by 240, add box 160 to 420 wide with 40 extra, its arrow 10, at most 10 visible rows, the weekend-night check boxes drawn in the browser's default look the preview shows (13 px square, 2 px corners, 1 px border, margins 4 px left and 3 px elsewhere, light and dark colors and tick colors, all in `StatusStyle`), the drop-down list's 1 px border, its chevron's width share (one third of the arrow) and minimum stroke (1.5 px), an 8 px gap beside the big number, and an 8 px gap above the reply lines of the Waiting, Not counted, and Turn off sections, `ReplyGapAbove`; the Rules reply line sits under the rules note's own 12 px margin):
  - Light colors: `Win #fbfcfd`, `Line #d8dee5`, `Fg #1b232c`, `Muted #5d6b79`, `Accent #2c6a8a`,
    `AccentSoft #e2eef4`, `Wait #9a5b00`, `WaitSoft #fbf0dd`, `Now #2d7a3e`, `NowSoft #e3f2e6`,
    `Bar #d3dbe3`. Dark colors: `Win #1a2027`, `Line #2c353f`, `Fg #e6ebf0`, `Muted #98a6b4`,
    `Accent #6db3d6`, `AccentSoft #1f3340`, `Wait #f0b357`, `WaitSoft #3a2c16`, `Now #74c88a`,
    `NowSoft #1b3323`, `Bar #2c353f`. The window's client area is `Win`.
  - Text: `BodyFont 14`; `TitleFont 22` semibold (display font); `SectionFont 12` semibold, upper
    case, `SectionLetterSpacing 0.06` em, `SectionGapBelow 8`; `CountingFont 13`; `LabelFont 13`;
    `BigFont 30` semibold (display font), `BigLineHeight 1.1`; `BigSmallFont 14` regular;
    `SubFont 12`; `NoteFont 12`; `ButtonFont 13`; `InputFont 13`; `VerdictFont 13`; `PillFont 12`;
    `DayLabelFont 13`; `EmptyFont 13`; rule values (`.row .val`) semibold, `RowValueSemibold true`;
    `CountingNameSemibold true` (the bold game name).
  - States: `DisabledOpacity 0.5` (a disabled button is drawn at half opacity, as the extras page's
    `button[disabled]`).
  - Layout: `ContentWidth 760`; `NarrowWidth 560`; `ContentPaddingTop 20`, `ContentPaddingX 24`,
    `ContentPaddingBottom 24`, `NarrowContentPadding 16`; `SectionGap 24`; `HeadGap 12`;
    `DotSize 8`, `DotGap 6`; `MeterGap 16`, `MeterPaddingY 14`, `MeterPaddingX 16`,
    `MeterInnerGap 8`, `MeterRadius 6`; `TrackHeight 6`, `TrackRadius 3`; `RowsRadius 6`,
    `RowGap 12`, `RowPaddingY 10`, `RowPaddingX 14`; `BorderWidth 1`; `ButtonPaddingY 5`,
    `ButtonPaddingX 12`, `ButtonRadius 4`; `FocusOutline 2`, `FocusOffset 1`; `EditorPaddingY 10`,
    `EditorPaddingX 12`, `EditorRadius 4`, `EditorGap 8`, `EditorLineGap 8`, `AddBoxMarginTop 8`, `HeadLinesGap 8` (the card 12 lines, the
    extras page's `.stack`);
    `InputPaddingY 4`, `InputPaddingX 8`, `InputRadius 4`, `NumberInputWidth 80`;
    `VerdictPaddingY 6`, `VerdictPaddingX 10`, `VerdictRadius 4`; `PillPaddingY 2`,
    `PillPaddingX 8`, `PillRadius 10`; `DaysGap 6`, `DayLabelGap 4`; `DangerGap 8`;
    `EmptyPaddingY 12`, `EmptyPaddingX 14`.
  - Fonts: `Segoe UI Variable Text` (body) and `Segoe UI Variable Display` (title and big numbers),
    falling back to `Segoe UI`. Button borders are `Line`, turning `Accent` on hover; the primary
    button is filled `Accent` with `Win` text; the focus outline is `Accent`.
  - Numbers: the meters' big text, the `Used` line, the rule values, and the pill use tabular
    figures (`font-variant-numeric: tabular-nums` in the preview), `TabularNumbers true`; the window
    draws them with a font feature or a fixed-width digit layout so digits line up.
  - Start size: `StartWidth 760`, `StartHeight 860`.
  - Paragraph spacing: the rules note is a paragraph (`<p class="note">`) with the browser's default
    margin of 1 em at its 12 px size, `NoteParagraphMargin 12` above and below.
  - Not used, because the real window replaces them: the preview's `--bg` page color, its `.window`
    frame (border and 8 px radius), its fake title bar, and its sample note.
  The window sets fonts in `GraphicsUnit.Pixel` and scales every size by the monitor's DPI factor, as
  the helper does. The dark colors are used when Windows' app mode is dark
  (`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`, `AppsUseLightTheme` 0), the
  Windows counterpart of the preview's `prefers-color-scheme`, and the title bar is then switched to
  dark with `DwmSetWindowAttribute` (`DWMWA_USE_IMMERSIVE_DARK_MODE`). The window opens with a
  client area of `StartWidth 760` by `StartHeight 860` (96-DPI pixels, scaled like every size, and
  each limited to 90% of the monitor's working area), is resizable, and scrolls vertically when its content is taller than the
  window.
- **Drawing is not machine-checked.** The CI install test proves the program starts, connects, keeps
  one copy per user per session, and closes cleanly, and unit tests prove every text, number, style
  value, and verdict; no CI check proves what the window looks like. The run's hand-off note says so.
- **Decision logic in Core under the Stryker gate:** the `state` message, `StatusState`,
  `StatusFeed`, `StatusText`, `PendingText`, `VerdictLine`, `StatusRequests`, `StatusView`, and
  `StatusStyle`. `SteamGate.Status` only connects them to Windows (the pipe, the window, the theme).

## Acceptance criteria
A named section runs from its heading to the next heading of the same or higher level. Unless stated,
tests use default settings (weeknight 90, weekend night 180, weekend nights Friday, Saturday, and
Sunday, weekly cap 600, cooldown 48, the four approved not-counted folders, nothing pending).

0. **Given** `docs/approved-spec-2026-10-20.md`, DESIGN.md, and this issue, **When** a reader greps
   them, **Then** the docs file contains a heading that ends with `(status screen look)` and whose
   body names both `preview-2026-10-21.html` and `preview-2026-10-22-extras.html` and quotes Trevor
   (`looks good my man`); DESIGN.md's `## SteamGate product spec` contains `(status screen look)`;
   and this issue's "Owner look gate" section quotes the same words and records his decisions on
   cards 12, 13, and 14 (accepted as proposed). This held on 2026-10-23; it is a check, not a step.
1. **Given** `StatusText.Minutes`, **When** it runs on 45, 60, 90, 600, 0, and 230, **Then** it
   returns `45 min`, `1 h`, `1 h 30 min`, `10 h`, `0 min`, and `3 h 50 min`. (Test
   `StatusTextTests.Minutes`.)
2. **Given** the meter functions, **When** they run, **Then** `Left(2580)` and `Left(2579)` return
   `43 min`, `Left(13800)` returns `3 h 50 min`, `Left(0)` returns `0 min`; `Used(2820, 90)` returns
   `47 of 90 min used`, `Used(1800, 75)` returns `30 of 75 min used`, `Used(22200, 600)` returns
   `6 h 10 min of 10 h used`, `Used(5400, 180)` returns `1 h 30 min of 3 h used`;
   `FillPercent(2820, 90)` is 52, `FillPercent(22200, 600)` is 62, `FillPercent(0, 90)` is 0,
   `FillPercent(6000, 90)` is 100, and `FillPercent(0, 0)` is 100; `TonightLabel(false)` is
   `Tonight (weeknight, ends 4 AM)`, `TonightLabel(true)` is `Tonight (weekend night, ends 4 AM)`, and
   `WeekLabel` is `This week (resets Monday 4 AM)`. (Test `StatusTextTests.Meters`.)
3. **Given** a state with `tonightLimitMinutes` 90, `nightSeconds` 2820, `weeklyCapMinutes` 600,
   `weekSeconds` 22200, and `remainingSeconds` 2580, **When** `StatusText.TonightLeft(state)` and
   `StatusText.WeekLeft(state)` run, **Then** they return `43 min` and `3 h 50 min`; **given**
   `nightSeconds` 0, `weekSeconds` 34980, and `remainingSeconds` 1020 (the week runs out first),
   **Then** they return `1 h 30 min` and `17 min`; **given** `nightSeconds` 6000 (past the limit),
   **Then** `TonightLeft` returns `0 min`. (Test `StatusTextTests.EachMeterShowsItsOwnTimeLeft`.)
4. **Given** the time functions, **When** they run, **Then** `AppliesAt(2026-10-22 19:02 local)`
   returns `Applies Thu, Oct 22, 7:02 PM`; `In(164880)` returns `in 1 d 22 h`, `In(86400)` returns
   `in 1 d 0 h`, `In(7200)` returns `in 2 h`, `In(1800)` returns `in 1 h`, and `In(1799)` returns
   `in 0 h`; `Days([Friday, Saturday, Sunday])` returns `Fri, Sat, Sun` and `Days([Sunday, Friday])`
   returns `Fri, Sun`. (Test `StatusTextTests.TimesAndDays`.)
5. **Given** a `StateMessage` with `remainingSeconds` 2580, `tonightLimitMinutes` 90,
   `tonightWeekend` false, `nightSeconds` 2820, `weekSeconds` 22200, `counting` `Cyberpunk 2077`,
   `helperConnected` true, default settings plus one pending `weeknightMinutes` 120 entry (a fixed
   `id`, `requestedUtc` 2026-10-20T21:14:00Z, `remainingWaitSeconds` 164880), and `addable`
   [`Viewfinder`], **When** it is serialized and parsed back with `PipeMessage.Parse`, **Then** the
   result is a `StateMessage` equal to the original (the serialized lines of the two are identical and their lists match), and its line ends with
   one newline; **given** the line `{"type":"state","remainingSeconds":null,"tonightLimitMinutes":null,"tonightWeekend":false,"nightSeconds":0,"weekSeconds":0,"counting":null,"helperConnected":false,"settings":{"weeknightMinutes":90,"weekendNightMinutes":180,"weeklyCapMinutes":600,"cooldownHours":48,"enabled":true,"allowUninstall":false,"weekendNights":["Friday"],"notCounted":[],"pending":[]},"addable":[]}`,
   **Then** it parses to a `StateMessage` with null `RemainingSeconds` and weekend nights [Friday].
   (Test `StateMessageTests.RoundTrips`.)
6. **Given** a state line without `settings`, one whose pending entry's `id` is not a GUID, one whose
   `weekendNights` holds `6`, and the text `{"type":"state"`, **When** `PipeMessage.Parse` reads
   each, **Then** it returns null each time without throwing. (Test `StateMessageTests.BadStateIsNull`.)
7. **Given** a state whose `counting` is `Café Ω 日本` and whose `addable` holds one entry with folder
   `Jeu é` and display name `日本語のゲーム`, **When** `ToLine()` runs, **Then** the bytes contain the
   UTF-8 encodings of `Café Ω 日本` and `日本語のゲーム` (`é` as the 2 bytes C3 A9, `日` as the 3 bytes
   E6 97 A5) and the line contains no `\u` text, and `PipeMessage.Parse` of the line gives back the
   same strings; **given** `counting` U+1F600 (the grinning face emoji), **Then** the line contains the 12
   characters `\uD83D\uDE00` and parses back to U+1F600; **and** a `HelloMessage` still serializes with the default
   encoder. (Test `StateMessageTests.NonAsciiEncoding`.)
7b. **Given** `StatusState.DisplayName` on an installed app whose display name is 127 `a` followed by
   U+1F600 (the grinning face emoji; 129 UTF-16 units), **When** the name is cut, **Then** the result is the 127 `a` alone (127
   units; the pair is not split); **given** 200 `a`, **Then** 128 `a`; **given** 128 units ending in a
   whole pair at units 127 and 128, **Then** all 128 are kept. (Test
   `StatusStateTests.NameCutKeepsWholeCharacters`.)
8. **Given** a snapshot with 600 installed apps not on the not-counted list, each with its own
   distinct 260-character folder made of `日` characters (for example 256 `日` followed by a
   four-digit number, so no two folders are equal) and a 128-character display name of the control
   character U+0001 (escaped as 6 bytes each), plus 50 not-counted folders and 50 pending
   `notCountedAdd` entries, each with its own distinct 260-character `日` folder made the same way
   (no folder repeats across the apps, the not-counted list, and the pending entries), **When** `StatusState.From` runs, **Then** its `Serialize()` byte length (the newline `ToLine` adds is not counted) is at
   most `StateMessage.MaxLineBytes`, which is 524288, `addable` holds fewer than 512 entries, the kept
   entries are the first ones in sort order, and adding back the next dropped entry would make the
   line longer than 524288; **and given** 600 short ASCII apps, `addable` holds exactly 512; **and**
   a 300-character display name is cut to 128 characters. (Test
   `StateMessageTests.LargestStateFitsByteCap`.)
9. **Given** a `GateSnapshot` with `Games` [`Cyberpunk 2077` (folder and display name)], `Installed`
   [`Cyberpunk 2077`, `Viewfinder`, `Blender`, `wallpaper_engine` (display `Wallpaper Engine`)],
   `Tally(Night 2026-10-20, 2820, Week 2026-10-19, 22200, null)`, `RemainingSeconds` 2580,
   `TonightLimitMinutes` 90, `HelperConnected` true, and `Settings` the defaults, **When**
   `StatusState.From(snapshot, 2026-10-20 21:14 local)` runs, **Then** the state has `counting`
   `Cyberpunk 2077`, `nightSeconds` 2820, `weekSeconds` 22200, `tonightWeekend` false,
   `remainingSeconds` 2580, `tonightLimitMinutes` 90, `addable` [`Cyberpunk 2077`, `Viewfinder`] in
   that order, and `notCounted` display names `Blender`, `OBS Studio`, `Wallpaper Engine`,
   `Steamworks Shared`; `StatusState.DisplayName("wallpaper_engine", [])` is `Wallpaper Engine`,
   `StatusState.DisplayName("VIEWFINDER", installed)` is `Viewfinder`, and
   `StatusState.DisplayName("Gone Game", [])` is `Gone Game`; **with** the same snapshot and
   `2026-10-23 21:14 local` (a Friday night),
   `tonightWeekend` is true and `nightSeconds` is 0 (the tally's night is not tonight); **with**
   `Settings` null, or `RemainingSeconds` null, it returns null; **with** no games, `counting` is
   null. (Tests `StatusStateTests.FromSnapshot`, `StatusStateTests.NullUntilReady`.)
10. **Given** settings with weeknight 75, cooldown 24, weekend nights [Saturday], `Enabled` false,
   `AllowUninstall` true, not-counted [`Blender`], and two pending entries (`weekendNightsAdd`
   [Sunday] and `notCountedAdd` `Viewfinder`), **When** `StatusState.ToSettings` runs on the state
   built from them, **Then** every rule value, the not-counted list, the weekend-night set, and each
   pending entry's `Id`, `Field`, `Number`, `Flag`, `Folder`, `Days`, `RequestedUtc`, and
   `RemainingWaitSeconds` equal the originals, and `Clock` is null. (Test
   `StatusStateTests.SettingsRoundTrip`.)
11. **Given** default settings, `utcNow` 2026-10-20T21:14:00Z, and zone UTC, **When**
   `VerdictLine.For` runs, **Then**: `weeknightMinutes` 60 gives (`This tightens the rule. It applies
   now.`, `Now`, true); 120 gives (`This loosens the rule. It applies Thu, Oct 22, 9:14 PM (48 h).`,
   `Wait`, true); 90 gives (`No change yet.`, `Wait`, false); 2000 gives
   (`Out of range: 0 to 1440.`, `Wait`, false); `cooldownHours` 200 gives (`The cooldown can be at
   most 168 h (7 days).`, `Wait`, false); `weekendNights` [Friday, Saturday] gives the tightens line;
   `weekendNights` [Thursday, Friday, Saturday, Sunday] gives the loosens line; **given** weekend
   nights [Friday, Saturday], `weekendNights` [Saturday, Sunday] (a `Split` result) gives the loosens
   line, `Wait`, true; **given** cooldown 0, `weeknightMinutes` 120 gives (`This loosens the rule. It
   applies Tue, Oct 20, 9:14 PM (0 h).`, `Wait`, true); **given** 50 pending `notCountedAdd` entries (folders
   `G01` to `G50`, so no pending entry touches a night limit and a weekend-night removal still
   counts as safe), `weeknightMinutes` 120 gives (`Not allowed.`, `Wait`, false); **given** the same
   50 pending entries and weekend nights [Friday, Saturday], `weekendNights` [Saturday, Sunday] gives (`Applied now: Friday.
   Not allowed: Sunday.`, `Wait`, true). (Test `VerdictLineTests.RulesEditor`.)
12. **Given** the panel lines, **When** read, **Then** `VerdictLine.NotCountedAdd(48)` is `Adding a
   program loosens the rules. It waits 48 h.`, `VerdictLine.NotCountedAdd(24)` is `Adding a program
   loosens the rules. It waits 24 h.`, `VerdictLine.TurnOffOrRemove(48)` is `Both wait 48 h.`,
   `VerdictLine.TurnOffOrRemove(24)` is `Both wait 24 h.`, `StatusText.RulesNote` is `Loosening a
   rule waits the cooldown. Tightening a rule applies now.`, `StatusText.RemovingAppliesNow` is
   `Removing applies now`, `StatusText.NothingWaiting` is `Nothing waiting.`, and
   `StatusText.NotConnected` is `Can't reach SteamGate.` (Test `VerdictLineTests.PanelLines`.)
12b. **Given** `StatusText.ReplyIsNow`, **When** it runs on each reply text below, **Then** it returns
   true (green) for `Applied now.` and for `Applied now. Saved in memory; will retry.`, and false
   (amber) for `Waits 48 h 0 min.`, `Waits 48 h 0 min. Saved in memory; will retry.`,
   `Applied now: Friday. Waits 48 h 0 min: Sunday.`,
   `Applied now: Friday. Waits 48 h 0 min: Sunday. Saved in memory; will retry.`,
   `Applied now: Friday. Not allowed: Sunday.`,
   `Applied now: Friday. Not allowed: Sunday. Saved in memory; will retry.`, `Already set.`,
   `Not in the list.`, `No pending change with that id.`, `Out of range: 0 to 1440.`,
   `Not allowed.`, and `Busy, try again.` (Test `StatusTextTests.ReplyColors`.)
13. **Given** a state with default settings except that `wallpaper_engine` is not on the not-counted
   list (removed earlier) and is not installed, and `addable` [`Viewfinder`], **When**
   `PendingText.For` runs on pending entries, **Then** `weeknightMinutes` 120 gives `Weeknight limit: 1 h 30 min to 2
   h`, `weekendNightMinutes` 240 gives `Weekend night limit: 3 h to 4 h`, `weeklyCapMinutes` 720 gives
   `Weekly cap: 10 h to 12 h`, `cooldownHours` 24 gives `Cooldown: 48 h to 24 h`, `weekendNightsAdd`
   [Thursday] gives `Weekend nights: Fri, Sat, Sun to Thu, Fri, Sat, Sun`, `weekendNightsRemove`
   [Sunday] gives `Weekend nights: Fri, Sat, Sun to Fri, Sat`, `notCountedAdd` `Viewfinder` gives
   `Stop counting Viewfinder`, `notCountedAdd` `Gone Game` (not installed) gives `Stop counting Gone
   Game`, `notCountedAdd` `wallpaper_engine` (an approved folder, in neither the list nor `addable`)
   gives `Stop counting Wallpaper Engine`, `enabled` false gives `Turn SteamGate off`,
   `allowUninstall` true gives `Allow SteamGate to be removed`, `enabled` true gives `enabled true`,
   and `allowUninstall` false gives `allowUninstall false`. (Test `PendingTextTests.Rows`.)
14. **Given** a new `StatusFeed`, **When** `Offer` runs on state A,
   then A again, then B (A with `nightSeconds` 5 more), then B again, **Then** it returns true, false,
   true, false, and `Latest` is B. (Test `StatusFeedTests.OnlyOnChange`.)
14b. **Given** a `StatusFeed` whose `Latest` is A, **When** `Forget()` runs, **Then** `Latest` is null and the next `Offer` of A returns true (the old text was forgotten) and `Latest` is A. (Test
   `StatusFeedTests.ForgetKeepsNothing`.) **And given** `src/SteamGate.Service/PipeServer.cs`,
   **When** a reader greps it, **Then** `PublishState` calls `Offer` inside a `lock (_lock)` block.
15. **Given** a connection whose hello was `status`, **When** it sends a `state` line, **Then**
   `ConnectionRules.DropsOnClientMessage` returns true. (Test `ConnectionRulesTests.ClientStateDrops`.)
16. **Given** an `EditorState` with the `weeknightMinutes` editor open, typed number 120, checked days
   [Friday, Saturday], chosen Add folder `Viewfinder`, and the Rules reply line `Applied now.`,
   **When** `StatusView.Merge` runs with a new state whose weeknight is 60 and whose `addable` still
   holds `Viewfinder`, **Then** the result equals the input editor state (editor open, 120, the same
   days, `Viewfinder`, the same reply line), and `VerdictLine.For` against the new settings gives the
   loosens line; **when** the new state's `addable` is [`Overwatch`, `Viewfinder 2`] instead, **Then**
   only the chosen Add folder changes, to `Overwatch`; **when** `addable` is empty, **Then** it becomes
   null. (Test `StatusViewTests.StateKeepsEdits`.)
17. **Given** `StatusView.RowsChanged`, **When** it compares [`a`, `b`] with [`a`, `b`], [`a`, `b`]
   with [`b`, `a`], [`a`, `b`] with [`a`], and [] with [], **Then** it returns false, true, true, and
   false. (Test `StatusViewTests.RowsRebuiltOnlyWhenKeysChange`.)
18. **Given** `StatusRequests`, **When** it builds the request for `weeknightMinutes` 120 with id
   `s1`, `weekendNights` [Saturday, Sunday] with id `s2`, `notCountedAdd` `Viewfinder` with id `s3`,
   `enabled` false with id `s4`, and the cancel of a fixed GUID with id `s5`, **Then** each line
   parses back with `PipeMessage.Parse` to a `RequestMessage` (or `CancelMessage`) with that id,
   field, and value, the days as `["Saturday","Sunday"]`; and the Core mapping the server uses turns
   each `RequestMessage` into a `ChangeRequest` with the same field and value. (Test
   `StatusRequestsTests.LinesMatchServerMapping`.)
19. **Given** `StatusStyle`, **When** read, **Then** it holds every color, font size, and layout
   value listed under "Style values" in Design decisions, with those exact values. (Test
   `StatusStyleTests.MatchesApprovedPreview`.)
20. **Given** `src/SteamGate.Status`, **When** a reader greps it, **Then** it contains
   `Local\SteamGate.Status` or `Local\\SteamGate.Status`, `StateMessage.MaxLineBytes`,
   `HelloMessage.StatusRole`, `StatusView.Merge`, and `StatusStyle.`, and its `.csproj` contains
   `..\SteamGate.Helper\PipeClient.cs`, `<OutputType>WinExe</OutputType>`, and `net10.0-windows`
   and no `ApplicationIcon`; its source contains `SystemIcons.Application`; and it contains none of `NotifyIcon`, `SoundPlayer`, `SystemSounds`, `PlaySound`,
   `CurrentVersion\Run`, or `Sample data`.
21. **Given** `src/SteamGate.Service`, **When** a reader greps it, **Then** `GateWorker.cs` contains
   `state unavailable`, `StatusConnected`, and `PublishState`, and `PipeServer.cs` contains
   `PublishState` and `StatusConnected` and no longer contains `and later the status screen`.
22. **Given** `SteamGate.sln` and `.github/workflows/ci.yml`, **When** a reader greps them, **Then**
   the solution contains `src\SteamGate.Status\SteamGate.Status.csproj` and `ci.yml` contains
   `dotnet publish src/SteamGate.Status` with `-o out`.
23. **Given** `install/PipeProbe.ps1`, **When** a reader greps it, **Then** it contains the literal
   strings `$StateFile`, `524288`, `"type":"reply"`, and `"type":"state"`; **and given** the CI Windows runner, **When**
   `Test-Install.ps1` runs 0006's criterion 12, 12b, and 13 groups after this change (the service now
   sends `state` lines to the probe's `status` connection before any reply), **Then** they still
   print `PENDING OK`, `TIGHTEN OK`, `TIGHTEN ENFORCED`, `UNINSTALL REFUSED`, and
   `UNINSTALL UNLOCKED`.
24. **Given** the CI Windows runner, after `Reset-SteamGate @{}`, **When** `Test-Install.ps1` runs
   `install/PipeProbe.ps1` with `-StateFile` (the probe says hello as `status` and writes the first
   line whose `type` is `state` to that file), **Then** within 15 seconds the file holds one JSON line
   whose `type` is `state`, `settings.weeknightMinutes` 90, `settings.cooldownHours` 48,
   `settings.notCounted` has 4 entries one of whose `displayName` is `Wallpaper Engine`, and
   `tonightLimitMinutes` is 180 when the runner's local time minus 4 hours falls on a Friday,
   Saturday, or Sunday and 90 otherwise; the script prints `STATE OK`. Without `-StateFile` and
   `-ReplyFile` the probe behaves exactly as before.
25. **Given** the CI Windows runner, after `Reset-SteamGate @{}`, **When** `Test-Install.ps1` starts
   `%ProgramFiles%\SteamGate\SteamGate.Status.exe` as the runner (`Start-Process -PassThru`, then
   reading `$p.Handle` at once so `ExitCode` is readable after exit in Windows PowerShell 5.1),
   **Then** within 15 seconds `service.log` contains `pipe hello role=status pid=<that process's
   pid>`; **when** it starts a second copy the same way (its `Handle` read at once), **Then** the
   second exits within 5 seconds with `ExitCode` 0 while the first is still running, and the script
   prints `STATUS SINGLE OK`; **when** it waits up to 15 seconds for the first copy's
   `MainWindowHandle` (calling `Refresh()`) to be non-zero and calls `CloseMainWindow()`, **Then** the
   first copy exits within 10 seconds with `ExitCode` 0, and the script prints `STATUS OK`. It exits
   non-zero at any failed check and ends with `Invoke-TestUninstall` (0006), stopping any copy still
   running in its `finally` block.
26. **Given** the CI Windows runner, **When** `Test-Install.ps1` runs `Reset-SteamGate @{}`, **Then**
   `$ShortcutPath` exists and its `TargetPath` (read through `WScript.Shell` `CreateShortcut`) equals
   `%ProgramFiles%\SteamGate\SteamGate.Status.exe`, and the script prints `SHORTCUT OK`; **when** the
   test then writes a shortcut at `$ShortcutPath` pointing at `C:\Windows\notepad.exe` and runs
   `Install-SteamGate.ps1` again (an upgrade), **Then** the `TargetPath` is again
   `%ProgramFiles%\SteamGate\SteamGate.Status.exe`; **when** it then runs `Invoke-TestUninstall`,
   **Then** `$ShortcutPath` no longer exists and the script prints `SHORTCUT REMOVED`; **and** in
   0006's criterion 13 group, after a refused uninstall, `$ShortcutPath` still exists.
27. **Given** `DESIGN.md`, **When** a reader greps it, **Then**
   `## SteamGate service design (orchestrator decisions, not part of the approved spec)` contains
   `How the status screen works`, `SteamGate.Status.exe`, `StateMessage`, `Start Menu`,
   `Request removal`, `the status window`, and the phrase `` `SteamGate.Service`, `SteamGate.Helper`,
   and `SteamGate.Status` only connect it to Windows ``; it contains `each line a client sends is at
   most 16 KB` and `524288` and does not contain `each line is at most 16 KB`; its `One tick` bullet
   contains `status screen`; DESIGN.md does not contain `the later status screen`,
   `Before the status screen (0007) ships`, or `no shipped tool can send a request`; `## Repo structure`
   contains `SteamGate.Status` and `preview-2026-10-22-extras.html` and does not contain
   `the approved warning overlay preview`, and its `docs/` line contains `preview-2026-10-21.html`
   exactly once; `## CI` contains
   `SteamGate.Status`, `Start Menu shortcut`, `PipeProbe`, `per session`, and `does not check that the
   real status screen reads a state`; `## Evals` contains
   `preview-2026-10-22-extras.html` and `(status screen look)`; and `## Testing` contains
   `status screen`.
28. **Given** `README.md`, `WHAT-IT-CHECKS.md`, `PLAN.md`, and `CLAUDE.md`, **When** a reader greps
   them, **Then** README.md contains `src/SteamGate.Status` and `Start Menu`, its `## Install` contains
   `Request removal` and contains `Uninstall-SteamGate.ps1` exactly once, after `Request removal`, and
   README.md contains neither `Until the status screen ships` nor `Removal must first be requested`;
   WHAT-IT-CHECKS.md contains `the real status screen program connects`, `No build check proves the
   real status screen reads`, `Start Menu`, and `two previews`, and
   does not contain `the status screen is issue 0007`; PLAN.md's line for 0007 contains
   `preview-2026-10-22-extras.html` and `(status screen look)`; and CLAUDE.md's `### Product
   outcomes` still contains `Status screen: see where you stand, request changes, edit the
   not-counted list.` (unchanged, because it stays true).
29. (Card 12; removed with the card if Trevor drops it at the gate.) **Given** `StatusText`, **When**
   read, **Then** `StatusText.SteamGateOff` is `SteamGate is off.`, `StatusText.RequestTurnOn` is
   `Request turn on`, `StatusText.RemovalAllowed` is `Removal is allowed.`, and
   `StatusText.StopAllowingRemoval` is `Stop allowing removal`; **given** a state with `enabled` false
   and `allowUninstall` true, **When** `StatusView.HeadLines(state)` runs, **Then** it returns
   [`SteamGate is off.`, `Removal is allowed.`] in that order, with `enabled` true and
   `allowUninstall` false it returns [], and with only `enabled` false it returns
   [`SteamGate is off.`]; **and** `StatusRequests` for `enabled` true and for `allowUninstall` false,
   dry-run through `ChangeEngine.Request` on settings with `Enabled` false and `AllowUninstall` true,
   give result `Applied` with `Loosens` false. (Test `StatusViewTests.OffAndRemovalLines`.)
30. **Given** `issues/0006-the-lock.md` (a check only; 0006 already made this true on `main`
   (e6a7824 or later), and this issue does not edit that file), **When** a reader greps it, **Then** its
   `**Blocks:**` line is `**Blocks:** 0007, GitHub #17.` and the file does not contain
   `not yet filed`.
31. **Given** `install/Uninstall-SteamGate.ps1` and `install/Test-Install.ps1`, **When** a reader greps
   them, **Then** each contains the literal text `Removal is not allowed yet. Request it in SteamGate;
   it applies after the cooldown.` (the whole message, ending at that period) and neither contains
   `until its status screen ships` or `runs the uninstall right away`; and in CI the
   0006 criterion 13 group still prints `UNINSTALL REFUSED` (it compares the printed text with
   `$refusalText`, now the new sentence).
32. **Given** `install/SteamGate.Common.ps1`, `install/Uninstall-SteamGate.ps1`, and
   `install/PipeProbe.ps1`, **When** a reader greps them, **Then** the first contains `(the helper and
   the status screen, in every session)`, the second contains `its Start Menu shortcut`, and the third
   contains `-StateFile` in its header comment and `Without -Line, -ReplyFile, and -StateFile` and no
   longer contains `the later status screen` or `Without -Line and -ReplyFile it behaves`;
   **and** `src/SteamGate.Core/Pipe/PipeLineReader.cs` contains `a client may send` and no longer
   contains `The longest line the pipe accepts`.

## Implementation plan
0. Owner look gate: DONE on 2026-10-23 by the orchestrator. The amendment is appended to
   `docs/approved-spec-2026-10-20.md` and DESIGN.md `## SteamGate product spec`, this issue quotes
   it, and GitHub #17 has the comment. Trevor changed no card. Do not repeat this step; the
   implementer starts at step 1 (criterion 0 stays as a check).
1. In `src/SteamGate.Core/Pipe/PipeMessage.cs`, add `StateMessage` (members in Design decisions, `MaxLineBytes` 524288 (not counting the newline), type name `state`, `Serialize` with
   `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`) with its nested entry records
   (`NamedFolder(Folder, DisplayName)` and the pending entry), and its `Parse` branch (null on any
   missing or wrong-kind member, a bad GUID, a bad date, or a day that `DayNames.TryParse` refuses).
   In `src/SteamGate.Core/Pipe/ConnectionRules.cs`, make sure a client `state` drops the connection.
2. Create `src/SteamGate.Core/Status/`: `StatusState.cs` (`From` with the one-pass byte-cap trim,
   `ToSettings`, `DisplayName` with the whole-character cut, `MaxAddable` 512, `MaxNameLength` 128),
   `StatusFeed.cs`, `StatusText.cs` (including `TonightLeft`, `WeekLeft`, `NotConnected`, and the card
   12 texts), `PendingText.cs` (names through `StatusState.DisplayName`), `VerdictLine.cs` (with
   `VerdictKind`), `StatusRequests.cs`, `StatusView.cs` (`EditorState`, `Merge`, `RowsChanged`,
   `HeadLines`), and
   `StatusStyle.cs`, implementing Design decisions. Reuse `WarnMinutes.FromRemaining`, `NightClock`,
   `DayNames`, `ChangeEngine.Request`, `GateSettings.ApprovedNames`, and `ChangeMessages.ToRequest`.
3. In `src/SteamGate.Service/PipeServer.cs`, record each connection's hello role, add
   `PublishState(StateMessage)` calling `StatusFeed.Offer` with whether a `status` connection is
   registered, post changed states to `status` connections only, post `StatusFeed.Latest` to a
   `status` connection when it is registered, add `StatusConnected`, and call `StatusFeed.Forget()`
   when the last `status` connection ends, all under `_lock`; change the class summary's "how the helper (and later the status screen) talks to the
   service" to "how the helper and the status screen talk to the service". In
   `src/SteamGate.Service/GateWorker.cs`, inside `RunTickAsync` after the snapshot is published and
   only while `PipeServer.StatusConnected` is true, call `StatusState.From` with the tick's local time
   and `PublishState` when it is not null, inside the catch-and-log-once wrapper
   (`state unavailable <message>`).
4. Create `src/SteamGate.Status` (`SteamGate.Status.csproj` as in Design decisions, referencing
   `SteamGate.Core` and linking `..\SteamGate.Helper\PipeClient.cs`): `Program.cs` (the mutex, then
   `Application.Run` of the window), the connection loop (hello, read with
   `StateMessage.MaxLineBytes`, reconnect every 5 seconds, the 10-second not-connected rule), and the
   window that draws the preview's layout and the extras from `StatusStyle` and `StatusText`, keeps an
   `EditorState` merged with `StatusView.Merge` on each state, rebuilds rows only when
   `StatusView.RowsChanged`, opens the Rules editors, shows `VerdictLine` results, sends requests built
   by `StatusRequests`, and shows reply lines. Add it to `SteamGate.sln`. In
   `src/SteamGate.Helper/PipeClient.cs`, change the summary's "The later status screen does the same"
   to say the status screen compiles this same file. In `src/SteamGate.Core/Pipe/PipeLineReader.cs`,
   change the `MaxLineBytes` summary to "The longest line a client may send, and the helper reads,
   in bytes, not counting the newline; a state line uses StateMessage.MaxLineBytes." (comment only).
5. In `.github/workflows/ci.yml`, add a step `Publish the status screen` after the helper's:
   `dotnet publish src/SteamGate.Status -c Release -r win-x64 --self-contained
   -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out`, and add the status
   screen to the header comment and the install-test step name.
6. In `install/SteamGate.Common.ps1`, define `$ShortcutPath`, and change the `Stop-SteamGateProcesses`
   comment's "(the helper, in every session)" to "(the helper and the status screen, in every
   session)". In `install/Uninstall-SteamGate.ps1`, change the header's first line to "# Removes the
   SteamGate service, its program files, its settings folder, its Start Menu shortcut, and the
   registry key HKLM:\SOFTWARE\SteamGate." In `install/PipeProbe.ps1`'s header, add one line saying
   that -StateFile is where to write the first state line the server sends, and that -ReplyFile
   skips state lines and writes the first reply line; and change its line "Without -Line and
   -ReplyFile it behaves as it always has." to "Without -Line, -ReplyFile, and -StateFile it behaves
   as it always has." In `install/Install-SteamGate.ps1`, add
   the shortcut function (delete any existing file at `$ShortcutPath`, then create it) and call it
   after `Copy-ProgramFiles` in both `Install-Fresh` and `Install-Upgrade` (inside the upgrade's try,
   so a failure is reported like a copy failure). In `install/Uninstall-SteamGate.ps1`, remove the
   shortcut after `Remove-Folder -Path $ProgramDir`, and change the refusal line (on `main`
   at e6a7824 it reads `Removal is not allowed yet. Request it in SteamGate; it applies after the
   cooldown (until its status screen ships, an admin sets allowUninstall to true in settings.json and
   runs the uninstall right away).`) to exactly
   `Removal is not allowed yet. Request it in SteamGate; it applies after the cooldown.`; in the same
   change set `$refusalText` in `install/Test-Install.ps1` (the same old text today) to that same new
   sentence (criterion 31). In `install/PipeProbe.ps1`: add the optional
   `-StateFile` parameter; make the line reader take a byte cap and read every line with 524288; make
   the `-ReplyFile` path keep reading until a line containing `"type":"reply"` arrives (skipping
   `state` and any other line) or its 10 seconds run out; make the `-StateFile` path write the first
   line containing `"type":"state"`; and change its header comment's "the later status screen" to
   "the status screen".
7. Add the tests in criteria 1 to 19 (12b and 14b included) and 29 under `tests/SteamGate.Tests/Status/` (and criterion 15 in
   `tests/SteamGate.Tests/Pipe/ConnectionRulesTests.cs`), plus further value-asserting tests on the
   new Core code as needed to keep the 80% Stryker break threshold. In `install/Test-Install.ps1`,
   add the criterion 24, 25, and 26 groups after 0006's last group, each exiting non-zero at any
   failed check and ending with `Invoke-TestUninstall`, and add the still-present shortcut check to
   0006's criterion 13 group.
8. Update the documents this change makes false or that must describe it:
   - `DESIGN.md` `## SteamGate service design (orchestrator decisions, not part of the approved
     spec)`: add a "How the status screen works" paragraph (the program and its single copy per user
     per session, the Start Menu shortcut, the `StateMessage` and when it is sent, the byte cap, the
     dry-run verdict, requests and replies, reconnecting, who can read the state, that a state failure
     is logged once and never stops enforcement, and that drawing is not machine-checked in CI).
     Change the opening sentence to "Decision logic lives in `SteamGate.Core`; `SteamGate.Service`,
     `SteamGate.Helper`, and `SteamGate.Status` only connect it to Windows (service lifetime, process
     and registry access, the tick, the pipe, the overlay window, the status window)". In "How closing
     works", replace "each line is at most 16 KB" with "each line a client sends is at most 16 KB, and
     a state line the server sends is at most 524288 bytes". In the `One tick` bullet's sentence about
     the helper's own timers, add that the status screen runs its own reconnect timer. Replace every
     "the later status screen" with "the status screen". Replace 0006's sentence that begins "Before the
     status screen (0007) ships, no shipped tool can send a request, so an admin unlocks removal" with one saying removal is requested with the status screen's
     Request removal button and waits the cooldown, and that an admin's hand edit stays inside the
     admin-teardown gap.
   - `DESIGN.md` `## Repo structure`: add `SteamGate.Status (the status screen in the user's
     session)` to the `src/` line, and rewrite the `docs/` line once, so each preview is named one
     time: `docs/ - pinned approved spec, the status screen and warning overlay preview
     (preview-2026-10-21.html), the status screen extras preview (preview-2026-10-22-extras.html),
     and the template's dated run log` (this replaces "the approved warning overlay preview"). `## CI`: step 5 publishes
     `SteamGate.Service`, `SteamGate.Helper`, and `SteamGate.Status`; step 6 adds exactly what
     CI proves and no more: that `PipeProbe.ps1`, a test client speaking the status screen's pipe
     protocol, receives the state from the service; that the real `SteamGate.Status.exe` says hello,
     runs one copy per user per session, and closes cleanly; that install adds the Start Menu shortcut and
     uninstall removes it; and that nothing in CI checks that the real status screen reads a state
     or draws it (the wording "does not check that the real status screen reads a state").
     `## Evals`: replace "The status screen's look is approved by Trevor on a preview before it is
     built" with a sentence saying Trevor's approval of the status screen look on
     `docs/preview-2026-10-21.html` and `docs/preview-2026-10-22-extras.html` is pinned as the
     `(status screen look)` amendment in `docs/approved-spec-2026-10-20.md`. `## Testing`: add that
     the status screen's connection, single copy, and clean close are checked by the install test, and
     its drawing is not machine-checked in CI.
   - `README.md`: add a layout row `src/SteamGate.Status` ("The status screen: time left, rules,
     waiting changes, and the not-counted list"); add to the opening paragraph a sentence that
     SteamGate opens from the Start Menu to show the time left and request changes, and change the
     opening paragraph's "It runs with no window, tray icon, or Alt-Tab entry" to "The service runs
     with no window, tray icon, or Alt-Tab entry" (the status screen is a window); in `## Install`,
     replace both of 0006's sentences, "Removal must first be requested in SteamGate's status screen;
     it is allowed after the cooldown. Then run `Uninstall-SteamGate.ps1` the same way." and "Until the
     status screen ships, ... (SteamGate rewrites the file on its next save).", with the one passage
     "To remove SteamGate, open SteamGate from the Start Menu and press Request removal. Once the
     cooldown has passed, run `Uninstall-SteamGate.ps1` the same way.", so the uninstall instruction
     appears once, after the request.
   - `WHAT-IT-CHECKS.md`: add one plain bullet: "Every build checks the status screen's link to
     SteamGate on a Windows test machine: a test program that talks to SteamGate the way the status
     screen does receives the current time and rules, and the real status screen program connects,
     runs only one copy per sign-in (a second start closes at once), and closes cleanly. Installing adds a SteamGate Start Menu shortcut
     and removing SteamGate takes it away. No build check proves the real status screen reads and
     shows the time and rules; you see that on the first real install." In the not-machine-checked paragraph,
     replace "(the status screen is issue 0007; you approved the warning overlay on 2026-10-21)" with
     "(the status screen on two previews, the screen itself and its extra states, with your words
     pinned in docs/approved-spec-2026-10-20.md; the warning overlay on 2026-10-21). The build does
     not prove what the status screen looks like."
   - `PLAN.md`: change the 0007 line's "Its look is approved by Trevor on a preview before it is
     built." to "Its look is approved by Trevor on docs/preview-2026-10-21.html and
     docs/preview-2026-10-22-extras.html before it is built; his words are pinned as the
     `(status screen look)` amendment."
   - `CLAUDE.md`: no change; its `### Product outcomes` status screen line stays true.

## Out of scope
- A tray icon, sound, any way of opening the screen other than the one Trevor picks at gate item
  (d), and bringing an already-open window to the front when a second
  copy is started (the second copy exits silently, extras card 10).
- Any control to turn SteamGate back on or to withdraw allowing removal other than card 12's, and
  card 12 itself if Trevor drops it.
- Showing whether the helper is connected (carried in the state, not drawn).
- A custom program icon, unless Trevor picks one at gate item (e) (card 14), in which case this
  issue is revised first.
- Any approved spec change other than the `(status screen look)` amendment the gate records.
