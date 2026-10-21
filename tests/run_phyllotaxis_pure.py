"""
run_phyllotaxis_pure.py — bpy-free correctness test for phyllotaxis.

Stubs bpy into sys.modules so the add-on's top-level imports succeed,
then execs the module and extracts phyllotaxis_points for direct assertion.
Run with plain Python (no Blender required).
"""

import sys, types, math, os

# Stub bpy so the add-on's top-level bpy import and class definitions work
# without a real Blender runtime.
stub = types.ModuleType('bpy')
stub.types = types.SimpleNamespace(Operator=object, Panel=object)
props = types.ModuleType('bpy.props')
props.IntProperty = lambda **k: None
props.FloatProperty = lambda **k: None
stub.props = props
stub.utils = types.SimpleNamespace(register_class=lambda c: None, unregister_class=lambda c: None)
sys.modules['bpy'] = stub
sys.modules['bpy.props'] = props

ns = {}
addon = os.environ.get('PHYLLO_ADDON_PATH') or os.path.join(os.path.dirname(__file__), '..', 'addons', 'phyllotaxis.py')
exec(open(addon, encoding='utf-8').read(), ns)
phyllotaxis_points = ns['phyllotaxis_points']

TWO_PI = 2.0 * math.pi
GA = math.radians(360.0 * (2.0 - (1.0 + math.sqrt(5.0)) / 2.0))

# 1. Golden-angle invariant: consecutive polar angles advance by GA mod 2pi.
pts = phyllotaxis_points(50, 0.1)
expected = GA % TWO_PI
for i in (1, 5, 12, 23, 40):
    xa, ya, _ = pts[i]
    xb, yb, _ = pts[i + 1]
    got = (math.atan2(yb, xb) - math.atan2(ya, xa)) % TWO_PI
    if abs(got - expected) > 1e-6:
        print(f"FAIL: golden-angle step at i={i} expected {expected}, got {got}")
        sys.exit(1)

# 2. Radius law: hypot(x, y) == scale * sqrt(i).
for scale in (0.1, 1.0):
    pts = phyllotaxis_points(50, scale)
    for i in (1, 7, 19, 33, 49):
        x, y, _ = pts[i]
        got = math.hypot(x, y)
        want = scale * math.sqrt(i)
        if abs(got - want) > 1e-9:
            print(f"FAIL: radius at i={i} scale={scale} expected {want}, got {got}")
            sys.exit(1)

# 3. Count / edge cases.
if phyllotaxis_points(0, 0.1) != []:
    print(f"FAIL: count=0 expected [], got {phyllotaxis_points(0, 0.1)}")
    sys.exit(1)

if phyllotaxis_points(1, 0.1) != [(0.0, 0.0, 0.0)]:
    print(f"FAIL: count=1 expected [(0.0, 0.0, 0.0)], got {phyllotaxis_points(1, 0.1)}")
    sys.exit(1)

if len(phyllotaxis_points(300, 0.1)) != 300:
    print(f"FAIL: count=300 expected len 300, got {len(phyllotaxis_points(300, 0.1))}")
    sys.exit(1)

# 4. Dome: paraboloid falloff dome * (1 - (r/r_max)**2).
count, scale, dome = 50, 0.1, 2.0
pts = phyllotaxis_points(count, scale, dome)
r_max = scale * math.sqrt(count - 1)

z0 = pts[0][2]
if abs(z0 - dome) > 1e-9:
    print(f"FAIL: dome center z (i=0) expected {dome}, got {z0}")
    sys.exit(1)

z_out = pts[count - 1][2]
if abs(z_out - 0.0) > 1e-9:
    print(f"FAIL: dome outermost z (i={count - 1}) expected 0.0, got {z_out}")
    sys.exit(1)

i = 17
x, y, z = pts[i]
r = math.hypot(x, y)
want_z = dome * (1.0 - (r / r_max) ** 2)
if abs(z - want_z) > 1e-9:
    print(f"FAIL: dome interior z at i={i} expected {want_z}, got {z}")
    sys.exit(1)

# dome=0.0: all z == 0.0.
pts = phyllotaxis_points(count, scale, 0.0)
for i, (_, _, z) in enumerate(pts):
    if z != 0.0:
        print(f"FAIL: dome=0 z at i={i} expected 0.0, got {z}")
        sys.exit(1)

print("PHYLLOTAXIS_PURE_PASS")
sys.exit(0)
