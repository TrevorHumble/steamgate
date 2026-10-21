"""
cases.py — eval case definitions for bevel_bezier_corners.

Each case is a dict with keys:
  name   — str, unique case identifier.
  build  — callable() -> bpy.types.Object; constructs an input curve object,
            links it into the scene, and returns it WITHOUT running the operator.
  checks — list of callables (obj) -> (bool, str); each asserts one geometric
            property AFTER the operator has run. Return (True, msg) on pass,
            (False, msg) on fail.
  radius — optional float, fillet radius for this case (default 0.4).

All build callables import bpy at the module level (cases.py runs under Blender).
"""

import math
import bpy

# ---------------------------------------------------------------------------
# Shared build helpers
# ---------------------------------------------------------------------------

def _new_cyclic_poly_curve(name, corners):
    """Return a new cyclic POLY curve object with the given corner XYZ tuples."""
    cd = bpy.data.curves.new(name=name, type='CURVE')
    cd.dimensions = '3D'
    sp = cd.splines.new('POLY')
    sp.use_cyclic_u = True
    sp.points.add(len(corners) - 1)
    for i, (x, y, z) in enumerate(corners):
        sp.points[i].co = (x, y, z, 1.0)
    obj = bpy.data.objects.new(name, cd)
    bpy.context.collection.objects.link(obj)
    return obj


def _new_open_poly_curve(name, points):
    """Return a new NON-cyclic POLY curve object with the given point XYZ tuples."""
    cd = bpy.data.curves.new(name=name, type='CURVE')
    cd.dimensions = '3D'
    sp = cd.splines.new('POLY')
    sp.use_cyclic_u = False
    sp.points.add(len(points) - 1)
    for i, (x, y, z) in enumerate(points):
        sp.points[i].co = (x, y, z, 1.0)
    obj = bpy.data.objects.new(name, cd)
    bpy.context.collection.objects.link(obj)
    return obj


# ---------------------------------------------------------------------------
# Check helpers
# ---------------------------------------------------------------------------

def _first_bezier_spline(obj):
    """Return (spline, None) or (None, error_str)."""
    splines = list(obj.data.splines)
    if len(splines) != 1:
        return None, f"expected 1 spline, got {len(splines)}"
    sp = splines[0]
    if sp.type != 'BEZIER':
        return None, f"expected BEZIER spline, got {sp.type}"
    return sp, None


def _is_finite(v):
    return all(math.isfinite(x) for x in v)


def _match_positions_unordered(produced, expected, tol):
    """Return (ok, msg) — every expected point must have a DISTINCT produced
    match within tol (each produced point consumed at most once).

    Tries index-by-index first (the operator preserves insertion order); on
    mismatch, falls back to distinct set matching so a mere ordering difference
    does not red-fail. Only a wrong/missing position fails. Mirrors the matcher
    in tests/run_headless.py.
    """
    def _close(p, q):
        return sum((p[i] - q[i]) ** 2 for i in range(3)) ** 0.5 <= tol

    n = len(expected)
    if len(produced) != n:
        return False, f"expected {n} points, got {len(produced)}"

    if all(_close(produced[k], expected[k]) for k in range(n)):
        return True, f"{n} points match expected positions (in order)"

    # Order-independent fallback: consume each produced point at most once.
    remaining = list(produced)
    for exp in expected:
        idx = next((i for i, got in enumerate(remaining) if _close(got, exp)), None)
        if idx is None:
            return False, f"no produced point within tol of expected {exp}"
        remaining.pop(idx)
    return True, f"{n} points match expected positions (unordered)"


# ---------------------------------------------------------------------------
# Case: square_r04
#   4 cyclic POLY corners (±1, ±1, 0) → 8 BEZIER points, cyclic.
# ---------------------------------------------------------------------------

def _build_square_r04():
    return _new_cyclic_poly_curve(
        'eval_square_r04',
        [(-1.0, -1.0, 0.0), (1.0, -1.0, 0.0), (1.0, 1.0, 0.0), (-1.0, 1.0, 0.0)],
    )


def _check_square_is_bezier(obj):
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    return True, "spline is BEZIER"


def _check_square_is_cyclic(obj):
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    if not sp.use_cyclic_u:
        return False, "spline is not cyclic"
    return True, "spline is cyclic"


def _check_square_8pts(obj):
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    n = len(sp.bezier_points)
    if n != 8:
        return False, f"expected 8 bezier points, got {n}"
    return True, f"8 bezier points"


# Expected .co for the square (radius 0.4), in _corner_knots emit order
# (per corner: t1 then t2). Computed OFFLINE from the verified pure
# rounded_corner (tests/run_pure.py), NOT imported here — cases.py runs under
# Blender and the ground truth must be independent of the add-on under test.
# Count alone is insufficient: 8 points at the origin or the wrong radius would
# pass an 8-count check; only a position check catches wrong geometry.
_SQUARE_EXPECTED_CO = [
    (-1.0, -0.6, 0.0),
    (-0.6, -1.0, 0.0),
    ( 0.6, -1.0, 0.0),
    ( 1.0, -0.6, 0.0),
    ( 1.0,  0.6, 0.0),
    ( 0.6,  1.0, 0.0),
    (-0.6,  1.0, 0.0),
    (-1.0,  0.6, 0.0),
]


def _check_square_positions(obj):
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    produced = [tuple(bp.co[:3]) for bp in sp.bezier_points]
    return _match_positions_unordered(produced, _SQUARE_EXPECTED_CO, 1e-4)


# ---------------------------------------------------------------------------
# Case: triangle
#   3 cyclic POLY corners → 6 BEZIER points.
# ---------------------------------------------------------------------------

def _build_triangle():
    return _new_cyclic_poly_curve(
        'eval_triangle',
        [(0.0, 1.5, 0.0), (-1.3, -0.75, 0.0), (1.3, -0.75, 0.0)],
    )


def _check_triangle_6pts(obj):
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    n = len(sp.bezier_points)
    if n != 6:
        return False, f"expected 6 bezier points, got {n}"
    return True, "6 bezier points"


# Expected .co for the acute triangle (radius 0.4), in _corner_knots emit order
# (per corner: t1 then t2). Computed OFFLINE from the verified pure
# rounded_corner (tests/run_pure.py), NOT imported here — cases.py runs under
# Blender and the ground truth must be independent of the add-on under test.
# This is the only end-to-end positional assertion at non-90° (acute) corners:
# a 6-count check passes even if the fillet were placed at wrong angles, so the
# exact positions are what prove the acute-corner geometry is correct.
_TRIANGLE_EXPECTED_CO = [
    ( 0.346346,  0.900555, 0.0),
    (-0.346346,  0.900555, 0.0),
    (-0.953269, -0.149889, 0.0),
    (-0.606923, -0.75,     0.0),
    ( 0.606923, -0.75,     0.0),
    ( 0.953269, -0.149889, 0.0),
]


def _check_triangle_positions(obj):
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    produced = [tuple(bp.co[:3]) for bp in sp.bezier_points]
    return _match_positions_unordered(produced, _TRIANGLE_EXPECTED_CO, 1e-4)


# ---------------------------------------------------------------------------
# Case: open_polyline
#   NON-cyclic 3-point L-shape: (0,0,0) (2,0,0) (2,2,0).
#   Endpoints are kept unfilleted; one interior corner is filleted.
#   Expected: 4 bezier points (1 endpoint + 2 fillet knots + 1 endpoint).
#   Endpoint knot positions must equal original first/last vertex.
# ---------------------------------------------------------------------------

_OPEN_POLY_PTS = [(0.0, 0.0, 0.0), (2.0, 0.0, 0.0), (2.0, 2.0, 0.0)]


def _build_open_polyline():
    return _new_open_poly_curve('eval_open_polyline', _OPEN_POLY_PTS)


def _check_open_4pts(obj):
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    n = len(sp.bezier_points)
    if n != 4:
        return False, f"expected 4 bezier points, got {n}"
    return True, "4 bezier points"


def _check_open_endpoints(obj):
    """First and last bezier points must sit on original first/last vertex."""
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    first_expected = _OPEN_POLY_PTS[0]
    last_expected  = _OPEN_POLY_PTS[-1]
    pts = sp.bezier_points
    eps = 1e-5
    first_co = tuple(pts[0].co[:3])
    last_co  = tuple(pts[-1].co[:3])
    for got, want, label in [(first_co, first_expected, "first"), (last_co, last_expected, "last")]:
        for i in range(3):
            if abs(got[i] - want[i]) > eps:
                return False, f"{label} endpoint co {got} != expected {want}"
    return True, "endpoints match original vertices"


# ---------------------------------------------------------------------------
# Case: colinear_skipped
#   Cyclic POLY square with 5 input points; one extra vertex sits exactly on
#   the midpoint of an edge (colinear).  The colinear vertex is NOT split into
#   two fillet knots — it stays as one knot.
#   Expected: 9 bezier points (4 real corners × 2 + 1 colinear kept), not 10.
# ---------------------------------------------------------------------------

def _build_colinear_skipped():
    # Square corners plus midpoint of the bottom edge (colinear).
    return _new_cyclic_poly_curve(
        'eval_colinear_skipped',
        [
            (-1.0, -1.0, 0.0),
            (0.0,  -1.0, 0.0),   # midpoint of bottom edge — colinear
            (1.0,  -1.0, 0.0),
            (1.0,   1.0, 0.0),
            (-1.0,  1.0, 0.0),
        ],
    )


def _check_colinear_9pts(obj):
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    n = len(sp.bezier_points)
    if n != 9:
        return False, f"expected 9 bezier points (colinear kept, not split), got {n}"
    return True, "9 bezier points: colinear vertex not split"


# ---------------------------------------------------------------------------
# Case: large_radius_clamped
#   Cyclic POLY square (±1, ±1, 0); operator run with radius=5 (half-edge=1).
#   Fillet radius is clamped so the setback d lands every tangent point on an
#   edge midpoint. At this radius each corner's T2 and the next corner's T1
#   coincide on the shared edge's midpoint, so the operator MERGES those
#   coincident knots: 8 raw knots collapse to 4 (the four edge midpoints), each
#   a proper bezier knot with arc handles on both sides — a clean rounded shape
#   with NO zero-length segments.
#   Expected: 4 points, all finite, every point within the original square's
#   bounding box (with a small epsilon for floating-point slop).
# ---------------------------------------------------------------------------

def _build_large_radius_clamped():
    return _new_cyclic_poly_curve(
        'eval_large_radius_clamped',
        [(-1.0, -1.0, 0.0), (1.0, -1.0, 0.0), (1.0, 1.0, 0.0), (-1.0, 1.0, 0.0)],
    )


def _check_large_4pts(obj):
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    n = len(sp.bezier_points)
    if n != 4:
        return False, f"expected 4 bezier points after coincident-knot merge, got {n}"
    return True, "4 bezier points"


def _check_large_all_finite(obj):
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    for i, bp in enumerate(sp.bezier_points):
        for attr in ('co', 'handle_left', 'handle_right'):
            v = getattr(bp, attr)
            if not _is_finite(v[:3]):
                return False, f"point {i} {attr} contains non-finite value: {tuple(v[:3])}"
    return True, "all coordinates finite"


def _check_large_within_bounds(obj):
    """All knot positions (not handles) must lie within the original ±1 square +eps."""
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    eps = 1e-4
    for i, bp in enumerate(sp.bezier_points):
        x, y = bp.co[0], bp.co[1]
        if x < -1.0 - eps or x > 1.0 + eps or y < -1.0 - eps or y > 1.0 + eps:
            return False, f"point {i} co ({x:.4f}, {y:.4f}) escaped square bounds"
    return True, "all knots within original square bounds"


# Expected .co for the clamped square (radius 5.0, half-edge 1.0) AFTER the
# coincident-knot merge. With radius 5.0 the setback d clamps to
# min(half_prev, half_nxt)=1.0, so each corner's T2 and the next corner's T1
# land on the SAME edge midpoint; the operator merges those coincident knots,
# collapsing 8 raw knots to the 4 distinct edge midpoints. Computed OFFLINE
# through the operator path (_corner_knots -> _resolve_straight_handles ->
# _merge_coincident_knots), NOT imported here — cases.py runs under Blender and
# the ground truth must be independent of the add-on under test. This verifies
# the merge produces CORRECT, non-degenerate positions; the within-bounds check
# alone passes for any in-bounds point, and would not catch duplicate knots.
_LARGE_EXPECTED_CO = [
    (-1.0,  0.0, 0.0),
    ( 0.0, -1.0, 0.0),
    ( 1.0,  0.0, 0.0),
    ( 0.0,  1.0, 0.0),
]


def _check_large_positions(obj):
    sp, err = _first_bezier_spline(obj)
    if err:
        return False, err
    produced = [tuple(bp.co[:3]) for bp in sp.bezier_points]
    return _match_positions_unordered(produced, _LARGE_EXPECTED_CO, 1e-4)


# ---------------------------------------------------------------------------
# EVAL_CASES — the list consumed by run_evals.py
# ---------------------------------------------------------------------------

EVAL_CASES = [
    {
        "name": "square_r04",
        "build": _build_square_r04,
        "checks": [
            _check_square_is_bezier,
            _check_square_is_cyclic,
            _check_square_8pts,
            _check_square_positions,
        ],
        # radius defaults to 0.4 in run_evals.py
    },
    {
        "name": "triangle",
        "build": _build_triangle,
        "checks": [
            _check_triangle_6pts,
            _check_triangle_positions,
        ],
    },
    {
        "name": "open_polyline",
        "build": _build_open_polyline,
        "checks": [
            _check_open_4pts,
            _check_open_endpoints,
        ],
    },
    {
        "name": "colinear_skipped",
        "build": _build_colinear_skipped,
        "checks": [
            _check_colinear_9pts,
        ],
    },
    {
        "name": "large_radius_clamped",
        "build": _build_large_radius_clamped,
        "checks": [
            _check_large_4pts,
            _check_large_all_finite,
            _check_large_within_bounds,
            _check_large_positions,
        ],
        "radius": 5.0,
    },
]
