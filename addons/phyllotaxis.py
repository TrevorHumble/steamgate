"""
phyllotaxis.py — Blender 5.1 add-on.

Places N points in the golden-angle (sunflower-seed) phyllotaxis pattern.
Point i sits at angle i × golden_angle and radius scale × sqrt(i).
An optional dome parameter lifts points into a paraboloid falloff.
"""

import math
import bpy
from bpy.props import IntProperty, FloatProperty

bl_info = {
    "name": "Phyllotaxis",
    "author": "blender-orchestrator",
    "version": (1, 0, 0),
    "blender": (5, 1, 0),
    "location": "View3D > Add > Mesh > Phyllotaxis",
    "description": "Generate a golden-angle phyllotaxis point cloud mesh",
    "category": "Add Mesh",
}


# ---------------------------------------------------------------------------
# Pure geometry — no bpy; unit-testable outside Blender.
# ---------------------------------------------------------------------------

def phyllotaxis_points(count, scale, dome=0.0):
    """
    Return a list of (x, y, z) tuples arranged in phyllotaxis order.

    Parameters
    ----------
    count : int
        Number of points to generate. Returns [] when count == 0.
    scale : float
        Radial scale factor. Point i sits at radius scale * sqrt(i).
    dome : float
        When non-zero, z is a paraboloid falloff: dome * (1 - (r/r_max)**2).
        When zero, all z == 0.

    Notes
    -----
    The golden angle is derived from the golden ratio phi = (1 + math.sqrt(5)) / 2:
        ga = math.radians(360.0 * (2.0 - phi))
    This guarantees no pair of consecutive points aligns, producing the
    interleaved spirals seen in sunflower seeds and pinecone scales.
    """
    if count == 0:
        return []

    ga = math.radians(360.0 * (2.0 - (1.0 + math.sqrt(5.0)) / 2.0))

    # Radius of the outermost point — used for dome normalisation.
    # Guard count <= 1: when count is 1 the only point is at r=0, so z=0.
    r_max = scale * math.sqrt(count - 1) if count > 1 else 1.0

    points = []
    for i in range(count):
        theta = i * ga
        r = scale * math.sqrt(i)
        x = r * math.cos(theta)
        y = r * math.sin(theta)
        if dome == 0.0 or count <= 1:
            z = 0.0
        else:
            z = dome * (1.0 - (r / r_max) ** 2)
        points.append((x, y, z))

    return points


# ---------------------------------------------------------------------------
# Operator
# ---------------------------------------------------------------------------

class MESH_OT_add_phyllotaxis(bpy.types.Operator):
    bl_idname = "mesh.add_phyllotaxis"
    bl_label = "Phyllotaxis"
    bl_description = "Add a golden-angle phyllotaxis point mesh"
    bl_options = {'REGISTER', 'UNDO'}

    count: IntProperty(
        name="Count",
        description="Number of points",
        min=1,
        default=300,
    )

    scale: FloatProperty(
        name="Scale",
        description="Radial scale factor",
        min=0.0001,
        default=0.1,
    )

    dome: FloatProperty(
        name="Dome",
        description="Dome height; 0 keeps the pattern flat",
        default=0.0,
    )

    def execute(self, context):
        points = phyllotaxis_points(self.count, self.scale, self.dome)

        mesh = bpy.data.meshes.new("Phyllotaxis")
        mesh.from_pydata(points, [], [])
        mesh.update()

        obj = bpy.data.objects.new("Phyllotaxis", mesh)
        context.collection.objects.link(obj)

        # Deselect all, then select and activate the new object.
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        context.view_layer.objects.active = obj

        self.report(
            {'INFO'},
            f"Phyllotaxis: {len(points)} points added",
        )
        return {'FINISHED'}


# ---------------------------------------------------------------------------
# Menu entry
# ---------------------------------------------------------------------------

def menu_func(self, context):
    self.layout.operator(
        "mesh.add_phyllotaxis",
        text="Phyllotaxis",
        icon='MESH_CIRCLE',
    )


# ---------------------------------------------------------------------------
# Register
# ---------------------------------------------------------------------------

_CLASSES = (MESH_OT_add_phyllotaxis,)


def register():
    for cls in _CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.VIEW3D_MT_mesh_add.append(menu_func)


def unregister():
    bpy.types.VIEW3D_MT_mesh_add.remove(menu_func)
    for cls in reversed(_CLASSES):
        bpy.utils.unregister_class(cls)


if __name__ == "__main__":
    register()
