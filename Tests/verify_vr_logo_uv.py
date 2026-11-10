"""Run in Blender with the artist's .blend open; pass the exported .glb after --."""
import bpy
import sys


def front_points(obj):
    uv = obj.data.uv_layers.active.data
    normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
    result = set()
    for poly in obj.data.polygons:
        if (normal_matrix @ poly.normal).normalized().y > -.9:
            continue
        for loop in poly.loop_indices:
            vertex = obj.matrix_world @ obj.data.vertices[obj.data.loops[loop].vertex_index].co
            result.add(tuple(round(value, 4) for value in (*vertex, *uv[loop].uv)))
    return result


source = front_points(bpy.data.objects["Cube.003"])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[sys.argv.index("--") + 1])
exported = front_points(bpy.data.objects["Cube.003"])
if not source or source - exported:
    raise RuntimeError("VR lettering front-face positions, normals, or UVs do not match the Blender source")
print(f"VR logo front faces and UVs verified: {len(source)} source points")
