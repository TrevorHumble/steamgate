# Claude-as-judge eval

## Purpose

Deterministic checks confirm point counts and coordinate bounds, but they cannot confirm that the result looks right — smooth, visually balanced fillets. This eval fills that gap.

## Workflow

1. After `bevel_bezier_corners` runs, capture a viewport screenshot with `get_viewport_screenshot`.
2. Hand the screenshot to an `Opus` agent with the rubric below and no other framing.
3. The judge returns `PASS` or `FAIL` with cited reasons drawn from the rubric.

## rubric

The judge evaluates the rendered curve against each criterion independently, then returns one overall verdict:

- **Tangent continuity** — each fillet arc meets its straight edge tangentially; no kinks or cusps at the tangent points.
- **Radius consistency** — all four fillet arcs appear visually equal in size for a symmetric input shape (e.g. a square).
- **Closed-curve integrity** — a cyclic input yields a visually closed output curve with no gap at the join point.
- **Absence of artifacts** — no stray control points, no self-intersections visible in the viewport, no handle vectors protruding through the curve.

A `PASS` requires all four criteria to hold. A `FAIL` must cite which criterion failed and describe the visible defect.

## Notes

- This is `Claude-as-judge` running inside the Anthropic Max subscription. No external model or hosted eval service is involved; the judgment stays in-license.
- The judge agent must be spawned with `model: "opus"` (the Opus tier) so it does not share correlated blind spots with the Sonnet implementation agent.
- This eval is a manual/agent step, not part of the headless CI run. It is documented here so the orchestrator can invoke it after a successful `EVAL SCORE:` from `evals/run_evals.py`.
- Rubric revisions follow the standard `issue → review → implement → PR → review → commit` pipeline.
