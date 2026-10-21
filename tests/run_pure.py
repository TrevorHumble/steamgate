"""
run_pure.py — bpy-free geometry test for bevel_bezier_corners.

Stubs bpy into sys.modules so the add-on's top-level imports succeed,
then execs the module and extracts rounded_corner for direct assertion.
Run with plain Python (no Blender required).

Every expected value is re-derived here from theta / radius / edge-directions
(including the 4/3 cubic-bezier arc constant) so the checks are an independent
spec, not a copy of the add-on's internals. The decisive check evaluates the
fillet's cubic-bezier midpoint and asserts it lies on the circular arc — this
is what separates a real arc from a flat chamfer (zero handles).
"""

import sys, types, math, os

# Stub bpy so the add-on's top-level bpy import and class definitions work
# without a real Blender runtime.
stub = types.ModuleType('bpy')
stub.types = types.SimpleNamespace(Operator=object, Panel=object)
props = types.ModuleType('bpy.props')
props.FloatProperty = lambda **k: None
stub.props = props
stub.utils = types.SimpleNamespace(register_class=lambda c: None, unregister_class=lambda c: None)
sys.modules['bpy'] = stub
sys.modules['bpy.props'] = props

ns = {}
addon = os.environ.get('BEVEL_ADDON_PATH') or os.path.join(os.path.dirname(__file__), '..', 'addons', 'bevel_bezier_corners.py')
exec(open(addon, encoding='utf-8').read(), ns)
rounded_corner = ns['rounded_corner']


# --- pure tuple helpers, re-derived here (do not import add-on internals) ---

def _sub(a, b):
    return (a[0]-b[0], a[1]-b[1], a[2]-b[2])

def _add(a, b):
    return (a[0]+b[0], a[1]+b[1], a[2]+b[2])

def _scale(v, s):
    return (v[0]*s, v[1]*s, v[2]*s)

def _norm(v):
    n = math.sqrt(v[0]*v[0] + v[1]*v[1] + v[2]*v[2])
    return (v[0]/n, v[1]/n, v[2]/n)

def _dist(a, b):
    return math.sqrt(sum((a[i]-b[i])**2 for i in range(3)))


def _expected_fillet(prev, corner, nxt, radius):
    """Independently re-derive the spec fillet geometry for one corner.

    Returns (t1, t2, handle_t1, handle_t2, h, r_eff, center, theta).
    No add-on code is used — this IS the specification the add-on must meet.
    Assumes no setback clamping (caller picks edges long enough).
    """
    a = _norm(_sub(prev, corner))          # corner -> prev
    b = _norm(_sub(nxt, corner))           # corner -> nxt
    theta = math.acos(max(-1.0, min(1.0, sum(a[i]*b[i] for i in range(3)))))
    d = radius / math.tan(theta / 2.0)     # setback along each edge
    r_eff = d * math.tan(theta / 2.0)
    t1 = _add(corner, _scale(a, d))
    t2 = _add(corner, _scale(b, d))
    # Standard cubic-bezier circular-arc handle constant, re-derived here.
    alpha = math.pi - theta
    h = (4.0/3.0) * math.tan(alpha / 4.0) * r_eff
    handle_t1 = _sub(t1, _scale(a, h))     # back toward corner along a
    handle_t2 = _sub(t2, _scale(b, h))     # back toward corner along b
    # Arc center: along the interior bisector at r_eff / sin(theta/2).
    bis = _norm(_add(a, b))
    center = _add(corner, _scale(bis, r_eff / math.sin(theta / 2.0)))
    return t1, t2, handle_t1, handle_t2, h, r_eff, center, theta


def _check_handles_and_arc(label, r, prev, corner, nxt, radius,
                           handle_tol=1e-3, arc_tol=2e-3):
    """Assert handle position/length/direction and the circular-arc property."""
    a = _norm(_sub(prev, corner))
    b = _norm(_sub(nxt, corner))
    (et1, et2, eh1, eh2, h, r_eff, center, theta) = _expected_fillet(
        prev, corner, nxt, radius)

    # Handle positions match the independent derivation.
    if _dist(r.handle_t1, eh1) > handle_tol:
        print(f"FAIL [{label}]: handle_t1 expected {eh1}, got {tuple(r.handle_t1)}")
        sys.exit(1)
    if _dist(r.handle_t2, eh2) > handle_tol:
        print(f"FAIL [{label}]: handle_t2 expected {eh2}, got {tuple(r.handle_t2)}")
        sys.exit(1)

    # Handle LENGTH equals the 4/3-arc length h (fails if h=0 chamfer or wrong constant).
    len1 = _dist(r.t1, r.handle_t1)
    len2 = _dist(r.t2, r.handle_t2)
    if abs(len1 - h) > handle_tol:
        print(f"FAIL [{label}]: |t1-handle_t1| expected {h}, got {len1}")
        sys.exit(1)
    if abs(len2 - h) > handle_tol:
        print(f"FAIL [{label}]: |t2-handle_t2| expected {h}, got {len2}")
        sys.exit(1)

    # Handle DIRECTION: (t1 - handle_t1) parallel to a, (t2 - handle_t2) parallel to b.
    # The handle must point back along the incoming edge (cross ~ 0, dot > 0).
    dir1 = _norm(_sub(r.t1, r.handle_t1))
    dir2 = _norm(_sub(r.t2, r.handle_t2))
    cross1 = (dir1[1]*a[2]-dir1[2]*a[1], dir1[2]*a[0]-dir1[0]*a[2], dir1[0]*a[1]-dir1[1]*a[0])
    cross2 = (dir2[1]*b[2]-dir2[2]*b[1], dir2[2]*b[0]-dir2[0]*b[2], dir2[0]*b[1]-dir2[1]*b[0])
    if math.sqrt(sum(c*c for c in cross1)) > handle_tol or sum(dir1[i]*a[i] for i in range(3)) < 0.5:
        print(f"FAIL [{label}]: t1 handle direction not along incoming edge a={a}, got {dir1}")
        sys.exit(1)
    if math.sqrt(sum(c*c for c in cross2)) > handle_tol or sum(dir2[i]*b[i] for i in range(3)) < 0.5:
        print(f"FAIL [{label}]: t2 handle direction not along incoming edge b={b}, got {dir2}")
        sys.exit(1)

    # Circular-arc property: cubic-bezier midpoint must lie on the arc (dist == r_eff).
    # B(0.5) = (t1 + 3*handle_t1 + 3*handle_t2 + t2) / 8.
    mid = tuple(
        (r.t1[i] + 3*r.handle_t1[i] + 3*r.handle_t2[i] + r.t2[i]) / 8.0
        for i in range(3)
    )
    mid_dist = _dist(mid, center)
    if abs(mid_dist - r_eff) > arc_tol:
        print(f"FAIL [{label}]: bezier midpoint distance from arc center "
              f"expected {r_eff}, got {mid_dist} (chamfer would give ~{r_eff*math.cos(theta/2.0)})")
        sys.exit(1)


# ===========================================================================
# Case 1 — 90-degree corner.
# prev=(-1,-1,0), corner=(1,-1,0), nxt=(1,1,0), radius=0.4
# theta=90, d=0.4, r_eff=0.4, a=(-1,0,0), b=(0,1,0).
# Expected tangent points: t1=(0.6,-1.0,0.0), t2=(1.0,-0.6,0.0).
# Arc center=(0.6,-0.6,0.0), arc radius=0.4.
# ===========================================================================
P1, C1, N1, R1 = (-1, -1, 0), (1, -1, 0), (1, 1, 0), 0.4
r = rounded_corner(P1, C1, N1, R1)

if r is None:
    print("FAIL: rounded_corner returned None for a 90-degree corner")
    sys.exit(1)

got_t1 = tuple(round(c, 3) for c in r.t1)
got_t2 = tuple(round(c, 3) for c in r.t2)
expected_t1 = (0.6, -1.0, 0.0)
expected_t2 = (1.0, -0.6, 0.0)

if got_t1 != expected_t1:
    print(f"FAIL: t1 expected {expected_t1}, got {got_t1}")
    sys.exit(1)

if got_t2 != expected_t2:
    print(f"FAIL: t2 expected {expected_t2}, got {got_t2}")
    sys.exit(1)

_check_handles_and_arc("90deg", r, P1, C1, N1, R1)


# ===========================================================================
# Case 2 — 60-degree (acute) corner. Exercises the d = radius/tan(theta/2)
# setback at a non-right, non-symmetric-in-coordinates angle.
#
# Derivation: place corner at the origin. Let the edge toward prev run along
# +x (a = (1,0,0)) and the edge toward nxt run 60 deg off it
# (b = (cos60, sin60, 0) = (0.5, 0.8660254, 0)). The interior angle between
# the two edges is therefore exactly theta = 60 deg.
# Pick edge length L=2 so prev=(2,0,0), nxt=(1, sqrt3, 0) ~= (1, 1.7320508, 0);
# half-edges = 1.0, comfortably larger than the setback d, so NO clamping.
# radius = 0.5  ->  d = 0.5 / tan(30) = 0.8660254,  r_eff = 0.5.
# t1 = corner + a*d = (0.8660254, 0, 0).
# t2 = corner + b*d = (0.4330127, 0.75, 0).
# Arc center = corner + bisector * (r_eff/sin30) = (0.8660254, 0.5, 0).
# ===========================================================================
P2 = (2.0, 0.0, 0.0)
C2 = (0.0, 0.0, 0.0)
N2 = (1.0, math.sqrt(3.0), 0.0)
R2 = 0.5
r2 = rounded_corner(P2, C2, N2, R2)

if r2 is None:
    print("FAIL: rounded_corner returned None for a 60-degree corner")
    sys.exit(1)

got2_t1 = tuple(round(c, 4) for c in r2.t1)
got2_t2 = tuple(round(c, 4) for c in r2.t2)
expected2_t1 = (0.8660, 0.0, 0.0)
expected2_t2 = (0.4330, 0.75, 0.0)

if got2_t1 != expected2_t1:
    print(f"FAIL [60deg]: t1 expected {expected2_t1}, got {got2_t1}")
    sys.exit(1)

if got2_t2 != expected2_t2:
    print(f"FAIL [60deg]: t2 expected {expected2_t2}, got {got2_t2}")
    sys.exit(1)

_check_handles_and_arc("60deg", r2, P2, C2, N2, R2)


# ===========================================================================
# Case 3 — degenerate zero-length edge (duplicate adjacent vertex).
# prev == corner makes edge a zero-length; rounded_corner must return None
# rather than emit a bogus pi/2 fillet.
# ===========================================================================
if rounded_corner((1, -1, 0), (1, -1, 0), (1, 1, 0), 0.4) is not None:
    print("FAIL [degenerate]: prev==corner should return None")
    sys.exit(1)


print("PURE_PASS")
sys.exit(0)
