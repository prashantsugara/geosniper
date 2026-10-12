import bpy
from mathutils import Vector,Matrix
from pathlib import Path
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
scene=bpy.data.scenes['Stronghold asset review'];camera=scene.camera
for label,source in [('desert-house',bpy.data.objects['DESERT_HOUSE_01']),('afghan-house',bpy.data.objects['Sketchfab_model.009'])]:
 copies=[]
 for mesh in source.children_recursive:
  if mesh.type!='MESH':continue
  obj=mesh.copy();obj.data=mesh.data.copy();obj.parent=None;obj.data.transform(mesh.matrix_world);obj.matrix_world=Matrix.Identity(4);scene.collection.objects.link(obj);copies.append(obj)
 points=[v.co for o in copies for v in o.data.vertices];lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)])
 factor=10/(hi.z-lo.z);transform=Matrix.Diagonal((factor,factor,factor,1))@Matrix.Translation(Vector((-(lo.x+hi.x)/2,-(lo.y+hi.y)/2,-lo.z)))
 for obj in copies:obj.data.transform(transform)
 scene.render.filepath=str(root/f'output/reference-building/{label}.png');bpy.ops.render.render(write_still=True,scene=scene.name)
 for obj in copies:bpy.data.objects.remove(obj,do_unlink=True)
print('Rendered two candidate houses')
