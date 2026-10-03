"""Inventory the supplied city packs without modifying source assets."""
import json
from pathlib import Path
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parents[1]
report = []
for source in sorted((root / 'Assets/3d/building').glob('*.glb')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))
    bpy.context.view_layer.update()
    items = []
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH':
            continue
        corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
        low = [min(c[i] for c in corners) for i in range(3)]
        high = [max(c[i] for c in corners) for i in range(3)]
        items.append(dict(name=obj.name, parent=obj.parent.name if obj.parent else None,
                          dimensions=[round(high[i]-low[i],3) for i in range(3)],
                          triangles=sum(len(p.vertices)-2 for p in obj.data.polygons),
                          materials=[m.name for m in obj.data.materials if m]))
    report.append(dict(source=source.name, meshes=items,
                       images=[dict(name=i.name,size=list(i.size)) for i in bpy.data.images]))
output = root / 'Logs/world-asset-inventory.json'
output.parent.mkdir(exist_ok=True)
output.write_text(json.dumps(report,indent=2))
print('INVENTORY',output)
