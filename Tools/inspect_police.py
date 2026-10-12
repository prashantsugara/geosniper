import bpy,json
before=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=r'C:/Users/Geeta Sugara/Documents/ChatGPT/sniper2/Assets/Resources/Models/PoliceCar.fbx')
for o in set(bpy.data.objects)-before:
 if o.type=='MESH':print(json.dumps({'name':o.name,'materials':[{'name':m.name,'color':list(m.diffuse_color)} for m in o.data.materials if m],'colors':[a.name for a in o.data.color_attributes]}))
for o in set(bpy.data.objects)-before:bpy.data.objects.remove(o,do_unlink=True)
