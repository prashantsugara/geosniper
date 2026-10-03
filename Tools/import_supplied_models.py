"""Run with Blender --background --python Tools/import_supplied_models.py."""
import math
import runpy
from pathlib import Path
import bpy
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'Assets/Resources/Models'

def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    bpy.context.view_layer.update()

def textures(folder, prefix):
    folder.mkdir(parents=True, exist_ok=True)
    for i, image in enumerate(bpy.data.images):
        if image.source not in {'FILE', 'GENERATED'}:
            continue
        # Packed glTF images can be lazy-loaded; saving resolves their pixels.
        image.filepath_raw = str(folder / (prefix + '_' + str(i) + '.png'))
        image.file_format = 'PNG'
        image.save()

def export(path, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
        object_types={'MESH', 'ARMATURE', 'EMPTY'}, add_leaf_bones=False,
        axis_forward='-Z', axis_up='Y', path_mode='COPY', embed_textures=True,
        bake_anim=False)
    print('EXPORTED', path.name)

for category, destination in [('enemy', 'Enemies'), ('civilian', 'Civilians')]:
    if category == 'civilian':
        runpy.run_path(str(ROOT / 'Tools/normalize_civilian_fbx.py'), run_name='__main__')
        continue
    folder = OUTPUT / destination
    folder.mkdir(parents=True, exist_ok=True)
    for source in sorted((ROOT / 'Assets/3d' / category).glob('*.glb')):
        load(source)
        textures(folder / 'Textures', source.stem)
        export(folder / (source.stem + '.fbx'), list(bpy.context.scene.objects))

load(ROOT / 'Assets/3d/cars/asset_of_low-poly_cars_part_2.glb')
folder = OUTPUT / 'Cars'
textures(folder / 'Textures', 'cars')
# These meshes are complete vehicles. Lamborghini/Mustang bodies have separate
# wheels and must not be exported as incomplete cars or as the whole showroom.
for obj in list(bpy.context.scene.objects):
    if obj.type != 'MESH' or not obj.name.lower().startswith(('audi', 'mazda', 'ford', 'bmw')):
        continue
    world = obj.matrix_world.copy()
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)
    obj.data = obj.data.copy()
    obj.data.transform(world)
    # The pack's long axis is X; orient each vehicle along Blender -Y (Unity +Z).
    obj.data.transform(Matrix.Rotation(-math.pi / 2, 4, 'Z'))
    points = [v.co for v in obj.data.vertices]
    low = Vector(tuple(min(v[i] for v in points) for i in range(3)))
    high = Vector(tuple(max(v[i] for v in points) for i in range(3)))
    center = (low + high) / 2
    obj.data.transform(Matrix.Translation(Vector((-center.x, -center.y, -low.z))))
    export(folder / (obj.name.split('_Material')[0] + '.fbx'), [obj])
