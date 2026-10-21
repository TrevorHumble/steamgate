"""
bevel_bezier_corners.py — Blender 5.1 add-on.

Rounds each sharp corner of an active POLY or BEZIER curve into a true
circular-arc fillet expressed as two bezier knots with explicit handles.
"""

import math
from collections import namedtuple
import bpy
from bpy.props import FloatProperty

bl_info = {
    "name": "Bevel Bezier Corners",
    "author": "blender-orchestrator",
    "version": (1, 0, 0),
    "blender": (5, 1, 0),
    "location": "View3D > Sidebar > Edit",
    "description": "Round sharp curve corners into circular-arc bezier fillets",
    "category": "Add Curve",
}


# ---------------------------------------------------------------------------
# Pure vector helpers — no bpy, no mathutils; unit-testable outside Blender.
# ---------------------------------------------------------------------------

def _v3(t):
    return (float(t[0]), float(t[1]), float(t[2]))


def _add(a, b):
    return (a[0]+b[0], a[1]+b[1], a[2]+b[2])


def _sub(a, b):
    return (a[0]-b[0], a[1]-b[1], a[2]-b[2])


def _scale(v, s):
    return (v[0]*s, v[1]*s, v[2]*s)


def _dot(a, b):
    return a[0]*b[0] + a[1]*b[1] + a[2]*b[2]


def _normalize(v):
    n = math.sqrt(v[0]*v[0] + v[1]*v[1] + v[2]*v[2])
    if n < 1e-12:
        return (0.0, 0.0, 0.0)
    return (v[0]/n, v[1]/n, v[2]/n)


def _clamp(x, lo, hi):
    return lo if x < lo else (hi if x > hi else x)


# ---------------------------------------------------------------------------
# Pure geometry — no bpy, no mathutils; unit-testable outside Blender.
# ---------------------------------------------------------------------------

# Fillet result: two bezier knot positions and their arc-side handles.
_Fillet = namedtuple("_Fillet", "t1 t2 handle_t1 handle_t2")

# A single bezier knot.
#
# Contract between the two build phases:
#   _corner_knots  sets the arc-side handle (hl or hr) and leaves the
#                  straight-side handle as None.
#   _resolve_straight_handles  fills every None handle by pointing it
#                  1/3 toward the adjacent emitted knot, returning
#                  fully-populated _Knots with no None handles.
#
# Fields:
#   co  — (x, y, z) knot position.
#   hl  — left handle; None when deferred to _resolve_straight_handles.
#   hr  — right handle; None when deferred to _resolve_straight_handles.
_Knot = namedtuple("_Knot", ["co", "hl", "hr"])

# Per-spline geometry bundle passed from the build phase to the write phase.
_SplineData = namedtuple("_SplineData", "cyclic knots")


def rounded_corner(prev, corner, nxt, radius, colinear_eps=1e-4):
    """
    Compute a circular-arc fillet for one curve corner.

    All arithmetic uses only the `math` module and plain (x,y,z) tuples.
    No external Blender modules are referenced here.

    Parameters
    ----------
    prev, corner, nxt : (x, y, z) tuples/lists
        Positions of the previous knot, the corner knot, and the next knot.
    radius : float
        Desired fillet radius (> 0).
    colinear_eps : float
        Threshold for the near-colinear guard (radians, near 0 or near pi).

    Returns
    -------
    _Fillet  namedtuple with fields t1, t2, handle_t1, handle_t2.
        t1 and t2 are the two new bezier knot positions.
        handle_t1 is the corner-facing handle of t1 (arc side).
        handle_t2 is the corner-facing handle of t2 (arc side).
    None  if the corner is near-colinear and should be left unfilleted.
    """

    p = _v3(prev)
    c = _v3(corner)
    n = _v3(nxt)

    a = _normalize(_sub(p, c))   # unit vector: corner → prev
    b = _normalize(_sub(n, c))   # unit vector: corner → nxt

    # Degenerate-edge guard: _normalize returns the zero vector when an edge
    # length is below 1e-12 (duplicate adjacent vertex). A zero vector would
    # give _dot(a,b)=0 -> theta=pi/2 and fool the colinear guard below into
    # emitting a bogus fillet, so reject the corner here as degenerate.
    if a == (0.0, 0.0, 0.0) or b == (0.0, 0.0, 0.0):
        return None  # zero-length edge — leave unfilleted

    raw_dot = _clamp(_dot(a, b), -1.0, 1.0)
    theta = math.acos(raw_dot)

    # near-colinear guard — covers theta near 0 (vectors point the same way)
    # and theta near pi (opposite: the corner is already a straight line).
    # Both cases are colinear in the sense that no arc geometry can be placed.
    if theta < colinear_eps or (math.pi - theta) < colinear_eps:
        return None  # colinear corner — leave unfilleted

    # Setback distance along each edge.
    d = radius / math.tan(theta / 2.0)

    # Clamp so neighbouring fillets on adjacent edges never overlap.
    half_prev = 0.5 * math.sqrt(_dot(_sub(p, c), _sub(p, c)))
    half_nxt  = 0.5 * math.sqrt(_dot(_sub(n, c), _sub(n, c)))
    d = _clamp(d, 0.0, min(half_prev, half_nxt))

    r_eff = d * math.tan(theta / 2.0)

    # Tangent points: the two new bezier knots.
    T1 = _add(c, _scale(a, d))
    T2 = _add(c, _scale(b, d))

    # The factor 4.0/3.0 is the standard cubic-bezier circular-arc approximation.
    alpha = math.pi - theta
    h = (4.0/3.0) * math.tan(alpha / 4.0) * r_eff

    # Corner-facing handles (arc side): offset from T1/T2 back toward the corner.
    handle_T1 = _add(T1, _scale(a, -h))
    handle_T2 = _add(T2, _scale(b, -h))

    return _Fillet(t1=T1, t2=T2, handle_t1=handle_T1, handle_t2=handle_T2)


# ---------------------------------------------------------------------------
# Operator helpers — module-level, named, docstring-contracted.
# ---------------------------------------------------------------------------

def _corner_knots(knots, cyclic, radius):
    """
    Build the list of _Knot records for one spline.

    The arc-side handle of each fillet knot is set here; the straight-side
    handle is left as None for _resolve_straight_handles to fill.

    Specifically:
    - Unfilleted knots (open-curve endpoint, colinear corner) emit one
      _Knot with both handles None.
    - Filleted corners emit two _Knots: T1 with hr set (arc handle) and
      hl=None; T2 with hl set (arc handle) and hr=None.
    - Returns an empty list when source knot count < 2.
    """
    n = len(knots)
    if n < 2:
        return []

    emitted = []
    for i in range(n):
        if cyclic:
            prev_pos = knots[(i - 1) % n]
            nxt_pos  = knots[(i + 1) % n]
        else:
            if i == 0 or i == n - 1:
                emitted.append(_Knot(co=knots[i], hl=None, hr=None))
                continue
            prev_pos = knots[i - 1]
            nxt_pos  = knots[i + 1]

        result = rounded_corner(prev_pos, knots[i], nxt_pos, radius)

        if result is None:
            # Colinear or degenerate — keep the original corner knot.
            emitted.append(_Knot(co=knots[i], hl=None, hr=None))
        else:
            # T1: arc handle is on the right (hr); straight handle on left deferred.
            emitted.append(_Knot(co=result.t1, hl=None, hr=result.handle_t1))
            # T2: arc handle is on the left (hl); straight handle on right deferred.
            emitted.append(_Knot(co=result.t2, hl=result.handle_t2, hr=None))

    return emitted


def _resolve_straight_handles(records, cyclic):
    """
    Return a new list of fully-populated _Knots with no None handles.

    Each None handle from _corner_knots is filled by pointing it 1/3 of the
    way toward the adjacent emitted knot.  Pre-set arc-side handles are kept
    unchanged.  Open-curve endpoints (both handles None) point both handles
    toward the single inward neighbor.
    """
    m = len(records)
    if m == 0:
        return []

    out = []
    for j, rec in enumerate(records):
        if cyclic:
            prev_co = records[(j - 1) % m].co
            nxt_co  = records[(j + 1) % m].co
        else:
            # Endpoints only have one real neighbor; point both handles that way.
            prev_co = records[max(j - 1, 0)].co
            nxt_co  = records[min(j + 1, m - 1)].co

        hl = rec.hl if rec.hl is not None else _add(rec.co, _scale(_sub(prev_co, rec.co), 1.0/3.0))
        hr = rec.hr if rec.hr is not None else _add(rec.co, _scale(_sub(nxt_co,  rec.co), 1.0/3.0))
        out.append(_Knot(co=rec.co, hl=hl, hr=hr))

    return out


def _merge_coincident_knots(knots, cyclic, eps=1e-7):
    """
    Collapse adjacent knots whose positions coincide within *eps* into one.

    When the fillet radius is large the per-corner setback clamps so that the
    T2 of one corner and the T1 of the next both land on the shared edge's
    midpoint — producing two knots at the same position (a zero-length segment)
    each with a collapsed straight-side handle. Merging removes the duplicate:
    the surviving knot keeps the FIRST knot's incoming handle (hl) and the
    SECOND knot's outgoing handle (hr), so the curve stays smooth through the
    meeting point.

    Per-spline, order-preserving. For a cyclic spline the last and first knot
    may also coincide; that pair is merged too (the survivor is written in the
    first knot's slot). Returns a new list with no two consecutive (and, when
    cyclic, no wrap-around) duplicate positions; for fewer than 2 knots, or
    when no positions coincide, the input is returned unchanged in content.
    """
    m = len(knots)
    if m < 2:
        return list(knots)

    def _coincident(a, b):
        return ((a[0]-b[0])**2 + (a[1]-b[1])**2 + (a[2]-b[2])**2) <= eps * eps

    # Forward pass: fold each knot into the previous one when coincident,
    # keeping the running knot's hl and adopting the incoming knot's hr.
    merged = []
    for rec in knots:
        if merged and _coincident(merged[-1].co, rec.co):
            prev = merged[-1]
            merged[-1] = _Knot(co=prev.co, hl=prev.hl, hr=rec.hr)
        else:
            merged.append(rec)

    # Cyclic wrap: if first and last now coincide, fold the last into the
    # first (the first keeps the last knot's incoming handle).
    if cyclic and len(merged) >= 2 and _coincident(merged[0].co, merged[-1].co):
        last = merged.pop()
        first = merged[0]
        merged[0] = _Knot(co=first.co, hl=last.hl, hr=first.hr)

    return merged


def _write_bezier_spline(curve, spline_data):
    """
    Create one BEZIER spline in *curve* from a _SplineData record.

    Contract: spline_data.knots must be fully-populated (no None handles).
    Returns the new spline, or None when knots is empty.
    """
    knots = spline_data.knots
    if not knots:
        return None

    sp = curve.splines.new('BEZIER')
    sp.use_cyclic_u = spline_data.cyclic
    sp.bezier_points.add(len(knots) - 1)

    for k, rec in enumerate(knots):
        bp = sp.bezier_points[k]
        bp.co                = rec.co
        bp.handle_left       = rec.hl
        bp.handle_right      = rec.hr
        bp.handle_left_type  = 'FREE'
        bp.handle_right_type = 'FREE'

    return sp


# ---------------------------------------------------------------------------
# Operator
# ---------------------------------------------------------------------------

class CURVE_OT_bevel_bezier_corners(bpy.types.Operator):
    bl_idname = "curve.bevel_bezier_corners"
    bl_label = "Bevel Bezier Corners"
    bl_description = "Replace sharp curve corners with circular-arc bezier fillets"
    bl_options = {'REGISTER', 'UNDO'}

    radius: FloatProperty(
        name="Radius",
        description="Fillet radius",
        min=0.0001,
        default=0.4,
        unit='LENGTH',
    )

    @classmethod
    def poll(cls, context):
        obj = context.active_object
        # Require OBJECT mode: execute() rebuilds curve.splines via the data
        # API, which is unsafe / not reflected while the object is in Edit Mode.
        return obj is not None and obj.type == 'CURVE' and obj.mode == 'OBJECT'

    def execute(self, context):
        obj = context.active_object
        curve = obj.data

        # Build phase: gather geometry per spline.
        new_splines = []
        for spline in curve.splines:
            cyclic = spline.use_cyclic_u

            if spline.type == 'BEZIER':
                knots = [_v3(bp.co) for bp in spline.bezier_points]
            else:
                # POLY/NURBS: point.co is (x,y,z,w) — take xyz.
                knots = [_v3(p.co) for p in spline.points]

            records  = _corner_knots(knots, cyclic, self.radius)
            resolved = _resolve_straight_handles(records, cyclic)
            merged   = _merge_coincident_knots(resolved, cyclic)
            new_splines.append(_SplineData(cyclic=cyclic, knots=merged))

        # Write phase: rebuild from scratch.
        for sp in list(curve.splines):
            curve.splines.remove(sp)

        total_pts = 0
        for sd in new_splines:
            sp = _write_bezier_spline(curve, sd)
            if sp is not None:
                total_pts += len(sp.bezier_points)

        self.report(
            {'INFO'},
            f"Bevel Bezier Corners: {total_pts} bezier points across {len(new_splines)} spline(s)",
        )
        return {'FINISHED'}


# ---------------------------------------------------------------------------
# Panel
# ---------------------------------------------------------------------------

class CURVE_PT_bevel_bezier_corners(bpy.types.Panel):
    bl_label = "Bevel Corners"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "Edit"

    @classmethod
    def poll(cls, context):
        obj = context.active_object
        # Grey out the button in Edit Mode — the operator rebuilds splines via
        # the data API and must only run in OBJECT mode.
        return obj is not None and obj.type == 'CURVE' and obj.mode == 'OBJECT'

    def draw(self, context):
        layout = self.layout
        col = layout.column()
        col.label(text="Fillet radius:")
        op = col.operator("curve.bevel_bezier_corners", text="Bevel Bezier Corners")
        op.radius = 0.4


# ---------------------------------------------------------------------------
# Register
# ---------------------------------------------------------------------------

_CLASSES = (
    CURVE_OT_bevel_bezier_corners,
    CURVE_PT_bevel_bezier_corners,
)


def register():
    for cls in _CLASSES:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(_CLASSES):
        bpy.utils.unregister_class(cls)


if __name__ == "__main__":
    register()
