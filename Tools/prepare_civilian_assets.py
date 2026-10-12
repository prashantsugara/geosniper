import bpy,json,re,requests,hashlib
from pathlib import Path
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
out=root/'Assets/Resources/Models/Civilians';out.mkdir(parents=True,exist_ok=True)
assets=[('Sketchfab_model.002','civilian_carla','acf520f450d14dd799f98a6fede3edf5'),('Sketchfab_model.003','civilian_casual','05d9dd5bdddd4157bd46dc179781ee6e'),('Sketchfab_model.004','civilian_doctor','31c09ab8b098489b8c46712650e45bac'),('Sketchfab_model.005','civilian_eric','a46bc9f67aaa415bb4f3241eef900e7f')]
records=[];surfaces=[]
for rootname,name,uid in assets:
 out=root/('Assets/ThirdParty/CivilianAlternates' if name=='civilian_casual' else 'Assets/Resources/Models/Civilians');out.mkdir(parents=True,exist_ok=True)
 source=bpy.data.objects[rootname];objects=[source,*source.children_recursive]
 # Work on imported characters only; retain the user's other scene assets.
 for o in objects:
  if o.animation_data:o.animation_data_clear()
  if o.type=='ARMATURE':
   for p in o.pose.bones:p.matrix_basis.identity()
   for b in o.data.bones:
    old=b.name
    clean=re.sub(r'_\d+$','',old)
    if name in {'civilian_carla','civilian_eric'}:
     clean=clean.replace('upperleg_l','leftthigh').replace('upperleg_r','rightthigh').replace('lowerleg_l','leftshin').replace('lowerleg_r','rightshin').replace('foot_l','leftfoot').replace('foot_r','rightfoot').replace('upperarm_l','leftarm').replace('upperarm_r','rightarm').replace('lowerarm_l','leftforearm').replace('lowerarm_r','rightforearm')
    if name=='civilian_casual':
     clean=clean.replace('Base HumanLThigh','leftthigh').replace('Base HumanRThigh','rightthigh').replace('Base HumanLCalf','leftshin').replace('Base HumanRCalf','rightshin').replace('Base HumanLFoot','leftfoot').replace('Base HumanRFoot','rightfoot').replace('Base HumanLUpperarm','leftarm').replace('Base HumanRUpperarm','rightarm').replace('Base HumanLForearm','leftforearm').replace('Base HumanRForearm','rightforearm').replace('Base HumanLPalm','lefthand').replace('Base HumanRPalm','righthand').replace('Base HumanHead','head').replace('Base HumanSpine','spine')
    if clean!=old:b.name=clean
 mats=[]
 for o in objects:
  if o.type=='MESH':
   for m in o.data.materials:
    if m and m not in mats:mats.append(m)
 texture_cache={}
 for i,m in enumerate(mats):
  m.name=name+'_surface_'+str(i)
  bsdf=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
  image=None
  if bsdf:
   inp=bsdf.inputs['Base Color']
   if inp.is_linked:
    n=inp.links[0].from_node
    if n.type=='TEX_IMAGE':image=n.image
  if image:
   tex=image.copy();sz=tex.size[:];factor=min(1,1024/max(sz));tex.scale(max(1,round(sz[0]*factor)),max(1,round(sz[1]*factor)))
   texture_name=name+'_albedo_'+str(i);tex.name=texture_name;tex.filepath_raw=str(out/(texture_name+'.png'));tex.file_format='PNG';tex.save()
   digest=hashlib.sha256(Path(tex.filepath_raw).read_bytes()).hexdigest()
   if digest in texture_cache:
    stable_name,stable_image=texture_cache[digest]
    Path(tex.filepath_raw).unlink()
    texture_name=stable_name;tex=stable_image
   else:texture_cache[digest]=(texture_name,tex)
   n.image=tex
   if name!='civilian_casual':surfaces.append({'material':m.name,'texture':'Models/Civilians/'+texture_name})
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=source
 bpy.ops.export_scene.fbx(filepath=str(out/(name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='AUTO')
 meta=requests.get('https://api.sketchfab.com/v3/models/'+uid,timeout=30).json()
 record={'name':name,'active':name!='civilian_casual','title':meta.get('name'),'source':meta.get('viewerUrl'),'creator':meta.get('user',{}).get('displayName'),'license':meta.get('license'),'downloaded':'2026-10-07','triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects if o.type=='MESH'),'changes':'FBX conversion; 1024px albedo textures; rest pose for gameplay gait; anatomical bone aliases. Roles and accessories adapted in game.'}
 records.append(record)
(root/'Store/SketchfabCivilianLicenses.json').write_text(json.dumps(records,indent=2))
(root/'Assets/Resources/CivilianSurfaces.json').write_text(json.dumps({'surfaces':surfaces},indent=2))
print(json.dumps({'models':records,'surfaces':surfaces}))
