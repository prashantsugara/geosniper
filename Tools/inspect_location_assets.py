import bpy,json
from mathutils import Vector
from pathlib import Path
rows=[]
for name in ['Sketchfab_model.013','Sketchfab_model.014','Sketchfab_model.015','Sketchfab_model.016']:
 for obj in bpy.data.objects[name].children_recursive:
  if obj.type!='MESH':continue
  points=[obj.matrix_world@Vector(p) for p in obj.bound_box]
  rows.append(dict(root=name,name=obj.name,triangles=sum(len(p.vertices)-2 for p in obj.data.polygons),min=[min(p[i] for p in points) for i in range(3)],max=[max(p[i] for p in points) for i in range(3)],materials=[dict(name=m.name,images=[dict(name=n.image.name,size=list(n.image.size)) for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]) for m in obj.data.materials if m and m.use_nodes]))
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2');(root/'output/level-locations/imported-assets.json').write_text(json.dumps(rows,indent=2));print(json.dumps(rows))
