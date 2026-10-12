import bpy, math, json
from pathlib import Path
from mathutils import Matrix, Vector
root = Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
objects = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.lower().startswith('object0')]
points = [o.matrix_world @ v.co for o in objects for v in o.data.vertices]
low = Vector([min(p[i] for p in points) for i in range(3)])
high = Vector([max(p[i] for p in points) for i in range(3)])
transform = Matrix.Rotation(math.pi,4,'Z') @ Matrix.Diagonal((.30/(high.y-low.y),)*3+(1,)) @ Matrix.Translation(-(low+high)/2)
copies=[]
for original in objects:
 o=original.copy();o.data=original.data.copy();bpy.context.collection.objects.link(o)
 o.parent=None;o.matrix_world=Matrix.Identity(4);o.data.transform(transform @ original.matrix_world)
 o.name='EagleCG_'+original.name
 # Keep UV seams and material boundaries; remove dense subdivision geometry.
 if len(o.data.polygons)>5000:
  mod=o.modifiers.new('Mobile topology','DECIMATE');mod.ratio=max(.04,min(1,3500/len(o.data.polygons)));mod.use_collapse_triangulate=True
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 for material in o.data.materials: material.name='EagleCG_'+material.name.replace('EagleCG_','')
 copies.append(o)
grip=bpy.data.objects.new('Grip',None);bpy.context.collection.objects.link(grip);grip.location=transform@Vector((0,2.15,1.65));copies.append(grip)
bpy.ops.object.select_all(action='DESELECT')
for o in copies:o.select_set(True)
bpy.context.view_layer.objects.active=copies[0]
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/Resources/Models/Weapons/EagleSidearm.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
print(json.dumps({'triangles':sum(len(p.vertices)-2 for o in copies if o.type=='MESH' for p in o.data.polygons)}))
for o in copies:bpy.data.objects.remove(o,do_unlink=True)
