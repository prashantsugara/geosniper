using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        bool HasInteriorRoofRoute(Vector3 point)
        {
            var local=transform.InverseTransformPoint(point);
            foreach(var pair in Buildings)
                if(pair.Value!=null && pair.Value.GetComponentInChildren<EnterableBuilding>() is EnterableBuilding interior && interior.HasRoofAccess &&
                    IsPointInPolygon(new Vector2(local.x,local.z),pair.Key.Points))return true;
            return false;
        }
        // Subtract the stairwell from triangulated mapped floors, retaining concave outlines.
        void BuildInteriorFloor(Transform root,List<Vector2> polygon,Rect? opening,float y,Material surface,Material ceiling=null)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();var underside=new List<int>();var uv=new List<Vector2>();var triangles=Triangulate(polygon);
            System.Action<List<Vector2>> append=points=>
            {
                for(int p=1;p+1<points.Count;p++)
                {
                    Vector3 a=new Vector3(points[0].x,y,points[0].y),b=new Vector3(points[p].x,y,points[p].y),c=new Vector3(points[p+1].x,y,points[p+1].y);
                    if(Vector3.Cross(b-a,c-a).y<0){var temp=b;b=c;c=temp;}
                    if(Vector3.Cross(b-a,c-a).sqrMagnitude<.000001f)continue;
                    int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(start);indices.Add(start+1);indices.Add(start+2);
                    uv.Add(new Vector2(a.x,a.z)*.2f);uv.Add(new Vector2(b.x,b.z)*.2f);uv.Add(new Vector2(c.x,c.z)*.2f);
                    // Separate underside vertices give the room a visible, solid ceiling.
                    start=vertices.Count;vertices.Add(a-Vector3.up*.18f);vertices.Add(c-Vector3.up*.18f);vertices.Add(b-Vector3.up*.18f);
                    underside.Add(start);underside.Add(start+1);underside.Add(start+2);
                    uv.Add(new Vector2(a.x,a.z)*.2f);uv.Add(new Vector2(c.x,c.z)*.2f);uv.Add(new Vector2(b.x,b.z)*.2f);
                }
            };
            for(int t=0;t<triangles.Count;t+=3)
            {
                var triangle=new List<Vector2>{polygon[triangles[t]],polygon[triangles[t+1]],polygon[triangles[t+2]]};
                if(!opening.HasValue){append(triangle);continue;}var h=opening.Value;
                append(ClipFloor(triangle,0,h.xMin,false));append(ClipFloor(triangle,0,h.xMax,true));
                var middle=ClipFloor(ClipFloor(triangle,0,h.xMin,true),0,h.xMax,false);
                append(ClipFloor(middle,1,h.yMin,false));append(ClipFloor(middle,1,h.yMax,true));
            }
            var mesh=new Mesh{name="Mapped interior floor"};mesh.SetVertices(vertices);mesh.subMeshCount=2;mesh.SetTriangles(indices,0);mesh.SetTriangles(underside,1);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var floor=new GameObject(y==0?"Interior floor":"Upper floor / roof");floor.transform.SetParent(root,false);floor.AddComponent<MeshFilter>().sharedMesh=mesh;floor.AddComponent<MeshRenderer>().sharedMaterials=new[]{surface,ceiling??surface};floor.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        static List<Vector2> ClipFloor(List<Vector2> source,int axis,float boundary,bool greater)
        {
            var result=new List<Vector2>();if(source.Count==0)return result;
            var previous=source[source.Count-1];bool prevInside=greater?previous[axis]>=boundary:previous[axis]<=boundary;
            foreach(var next in source)
            {
                bool inside=greater?next[axis]>=boundary:next[axis]<=boundary;
                if(inside!=prevInside){float fraction=(boundary-previous[axis])/(next[axis]-previous[axis]);result.Add(Vector2.Lerp(previous,next,fraction));}
                if(inside)result.Add(next);previous=next;prevInside=inside;
            }
            return result;
        }
    }
}
