"""Rig supplied static army meshes and bake portable Unity animation clips."""
import math
from pathlib import Path
import bpy
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Resources/Models/Enemies'

def segment_distance(p,a,b):
    delta=b-a
    return (p-(a+delta*max(0,min(1,(p-a).dot(delta)/delta.length_squared)))).length

def aim(rig,name,target):
    bpy.context.view_layer.update()
    bone=rig.pose.bones[name]
    q=(bone.tail-bone.head).rotation_difference(Vector(target)-bone.head)
    bone.matrix=Matrix.Translation(bone.head)@q.to_matrix().to_4x4()@Matrix.Translation(-bone.head)@bone.matrix
    bpy.context.view_layer.update()

for number in [1,2]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT/f'Assets/3d/enemy/army_character_{number}.glb'))
    bpy.context.view_layer.update()
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    low=Vector([min(v[i] for v in points) for i in range(3)])
    high=Vector([max(v[i] for v in points) for i in range(3)])
    normal=Matrix.Rotation(math.pi/2,4,'Z')@Matrix.Scale(1.85/(high.z-low.z),4)@Matrix.Translation(Vector((-(low.x+high.x)/2,-(low.y+high.y)/2,-low.z)))
    for obj in meshes:
        matrix=normal@obj.matrix_world.copy()
        obj.parent=None; obj.matrix_world=Matrix.Identity(4); obj.data.transform(matrix)
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH': bpy.data.objects.remove(obj,do_unlink=True)
    for i,image in enumerate(bpy.data.images):
        if image.source=='FILE':
            image.filepath_raw=str(OUT/'Textures'/f'army_character_{number}_{i}.png')
            image.file_format='PNG'; image.save()
    rig=bpy.data.objects.new('ArmyRig',bpy.data.armatures.new('ArmySkeleton'))
    bpy.context.collection.objects.link(rig); bpy.context.view_layer.objects.active=rig; rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    definitions=[('hips',(0,0,.87),(0,0,1.06),None),('spine',(0,0,1.06),(0,0,1.40),'hips'),
                 ('neck',(0,0,1.40),(0,0,1.56),'spine'),('head',(0,0,1.56),(0,0,1.8),'neck')]
    for side,sign in [('left',1),('right',-1)]:
        def p(x,y,z): return (sign*x,y,z)
        shoulder=p(.21,0,1.39)
        elbow=p(.31,0,1.13) if number==1 else p(.49,0,1.38)
        wrist=p(.38,-.015,.94) if number==1 else p(.72,0,1.37)
        hand=p(.40,-.02,.84) if number==1 else p(.83,0,1.36)
        definitions.extend([(side+'arm',shoulder,elbow,'spine'),(side+'forearm',elbow,wrist,side+'arm'),
            (side+'hand',wrist,hand,side+'forearm'),(side+'upleg',p(.105,0,.9),p(.11,-.025,.49),'hips'),
            (side+'leg',p(.11,-.025,.49),p(.11,0,.13),side+'upleg'),
            (side+'foot',p(.11,0,.13),p(.11,-.16,.07),side+'leg')])
    for name,head,tail,parent in definitions:
        bone=rig.data.edit_bones.new(name); bone.head=head; bone.tail=tail
        if parent: bone.parent=rig.data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    segments={n:(Vector(a),Vector(b)) for n,a,b,_ in definitions}
    for obj in meshes:
        groups={name:obj.vertex_groups.new(name=name) for name in segments}
        for vertex in obj.data.vertices:
            p=vertex.co; side='left' if p.x>0 else 'right'
            names=['hips','spine','neck','head',side+'arm',side+'forearm',side+'hand',side+'upleg',side+'leg',side+'foot']
            nearest=sorted((segment_distance(p,*segments[n]),n) for n in names)[:3]
            weights=[1/max(.015,d)**4 for d,_ in nearest]; total=sum(weights)
            for weight,(_,name) in zip(weights,nearest): groups[name].add([vertex.index],weight/total,'REPLACE')
        modifier=obj.modifiers.new('Skin','ARMATURE'); modifier.object=rig; obj.parent=rig
    for bone in rig.pose.bones: bone.rotation_mode='QUATERNION'
    # Bring both arms into a compact two-handed weapon stance before baking clips.
    aim(rig,'rightarm',(-.26,-.04,1.14)); aim(rig,'rightforearm',(-.08,-.40,1.22))
    aim(rig,'leftarm',(.26,-.06,1.15)); aim(rig,'leftforearm',(.07,-.46,1.25))
    rest={b.name:b.rotation_quaternion.copy() for b in rig.pose.bones}
    rig.animation_data_create()
    for label,frames in [('Idle',61),('Walk',31),('Fire',13),('Hit',13),('Death',31)]:
        action=bpy.data.actions.new(label); action.use_fake_user=True; rig.animation_data.action=action
        for frame in range(1,frames+1):
            t=(frame-1)/(frames-1)
            for bone in rig.pose.bones:
                bone.rotation_quaternion=rest[bone.name]; bone.location=(0,0,0)
            if label=='Walk':
                for side,phase in [('left',0),('right',math.pi)]:
                    cycle=2*math.pi*t+phase
                    rig.pose.bones[side+'upleg'].rotation_quaternion=rest[side+'upleg']@Matrix.Rotation(.30*math.sin(cycle),4,'X').to_quaternion()
                    rig.pose.bones[side+'leg'].rotation_quaternion=rest[side+'leg']@Matrix.Rotation(-.48*max(0,-math.sin(cycle)),4,'X').to_quaternion()
                rig.pose.bones['hips'].location.y=.014*(1-math.cos(4*math.pi*t))
            elif label=='Idle':
                rig.pose.bones['spine'].rotation_quaternion=rest['spine']@Matrix.Rotation(.008*math.sin(2*math.pi*t),4,'X').to_quaternion()
            elif label in ['Fire','Hit']:
                rig.pose.bones['spine'].rotation_quaternion=rest['spine']@Matrix.Rotation((.035 if label=='Fire' else .12)*math.sin(math.pi*t),4,'X').to_quaternion()
            else:
                ease=t*t*(3-2*t)
                rig.pose.bones['hips'].rotation_quaternion=Matrix.Rotation(1.35*ease,4,'X').to_quaternion()
                rig.pose.bones['hips'].location.y=-.66*ease
            for bone in rig.pose.bones:
                bone.keyframe_insert('rotation_quaternion',frame=frame); bone.keyframe_insert('location',frame=frame)
    bpy.context.scene.render.fps=30
    bpy.context.scene.frame_start=1; bpy.context.scene.frame_end=61
    rig.animation_data.action=bpy.data.actions['Idle']; bpy.context.scene.frame_set(1)
    for obj in meshes+[rig]: obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(OUT/f'army_character_{number}.fbx'),use_selection=True,
        object_types={'MESH','ARMATURE'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
        path_mode='COPY',embed_textures=True,bake_anim=True,bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
    folder=ROOT/'Tools/generated'; folder.mkdir(exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/f'army_character_{number}_rigged.blend'))
    print('RIGGED',number,'bones',len(rig.data.bones),'clips',len(bpy.data.actions))
