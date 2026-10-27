# Build plan

**Owner:** orchestrator (the main Opus loop). Authoritative design: [DESIGN.md](DESIGN.md).

Holds the build sequence for THIS project — the order the orchestrator works through, broken into
dependency-correct segments. The orchestrator ([agents/orchestrator.md](agents/orchestrator.md)) drives
execution: for each segment it runs the `issue → review → implement → review → commit` pipeline and
appends a one-line entry to [BUILDLOG.md](BUILDLOG.md) on each commit.

## Segment order

1. **0001 North Star.** Write the SteamGate North Star, product outcomes, and known gaps into CLAUDE.md, DESIGN.md, and PLAN.md.
2. **0002 Service skeleton.** Windows service, protected settings folder (ProgramData, admin-only writes), install and uninstall, restart after being killed, retire the Blender example, and rewrite DESIGN.md's Python-only test, eval, and CI rules and the README's opening page for a C# / .NET build on GitHub's Windows build machines.
3. **0003 Steam game detection.**
4. **0004 Time accounting.**
5. **0005 Enforcement.**
6. **0006 The lock.** Cooldown rules, including making uninstall and disable wait the cooldown.
7. **0007 Status screen.** Its look is approved by Trevor on docs/preview-2026-10-21.html and docs/preview-2026-10-22-extras.html before it is built; his words are pinned as the `(status screen look)` amendment.
8. **0009 Extend button.** Its look is approved by Trevor on docs/preview-2026-10-26-extend.html before it is built; his words are pinned as the `(extension button)` amendment.
