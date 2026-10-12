import bpy,json,requests
from pathlib import Path
from mathutils import Vector,Matrix
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
out=root/'Assets/Resources/Models/NeighborhoodProps'
out.mkdir(parents=True,exist_ok=True)
records=[]
for group,name,width in [('bench','StreetBench',2.4),('trash_bin','StreetBin',.65),('crate','DeliveryCrate',.85),('cone','TrafficCone',.45),('Oil_Tank','UtilityDrum',.65)]:
 source=bpy.data.objects[group]
 meshes=[o for o in source.children_recursive if o.type=='MESH']
 copies=[]
 for src in meshes:
  obj=src.copy();obj.data=src.data.copy();bpy.context.collection.objects.link(obj)
  obj.parent=None;obj.matrix_world=Matrix.Identity(4);obj.data.transform(src.matrix_world);copies.append(obj)
 points=[v.co for o in copies for v in o.data.vertices]
 lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)])
 factor=width/max(hi.x-lo.x,hi.y-lo.y)
 transform=Matrix.Diagonal((factor,factor,factor,1))@Matrix.Translation(Vector((-(lo.x+hi.x)/2,-(lo.y+hi.y)/2,-lo.z)))
 for obj in copies:obj.data.transform(transform)
 bpy.ops.object.select_all(action='DESELECT')
 for obj in copies:obj.select_set(True)
 bpy.context.view_layer.objects.active=copies[0]
 bpy.ops.export_scene.fbx(filepath=str(out/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
 records.append({'name':name,'triangles':sum(len(p.vertices)-2 for o in copies for p in o.data.polygons),'size':[round((hi[i]-lo[i])*factor,3) for i in range(3)]})
 for obj in copies:bpy.data.objects.remove(obj,do_unlink=True)
for material in ['Mat.1','Mat.2','material']:
 image=bpy.data.images[material+'_baseColor.jpeg'].copy();image.filepath_raw=str(out/(material+'_baseColor.png'));image.file_format='PNG';image.save()
meta=requests.get('https://api.sketchfab.com/v3/models/741e544242a9481fa54c1f7bca6e92b2',timeout=30).json()
evidence={'title':meta.get('name'),'source':meta.get('viewerUrl'),'creator':meta.get('user',{}).get('displayName'),'license':meta.get('license'),'downloaded':'2026-10-06','changes':'Separated five props, centered and grounded meshes, scaled uniformly to game dimensions, exported FBX and original 1024px atlases.','models':records}
(root/'Store/SketchfabNeighborhoodLicense.json').write_text(json.dumps(evidence,indent=2))
print(json.dumps(evidence))
