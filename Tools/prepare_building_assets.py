"""Bake selected supplied buildings into single-material, mobile-sized models."""
from pathlib import Path
import json
import bpy
from mathutils import Vector, Matrix

root=Path(__file__).resolve().parents[1]
out=root/'Assets/Resources/Models/Buildings'
out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(root/'Assets/3d/building/buildings.glb'))
bpy.context.view_layer.update()
sources=[o for o in bpy.context.scene.objects if o.type=='MESH']
manifest=[]
for number,name in enumerate(['Building_01','Building_03','Building_06','Building_09']):
    bpy.ops.object.select_all(action='DESELECT')
    parts=[]
    for source in sources:
        if source.parent and source.parent.name==name:
            part=source.copy(); part.data=source.data.copy(); bpy.context.collection.objects.link(part)
            part.matrix_world=source.matrix_world.copy(); world=part.matrix_world.copy()
            part.parent=None; part.matrix_world=world; part.select_set(True); parts.append(part)
    if not parts:
        raise RuntimeError('Missing building group '+name)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join(); model=bpy.context.object
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # The supplied shop photography contains unrelated business names. Neutralize
    # those panels; the game attaches the downloaded local business names instead.
    plain=bpy.data.materials.new('Neutral shopfront'); plain.diffuse_color=(.17,.23,.25,1)
    plain.use_nodes=True; plain.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.17,.23,.25,1)
    for i,material in enumerate(model.data.materials):
        if material and 'shop' in material.name.lower(): model.data.materials[i]=plain
    points=[v.co for v in model.data.vertices]
    low=Vector([min(p[i] for p in points) for i in range(3)])
    high=Vector([max(p[i] for p in points) for i in range(3)])
    size=high-low
    model.data.transform(Matrix.Translation(Vector((-(low.x+high.x)/2,-(low.y+high.y)/2,-low.z))))
    model.name='CityBlock'+str(number+1)
    atlas=bpy.data.images.new(model.name+'_albedo',width=1024,height=1024,alpha=False)
    for material in model.data.materials:
        material.use_nodes=True
        node=material.node_tree.nodes.new('ShaderNodeTexImage'); node.image=atlas
        material.node_tree.nodes.active=node
    model.data.uv_layers.new(name='Atlas'); model.data.uv_layers.active_index=len(model.data.uv_layers)-1
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=.015); bpy.ops.object.mode_set(mode='OBJECT')
    model.data.uv_layers.active.active_render=True
    # Existing texture nodes need their original UV set while baking to the new one.
    for material in model.data.materials:
        tree=material.node_tree
        for node in list(tree.nodes):
            if node.type=='TEX_IMAGE' and node.image!=atlas and not node.inputs['Vector'].is_linked:
                uv=tree.nodes.new('ShaderNodeUVMap'); uv.uv_map=model.data.uv_layers[0].name
                tree.links.new(uv.outputs['UV'],node.inputs['Vector'])
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=1
    scene.render.bake.use_pass_direct=False; scene.render.bake.use_pass_indirect=False
    scene.render.bake.use_pass_color=True; scene.render.bake.margin=8
    bpy.ops.object.bake(type='DIFFUSE')
    atlas.filepath_raw=str(out/(model.name+'_albedo.png')); atlas.file_format='PNG'; atlas.save()
    single=bpy.data.materials.new(model.name+'_surface'); single.use_nodes=True
    texture=single.node_tree.nodes.new('ShaderNodeTexImage'); texture.image=atlas
    single.node_tree.links.new(texture.outputs['Color'],single.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    model.data.materials.clear(); model.data.materials.append(single)
    for face in model.data.polygons: face.material_index=0
    model.data.uv_layers.remove(model.data.uv_layers[0])
    bpy.ops.export_scene.fbx(filepath=str(out/(model.name+'.fbx')),use_selection=True,
        object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
    manifest.append(dict(name=model.name,source='Assets/3d/building/buildings.glb',group=name,
                         dimensions=list(size),triangles=sum(len(p.vertices)-2 for p in model.data.polygons)))
    bpy.data.objects.remove(model,do_unlink=True)
(out/'catalog.json').write_text(json.dumps(manifest,indent=2))
print('PREPARED',json.dumps(manifest))
