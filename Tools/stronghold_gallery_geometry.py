"""Authored open gallery extension. Blender Z-up metres; front is positive Y.

The licensed Afghan stair-bearing shell remains a separate set of meshes.
Geometry is batched by finish, with actual voids for doors and firing bays.
"""
import math
from mathutils import Vector

def build_gallery(bpy):
    batches={name:[[],[]] for name in ('GalleryStone','GalleryFloor','GalleryWood','GalleryMetal','GalleryDark')}
    def solid(name,vertices,faces):
        verts,polys=batches[name];base=len(verts)
        verts.extend(vertices);polys.extend(tuple(base+i for i in p) for p in faces)
    def box(name,center,size):
        x,y,z=center;a,b,c=(v*.5 for v in size)
        solid(name,[(x-a,y-b,z-c),(x+a,y-b,z-c),(x+a,y+b,z-c),(x-a,y+b,z-c),
                    (x-a,y-b,z+c),(x+a,y-b,z+c),(x+a,y+b,z+c),(x-a,y+b,z+c)],
              [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
    def rod(name,a,b,radius,segments=8):
        a,b=Vector(a),Vector(b);axis=(b-a).normalized()
        side=axis.cross(Vector((0,0,1)))
        if side.length<.01:side=axis.cross(Vector((0,1,0)))
        side.normalize();up=axis.cross(side).normalized()
        vertices=[tuple(p+radius*(side*math.cos(i*math.tau/segments)+up*math.sin(i*math.tau/segments))) for p in (a,b) for i in range(segments)]
        faces=[tuple(range(segments-1,-1,-1)),tuple(range(segments,segments*2))]
        faces.extend((i,(i+1)%segments,(i+1)%segments+segments,i+segments) for i in range(segments))
        solid(name,vertices,faces)
    def arch(cx,y,base,radius=1.40):
        # Separate voussoirs and a genuine open arch, never a painted solid cap.
        spring=base+1.35
        for i in range(14):
            a=i*math.pi/14+.004;b=(i+1)*math.pi/14-.004
            vertices=[(cx+r*math.cos(t), yy, spring+r*math.sin(t))
                      for yy in (y-.23,y+.23) for r,t in ((radius,a),(radius,b),(radius+.23,b),(radius+.23,a))]
            solid('GalleryStone',vertices,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
    def railing(a,b,height):
        # Two rails and spaced balusters leave heads/upper bodies visible.
        box('GalleryWood',((a+b)/2,16.78,height+.90),(b-a,.12,.12))
        box('GalleryWood',((a+b)/2,16.78,height+.36),(b-a,.10,.10))
        count=math.ceil((b-a)/.55)
        for i in range(count+1):box('GalleryWood',(a+(b-a)*i/count,16.78,height+.62),(.08,.10,.60))

    route=[];firing=[];doors=[];floors=[]
    centres=(-4.95,-1.65,1.65,4.95)
    posts=(-6.6,-3.3,0,3.3,6.6)
    for level in range(4):
        h=level*3.05
        # Stone edged floor plate and open circulation behind the galleries.
        box('GalleryFloor',(0,12.55,h+.09),(14.1,8.9,.18))
        box('GalleryStone',(0,17.02,h+.08),(14.5,.42,.30))
        box('GalleryStone',(0,16.95,h-.12),(14.25,.26,.14))
        floors.append((0,15,h+.23))
        if level==3:continue
        floor=h+.18
        for x in posts:
            box('GalleryStone',(x,16.70,floor+.72),(.42,.52,1.44))
            box('GalleryStone',(x,16.70,floor+.09),(.65,.72,.18))
            box('GalleryStone',(x,16.70,floor+1.36),(.62,.70,.16))
            # Rear wall piers form four full-height open doorways.
            box('GalleryStone',(x,9.20,floor+1.43),(.62,.42,2.86))
        for x in centres:
            arch(x,16.70,floor)
            # Rear lintel and deep reveals; no solid window/door insert.
            box('GalleryStone',(x,9.20,floor+2.48),(2.70,.42,.78))
            for side in (-1,1):
                box('GalleryWood',(x+side*1.30,9.45,floor+1.08),(.10,.15,2.16))
                # Folded shutters beside the doorway, rather than across it.
                box('GalleryWood',(x+side*1.05,9.47,floor+.99),(.35,.10,1.72))
                for slat in range(7):box('GalleryWood',(x+side*1.05,9.54,floor+.30+slat*.22),(.34,.04,.065))
            box('GalleryWood',(x,9.45,floor+2.19),(2.70,.15,.13))
            doors.append((x,9.20,floor+1.4))
            if level>0:
                railing(x-1.37,x+1.37,floor)
                firing.append((x,15.65,floor+.08))
        # Side gallery rails and corner buttresses; avoid boxing in the arcade.
        for side in (-1,1):
            x=side*6.80
            box('GalleryStone',(x,12.05,floor+1.43),(.38,3.00,2.86))
            if level>0:
                box('GalleryWood',(x,14.70,floor+.90),(.12,3.9,.12))
                for y in (13.0,14.0,15.0,16.0):box('GalleryWood',(x,y,floor+.49),(.10,.10,.90))
        # Two weathered AC housings under each upper cornice, with real fan grilles.
        if level>0:
            for x in (-5.1,5.1):
                z=floor-.50
                box('GalleryMetal',(x,17.24,z),(.92,.42,.53))
                rod('GalleryDark',(x-.18,17.44,z),(x-.18,17.47,z),.19,12)
                for blade in range(6):
                    t=blade*math.tau/6
                    rod('GalleryMetal',(x-.18,17.48,z),(x-.18+math.cos(t)*.165,17.48,z+math.sin(t)*.165),.019,5)
                for slit in range(5):box('GalleryDark',(x+.26,17.46,z-.16+slit*.075),(.22,.015,.020))
                for side in (-1,1):box('GalleryMetal',(x+side*.30,17.15,z-.34),(.09,.65,.09))
    # Individual offset stone courses make the parapet read as masonry.
    for course in range(2):
        for i in range(15):
            x=-6.65+i*.92+(course%2)*.30
            if x>6.9:continue
            box('GalleryStone',(x,16.78,9.33+.145+course*.29),(.90,.46,.275))
        for side in (-1,1):
            for i in range(8):box('GalleryStone',(side*6.80,9.65+i*.92,9.33+.145+course*.29),(.46,.90,.275))
    # Roof firing positions, behind collidable low parapets.
    firing.extend((x,15.65,9.41) for x in (-4.95,1.65,4.95))
    # Switchback exterior stairs. 19cm risers fit the production controller's step offset.
    # The landings connect each floor, including the roof; no teleport ladder needed.
    box('GalleryFloor',(8.0,16.8,.09),(2.1,1.4,.18))
    route.append((8.0,17.0,.24))
    for level in range(3):
        h=level*3.05;forward=level%2==0;stair_x=8.0 if forward else 9.8
        route.append((stair_x,16.65 if forward else 9.7,h+.24))
        for step in range(16):
            y=16.0-(step+.5)*.36 if forward else 10.24+(step+.5)*.36
            top=h+.18+(step+1)*3.05/16
            # Thin stair blocks leave headroom beneath the next switchback.
            box('GalleryStone',(stair_x,y,top-.15),(1.65,.365,.30))
            route.append((stair_x,y,top+.06))
        y=9.7 if forward else 16.65
        box('GalleryFloor',(8.65,y,(level+1)*3.05+.09),(4.1,1.6,.18))
        route.append((stair_x,y,(level+1)*3.05+.24))
        # Main-gallery connection at the landing (no side wall at these ends).
        box('GalleryFloor',(6.9,y,(level+1)*3.05+.09),(1.25,1.6,.18))
        for side in (-1,1):
            x=stair_x+side*.80
            a=(x,16.0,h+.18+.9) if forward else (x,10.24,h+.18+.9)
            b=(x,10.24,h+3.23+.9) if forward else (x,16.0,h+3.23+.9)
            rod('GalleryMetal',a,b,.035)
        for step in (0,4,8,12,15):
            y=16.0-(step+.5)*.36 if forward else 10.24+(step+.5)*.36
            top=h+.18+(step+1)*3.05/16
            rod('GalleryMetal',(stair_x+.80,y,top),(stair_x+.80,y,top+.9),.028)
    # Pipes and sagging utility cables carry detail without lights or transparency.
    for x in (-6.15,6.15):rod('GalleryMetal',(x,17.12,.3),(x,17.12,8.8),.055,8)
    for height in (4.30,4.45):
        for i in range(8):
            x=-6.4+i*1.6;nextx=x+1.6
            z=height-.35*math.sin(i*math.pi/8);nextz=height-.35*math.sin((i+1)*math.pi/8)
            rod('GalleryDark',(x,17.55,z),(nextx,17.55,nextz),.018,5)
    objects=[]
    for name,(vertices,faces) in batches.items():
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
        # Every solid's faces are outward wound; retain sharp stone/wood normals.
        material=bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mesh.materials.append(material)
        obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);objects.append(obj)
    return objects,{'stairs':route,'firing':firing,'doors':doors,'floors':floors}
