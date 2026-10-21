"""
run_phyllotaxis_headless.py — bpy operator test for add_phyllotaxis.

Run under: blender --background --python tests/run_phyllotaxis_headless.py

Runs the MESH_OT_add_phyllotaxis operator, finds the created "Phyllotaxis"
mesh object, then asserts its vertices ARE the pure phyllotaxis_points — both
against the pure function (index-by-index, order preserved by from_pydata) and
against INDEPENDENT geometric anchors (origin point, radius law) that hold even
if the pure function were swapped.
"""

import sys
import os
import math
import runpy

import bpy

ADDON_PATH = os.path.join(os.path.dirname(__file__), '..', 'addons', 'phyllotaxis.py')

# Load and register the add-on into the running Blender session. Reuse the same
# globals dict runpy returns so the already-tested pure function is the ground
# truth, exec'd once under real bpy.
addon_globals = runpy.run_path(ADDON_PATH)
addon_globals['register']()
phyllotaxis_points = addon_globals['phyllotaxis_points']

# Clear factory-startup default objects so the active object after the op is
# unambiguously ours.
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

COUNT = 50
SCALE = 0.1
DOME = 2.0

result = bpy.ops.mesh.add_phyllotaxis(count=COUNT, scale=SCALE, dome=DOME)

if 'FINISHED' not in result:
    print(f"FAIL: operator returned {result}, expected FINISHED")
    sys.exit(1)

# The operator names the object "Phyllotaxis" and leaves it active.
obj = bpy.context.view_layer.objects.active
if obj is None:
    print("FAIL: no active object after operator")
    sys.exit(1)

if obj.name != "Phyllotaxis":
    print(f"FAIL: active object name expected 'Phyllotaxis', got {obj.name!r}")
    sys.exit(1)

if obj.type != 'MESH':
    print(f"FAIL: active object type expected MESH, got {obj.type}")
    sys.exit(1)

verts = obj.data.vertices

# --- Vertex count -----------------------------------------------------------
if len(verts) != COUNT:
    print(f"FAIL: vertex count expected {COUNT}, got {len(verts)}")
    sys.exit(1)

# --- Match the pure function ------------------------------------------------
expected = phyllotaxis_points(COUNT, SCALE, DOME)
TOL = 1e-5


def _close(p, q, tol=TOL):
    return sum((p[i] - q[i]) ** 2 for i in range(3)) ** 0.5 <= tol


produced = [tuple(v.co) for v in verts]

# from_pydata preserves order, so index-by-index should hold. Fall back to
# order-independent set equality before failing — a mere ordering difference
# shouldn't red-fail CI, only a wrong position should.
ordered_ok = all(_close(produced[k], expected[k]) for k in range(COUNT))

if not ordered_ok:
    remaining = list(produced)
    set_ok = True
    for exp in expected:
        match = next((i for i, got in enumerate(remaining) if _close(got, exp)), None)
        if match is None:
            set_ok = False
            break
        remaining.pop(match)

    if not set_ok:
        for k in range(COUNT):
            if not _close(produced[k], expected[k]):
                print(f"FAIL: vertex {k} co expected {expected[k]}, got {produced[k]}")
        sys.exit(1)

# --- Independent anchors (don't just trust the pure function) ---------------
ANCHOR_TOL = 1e-6

# Point i=0 has r = scale*sqrt(0) = 0, so it sits on the Z axis: x=y=0.
v0 = produced[0]
if abs(v0[0]) > ANCHOR_TOL or abs(v0[1]) > ANCHOR_TOL:
    print(f"FAIL: vertex 0 expected x=y=0 (r=0 at i=0), got {v0}")
    sys.exit(1)

# Radius law: hypot(x, y) == scale*sqrt(i), independent of the pure function.
for i in (10, COUNT - 1):
    x, y, _ = produced[i]
    r = math.hypot(x, y)
    want = SCALE * math.sqrt(i)
    if abs(r - want) > ANCHOR_TOL:
        print(f"FAIL: radius at i={i} expected {want}, got {r}")
        sys.exit(1)

# Golden-angle law: re-derive GA IN-FILE (do NOT read it from the add-on), so a
# hard-coded 137.5 or a wrong constant in the operator output is caught against
# an independent reference rather than against the add-on's own value. Each
# consecutive polar-angle step must advance by GA mod 2pi.
TWO_PI = 2.0 * math.pi
GA = math.radians(360.0 * (2.0 - (1.0 + math.sqrt(5.0)) / 2.0))
ga_expected = GA % TWO_PI
for i in (1, 23):
    xa, ya, _ = produced[i]
    xb, yb, _ = produced[i + 1]
    step = (math.atan2(yb, xb) - math.atan2(ya, xa)) % TWO_PI
    if abs(step - ga_expected) > ANCHOR_TOL:
        print(f"FAIL: golden-angle step at i={i} expected {ga_expected}, got {step}")
        sys.exit(1)

# Dome law: re-derive the paraboloid falloff IN-FILE from the geometry (radius),
# NOT by calling phyllotaxis_points. With DOME=2.0: center (i=0, r=0) lifts to
# DOME, the outermost vertex falls to ~0, and an interior vertex matches
# DOME*(1 - (r/r_max)**2). Catches a wrong dome formula or a flipped falloff.
r_max = SCALE * math.sqrt(COUNT - 1)

z0 = produced[0][2]
if abs(z0 - DOME) > ANCHOR_TOL:
    print(f"FAIL: dome center z (i=0, r=0) expected {DOME}, got {z0}")
    sys.exit(1)

z_out = produced[COUNT - 1][2]
if abs(z_out - 0.0) > ANCHOR_TOL:
    print(f"FAIL: dome outermost z (i={COUNT - 1}) expected ~0.0, got {z_out}")
    sys.exit(1)

i = 17
x, y, z = produced[i]
want_z = DOME * (1.0 - (math.hypot(x, y) / r_max) ** 2)
if abs(z - want_z) > ANCHOR_TOL:
    print(f"FAIL: dome interior z at i={i} expected {want_z}, got {z}")
    sys.exit(1)

print("PHYLLOTAXIS_HEADLESS_PASS")
sys.exit(0)
