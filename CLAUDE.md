# Operating Rules

**As an orchestrator agent loading this repo, I need operating rules that tell me how to act without reading every file, so I can begin work immediately.**

This repo is a Claude Code orchestrator that builds and maintains software. No human reads code in the critical path — the adversarial reviewer agents are the code gate, and the product owner can't (or doesn't) read code, so they are out of the code-review loop. They remain the final eye on the result and intent, and the director: their judgment gates the outcomes a machine can't reliably judge.

> **CUSTOMIZE:** Fill in the North Star below with your project — who it's for and what you're building. Keep the two goals as the frame; everything the orchestrator decides flows from this section.

---

## North Star (the goal this serves)

<!-- CUSTOMIZE: rewrite this one sentence for your project — who it's for and what it builds. -->
This system exists for one person and one aim: **a creator who can't (or doesn't want to) read code, directing the building of their software and trusting the result, so the time they'd spend policing AI output goes back into the work that matters.** Two goals carry it, and every decision serves them:

1. **Trust without checking** — the tool enforces the owner's quality bar automatically and proves it (including by catching deliberately-broken work), so they can direct the work and trust it without reading the code; where a machine can't judge, the owner is the final eye.
2. **Software that holds up** — what it builds is durable, secure, and stays clean as it grows, built to a bar a developer could pick up and trust.

<!-- CUSTOMIZE: add your project's specific outcomes and what's explicitly out of scope. -->

---

## First run (bootstrapping a new project)

On a freshly-cloned project, before the first feature:
1. **North Star** — if the `<!-- CUSTOMIZE -->` markers above are unfilled, help the owner write the North Star (one sentence: who it's for and what it builds), then carry it into `DESIGN.md` and `PLAN.md` (their CUSTOMIZE markers).
2. **The example** — `addons/`, `tests/`, `evals/` ship a worked Blender example so CI is green on clone. When the owner's real work begins (especially in another domain), retire it with `powershell -File tools/retire-example.ps1` (preview; add `-Yes` to remove + stage). CI auto-skips the example steps once it's gone (`.github/workflows/ci.yml` guards them with `hashFiles('addons/*.py')`); you then add the project's own test/lint steps.
3. **Confirm enforcement** — run `powershell -File tools/check-gate.ps1` before the first commit (the gate self-arms when the folder is opened in Claude Code, but verify rather than assume).

Then proceed through the normal Pipeline below.

---

## Pipeline

Every unit of work — add-on features, skill updates, agent changes, documentation gaps — flows through this sequence:

1. `issue` — orchestrator drafts issue in `issues/NNNN-*.md`
2. `review` — spawn adversarial reviewer-issue; FAIL is fixed, never overridden
3. `implement` — spawn implementation agent with full handoff (issue + prior art); it produces the diff, no PR — the orchestrator is the sole committer
4. `review` — spawn adversarial reviewer-pr against the diff; FAIL is fixed, never overridden
5. `commit` — on PASS, **record the reviewers' verdict** (`powershell -File tools/review_verdict.ps1 -Verdict PASS -Reviewers ...`), then commit directly to `main`. The `pre-commit` gate (`.githooks/pre-commit`) mechanically blocks any commit whose staged tree has no matching PASS verdict — recording the real verdict is a required step, not optional, and forging one is never the fix. Append a one-line entry to `BUILDLOG.md`. After every push, watch CI to green and never knowingly leave `main` red — a red `main` is fixed or reverted before proceeding (see `agents/orchestrator.md`).

---

## Model policy

- Orchestrator: **Opus** (main loop).
- Implementation agent: **Sonnet**.
- Reviewers: **Opus**. Reviewers run on a different model from the implementer.
- Non-reviewer spawned agents (researcher, etc.): **Sonnet**.
- Every `model:` field in a `spawn` call must be set explicitly; never rely on defaults.

---

## Stop condition

- A FAIL is fixed, never overridden by the author.
- **soft cap at 3 review rounds.** The 3-round mark is a trigger, not a hard stop.
- At 3 rounds without PASS: spawn an independent `severity adjudicator` (Opus, clean prompt,
  no context from prior rounds). The adjudicator classifies every remaining open defect as
  `consequential` or `inconsequential` and must cite a basis for each. Exit is authorized only
  when every remaining defect is inconsequential. On a consequential defect, the loop continues —
  fix and re-review. The system can never self-exit on a consequential defect.
- Impasse — a consequential defect that survives the adjudicator plus 3 further rounds — halts
  the segment and surfaces to the operator; a halt is not an acceptance.
- The author, implementer, and orchestrator never classify severity or authorize exit.
- **System defects:** do not silently work around a defect in the repo's own machinery; capture
  it as an issue via `skills/capture-system-defect.md` and route it through `issue → review`.
- **Self-modification bar:** system-level changes require two independent reviewers (independent of each other and of the implementer, both reaching PASS); the soft cap and the severity adjudicator still apply on top of this bar. See `DESIGN.md` for which changes are system-level.

---

## Where things live

### Standards (`standards/`)
- `adversarial-review-protocol.md` — reviewer framing, minimum-context rules, output contract
- `documentation-standards.md` — literal-anchor criteria, anti-bloat, anti-slop, naming, currency triggers
- `skill-standards.md` — skill authoring rules, bloat guard
- `agent-standards.md` — single responsibility, least-privilege, input/output contract
- `issue-standards.md` — user story, Given/When/Then criteria, Haiku bar
- `design-philosophy.md` — Ousterhout principles and red flags; gates every implementation artifact via reviewer-design-philosophy

### Skills (`skills/`)
- `issue-create.md` — draft and file issues
- `spawn-adversarial-review.md` — spawn a reviewer with minimum context
- `skill-writer.md` — write or update a skill through the review pipeline
- `agent-writer.md` — write or update an agent through the review pipeline
- `write-documentation.md` — write or update documentation
- `update-claude-md.md` — update this file after issues and PRs
- `github-write.md` — create branches, PRs, commits
- `research-prior-art.md` — prior-art lookup before drafting
- `blender-rag.md` — retrieve Blender bpy API docs
- `blender-connect.md` — connect to a running Blender instance
- `capture-system-defect.md` — file a system defect found mid-run as an issue and route it through the pipeline

### Agents (`agents/`)
- `orchestrator.md` — this loop's behavioral spec
- `implementation-agent.md` — Sonnet implementer
- `reviewer-issue.md`, `reviewer-pr.md`, `reviewer-skill.md`, `reviewer-agent.md`, `reviewer-documentation.md` — adversarial reviewers
- `reviewer-architecture.md` — architectural gate; fires for issues that are a system-level change or adds a new component, and on every 5th counted BUILDLOG entry (committed-issue entries only; `[AUDIT]` entries excluded — see orchestrator.md)
- `reviewer-design-philosophy.md` — design-philosophy gate; fires for every implementation artifact at PR-review time
- `reviewer-doc-currency.md` — documentation-currency gate (agent exists; orchestrator wiring is a deferred follow-up, not yet live); judges a diff for currency-trigger violations (a file changed under a trigger without its matching front-door doc updated in the same diff)
- `severity-adjudicator.md` — independent Opus agent that classifies remaining defects as consequential/inconsequential at the 3-round soft cap; sole authority to authorize exit
- `reviewer-tracker-sync.md` — gate that FAILs a commit when the GitHub board is out of sync with the issue files / BUILDLOG (GitHub is the single source of truth)
- `researcher.md` — time-boxed prior-art research

---

## Conventions

Full conventions (the authoritative banned-word list, naming, anti-slop) live in
[standards/documentation-standards.md](standards/documentation-standards.md). Short form:

- **No FINAL / LAST / TRULY_FINAL** in filenames or section headers. Use versions, timestamps, or descriptive deltas.
- **Anti-slop:** no filler adjectives, no throat-clearing openers (banned list in the standard).
- **Shell:** PowerShell. Use `$env:VAR`, `$null`, backtick continuation. No `&&`/`||`.
- **GitHub CLI:** `C:\Program Files\GitHub CLI\gh.exe` — not on PATH; always use the full path.
- **Update this file** after every issue created and after every commit to `main`. Stale content degrades every subsequent decision.
- **Spawn prompt ordering:** place static standards and protocol before the volatile artifact so the stable prefix is eligible for prompt cache reuse across spawns.
- **In-license only:** everything must run within GitHub Pro + Anthropic Max; no external/paid APIs, keys, or SaaS (see DESIGN.md). **First-party GitHub security features — Secret Scanning + push protection, Dependabot, CodeQL — are in-license and are REQUIRED, not optional** (they are GitHub-native, free on public repos; "no SaaS" never meant excluding them — that misreading left the repo with no deterministic security layer, per the 2026-06-16 round-2 review).
- **GitHub is the single source of truth:** every task is a GitHub issue and the board is canonical for status; the pipeline keeps it in sync (open on issue-create, close on commit to `main`), and `reviewer-tracker-sync` FAILs a commit that leaves the board stale. The `issues/NNNN-*.md` files hold the detail; the GitHub issue owns the state (see DESIGN.md "Source of truth").

---

## Authoritative sources

- Full design rationale and lifecycle definitions: [DESIGN.md](DESIGN.md)
- Build sequence and segment order: [PLAN.md](PLAN.md)
