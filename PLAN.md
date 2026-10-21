# Build plan

**Owner:** orchestrator (the main Opus loop). Authoritative design: [DESIGN.md](DESIGN.md).

Holds the build sequence for THIS project — the order the orchestrator works through, broken into
dependency-correct segments. The orchestrator ([agents/orchestrator.md](agents/orchestrator.md)) drives
execution: for each segment it runs the `issue → review → implement → review → commit` pipeline and
appends a one-line entry to [BUILDLOG.md](BUILDLOG.md) on each commit.

<!-- CUSTOMIZE: replace the segment below with your own. Define the North Star first, then sequence
     features so each segment only depends on ones above it. One segment = one coherent unit of work. -->

## Segment order

1. **Segment 1 — define your North Star and first feature.** State what this project builds and for
   whom, then file the first feature issue (`issues/NNNN-*.md`) through the pipeline. See
   `issues/0001-example-feature.md` for the issue format.

(Add further segments here as the build sequence becomes clear.)
