import bpy, json, math, requests, bmesh
from pathlib import Path
from mathutils import Vector, Matrix
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
out=root/'Assets/Resources/Models/LevelSites';out.mkdir(parents=True,exist_ok=True)
records=[]
sources=[('MilitaryTent','Sketchfab_model.013',4.4,'width'),('BrickApartments','Sketchfab_model.015',14,'width'),('Warehouse','Sketchfab_model.014',14,'width'),('ShopShelf','Sketchfab_model.016',1.8,'width'),('ShopFridge','Sketchfab_model.016',1.8,'height')]
for label,source,dimension,axis in sources:
 if globals().get('SITE_MODEL_FILTER') and label not in SITE_MODEL_FILTER:continue
 copies=[]
 for original in bpy.data.objects[source].children_recursive:
  if original.type!='MESH':continue
  if label=='BrickApartments' and original.name!='Material2.007':continue
  if label=='ShopShelf' and original.name not in ['LongShelfBase_Shelves_0','Shelf_Shelves_0']:continue
  if label=='ShopFridge' and not original.name.startswith(('Fridge_','FridgeCover_')):continue
  if label=='Warehouse' and original.name.startswith(('Object_2.','Object_7.')):continue
  obj=original.copy();obj.data=original.data.copy();obj.parent=None;obj.data.transform(original.matrix_world);obj.matrix_world=Matrix.Identity(4);bpy.context.collection.objects.link(obj)
  if label=='Warehouse':
   # Select the central building from the three-building source pack; preserve UVs.
   bm=bmesh.new();bm.from_mesh(obj.data)
   bmesh.ops.delete(bm,geom=[f for f in bm.faces if not -10<f.calc_center_median().x<5],context='FACES')
   bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
   bm.to_mesh(obj.data);bm.free()
   if not obj.data.polygons:bpy.data.objects.remove(obj,do_unlink=True);continue
  copies.append(obj)
 points=[]
 for obj in copies:
  used={i for face in obj.data.polygons for i in face.vertices};points.extend(v.co.copy() for v in obj.data.vertices if v.index in used)
 # Tent stakes extend beyond the fabric. Use the fabric body for human-scale sizing.
 body=[v.co.copy() for o in copies if not o.name.startswith('Cylinder') for v in o.data.vertices] if label=='MilitaryTent' else points
 lo=Vector([min(p[i] for p in body) for i in range(3)]);hi=Vector([max(p[i] for p in body) for i in range(3)])
 factor=dimension/(hi.x-lo.x if axis=='width' else hi.z-lo.z)
 transform=Matrix.Rotation(math.pi,4,'Z')@Matrix.Diagonal((factor,factor,factor,1))@Matrix.Translation(Vector((-(lo.x+hi.x)/2,-(lo.y+hi.y)/2,-lo.z)))
 mats={}
 for obj in copies:
  obj.data.transform(transform)
  if label=='Warehouse':
   bm=bmesh.new();bm.from_mesh(obj.data);bm.normal_update()
   fronts=[face for face in bm.faces if face.normal.y>.95]
   if fronts:
    edge=max(face.calc_center_median().y for face in fronts)
    panels=[face for face in fronts if face.calc_center_median().y>edge-.06]
    bmesh.ops.delete(bm,geom=panels,context='FACES');print('Warehouse front loading panels removed: '+str(len(panels)))
   bm.to_mesh(obj.data);bm.free()
  if label=='BrickApartments':
   bm=bmesh.new();bm.from_mesh(obj.data)
   panels=[face for face in bm.faces if all(abs(v.co.x)<.9 and v.co.y>1.85 and v.co.z<1.9 for v in face.verts)]
   if not panels:raise RuntimeError('Apartment door panel selection is empty')
   print('Apartment closed door/frame faces removed: '+str(len(panels)))
   bmesh.ops.delete(bm,geom=panels,context='FACES');bm.to_mesh(obj.data);bm.free()
  for material in obj.data.materials:
   if not material or material.name in mats:continue
   key=label+'_'+str(len(mats));maps={}
   if material.use_nodes:
    for node in material.node_tree.nodes:
     if node.type!='TEX_IMAGE' or not node.image:continue
     name=node.image.name.lower();kind='normal' if 'normal' in name else 'orm' if 'rough' in name or 'metallic' in name else 'emission' if 'emissive' in name else 'albedo'
     filename=key+'_'+kind+'.png';im=node.image.copy();im.scale(min(1024,im.size[0]),min(1024,im.size[1]));im.filepath_raw=str(out/filename);im.file_format='PNG';im.save();bpy.data.images.remove(im);maps[kind]=filename[:-4]
   mats[material.name]={'key':key,'maps':maps}
  # All reviewed source parts have one material. Keep the key in the renderer name.
  obj.name=mats[obj.data.materials[0].name]['key']+'_'+str(copies.index(obj))
 bpy.ops.object.select_all(action='DESELECT')
 for obj in copies:obj.select_set(True)
 bpy.context.view_layer.objects.active=copies[0]
 bpy.ops.export_scene.fbx(filepath=str(out/(label+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
 records.append({'name':label,'triangles':sum(len(p.vertices)-2 for o in copies for p in o.data.polygons),'materials':list(mats.values())})
 for obj in copies:bpy.data.objects.remove(obj,do_unlink=True)
if globals().get('SITE_MODEL_FILTER'):
 previous=json.loads((out/'Surfaces.json').read_text())['models'];records=[r for r in previous if r['name'] not in SITE_MODEL_FILTER]+records
(out/'Surfaces.json').write_text(json.dumps({'models':records},indent=2))
licenses=[]
for uid in ['d95296298b2a4caca8a5c3ce4450bb15','a2a27bec2e364496b34ae399ee93cb67','320c272513a94c0482dbb3c4e79d61f5','5ede6f812941428dae4b31a3487623fd']:
 m=requests.get('https://api.sketchfab.com/v3/models/'+uid,timeout=30).json()
 if m.get('license',{}).get('slug') not in ['by','cc0']:raise RuntimeError('Unsupported license '+uid)
 licenses.append({'title':m['name'],'creator':m['user']['displayName'],'source':m['viewerUrl'],'license':m['license'],'downloaded':'2026-10-10','changes':'Grounded and scaled for gameplay. Warehouse central building extracted and door/window inserts omitted. Apartment ground and glass omitted. Mall shelves and fridge extracted; the source is an interior vignette. Texture maps resized to 1024 pixels and roughness channels packed for Unity. Original imports preserved.'})
(root/'Store/SketchfabLevelLocationLicenses.json').write_text(json.dumps(licenses,indent=2))
print(json.dumps(records))
