import bpy,math,json
from mathutils import Vector,Matrix
from pathlib import Path
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
scene=bpy.data.scenes.new('Stronghold asset review')
scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.resolution_x=960;scene.render.resolution_y=720;scene.render.resolution_percentage=100
world=bpy.data.worlds.new('Stronghold review daylight');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.42,.48,.57,1);world.node_tree.nodes['Background'].inputs[1].default_value=.6;scene.world=world
camera_data=bpy.data.cameras.new('Stronghold asset camera');camera=bpy.data.objects.new('Stronghold asset camera',camera_data);scene.collection.objects.link(camera);scene.camera=camera
camera_data.type='ORTHO';camera_data.ortho_scale=27
camera.location=(22,-28,20);camera.rotation_euler=(Vector((0,0,5))-camera.location).to_track_quat('-Z','Y').to_euler()
sun_data=bpy.data.lights.new('Stronghold daylight','SUN');sun_data.energy=3
sun=bpy.data.objects.new('Stronghold daylight',sun_data);scene.collection.objects.link(sun);sun.rotation_euler=(.5,-.5,-.4)
for index in [1,3,7,14]:
 source=bpy.data.objects[f'House_0_{index}_Arab_Build_0'];obj=source.copy();obj.data=source.data.copy();obj.parent=None
 obj.data.transform(source.matrix_world);obj.matrix_world=Matrix.Identity(4);scene.collection.objects.link(obj)
 points=[v.co for v in obj.data.vertices];lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)])
 factor=10/(hi.z-lo.z);obj.data.transform(Matrix.Diagonal((factor,factor,factor,1))@Matrix.Translation(Vector((-(lo.x+hi.x)/2,-(lo.y+hi.y)/2,-lo.z))))
 scene.render.filepath=str(root/f'output/reference-building/arab-{index}.png');bpy.ops.render.render(write_still=True,scene=scene.name)
 bpy.data.objects.remove(obj,do_unlink=True)
print('Rendered four authored Arab-building candidates')
