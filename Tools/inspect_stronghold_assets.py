import bpy,json,requests
from mathutils import Vector
from pathlib import Path
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
rows=[]
for name in ['Sketchfab_model.009','Sketchfab_model.010','Sketchfab_model.011']:
 obj=bpy.data.objects.get(name)
 if not obj:continue
 for mesh in obj.children_recursive:
  if mesh.type!='MESH':continue
  points=[mesh.matrix_world@Vector(c) for c in mesh.bound_box]
  size=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
  mats=[]
  for m in mesh.data.materials:
   if m:mats.append({'name':m.name,'images':[n.image.name for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image] if m.use_nodes else []})
  rows.append({'root':name,'name':mesh.name,'triangles':sum(len(p.vertices)-2 for p in mesh.data.polygons),'size':size,'materials':mats})
(root/'output/reference-building/imported-models.json').write_text(json.dumps(rows,indent=2))
print(json.dumps(rows))
