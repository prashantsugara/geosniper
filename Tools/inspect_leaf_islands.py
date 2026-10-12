import bpy,bmesh,json,collections
for name in ['leaves','leaves.002']:
 bm=bmesh.new();bm.from_mesh(bpy.data.objects[name].data);remaining=set(bm.verts);sizes=[]
 while remaining:
  seed=remaining.pop();group={seed};queue=[seed]
  while queue:
   v=queue.pop()
   for edge in v.link_edges:
    other=edge.other_vert(v)
    if other in remaining:remaining.remove(other);group.add(other);queue.append(other)
  faces={f for v in group for f in v.link_faces};sizes.append(sum(len(f.verts)-2 for f in faces))
 print(json.dumps({'name':name,'islands':len(sizes),'histogram':collections.Counter(sizes).most_common(15)}));bm.free()
