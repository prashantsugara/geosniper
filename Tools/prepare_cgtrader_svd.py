import bpy,math,json
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
objects=[o for o in bpy.data.objects if o.get('svdsource') and o.type=='MESH']
points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
low=Vector([min(p[i] for p in points) for i in range(3)]);high=Vector([max(p[i] for p in points) for i in range(3)])
transform=Matrix.Rotation(-math.pi/2,4,'Z')@Matrix.Diagonal((1.22/(high.x-low.x),)*3+(1,))@Matrix.Translation(-(low+high)/2)
copies=[]
for original in objects:
 o=original.copy();o.data=original.data.copy();bpy.context.collection.objects.link(o);o.parent=None;o.matrix_world=Matrix.Identity(4);o.data.transform(original.matrix_world);o.name='SVDCG_'+original.name;o.modifiers.clear()
 if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
 # The exchange file has no texture payload. Add planar UVs for the licensed M40 surface maps.
 for layer in o.data.uv_layers:
  for loop in o.data.loops:
   p=o.data.vertices[loop.vertex_index].co;layer.data[loop.index].uv=(p.x*.6,p.z*2)
 name='Wood' if any('wood' in m.name.lower() for m in o.data.materials if m) else 'Metal'
 o.data.materials.clear();mat=bpy.data.materials.get('SVDCG_'+name) or bpy.data.materials.new('SVDCG_'+name);o.data.materials.append(mat)
 for p in o.data.polygons:p.material_index=0
 if len(o.data.polygons)>2000:
  bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('Mobile topology','DECIMATE');mod.ratio=.16;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
 o.data.transform(transform);copies.append(o)
grip=bpy.data.objects.new('Grip',None);bpy.context.collection.objects.link(grip);grip.location=transform@Vector((2.3,.74,-.25));copies.append(grip)
bpy.ops.object.select_all(action='DESELECT')
for o in copies:o.select_set(True)
bpy.context.view_layer.objects.active=copies[0]
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/Resources/Models/Weapons/SVDMarksman.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
print(json.dumps({'triangles':sum(len(p.vertices)-2 for o in copies if o.type=='MESH' for p in o.data.polygons)}))
for o in copies:bpy.data.objects.remove(o,do_unlink=True)
