# addons/

> **These add-ons are INCLUDED EXAMPLES.** `bevel_bezier_corners.py` and `phyllotaxis.py` ship with
> the template to prove the pipeline, tests, and CI all work end-to-end on a fresh clone. They are not
> your project. When you start your own work, delete them along with their tests (`tests/`) and evals
> (`evals/`), and replace this file with documentation for what you build.

Blender 5.1 add-ons produced by the orchestrator pipeline.

---

## bevel_bezier_corners.py

Rounds sharp corners of any active POLY or BEZIER curve into circular-arc
bezier fillets. Each corner becomes two bezier knots with explicit `FREE`
handles; straight segments between fillets are preserved.

### Install

**Option A — Blender Preferences (persistent)**

1. Blender menu: Edit > Preferences > Add-ons > Install.
2. Navigate to `addons/bevel_bezier_corners.py` and confirm.
3. Enable the add-on in the list.

**Option B — Text Editor (session only)**

1. Open the Blender Text Editor, open `bevel_bezier_corners.py`.
2. Run Script (Alt+P or the Run Script button).

### Use

1. Select a curve object in the viewport.
2. Open the Sidebar (N key) > Edit tab > Bevel Corners panel.
3. Click **Bevel Bezier Corners**. Adjust **Radius** to taste.

The operator is also available via the F3 search menu as "Bevel Bezier Corners".

### Radius parameter

Sets the fillet radius in scene units. The setback distance is automatically
clamped so fillets on adjacent corners never overlap. Corners that are nearly
straight (colinear within 1e-4 rad) are left unchanged.

### Geometry function

`rounded_corner()` is bpy-free (uses only the `math` standard library) and
is unit-tested outside Blender in `tests/run_pure.py` — which checks the
tangent points, the arc-side handles, and that the fillet is a true circular
arc (not a flat chamfer). `tests/mutation_harness.py` proves that test catches
deliberately-broken geometry.

---

## phyllotaxis.py

Places N points in the golden-angle (sunflower-seed) phyllotaxis pattern and
creates a new mesh object whose vertices are those points. Appears in
**Add > Mesh > Phyllotaxis**.

### Install

Same two options as `bevel_bezier_corners.py` above (Blender Preferences or
Text Editor run-script).

### Use

In Object Mode, go to **Add > Mesh > Phyllotaxis** (or F3 search "Phyllotaxis").
Adjust parameters in the operator redo panel.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `count` | int (min 1) | 300 | Number of points to generate. |
| `scale` | float (min 0.0001) | 0.1 | Radial scale; point i sits at radius `scale * sqrt(i)`. |
| `dome` | float | 0.0 | When non-zero, lifts points into a paraboloid: `dome * (1 - (r/r_max)^2)`. Zero keeps the pattern flat. |

### Geometry function

`phyllotaxis_points(count, scale, dome=0.0)` is bpy-free (uses only the `math`
standard library) and is unit-tested outside Blender in
`tests/run_phyllotaxis_pure.py` — which checks the golden-angle step between
points, the `scale * sqrt(i)` radius law, and the dome paraboloid.
`tests/mutation_harness.py` proves that test catches a hard-coded or wrong
golden angle. The golden angle is derived from the golden ratio (`math.sqrt(5)`),
not a hard-coded constant.
