"""Remove per-node centimetre scaling from enemy FBXs without changing their poses.

Run with Blender --background --python Tools/normalize_enemy_fbx.py.
Original files are backed up before replacement; every clip is checked after export.
"""
import json
import shutil
from pathlib import Path
import bpy
from mathutils import Vector
from io_scene_fbx import parse_fbx

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / '.utmp/enemy-fbx-normalization'
WORK.mkdir(parents=True, exist_ok=True)

def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')

def poses(rig):
    result = {}
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for action in bpy.data.actions:
        rig.animation_data.action = action
        for fraction in (0, .25, .5, .75, 1):
            start, end = action.frame_range
            bpy.context.scene.frame_set(int(start + (end-start)*fraction))
            deps = bpy.context.evaluated_depsgraph_get()
            points = [o.matrix_world @ v.co for o in meshes for v in o.evaluated_get(deps).data.vertices]
            low = Vector([min(p[i] for p in points) for i in range(3)])
            high = Vector([max(p[i] for p in points) for i in range(3)])
            size = high-low
            assert max(size) < 3 and min(size) > .1, (action.name, fraction, size)
            result[(action.name.split('|')[-1], fraction)] = (low, high)
    return result

report = []
for number in (1, 2):
    asset = ROOT / f'Assets/Resources/Models/Enemies/army_character_{number}.fbx'
    backup = WORK / asset.name
    if not backup.exists():
        shutil.copy2(asset, backup)
    rig = load(asset)
    before = poses(rig)
    for action in bpy.data.actions:
        action.name = action.name.split('|')[-1]
    rig.animation_data.action = bpy.data.actions['Idle']
    bpy.context.scene.frame_set(1)
    bpy.ops.object.select_all(action='SELECT')
    candidate = WORK / f'normalized_{number}.fbx'
    bpy.ops.export_scene.fbx(filepath=str(candidate), use_selection=True,
        object_types={'MESH', 'ARMATURE'}, add_leaf_bones=False,
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', path_mode='COPY', embed_textures=True,
        bake_anim=True, bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0)
    tree, _ = parse_fbx.parse(str(candidate))
    for objects in tree.elems:
        if objects.id != b'Objects':
            continue
        for obj in objects.elems:
            if obj.id != b'Model':
                continue
            for props in obj.elems:
                if props.id == b'Properties70':
                    for prop in props.elems:
                        if prop.props[0] == b'Lcl Scaling':
                            assert all(abs(s-1) < .0001 for s in prop.props[4:]), obj.props
    after = poses(load(candidate))
    assert before.keys() == after.keys(), 'Clip samples changed during export'
    error = max((a-b).length for key in before for a,b in zip(before[key], after[key]))
    assert error < .005, f'Pose bounds changed by {error} metres'
    shutil.copy2(candidate, asset)
    report.append({'model': number, 'samples': len(after), 'max_bounds_error_metres': error,
                   'node_scales': 'identity', 'clips': sorted({key[0] for key in after})})
(ROOT / 'Logs/enemy-fbx-normalization.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
