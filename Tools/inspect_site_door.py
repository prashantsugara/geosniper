import bpy,json,math
from mathutils import Matrix,Vector
obj=bpy.data.objects['Material2.007'];points=[obj.matrix_world@v.co for v in obj.data.vertices];lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)]);f=14/(hi.x-lo.x);t=Matrix.Rotation(math.pi,4,'Z')@Matrix.Diagonal((f,f,f,1))@Matrix.Translation(Vector((-(lo.x+hi.x)/2,-(lo.y+hi.y)/2,-lo.z)))
rows=[]
for face in obj.data.polygons:
 vs=[t@(obj.matrix_world@obj.data.vertices[i].co) for i in face.vertices];center=sum(vs,Vector())/len(vs)
 if abs(center.x)<1.5 and center.z<1.85 and center.y>1.8:
  rows.append({'index':face.index,'min':[min(v[i] for v in vs) for i in range(3)],'max':[max(v[i] for v in vs) for i in range(3)]})
print(json.dumps(rows[:40]))


