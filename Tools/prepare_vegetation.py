import bpy,json,bmesh,random
from pathlib import Path
from mathutils import Vector,Matrix
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
out=root/'Assets/Resources/Models/Vegetation';out.mkdir(parents=True,exist_ok=True)
records=[]
def export(name,sources,limit):
 copies=[]
 for src in sources:
  obj=src.copy();obj.data=src.data.copy();bpy.context.collection.objects.link(obj)
  obj.parent=None;obj.matrix_world=Matrix.Identity(4);obj.data.transform(src.matrix_world);copies.append(obj)
 points=[v.co for o in copies for v in o.data.vertices]
 lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)])
 factor=1/(hi.z-lo.z)
 transform=Matrix.Diagonal((factor,factor,factor,1))@Matrix.Translation(Vector((-(lo.x+hi.x)/2,-(lo.y+hi.y)/2,-lo.z)))
 for obj in copies:
  obj.data.transform(transform)
  tris=sum(len(p.vertices)-2 for p in obj.data.polygons)
  foliage=any(m and m.use_nodes and any(n.type=='TEX_IMAGE' and n.image and ('leaf_diffuse' in n.image.name or n.image.name.startswith('R (1)')) for n in m.node_tree.nodes) for m in obj.data.materials)
  if 'Tree' in name:limit=14000 if foliage else 2000
  if tris>limit:
   if not foliage:
    bpy.context.view_layer.objects.active=obj
    mod=obj.modifiers.new('Mobile geometry reduction','DECIMATE');mod.ratio=limit/tris
    bpy.ops.object.modifier_apply(modifier=mod.name)
   # Disconnected twig/leaf islands have a minimum size under collapse decimation.
   # Thin those islands evenly instead of leaving tens of thousands of leaf cards.
   if sum(len(p.vertices)-2 for p in obj.data.polygons)>limit*1.2:
    bm=bmesh.new();bm.from_mesh(obj.data)
    remaining=set(bm.verts);groups=[]
    while remaining:
     seed=remaining.pop();group={seed};queue=[seed]
     while queue:
      current=queue.pop()
      for edge in current.link_edges:
       other=edge.other_vert(current)
       if other in remaining:remaining.remove(other);group.add(other);queue.append(other)
     groups.append(group)
    random.Random(42).shuffle(groups)
    if not foliage:groups.sort(key=lambda g:min(v.co.z for v in g))
    keep=set();budget=0
    for group in groups:
     faces={f for v in group for f in v.link_faces};cost=sum(len(f.verts)-2 for f in faces)
     if budget+cost<=limit:keep.update(group);budget+=cost
    if foliage:
     # Larger leaf cards compensate for thinning and for the smaller in-game trees.
     # UVs stay intact, preserving photographed leaf edges and veins.
     for group in groups:
      if group.issubset(keep):
       center=sum((v.co for v in group),Vector())/len(group)
       for v in group:v.co=center+(v.co-center)*2.2
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if v not in keep],context='VERTS')
    bm.to_mesh(obj.data);bm.free()
  for mat in obj.data.materials:
   if not mat:continue
   # Preserve source leaf atlases; material names identify the texture at runtime.
   image=next((n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image),None) if mat.use_nodes else None
   if image:
    texname='LeafAtlas' if 'leaf_diffuse' in image.name else 'LeafPhoto' if '1-130' in image.name else 'BarkPhoto'
    mat.name=texname
    destination=out/(texname+'.png')
    if not destination.exists():
     pic=image.copy()
     if max(pic.size)>1024:pic.scale(1024,1024)
     pic.filepath_raw=str(destination);pic.file_format='PNG';pic.save()
   else: mat.name='ShrubGreen'
 bpy.ops.object.select_all(action='DESELECT')
 for obj in copies:obj.select_set(True)
 bpy.context.view_layer.objects.active=copies[0]
 bpy.ops.export_scene.fbx(filepath=str(out/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
 records.append({'name':name,'triangles':sum(len(p.vertices)-2 for o in copies for p in o.data.polygons)})
 for obj in copies:bpy.data.objects.remove(obj,do_unlink=True)
export('BroadleafTree',[bpy.data.objects['tree'],bpy.data.objects['leaves']],4500)
export('SpreadingTree',[bpy.data.objects['tree.001'],bpy.data.objects['leaves.002']],4500)
for name,file in [('RoundedShrub','BushBig/BushBig1.fbx'),('ConeShrub','BushCone/BushCone1.fbx'),('LowShrub','BushSmall/BushSmall1.fbx'),('TallShrub','BushTall/BushTall1.fbx')]:
 before=set(bpy.data.objects)
 bpy.ops.import_scene.fbx(filepath=str(root/'.utmp/vegetation-source/shrubs/Plants/FBX'/file))
 imported=set(bpy.data.objects)-before
 export(name,[o for o in imported if o.type=='MESH'],2000)
 for obj in imported:bpy.data.objects.remove(obj,do_unlink=True)
(out/'mesh-budget.json').write_text(json.dumps(records,indent=2))
print(json.dumps(records))
