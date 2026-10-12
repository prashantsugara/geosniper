import bpy,json
for name in ['tree','leaves','tree.001','leaves.002']:
 obj=bpy.data.objects[name]
 uv=obj.data.uv_layers.active
 print(json.dumps({'object':name,'uv':[[min(d.uv[i] for d in uv.data),max(d.uv[i] for d in uv.data)] for i in range(2)] if uv else None,'materials':[{'name':m.name,'nodes':[{'type':n.type,'image':n.image.name if n.type=='TEX_IMAGE' and n.image else None,'inputs':[(s.name,str(s.default_value)) for s in n.inputs if hasattr(s,'default_value')],'links':[(l.from_node.type,l.to_socket.name) for l in n.inputs for l in l.links]} for n in m.node_tree.nodes]} for m in obj.data.materials if m and m.use_nodes]}))
