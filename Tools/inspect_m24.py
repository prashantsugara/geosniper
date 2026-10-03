import bpy
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Resources/Models/Weapons/M24Tactical.fbx'))
print('M24_INSPECTION',json.dumps([{'name':o.name,'size':list(o.dimensions),'materials':[m.name if m else None for m in o.data.materials]} for o in bpy.context.selected_objects if o.type=='MESH']))
