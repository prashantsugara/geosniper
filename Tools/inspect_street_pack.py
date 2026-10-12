import bpy,json,requests
from mathutils import Vector
meta=requests.get('https://api.sketchfab.com/v3/models/741e544242a9481fa54c1f7bca6e92b2',timeout=30).json()
print(json.dumps({'title':meta.get('name'),'creator':meta.get('user',{}).get('displayName'),'license':meta.get('license')}))
for o in bpy.data.objects:
 if o.type=='MESH' and o.name!='Cube':
  print(json.dumps({'mesh':o.name,'parent':o.parent.name if o.parent else None,'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'materials':[m.name if m else None for m in o.data.materials]}))
print(json.dumps({'images':[(i.name,list(i.size)) for i in bpy.data.images]}))
