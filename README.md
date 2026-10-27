# SteamGate

SteamGate is a Windows service, written in C# / .NET, that holds Trevor's Steam gaming to time limits he sets. The service runs with no window, tray icon, or Alt-Tab entry, keeps its settings in an admin-only ProgramData folder, and Windows restarts it if it is killed. Warnings and closing games work: an invisible helper shows a silent overlay at 30, 20, 10, 5, and 1 minutes left, then the game is closed at the limit. SteamGate opens from the Start Menu to show the time left, to request changes, and to extend tonight by 10, 20, or 30 minutes in the last 30 minutes before the limit. See [DESIGN.md](DESIGN.md) for the spec.

## Install

Open the repo's Actions tab, pick the latest green CI run on `main`, and download `steamgate-win-x64` from its Artifacts list (you must be signed in to GitHub). Extract it. Then, from an administrator PowerShell in that folder, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-SteamGate.ps1
```

Artifacts expire after 90 days. If none is listed, open the Actions tab, choose CI, and press Run workflow to make a fresh one. To remove SteamGate, open SteamGate from the Start Menu and press Request removal. Once the cooldown has passed, run `Uninstall-SteamGate.ps1` the same way.

---

The rest of this file describes the orchestrator template SteamGate is built with.

## Self-Enforcing Orchestrator: project template

Clone this and you have a project where an AI orchestrator builds your software through an adversarial review pipeline — AI reviewers whose job is to *find fault*, not rubber-stamp — **that you don't have to police**. The enforcement is baked into the repo, not left to the agent's good behavior.

No human reads code in the critical path. Reviewer agents are the gate. Three hooks make the rules mechanical, not optional. You direct; the system builds and checks itself.

---

## Before you start

You need these installed once (all free):
- **GitHub CLI** — [cli.github.com](https://cli.github.com). Sign in with `gh auth login`.
- **Claude Code** — the AI that does the building.
- .NET 10 SDK: only to build locally; CI builds without it.
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
| `src/` | `SteamGate.Core` (decision and file-store logic) and `SteamGate.Service` (the Windows service) |
| `src/SteamGate.Helper` | The invisible helper in the user's session: the warning overlay and the polite close |
| `src/SteamGate.Status` | The status screen: time left, rules, waiting changes, and the not-counted list |
| `tests/SteamGate.Tests/` | xUnit tests that assert output values |
| `install/` | Install and uninstall scripts, the CI-only install test, and `PipeProbe.ps1` (CI-only) |
| `.config/` | Pinned dotnet tools (Stryker.NET mutation testing) |
| `SteamGate.sln` · `global.json` · `Directory.Build.props` · `stryker-config.json` | Build files: solution, SDK pin, shared compiler and analyzer settings, mutation-test settings |
| `.github/workflows/` | CI (`ci.yml`) + CodeQL (`codeql.yml`) |
| `DESIGN.md` · `PLAN.md` · `BUILDLOG.md` | Design rationale · build plan · running log |

Full design rationale: **[DESIGN.md](DESIGN.md)**. Plain-language "what a green build proves": **[WHAT-IT-CHECKS.md](WHAT-IT-CHECKS.md)**.
