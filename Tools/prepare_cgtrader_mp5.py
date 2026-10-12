import bpy,math,json
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2')
objects=[o for o in bpy.data.objects if o.get('mp5source') and o.type=='MESH' and not o.name.startswith('Bullet')]
points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
low=Vector([min(p[i] for p in points) for i in range(3)]);high=Vector([max(p[i] for p in points) for i in range(3)])
transform=Matrix.Rotation(math.pi,4,'Z')@Matrix.Diagonal((.68/(high.y-low.y),)*3+(1,))@Matrix.Translation(-(low+high)/2)
materials={}
for m in set(m for o in objects for m in o.data.materials):
 image=next(n.image for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and '_BaseColor' in n.image.filepath)
 key=image.filepath.replace('\\','/').split('/')[-1].split('_BaseColor')[0].replace('.','')
 copy=m.copy();copy.name='MP5CG_'+key;materials[m]=copy
copies=[]
for original in objects:
 o=original.copy();o.data=original.data.copy();bpy.context.collection.objects.link(o);o.parent=None;o.matrix_world=Matrix.Identity(4);o.data.transform(transform@original.matrix_world);o.name='MP5CG_'+original.name
 for i,m in enumerate(o.data.materials):o.data.materials[i]=materials[m]
 copies.append(o)
grip=bpy.data.objects.new('Grip',None);bpy.context.collection.objects.link(grip);grip.location=transform@Vector((0,-2.8,-1.1));copies.append(grip)
bpy.ops.object.select_all(action='DESELECT')
for o in copies:o.select_set(True)
bpy.context.view_layer.objects.active=copies[0]
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/Resources/Models/Weapons/MP5Compact.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
print(json.dumps({'triangles':sum(len(p.vertices)-2 for o in copies if o.type=='MESH' for p in o.data.polygons)}))
for o in copies:bpy.data.objects.remove(o,do_unlink=True)
