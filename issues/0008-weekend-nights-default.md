# Issue #0008: Weekend nights default to Friday, Saturday, and Sunday

**Type:** ready. **Category:** correction of a default; amends the approved spec at Trevor's word.
**Review bar:** system-level change (edits DESIGN.md and CLAUDE.md); two independent reviewers, both PASS; architecture gate at issue time; reviewer-documentation on the text edits; reviewer-pr then design-philosophy gate at PR time.
**GitHub:** #16.
**Depends on:** 0002, 0004.
**Blocks:** 0005 (its spec-amendment step comes after this one), 0006.
**Touches:** `src/SteamGate.Core/GateSettings.cs`, `tests/SteamGate.Tests/GateSettingsTests.cs`,
`tests/SteamGate.Tests/SettingsStoreTests.cs`, `tests/SteamGate.Tests/Accounting/BudgetTests.cs`,
`CLAUDE.md`, `DESIGN.md`, `docs/approved-spec-2026-10-20.md` (amendment appended), `issues/0001-north-star.md`,
`issues/0002-service-skeleton.md`, `issues/0004-time-accounting.md`, `BUILDLOG.md`,
`issues/0008-weekend-nights-default.md`. (Issues 0005 and 0006 are not committed yet; the edits this
change needs in them are already made in the working files and ride in with their own commits.)

## User story
As Trevor, I need the weekend nights to be Friday, Saturday, and Sunday, so that the 3 h limit
applies to all three nights of my weekend and not only two.

## Approved spec this issue carries
Trevor, in chat with the build session (session cd2c77e4) on 2026-10-21: "weekend nights are fri sat and sunday btw". This replaces
"weekend nights = Friday + Saturday" in the approved spec. Nothing else in the spec changes.

## Design decisions (orchestrator)
- The default list in `GateSettings.Defaults` becomes Friday, Saturday, Sunday (that order). The
  settings file is written from these defaults, so a new install writes
  `"weekendNights":["Friday","Saturday","Sunday"]`.
- No install exists outside CI yet (the first real install on this PC is still to come), so no
  existing settings file needs migrating; the hand-off note to Trevor says so.
- A night is named by its start date (0004). The Sunday night ends at 4 AM Monday and counts
  toward the week that ends then. Nothing in the counting changes.
- The approved text is never edited. `docs/approved-spec-2026-10-20.md` keeps its line 25 as Trevor
  first approved it, and `DESIGN.md`'s copied spec keeps the same line, so 0001 criterion 15 (every
  spec line appears character for character in DESIGN.md) still holds. This issue appends an
  amendment below the original: `## Amendment 2026-10-21 (weekend nights)` at the end of the pinned
  file, and the same text under `### Amendment 2026-10-21 (weekend nights)` (three hashes, so it
  stays inside `## SteamGate product spec`) after DESIGN.md's line "The Blender line in the Issue lifecycle ... stays." and before
  `## SteamGate service design`. The text quotes
  Trevor's words and says: the weekend nights are Friday, Saturday, and Sunday; this replaces the
  weekend-nights default in the SPEC list above and wins over it.
- This is the same amendment mechanism issue 0005 already uses for Trevor's approved warning
  changes. The pinned file's line 3 becomes exactly: `**As the orchestrator or a reviewer checking
  SteamGate work, I need Trevor's approved spec text unedited, with dated amendments appended below
  the original, so I can tell what he approved from what was added later.**`, and the three "pinned unedited" phrases (`CLAUDE.md` `## Authoritative sources`,
  `DESIGN.md` `## SteamGate product spec` and `## Where the documentation lives`) change to say
  "pinned unedited, with dated amendments appended below the original".
- Issue 0005 comes after this issue. The edits it needs are already made in its working file (not
  committed): it depends on 0008, its amendment heading in the pinned file is
  `## Amendment 2026-10-21 (warnings)` (and `###` in DESIGN.md), and it no longer rewrites the
  "pinned unedited" phrases. It is sent back through issue review before it is built. Issue 0006's
  Depends line already lists 0008 and its line 238 already reads "default settings (except weekend
  nights, where a criterion states them; ...)". Both ride in with their own commits, not this one.

- `CLAUDE.md`'s Defaults line and 0001's spec summary line 23 state the new default. Quotes of the
  2026-10-20 approved text in issues 0001, 0002, and 0004 stay as they are: they record what was approved. After 0002's quote (lines 36 to 37) and after
  the Defaults sentence in 0004's "Approved spec this issue carries" section (lines 24 to 25), a
  line is added: "Amended 2026-10-21 (issue 0008): weekend nights are Friday, Saturday, and
  Sunday." Statements of a test's expected value are corrected: 0001 criterion 3's string list,
  0002's Design decisions `settings.json` line and criteria that give the default array, and 0004 criterion 2's Sunday value.
  0001 line 16 ("the same as the GitHub #6 body") becomes "the same as the GitHub #6 body, plus
  amendments appended below", and line 92 ("the pinned copy of the GitHub #6 body") becomes "the
  pinned copy of the GitHub #6 body, plus amendments appended below". The GitHub #6 body is not edited. Issue 0006 keeps its explicit
  "Friday and Saturday" starting sets.

## Acceptance criteria
1. **Given** `GateSettings.Defaults`, **When** its `WeekendNights` is read, **Then** it is
   `[Friday, Saturday, Sunday]`. (Test `GateSettingsTests.Defaults_AreBuilt_WithNonNullLists`.)
2. **Given** an empty data folder, **When** `SettingsStore.Load` runs, **Then** the file written
   contains `"weekendNights":["Friday","Saturday","Sunday"]`. (Test
   `SettingsStoreTests.Load_EmptyFolder_WritesDefaults`.)
3. **Given** the default settings, **When** `Budget.NightLimitMinutes` runs for Thursday
   2026-10-22, Friday 10-23, Saturday 10-24, and Sunday 10-25, **Then** it returns 90, 180, 180,
   180. (Test `BudgetTests.NightLimitByWeekday`.)
4. **Given** `CLAUDE.md`, **When** a reader greps it, **Then** it contains `weekend nights are
   Friday, Saturday, and Sunday` and does not contain `Friday and Saturday`.
5. **Given** `docs/approved-spec-2026-10-20.md` and `DESIGN.md`, **When** a reader greps them,
   **Then** each still contains the line `- Defaults: weeknight 90 min; weekend night 3 h; weekend
   nights = Friday + Saturday; weekly cap 10 h; cooldown 48 h.` exactly once; the pinned file
   contains the heading `## Amendment 2026-10-21 (weekend nights)` exactly once, and DESIGN.md
   contains `### Amendment 2026-10-21 (weekend nights)` exactly once, inside `## SteamGate product
   spec`, after that line; and every other line of the pinned file from its first line to its line 32 is unchanged except line 3, compared with the pinned file at `HEAD` before the commit (`HEAD~1` after); and the lines under the pinned file's amendment heading equal, line for line, the lines under
   DESIGN.md's amendment heading; and the text under each heading contains `Friday, Saturday, and Sunday`,
   `wins over`, and `replaces the weekend-nights default in the SPEC list above`.
6. **Given** `CLAUDE.md`, `DESIGN.md`, and `docs/approved-spec-2026-10-20.md`, **When** a reader
   greps `CLAUDE.md` and `DESIGN.md` for `pinned unedited` and `pinned, unedited`, **Then** the
   command prints exactly 3 lines and each contains `with dated amendments appended below the
   original`; **and** line 3 of the pinned file contains `unedited, with dated amendments appended
   below the original`.
7. **Given** `issues/0002-service-skeleton.md`, **When** a reader runs
   `grep -nE 'Saturday\]|Saturday"\]' issues/0002-service-skeleton.md`, **Then** it prints
   nothing, and the file's Design decisions `settings.json` line and its criteria that give the default array each contain `Sunday`; **and given**
   `issues/0001-north-star.md`, **Then** criterion 3's string list has `Friday, Saturday, and
   Sunday` and not `Friday and Saturday`, the file contains no `Friday and Saturday`, the bullet under `## Product spec summary` states `Friday, Saturday, and Sunday` on one line, the heading
   `## Product spec summary` line contains `the same as the GitHub #6 body, plus amendments appended
   below`, and criterion 15 contains `plus amendments appended below`; **and given**
   `issues/0004-time-accounting.md` criterion 2, **Then** the Sunday 2026-10-25 value is 180; **and
   given** 0002 and 0004, **Then** each contains `Amended 2026-10-21 (issue 0008): weekend nights are
   Friday, Saturday, and Sunday.` on one line.

## Implementation plan
1. Change the default in `GateSettings.cs`; update `GateSettingsTests`, `SettingsStoreTests`
   (the `DefaultWeekendNights` array and the compact-JSON assertion), and `BudgetTests` (Sunday
   180) to criteria 1 to 3.
2. Edit the text as in Design decisions: `CLAUDE.md`, the amendment in `DESIGN.md` and
   `docs/approved-spec-2026-10-20.md`, the "pinned unedited" phrases, and issues 0001, 0002, 0004.
3. Build, test, and run Stryker; append the previous issue's (0004) BUILDLOG line, as each commit carries the one before it.
4. When the commit lands, close GitHub #16 and comment `Depends on 0008` on #10 and #11.

## Out of scope
Any other spec or text change; migration of existing settings files (none exist); the stale
warning-time and sound text in CLAUDE.md and DESIGN.md (issue 0005 corrects it). The GitHub issue
#6 body is not edited.
