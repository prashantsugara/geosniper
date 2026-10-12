import bpy,json,requests
from pathlib import Path
from mathutils import Vector,Matrix
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
out=root/'Assets/Resources/Models/Buildings';out.mkdir(parents=True,exist_ok=True)
sources=[o for o in bpy.data.objects['Sketchfab_model.001'].children_recursive if o.type=='MESH']
def export(name,meshes):
 copies=[]
 for src in meshes:
  obj=src.copy();obj.data=src.data.copy();bpy.context.collection.objects.link(obj)
  obj.parent=None;obj.matrix_world=Matrix.Identity(4);obj.data.transform(src.matrix_world);copies.append(obj)
 points=[v.co for o in copies for v in o.data.vertices]
 lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)])
 factor=3.2/max(hi.z-lo.z,.001)
 transform=Matrix.Diagonal((factor,factor,factor,1))@Matrix.Translation(Vector((-(lo.x+hi.x)/2,-(lo.y+hi.y)/2,-lo.z)))
 for obj in copies:obj.data.transform(transform)
 bpy.ops.object.select_all(action='DESELECT')
 for obj in copies:obj.select_set(True)
 bpy.context.view_layer.objects.active=copies[0]
 bpy.ops.object.join()
 copies=[bpy.context.view_layer.objects.active]
 bpy.ops.export_scene.fbx(filepath=str(out/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
 record={'name':name,'triangles':sum(len(p.vertices)-2 for o in copies for p in o.data.polygons),'size':list((hi-lo)*factor)}
 for obj in copies:bpy.data.objects.remove(obj,do_unlink=True)
 return record
records=[export('HouseInteriorSF',sources)]
before=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=r'C:/Users/Geeta Sugara/Downloads/House+Layout.fbx')
imported=set(bpy.data.objects)-before
records.append(export('HouseLayoutCG',[o for o in imported if o.type=='MESH']))
for obj in imported:bpy.data.objects.remove(obj,do_unlink=True)
meta=requests.get('https://api.sketchfab.com/v3/models/e910b8395ddc4b9886d87caffdbacc69',timeout=30).json()
license={'title':meta.get('name'),'creator':meta.get('user',{}).get('displayName'),'source':meta.get('viewerUrl'),'license':meta.get('license'),'downloaded':'2026-10-06','changes':'Grounded and normalized the house to 3.2m height, joined source meshes to reduce draw calls and exported FBX for Unity.'}
(root/'Store/SketchfabHouseLicense.json').write_text(json.dumps(license,indent=2))
print(json.dumps(records))
