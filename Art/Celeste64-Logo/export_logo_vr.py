"""Export the artist's VR title logo without changing the source .blend.

Run with Blender 5: blender --background --factory-startup Celeste64-Title-GameVR.blend --python export_logo_vr.py
"""
from pathlib import Path
import bpy

repo = Path(__file__).resolve().parents[2]
out = repo / "Content" / "Models" / "logo_vr.glb"
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and not obj.hide_render]
if not meshes:
    raise RuntimeError("No visible logo meshes found")

# Applying a mirrored scale reverses the mesh winding. Correct it after the
# transform so the game's face culling sees the same textured fronts as Blender.
mirrored = [obj for obj in meshes if obj.matrix_world.to_3x3().determinant() < 0]
bpy.ops.object.select_all(action="DESELECT")
for obj in meshes:
    if not obj.data.uv_layers or not obj.data.materials:
        raise RuntimeError(f"{obj.name} is missing UVs or material")
    obj.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
for obj in mirrored:
    obj.data.flip_normals()
    obj.data.update()

bpy.ops.export_scene.gltf(filepath=str(out), export_format="GLB", use_selection=True,
                          export_apply=True, export_animations=False)
print("Exported VR logo:", out)
print("Corrected mirrored meshes:", [obj.name for obj in mirrored])
