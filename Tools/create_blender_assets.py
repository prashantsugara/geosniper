import bpy
import os
import math
from mathutils import Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Models")
os.makedirs(OUT, exist_ok=True)

def mat(name, color, metallic=0.0, roughness=0.45):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1.0)
    m.metallic = metallic
    m.roughness = roughness
    return m

BLACK = mat("Weapon matte black", (0.025, 0.035, 0.045), 0.72, 0.25)
DARK = mat("Weapon dark polymer", (0.08, 0.10, 0.095), 0.18, 0.38)
GREEN = mat("Weapon olive polymer", (0.18, 0.22, 0.12), 0.12, 0.5)
GLASS = mat("Optic glass", (0.015, 0.14, 0.18), 0.45, 0.12)
RED = mat("Enemy jacket", (0.42, 0.045, 0.035), 0.05, 0.6)
SKIN = mat("Enemy skin", (0.48, 0.22, 0.12), 0.0, 0.7)
BOOT = mat("Enemy boots", (0.025, 0.03, 0.035), 0.1, 0.55)
ARMOR = mat("Enemy armor", (0.12, 0.16, 0.18), 0.55, 0.32)
ARMOR_LIGHT = mat("Enemy armor highlights", (0.22, 0.27, 0.28), 0.45, 0.36)
STRAP = mat("Enemy webbing", (0.06, 0.08, 0.07), 0.1, 0.58)
PATCH = mat("Enemy identification patch", (0.86, 0.38, 0.06), 0.05, 0.55)

def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

def collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c

def move_to(obj, c):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    c.objects.link(obj)

def cube(name, loc, scale, material, c, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        b = o.modifiers.new("Small edge bevel", "BEVEL")
        b.width = bevel
        b.segments = 2
    o.data.materials.append(material)
    move_to(o, c)
    return o

def cylinder(name, loc, radius, depth, material, c, rotation=(0, 0, 0), vertices=12):
    # Weapon tubes run along the authored Z axis, not the vertical Y axis.
    rotation = (0, 0, 0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    move_to(o, c)
    return o

def sphere(name, loc, scale, material, c):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(material)
    move_to(o, c)
    return o

def empty(name, c):
    o = bpy.data.objects.new(name, None)
    c.objects.link(o)
    return o

def parent(o, p):
    o.parent = p
    o.matrix_parent_inverse = p.matrix_world.inverted()

def rifle():
    c = collection("GeoSniper Rifle")
    r = empty("GeoSniper_Rifle", c)
    pieces = [
        cube("Receiver", (0, 0, .10), (.15, .13, .34), BLACK, c, .035),
        cube("Stock", (0, -.02, -.28), (.12, .16, .27), GREEN, c, .045),
        cube("Cheek rest", (0, .11, -.22), (.11, .045, .15), DARK, c, .025),
        cube("Foregrip", (0, -.06, .39), (.09, .12, .20), DARK, c, .03),
        cylinder("Barrel", (0, .025, .67), .035, .58, BLACK, c, (math.pi / 2, 0, 0)),
        cylinder("Muzzle brake", (0, .025, .96), .065, .13, BLACK, c, (math.pi / 2, 0, 0)),
        cube("Magazine", (0, -.16, .08), (.09, .18, .12), BLACK, c, .025),
        cube("Trigger guard", (0, -.14, -.08), (.075, .055, .11), BLACK, c, .02),
        cube("Scope mount", (0, .14, .08), (.075, .045, .16), BLACK, c, .02),
        cylinder("Scope body", (0, .23, .08), .105, .38, BLACK, c, (math.pi / 2, 0, 0), 16),
        cylinder("Scope objective", (0, .23, .31), .14, .08, BLACK, c, (math.pi / 2, 0, 0), 16),
        cylinder("Scope lens", (0, .23, .355), .105, .012, GLASS, c, (math.pi / 2, 0, 0), 16),
        cylinder("Scope eyepiece", (0, .23, -.14), .12, .08, BLACK, c, (math.pi / 2, 0, 0), 16),
    ]
    for o in pieces:
        parent(o, r)
    return r, c

def enemy():
    c = collection("GeoSniper Enemy")
    e = empty("GeoSniper_Enemy", c)
    pieces = [
        cube("Torso jacket", (0, 1.00, 0), (.31, .48, .20), RED, c, .08),
        cube("Lower jacket", (0, .67, 0), (.27, .18, .18), RED, c, .05),
        cube("Chest plate", (0, 1.16, -.20), (.25, .20, .035), ARMOR, c, .03),
        cube("Chest plate highlight", (0, 1.24, -.24), (.15, .07, .025), ARMOR_LIGHT, c, .02),
        cube("Left shoulder pad", (-.38, 1.28, 0), (.15, .12, .20), ARMOR, c, .04),
        cube("Right shoulder pad", (.38, 1.28, 0), (.15, .12, .20), ARMOR, c, .04),
        cube("Left chest pouch", (-.18, .93, -.23), (.09, .10, .04), ARMOR_LIGHT, c, .02),
        cube("Right chest pouch", (.18, .93, -.23), (.09, .10, .04), ARMOR_LIGHT, c, .02),
        cube("Waist belt", (0, .73, -.02), (.30, .07, .22), STRAP, c, .025),
        cube("Identification patch", (-.27, 1.38, -.22), (.07, .07, .025), PATCH, c, .01),
        cylinder("Neck", (0, 1.47, 0), .11, .18, SKIN, c),
        sphere("Head", (0, 1.75, 0), (.22, .26, .22), SKIN, c),
        cube("Helmet shell", (0, 1.98, 0), (.26, .14, .25), ARMOR, c, .05),
        cube("Helmet side rail", (-.27, 1.94, 0), (.025, .09, .20), ARMOR_LIGHT, c, .01),
        cube("Helmet visor", (0, 1.86, -.22), (.19, .08, .025), DARK, c, .02),
        cube("Left upper arm", (-.43, 1.10, 0), (.11, .27, .12), RED, c, .05),
        cube("Right upper arm", (.43, 1.10, 0), (.11, .27, .12), RED, c, .05),
        cube("Left forearm", (-.28, .83, -.02), (.10, .25, .11), ARMOR, c, .04),
        cube("Right forearm", (.28, .83, -.02), (.10, .25, .11), ARMOR, c, .04),
        sphere("Left glove", (-.16, .70, .02), (.11, .10, .12), BOOT, c),
        sphere("Right glove", (.16, .70, .02), (.11, .10, .12), BOOT, c),
        cube("Left leg", (-.15, .35, 0), (.13, .37, .14), BOOT, c, .04),
        cube("Right leg", (.15, .35, 0), (.13, .37, .14), BOOT, c, .04),
        cube("Left knee pad", (-.15, .47, -.15), (.15, .09, .04), ARMOR_LIGHT, c, .025),
        cube("Right knee pad", (.15, .47, -.15), (.15, .09, .04), ARMOR_LIGHT, c, .025),
        cube("Left boot", (-.15, .06, -.06), (.14, .08, .23), BOOT, c, .03),
        cube("Right boot", (.15, .06, -.06), (.14, .08, .23), BOOT, c, .03),
        cube("Backpack", (0, 1.08, .25), (.25, .35, .09), ARMOR, c, .04),
        cube("Backpack top", (0, 1.40, .25), (.18, .08, .08), ARMOR_LIGHT, c, .025),
        cube("Left backpack strap", (-.19, 1.17, -.18), (.035, .34, .025), STRAP, c, .01),
        cube("Right backpack strap", (.19, 1.17, -.18), (.035, .34, .025), STRAP, c, .01),
    ]
    for o in pieces:
        parent(o, e)
    # Include the weapon in the authored asset so every patrol has the same readable silhouette.
    rifle_parts = [
        cube("Enemy rifle receiver", (0, .78, .06), (.11, .10, .24), BLACK, c, .03),
        cube("Enemy rifle stock", (0, .78, -.19), (.09, .09, .22), GREEN, c, .03),
        cube("Enemy rifle foregrip", (0, .70, .25), (.07, .11, .10), DARK, c, .025),
        cylinder("Enemy rifle barrel", (0, .79, .40), .032, .34, BLACK, c, vertices=12),
        cylinder("Enemy rifle muzzle brake", (0, .79, .58), .05, .08, BLACK, c, vertices=12),
        cylinder("Enemy rifle optic", (0, .90, .08), .055, .20, BLACK, c, vertices=12),
        cube("Enemy rifle magazine", (0, .60, .05), (.06, .12, .07), BLACK, c, .02),
    ]
    for o in rifle_parts:
        parent(o, e)
    return e, c

def export(root, c, path):
    # Authoring helpers use Y-up. Bake into Blender's Z-up space before FBX export.
    conversion = Matrix.Rotation(math.pi / 2, 4, 'X')
    for obj in c.objects:
        if obj.type == 'MESH':
            obj.matrix_world = conversion @ obj.matrix_world
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    for o in c.objects:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"EMPTY", "MESH"}, apply_unit_scale=True, add_leaf_bones=False, use_mesh_modifiers=True, path_mode="AUTO")

clear()
r, rc = rifle()
e, ec = enemy()
export(r, rc, os.path.join(OUT, "GeoSniperRifle.fbx"))
export(e, ec, os.path.join(OUT, "GeoSniperEnemy.fbx"))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "GeoSniperAssets.blend"))
print("GeoSniper Blender assets created")
