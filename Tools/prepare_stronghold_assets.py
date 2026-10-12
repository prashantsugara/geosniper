import bpy,json,requests,math
from mathutils import Vector,Matrix
from pathlib import Path
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2');out=root/'Assets/Resources/Models/Stronghold';out.mkdir(parents=True,exist_ok=True)
records=[];gallery_layout=None
# Preserve the imported originals. Export grounded copies with real source stairs.
sources=[('AfghanStronghold',bpy.data.objects['Sketchfab_model.009'],10),('Sandbag',bpy.data.objects['Sketchfab_model.011'],.26)]
for label,source,height in sources:
 copies=[]
 for mesh in source.children_recursive:
  if mesh.type!='MESH':continue
  # Window inserts are removed, leaving source wall openings usable for shots.
  if label=='AfghanStronghold' and any(m and m.name=='Window' for m in mesh.data.materials):continue
  obj=mesh.copy();obj.data=mesh.data.copy();obj.parent=None;obj.data.transform(mesh.matrix_world);obj.matrix_world=Matrix.Identity(4);bpy.context.collection.objects.link(obj);copies.append(obj)
 if label=='AfghanStronghold':
  # Replace the closed-panel Arab wing with actual open galleries. Preserve the
  # licensed Afghan shell/stairs and author the extension in physical metres.
  body_points=[v.co for o in copies for v in o.data.vertices]
  body_low=Vector([min(p[i] for p in body_points) for i in range(3)]);body_high=Vector([max(p[i] for p in body_points) for i in range(3)])
  body_factor=height/(body_high.z-body_low.z)
  body_transform=Matrix.Diagonal((body_factor,body_factor,body_factor,1))@Matrix.Translation(Vector((-(body_low.x+body_high.x)/2,-(body_low.y+body_high.y)/2,-body_low.z)))
  for obj in copies:obj.data.transform(body_transform)
  gallery_namespace={};exec((root/'Tools/stronghold_gallery_geometry.py').read_text(),gallery_namespace)
  gallery_objects,gallery_layout=gallery_namespace['build_gallery'](bpy);copies.extend(gallery_objects)
 points=[v.co for o in copies for v in o.data.vertices];lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)])
 factor=height/(hi.z-lo.z);transform=Matrix.Diagonal((factor,factor,factor,1))@Matrix.Translation(Vector((-(lo.x+hi.x)/2,-(lo.y+hi.y)/2,-lo.z)))
 if label=='AfghanStronghold':
  # Keep a Unity-space physical route for reproducible collision validation.
  layout={}
  for key,points in gallery_layout.items():
   layout[key]=[]
   for point in points:
    # Unity's handedness conversion also reflects Blender X at FBX import.
    p=transform@Vector(point);layout[key].append({'x':-p.x,'y':p.z,'z':-p.y})
  (out/'GalleryLayout.json').write_text(json.dumps(layout,indent=2))
 materials=[]
 for obj in copies:
  obj.data.transform(transform)
  if label=='AfghanStronghold':
   # Source has no authored image maps. Physical planar UVs bind the separately
   # licensed plaster material without stretched building-sized texture patches.
   uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='Physical metres')
   for p in obj.data.polygons:
    axis=max(range(3),key=lambda i:abs(p.normal[i]));axes=[i for i in range(3) if i!=axis]
    for loop in p.loop_indices:
     co=obj.data.vertices[obj.data.loops[loop].vertex_index].co;uv.data[loop].uv=(co[axes[0]]*.6,co[axes[1]]*.6)
  for i,m in enumerate(obj.data.materials):
   if not m:continue
   key=m.name
   if key in [r['sourceMaterial'] for r in materials]:continue
   dest='surface_'+str(len(materials));maps={}
   if m.use_nodes and label=='Sandbag':
    for node in m.node_tree.nodes:
     if node.type=='TEX_IMAGE' and node.image:
      name=node.image.name;kind='normal' if 'normal' in name.lower() else 'orm' if 'rough' in name.lower() else 'albedo'
      filename=label+'_'+dest+'_'+kind+'.png';image=node.image.copy();image.scale(min(1024,image.size[0]),min(1024,image.size[1]));image.filepath_raw=str(out/filename);image.file_format='PNG';image.save();maps[kind]=filename[:-4];bpy.data.images.remove(image)
   materials.append({'sourceMaterial':key,'maps':maps})
 bpy.ops.object.select_all(action='DESELECT')
 for obj in copies:obj.select_set(True)
 bpy.context.view_layer.objects.active=copies[0]
 bpy.ops.export_scene.fbx(filepath=str(out/(label+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
 records.append({'name':label,'triangles':sum(len(p.vertices)-2 for o in copies for p in o.data.polygons),'size':list((hi-lo)*factor),'materials':materials})
 for obj in copies:bpy.data.objects.remove(obj,do_unlink=True)
# Licensed PBR plaster, masonry and timber keep each surface physically distinct.
surface_maps={}
for label,source in [('Plaster','PLASTER_19'),('Stone','STONE_10'),('Timber','WOOD_06')]:
 material=bpy.data.materials.get(source)
 if material is None:raise RuntimeError('Missing licensed surface '+source)
 maps={}
 for node in material.node_tree.nodes:
  if node.type!='TEX_IMAGE' or not node.image:continue
  kind='normal' if 'normal' in node.image.name.lower() else 'orm' if 'rough' in node.image.name.lower() else 'albedo'
  filename=label+'_'+kind+'.png';image=node.image.copy();image.scale(min(1024,image.size[0]),min(1024,image.size[1]));image.filepath_raw=str(out/filename);image.file_format='PNG';image.save();maps[kind]=filename[:-4];bpy.data.images.remove(image)
 surface_maps[label]=maps
(out/'surfaces.json').write_text(json.dumps({'models':records,'surfaces':surface_maps},indent=2))
licenses=[]
for uid in ['9f97af2ec2fd4379a65c0683b65d08c3','2f7b88bbcf1b454cad2c3e9dbc9dd18e','7d52600a15c747749d845d9f906045cf','a00d262125c245058f8ddd0e887bb833']:
 m=requests.get('https://api.sketchfab.com/v3/models/'+uid,timeout=30).json()
 licenses.append({'title':m.get('name'),'creator':m.get('user',{}).get('displayName'),'source':m.get('viewerUrl'),'license':m.get('license'),'downloaded':'2026-10-10','changes':'Afghan shell grounded/scaled, window inserts removed, physical planar UVs; authored open gallery extension with collidable stairs, doorways, arches, timber rails and roof parapet. Desert-house plaster, stone and timber maps copied at 1024px. Sandbag geometry/PBR preserved. Arab Building downloaded for review; its closed-panel wing is no longer used in the exported stronghold.'})
(root/'Store/SketchfabStrongholdLicenses.json').write_text(json.dumps(licenses,indent=2))
print(json.dumps({'models':records,'surfaces':surface_maps}))
