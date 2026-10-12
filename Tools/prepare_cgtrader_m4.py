import bpy,math,json
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
objects=[o for o in bpy.data.objects['M4A1'].children_recursive if o.type=='MESH']
points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
low=Vector([min(p[i] for p in points) for i in range(3)]);high=Vector([max(p[i] for p in points) for i in range(3)])
transform=Matrix.Rotation(math.pi/2,4,'Z')@Matrix.Diagonal((1/(high.x-low.x),)*3+(1,))@Matrix.Translation(-(low+high)/2)
copies=[]
for original in objects:
 o=original.copy();o.data=original.data.copy();bpy.context.collection.objects.link(o);o.parent=None;o.matrix_world=Matrix.Identity(4);o.animation_data_clear();o.data.transform(transform@original.matrix_world);o.name='M4CG_'+original.name
 for m in o.data.materials:m.name='M4CG_T_M4A1'
 copies.append(o)
grip=bpy.data.objects.new('Grip',None);bpy.context.collection.objects.link(grip);grip.location=transform@Vector((-.55,0,-.15));copies.append(grip)
bpy.ops.object.select_all(action='DESELECT')
for o in copies:o.select_set(True)
bpy.context.view_layer.objects.active=copies[0]
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/Resources/Models/Weapons/M16Assault.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
print(json.dumps({'triangles':sum(len(p.vertices)-2 for o in copies if o.type=='MESH' for p in o.data.polygons)}))
for o in copies:bpy.data.objects.remove(o,do_unlink=True)
