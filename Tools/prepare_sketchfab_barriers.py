import bpy, json, math, requests
from pathlib import Path
from mathutils import Vector, Matrix
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
out=root/'Assets/Resources/Models/StreetProps'
out.mkdir(parents=True,exist_ok=True)
records=[]
for source_name,name in [('Barrel_orange_Barrier_0','BarrierOrange'),('Barrel_yellow_Barrier_yellow_0','BarrierYellow')]:
 source=bpy.data.objects[source_name]
 obj=source.copy();obj.data=source.data.copy();bpy.context.collection.objects.link(obj)
 world=source.matrix_world.copy();obj.parent=None;obj.matrix_world=Matrix.Identity(4)
 obj.data.transform(world)
 points=[v.co for v in obj.data.vertices]
 low=Vector([min(p[i] for p in points) for i in range(3)])
 high=Vector([max(p[i] for p in points) for i in range(3)])
 size=high-low
 obj.data.transform(Matrix.Translation(Vector((-(low.x+high.x)/2,-(low.y+high.y)/2,-low.z))))
 obj.data.transform(Matrix.Diagonal((2.4/size.x,.6/size.y,.95/size.z,1)))
 obj.data.transform(Matrix.Rotation(math.pi/2,4,'Z'))
 obj.name=name
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.export_scene.fbx(filepath=str(out/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
 image=bpy.data.images['Barrier_diffuse.jpeg' if name=='BarrierOrange' else 'Barrier_yellow_diffuse.jpeg'].copy()
 image.scale(1024,1024);image.filepath_raw=str(out/(name+'_albedo.png'));image.file_format='PNG';image.save()
 records.append({'name':name,'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'dimensionsUnity':[.6,.95,2.4]})
 bpy.data.objects.remove(obj,do_unlink=True)
normal=bpy.data.images['Barrier_normal.jpeg'].copy();normal.scale(1024,1024);normal.filepath_raw=str(out/'Barrier_normal.png');normal.file_format='PNG';normal.save()
meta=requests.get('https://api.sketchfab.com/v3/models/afaab6285c484c36aab250e06727d471',timeout=30).json()
evidence={'title':meta.get('name'),'source':meta.get('viewerUrl'),'creator':meta.get('user',{}).get('displayName'),'license':meta.get('license'),'downloaded':'2026-09-30','changes':'Split variants; resized textures to 1024; grounded, rotated and scaled meshes for Unity.','models':records}
(root/'Store/SketchfabBarrierLicense.json').write_text(json.dumps(evidence,indent=2))
print(json.dumps(evidence))
