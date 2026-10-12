import bpy,json,math,requests
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
output=root/'Assets/Resources/Models/Weapons'
records=[]
for source,target,length in [('M40_33','M40Scout',1.12),('Dragunov SVD_37','SVDMarksman',1.22),('M16_8','M16Assault',1.0),('MP5_17','MP5Compact',.68),('Desert Eagle Handgun_60','EagleSidearm',.30)]:
 meshes=[]
 for original in bpy.data.objects[source].children_recursive:
  if original.type!='MESH':continue
  obj=original.copy();obj.data=original.data.copy();bpy.context.collection.objects.link(obj)
  obj.parent=None;obj.matrix_world=Matrix.Identity(4);obj.data.transform(original.matrix_world)
  meshes.append(obj)
 points=[v.co for obj in meshes for v in obj.data.vertices]
 low=Vector([min(v[i] for v in points) for i in range(3)]);high=Vector([max(v[i] for v in points) for i in range(3)])
 scale=length/(high.y-low.y)
 # Export source world-space geometry centered; FBX converts Blender Y length into Unity Z.
 transform=Matrix.Diagonal((scale,scale,scale,1))@Matrix.Translation(-(low+high)/2)
 for i,obj in enumerate(meshes):obj.data.transform(transform);obj.name=target+'_Part_'+str(i)
 trigger=next((obj for obj in bpy.data.objects[source].children_recursive if obj.type=='MESH' and 'trigger' in obj.parent.name.lower()),None)
 marker=bpy.data.objects.new('Grip',None);bpy.context.collection.objects.link(marker)
 marker.location=transform@(trigger.matrix_world.translation if trigger else (low+high)/2)
 meshes.append(marker)
 bpy.ops.object.select_all(action='DESELECT')
 for obj in meshes:obj.select_set(True)
 bpy.context.view_layer.objects.active=meshes[0]
 bpy.ops.export_scene.fbx(filepath=str(output/(target+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO',add_leaf_bones=False)
 records.append({'model':target,'triangles':sum(len(p.vertices)-2 for obj in meshes if obj.type=='MESH' for p in obj.data.polygons),'length':length})
 for obj in meshes:bpy.data.objects.remove(obj,do_unlink=True)
meta=requests.get('https://api.sketchfab.com/v3/models/d5f3f79b65b64390a42b0b61513017d7',timeout=30).json()
evidence={'title':meta.get('name'),'source':meta.get('viewerUrl'),'creator':meta.get('user',{}).get('displayName'),'license':meta.get('license'),'downloaded':'2026-10-05','changes':'Five variants separated, transformed and scaled for Unity mobile gameplay.','models':records}
(root/'Store/SketchfabGunLicense.json').write_text(json.dumps(evidence,indent=2))
print(json.dumps(evidence))
