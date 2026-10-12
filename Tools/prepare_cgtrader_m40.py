import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
for previous in list(bpy.data.objects):
 if previous.name.startswith('M40CG_') or previous.get('m40_export_marker'):bpy.data.objects.remove(previous,do_unlink=True)
for previous in list(bpy.data.materials):
 if previous.name.startswith('M40CG_'):bpy.data.materials.remove(previous)
source=bpy.data.objects['M40'];o=source.copy();o.data=source.data.copy();bpy.context.collection.objects.link(o);o.parent=None;o.matrix_world=Matrix.Identity(4);o.data.transform(source.matrix_world);o.modifiers.clear()
# Remove the hanging sling as an entire connected island, without cutting the stock.
bm=bmesh.new();bm.from_mesh(o.data);todo=set(bm.verts);remove=[]
while todo:
 first=todo.pop();stack=[first];island=[first]
 while stack:
  v=stack.pop()
  for e in v.link_edges:
   n=e.other_vert(v)
   if n in todo:todo.remove(n);stack.append(n);island.append(n)
 extent=max(max(v.co[i] for v in island)-min(v.co[i] for v in island) for i in range(3))
 if min(v.co.z for v in island)<-1.3 or extent<.035:remove.extend(island)
bmesh.ops.delete(bm,geom=remove,context='VERTS');bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bm.to_mesh(o.data);bm.free()
points=[v.co for v in o.data.vertices];low=Vector([min(p[i] for p in points) for i in range(3)]);high=Vector([max(p[i] for p in points) for i in range(3)])
transform=Matrix.Rotation(math.pi/2,4,'Z')@Matrix.Diagonal((1.12/(high.x-low.x),)*3+(1,))@Matrix.Translation(-(low+high)/2)
o.data.transform(transform);o.name='M40CG_Rifle'
materials=[]
out=root/'.utmp/cgtrader/m40/packed';out.mkdir(parents=True,exist_ok=True)
for m in o.data.materials:
 for node in m.node_tree.nodes:
  if node.type=='TEX_IMAGE' and node.image and node.image.packed_file:
   filename=node.image.filepath.replace('\\','/').split('/')[-1]
   (out/(m.name+'_'+filename)).write_bytes(node.image.packed_file.data)
 copy=m.copy();copy.name='M40CG_'+m.name;materials.append(copy)
face_materials=[p.material_index for p in o.data.polygons]
o.data.materials.clear()
for m in materials:o.data.materials.append(m)
for p,index in zip(o.data.polygons,face_materials):p.material_index=index
for layer in o.data.uv_layers:
 for uv in layer.data:
  if not all(math.isfinite(v) for v in uv.uv):uv.uv=(0,0)
bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('Mobile topology','DECIMATE');mod.ratio=min(1,18000/sum(len(p.vertices)-2 for p in o.data.polygons));mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
grip=bpy.data.objects.new('Grip',None);grip['m40_export_marker']=True;bpy.context.collection.objects.link(grip);grip.location=transform@Vector((-2.25,0,-.15))
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);grip.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/Resources/Models/Weapons/M40Scout.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
print(json.dumps({'triangles':sum(len(p.vertices)-2 for p in o.data.polygons)}))
bpy.data.objects.remove(o,do_unlink=True);bpy.data.objects.remove(grip,do_unlink=True)
