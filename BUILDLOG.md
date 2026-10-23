# Build log

Append-only record of this project's build. Newest at the bottom. One line per committed unit of work (the orchestrator appends on each commit to `main`). `[AUDIT]` entries are periodic full-system reviews.

- 2026-10-20 #0001 North Star written into CLAUDE.md, DESIGN.md, PLAN.md; approved spec pinned in docs/approved-spec-2026-10-20.md; 17/17 spec lines matched the brief. Reviewers: reviewer-issue + reviewer-architecture (exit via severity adjudicator), reviewer-pr + reviewer-documentation PASS.
- 2026-10-21 #0002 Service skeleton, install/uninstall, auto-restart recovery; template example retired; CI runs build, tests, Stryker (95%) and the install test on windows-latest, green on e4c70f2. Reviewers: reviewer-issue, reviewer-pr, reviewer-design-philosophy, reviewer-agent (exit via severity adjudicator) PASS.
- 2026-10-21 #0003 Steam game detection: libraries and manifests read as untrusted input, local-junction support, not-counted list, snapshot each tick; CI green on fe4c02b. Reviewers: reviewer-pr, reviewer-design-philosophy, reviewer-documentation (exit via severity adjudicator) PASS.
- 2026-10-21 #0004 Time accounting: nights end 4 AM, week resets Monday 4 AM, tally saved at least once a minute and kept across a kill and restart; CI green on 195e593. Reviewers: reviewer-pr, reviewer-design-philosophy, reviewer-documentation PASS.
- 2026-10-21 #0008 Weekend nights default to Friday, Saturday, and Sunday; approved spec amended below the original, not edited; CI green on db9c7f9. Reviewers: reviewer-pr, reviewer-design-philosophy, reviewer-documentation (issue exit via severity adjudicator) PASS.
- 2026-10-21 #0005 Warnings and closing games: enforcement planner, pipe server, invisible helper with a silent overlay, polite then force close; CI green on 2848f88. Reviewers: reviewer-pr, reviewer-design-philosophy, reviewer-documentation, security review (exit via severity adjudicator) PASS.
