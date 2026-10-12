import bpy,json
from pathlib import Path
folder=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2/.utmp/vegetation-source/trees')
for path in folder.glob('*.glb'):
 before=set(bpy.data.objects)
 bpy.ops.import_scene.gltf(filepath=str(path))
 for obj in set(bpy.data.objects)-before:
  if obj.type=='MESH':
   print(json.dumps({'file':path.name,'name':obj.name,'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'size':list(obj.dimensions),'materials':[m.name for m in obj.data.materials if m]}))
print(json.dumps({'images':[(i.name,list(i.size)) for i in bpy.data.images]}))
