# Orchestrator — System Design

## Prime directive

This project is SteamGate. Its owner is Trevor, a functional tech who does not read code. It builds a Windows service in C# / .NET that holds his Steam gaming to time limits he sets, using the pipeline below. The spec is in `## SteamGate product spec`.

The product owner sets vision and tests outcomes. They do not necessarily write code. No human reads code in the critical path — the adversarial review agents are the code gate for everything that lands, so the owner is out of the code-review loop. They remain the final eye on the result and intent, and the director: their judgment gates the visible and qualitative outcomes a machine can't reliably judge.

## SteamGate product spec

The `CLAUDE.md` sections `### Product outcomes` and `### Known gaps (documented, not solved)` are the short form of this section.

The approved text is pinned, unedited, in docs/approved-spec-2026-10-20.md. If this section and that file disagree, that file governs and this section is corrected to match.

Approval source: Trevor said go on 2026-10-20 (session local_9d1a5e4a-29cd-4cb2-ae67-ba26dbd1d900) to the plan he pasted plus the four fixes the assistant proposed, then asked for "a max cooldown so you don't accidentally set it for like a year" and for Blender, OBS, and Wallpaper Engine on the not-counted list. Two values were added in the build brief afterward and were not shown to him before his go: "Steamworks Shared" in the pre-filled list, and the number 7 for the cooldown maximum (he asked for a maximum without a number). Trevor approved both later on 2026-10-20, passed on from the main session (local_9d1a5e4a-29cd-4cb2-ae67-ba26dbd1d900), in his words: "Both of those sound good to me."

Issue lines 1 to 6 below are repo issues 0002 to 0007; 0002 creates the protected settings folder from line 5 because the service needs it to start, and also retires the Blender example and rewrites the template's test, eval, CI, and README text, and 0006 builds the cooldown rules (an order chosen after approval, not part of the approved spec).

Issues, in order:

1. Service skeleton, install/uninstall, auto-restart recovery
2. Steam game detection (read libraryfolders.vdf + appmanifest files, match running programs to steamapps\common folders, exclude the not-counted list)
3. Time accounting (night ends 4 AM, weeknight/weekend nights, weekly cap resets Monday 4 AM, saved to disk every minute)
4. Enforcement (warnings at 10/5/1 min with sound, polite close then force, close any game started after the limit)
5. The lock (protected settings folder, cooldown rules below)
6. Status screen (see where you stand, request changes, edit the not-counted list)

SPEC

- Windows service in C# / .NET. No window, tray icon, or Alt-Tab entry. Windows restarts it if killed. Settings and tally in a protected system folder (ProgramData, admin-only writes).
- A small invisible helper runs in the user's own login and shows the warnings and does the polite close, because services can't draw on the screen or talk to game windows. The service does the counting and the force-close. Each warning plays a sound too, since Windows mutes notifications during full-screen games.
- Counts wall-clock minutes while at least one Steam game is open; two games at once don't double-count.
- Defaults: weeknight 90 min; weekend night 3 h; weekend nights = Friday + Saturday; weekly cap 10 h; cooldown 48 h.
- Cooldown rules: any change that loosens a rule (raise a limit, add a weekend night, shorten the cooldown, add a program to the not-counted list, disable, uninstall) waits the full cooldown. Any change that tightens applies right away. The cooldown has a hard maximum of 7 days, so it can't be set to a year by mistake.
- Not-counted list (whitelist), pre-filled with Blender, OBS Studio, Wallpaper Engine, and Steamworks Shared. Editable from the status screen; adding waits the cooldown, removing applies now.
- Known gaps, documented, not solved: Trevor is the admin, so a deliberate admin teardown can remove it; changing the system clock could fool the counter. Never hide from antivirus or Windows or block their removal.
- Trevor's account is an admin, so Task Manager CAN kill the service. Never claim it can't; Windows restarts it and the tally survives.
- Current Steam library: Arc Raiders, Cyberpunk 2077, Dying Light The Beast, Overwatch, Red Dead Redemption 2, Viewfinder (C:\Program Files (x86)\Steam).
- The .NET SDK was not found on this PC. CI can build on GitHub's Windows runners; ask Trevor before installing the SDK locally.
- The status screen is visual: Trevor approves its look on a preview before it is built.

The Blender line in the Issue lifecycle describes the template's own machinery and stays.

## SteamGate service design (orchestrator decisions, not part of the approved spec)

Decision logic lives in `SteamGate.Core`; `SteamGate.Service` only connects it to Windows (service lifetime, process and registry access, the tick).

- **One tick.** The worker runs one 5-second tick (`RunTickAsync`) and every periodic step of every later issue runs inside it. No other periodic loop or timer is started.
- **One file store.** Every JSON file goes through `JsonFileStore<T>`. A missing file loads the defaults and tries to write them. A file that is not valid JSON is kept under a `corrupt-<UTC time>` name and replaced by the defaults. A file whose values the type rejects (for settings: a null list item or a negative limit) counts as not valid JSON. The move to the corrupt name is retried like a read. A locked file is retried, then the error is thrown and nothing is overwritten. A save writes a temp file and moves it over the old one, and returns false instead of throwing when it keeps failing.
- **One caller of the stores.** Only the worker calls `Load` or `Save` on any store. Other components read an immutable snapshot the worker publishes after each tick.
- **Servers hand work to the worker.** A server that answers clients (the pipe) may run its own accept loop, but it hands every settings or tally change to the worker through a `Channel<T>` that `RunTickAsync` drains. Issue 0002 builds no channel; the first issue whose server hands a settings or tally change to the worker creates it.
- **Folder permissions.** `%ProgramData%\SteamGate` has inheritance removed, owner `BUILTIN\Administrators`, and full control for SYSTEM and Administrators only. No other identity has an entry.
- **Stop is refused, Shutdown is accepted.** The service refuses Stop from the Services window and accepts Shutdown. It is still listed under its real name, an admin can still end it (Windows restarts it), and the uninstall script removes it.
- **Crashes restart it.** Any unhandled error in the worker, or in any other long-running loop, logs and ends the process with exit code 1, so the Windows recovery setting restarts it. A server loop isolates per-connection and per-message errors and exits 1 only when the loop itself fails.
- **Fault switch.** The service crashes on start when its registry `Environment` value holds `STEAMGATE_FAULT_TEST=1`. It exists only for `install/Test-Install.ps1`, setting it needs admin, and it sits inside the admin-teardown gap.
- **Install doubles as upgrade.** An upgrade never touches ProgramData. A fresh install renames any existing `ProgramData\SteamGate` to `SteamGate.old-<time>` and creates a new admin-only folder.
- **Additive settings only.** The missing or corrupt fallback is a loosening that only an admin edit (the documented admin-teardown gap) or a disk failure can reach. Settings and tally schema changes are additive only (never rename a field or change its type), so an upgrade never takes the corrupt path.

## What the system is

A Claude Code orchestrator agent that builds the software this project targets and maintains itself. All work — including updates to the skills and agents that run it — flows through one pipeline:

`issue → review → implement → review → commit`

This is a direct-push model: the orchestrator is the sole committer; on PASS it records the verdict and commits straight to `main`. There is no pull request and no merge step. The system is self-maintaining: when a skill, agent, or standard needs to change, that change is issued, reviewed, implemented, and committed through the same pipeline as any product feature.

## Core principles

**Human-out-of-the-code-loop.** The product owner is never a required approver *of code* — the adversarial review agents approve or reject every code/spec/doc artifact, and no human reads code in the critical path. The owner remains the final eye on the result, where their judgment gates outcomes a machine can't reliably judge; the agents gate the code, the owner gates the qualitative result.

**Unbiased reviewers.** Reviewers receive minimum context. Prior feedback, author identity, and intent framing are withheld so the reviewer evaluates what the artifact actually says, not what it was meant to say.

**Adversarial-by-design.** Reviewers are instructed to find failure, not to confirm success. A passing review means nothing was left to object to, not that the reviewer was satisfied.

**Model-by-role.** The orchestrator runs on Opus. The implementation agent runs on Sonnet. Reviewers run on a different model from the implementer. All reviewer agents run on Opus so they do not inherit the implementer's correlated blind spots. Non-reviewer spawned agents (researcher, etc.) run on Sonnet. Haiku serves as the plan-clarity bar: if a plan would send a weak model off the rails, it is not ready to ship.

**Skill-bloat guard.** When updating a skill, apply the author's intent rather than transcribing the user's words. See the Skill-bloat problem section.

**Living-repo-same-pipeline.** Skills and agents are never patched in place without review. Every change — including changes to this system itself — goes through `issue → review → implement → review → commit` (direct-push: sole committer, no PR/merge).

## Repo structure

```
skills/
agents/
standards/
src/                               - product code: SteamGate.Core (decision and file-store logic), SteamGate.Service (the Windows service)
tests/SteamGate.Tests/             - xUnit tests that assert output values
install/                           - Install-SteamGate.ps1, Uninstall-SteamGate.ps1, SteamGate.Common.ps1 (shared by the three scripts), and Test-Install.ps1 (CI-only)
.config/                           - pinned dotnet tools (Stryker.NET)
tools/                             - gate and run scripts
docs/                              - pinned approved spec and the template's dated run log
issues/                            — issue files (NNNN-*.md); the GitHub board owns their status
.github/workflows/ci.yml           — CI pipeline; runs on every push and PR
.claude/commands/build.md          — /build slash command that triggers a pipeline run
.claude/settings.json              — Claude Code settings
SteamGate.sln · global.json · Directory.Build.props · stryker-config.json - build files
PLAN.md         — segment-by-segment build sequence
BUILDLOG.md     — one-line entries appended after each commit or halt
README.md
CLAUDE.md
DESIGN.md
```

Names only. This document describes the structure; it does not create or modify those files.

## Current scope

### Skills

- `blender-connect` — connect to a running Blender instance
- `blender-rag` — retrieve Blender API documentation for bpy code
- `github-write` — create issues, branches, PRs, and commits
- `issue-create` — draft and file issues through the issue lifecycle
- `spawn-adversarial-review` — spawn a reviewer agent with minimum context
- `skill-writer` — write or update a skill file through the review pipeline
- `agent-writer` — write or update an agent file through the review pipeline
- `update-claude-md` — update CLAUDE.md after issues and PRs
- `research-prior-art` — time-boxed prior-art lookup before drafting
- `write-documentation` — write or update documentation through the review pipeline
- `capture-system-defect` — file a defect found in the repo's own machinery as an issue and route it through the pipeline

### Agent roster

| Role | Tier |
|---|---|
| orchestrator | Opus |
| implementation agent | Sonnet |
| reviewer-issue | Opus |
| reviewer-pr | Opus |
| reviewer-skill | Opus |
| reviewer-agent | Opus |
| reviewer-documentation | Opus |
| reviewer-architecture | Opus |
| reviewer-design-philosophy | Opus |
| reviewer-doc-currency | Opus |
| researcher | Sonnet |

Reviewers run on Opus so they do not share the implementer's correlated blind spots. Non-reviewer spawned agents run on Sonnet.

`reviewer-architecture` gates issues that are a system-level change or adds a new component. It fires at issue-review time, after `reviewer-issue` passes.

`reviewer-design-philosophy` gates every implementation artifact at PR-review time. It fires after `reviewer-pr` returns PASS. This gate adds a reviewer spawn on every qualifying PR — a deliberate, accepted cost for design quality.

`reviewer-doc-currency` is designed to fire at PR-review time on any PR whose diff touches a currency-triggering path (a new or moved top-level directory, a new agent/skill/standard, a renamed interface or path). It judges the diff — not a single file — and FAILs when a currency-triggering change landed without its matching front-door doc (`README.md` layout, `DESIGN.md` structure map, `CLAUDE.md` roster) updated in the same diff. It complements `reviewer-documentation`, which judges one doc against the standard and cannot see cross-file staleness. The agent exists; wiring it into the orchestrator's PR-review fan-out as a mandatory gate is a deferred follow-up and is not yet live.

`reviewer-tracker-sync` fires at the end of a segment, after commit: it FAILs if the GitHub board is out of sync with the issue files / BUILDLOG (an issue shipped but still OPEN, an unfinished item CLOSED, an issue file with no card, or a label contradicting the declared tier). This is the maintaining gate that keeps GitHub-as-single-source-of-truth honest — the safeguard a manually-maintained mirror lacks.

A full-system architectural audit runs on every 5th counted BUILDLOG entry (committed-issue entries only; `[AUDIT]` entries excluded — see orchestrator.md): `reviewer-architecture` reviews DESIGN.md and the current agent/skill/standard inventory for drift, duplication, and contradiction.

### Process flows

- **Issue creation + review loop:** orchestrator drafts issue → `spawn-adversarial-review` sends it to reviewer-issue → PASS creates the issue; FAIL triggers revision and a fresh reviewer.
- **Implementation + review loop:** implementation agent builds the diff (no PR) → `spawn-adversarial-review` sends the diff to reviewer-pr → on PASS the orchestrator records the verdict via `tools/review_verdict.ps1`, commits directly to `main`, and watches CI to green; FAIL triggers revision and a fresh reviewer.
- **Skill/agent update loop:** changes to skills or agents enter the same `issue → review → implement → review → commit` pipeline (direct-push), using reviewer-skill and reviewer-agent respectively.

## Adversarial review protocol

### Minimum context

The reviewer receives only the artifact under review plus the single relevant standard. Nothing else is provided. "No framing" means the spawner adds no editorializing or expectation — it does not mean withholding the standard the reviewer requires.

For a PR review the "single relevant standard" is the linked issue's `## Acceptance criteria` section (per `agents/reviewer-pr.md`), not a separate standards file. Artifact-to-standard mapping:

| Artifact | Standard the reviewer receives |
|---|---|
| Issue | `standards/issue-standards.md` |
| PR diff | The linked issue's `## Acceptance criteria` section |
| Skill | `standards/skill-standards.md` |
| Agent | `standards/agent-standards.md` |
| Doc | `standards/documentation-standards.md` |
| Implementation artifact (design-philosophy gate) | `standards/design-philosophy.md` |

### Spawner constraints

The spawner must never:

- state what the artifact is trying to accomplish
- express any expectation about the outcome
- pre-answer anticipated objections
- summarize the artifact before handing it over
- identify the author
- use pass-leaning language such as "just verify" or "looks good, please confirm"

### Canonical reviewer framing

> "Your job is to find every way this artifact fails. Assume the author made mistakes. Do not give the benefit of the doubt. If something is ambiguous, treat it as a defect. Return a PASS only if there is nothing left to object to."

### Anchoring and position-bias mitigations

- A fresh reviewer is spawned for each review round. Prior reviewers' feedback is never passed to the next reviewer.
- When comparing two versions of an artifact, order is randomized and versions are labeled A/B — never "old/new" or "before/after."

### Output contract

The reviewer returns PASS/FAIL followed by a numbered list of specific defects. A PASS with no defects closes the review loop. A FAIL sends the artifact back for revision.

## Lifecycles

### Issue lifecycle

1. Orchestrator identifies a need (feature, bug, system change, documentation gap).
2. If the issue touches Blender APIs, pull the `blender-rag` skill to retrieve relevant bpy documentation.
3. Read `CLAUDE.md` and scan open issues for repo awareness before drafting.
4. Draft the issue: write an end-consumer user story, Given/When/Then acceptance criteria, an implementation plan, and a dependencies block.
5. Hand the raw issue to reviewer-issue with no framing — no summary, no intent statement.
6. Reviewer returns PASS/FAIL with a numbered list of defects.
7. On FAIL, revise the issue and re-submit to a fresh reviewer (the Ralph loop). Repeat until PASS.
8. If the issue builds visible output whose look the owner approves, nothing is researched or implemented until the issue records that approval (orchestrator step 2a).
9. On PASS, create the issue via `github-write`.
10. Update `CLAUDE.md` to reflect the new issue.

### Implementation lifecycle

This is a direct-push model: the orchestrator is the sole committer; there is no pull request and no merge step.

1. The Sonnet implementation agent builds toward the passing issue, confirming APIs per its build rule 2. It produces the diff only, no PR.
2. Hand the raw diff to reviewer-pr with no framing.
3. Reviewer checks: correctness, tests that assert real output values (not just counts) and trace to acceptance criteria, that the mutation/tamper gate still catches deliberately-broken code, lint/format, comment quality, naming, and architectural fit. When no .NET SDK is installed locally, reviewer-pr verifies test, mutation, and install criteria by tracing the changed code on a concrete input to a concrete output with file:line; the executed proof is the post-push CI build-test run, and a red run is fixed or the commit reverted before any other work.
4. On FAIL, fix the implementation and re-submit to a fresh reviewer (the Ralph loop). Repeat until PASS.
5. The design-philosophy gate (reviewer-design-philosophy) fires after reviewer-pr returns PASS.
6. On PASS, record the verdict via `tools/review_verdict.ps1` (binds it to the exact staged tree), then commit directly to `main` and watch CI to green before moving on — `main` is never knowingly left red. There is no branch protection by design; the post-commit CI-watch is the operative enforcement.
7. Update `CLAUDE.md` and the README if public-facing behavior changed.

### Skill/agent-update lifecycle

Updates to skills or agents follow the same pipeline. The artifact goes to reviewer-skill or reviewer-agent (as appropriate) rather than reviewer-pr. No skill or agent is patched in place without a passing review. This lifecycle is the enforcement mechanism for the living-repo-same-pipeline principle.

### The Ralph loop

An external evaluator (a fresh reviewer agent) checks the work and re-injects the task until it passes. Self-assessment is not trusted — the agent that produced the artifact does not evaluate it.

**Stop condition (soft cap + severity gate):** a FAIL is fixed, never overridden by the author.
The 3-round mark is a soft cap — a trigger, not a hard stop. At 3 rounds without PASS, the
orchestrator spawns an independent `severity adjudicator` (a fresh Opus agent, clean prompt, no
context from prior rounds). The adjudicator classifies every remaining open defect as
`consequential` or `inconsequential` and must cite a basis for each. Exit is authorized only
when every remaining defect is inconsequential; the loop never accepts work while a consequential
defect remains. The author, implementer, and orchestrator never classify severity or authorize
exit — that power belongs solely to the severity adjudicator (see `agents/severity-adjudicator.md`
and `standards/adversarial-review-protocol.md`). On a consequential defect, the loop continues:
fix and re-review, then re-invoke the adjudicator. Genuinely inconsequential items are logged to
`BUILDLOG.md` as follow-up issues. Impasse — a consequential defect that survives the severity
gate plus 3 further fix-and-re-review rounds — halts the segment; independent segments continue.
a halt is not an acceptance; the work is not committed.

**Design lesson — no dead safety mechanism.** A loop-continuation gate must release on a condition the
pipeline actually produces. An earlier design gated loop continuation on a verdict file that nothing in
the pipeline ever wrote, so it enforced nothing and only ever hit an iteration-count backstop — which
fails open AND would terminate a long autonomous run early, directly contradicting the never-stop loop it
was meant to protect. A dead safety mechanism documented as live is worse than none for a code-blind
owner. The template's loop gate below is the clock-driven successor that corrects this: tooling-enforced
loop continuation must be designed against the real clock, never a verdict file nothing writes.

**Loop gate (`.claude/hooks/loop-gate.ps1`) — the clock-driven enforcement this template provides.**
A Stop hook that, during an explicit timed run, blocks the model from ending its turn
until the clock budget is spent — the tooling-enforced never-stop loop, forcing what is otherwise only the
orchestrator's discipline. The release condition is the real
clock (`Get-Date -UFormat %s`), not a verdict file nothing writes, so it cannot dead-gate; it activates only
while `.run_state/run.json` exists (written by `tools/start-run.ps1`) and FAILS OPEN on any error, so it can
never trap a normal session; it releases automatically once `now >= end_epoch`; and `tools/stop-run.ps1` (or
a `.run_state/STOP` sentinel) is an always-available emergency brake. Runaway token-burn is bounded too: if the model
is already continuing from the hook (`stop_hook_active`) and stops again within ~25s (no real work), that
counts as churn, and after ~6 such empty turns the gate releases the session (real work between stops resets
the count and forcing resumes); a high absolute per-run turn cap is a final circuit breaker. All three
breakers RELEASE (never trap) and only true budget-expiry disarms the run — time is the primary bound. Project-scoped: fires only for in-repo sessions, where the orchestrator actually runs. Enforcement
boundary: it forces continuation of the session's turns; it does not by itself make the work each turn
high-value — that is the Done-Early Cascade's job (`orchestrator.md`). Paired with the commit gate below, the
loop is now forced at both ends: you cannot stop early, and you cannot commit unreviewed.

**Commit gate (`.githooks/pre-commit`) — the mechanical forcing this template provides.**
This is the mechanical forcing of "no unreviewed work reaches a commit" — without it the review pipeline
is just markdown the model could silently skip. A git-native `pre-commit` hook runs at true commit time and blocks the
commit unless `.review_state/verdict.json` records a PASS whose `tree_oid` equals `git write-tree`
of the staged index. `tools/review_verdict.ps1` records that verdict (from the reviewers' real
result); `tools/check-gate.ps1` asserts the gate is active; `tools/setup-hooks.ps1` activates it
(`core.hooksPath -> .githooks`). It is git-native, not a Claude hook, deliberately: it then covers
`git commit -a`/amend/pathspec/untracked content and cannot be regex-bypassed or wrongly fire on a
`grep "git commit"`. Per the no-dead-mechanism lesson, the verdict file is actually written by
the pipeline (orchestrator step 6), and it fails CLOSED.

*Enforcement boundary (stated honestly so a future audit isn't misled):* the hook enforces only on a
working copy where it is active and not bypassed with `git commit --no-verify`. It does NOT stop a
deliberately forged verdict (same actor can write one), `--no-verify`, or an unconfigured clone.
Two backstops narrow those: a CI integrity check (`.github/workflows/ci.yml`) fails `main` red if
the gate file is deleted or LF-corrupted, and the run-start `check-gate.ps1` assertion stops an
orchestrator from trusting a gate that isn't on. The remaining gap — proving a review *genuinely
happened* per commit — is not closeable by a local hook or any forgeable committed record; it is
closed only by the program-driven loop (P2), where the verdict is produced by a reviewer agent call
in control flow rather than asserted by the committer. The gate is a strong forgetting/stale guard
and the durable floor P2 builds on, not a claim of unforgeable per-commit review.

## Standards

### User story

Each issue begins with an end-consumer user story written from the perspective of the person or system consuming the artifact. It states who needs what and why, with no implementation detail.

### Acceptance criteria

Criteria are Given/When/Then criteria testable by an agent — literal, mechanically checkable properties of the artifact. Semantic interpretation is not required. At least one criterion must assert a behavioral output value (a concrete input → expected output) so the criteria can catch a wrong implementation, not just a missing file or string (documentation-only issues are exempt). A behavioral input→output criterion is checked by reading the produced artifact or the output of the artifact's own test — both are mechanical, not semantic. If a criterion cannot be checked that way (by string match, structural inspection, or a concrete input→output assertion), rewrite it until it can; no vague or semantic criteria.

### Implementation plan

Every issue contains an implementation plan that meets the Haiku bar. The Haiku bar is a clarity heuristic: the plan must be unambiguous enough that following it would not send a weak model off the rails. The implementer is a Sonnet agent; Opus is used only when called for.

### Skills

A skill applies the author's intent rather than transcribing the user's words. When feedback arrives, diagnose the root cause and make the smallest edit that addresses it. See the Skill-bloat problem section.

### Agents

Each agent has a single responsibility, the least-privilege tool set needed for that responsibility, and a defined input/output contract. An agent that does too much is a design defect.

### Comments

Comments state why, not what. One line maximum. No filler, no restatement of what the code already says, no AI-slop voice.

### Naming

No FINAL, LAST, or v2_final in any filename. Use version numbers (v1, v2), dates, or descriptive deltas that remain accurate after the next iteration.

### Test strength

The bar is that tests assert **correct output values**, not that they exist or that some
line-coverage percentage is hit. A coverage percentage is a vanity metric — a suite can touch
every line and still assert nothing meaningful, which for a code-blind owner is false confidence.
So the gate is two real things instead: (1) the xUnit tests assert actual output values of the
project's core logic, and (2) Stryker.NET mutation testing deliberately breaks `SteamGate.Core` and
proves those tests flip to FAIL. The build breaks below an 80% mutation score, and surviving mutants
are listed in the CI log so a reviewer can tell an equivalent mutant from a missing test. No
line-coverage threshold is measured or required. Documentation artifacts are exempt from tests.

### CLAUDE.md update triggers

Update `CLAUDE.md` after every issue created and after every commit to `main`. The orchestrator loads CLAUDE.md at the start of each session; stale content degrades every subsequent decision.

### Documentation currency

When public-facing behavior changes, update the README in the same PR. When operating rules change, update CLAUDE.md. When the system design changes, update DESIGN.md.

## Skill-bloat problem

**The failure mode.** When updating a skill from feedback, the model over-indexes on the user's literal words, appends them verbatim, and bloats a tight skill into long, unfocused mush. The original intent is diluted. The skill becomes harder to invoke and harder to review.

**The discipline.** Treat feedback as a symptom report. Diagnose the root cause of the symptom, make the smallest edit that fixes it, and default to subtraction over addition. Never paste the user's sentence into the skill. Re-review the skill after every update, using reviewer-skill through the standard review pipeline.

## Deferred items

The following have not yet shipped. Each becomes a future issue when the system is ready for it.

- Documentation-enforcement agent — checks that CLAUDE.md and README are updated when required triggers fire.
- Comment-review agent — enforces the why-not-what, one-line, anti-slop comment standard.
- Resilience and reliability testing — testing the orchestrator's behavior under failed tool calls, bad reviewer outputs, and looping conditions.

### Provided by the template

- CI enforcement: `.github/workflows/ci.yml` runs the .NET build, unit tests, Stryker.NET mutation tests, and the install test on a Windows runner on every push and PR via GitHub Actions.
- Loop and commit gates — see the "Loop gate" and "Commit gate" notes above. An earlier verdict-file-gated Stop hook was the dead-mechanism design lesson the loop gate corrects.

## System-level change definition

A change is a system-level change when it modifies any of the governing artifacts:
`standards/adversarial-review-protocol.md`, any other file under `standards/`, any file under
`agents/`, `DESIGN.md`, or `CLAUDE.md`.

System-level changes are subject to the two-independent-reviewer bar defined in
`standards/adversarial-review-protocol.md`. Product code under `src/`, `tests/`, and
`install/` and non-governing skills are
not system-level — single reviewer suffices for them.

When uncertain whether a change qualifies, apply the enumerated list above: if the diff touches
any of those paths, it is system-level.

---

## Governance: in-license constraint

The system runs inside two entitlements only: a `GitHub Pro` license and an `Anthropic Max` (Claude) subscription. There is `no other external/paid API`, account, key, or third-party SaaS.

The only sanctioned model access is the Anthropic subscription. No non-Anthropic model key (OpenAI, Gemini, etc.) and no hosted eval/judge service (`Braintrust`, `LangSmith`, `OpenAI Evals`, Langfuse, Weights & Biases).

Under the in-license constraint above, SteamGate uses no Steam Web API and no service that needs a key or payment. It detects running games from local Steam files and the local list of running programs (see `## SteamGate product spec`); issue 0003 builds that detection.

CI runs on GitHub Actions under `GitHub Pro` on the public repo (unlimited minutes). `actions/setup-dotnet` installs the SDK pinned in `global.json` on the runner, and the tests and Stryker.NET come from free NuGet packages.

Code review is `Claude-as-judge` (the Opus reviewer agents). The runtime is C# / .NET: the xUnit tests, the Stryker.NET mutation tests, and the install test (`install/Test-Install.ps1`) run on the GitHub Windows runner. Any dependency needing a non-Anthropic key or a hosted account is out of license by definition.

## CI

`.github/workflows/ci.yml` runs on every push and pull request via GitHub Actions on the public repo
(GitHub Pro, unlimited minutes). Two jobs run:

- `gate-integrity` (Ubuntu) checks that the commit gate file exists and is protected from line-ending damage.
- `build-test` (`windows-latest`) runs these steps in order:
  1. `actions/checkout`, then `actions/setup-dotnet`, which installs the SDK pinned in `global.json`.
  2. `dotnet build -c Release`. The code analyzers fail the build on any warning.
  3. `dotnet test -c Release`. xUnit tests assert output values.
  4. `dotnet tool restore`, then `dotnet stryker`. Stryker.NET mutation testing on `SteamGate.Core` breaks the build below an 80% mutation score.
  5. `dotnet publish` of `SteamGate.Service` as a self-contained single file for `win-x64`, then a copy of the install and uninstall scripts and their shared `SteamGate.Common.ps1` next to it.
  6. `install/Test-Install.ps1`, run with Windows PowerShell 5.1. It installs the service, ends it, and checks that Windows restarts it, that a crash inside the service is restarted, that Stop is refused, that the settings folder is admin-only, and that upgrade keeps settings and uninstall removes the service, its program files, and its settings folder.
  7. Upload of the install package as the `steamgate-win-x64` artifact.

Each step fails the job on a non-zero exit. In-license: no Codecov, Coveralls, or any hosted coverage or test SaaS.

## Evals

There is no separate eval harness. The end-to-end check is `install/Test-Install.ps1` on the Windows runner. The status screen's look is approved by Trevor on a preview before it is built (see `## SteamGate product spec`).

## Testing

Tests are xUnit v3 tests in `tests/SteamGate.Tests`, run through `dotnet test`:

- They cover `SteamGate.Core`, where all decision and file-store logic lives, plus the service log writer.
- Stryker.NET reruns them against deliberately broken copies of `SteamGate.Core` (see `### Test strength`). It runs them through the Microsoft Testing Platform runner.

The tests run on the CI Windows runner on every push, and locally with `dotnet test -c Release` when a .NET SDK is installed.

## Setup items

<!-- CUSTOMIZE: fill these in when you stand up your project. -->
- Repo name and URL — set automatically when you create the repo from the template (`git remote -v` shows it); no separate file needed.
- License — choose one before publishing (MIT is a reasonable default for an open template).
- How a run is triggered — the `/build` slash command at `.claude/commands/build.md`.

## Bootstrap

A first build can be bootstrapped: until the committed standards and skills exist, frame reviewers from
an external adversarial-review protocol, commit directly with git, and spawn reviewers directly, until the
committed standards and skills in this repo take over. PLAN.md holds the segment-by-segment build sequence.

## Source of truth

**GitHub is the single source of truth** for project state — every task is a GitHub issue, and its
status (open/closed/labels) on the board is canonical. The board is **kept in sync by the pipeline**, not
by anyone remembering to update it: the orchestrator opens a GitHub issue when an issue file is created
and closes it on commit to `main`, and `agents/reviewer-tracker-sync.md` FAILs any commit that leaves the
board disagreeing with reality. A single, always-current board is what the operator needs — not a log that
some other record has to be reconciled against.

**File model — files = detail, issue = status.** The rich `issues/NNNN-*.md` files stay in the repo: they
hold the full acceptance criteria and plan, are version-controlled and diffable, and are what the reviewer
agents read. The matching GitHub issue owns the *status*. The discipline that prevents the drift that
killed the old manual mirror: a file describes the work; the GitHub issue owns the state; the pipeline
keeps them equal, and the sync gate enforces it. `BUILDLOG.md` remains the append-only history of commits;
it is a log, not the task tracker.

## Where the documentation lives

**DESIGN.md** is the full evolving design and the source of truth for the system. When in doubt about how something works or why a decision was made, this is the document to consult. It grows with the system. On any conflict between DESIGN.md and CLAUDE.md, DESIGN.md governs and CLAUDE.md is corrected to match.

**CLAUDE.md** is the distilled operating rules the orchestrator loads at the start of each session. It is derived from DESIGN.md. It contains what the orchestrator needs to act correctly — not the full rationale, just the rules. It is kept short enough to load every time.

**README.md** is the short human-facing front door. It orients a newcomer, links to DESIGN.md for depth, and links to CLAUDE.md for operating rules. It does not duplicate content from either.

**docs/approved-spec-2026-10-20.md** is Trevor's approved SteamGate spec, pinned unedited. On any conflict about the product spec, it governs DESIGN.md "## SteamGate product spec", which governs CLAUDE.md "### Product outcomes".
