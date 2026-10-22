# Issue #0001: Write the SteamGate North Star into CLAUDE.md, DESIGN.md, and PLAN.md

**Type:** ready. **Category:** system-level change (DESIGN.md "System-level change definition"). Documentation only.
**Review bar:** system-level change (touches CLAUDE.md, DESIGN.md); two independent reviewers, both PASS; plus the architecture gate at issue time.
**GitHub:** #6 (its body holds the approved spec copied word for word from the brief that started this build: the six issue lines and the SPEC bullets).
**Depends on:** none.
**Blocks:** 0002 to 0007 (sequenced in PLAN.md by this issue; each issue file is drafted and reviewed under its own number).
**Touches:** `CLAUDE.md`, `DESIGN.md`, `PLAN.md`, `standards/issue-standards.md`, `standards/documentation-standards.md`, `issues/0001-example-feature.md` (deleted; it is the template's placeholder and this file takes its number), `issues/0001-north-star.md` (added), `docs/approved-spec-2026-10-20.md` (added: the pinned word-for-word copy of the #6 body from its "Approval source:" line to the end).

## User story
As the orchestrator agent that loads this repo before every SteamGate task, I need the North Star,
the product outcomes, and the known gaps written into CLAUDE.md, DESIGN.md, and PLAN.md in place of
the template's placeholders, so that every decision I make on issues 0002 to 0007 is checked against
the spec in `docs/approved-spec-2026-10-20.md`, read with its approval note, instead of a generic template.

## Product spec summary (paraphrase; the approved text is `docs/approved-spec-2026-10-20.md`, the same as the GitHub #6 body, plus amendments appended below, and governs)
- A Windows service in C# / .NET. No window, tray icon, or Alt-Tab entry. Windows restarts it if
  killed. Settings and tally live in a protected system folder (ProgramData, admin-only writes).
- A small invisible helper runs in the user's own login, shows the warnings, and does the polite
  close. The service does the counting and the force-close. Each warning is a silent overlay on top of
  the game that takes no focus or input and fades out. Amended 2026-10-21 (issue 0005).
- Counts wall-clock minutes while at least one Steam game is open; two games at once do not
  double-count.
- Defaults: weeknight 90 min; weekend night 3 h; weekend nights are
  Friday, Saturday, and Sunday; weekly cap 10 h; cooldown 48 h. A night ends at 4 AM. The weekly cap
  resets Monday 4 AM. The tally is saved to disk every minute.
- Warnings at 30, 20, 10, 5, and 1 minutes left, each a silent overlay; at the limit a polite close, then a force
  close; any game started after the limit is closed. Amended 2026-10-21 (issue 0005).
- Any change that loosens a rule (raise a limit, add a weekend night, shorten the cooldown, add a
  program to the not-counted list, disable, uninstall) waits the full cooldown. Any change that
  tightens a rule applies right away. The cooldown has a hard maximum of 7 days.
- Not-counted list, pre-filled with Blender, OBS Studio, Wallpaper Engine, and Steamworks Shared,
  editable from the status screen; adding waits the cooldown, removing applies now.
- Status screen: see where you stand, request changes, edit the not-counted list. It is visual, so
  Trevor approves its look on a preview before it is built.
- Known gaps, documented and not solved: Trevor is the admin, so a deliberate admin teardown can
  remove it; changing the system clock could fool the counter.
- Guarantees: it never hides from antivirus or Windows and never blocks their removal. Task Manager
  CAN kill the service; Windows restarts it and the tally survives.

## Acceptance criteria
In this issue, "the North Star section" of `CLAUDE.md` runs from `## North Star (the goal this serves)`
to the next `## ` heading, including its `###` subsections. Any other named section runs from its
heading to the next heading of the same or higher level.

1. **Given** `CLAUDE.md`, **When** a reader greps it, **Then** the North Star section contains the
   literal string `SteamGate`, the string `<!-- CUSTOMIZE` appears nowhere inside the North Star
   section, and no line starting `> **CUSTOMIZE:**` exists in the file. (The `## First run`
   section's mention of the markers stays.)
2. **Given** `CLAUDE.md`, **When** a reader greps it, **Then** it still contains the literal strings
   `**Trust without checking**` and `**Software that holds up**`.
3. **Given** `CLAUDE.md`, **When** a reader greps it, **Then** it contains a section headed
   `### Product outcomes` that contains each of these literal strings:
   `90 min`, `weekend night 3 h`, `Friday, Saturday, and Sunday`, `10 h`, `night ends at 4 AM`,
   `Monday 4 AM`, `48 h`, `7 days`, `10, 5, and 1 minutes`, `Blender`, `OBS Studio`,
   `Wallpaper Engine`, `Steamworks Shared`, `correct this section to match DESIGN.md`.
4. **Given** `CLAUDE.md`, **When** a reader greps it, **Then** it contains a section headed
   `### Known gaps (documented, not solved)` that contains the literal strings `admin teardown`,
   `system clock`, `Task Manager`, `Windows restarts it`, `the tally survives`, and
   `never hides from antivirus`.
5. **Given** `CLAUDE.md`, **When** a reader greps the `### Known gaps (documented, not solved)`
   section, **Then** it does not contain the strings `cannot be killed`, `can't be killed`,
   `unkillable`, or `cannot be stopped`.
6. **Given** `DESIGN.md`, **When** a reader greps it, **Then** the strings
   `<!-- CUSTOMIZE: state your domain`, `<!-- CUSTOMIZE: the example add-ons`, and
   `for your domain. The principle` are each absent from the file, the section `## Prime directive`
   contains the literal string `SteamGate`, and the section headed
   `## Governance: in-license constraint` contains the literal string `no Steam Web API`.
7. **Given** `PLAN.md`, **When** a reader lists the numbered lines under `## Segment order`, **Then**
   there are seven, their first four-digit tokens are `0001`, `0002`, `0003`, `0004`, `0005`, `0006`,
   `0007` in that order, and the string `<!-- CUSTOMIZE` is absent from the file.
8. **Given** the repo after the change, **When** a reader lists `issues/`, **Then**
   `issues/0001-example-feature.md` does not exist and `issues/0001-north-star.md` exists.
9. **Given** the five files in Touches that this change edits, **When** a reader runs
   `git diff HEAD` (before commit) or `git diff HEAD~1 HEAD` (after commit) with the path list `-- CLAUDE.md DESIGN.md PLAN.md standards/issue-standards.md standards/documentation-standards.md`,
   **Then** no added line contains the character U+2014 (em dash) or the HTML entity spelled
   ampersand, `mdash`, semicolon.
10. **Given** `CLAUDE.md`, **When** a reader greps the `### Product outcomes` section, **Then** it
   contains each of these literal strings: `do not double-count`, `silent overlay`, `polite close`,
   `force close`, `started after the limit`, `tightens a rule applies right away`, `loosens a rule`,
   `disable`, `uninstall`, `removing applies now`, `ProgramData`, `Alt-Tab`, `invisible helper`,
   `status screen`, `Windows restarts it`, `every minute`. Amended 2026-10-21 (issue 0005).
11. **Given** `DESIGN.md`, **When** a reader greps it, **Then** it contains a section headed
   `## SteamGate product spec` that contains the literal strings `90 min`, `48 h`, `7 days`, `Approval source:`,
   `admin teardown`, `system clock`, `issue 0002 rewrites them`, `services can't draw on the screen`,
   `mutes notifications during full-screen games`, `set to a year by mistake`, and `Never claim it can't`.
12. **Given** the working tree, **When** a reader runs
   `git grep -n --untracked 0001-example-feature -- CLAUDE.md DESIGN.md PLAN.md standards/ docs/approved-spec-2026-10-20.md`, **Then** there are no matches.
13. **Given** `DESIGN.md`, **When** a reader greps the `## Deferred items` section, **Then** it
   contains `0002`.
14. **Given** `standards/issue-standards.md` and `standards/documentation-standards.md`, **When** a
   reader greps them, **Then** neither contains `Lesson from issue #0001`.
15. **Given** `DESIGN.md` and `docs/approved-spec-2026-10-20.md` (the pinned copy of the GitHub #6 body, plus amendments appended below), **When** a reader compares each numbered issue line
   and each bullet line under `SPEC` in that file against the `## SteamGate product spec` section, **Then**
   every such line, including its leading list marker, appears as one whole line, character for
   character, in that section.

## Implementation plan
1. In `CLAUDE.md`, delete the `> **CUSTOMIZE:**` line (`CLAUDE.md:7`) and both
   `<!-- CUSTOMIZE ... -->` comments (`CLAUDE.md:13`, `CLAUDE.md:19`) entirely. Rewrite the North Star
   sentence (`CLAUDE.md:14`) so it names Trevor (a functional tech who does not read code) directing
   the build of SteamGate and
   trusting the result: a Windows service that holds his Steam gaming to the limits in
   `### Product outcomes`. Keep the two numbered goals unchanged, and keep the closing clause "Two goals carry it, and every decision serves them:" on that line. The removed `CLAUDE.md:19` marker's
   out-of-scope prompt is covered by `### Known gaps (documented, not solved)`. In `## First run`
   step 1, append: `(Done for SteamGate in issue 0001; the remaining DESIGN.md Setup items marker is the license choice, not the North Star.)` On that same line, replace the em dash (U+2014) before "if the" with a colon, because the edited line counts as added for criterion 9.
2. Under the two goals in `CLAUDE.md`, add `### Product outcomes`, in this order: (a) the first line
   `Short form of DESIGN.md "## SteamGate product spec".`; (b) one bullet per bullet of
   `## Product spec summary` in this issue, keeping its wording, each bullet on one unwrapped line, except the Known gaps and Guarantees
   bullets; (c) the last line
   `On conflict, correct this section to match DESIGN.md (DESIGN.md "Where the documentation lives").`
   Then add `### Known gaps (documented, not solved)` with the two gaps (admin teardown, system clock)
   and the two guarantees (write it as `SteamGate never hides from antivirus or Windows` and never blocks their removal; Task
   Manager can kill the service, Windows restarts it, and the tally survives), stated as facts.
3. In `DESIGN.md`, replace the `<!-- CUSTOMIZE: state your domain ... -->` comment (`DESIGN.md:5`)
   with one paragraph naming SteamGate, its owner Trevor, and what it builds. Delete the two-line
   comment at `DESIGN.md:327-328` entirely; keep the Blender paragraph at `DESIGN.md:329` (0002
   retires the example) and add after it one paragraph stating that, under the in-license constraint above, SteamGate uses no Steam Web API and no
   key-gated or paid service; it detects running games from local Steam files and the local list of running programs (see `## SteamGate product spec`); issue 0003 builds it.
4. In `DESIGN.md`, add `## SteamGate product spec` after the end of the `## Prime directive` section,
   immediately before `## What the system is` (`DESIGN.md:8`), holding the
   approved spec copied word for word from `docs/approved-spec-2026-10-20.md` (the GitHub #6 body): its six numbered issue lines and every
   bullet under `SPEC`, known gaps and guarantees included, plus its `Approval source:` line; the bullets in this issue are a summary
   only. Copy each #6 numbered issue line and each `SPEC` bullet as one unwrapped line, keeping its
   leading `N. ` or `- ` marker. Before the copied issue lines, add the line
   `Issue lines 1 to 6 below are repo issues 0002 to 0007; 0002 creates the protected settings folder from line 5 because the service needs it to start, and 0006 builds the cooldown rules (a sequencing choice made after approval, not part of the approved spec).` State that the `CLAUDE.md` sections
   `### Product outcomes` and `### Known gaps (documented, not solved)` are its short form. End the
   section with the line: `The Test strength, Governance (test line), CI, Evals, and Testing sections
   below still describe the template's Python and Blender example; issue 0002 rewrites them for the
   C# / .NET build.` Write that line unwrapped. Add one bullet to
   `## Deferred items`: `C# / .NET build, test, eval, and CI rules on a Windows runner (issue 0002);
   until then the Python-only sections describe the template example only.` Remove the Deferred items
   bullet "Blender add-on development standards ..." (`DESIGN.md:298`); SteamGate builds no Blender
   add-ons.
5. In `PLAN.md`, replace the `<!-- CUSTOMIZE ... -->` comment, the placeholder Segment 1 (which
   links to the deleted example issue), and the "(Add further segments here ...)" line at
   `PLAN.md:19` with seven numbered segments in order: 0001 North Star; 0002
   service skeleton, the protected settings folder (ProgramData, admin-only writes), install and
   uninstall, restart after being killed, retire the Blender example,
   rewrite DESIGN.md's Python-only test, eval, and CI rules and the README front door for a C# / .NET
   build on a Windows runner; 0003 Steam game detection; 0004 time accounting; 0005 enforcement; 0006
   the lock (cooldown rules, including making uninstall and disable wait the cooldown); 0007 status screen (its look approved by Trevor on a preview before it is
   built).
6. Delete `issues/0001-example-feature.md`.
7. In `standards/issue-standards.md:19` and `standards/documentation-standards.md:17`, change
   "Lesson from issue #0001" to "Lesson from the orchestrator template's first issue", since issue
   0001 in this repo is now the North Star. On `standards/issue-standards.md:19`, also replace the
   em dash (U+2014) clause before "that is unfalsifiable" with ", which is unfalsifiable", because the
   edited line counts as added for criterion 9.
8. Grep `CLAUDE.md`, `DESIGN.md`, `PLAN.md`, `standards/issue-standards.md`, and
   `standards/documentation-standards.md` for U+2014, the em dash HTML entity, and
   `<!-- CUSTOMIZE`, and fix any hit in a line this change added.
9. Before commit, check each numbered issue line and each SPEC bullet in
   `docs/approved-spec-2026-10-20.md` against the brief in session
   local_9d1a5e4a-29cd-4cb2-ae67-ba26dbd1d900 and record "N/N spec lines matched the brief" in the
   BUILDLOG entry; any line without a match fails this step.

## Out of scope
- Any code, CI, or retirement of the bundled Blender example (issue 0002).
- DESIGN.md's Python-only test, eval, and CI rules (around `DESIGN.md:274`, `:333`, `:338-366`);
  rewritten in 0002.
- `README.md` front-door rewrite (it still describes the template); done in 0002.
- Blender-specific skill and lifecycle lines (`DESIGN.md:56-57`, `:148`, `:161`); they describe the template's own machinery, not this change, and are left as they are.
- The `## Setup items` CUSTOMIZE marker in DESIGN.md (license choice), which is Trevor's to make.
