"""Normalize civilian FBX unit conversion, preserving their original skin and materials."""
import json
import shutil
from pathlib import Path
import bpy
from mathutils import Vector, Quaternion, Matrix
from io_scene_fbx import parse_fbx

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / '.utmp/civilian-fbx-normalization'
WORK.mkdir(parents=True, exist_ok=True)

def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))

def bounds():
    deps = bpy.context.evaluated_depsgraph_get()
    helpers={bone.custom_shape for obj in bpy.context.scene.objects if obj.type=='ARMATURE'
             for bone in obj.pose.bones if bone.custom_shape}
    points = [obj.matrix_world @ vertex.co for obj in bpy.context.scene.objects
              if obj.type == 'MESH' and obj not in helpers for vertex in obj.evaluated_get(deps).data.vertices]
    return [Vector([fn(p[i] for p in points) for i in range(3)]) for fn in (min, max)]

def scales(path):
    tree, _ = parse_fbx.parse(str(path))
    return [list(prop.props[4:]) for objects in tree.elems if objects.id == b'Objects'
            for obj in objects.elems if obj.id == b'Model'
            for props in obj.elems if props.id == b'Properties70'
            for prop in props.elems if prop.props[0] == b'Lcl Scaling']

report = []
for asset in sorted((ROOT / 'Assets/Resources/Models/Civilians').glob('*.fbx')):
    backup = WORK / asset.name
    if not backup.exists():
        shutil.copy2(asset, backup)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT / 'Assets/3d/civilian' / (asset.stem+'.glb')))
    bpy.context.view_layer.update()
    before = bounds()
    bone_count = sum(len(o.data.bones) for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    materials = len(bpy.data.materials)
    # Remove nested glTF wrapper nodes from the exported skeleton graph. Keep
    # each mesh linked directly to the armature used by its skin modifier.
    helpers={bone.custom_shape for obj in bpy.context.scene.objects if obj.type=='ARMATURE'
             for bone in obj.pose.bones if bone.custom_shape}
    objects=[o for o in bpy.context.scene.objects if o.type in {'MESH','ARMATURE'} and o not in helpers]
    # The static female model is authored in centimetres. Bake its geometry
    # upright at human scale rather than relying on a large import transform.
    if bone_count==0:
        low,high=before
        correction=Matrix.Scale(1.85/(high.z-low.z),4) @ Matrix.Translation(Vector((-(low.x+high.x)/2,-(low.y+high.y)/2,-low.z)))
        for obj in objects:
            world=obj.matrix_world.copy()
            obj.parent=None
            obj.data.transform(correction @ world)
            obj.matrix_world=Matrix.Identity(4)
        bpy.context.view_layer.update()
        before=bounds()
    matrices={o:o.matrix_world.copy() for o in objects}
    for obj in objects:
        obj.parent=None
        obj.matrix_world=matrices[obj]
    for obj in objects:
        if obj.type=='MESH':
            skin=next((m for m in obj.modifiers if m.type=='ARMATURE' and m.object),None)
            if skin:
                obj.parent=skin.object
                obj.matrix_world=matrices[obj]
    bpy.context.view_layer.update()
    assert max((a-b).length for a,b in zip(before,bounds())) < .005
    # glTF can supply a posed skeleton whose bind matrices differ from the
    # displayed pose. Bake that pose into BOTH mesh and skeleton before FBX
    # export, otherwise FBX rest-pose import stretches the vertices.
    deps=bpy.context.evaluated_depsgraph_get()
    skins=[]
    for obj in objects:
        if obj.type!='MESH': continue
        modifier=next((m for m in obj.modifiers if m.type=='ARMATURE' and m.object),None)
        if modifier:
            mesh=bpy.data.meshes.new_from_object(obj.evaluated_get(deps),preserve_all_data_layers=True,depsgraph=deps)
            skins.append((obj,modifier.object,mesh))
    for obj,rig,mesh in skins:
        obj.data=mesh
        for modifier in list(obj.modifiers):
            if modifier.type=='ARMATURE': obj.modifiers.remove(modifier)
    bpy.ops.object.select_all(action='DESELECT')
    for rig in [o for o in objects if o.type=='ARMATURE']:
        rig.select_set(True);bpy.context.view_layer.objects.active=rig
        bpy.ops.object.mode_set(mode='POSE');bpy.ops.pose.armature_apply(selected=False);bpy.ops.object.mode_set(mode='OBJECT')
        rig.select_set(False)
    for obj,rig,mesh in skins:
        modifier=obj.modifiers.new('Civilian skin','ARMATURE');modifier.object=rig
    bpy.context.view_layer.update()
    assert max((a-b).length for a,b in zip(before,bounds())) < .005
    texture_folder=asset.parent/'Textures'
    texture_folder.mkdir(exist_ok=True)
    for index,image in enumerate(bpy.data.images):
        if image.packed_file or image.source=='FILE':
            image.filepath_raw=str(texture_folder/f'{asset.stem}_restored_{index}.png')
            image.file_format='PNG'
            image.save()
    candidate = WORK / ('normalized_' + asset.name)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(candidate), use_selection=True,
        object_types={'MESH', 'ARMATURE'}, add_leaf_bones=False,
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', path_mode='COPY', embed_textures=True,
        bake_anim=False)
    load(candidate)
    after = bounds()
    error = max((a-b).length for a,b in zip(before, after))
    assert error < .005, (asset.name, error)
    assert bone_count == sum(len(o.data.bones) for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    assert materials == len(bpy.data.materials)
    # Exercise the retained leg weights after FBX round-trip, not only the bind pose.
    rigged_legs=[bone for rig in bpy.context.scene.objects if rig.type=='ARMATURE'
                 for bone in rig.pose.bones if any(word in bone.name.lower() for word in ('upleg','thigh','calf'))]
    rest=[(bone,bone.rotation_mode,bone.rotation_quaternion.copy()) for bone in rigged_legs]
    span=(after[1]-after[0]).length
    for angle in (-.3,-.15,0,.15,.3):
        for bone,mode,rotation in rest:
            bone.rotation_mode='QUATERNION'
            bone.rotation_quaternion=rotation @ Quaternion((1,0,0),angle)
        bpy.context.view_layer.update()
        posed=bounds()
        assert (posed[1]-posed[0]).length < span*1.5, 'Leg deformation exploded'
    for bone,mode,rotation in rest:
        bone.rotation_quaternion=rotation;bone.rotation_mode=mode
    before_scales, after_scales = scales(asset), scales(candidate)
    assert all(max(abs(s) for s in scale) < 10 for scale in after_scales), after_scales
    shutil.copy2(candidate, asset)
    report.append({'asset': asset.name, 'bones': bone_count, 'materials': materials,
                   'max_bounds_error_metres': error, 'leg_pose_checks': 5 if rest else 0,
                   'max_node_scale_before': max((max(s) for s in before_scales),default=1),
                   'max_node_scale_after': max((max(s) for s in after_scales),default=1)})
(ROOT / 'Logs/civilian-fbx-normalization.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
