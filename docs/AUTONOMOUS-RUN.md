# Autonomous Run — working state

**Loop mechanics are canonical in `agents/orchestrator.md` → "Autonomous timed run (never-stop loop)" and
"Self-review is automatic." This file is the run's WORKING STATE — the budget, the priority queue, and the
Live log. A fresh/compacted instance reads `orchestrator.md` for the rules and this file for where it is.**

## The one rule
Time-driven, not task-driven. The run ends only when real elapsed time reaches the budget (default
**120 min**) — never because a queue emptied or work "felt done." "Done early" is not a state; it triggers
the Done-Early Cascade (see `orchestrator.md`), which refills the queue.

## North Star
The owner directs and trusts the tool without reading code, so their time goes to the work that matters. The
loop's job is to enforce on itself what they are tired of enforcing on the agent. Gates must be REAL and PROVEN.

## Priority order (engine before polish)
- **P1 — The engine.** This loop + the self-timing ledger + Produce→Auto-Review, baked into
  `orchestrator.md` (done; keep verifying it holds in practice). Without it, the next session needs the owner.
- **P2 — Product gates.** Real correctness tests (geometry values, not counts; phyllotaxis has none); the
  mutation/tamper harness that proves the tests catch deliberately-broken work, in CI, logging
  `guards caught N/N`; honesty doc fixes (kill the coverage-fiction in DESIGN.md).
- **P3 — Continuous improvement** via the Cascade until the clock runs out.

## Live log (ledger — one line per increment; a fresh instance continues from the last line)
Format: the ledger line defined in `agents/orchestrator.md` → "Autonomous timed run" (canonical there).

- **2026-06-21 — Engine built and hardened.** Plan rewritten as a time-driven never-stop loop; the engine
  was baked into `orchestrator.md` and hardened after a two-reviewer FAIL→fix (impasse-halt reconciled to
  re-enter the selector, emergency stop-list added, ledger mechanism, WRAP-window forcing, cascade-cannot-
  exit-empty, write-scope contradiction removed). Budget: 120 min. Non-blocking decisions for the owner
  (answer in chat, never stalls the run): (a) should `main` hard-require CI/PRs before merge; (b) CLAUDE.md
  "out of the operational loop" contradicts the goal — should the orchestrator surface domain-judgment
  checkpoints to the owner (the "final eye"), or treat their absence as total?

### RUN OUTCOME (summary for a returning reader; full ledger below)

**Mission: make the gates REAL and PROVEN. Done and CI-verified end-to-end (incl. real Blender steps).**

Shipped (13 commits, `a995f13..`, all pushed, every CI run green — incl. the real Blender steps):
- **Every gate now asserts real geometry, not counts** — bevel pure (handles + circular-arc midpoint), phyllotaxis pure (golden angle / radius / dome), bevel headless operator positions, phyllotaxis headless operator mesh, eval square + triangle (acute) + large-radius CLAMP positions. All five gates green in CI including the live Blender headless + eval runs.
- **Mutation/tamper harness** (`tests/mutation_harness.py`) — breaks both add-ons, proves the pure tests catch it (`guards caught 6/6`), with per-mutant attribution (credits a catch only when the RIGHT guard fired) + a `MUTATION_SCORE` line. The North-Star "smoke detector," teeth proven both directions.
- **Deterministic (non-LLM) layer strengthened** — new parallel `lint` CI job: ruff (`F,B`) + bandit on `addons/`, verified clean before wiring. Turns code-cleanliness + security-pattern checks from LLM-opinion into identical-every-run gates; further-gate menu (mypy/vulture/radon) filed on #32.
- **Docs made honest** — removed the fictional ≥80% coverage gate (#44); marked the dead Stop-hook inert pending #46; refreshed test docs; eval added to the local runner.
- **Engine hardened** — autonomous-run timing must be read from a real clock, never estimated (fixed mid-run after an 11m drift would have caused an early WRAP).
- Closed #41, #42, #44. Filed #48 (CodeQL gap), #49 (ledger binding), #50 (mutation-test the Blender gates). Reconciled #35/#32; commented dead-hook evidence on #46; posted an SVG-editor security checklist on #40 and a deterministic-gate menu on #32.

Parked for the owner (decisions only they can make — never blocked the run):
- **#48 CodeQL** — claimed enabled in an old log, but zero analyses exist; enabling is a security setting (their call). Secret Scanning + push protection + Dependabot are verified ON.
- **#46** — the Stop-hook is dead (nothing writes its verdict file); fix-or-delete is governance.
- **#45 / CLAUDE.md "out of the operational loop" vs the goal's "final eye on the result" (e.g. in Blender, for a visual project)** — the standing contradiction, theirs to resolve.
- **#40 SVG editor** — the product direction; security gates pre-scoped.

### RUN STARTED — 2026-06-21 18:15:33 (epoch 1782065733) · budget 120m · WRAP at 20:15:33 (epoch 1782072933)
- `[18:15] elapsed=0m/budget=120m | selector→DO pre-flight-arch+quality-review | next=architecture+quality review (self + 2 adversaries) then fix before P2`
- `[18:19] elapsed=4m/budget=120m | selector→DO pre-flight-review-done | next=fix-cluster: tests→harness→doc-honesty→security. Both adversaries PASS-equivalent (converged punch-list); confirmed plan P2 priority. Findings: phyllotaxis 0 tests, bevel ignores handles, no mutation harness, coverage-80% fiction, CodeQL absent, timing never real.`
- `[18:21] elapsed=5m/budget=120m | selector→DO #41-phyllotaxis-test | next=delegate phyllotaxis correctness test (golden angle + radius law + dome) to implementation-agent, then Opus review`
- `[18:26] elapsed=11m/budget=120m | selector→DO #41-phyllotaxis-test(commit) | next=commit phyllotaxis test (impl PASS + tamper-checked, Opus review PASS non-tautological), then bevel handle assertions`
- `[18:24] elapsed~9m/budget=120m | selector→DO #41-bevel-arc(commit) | next=commit bevel handle+arc test (Opus review PASS; chamfer rejected ~58x; 60deg case correct), close #41, then #42 mutation harness`
- NOTE: ledger timestamps before this line were hand-estimated and ran ~11m fast. Corrected against the epoch clock: measured elapsed at the #41 push = 13.7m (real time ~18:29), not 25m. All subsequent lines are stamped from `Get-Date -UFormat %s`, never guessed — the WRAP decision depends on the real clock.
- `[18:29] elapsed=13.7m/budget=120m | selector→DO #42-mutation-harness | next=delegate the tamper/mutation harness (the North Star "smoke detector"): mutate copies of both add-ons, prove the pure gates flip to FAIL, emit "guards caught N/N", wire into CI. Then Opus review.`
- `[18:38] elapsed=22.3m/budget=120m | selector→DO #42-mutation-harness(commit) | next=commit+push harness (guards caught 6/6; teeth proven both ways: no-op→SURVIVED, crash→ERRORED; 2 Opus reviews PASS), close #42, then #44 doc-honesty (coverage fiction + false "unit-tested" claim)`
- `[18:47] elapsed=31.6m/budget=120m | selector->DO #44-doc-honesty(commit) | next=commit doc-honesty (coverage fiction removed from DESIGN; README made honest; impl-agent contradiction fixed FAIL->fix; 2 reviewers), close #44, then security determinism (CodeQL workflow + pip dependabot)`
- `[18:52] elapsed=36.8m/budget=120m | selector->DO engine-timing-hardening(commit) | next=commit orchestrator.md clock-read timing fix (2 reviewers PASS; [HH:MM]-binding residual filed backlog), file CodeQL-gap + ledger-binding issues, then continue cascade`
- `[18:54] elapsed=38.6m/budget=120m | selector->CASCADE (pre-flight punch-list cleared at ~37m, well before budget) | next=Done-Early Cascade: (a) holistic review of whole system vs North Star + (c) deep web research on mutation-testing/Blender-addon best practice, in parallel; act on findings to refill queue`
- `[19:04] elapsed=49.1m/budget=120m | selector->DO harness-rigor(commit) | next=commit 4a per-mutant attribution + 4b mutation-score (Opus review PASS, MIS-CAUGHT teeth proven), push, then continue cascade: comment #46 dead-hook evidence, holistic state`
- `[19:11] elapsed=56m/budget=120m | selector->DO headless-geometry(commit+CI-verify) | next=commit run_headless.py position assertions (Opus review PASS, expected recomputed 1e-16), push, WATCH CI green (Blender-side verification), then continue`
- `[19:18] elapsed=62.6m/budget=120m | selector->DO eval-geometry(commit+CI) | next=commit eval position checks (Opus review PASS, 14 pts recomputed 0.0 diff; every gate now asserts geometry), push, watch CI green, then final completeness review before WRAP`
- `[19:19] elapsed=64.1m/budget=120m | selector->CASCADE completeness-critic | next=independent whole-run audit (8 commits a995f13..c2e2180) for regressions/contradictions/loose-ends + board sync; act on findings, then keep improving until WRAP`
- `[19:25] elapsed=69.8m/budget=120m | selector->DO phyllotaxis-operator-test(commit+CI) | next=commit phyllotaxis headless test (Opus PASS, #35 operator half), push, then DESIGN.md dead-hook honesty fix (completeness-critic MAJOR) + tests/README refresh`
- `[19:29] elapsed=74.4m/budget=120m | selector->DO doc-honesty(commit+CI) | next=commit DESIGN dead-hook annotation (2 reviewers PASS) + tests/README refresh + eval-in-local-runner, push, watch CI, approach WRAP`
- `[19:32] elapsed=77.4m/budget=120m | selector->CASCADE deep-research | next=research SVG-parsing security gates (forward prep for #40, ties to CodeQL #48 + Goal 2); post checklist to #40, then approach WRAP`
- `[19:43] elapsed=88.4m/budget=120m | selector->DO eval-clamp-positions(commit+CI) | next=commit clamp position check (Opus PASS, duplicate-safety proven; every eval case now position-asserts), push, watch CI, then WRAP window`
- `[19:46] elapsed=91.2m/budget=120m | selector->CASCADE deep-research (genuine work past 90m, not over-building) | next=research in-license deterministic quality gates (addresses arch-review thin-deterministic-layer + #32 ruff scope); post actionable menu, then approach WRAP window`
- `[19:54] elapsed=98.9m/budget=120m | selector->DO lint-gate(commit+CI) | next=commit ruff+bandit deterministic gate (verified clean twice, Opus PASS), push, watch CI (new lint job must go green), then WRAP window (~105m)`
- `[19:56] elapsed=101m/budget=120m | selector->WRAP-approach (genuine improvement space exhausted; remaining work is owner-decisions or over-building two 250-line add-ons) | next=commit final run-outcome summary, hand back. Mission + full Cascade done, CI-verified; deterministic layer strengthened past 90m as instructed.`
- `[19:59] elapsed=104m/budget=EXTENDED (owner: don't stop; answer your own questions, 10-expert holistic review, compare-to-goals, research+fix) | next=spawn 12-agent fan-out (10 experts + goal-conformance + best-practices research), synthesize, execute goal-aligned fixes via pipeline; act on self-answered Qs (CodeQL on, final-eye doc fix, dead-hook consultant, SVG=don't-build)`
- `[20:09] elapsed=113.6m/EXTENDED | selector->DO wave1-bugfixes(commit+CI) | next=commit 3 real bugs (Edit-Mode poll, degenerate-edge guard, phyllotaxis self-test->independent anchors; 2 reviews PASS each), push, watch CI; then Wave2: clamp coincident-knots, DELETE dead hook, reconcile out-of-loop/final-eye, CodeQL workflow`
- `[20:24] elapsed=129.4m/EXTENDED | wave2 done (clamp e8fe729 + governance a705403, CI green, #46 closed). next=Wave3 (Extensions manifest+validate, Blender download cache) then synthesize decision-set for the owner (visual gate, branch-protection, CodeQL, reviewer-collapse, DRY refactor)`
- `[20:33] elapsed=137.5m/EXTENDED | wave3 done (manifests + Blender cache, 7e8b3d5, CI green). Extended session totals: 12-expert review + goal audit + research; fixed Edit-Mode guard, degenerate-edge guard, phyllotaxis self-test, clamp coincident-knots; deleted dead hook; reconciled out-of-loop/final-eye; manifests; CI cache. 4 commits, all CI-green. Surfacing director-level forks (visual gate, CodeQL, branch protection, remaining engineering backlog) to the owner.`
- `[22:09] elapsed=234.2m/EXTENDED | Goal-mandated items done: decision-rule baked in (4eaa3dd); CodeQL enabled+verified (0 results, real scanning); visual-confirmation gate v1 BUILT + WORKING (rendered both add-ons, downloaded artifact, LOOKED — bevel=rounded square, phyllotaxis=sunflower spiral; #1 goal gap closed). next=remaining engineering backlog (DRY refactor, reviewer-collapse, docs onboarding, SHA-pin, merge-gate codify, #50 Blender-mutation).`
