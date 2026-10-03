from pathlib import Path
import bpy
from mathutils import Vector
root=Path(__file__).resolve().parents[1]
for number in [1,2]:
    bpy.ops.wm.open_mainfile(filepath=str(root/f'Tools/generated/army_character_{number}_rigged.blend'))
    scene=bpy.context.scene; scene.render.engine='BLENDER_EEVEE'
    scene.render.resolution_x=640; scene.render.resolution_y=640; scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('Review'); scene.world.color=(.3,.3,.3)
    bpy.ops.object.light_add(type='AREA',location=(2,-4,4)); bpy.context.object.data.energy=650; bpy.context.object.data.size=5
    bpy.ops.object.camera_add(location=(2,-4,1.4)); scene.camera=bpy.context.object
    scene.camera.rotation_euler=(Vector((0,0,.95))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    rig=bpy.data.objects['ArmyRig']
    for label,frame in [('Idle',1),('Walk',8)]:
        rig.animation_data.action=bpy.data.actions[label]; scene.frame_set(frame)
        scene.render.filepath=str(root/f'Temp/army-review/{number}-{label}.png'); bpy.ops.render.render(write_still=True)
