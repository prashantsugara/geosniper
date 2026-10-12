import bpy,json
from pathlib import Path
from mathutils import Vector
path=r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2/Assets/Resources/Models/PoliceCar.fbx'
backup=Path(r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2/.utmp/PoliceCar-original.fbx')
if not backup.exists():backup.write_bytes(Path(path).read_bytes())
before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(backup))
imported=set(bpy.data.objects)-before
colors=[('PoliceWhite',(.9,.93,.97,1)),('PoliceBlue',(.025,.12,.44,1)),('PoliceGlass',(.055,.16,.23,1)),('PoliceRubber',(.025,.028,.035,1)),('PoliceLightRed',(.95,.015,.02,1)),('PoliceLightBlue',(.025,.18,1,1)),('PoliceChrome',(.55,.6,.66,1))]
for obj in imported:
 if obj.type!='MESH':continue
 points=[obj.matrix_world@v.co for v in obj.data.vertices]
 lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)])
 print(json.dumps({'bounds':[list(lo),list(hi)],'dimensions':list(hi-lo)}))
 obj.data.materials.clear()
 for name,color in colors:
  material=bpy.data.materials.new(name);material.diffuse_color=color;material.use_nodes=True
  bsdf=material.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=color;bsdf.inputs['Roughness'].default_value=.3
  obj.data.materials.append(material)
 for face in obj.data.polygons:
  point=obj.matrix_world@face.center
  u=(point.x-lo.x)/(hi.x-lo.x);side=abs((point.y-(hi.y+lo.y)/2)/(hi.y-lo.y));h=(point.z-lo.z)/(hi.z-lo.z)
  normal=(obj.matrix_world.to_3x3()@face.normal).normalized()
  index=0
  if h<.29:index=3
  elif h>.92:index=4 if point.y<0 else 5
  elif .25<u<.78 and .60<h<.85 and (abs(normal.y)>.32 or abs(normal.x)>.35):index=2
  elif (u<.3 or u>.78) and h>.46 and normal.z>.55:index=1
  elif (u<.05 or u>.96) and h<.44:index=6
  face.material_index=index
bpy.ops.object.select_all(action='DESELECT')
for obj in imported:obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
for obj in imported:bpy.data.objects.remove(obj,do_unlink=True)
