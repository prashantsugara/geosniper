from pathlib import Path
import bpy
from mathutils import Vector, Matrix
root=Path(__file__).resolve().parents[1]
out=root/'Temp/army-review'; out.mkdir(parents=True,exist_ok=True)
for number in [1,2]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(root/f'Assets/3d/enemy/army_character_{number}.glb'))
    bpy.context.view_layer.update()
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    low=Vector([min(p[i] for p in points) for i in range(3)])
    high=Vector([max(p[i] for p in points) for i in range(3)])
    normal=Matrix.Scale(1.85/(high.z-low.z),4)@Matrix.Translation(Vector((-(low.x+high.x)/2,-(low.y+high.y)/2,-low.z)))
    for o in meshes:
        matrix=normal@o.matrix_world.copy(); o.parent=None; o.matrix_world=Matrix.Identity(4); o.data.transform(matrix)
    scene=bpy.context.scene; scene.render.engine='BLENDER_EEVEE'; scene.render.resolution_x=600; scene.render.resolution_y=600; scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('Review world'); scene.world.color=(.3,.3,.3)
    bpy.ops.object.light_add(type='AREA',location=(3,-4,5)); bpy.context.object.data.energy=650; bpy.context.object.data.shape='DISK'; bpy.context.object.data.size=5
    bpy.ops.object.camera_add(); scene.camera=bpy.context.object
    for label,pos in [('front',(0,-4,1.1)),('side',(4,0,1.1))]:
        scene.camera.location=pos; scene.camera.rotation_euler=(Vector((0,0,.95))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(out/f'{number}-{label}.png'); bpy.ops.render.render(write_still=True)
