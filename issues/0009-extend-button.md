# Issue #0009: Extend button (add 10, 20, or 30 minutes to tonight, once per night)

**Type:** ready. **Category:** product feature; adds a new pipe message (`extend`), a new tally member, new Core
rules (`ExtendRules`, `ExtendText`, `ExtensionCost`), and a new section on the status screen, and it edits
CLAUDE.md and DESIGN.md, so it is a system-level change.
**Review bar:** system-level change; two independent reviewers, both PASS; reviewer-documentation on the
CLAUDE.md, DESIGN.md, README.md, WHAT-IT-CHECKS.md, and PLAN.md changes; plus the architecture gate at
issue time and the design-philosophy gate at PR time. Visual-approval note: this issue builds visible
output whose look Trevor approves (orchestrator step 2a). Trevor approved the look in chat on 2026-10-26
("looks good"), pinned as the `(extension button)` amendment; see "Owner look gate". The screen's drawing is
not machine-checked in CI.
**GitHub:** #18.
**Depends on:** 0004, 0005, 0006, 0007, 0008. (All five are committed to `main`. Names this issue cites were
read in the working tree: `Accountant.Tick`, `Budget`, `NightClock.RollOver`, `Tally`, `TallyStore.IsValid`,
`EnforcementPlanner.Step` and `WarnThresholdSeconds`, `WarnMinutes.FromRemaining`, `SettingsCommand`,
`ChangeMessages.Failure`, `ChangeLog`, `ConnectionRules.DropsOnClientMessage`, `PipeServer.Submit`,
`StateMessage`, `StatusState.From`, `StatusText.Minutes` and `ReplyIsNow`, `StatusView.Merge`, `EditorState`,
`ReplySection`, `StatusRequests`, `Send-PipeRequest`, `Reset-SteamGate`, `Restart-ServiceProcess`, `Wait-SteamReady`, `New-FakeSteamRoot` and `Invoke-TestUninstall` in `Test-Install.ps1`.)
**Blocks:** none. (The `BUILDLOG.md` line is appended in a separate commit after this one, as for 0007.)
**Touches:** `src/SteamGate.Core/DurationText.cs` (new), `src/SteamGate.Core/Accounting/Tally.cs`,
`src/SteamGate.Core/Accounting/NightExtension.cs` (new), `src/SteamGate.Core/Accounting/ExtensionCost.cs` (new),
`src/SteamGate.Core/Accounting/TallyStore.cs`, `src/SteamGate.Core/Accounting/NightClock.cs`,
`src/SteamGate.Core/Accounting/Accountant.cs`, `src/SteamGate.Core/Accounting/Budget.cs`,
`src/SteamGate.Core/Extend/**` (new, namespace `SteamGate.Core.Extend`: `ExtendRules`, `ExtendText`,
`ExtendKind`, `ExtendView`, `ExtendOutcome`), `src/SteamGate.Core/GateSnapshot.cs`,
`src/SteamGate.Core/Rules/WorkerCommand.cs` (new), `src/SteamGate.Core/Rules/ExtendCommand.cs` (new),
`src/SteamGate.Core/Rules/SettingsCommand.cs`, `src/SteamGate.Core/Rules/ChangeLog.cs`,
`src/SteamGate.Core/Pipe/PipeMessage.cs`, `src/SteamGate.Core/Pipe/ConnectionRules.cs`,
`src/SteamGate.Core/Pipe/ChangeMessages.cs`, `src/SteamGate.Core/Status/StatusState.cs`,
`src/SteamGate.Core/Status/StatusText.cs`, `src/SteamGate.Core/Status/PendingText.cs` (caller of the moved
minutes formatter), `src/SteamGate.Core/Status/StatusView.cs`, `src/SteamGate.Core/Status/StatusRequests.cs`,
`src/SteamGate.Service/GateWorker.cs`, `src/SteamGate.Service/PipeServer.cs`, `src/SteamGate.Service/Program.cs`
(the channel type), `src/SteamGate.Status/StatusForm.cs`, `src/SteamGate.Status/StatusPage.cs` (and files under `src/SteamGate.Status/Ui/` only if the section needs
a control that is not there yet), `tests/SteamGate.Tests/DurationTextTests.cs` (new),
`tests/SteamGate.Tests/Accounting/**`, `tests/SteamGate.Tests/Extend/**` (new),
`tests/SteamGate.Tests/Enforcement/PlannerTests.cs`, `tests/SteamGate.Tests/Pipe/**` (including the new
`ChangeMessagesTests.cs`), `tests/SteamGate.Tests/Rules/**` (including the new `WorkerCommandTests.cs`),
`tests/SteamGate.Tests/Status/**`, `install/Test-Install.ps1`, `CLAUDE.md`, `DESIGN.md`, `README.md`,
`WHAT-IT-CHECKS.md`, `PLAN.md`,  `issues/0009-extend-button.md`,
`docs/preview-2026-10-26-extend.html` (new, rides in this commit) and `docs/approved-spec-2026-10-20.md`
(the `Amendment 2026-10-26 (extension button)` section, appended; it and the DESIGN.md copy ride in this same
commit). Not touched, and a criterion says so: `src/SteamGate.Core/Enforcement/**`,
`src/SteamGate.Core/Status/StatusStyle.cs`, `tests/SteamGate.Tests/Status/StatusStyleTests.cs`, `install/PipeProbe.ps1`, `.github/workflows/ci.yml`, and the
helper project.

## User story
As Trevor, I need an Extend section on the status screen that, in the last 30 minutes before tonight's
limit, lets me add 10, 20, or 30 minutes to tonight's limit once per night, applied at once, so that I can
finish a competitive round without being cut off in the middle of it, while the weekly cap stays a hard
limit and the minutes I play past the old limit cost double toward it.

## Approved spec this issue carries
Trevor's words, pinned as the `Amendment 2026-10-26 (extension button)` at the end of
`docs/approved-spec-2026-10-20.md` (identical text in DESIGN.md `### Amendment 2026-10-26 (extension button)`):

"I want a extension button, because competitive games happen in rounts, up to 30 minutes, counts double towards my weekly rate". "I think I can pick the time of how much time I add, but I get. Let's have some quick options at 10, 20, and 30 minutes. Once per night. I'd say, yeah, only 30 minutes before the limit ends. Okay. Let's just say there's going to be an interface for this, right? Like a window. Just say in that interface."

Trevor's 15-minute example, also pinned in the amendment: "say I extend 30 minutes but I only use 15. The round enters early, then I shouldn't be charged 60 minutes, I should be charged 30."

The amendment's rules, which this issue builds and does not reword: the status screen gets an Extend
button. In the last 30 minutes before tonight's limit he can add 10, 20, or 30 minutes to tonight's limit,
once per night. The extension applies at once. It is the one loosening that does not wait the cooldown; it
replaces "Any change that loosens a rule ... waits the full cooldown" for this button only. Minutes played
past tonight's original limit count double toward the weekly cap: only the minutes actually played past
the old limit are doubled, and extension time he does not use costs nothing. The weekly cap stays hard: a
length that would not fit in what is left this week is not offered. The Extend section is always shown,
greyed with the reason when it is not available. Trevor approved the look on
`docs/preview-2026-10-26-extend.html` on 2026-10-26: "looks good".

## Owner look gate (met 2026-10-26)
Trevor approved the look of the Extend section on `docs/preview-2026-10-26-extend.html` (three cards: the
section in the last 30 minutes; the section after pressing Extend; the section when it is not available,
three cases) in chat on 2026-10-26 with the words "looks good". The amendment is pinned in
`docs/approved-spec-2026-10-20.md` and DESIGN.md. Every screen text in this issue is the preview's text, except
numbers the preview samples (computed here by the rule this issue defines). No screen text is added beyond the preview's, except the section's reply line, which follows the approved reply-line pattern of `docs/preview-2026-10-22-extras.html` card 5 (answers such as `Not allowed.`, `Busy, try again.`, `Not enough time left this week.`, and the `Saved in memory; will retry.` ending). On 2026-10-27 Trevor settled the week note. He rejected both numeric wordings as confusing and answered "yes do that" to the fixed text `Not enough time left this week.` (no numbers, no sums). It shows under the Extend buttons when one or more lengths are greyed only because of the week. This text replaces the preview's third-card sentence, and `docs/preview-2026-10-26-extend.html`, the approved spec, and DESIGN.md were updated to match.
The two cards disagree about a starting pick, and the build follows both: card 1 shows the section with 20 min picked
(explanation line and `Extend` button visible) when all three lengths can be chosen, and card 3 case 3 shows none picked
when only some can be chosen. The rule is in Design 8 ("Which length is picked"). Trevor may change it in chat; if he
does, this issue goes back through issue review before the build.


## Decisions for Trevor
None open. Trevor settled the last one on 2026-10-27 (see the Owner look gate).

## Design decisions (orchestrator)
Technical choices, decided here so the builder does not choose them:

### 0. Layout of the new code
- `src/SteamGate.Core/DurationText.cs`: `public static class DurationText` with `Minutes(int minutes)`, the body of
  today's `StatusText.Minutes` moved as is (`45 min`, `1 h`, `1 h 30 min`). `DurationText` holds only the hours-and-minutes form that the screen and the Extend texts share; `ChangeReply.HoursMinutes`, `StatusText.In`, and `StatusText.Hours` stay where they are. `StatusText.Minutes` is deleted (no wrapper) and every caller (`StatusText`, `StatusView`, `PendingText`, the tests) calls `DurationText.Minutes`.
- `src/SteamGate.Core/Accounting/NightExtension.cs`: `public sealed record NightExtension(DateOnly Night, int Minutes)`
  with the static `NightExtension.Lengths`, `[10, 20, 30]`: the one list of allowed lengths. `TallyStore.IsValid`,
  `PipeMessage.Parse`, and `ExtendRules` all use it (there is no `ExtendRules.Lengths`); no other code holds the numbers 10, 20, 30 as a list.
- `src/SteamGate.Core/Accounting/ExtensionCost.cs`: the one place that says what time past the limit costs.
  `public const int PastLimitWeight = 2;`
  `double WeekCharge(double oldNightSeconds, double stepSeconds, double originalLimitSeconds, bool hasExtension)`
  returns `step + (PastLimitWeight - 1) * over`, where `over = max(0, old + step - max(old, limit))`
  when `hasExtension` (every second past the original limit is doubled while the night has an extension), and `over = 0` when not;
  `double PlayableWeekSeconds(double weekLeftSeconds, double nightSeconds, double originalLimitSeconds, bool hasExtension)`
  returns `weekLeft` when not `hasExtension` or when `weekLeft <= s1`, else `s1 + (weekLeft - s1) / PastLimitWeight`,
  with `s1 = max(0, limit - night)`;
  `double FullExtensionWeekSeconds(int minutes)` returns `minutes * 60 * PastLimitWeight`.
  `Accountant.Tick`, `Budget.RemainingSeconds`, `ExtendRules`, and `ExtendText` call these and hold no copy of the factor.
- `src/SteamGate.Core/Extend/` (namespace `SteamGate.Core.Extend`): `ExtendRules`, `ExtendText`, `ExtendKind`,
  `ExtendView`, `ExtendOutcome`. It depends only on `SteamGate.Core` (`GateSettings`, `DurationText`),
  `SteamGate.Core.Accounting`, and `ChangeReply.NotAllowed`, directly; not on `Status` or `Pipe` directly (`ChangeReply` and the Rules code reach the Pipe code only through `OutboundQueue`, which is existing). `SteamGate.Core.Accounting`
  does not reference `SteamGate.Core.Extend`.

### 1. Where the extension lives, and how it persists
- **A new tally member.** `Tally` gains one parameter at the end, `NightExtension? Extension = null`, so every
  existing `new Tally(a, b, c, d, e)` still compiles. In `tally.json` it is the member `extension`:
  `"extension": {"night": "2026-10-26", "minutes": 20}`, or null or absent for none.
- **Backward compatible.** A `tally.json` written before this change has no `extension` member and loads as
  `Extension` null, with `Corrupt` false and every other value unchanged (additive schema change only).
- **Validation in `TallyStore.IsValid`,** in the style of the existing count checks: an `Extension` that is not null
  must have `Minutes` in `NightExtension.Lengths`. Any other value makes the file count as corrupt, exactly as a
  negative count does today. A `Night` that is not tonight is not corrupt; `TallyStore` loads and saves it unchanged, and `NightClock.RollOver` clears it.
- **One night only.** `NightClock.RollOver` keeps the tally's extension only when its `Night` equals the night the
  local time names, else sets it to null. So it is cleared at the 4 AM night change by the same call that zeroes the
  night's seconds, with no job and no timer.
- **Survives a kill, with one caveat.** The worker saves the tally the moment an extension is applied, and a
  restarted service loads it back. If that save fails, the reply says so (section 4) and the extension exists only in
  memory until the next tally save (the worker retries within a minute); a kill before then loses it.

### 2. The exact accounting
**L** is the night's original limit in minutes (`Budget.NightLimitMinutes(night, settings)`, without the extension).
**E** is the night's extension minutes (0, 10, 20, or 30). Night seconds is the tally's real played seconds; it is
never doubled.
- **One rule for the original limit.** `Budget.OriginalLimitSeconds(GateSettings settings, DateTime localNow)` (new)
  returns `NightLimitMinutes(NightClock.NightOf(localNow), settings) * 60.0`. `Accountant.Tick`, `Budget.RemainingSeconds`,
  and `ExtendRules.Apply` call it; the worker computes no limit itself.
- **Tonight's limit is L + E.** `Budget.TonightLimitMinutes(Tally tally, GateSettings settings, DateTime localNow)` (new)
  rolls the tally over and returns `NightLimitMinutes(night, settings) + (rolled.Extension?.Minutes ?? 0)`. The worker
  puts it in `GateSnapshot.TonightLimitMinutes`, so the state, the Tonight meter, and the time left use it.
- **Seconds past the original limit add double to the week.** `Accountant.Tick` takes a last parameter
  `GateSettings settings` (required) and gets the original limit from `Budget.OriginalLimitSeconds(settings, localNow)`.
  With a game open `Tick` adds the step to the night as now and adds
  `ExtensionCost.WeekCharge(oldNight, step, originalLimitSeconds, rolled.Extension is not null)` to the week. The night
  tally keeps real seconds. Seconds before the old limit, and seconds past it on a night with no extension (the 60 seconds of a polite
  close), are charged once; on a night with an extension every second past the original limit is doubled. Seconds already counted are never re-charged when an extension is applied. Unused
  extension time costs nothing (Trevor's example, pinned in the amendment); extension time not used yet stays usable tonight at double cost.
- **The original limit is re-read each tick.** Because every tick reads the limit from the settings, a limit change after an
  extension (a tightening applies at once; a loosening applies after its cooldown) moves the line where doubling starts;
  the seconds already charged are not re-charged.
- **Time left counts the doubling.** `Budget.RemainingSeconds` returns `max(0, min(nightLeft, weekPlayable))` with
  `nightLeft = (L + E) * 60 - nightSeconds`, `weekLeft = weeklyCap * 60 - weekSeconds`, and
  `weekPlayable = ExtensionCost.PlayableWeekSeconds(weekLeft, nightSeconds, Budget.OriginalLimitSeconds(settings, localNow), E > 0)`.
  With no extension it returns exactly what it returned before this issue. So the weekly cap stays hard: the service warns and
  closes on this number, even in the middle of an extension.
- **Tonight left on the screen stays the night's own number.** `StatusText.TonightLeft` is the tonight limit (L + E) minus night seconds, as today, and `StatusText.WeekLeft` is the cap minus week seconds. Neither meter folds in the other, and neither is `Budget.RemainingSeconds`. The warnings count down the service's `RemainingSeconds`, which is the smaller of the two after doubling. So when the week, not the night, limits play during an extension, Tonight left can read more than the warnings count. This is the same as today (a tight week already shows a larger Tonight left than the warnings use); folding the week into the Tonight meter would change an approved meter and is not built.
- **The meters keep their approved numbers; only the two used-line suffixes from card 2 are added.** The This week meter
  shows cap minus week seconds (which already include the doubled time), rounded up. When the week is what limits play, that
  meter can read up to twice the minutes the warnings count down, because time past the old limit costs double; the preview's
  card 2 says so on the meter.

### 3. The availability rule (one Core function, used by the service and the screen)
`ExtendRules.View(double originalLimitSeconds, int usedMinutes, double nightSeconds, int weeklyCapMinutes,
double weekSeconds)` returns `ExtendView(ExtendKind Kind, IReadOnlyList<int> Offered, string Note)`.
The lengths are `NightExtension.Lengths`; `ExtendRules.WindowSeconds` is 1800. With
`tonightLeft = originalLimitSeconds - nightSeconds` and `weekLeft = weeklyCapMinutes * 60 - weekSeconds`:
- `Kind` is `Used` when `usedMinutes > 0`; else `NotInWindow` when `tonightLeft <= 0` or `tonightLeft > 1800`; else
  `Open`. The window is "tonight's remaining time is at most 30 minutes and the limit has not been reached", measured
  against the original limit. After the limit is reached (the polite-close wait and after the force close) the kind is
  `NotInWindow` and the buttons stay greyed with the same approved note; extending during the polite-close wait is not
  built. SteamGate being turned on or off plays no part.
- `Offered` is empty unless `Kind` is `Open`. When `Open`, a length N is offered if and only if
  `weekLeft - tonightLeft >= ExtensionCost.FullExtensionWeekSeconds(N)`: what is left of the week after tonight's own remaining
  time must cover the N extension minutes at double cost. Then, after applying N,
  `Budget.RemainingSeconds` equals `(L + N) * 60 - nightSeconds`, so "Tonight ends N min later" is always true.
- `Note` is: for `Used`, `Extension used tonight: <usedMinutes> min.`; for `NotInWindow`,
  `Extend is available in the last 30 minutes before tonight's limit.`; for `Open` with all three offered,
  `Once per night, in the last 30 minutes before tonight's limit.`; for `Open` with at least one length not offered (the week is the only reason), `ExtendText.WeekNote`, which is `Not enough time left this week.` with no numbers. It is the same text whichever lengths are greyed.
- `ExtendText.WeekNote` holds the wording and `ExtendText.UsedNote(int minutes)` the used note. `ExtendRules` holds no sentence text and has no `WeekSentence`. The service uses `WeekNote` for the refusal of the
  exact length asked.
- On the screen the original limit is `StatusText.OriginalLimitSeconds(StateMessage state)`, which is `(StatusText.TonightLimit(state) - state.ExtensionMinutes) * 60.0`, and `StatusView.ExtendSection` uses it. When the state's
  `TonightLimitMinutes` is null, `TonightLimit` falls back to the weeknight or weekend-night limit for tonight plus
  `ExtensionMinutes`, so the original limit is that setting.

### 4. The pipe message, and the service re-checking everything
- **New client message `extend`,** sent by the status screen: `{"type":"extend","requestId":"s7","minutes":20}`.
  `ExtendMessage(string RequestId, int Minutes)` in `PipeMessage.cs`, type name `extend`, parsed by `PipeMessage.Parse`
  (null for a missing or non-string `requestId`, and for a `minutes` that is missing, not a JSON number written as an integer (so `20.0` and `20.5` are refused), or too large for an `int`; a connection that sends such a line is dropped like any malformed line) and written with
  `Serialize` and `ToLine`. `Parse` does not check the value against the allowed lengths: the service does, so a wrong
  length gets a `Not allowed.` reply instead of a dropped connection. The request id is the same kind as `request`'s (over
  64 characters drops the connection, in `ConnectionRules.DropsOnClientMessage`). As with `request` and `cancel`, the
  server accepts it from any connection that has said hello (0006's accepted rule that any signed-in account may send
  requests; here that account could spend Trevor's one extension for the night, which is accepted the same way, because it
  can only add time Trevor himself may add).
- **One channel, one type per kind.** `WorkerCommand` is a new abstract record with get-only properties `RequestId`, `Replies`, `ClientPid`, and `SessionId` and an explicit `private protected WorkerCommand(string requestId, OutboundQueue replies, int clientPid, int sessionId)` constructor (not the one-line positional form). `SettingsCommand : WorkerCommand` keeps its request-or-cancel members as today.
  `ExtendCommand(string RequestId, int Minutes, OutboundQueue Replies, int ClientPid, int SessionId) : WorkerCommand(RequestId, Replies, ClientPid, SessionId)` is new, and `SettingsCommand` passes its shared four members to `WorkerCommand` the same way, and its constructor throws `ArgumentException` unless exactly one of `Request` or `CancelId` is set. The channel becomes `Channel<WorkerCommand>` (capacity 32, in
  `Program.cs`, `PipeServer`, and `GateWorker`). `PipeServer.Submit` writes an `extend` as an `ExtendCommand`; a full
  channel answers `Busy, try again.` at once and logs `extend busy pid=<pid> session=<session>` through the request log
  (`ChangeLog.Extend`). The server decides nothing about the extension.
- **The worker decides.** `RunTickAsync` drains the channel once into two lists by type match (`SettingsCommand`,
  `ExtendCommand`; anything else throws). `RunLock` takes the settings list and keeps its order (count down, answer
  settings requests, apply what is due), dispatching a command with `Request` to `ChangeEngine.Request` and one with
  `CancelId` to `ChangeEngine.Cancel` (a `SettingsCommand` always has exactly one of the two, so no `?? Guid.Empty` is needed). A new `RunExtend` takes the extend list and runs in `RunTickAsync` immediately
  before `CountTime(`, so a press with seconds left is judged on the tally before this tick's count, and `CountTime` then
  charges this tick with the new extension. `RunExtend` answers the commands in arrival order with
  `ExtendRules.Apply(_tally, _settings, localNow, minutes)`. When it applies, the worker replaces `_tally`, saves the
  tally at once (`CountTime` writes it again in the same tick when a game is open; the double write is accepted), and logs `extension <minutes> night=<yyyy-MM-dd>` to `service.log` (fixed fields). A second `extend` in
  the same tick sees the first and is refused with `Extension used tonight: <E> min.` Every extend command, applied or
  refused, is logged through the request log as `ChangeLog.Extend(ChangeLog.ResultName(applied ? ChangeResult.Applied :
  ChangeResult.Refused), command.ClientPid, command.SessionId)`. The service never trusts the screen: `Apply` re-checks the length, the window, the used flag, and
  the week from its own tally and settings.
  `ExtendRules.Apply(Tally tally, GateSettings settings, DateTime localNow, int minutes)` returns
  `ExtendOutcome(Tally Updated, bool Applied, string Text)`. It gets the original limit from `Budget.OriginalLimitSeconds(settings, localNow)`. Order of checks: the length (not in
  `NightExtension.Lengths` gives `ChangeReply.NotAllowed`, `Not allowed.`), then `View`'s kind (its `Note` is the refusal text for `Used` and
  `NotInWindow`), then the week rule for that length (`ExtendText.WeekNote`), then applied. On refusal `Updated` is the input
  tally. On success `Updated` is the rolled tally with `Extension = new NightExtension(rolled.Night, minutes)` and nothing
  else changed.
- **Reply texts (exact).** Applied: `ExtendText.AppliedPrefix` (`Applied now. Tonight ends `) followed by `<N> min later (limit
  <DurationText.Minutes(L + N)>).` (card 2: `Applied now. Tonight ends 20 min later (limit 1 h 50 min).`); `ExtendRules.Apply` builds it
  from `ExtendText` and holds no copy of the prefix. Refused: one of the two refusal notes, `Not enough time left this week.`,
  or `Not allowed.`; a full channel, `Busy, try again.` The reply is a `ReplyMessage` with `ok` true only when applied.
  When the tally save failed, the applied text gets the existing ` Saved in memory; will retry.` suffix
  (`ChangeReply.SaveFailedSuffix`). `ChangeMessages.ExtendReply(string requestId, ExtendOutcome outcome, bool
  saveFailed)` builds it, in the shape of `ChangeMessages.Reply` (the Pipe code may depend on the Extend code, not the reverse).
- **Reply color.** `StatusText.ReplyIsNow(text)` (green): the text with a trailing ` Saved in memory; will retry.` removed
  (once, if present) equals `Applied now.` or starts with `ExtendText.AppliedPrefix`. Every other text is amber. `StatusText` holds no copy of the prefix.
- **The state message gets one field.** `StateMessage` gains `ExtensionMinutes` (int, last constructor parameter, default
  0), JSON member `extensionMinutes`, written right after `tonightWeekend`, and included in `Equals` and `GetHashCode`.
  `StatusState.From` reads it from the rolled tally and passes it inside the new `StateMessage(...)` call before the line is measured for the byte cap. `PipeMessage.Parse` reads it as optional: absent or null gives 0; any other value
  must be 0 or in `NightExtension.Lengths`, else the line is not a state. `tonightLimitMinutes` already carries L + E.
  `StateMessage.MaxLineBytes` (524288) and the byte-cap trim in `StatusState.From` are unchanged and still hold.
  `StatusFeed` needs no change.

### 5. What the extension does to the warnings
`EnforcementPlanner` is not edited. It already re-arms on its own, because it compares the smallest threshold reached with
the smallest issued: when the time left rises (an extension raises it), a larger threshold than the issued one is
reached, it is recorded as issued without a warning, and every smaller threshold warns again as the time falls through
it. The rule, stated:
- After an extension, each threshold below the new time left warns again as the time falls through it, once per approach.
  A threshold at or above the new time left is not announced at once, and none is announced twice for the same fall. If
  the new time left is above 30 minutes, all five thresholds, including 30, warn again in turn.
- No polite close can be in progress when an extension arrives: `ExtendRules` refuses once tonight's original limit is
  reached, and refuses a length the week cannot cover, so at the moment an extension is applied it leaves more than zero
  seconds left, and the planner drops all close state whenever the time left is above zero.
- The warning minutes are `WarnMinutes.FromRemaining` of `RemainingSeconds`, which counts the doubling.

### 6. Clock, boot, and the 4 AM change
- `ExtendRules`, `ExtendText`, `ExtensionCost`, and the extend path in the worker do not read or write
  `GateSettings.Clock`, `LockClock`, or `BootMarker`. Those drive only the cooldown countdown for pending changes. An
  extension needs no countdown, writes no `settings.json`, and is neither helped nor hurt by a restart of Windows or of
  the service: it is in `tally.json`, tied to a night date.
- It uses the same local-time reading the counter uses (`NightClock`), so the approved gap "changing the system clock
  could fool the counter" covers it and no new gap is created.
- **4 AM mid-extension.** At 04:00 `RollOver` drops the extension; the new night has its own limit, no extension, and 0
  seconds, and a game still running is counted against the new night at single cost. The old night's doubled seconds stay
  charged to the week they belong to.

### 7. Where the code lives, so it is mutation-testable
All decisions and texts are `SteamGate.Core` under unit tests and the Stryker gate. `SteamGate.Service` and
`SteamGate.Status` only connect them to Windows. The window draws; it holds no rule and no string of its own for this
section.

### 8. Look (`docs/preview-2026-10-26-extend.html`, cards 1 to 3)
- **Placement.** The `EXTEND TONIGHT` section sits directly under the two meters and above the Rules section. Always
  shown, but like the other sections it is hidden until the first state arrives. Heading text `Extend tonight`, upper-cased by the existing section style.
- **Which length is picked.** `EditorState.ExtendChoice` (null at first) holds the length he clicked. It counts only while that
  length is offered, and `StatusView.Merge` sets it to null whenever the new state's `ExtensionMinutes` is greater than 0. The picked length shown is: `ExtendChoice` when it is offered; else
  20 min when all three lengths are offered (card 1); else none (card 3 case 3, where only some are offered, shows none picked
  until he clicks one).
- **Parts (card 1).** In the existing editor box (filled `AccentSoft`): the label `Finish your round:`, then three
  buttons `10 min`, `20 min`, `30 min` in the existing button style, the picked one in the existing primary style; below
  the buttons, only when a length is picked, the explanation line in the wait verdict style (`Adds <N> min to tonight (limit
  becomes <DurationText.Minutes(L + N)>). Up to <2N> min comes off this week, because time past tonight's limit counts
  double.` where `<2N>` is plain minutes, `20`, `40`, or `60`) and a primary `Extend` button on its own line; below the
  box, the note line. Pressing `Extend` calls `StatusView.ExtendSent(editor, minutes)` (which sets `ExtendChoice` to the sent length and `ExtendWaiting` to true) and sends `extend`; the page calls `ExtendSent` only after `Send(...)` returns true, and a send that fails leaves the editor unchanged. While `ExtendWaiting` is true, further clicks on the three length buttons and on `Extend` are ignored (`StatusView.ExtendClickAllowed(editor)` is false) and nothing on screen changes. `ExtendWaiting` is cleared when the reply arrives or the connection is lost.
- **After pressing (card 2).** When the extension is used (`extensionMinutes` greater than 0) and `ExtendApplied` is true, the section shows only the green reply line directly under the heading and the note
  `Extension used tonight: <E> min. Only the minutes you actually play past the old limit are counted double.` (no box, no
  buttons). When `ExtendApplied` is true but the state's `extensionMinutes` is still 0 (the reply arrived one draw before the state), the layout is also card 2, with `<E>` taken from the picked length `ExtendChoice`. When the extension is used and no applied reply is on screen (the screen was opened later), card 3 case 2: the
  box with all three buttons greyed and the note `Extension used tonight: <E> min.` The Tonight meter's used line reads
  `<Used(night, L + E)> (<E> min extension)` and the This week meter's reads `<Used(week, cap)> (past the old limit counts
  double)` whenever `extensionMinutes` is greater than 0.
- **Not available (card 3).** The box shows `Finish your round:` and the three buttons with the ones not offered greyed
  (half opacity); no explanation line and no `Extend` button when no length is picked; the note under the box is
  `ExtendView.Note`. Outside the window and after the limit: all three greyed with the approved window note. Some lengths
  not fitting: the ones that fit are enabled and the others greyed, with the week note `Not enough time left this week.`
- **Reply line.** The service's `reply` text for an `extend`, verbatim, under the box (with the existing `ReplyGapAbove`
  space above it) and above the note when the box is shown, and directly under the heading in the card 2 layout. Green or
  amber by `ReplyIsNow` (color only). It is not shown when its text equals the section's note (no duplicate line). It stays
  until the next `extend`, a new night, or a state with `extensionMinutes` 0 while `ExtendApplied` is true. `EditorState` also holds `bool ExtendApplied` (set from the reply's `ok` flag by
  `StatusView.ExtendAnswered`, cleared when the reply is cleared) and `DateOnly? ExtendReplyNight` (the night the reply came
  in, which the window passes as `NightClock.NightOf(DateTime.Now)`). The card 2 layout uses `ExtendApplied`, not the text or
  its color. `EditorState.WithReply(ReplySection.Extend, null)` also sets `ExtendApplied` false and `ExtendReplyNight` null. The page sends `extend` through `StatusView.ExtendSent` and records a click on a length with `StatusView.ExtendPick(EditorState editor, int minutes)` (returns the editor with `ExtendChoice = minutes`). `Merge` called with a night parameter different from `ExtendReplyNight` drops any Extend reply; a state with
  `extensionMinutes` 0 drops a reply whose `ExtendApplied` is true. Another reply for the same night, such as `Not allowed.`,
  stays. New `ReplySection.Extend`.
- **No stale state after an applied reply, with no new machinery.** The worker posts the `extend` reply to the sender's own outbound queue inside `RunExtend`, and the tick publishes its state (`PublishState`) only after `CountTime`, so every state built after the reply includes the extension. Every state built before it was posted to the same queue in an earlier tick and is delivered ahead of the reply (the queue is first in, first out). So after an applied reply the connection receives only states with `extensionMinutes` greater than 0 for that night. `RunExtend` itself publishes no state. `Merge` still drops an applied reply on a state with `extensionMinutes` 0 as above (that covers a new night or a screen opened later).
- **Not connected.** The three length buttons and `Extend` are disabled with all the other buttons (extras card 4). When
  the connection ends, `StatusForm.OnConnected(false)` calls `StatusPage.ConnectionLost()`, which clears `ExtendWaiting`
  through `StatusView.ConnectionLost(EditorState)`.
- **No new style value.** Every size, color, padding, gap, and radius the three cards use is already in `StatusStyle` and
  pinned by `StatusStyleTests.MatchesApprovedPreview`: the `.editor` box (`EditorPaddingY` 10, `EditorPaddingX` 12,
  `EditorRadius` 4, `EditorGap` 8, `EditorLineGap` 8, `AccentSoft`), `.verdict` (`VerdictPaddingY` 6, `VerdictPaddingX` 10,
  `VerdictRadius` 4, `VerdictFont` 13), buttons (`ButtonPaddingY` 5, `ButtonPaddingX` 12, `ButtonRadius` 4, `ButtonFont` 13,
  `DisabledOpacity` 0.5), the section heading (`SectionFont` 12, `SectionGapBelow` 8), the note (`NoteFont` 12,
  `NoteParagraphMargin` 12), the label (`BodyFont` 14), and `SectionGap` 24. This issue changes none and adds none.

## Acceptance criteria
A named section runs from its heading to the next heading of the same or higher level. Unless stated, tests use default
settings (weeknight 90, weekend night 180, weekend nights Friday, Saturday, and Sunday, weekly cap 600, cooldown 48, the four
approved not-counted folders, nothing pending). 2026-10-26 is a Monday (a weeknight); 2026-10-30 is a Friday (a weekend
night). `Ext(d, m)` means `NightExtension(d, m)`.

0. **Given** `docs/approved-spec-2026-10-20.md`, DESIGN.md, and this issue, **When** a reader greps them, **Then** the docs
   file and DESIGN.md each contain a heading that contains `(extension button)` whose body names
   `preview-2026-10-26-extend.html`, quotes Trevor (`looks good`), quotes his 15-minute example (`I should be charged
   30`), and records his 2026-10-27 answer (`yes do that` and `Not enough time left this week.`); this issue's "Owner look gate" section quotes `looks good`, contains `card 1 shows the section with 20 min picked`,
   `card 3 case 3 shows none picked`, and `goes back through issue review`; its Owner look gate quotes `yes do that` with the date `2026-10-27` and contains `Not enough time left this week.`; and its "Decisions for Trevor" section contains `None open.`
1. **Given** `TallyStore`, **When** a file holding `{"night":"2026-10-26","nightSeconds":100,"week":"2026-10-26","weekSeconds":200,"lastCountedUtc":null}`
   (no `extension` member) is loaded, **Then** the result has `Corrupt` false, `Extension` null, and the other five values as
   written; **when** a tally with `Extension` `Ext(2026-10-26, 20)` is saved, **Then** a test parses the file text as JSON and
   asserts that its `extension.night` is the string `2026-10-26` and `extension.minutes` is the number 20, and a new
   `TallyStore` on the same folder loads a tally equal to the saved one. (Test `TallyStoreTests.ExtensionRoundTrips`,
   `TallyStoreTests.OldFileLoadsWithNoExtension`.)
2. **Given** `TallyStore`, **When** a file holds an `extension` of `{"night":"2026-10-26","minutes":25}`,
   `{"night":"2026-10-26","minutes":0}`, `{"night":"2026-10-26","minutes":-10}`, or `{"night":"2026-10-26","minutes":40}`,
   **Then** each loads as `Corrupt` true with the defaults and the bad file kept under a `corrupt-` name, and `Save` of a
   tally with such an extension returns false and leaves the file unchanged; **and** an extension for another night,
   `Ext(2026-10-20, 10)`, loads and saves unchanged with `Corrupt` false. `NightExtension.Lengths` is [10, 20, 30]. (Test
   `TallyStoreTests.BadExtensionIsCorrupt`.)
3. **Given** `NightClock.RollOver`, **When** it runs on a tally with Night 2026-10-26, NightSeconds 5000, Week 2026-10-26,
   WeekSeconds 9000, `Ext(2026-10-26, 20)` at local time 2026-10-26 21:00, **Then** the result is unchanged, extension
   included; **at** 2026-10-27 03:59 the extension is kept; **at** 2026-10-27 04:00 the result has Night 2026-10-27,
   NightSeconds 0, WeekSeconds 9000, and `Extension` null; and a tally with `Ext(2026-10-25, 20)` and Night 2026-10-26 rolled
   at 2026-10-26 21:00 has `Extension` null. (Test `NightClockTests.ExtensionLivesOneNight`.)
4. **Given** `Accountant.Tick` with one open game, step 15 s, max step 15 s, default settings, local time 2026-10-26 21:00, and a
   tally with Night and Week 2026-10-26 (the original limit is then 5400 s), **When** it runs, **Then**:
   (a) NightSeconds 5400, WeekSeconds 10000, `Ext(2026-10-26, 30)` gives NightSeconds 5415 and WeekSeconds 10030;
   (b) NightSeconds 5390 with the same extension gives 5405 and 10020; (c) NightSeconds 5400, WeekSeconds 10000, no extension
   gives 5415 and 10015 (a polite-close second is charged once); (d) NightSeconds 5000 with the extension gives 5015 and
   10015; (e) step 720 s and max step 720 s, NightSeconds 5400, WeekSeconds 10000, `Ext(2026-10-26, 30)` gives NightSeconds
   6120 and WeekSeconds 11440 (take 30, play 12 past the old limit: the week is charged 24 for those, 1440 s); (f) NightSeconds 5400, WeekSeconds 10000, `Ext(2026-10-26, 30)`, and no open game leaves 5400 and 10000 and `LastCountedUtc` null; (g) Night 2026-10-26 with a stale
   `Ext(2026-10-25, 30)`, NightSeconds 5400, WeekSeconds 10000 gives 5415 and 10015; (h) local time 2026-10-27 04:00:05 with
   tally Night 2026-10-26, NightSeconds 5400, WeekSeconds 10000, `Ext(2026-10-26, 30)` gives Night 2026-10-27, NightSeconds 15,
   WeekSeconds 10015, `Extension` null; (i) **Trevor's example, pinned in the amendment** ("say I extend 30 minutes but I only
   use 15 ... I should be charged 30"): step 900 s and max step 900 s, NightSeconds 5400, WeekSeconds 10000, `Ext(2026-10-26,
   30)` gives NightSeconds 6300 and WeekSeconds 11800: the week is charged 1800 s (30 min, the 15 played doubled), the other 15
   minutes of the extension cost nothing, and `Budget.RemainingSeconds` on that result is 900 (the unused 15 minutes are still
   playable); (j) a second tick of step 900 s on the result of (i) gives NightSeconds 7200 and WeekSeconds 13600 (the rest of
   the extension is used at double cost); (k) with settings whose weeknight limit is 60 (lowered after the extension, so the
   original limit is 3600 s), NightSeconds 4000, WeekSeconds 10000, `Ext(2026-10-26, 30)` gives 4015 and 10030 (the line where
   doubling starts moved with the limit). (l) NightSeconds 7200, WeekSeconds 13600, `Ext(2026-10-26, 30)`, step 15 gives 7215 and 13630 (every second past the original limit is doubled while the night has an extension); (m) NightSeconds 7195 with the same gives 7210 and 13630. (Test `AccountantTests.PastTheOldLimitCountsDouble`.)
5. **Given** `Budget`, **When** `TonightLimitMinutes` and `RemainingSeconds` run at local time 2026-10-26 21:00 on a tally with
   Night and Week 2026-10-26, **Then**: NightSeconds 4680, WeekSeconds 22200, `Ext(2026-10-26, 20)` gives limit 110 and
   remaining 1920; NightSeconds 5700 with the same gives remaining 900; NightSeconds 5400, WeekSeconds 35400, `Ext(.., 20)`
   gives remaining 300; NightSeconds 5300, WeekSeconds 35600, `Ext(.., 30)` gives remaining 250; NightSeconds 4800, WeekSeconds
   35700, `Ext(.., 20)` gives remaining 300; with no extension NightSeconds 4680, WeekSeconds 22200 gives limit 90 and remaining
   720 (unchanged from before); a tally with Night 2026-10-25 and
   `Ext(2026-10-25, 20)` gives limit 90. `Budget.OriginalLimitSeconds(defaults, ...)` is 5400 at 2026-10-26 21:00, 10800 at
   2026-10-30 21:00 (Friday), 10800 at 2026-10-31 02:00 (still the Friday night), and 10800 at 2026-10-26 03:00 (still the
   Sunday night). (Test
   `BudgetTests.ExtensionRaisesTheLimitAndTheWeekCostsDouble`, `BudgetTests.OriginalLimitSeconds`.)
5b. **Given** `ExtensionCost`, **When** it runs, **Then** `PastLimitWeight` is 2; `WeekCharge(5390, 15, 5400, true)` is 20;
   `WeekCharge(5400, 15, 5400, true)` is 30; `WeekCharge(5400, 15, 5400, false)` is 15; `WeekCharge(5000, 15, 5400, true)` is 15;
   `WeekCharge(7200, 15, 5400, true)` is 30; `WeekCharge(7195, 15, 5400, true)` is 30;
   `PlayableWeekSeconds(13800, 5300, 5400, true)` is 6950; `PlayableWeekSeconds(400, 5300, 5400, true)` is 250;
   `PlayableWeekSeconds(300, 4800, 5400, true)` is 300; `PlayableWeekSeconds(13800, 5300, 5400, false)` is 13800;
   `FullExtensionWeekSeconds(20)` is 2400. **And given** `Accountant.cs`, `Budget.cs`, `ExtendRules.cs`, and `ExtendText.cs`,
   **When** a reader greps them, **Then** each contains `ExtensionCost.` and none matches the word-boundary patterns `/ 2\b`, `\* 2\b`, or `120 \*`. (Test
   `ExtensionCostTests.Values`.)
6. **Given** `ExtendRules.View` (original limit in seconds, used minutes, night seconds, weekly cap, week seconds), **When** it runs,
   **Then**:
   (a) `View(5400, 0, 4680, 600, 22200)` is `Open`, offers [10, 20, 30], note `Once per night, in the last 30 minutes before
   tonight's limit.`;
   (b) `View(5400, 0, 3600, 600, 0)` is `Open` (exactly 30 minutes left); `View(5400, 0, 3599.9, 600, 0)` is `NotInWindow` with note
   `Extend is available in the last 30 minutes before tonight's limit.` and offers nothing;
   (c) `View(5400, 0, 5399.9, 600, 0)` is `Open`; `View(5400, 0, 5400, 600, 0)` and `View(5400, 0, 6000, 600, 0)` are `NotInWindow` with
   the same note as (b) and offer nothing;
   (d) `View(5400, 20, 4680, 600, 22200)` is `Used` with note `Extension used tonight: 20 min.` and offers nothing;
   `View(5400, 20, 5400, 600, 0)` is also `Used`;
   (e) `View(5400, 0, 4680, 600, 32400)` (tonight left 720 s, week left 3600 s) offers [10, 20] and its note is `Not enough time left this week.`;
   (f) `View(5400, 0, 4680, 600, 34080)` (week left exactly 1920 s = 720 + 1200) offers [10] and its note is `Not enough time left this week.`; `View(5400, 0, 4680, 600, 34080.1)` offers none and its note is `Not enough time left this week.`;
   (g) `View(5400, 0, 4680, 600, 34500)` (week left 1500 s) offers none and its note is `Not enough time left this week.`;
   (h) `View(5400, 0, 4680, 600, 36000)` and `View(5400, 0, 4680, 600, 40000)` offer none, with note `Not enough time left this week.`;
   (i) `ExtendRules.WindowSeconds` is 1800 and `ExtendRules.WindowSeconds / 60` is 30.
   (Test `ExtendRulesTests.ViewRules`.)
7. **Given** `ExtendRules.Apply(tally, settings, localNow, minutes)` at local time 2026-10-26 21:00 with defaults and a tally
   with Night and Week 2026-10-26, NightSeconds 4680, WeekSeconds 22200, no extension, **When** it runs, **Then**:
   (a) minutes 20 gives `Applied` true, text `Applied now. Tonight ends 20 min later (limit 1 h 50 min).`, `Updated.Extension`
   equal to `Ext(2026-10-26, 20)`, and `NightSeconds`, `WeekSeconds`, and `LastCountedUtc` unchanged;
   (b) applying 10 to that result gives `Applied` false, text `Extension used tonight: 20 min.`, and the tally unchanged;
   (c) minutes 25, 0, -10, 40, and `int.MaxValue` each give `Applied` false, text `Not allowed.`, tally unchanged;
   (d) NightSeconds 3000 with minutes 20 gives `Extend is available in the last 30 minutes before tonight's limit.`; NightSeconds 3600 with minutes 20 applies;
   (e) NightSeconds 5400 and NightSeconds 6000, each with minutes 20, give `Extend is available in the last 30 minutes before tonight's limit.`;
   (f) WeekSeconds 32400: minutes 30 gives `Not enough time left this week.` with the tally unchanged, while
   minutes 20 applies; WeekSeconds 34100 with minutes 10 gives `Not enough time left this week.`;
   (g) a tally with Night 2026-10-26, NightSeconds 4680, and a stale `Ext(2026-10-25, 20)` is treated as having no extension:
   minutes 20 applies and `Updated.Extension` is `Ext(2026-10-26, 20)`;
   (h) at Friday 2026-10-30 21:00 with a tally of Night 2026-10-30, Week 2026-10-26, NightSeconds 10200, WeekSeconds 22200
   (limit 180), minutes 30 gives `Applied now. Tonight ends 30 min later (limit 3 h 30 min).`;
   (i) with settings weeknight 20 and weekly cap 50 and a tally with NightSeconds 0, WeekSeconds 0 (tonight left 1200 s, week
   left 3000 s): minutes 20 gives `Not enough time left this week.` (the same text as group 23, criterion 23) and minutes 10 gives `Applied now.
   Tonight ends 10 min later (limit 30 min).`;
   (j) **the text is always true:** for each of 10, 20, 30 applied to the tally above, `Budget.RemainingSeconds` on `Updated`
   at the same local time is `(90 + N) * 60 - 4680`, that is 1320, 1920, and 2520; and with WeekSeconds 34080 (week left
   exactly the need for 10), minutes 10 applies and `Budget.RemainingSeconds` is 1320.
   (k) a tally with Night 2026-10-25, Week 2026-10-19, NightSeconds 4680, at 2026-10-26 21:00, minutes 20 gives `Extend is available in the last 30 minutes before tonight's limit.` and the tally unchanged (`Apply` rolls the tally over before it judges the window).
   (Test `ExtendRulesTests.ApplyRules`, `ExtendRulesTests.TonightEndsWhenTheTextSays`.)
8. **Given** `ExtendText`, **When** read, **Then** `Heading` is `Extend tonight`, `RoundLabel` is `Finish your round:`, `ExtendButton` is
   `Extend`, `Length(20)` is `20 min`, `Explain(20, 90)` is `Adds 20 min to tonight (limit becomes 1 h 50 min). Up to 40 min comes off this
   week, because time past tonight's limit counts double.`, `Explain(30, 180)` is `Adds 30 min to tonight (limit becomes 3 h 30 min). Up to
   60 min comes off this week, because time past tonight's limit counts double.`, `DoneNote(20)` is `Extension used tonight: 20 min. Only the
   minutes you actually play past the old limit are counted double.`, `TonightSuffix(20)` is ` (20 min extension)`, `WeekSuffix` is
   ` (past the old limit counts double)`, `AppliedPrefix` is `Applied now. Tonight ends `, `WeekNote` is `Not enough time left this week.`, and `UsedNote(20)` is `Extension used tonight: 20 min.`; and `ExtendText.cs` builds the two window notes
   (`Extend is available in the last 30 minutes before tonight's limit.` and `Once per night, in the last 30 minutes before tonight's
   limit.`) from `ExtendRules.WindowSeconds / 60` and contains no `30 minutes`. (Test `ExtendTextTests.Texts`.)
8b. **Given** `DurationText.Minutes`, **When** it runs on 45, 60, 61, 90, 600, 0, 230, and 59, **Then** it returns `45 min`, `1 h`,
   `1 h 1 min`, `1 h 30 min`, `10 h`, `0 min`, `3 h 50 min`, and `59 min`. **And given** `src` and `tests`, **When** a reader
   greps them, **Then** no file contains `StatusText.Minutes(`, `StatusText.cs` no longer contains `public static string
   Minutes(`, `ExtendRules.cs` and `StatusText.cs` contain no `Tonight ends`, `ExtendRules.cs` contains no `Not enough time left` and no `Extension used tonight`, no file under `src/SteamGate.Core/Accounting/` contains `SteamGate.Core.Extend`, `ExtendRules`, `SteamGate.Core.Status`,
   or `StatusText`, and no file under `src/SteamGate.Core/Extend/` contains `SteamGate.Core.Status`, `StatusText`,
   `SteamGate.Core.Pipe`, or `WarnMinutes`. The `IsValid` method of `TallyStore.cs` contains `NightExtension.Lengths` and no `30`,
   and the `extensionMinutes` check in `PipeMessage.cs` contains `NightExtension.Lengths` and no `30`. (Test
   `DurationTextTests.Minutes`; the old `StatusTextTests.Minutes` moves there.)
9. **Given** `ExtendMessage`, **When** `new ExtendMessage("s7", 20).Serialize()` runs, **Then** it is exactly
   `{"type":"extend","requestId":"s7","minutes":20}`, `ToLine()` ends with one newline, and `PipeMessage.Parse` of that line gives
   an equal message; **given** `{"type":"extend","requestId":"s7","minutes":25}`, **Then** `Parse` gives `ExtendMessage("s7", 25)`
   (not null); **given** lines with no `requestId`, a numeric `requestId`, no `minutes`, `"minutes":"20"`, `"minutes":20.5`, `"minutes":20.0`,
   `"minutes":null`, and `"minutes":3000000000`, **Then** `Parse` returns null each time without throwing. (Test
   `PipeMessageTests.ExtendMessageRoundTrips`, `PipeMessageTests.BadExtendIsNull`.)
10. **Given** `ConnectionRules.DropsOnClientMessage`, **When** it runs on `ExtendMessage("s1", 20)` it returns false, on an
    `ExtendMessage` with a 64-character id false, and with a 65-character id true; `StateMessage`, `ReplyMessage`,
    `WarnMessage`, `CloseMessage`, and a second `HelloMessage` still return true. (Test
    `ConnectionRulesTests.ExtendIsAllowedAfterHello`.)
11. **Given** a `StateMessage` built as in 0007's criterion 5 with `ExtensionMinutes` 20, **When** serialized and parsed back,
    **Then** the line contains `"extensionMinutes":20` directly after `"tonightWeekend":false`, the parsed message has
    `ExtensionMinutes` 20 and equals the original, and a state with `ExtensionMinutes` 0 serializes `"extensionMinutes":0`; two
    states that differ only in `ExtensionMinutes` are not equal; **given** 0007's criterion 5 literal line (no
    `extensionMinutes`), **Then** it still parses with `ExtensionMinutes` 0, and `StateMessageTests.ParsesTheNullForms` is updated so
    that its re-serialized line equals the input line with `,"extensionMinutes":0` inserted after `"tonightWeekend":false`; **given** a
    state line with `"extensionMinutes":null`, **Then** it parses with `ExtensionMinutes` 0 (null counts as absent); **given** a state line with
    `"extensionMinutes":25`, `-1`, `"20"`, `20.5`, or `20.0`, **Then** `Parse` returns null. (Test
    `StateMessageTests.ExtensionMinutesRoundTrips`, `StateMessageTests.BadExtensionMinutesIsNull`.)
12. **Given** a `GateSnapshot` as in 0007's criterion 9 with `TonightLimitMinutes` 110 and a tally `Tally(2026-10-20, 2820,
    2026-10-19, 22200, null, Ext(2026-10-20, 20))`, **When** `StatusState.From(snapshot, 2026-10-20 21:14 local)` runs, **Then**
    the state has `ExtensionMinutes` 20 and `TonightLimitMinutes` 110; **with** `2026-10-21 21:14 local` it has `ExtensionMinutes`
    0 and `NightSeconds` 0; **with** no extension it has `ExtensionMinutes` 0; **and given** the largest-state snapshot of 0007's criterion 8 with `Ext(2026-10-20, 20)` in its tally, **Then** the UTF-8 byte length of the state's `Serialize()` is at most `StateMessage.MaxLineBytes`. (Test `StatusStateTests.ExtensionMinutesFromTally`.)
13. **Given** `StatusText`, **When** it runs on a state with `TonightLimitMinutes` 110, `ExtensionMinutes` 20, `NightSeconds` 4680,
    `WeeklyCapMinutes` 600, `WeekSeconds` 22200, **Then** `TonightLeft` is `32 min`, `WeekLeft` is `3 h 50 min`, `TonightUsed` is
    `78 of 110 min used (20 min extension)`, `WeekUsed` is `6 h 10 min of 10 h used (past the old limit counts double)`, and
    `FillPercent(4680, 110)` is 71; **with** `ExtensionMinutes` 0 and `TonightLimitMinutes` 90, `TonightUsed` is `78 of 90 min used`
    and `WeekUsed` is `6 h 10 min of 10 h used`; **with** `TonightLimitMinutes` 110, `ExtensionMinutes` 20, `NightSeconds` 5400, `WeeklyCapMinutes` 600, `WeekSeconds` 35400 (the week limits play), `TonightLeft` is `20 min` (6600 - 5400 = 1200 s) and `WeekLeft` is `10 min` (600 s), while the warnings count 300 s (criterion 5); **with** `TonightLimitMinutes` null, `ExtensionMinutes` 20, and a weeknight setting
    of 90, `StatusText.TonightLimit` is 110, and `StatusText.OriginalLimitSeconds(state)` is 5400 for (limit 110, extension 20), for (limit 90, extension 0), and for (limit null, extension 20, weeknight 90). `StatusText.ReplyIsNow` returns true for `Applied now.`, `Applied now. Tonight ends 20
    min later (limit 1 h 50 min).`, and `Applied now. Tonight ends 20 min later (limit 1 h 50 min). Saved in memory; will retry.`, and
    false for `Extension used tonight: 20 min.`, `Extend is available in the last 30 minutes before tonight's limit.`, `Not enough time left this week.`, `Not allowed.`, `Busy, try again.`, and `Applied now. Tonight ends`; and every text in
    0007's criterion 12b keeps its color. (Test `StatusTextTests.ExtensionLines`, `StatusTextTests.ReplyColors`.)
14. **Given** `StatusView.ExtendSection(StateMessage state, EditorState editor)` (returns the layout kind `Choose` or `Done`, the
    three buttons each with its length, whether it is enabled, whether it is picked, the explanation line or null, whether the
    `Extend` button shows, the note, and whether the Extend reply line shows), **When** it runs with `TonightLimitMinutes` 90,
    `ExtensionMinutes` 0, `NightSeconds` 4680, `WeekSeconds` 22200, cap 600, **Then**: with `EditorState.Empty` (`ExtendChoice` null)
    it is `Choose`, buttons 10, 20, 30 all enabled with only 20 picked, the explanation line `Adds 20 min to tonight (limit becomes
    1 h 50 min). Up to 40 min comes off this week, because time past tonight's limit counts double.`, the `Extend` button shown, and
    the note `Once per night, in the last 30 minutes before tonight's limit.` (card 1); with `ExtendChoice` 10 only 10 is picked and
    the explanation reads `Adds 10 min to tonight (limit becomes 1 h 40 min). Up to 20 min comes off this week, because time past
    tonight's limit counts double.`; with `WeekSeconds` 32400 (10 and 20 offered): buttons 10 and 20 are enabled and 30 is disabled,
    `EditorState.Empty` picks none (no explanation, no `Extend` button; card 3 case 3), `ExtendChoice` 30 picks none, `ExtendChoice`
    20 picks 20 with its explanation and the `Extend` button, and the note is `Not enough time left this week.`;
    with `WeekSeconds` 33600 only the 10 button is enabled, none is picked, and the note is `Not enough time left this week.`; with `WeekSeconds` 34500 all three are disabled, none is picked, there is no explanation and no `Extend`
    button, and the note is `Not enough time left this week.`; with `NightSeconds` 3000 all three are disabled and
    the note is `Extend is available in the last 30 minutes before tonight's limit.` (card 3, case 1); the same as the first case but
    `TonightLimitMinutes` null and weeknight setting 90 gives the same buttons and note; with `TonightLimitMinutes` 110,
    `ExtensionMinutes` 20, `NightSeconds` 4680 and no Extend reply, it is `Choose`, all three disabled, none picked, and the note is
    `Extension used tonight: 20 min.` (card 3, case 2); with the same state and an editor whose Extend reply is `Applied now. Tonight
    ends 20 min later (limit 1 h 50 min).` and `ExtendApplied` true it is `Done`, no buttons, the note `Extension used tonight: 20 min.
    Only the minutes you actually play past the old limit are counted double.` (card 2), and the reply line shows; with the same state
    and an editor whose Extend reply is `Not allowed.` and `ExtendApplied` false it is `Choose` as in case 2 and the reply line shows;
    with the same state and the Extend reply `Extension used tonight: 20 min.` (equal to the note, `ExtendApplied` false) the reply line
    does not show; with the first state, `ExtendChoice` 20 and `ExtendWaiting` true, the section is the same as with `ExtendWaiting` false, and `StatusView.ExtendClickAllowed(editor)` is false (true when `ExtendWaiting` is false); and with the first state (`ExtensionMinutes` 0), an editor whose Extend reply is `Applied now. Tonight ends 20 min later (limit 1 h 50 min).`, `ExtendApplied` true, and `ExtendChoice` 20, it is `Done` with no buttons, the reply line shows, and the note is `Extension used tonight: 20 min. Only the minutes you actually play past the old limit are counted double.` `StatusView.ExtendPick(EditorState.Empty, 10)` has `ExtendChoice` 10 and every other member equal to the input. (Test
    `StatusViewTests.ExtendSection`.)
15. **Given** `StatusView.Merge(editor, next, night)` (the third parameter, new, is tonight's night; existing tests pass it), **When** an
    `EditorState` with `ExtendChoice` 20 and `ExtendWaiting` true meets a new state with `WeekSeconds` 34500 (nothing offered) and
    `ExtensionMinutes` 0, **Then** `ExtendChoice` stays 20 and `ExtendWaiting` stays true, and the other members equal the input; **when**
    `ExtendChoice` 30 meets a state with `ExtensionMinutes` 30, **Then** `ExtendChoice` becomes null; **when** the editor's Extend reply is
    `Applied now. Tonight ends 20 min later (limit 1 h 50 min).` with `ExtendApplied` true and the new state has `ExtensionMinutes` 0,
    **Then** that reply and `ExtendApplied` are dropped; **when** the editor's Extend reply is `Not allowed.` with `ExtendReplyNight`
    2026-10-26 and `Merge` is called with `night` 2026-10-26, **Then** it is kept, and with `night` 2026-10-27, **Then** it is dropped (and
    so is `ExtendReplyNight`); 0007's criterion 16 result is unchanged. `EditorState` has `int? ExtendChoice = null`, `bool ExtendWaiting =
    false`, `bool ExtendApplied = false`, and `DateOnly? ExtendReplyNight = null` as its last four parameters, `EditorState.Empty` is
    unchanged, two values differing only in one of those four are not equal, and equal values have equal hashes. (Test
    `StatusViewTests.MergeKeepsTheChosenLength`.)
15b. **Given** an `EditorState` with an Extend reply `Not allowed.`, `ExtendChoice` 10, and a Rules editor open, **When**
    `StatusView.ExtendSent(editor, 20)` runs, **Then** `ExtendWaiting` is true, `ExtendChoice` is 20, the Extend reply, `ExtendApplied`, and `ExtendReplyNight` are
    cleared, and every other member equals the input; **when** `StatusView.ExtendAnswered(editor, "Applied now. Tonight ends 20 min later
    (limit 1 h 50 min).", ok: true, night: 2026-10-26)` runs on that result, **Then** `ExtendWaiting` is false, the Extend reply is that text,
    `ExtendApplied` is true, `ExtendReplyNight` is 2026-10-26, and every other member equals the input; with `ok: false` and the text
    `Not allowed.`, `ExtendApplied` is false; **when** `StatusView.ConnectionLost(editor)` runs on a state with `ExtendWaiting` true, **Then**
    `ExtendWaiting` is false and every other member, replies included, equals the input. **And given** `src/SteamGate.Status/StatusForm.cs`
    and `StatusPage.cs`, **Then** `StatusForm.cs` contains `ConnectionLost(` and `StatusPage.cs` contains `ConnectionLost(`,
    `StatusView.ExtendSent`, `StatusView.ExtendAnswered`, and `StatusView.ConnectionLost`. **And given** an editor with an Extend reply, `ExtendApplied` true, and `ExtendReplyNight` 2026-10-26, **when** `WithReply(ReplySection.Extend, null)` runs, **Then** the reply is gone, `ExtendApplied` is false, and `ExtendReplyNight` is null. (Test `StatusViewTests.ExtendWaitingLifecycle`.)
16. **Given** `StatusRequests.Extend("s7", 20)`, **When** it runs, **Then** it returns `ExtendMessage("s7", 20)`, and its line is
    `{"type":"extend","requestId":"s7","minutes":20}`. (Test `StatusRequestsTests.ExtendLine`.)
17. **Given** `ChangeMessages.ExtendReply("s7", outcome, saveFailed)`, **When** it runs with an applied `ExtendOutcome` whose text is
    `Applied now. Tonight ends 20 min later (limit 1 h 50 min).`, **Then** the reply has request id `s7`, `ok` true, that text, and no
    pending id; with `saveFailed` true the text ends with ` Saved in memory; will retry.`; with a refused outcome (text `Extension used
    tonight: 20 min.`), `ok` is false and the text is unchanged even when `saveFailed` is true. `ChangeLog.Extend("applied", 12, 1)` is
    `extend applied pid=12 session=1`, `ChangeLog.Extend("refused", 12, 1)` is `extend refused pid=12 session=1`, and
    `ChangeLog.Extend(ChangeLog.Busy, 12, 1)` is `extend busy pid=12 session=1`. (Test `ChangeMessagesTests.ExtendReply` in `tests/SteamGate.Tests/Pipe/ChangeMessagesTests.cs`;
    `ChangeLogTests.ExtendLog` in `tests/SteamGate.Tests/Rules/LockSupportTests.cs`.)
17b. **Given** `WorkerCommand`, **When** a `SettingsCommand` and an `ExtendCommand("s7", 20, queue, 12, 1)` (request id, minutes, queue,
    pid, session, as declared) are read as `WorkerCommand`, **Then** each carries its request id, queue, pid, and session,
    `ExtendCommand.Minutes` is 20, and a type test tells the two kinds apart. `WorkerCommand.cs` contains `private protected WorkerCommand(string requestId, OutboundQueue replies, int clientPid, int sessionId)` and does not contain `abstract record WorkerCommand(`. (Test `WorkerCommandTests.Kinds`.)
17c. **Given** `SettingsCommand`, **When** constructed with only a `Request`, or only a `CancelId`, **Then** it is created; **when** constructed with
    neither, or with both, **Then** it throws `ArgumentException`. (Test `SettingsCommandTests.ExactlyOneKind` in
    `tests/SteamGate.Tests/Rules/SettingsCommandTests.cs`.)
18. **Given** `EnforcementPlanner.Step` with warnings fed in this order, **When** the time left is 1790 (game open, enabled), then 1190,
    then 1785 (an extension of 10 minutes was applied between), then 1190, **Then** the actions are `Warn(30)`, `Warn(20)`, none,
    `Warn(20)`; **and given** a fresh state and the time left 1190 then 2300 (an extension of 20 minutes), then 1790, then 1190,
    **Then** the actions are `Warn(20)`, none, `Warn(30)`, `Warn(20)`; **and given** a game that started after the service did, a polite
    close issued at time left 0 with monotonic time 0 s, **When** the time left is then 600 at 30 s and 0 again at 120 s, **Then** the step
    at 30 s returns no `ForceClose`, and the step at 120 s returns a new `PoliteClose` and no `ForceClose` (the close state was dropped;
    without the drop, 120 s would have been a force close). (Test `PlannerTests.ExtensionRearmsTheWarnings`.)
19. **Given** `src/SteamGate.Service`, **When** a reader greps it, **Then** `GateWorker.cs` contains `ExtendRules.Apply`,
    `ChangeMessages.ExtendReply`, `ChangeLog.Extend(`, `ChangeResult.Applied`, `ChangeResult.Refused`, `extension {`,
    `Budget.TonightLimitMinutes`, `is ExtendCommand`, `is SettingsCommand`, and `Channel<WorkerCommand>`, and no longer contains `?? Guid.Empty`,
    `Channel<SettingsCommand>`, `NightLimitMinutes(` (case-sensitive grep), the first `PublishState(` in `RunTickAsync` comes after `CountTime(` and after `RunExtend(`, `RunExtend` contains no `PublishState(`, or `command with` (no `with` expression is applied to a `SettingsCommand`, which would skip its constructor check); its `Accountant.Tick(` call passes `_settings`; in `RunTickAsync` the call `RunExtend(`
    comes after `RunLock(` and before `CountTime(`, and `CountTime(` comes before `Budget.RemainingSeconds(`; within `RunLock` the first
    occurrence of `ChangeEngine.Request(` or `CancelId` comes before `ChangeEngine.Cancel(`; the method `RunExtend` contains no
    `_settingsDirty`. `PipeServer.cs` contains `ExtendMessage`, `ExtendCommand`, and `ChannelWriter<WorkerCommand>`; `Program.cs` contains
    `Channel.CreateBounded<WorkerCommand>`; neither contains `SettingsCommand>`. `src/SteamGate.Core/Extend/` and
    `src/SteamGate.Core/Accounting/ExtensionCost.cs` contain none of `LockClock`, `BootMarker`, or `.Clock` (a `GateSettings.Clock` access);
    use of `NightClock` is allowed.
20. **Given** `src/SteamGate.Status`, **When** a reader greps it, **Then** `StatusPage.cs` contains `ReplySection.Extend`,
    `StatusRequests.Extend`, `StatusView.ExtendSection(`, and `ExtendText.Heading`; it contains no string literal of the Extend texts (none of
    `Finish your round`, `Extension used tonight`, `Once per night`, `Extend is available`, `counts double`); and it still contains none of
    `NotifyIcon`, `SoundPlayer`, `SystemSounds`, `PlaySound`, `CurrentVersion\Run`, or `Sample data`.
21. **Given** the commit that carries this change and its parent (`HEAD~1`), **When** the files are compared, **Then** these are not
    modified: everything under `src/SteamGate.Core/Enforcement/`, `src/SteamGate.Core/Status/StatusStyle.cs`,
    `tests/SteamGate.Tests/Status/StatusStyleTests.cs`, `install/PipeProbe.ps1`, `.github/workflows/ci.yml`, and everything under
    `src/SteamGate.Helper/`; and in `docs/approved-spec-2026-10-20.md` and in DESIGN.md's `## SteamGate product spec` section every
    line that existed before the `Amendment 2026-10-26 (extension button)` heading is byte for byte unchanged (the only difference is
    that heading and its body, appended). (`PipeProbe.ps1` already sends any `-Line`, reads a reply with `-ReplyFile`, and reads a
    state with `-StateFile`.)
22. **Given** the CI Windows runner, **When** `Test-Install.ps1` runs its new group 21 after group 20 (`Reset-SteamGate @{
    weeknightMinutes = 20; weekendNightMinutes = 20 }`, with the same 15-minute guard before 04:00 that group 10 uses, and a record of
    `settings.json`'s `LastWriteTimeUtc`), **Then** through `Send-PipeRequest`: `{"type":"extend","requestId":"x0","minutes":25}` is
    answered `ok` false with message `Not allowed.`; `{"type":"extend","requestId":"x1","minutes":20}` with `ok` true and message
    `Applied now. Tonight ends 20 min later (limit 40 min).`; `{"type":"extend","requestId":"x2","minutes":10}` with `ok` false and
    `Extension used tonight: 20 min.`; within 70 seconds `tally.json` holds `extension.minutes` 20 and an `extension.night` equal to
    the local date minus 4 hours (the date before or after the wait); then the service process is ended and restarted
    (`Restart-ServiceProcess`), a state read with a `-StateFile` probe (a new function `Get-ProbeState` that group 18 also calls) has
    `extensionMinutes` 20 and `tonightLimitMinutes` 40, and `{"type":"extend","requestId":"x3","minutes":30}` is answered `ok` false
    with `Extension used tonight: 20 min.`; `settings.json`'s `LastWriteTimeUtc` is the value recorded before `x0`. The script prints
    `EXTEND OK`, ends with `Invoke-TestUninstall`, and exits non-zero at any failed check.
23. **Given** the CI Windows runner, **When** `Test-Install.ps1` runs group 22 (`Reset-SteamGate @{ weeknightMinutes = 60;
    weekendNightMinutes = 60 }`), **Then** `{"type":"extend","requestId":"w1","minutes":10}` is answered `ok` false with `Extend is
    available in the last 30 minutes before tonight's limit.` and `tally.json` has no extension (`extension` null or absent) after 10
    seconds; the script prints `EXTEND WINDOW OK`. **When** it then runs group 23 (`Reset-SteamGate @{ weeknightMinutes = 20;
    weekendNightMinutes = 20; weeklyCapMinutes = 50 }`), **Then** an `extend` of 20 is answered `ok` false with `Not enough time left this week.`, and an `extend` of 10 is answered `ok` true with `Applied now. Tonight ends 10 min later (limit
    30 min).`; the script prints `EXTEND WEEK OK`. Each group ends with `Invoke-TestUninstall`.
23b. **Given** this issue, **When** a reader greps it, **Then** the text `Not enough time left this week.` appears in both
    criterion 7(i) and criterion 23, and the settings in both are weeknight 20 and weekly cap 50.
24. **Given** the CI Windows runner, **When** `Test-Install.ps1` runs group 24 (the 15-minute guard before 04:00; `Reset-SteamGate @{
    weeknightMinutes = 2; weekendNightMinutes = 2 }`; `New-FakeSteamRoot`; `$mark = Wait-SteamReady` on the service it restarted, which
    returns the number of log lines at that moment; an `extend` of 10, which must be answered `ok` true with `Applied now. Tonight ends
    10 min later (limit 12 min).`; then the fake game started as group 10 starts it), **Then** within 400 seconds `tally.json` reaches
    `nightSeconds` of at least 150; at that read the fake game is still running (`Get-Process -Name FakeGame` finds it; without the extension a `polite close FakeGame` line would have been logged at 120 seconds), `service.log` has no `polite close FakeGame` line after `$mark`, and `weekSeconds - nightSeconds` equals
    `nightSeconds - 120` within 0.5. The script prints `EXTEND DOUBLE OK`, stops the fake game and removes the fake Steam root in its
    `finally`, and ends with `Invoke-TestUninstall`.
25. **Given** `CLAUDE.md`, **When** a reader greps it, **Then** its `### Product outcomes` contains `waits the full cooldown, with one
    exception: the status screen's Extend button (next bullet)`, still contains `Any change that tightens a rule applies right away.`
    and `The cooldown has a hard maximum of 7 days.`, does not contain `uninstall) waits the full cooldown. Any change that tightens`,
    contains a bullet that begins `- Extend (approved 2026-10-26):` and contains `once per night`, `10, 20, or 30 minutes`, `at once,
    with no cooldown`, `count double toward the weekly cap`, and `unused extension time costs nothing`, and its status screen bullet
    reads `Status screen: see where you stand, request changes, edit the not-counted list, extend tonight.` and no longer contains
    `edit the not-counted list. It is visual`.
26. **Given** `DESIGN.md`, **When** a reader greps it, **Then** `DESIGN.md` contains no `Channel<SettingsCommand>`, its `Servers hand
    work to the worker` bullet contains `Channel<WorkerCommand>` and `Issue 0006 created the settings channel`; and in `## SteamGate service design (orchestrator decisions, not part
    of the approved spec)`: the `One file store` bullet contains `a negative or non-finite count, or an extension that is not 10, 20, or
    30 minutes` and no longer contains `for the tally: a negative or non-finite count)`; the `How closing works` bullet contains `(a new
    night, a raised limit, or an extension)` and no longer contains `(a new night or a raised limit)`, contains `` `request`, `cancel`,
    and `extend` are allowed after the hello`` and no longer contains `` `request` and `cancel` are allowed after the hello``; the `How
    the lock works` bullet contains ``the one early loosening is the `extend` message``, contains `` `request`, `cancel`, and `extend`
    messages``, and no longer contains `so no request loosens a rule early;`; the `How the status screen works` bullet contains `Extend
    tonight`, `extensionMinutes`, `ExtendMessage`, `ExtendRules`, `once per night`, `` the Extend texts are `ExtendText` ``, and `section
    headings other than Extend tonight`, and its list of Core code under test contains `ExtendRules`; the `How time is counted` bullet
    contains `tonight's limit is the original limit plus the extension`, `time past tonight's original limit counts double toward the
    week`, `extension` and `tally.json` in the same sentence, `cleared at 4 AM`, `a limit change after an extension moves the line where
    doubling starts`, `an extension whose save failed exists only in memory until the next tally save, and a kill before then loses it`,
    and `extend commands are answered before the tick is counted` (in a parenthesis), and no longer contains `The remaining seconds are the smaller of
    tonight's limit minus tonight's use and the weekly cap minus the week's use, never below 0,`; the `Additive settings only` bullet
    contains `the tally's optional extension member`. `## Repo structure`'s `docs/` line contains `preview-2026-10-26-extend.html`
    exactly once. `## CI` step 6 contains `an extension over the pipe` and `counted double past the old limit`. `## Evals` contains
    `(extension button)` and `preview-2026-10-26-extend.html`. `## Testing` contains `group 21`, `group 24`, `Extend`, and `section headings
    other than Extend tonight`.
27. **Given** `README.md`, `WHAT-IT-CHECKS.md`, and `PLAN.md`, **When** a reader greps them, **Then** README.md's opening paragraph contains
    `extend tonight by 10, 20, or 30 minutes`; WHAT-IT-CHECKS.md contains the bullet `**Extend works.**` with the words `once per night`,
    `double`, and `weekly cap`, its lock bullet ends with the sentence `The Extend button is the one exception: it applies at once, and unit
    tests prove its rules.`, its not-machine-checked paragraph contains `three previews` and `the Extend section` and does not contain `two
    previews`, and the status-screen bullet still contains `No build check proves the real status screen reads`; PLAN.md has a line that
    begins `8. **0009 Extend button.**` and contains `preview-2026-10-26-extend.html` and `(extension button)`.
28. **Given** the source comments this change makes false, **When** a reader greps them, **Then**:
    `src/SteamGate.Core/Rules/SettingsCommand.cs` contains `Exactly one of Request or CancelId is set` and contains no `extend` or `Extend`;
    `src/SteamGate.Core/Rules/WorkerCommand.cs` contains `A command the worker takes from the pipe: a settings request or cancel, or an
    extend` and no `Exactly one of`; `src/SteamGate.Core/Pipe/ConnectionRules.cs` contains `request, cancel, and extend` and
    no longer contains `may send only request and cancel`; `src/SteamGate.Core/Pipe/PipeMessage.cs` contains `the answer to the request,
    cancel, or extend with this requestId` and no longer contains `the answer to the request or cancel with this requestId`;
    `src/SteamGate.Core/GateSnapshot.cs` contains `including tonight's extension` and `counting tonight's extension and the double cost past
    the original limit`; `src/SteamGate.Core/Accounting/Budget.cs` contains `(the limit plus tonight's extension)` and no longer contains `The
    smaller of what is left tonight and what is left this week, never below 0 and never rounded.`; `src/SteamGate.Core/Accounting/Accountant.cs`
    contains `ExtensionCost.WeekCharge` in the `Tick` summary and no longer contains `is added once to the night and the week`;
    `src/SteamGate.Core/Accounting/TallyStore.cs` contains `or an extension that is not 10, 20, or 30 minutes` in the `Save` summary and `An
    extension is one of the allowed lengths` above `IsValid`; `src/SteamGate.Core/Accounting/NightClock.cs` contains `another night's extension
    is dropped` in the `RollOver` summary; `src/SteamGate.Core/Accounting/Tally.cs` contains `Extension is the extension taken for the named
    night, or null`; `src/SteamGate.Service/PipeServer.cs` contains `request, cancel, or extend` above `ReadLoopAsync` and no longer contains `A
    request or` there, and its `Submit` comment contains `request, cancel, or extend`; `src/SteamGate.Service/GateWorker.cs` has a comment above
    `RunLock` containing `extend commands go to RunExtend`; `src/SteamGate.Core/Status/StatusView.cs` contains `the chosen Extend length` and
    `whether an Extend request is waiting` in the `EditorState` summary and `the chosen Extend length and the Extend wait are kept as they are` in the `Merge` summary, and `Merge` takes `night`;
    `src/SteamGate.Core/Status/StatusText.cs` contains `plus tonight's extension` in the `TonightLimit` summary and `Section headings other
    than Extend tonight (ExtendText)` in the class summary; `src/SteamGate.Core/Rules/ChangeLog.cs` contains `The service.log lines for the lock
    and the Extend button` and no longer contains `The service.log lines for the lock. Only`; `src/SteamGate.Core/Pipe/ChangeMessages.cs` contains
    `and builds the reply to an extend` in the class summary.
29. **Given** the whole change, **When** `dotnet build -c Release`, `dotnet test -c Release`, and `dotnet stryker` run on the CI Windows runner,
    **Then** all three pass (build with no analyzer warning, the Stryker mutation score at or above 80), and every existing test that constructs
    `Tally`, `StateMessage`, `EditorState`, or `SettingsCommand`, or calls `Accountant.Tick` or `StatusText.Minutes` still passes (updated only for
    the new `Tick` parameter, the moved `DurationText.Minutes`, the new `WorkerCommand` base, the `ParsesTheNullForms` line in criterion 11, and the new `night` parameter of `StatusView.Merge` in `StatusViewTests.StateKeepsEdits` and every other `Merge` caller).

## Implementation plan
1. **Move the formatter.** Create `src/SteamGate.Core/DurationText.cs` with `Minutes`, delete `StatusText.Minutes`, change every caller
   (`StatusText.cs`, `StatusView.cs`, `PendingText.cs`, tests); move the test to `tests/SteamGate.Tests/DurationTextTests.cs` (criterion 8b).
2. **Tally and accounting (Core).** Create `NightExtension.cs` (with `Lengths`) and `ExtensionCost.cs`; add `Budget.OriginalLimitSeconds`; add the optional `Extension` to `Tally.cs`;
   extend `TallyStore.IsValid`; make `NightClock.RollOver` keep the extension only for the named night; give `Accountant.Tick` the
   `GateSettings` parameter and use `ExtensionCost.WeekCharge`; add `Budget.TonightLimitMinutes` and the new `RemainingSeconds` using
   `ExtensionCost.PlayableWeekSeconds`. Tests: criteria 1 to 5b in `tests/SteamGate.Tests/Accounting/` (including `ExtensionCostTests.cs`).
3. **The rules and texts (Core).** Create `src/SteamGate.Core/Extend/ExtendRules.cs` (`WindowSeconds`, `View`, `Apply`) with
   `ExtendKind`, `ExtendView`, `ExtendOutcome`, and `ExtendText.cs`. Tests: `tests/SteamGate.Tests/Extend/ExtendRulesTests.cs` and `ExtendTextTests.cs`
   (criteria 6 to 8). The pipe messages: `ExtendMessage` and `ExtensionMinutes` in `PipeMessage.cs`; the allowance in `ConnectionRules.cs`;
   `WorkerCommand`, `ExtendCommand`, and `SettingsCommand : WorkerCommand`; `ChangeMessages.ExtendReply`; `ChangeLog.Extend`. Tests:
   `tests/SteamGate.Tests/Pipe/PipeMessageTests.cs`, `ConnectionRulesTests.cs`, the new `ChangeMessagesTests.cs`,
   `tests/SteamGate.Tests/Status/StateMessageTests.cs` (criteria 9 to 11, 17), the new `tests/SteamGate.Tests/Rules/WorkerCommandTests.cs` (17b), and the new `tests/SteamGate.Tests/Rules/SettingsCommandTests.cs` (17c).
4. **The status logic (Core).** `StatusState.From` fills `ExtensionMinutes`; `StatusText` gets `TonightUsed`, `WeekUsed`, the `TonightLimit` fallback
   plus extension, and the new `ReplyIsNow`; `StatusRequests.Extend`; `StatusView` gets `ReplySection.Extend`, `EditorState.ExtendChoice`,
   `ExtendWaiting`, `ExtendApplied`, and `ExtendReplyNight`, `ExtendSection`, `ExtendPick`, `ExtendClickAllowed`, `ExtendSent(editor, minutes)`, `ExtendAnswered`, `ConnectionLost`, and the `Merge` additions (`StatusText.OriginalLimitSeconds` too). Tests: criteria 12 to 16 in
   `tests/SteamGate.Tests/Status/`.
5. **The service.** In `Program.cs`, `PipeServer.cs`, and `GateWorker.cs` change the channel to `Channel<WorkerCommand>`; in `GateWorker.cs` drain once
   into two lists, make `RunLock` take the settings list and dispatch explicitly, add `RunExtend` between `RunLock(` and `CountTime(` (apply, save the
   tally at once with `SaveTally` returning whether it succeeded, log, reply after the save), pass `_settings` to
   `Accountant.Tick`, and use `Budget.TonightLimitMinutes` for the snapshot; in `PipeServer.cs` `Submit`, queue an `ExtendMessage` as an
   `ExtendCommand` with the busy answer and log line, and update the comments (criterion 28). Planner test: criterion 18 (no planner edit).
6. **The window.** In `src/SteamGate.Status/StatusPage.cs` add the `EXTEND TONIGHT` section under the meters from `StatusView.ExtendSection`, the used
   lines from `StatusText.TonightUsed` and `WeekUsed`, the reply line for `ReplySection.Extend`, `ExtendSent` on send, `ExtendAnswered` on the reply,
   `ConnectionLost()`, and the disabled state when not connected; in `StatusForm.cs` call `ConnectionLost()` from `OnConnected(false)`. The Extend reply line is not registered in `_replies`; its shown flag, position, and text come only from `StatusView.ExtendSection` (its color from `ReplyIsNow`). The window holds no Extend text of its own (criterion 20) and no new style value.
7. **The install test.** In `install/Test-Install.ps1` add `Get-ProbeState` (and make group 18 call it) and groups 21 to 24 after group 20 (criteria 22
   to 24), each ending with `Invoke-TestUninstall`.
8. **Documents.** Edit `CLAUDE.md`, `DESIGN.md`, `README.md`, `WHAT-IT-CHECKS.md`, and `PLAN.md` as criteria 25 to 27 say, and the source comments as
   criterion 28 says. CLAUDE.md `### Product outcomes` gets the exception named in the loosening sentence (`... uninstall) waits the full cooldown, with
   one exception: the status screen's Extend button (next bullet). Any change that tightens a rule applies right away.`), a bullet directly after the loosening
   bullet reading `- Extend (approved 2026-10-26): in the last 30 minutes before tonight's limit, once per night, the status screen can add 10, 20, or 30
   minutes to tonight's limit at once, with no cooldown. Only minutes played past tonight's original limit count double toward the weekly cap; unused
   extension time costs nothing. The weekly cap stays hard: a length that would not fit is greyed out with the reason.`, and the status screen bullet
   ending `edit the not-counted list, extend tonight. It is visual, so Trevor approves its look on a preview before it is built.` DESIGN.md service-design
   bullets: `Servers hand work to the worker` (`Issue 0006 created the settings channel (capacity 32), now Channel<WorkerCommand>`), `One file store`, `How closing works`, `How the lock works` (replace `Channel<SettingsCommand>` with `Channel<WorkerCommand>`; add that any signed-in
   account may also send `extend`, which can only spend the one extension Trevor may take), `How the status screen works` (the Extend section, `extensionMinutes`, the two layouts, the green extend reply, that the Extend texts are
   `ExtendText` in Core, and `section headings other than Extend tonight` as fixed text), `How time is counted` (limit plus extension, with the parenthesis `extend commands are answered before the tick is counted`, double past the old
   limit, the new remaining seconds, `tally.json` and its `extension` member, cleared at 4 AM, the moving doubling line, the failed-save caveat) and
   `Additive settings only`, plus `## Repo structure`, `## CI`, `## Evals`, and `## Testing`; README.md opening paragraph; WHAT-IT-CHECKS.md (new bullet,
   lock bullet sentence, `three previews`); PLAN.md line 8.
9. Run the criteria 0 to 29 checks. The `BUILDLOG.md` line is appended in a separate commit after this one.

## Out of scope
- Any length other than 10, 20, and 30, a typed number of minutes, or more than one extension a night.
- Extending after the original limit is reached, including during the polite-close wait.
- A change to the warning overlay, the helper, the polite close, or the planner (the planner's existing re-arm is relied on and tested, not changed).
- Any new style value, icon, or sound; any change to the other status screen sections or to the meters beyond the two used-line suffixes from card 2.
- Any change to the cooldown or to existing approved spec text (the amendment is appended, not edited); the lock changes only by the Extend exception.
- Charging time already played before an extension at double cost.
