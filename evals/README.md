# Evals

Two complementary eval modes for `bevel_bezier_corners`. Both are `in-license` — no external SaaS, no non-Anthropic model, no additional paid API.

## Deterministic geometry evals

Machine-scored checks against measurable geometric properties. Dependency-free: no pytest, no pip.

**Run:**

```
blender --background --python evals/run_evals.py
```

The script registers the add-on, builds each input curve, applies the operator, and asserts properties (point counts, cyclic flag, endpoint positions, colinear-vertex handling, clamped-radius bounds). It prints a `[PASS]` or `[FAIL]` line per case and a final `EVAL SCORE: <passed>/<total>` line. Exits 0 on full pass, 1 on any failure.

**Cases** (`evals/cases.py`):

| Name | Input | Checks |
|---|---|---|
| `square_r04` | Cyclic POLY square (±1,±1) | BEZIER type, cyclic, 8 points |
| `triangle` | Cyclic POLY triangle | 6 points |
| `open_polyline` | NON-cyclic 3-point L-shape | 4 points; endpoints at original vertices |
| `colinear_skipped` | Cyclic square + 1 colinear midpoint | 9 points (colinear not split) |
| `large_radius_clamped` | Cyclic square, radius=5 | 8 points, all finite, within bounds |

## Claude-as-judge eval

For the subjective axis — does the curve look right? — the orchestrator captures a viewport screenshot via `get_viewport_screenshot` and hands it to an `Opus` agent with a written rubric. The judge returns `PASS` or `FAIL` with cited reasons. See `evals/judge.md` for the full rubric and workflow.

This is a `Claude-as-judge` pass using the Anthropic Max subscription. It is a manual/agent step triggered after the deterministic suite passes — not part of the headless CI run.

## License

All evals are `in-license`. No external eval service, no non-Anthropic model key, no hosted account required.
