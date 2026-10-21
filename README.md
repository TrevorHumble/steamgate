# Self-Enforcing Orchestrator — project template

Clone this and you have a project where an AI orchestrator builds your software through an adversarial review pipeline — AI reviewers whose job is to *find fault*, not rubber-stamp — **that you don't have to police**. The enforcement is baked into the repo, not left to the agent's good behavior.

No human reads code in the critical path. Reviewer agents are the gate. Three hooks make the rules mechanical, not optional. You direct; the system builds and checks itself.

---

## Before you start

You need these installed once (all free):
- **GitHub CLI** — [cli.github.com](https://cli.github.com). Sign in with `gh auth login`.
- **Claude Code** — the AI that does the building.
- **Python 3** — the safety checks run on it.
- **Windows + PowerShell** — the hooks and setup are written in PowerShell (this template targets Windows).

## Start a new project (3 steps)

```powershell
# 1. Make your own repo from this template (creates it on GitHub + clones it here).
#    --public lets the free CodeQL security scan run; use --private to skip only that.
gh repo create my-project --template TrevorHumble/blender-orchestrator-template --public --clone
cd my-project

# 2. Turn on enforcement (once).
powershell -ExecutionPolicy Bypass -File setup.ps1

# 3. Open the folder in Claude Code, accept the trust prompt, then type your goal:
#    /build a tool that <does the thing you want>
```

You can't forget enforcement: **opening the folder in Claude Code arms the commit gate automatically** the first time you open it and accept the workspace-trust prompt. Running `setup.ps1` (step 2) is still recommended — it *also* sets your git identity and confirms every gate up front, and it works from a plain terminal — but the gate won't be left off if you skip it.

That's it. Before your first build, open **[CLAUDE.md](CLAUDE.md)** and fill in your **North Star** (one sentence: who it's for and what it builds) — every decision the agent makes flows from it.

**Want to be sure you're protected?** Run `powershell -File tools/check-enforcement.ps1` anytime — it tells you, in plain words, which gates are on. For the deeper picture — *how thoroughly* each change was actually reviewed — run `powershell -File tools/pipeline-audit.ps1`.

> **If a hook doesn't seem to fire:** close and reopen the folder in Claude Code once — the goal and loop gates load when the session starts and you accept the workspace-trust prompt.

---

## What's already wired in

**Three enforcement hooks** (the part that makes "trust without checking" real):

| Hook | What it forces | Lives in |
|---|---|---|
| **Commit gate** | No commit lands unless a review verdict says PASS for exactly that code. | `.githooks/pre-commit` |
| **Goal gate** | Blocks the agent's *ask-a-question* tool, so it decides from your goals instead of stalling on you. | `.claude/hooks/goal-gate.ps1` |
| **Loop gate** | A timed autonomous run can't quit early — it works the full budget. | `.claude/hooks/loop-gate.ps1` |

**The build pipeline** — `issue → review → implement → review → commit`, driven by the orchestrator (`agents/orchestrator.md`), gated by adversarial reviewer agents (`agents/reviewer-*.md`). System-level changes need two independent reviewers.

**CI/CD on every push** (`.github/workflows/`) — tests + a mutation/tamper gate that proves the tests catch broken work, GitHub CodeQL security scanning, and Dependabot. All free on a public repo within GitHub Pro.

**Doc stubs you fill in** — `CLAUDE.md` (operating rules + your North Star), `DESIGN.md`, `PLAN.md`, `BUILDLOG.md`.

**A worked example** — `addons/` ships two small Blender add-ons with full tests so CI is green the moment you clone. When you start your own work, retire them with one command — `powershell -File tools/retire-example.ps1` (preview; add `-Yes` to remove). They're there to prove the whole pipeline runs end-to-end on a fresh clone.

---

## Run it autonomously

```powershell
powershell -File tools/start-run.ps1 -Minutes 120   # arm a timed run; the loop gate blocks early stops
powershell -File tools/stop-run.ps1                  # emergency brake (or create .run_state/STOP)
```

---

## Repo layout

| Path | Purpose |
|---|---|
| `CLAUDE.md` | Operating rules loaded every session — **edit the North Star first** |
| `agents/` | Orchestrator, implementation agent, reviewers, severity adjudicator, researcher |
| `skills/` | Orchestrator actions (issue-create, spawn-adversarial-review, github-write, …) |
| `standards/` | Authoring + review standards (adversarial protocol, design-philosophy, …) |
| `.githooks/` | The commit gate (`pre-commit`) |
| `.claude/` | Hooks (goal gate, loop gate), settings, the `/build` command |
| `setup.ps1` · `tools/` | one-time setup (root `setup.ps1`); `check-enforcement`, `setup-hooks`, `check-gate`, `review_verdict`, `start-run`, `stop-run` |
| `tests/` · `evals/` | Dependency-free test + eval harness (with the example add-ons) |
| `.github/workflows/` | CI (`ci.yml`) + CodeQL (`codeql.yml`) |
| `DESIGN.md` · `PLAN.md` · `BUILDLOG.md` | Design rationale · build plan · running log |

Full design rationale: **[DESIGN.md](DESIGN.md)**. Plain-language "what a green build proves": **[WHAT-IT-CHECKS.md](WHAT-IT-CHECKS.md)**.
