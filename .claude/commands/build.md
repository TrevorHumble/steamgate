---
description: Run the full issue-to-commit pipeline on a goal. Usage: /build <goal>
---

You are the orchestrator defined in `agents/orchestrator.md`. Follow all rules in `CLAUDE.md` and `standards/`.

This pipeline assumes the session runs on Opus. If it does not, switch with `/model` before continuing — running the orchestrator below Opus degrades decision quality. (Reviewers and the severity adjudicator run on Opus; the implementation agent and other spawned agents run on Sonnet — see the Model policy below.)

Run goal: $ARGUMENTS

Execute the pipeline in order: `issue → review → implement → review → commit` (direct-push: the orchestrator is the sole committer; there is no PR/merge step).

Steps:
1. Delegate prior-art research to `agents/researcher.md` via `skills/research-prior-art.md`. Local sources first; web search only if they do not answer. If the goal touches Blender APIs, also run `skills/blender-rag.md` against the relevant symbols.
2. Draft the issue in `issues/NNNN-*.md` using `skills/issue-create.md`, incorporating prior-art findings. Then **open its GitHub issue** (`gh issue create`, labelled by tier) — GitHub is the single source of truth, so the board reflects the task from creation (see `skills/github-write.md`).
3. Spawn `agents/reviewer-issue.md` (Opus) via `skills/spawn-adversarial-review.md`. A FAIL is fixed, never overridden. Re-review with a fresh instance. If the issue is a system-level change or adds a new component, also spawn `agents/reviewer-architecture.md` (Opus) — both gates must pass before implementation begins.
4. Spawn `agents/implementation-agent.md` (Sonnet) with the passing issue and all prior-art paths.
5. Spawn the appropriate `agents/reviewer-*.md` (Opus) against the artifact. Reviewer receives only the artifact and the relevant standard — no framing, no hints. For every implementation artifact (code, agent spec, skill, or standard — not a doc-only or typo-only change), after `reviewer-pr` returns PASS, also spawn `agents/reviewer-design-philosophy.md` (Opus) — both gates must pass before commit.
6. On PASS, **record the reviewers' verdict** (`powershell -File tools/review_verdict.ps1 -Verdict PASS -Reviewers "<who>"`), then commit with a short message — the `.githooks/pre-commit` gate mechanically blocks any commit whose staged tree lacks a matching PASS verdict, so recording the real verdict is required (forging one is never the fix). Append one line to `BUILDLOG.md`. Then **close the GitHub issue** for this work (`gh issue close`, referencing the commit), and spawn `agents/reviewer-tracker-sync.md` (Opus) — it FAILs if the board is out of sync with the issue files / BUILDLOG. After every push, watch CI to green and never knowingly leave `main` red. The board is maintained at every transition: issue created → `gh issue` opened; committed → `gh issue` closed.

Stop condition — no author override: the 3-round mark is a soft cap — a trigger, not a hard cap. After 3 rounds without PASS, spawn one independent `severity adjudicator` (Opus, clean prompt, no prior-round context). The severity adjudicator classifies every remaining open defect as `consequential` or `inconsequential` and must cite a basis for each. Exit is authorized only when every remaining defect is inconsequential; the loop never accepts work while a consequential defect remains. On a consequential defect, the loop continues — fix and re-review, then re-invoke the severity adjudicator. Impasse — a consequential defect that survives the severity gate plus 3 further fix-and-re-review rounds — halts the segment; log the halt in `BUILDLOG.md` and continue with independent segments. The orchestrator tracks the post-gate round count and declares the impasse. a halt is not an acceptance — the work is not merged.

Model policy: orchestrator = Opus; implementation agent and non-reviewer spawned agents = Sonnet; all reviewers (including severity adjudicator) = Opus. Set `model:` explicitly on every spawn call — never rely on defaults.
