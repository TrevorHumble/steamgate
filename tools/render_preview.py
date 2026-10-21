"""
render_preview.py — headless visual-confirmation render for one add-on.

Run under:
    blender --background --python tools/render_preview.py -- <addon_name>

where <addon_name> is one of: bevel_bezier_corners | phyllotaxis

It registers the named add-on (same runpy pattern as tests/run_headless.py),
builds a representative example of that add-on's output in a clean scene,
auto-frames a top-down orthographic camera over the geometry, adds light and a
world so the image is not black, and renders an 800x800 PNG to:
    renders/<addon_name>.png

On success it prints  RENDER_OK <path>  and exits 0.
On any failure it prints  RENDER_FAIL: <reason>  and exits 1.

This is the HUMAN visual gate — it lets the owner (who cannot read code) SEE what an
add-on produces and confirm it is what they meant. It is distinct from the
deterministic geometry gates (run_headless.py, evals): those prove the math is
correct; this proves the result is what was intended.

Engine note (confirmed via blender-docs RAG, Blender 5.0 release notes):
EEVEE's render engine identifier was changed from 'BLENDER_EEVEE_NEXT' to
'BLENDER_EEVEE'. So on Blender 5.x the correct id is 'BLENDER_EEVEE' — the
old 'BLENDER_EEVEE_NEXT' no longer exists. EEVEE is used because it is fast and
needs no GPU/path-tracer in CI.
"""

import os
import sys
import math
import runpy

import bpy

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------

_HERE = os.path.dirname(os.path.abspath(__file__))
_REPO = os.path.normpath(os.path.join(_HERE, '..'))
_ADDONS = os.path.join(_REPO, 'addons')
_RENDERS = os.path.join(_REPO, 'renders')

# Confirmed via RAG (5.0 release notes): EEVEE id is 'BLENDER_EEVEE' in 5.x.
_EEVEE_ENGINE = 'BLENDER_EEVEE'

_RESOLUTION = 800  # square; these patterns are radially/2D symmetric.


def _fail(msg):
    """Print the standard failure line and exit non-zero."""
    print(f"RENDER_FAIL: {msg}")
    sys.exit(1)


# ---------------------------------------------------------------------------
# Add-on registration (runpy pattern, mirrors tests/run_headless.py)
# ---------------------------------------------------------------------------

def _register_addon(name):
    """Load addons/<name>.py and call its register(); return its globals dict."""
    path = os.path.join(_ADDONS, f"{name}.py")
    if not os.path.isfile(path):
        _fail(f"add-on file not found: {path}")
    addon_globals = runpy.run_path(path)
    register = addon_globals.get('register')
    if register is None:
        _fail(f"add-on {name} has no register()")
    register()
    return addon_globals


# ---------------------------------------------------------------------------
# Scene reset
# ---------------------------------------------------------------------------

def _clean_scene():
    """Remove every object so we frame only the add-on's output."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    # Purge orphan data so stale meshes/curves don't linger.
    for block in (bpy.data.meshes, bpy.data.curves, bpy.data.objects):
        for datum in list(block):
            if getattr(datum, 'users', 0) == 0:
                block.remove(datum)


# ---------------------------------------------------------------------------
# Per-add-on example builders — each returns the object to be framed/rendered.
# ---------------------------------------------------------------------------

def _build_bevel_example():
    """
    Build a cyclic POLY square, run curve.bevel_bezier_corners(radius=0.4)
    (same construction as tests/run_headless.py), then give the curve a small
    bevel_depth so the rounded corners render as a visible solid tube.
    """
    curve_data = bpy.data.curves.new(name='BevelSquare', type='CURVE')
    curve_data.dimensions = '3D'

    spline = curve_data.splines.new('POLY')
    spline.use_cyclic_u = True
    spline.points.add(3)  # POLY starts with 1 point; 4 corners total.
    corners = [(-1.0, -1.0, 0.0), (1.0, -1.0, 0.0),
               (1.0, 1.0, 0.0), (-1.0, 1.0, 0.0)]
    for i, (x, y, z) in enumerate(corners):
        spline.points[i].co = (x, y, z, 1.0)

    obj = bpy.data.objects.new('BevelSquare', curve_data)
    bpy.context.collection.objects.link(obj)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    result = bpy.ops.curve.bevel_bezier_corners(radius=0.4)
    if 'FINISHED' not in result:
        _fail(f"bevel operator returned {result}, expected FINISHED")

    # Make the curve render as a solid rounded tube so the fillets read clearly.
    curve_data.bevel_depth = 0.06
    curve_data.bevel_resolution = 6
    curve_data.fill_mode = 'FULL'

    _give_material(obj, (0.9, 0.55, 0.15))  # warm orange — reads on grey bg.
    return obj


def _build_phyllotaxis_example():
    """
    Run mesh.add_phyllotaxis(count=300, scale=0.1, dome=0.0), then instance a
    small sphere on every vertex so the point pattern (the sunflower spiral)
    is actually visible in the render — bare verts do not show in EEVEE.
    """
    result = bpy.ops.mesh.add_phyllotaxis(count=300, scale=0.1, dome=0.0)
    if 'FINISHED' not in result:
        _fail(f"phyllotaxis operator returned {result}, expected FINISHED")

    point_obj = bpy.context.view_layer.objects.active
    if point_obj is None:
        _fail("phyllotaxis produced no active object")

    # A tiny sphere used as the instanced dot at each vertex.
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.035, segments=12, ring_count=6)
    dot = bpy.context.view_layer.objects.active
    dot.name = 'PhylloDot'
    _give_material(dot, (0.15, 0.5, 0.9))  # blue dots.

    # Instance the dot on every vertex of the point cloud.
    dot.parent = point_obj
    point_obj.instance_type = 'VERTS'

    _give_material(point_obj, (0.15, 0.5, 0.9))
    return point_obj


def _give_material(obj, rgb):
    """Attach a simple flat-ish Principled material in the given color."""
    mat = bpy.data.materials.new(name=f"{obj.name}_mat")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    if bsdf is not None:
        bsdf.inputs['Base Color'].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        # Roughness up so it reads as a clean matte shape, not a shiny blob.
        if 'Roughness' in bsdf.inputs:
            bsdf.inputs['Roughness'].default_value = 0.6
    obj.data.materials.append(mat)


# ---------------------------------------------------------------------------
# Framing — bounding box of the rendered geometry, then fit an ortho camera.
# ---------------------------------------------------------------------------

def _world_bbox(obj):
    """
    Return (min_x, min_y, min_z, max_x, max_y, max_z) over the object's own
    world-space bounding box AND those of its children (the phyllotaxis dots
    are instanced from a child sphere, so the parent bbox alone is too small).
    """
    objs = [obj] + list(obj.children)
    xs, ys, zs = [], [], []
    for o in objs:
        for corner in o.bound_box:
            wc = o.matrix_world @ _as_vec(corner)
            xs.append(wc.x)
            ys.append(wc.y)
            zs.append(wc.z)
    if not xs:
        _fail("could not compute bounding box (no geometry)")
    return (min(xs), min(ys), min(zs), max(xs), max(ys), max(zs))


def _as_vec(corner):
    """bound_box corners come back as plain float triples; wrap as Vector."""
    from mathutils import Vector
    return Vector((corner[0], corner[1], corner[2]))


def _add_top_down_ortho_camera(bbox):
    """
    Place a top-down orthographic camera that fits the bbox with a margin.

    Top-down ortho is the sensible default for these flat (2D-ish) radial
    patterns: it shows the spiral / the rounded square without perspective
    distortion. ortho_scale is set to the larger XY span * margin so the whole
    shape fits the square frame (confirmed via RAG: Camera.ortho_scale controls
    orthographic framing, default 6.0).
    """
    min_x, min_y, min_z, max_x, max_y, max_z = bbox
    cx = 0.5 * (min_x + max_x)
    cy = 0.5 * (min_y + max_y)
    span = max(max_x - min_x, max_y - min_y)
    if span <= 0:
        span = 1.0
    margin = 1.25  # 25% padding around the geometry.

    cam_data = bpy.data.cameras.new('PreviewCam')
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = span * margin

    cam_obj = bpy.data.objects.new('PreviewCam', cam_data)
    # Look straight down from above the top of the geometry.
    cam_obj.location = (cx, cy, max_z + span + 5.0)
    cam_obj.rotation_euler = (0.0, 0.0, 0.0)  # default cam looks down -Z.
    bpy.context.collection.objects.link(cam_obj)
    bpy.context.scene.camera = cam_obj
    return cam_obj


def _add_lighting_and_world():
    """A sun light plus a mid-grey world so nothing renders black."""
    world = bpy.data.worlds.new('PreviewWorld')
    world.use_nodes = True
    bg = world.node_tree.nodes.get('Background')
    if bg is not None:
        bg.inputs['Color'].default_value = (0.18, 0.18, 0.2, 1.0)
        bg.inputs['Strength'].default_value = 1.0
    bpy.context.scene.world = world

    sun_data = bpy.data.lights.new('PreviewSun', type='SUN')
    sun_data.energy = 4.0
    sun_obj = bpy.data.objects.new('PreviewSun', sun_data)
    sun_obj.location = (3.0, -2.0, 8.0)
    # Tilt slightly so the tube/dots get some shading relief.
    sun_obj.rotation_euler = (math.radians(25.0), math.radians(15.0), 0.0)
    bpy.context.collection.objects.link(sun_obj)


# ---------------------------------------------------------------------------
# Render
# ---------------------------------------------------------------------------

def _render_to(path):
    scene = bpy.context.scene
    try:
        scene.render.engine = _EEVEE_ENGINE
    except (TypeError, ValueError) as exc:
        _fail(f"could not set engine {_EEVEE_ENGINE}: {exc}")

    scene.render.resolution_x = _RESOLUTION
    scene.render.resolution_y = _RESOLUTION
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = path

    bpy.ops.render.render(write_still=True)

    if not os.path.isfile(path):
        _fail(f"render reported success but no file at {path}")


# ---------------------------------------------------------------------------
# Argument parsing — everything after '--' on the Blender command line.
# ---------------------------------------------------------------------------

_BUILDERS = {
    'bevel_bezier_corners': _build_bevel_example,
    'phyllotaxis': _build_phyllotaxis_example,
}


def _addon_arg():
    argv = sys.argv
    if '--' not in argv:
        _fail("no add-on name given (expected: ... -- <addon_name>)")
    after = argv[argv.index('--') + 1:]
    if not after:
        _fail("no add-on name after '--'")
    name = after[0]
    if name not in _BUILDERS:
        _fail(f"unknown add-on '{name}'; expected one of {sorted(_BUILDERS)}")
    return name


def main():
    name = _addon_arg()

    _register_addon(name)
    _clean_scene()

    obj = _BUILDERS[name]()

    bbox = _world_bbox(obj)
    _add_top_down_ortho_camera(bbox)
    _add_lighting_and_world()

    os.makedirs(_RENDERS, exist_ok=True)
    out_path = os.path.join(_RENDERS, f"{name}.png")
    _render_to(out_path)

    print(f"RENDER_OK {out_path}")
    sys.exit(0)


if __name__ == "__main__":
    main()
