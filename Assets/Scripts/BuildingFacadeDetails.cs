using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    // Small, batched architectural kit. Coordinates and the original collision shell stay intact.
    public static class BuildingFacadeDetails
    {
        // Prefer a facade that faces a nearby street; fall back to the longest wall
        // when map data does not provide a street beside the footprint.
        public static int EntranceEdge(MapFeature building,List<MapFeature> surroundings)
        {
            if(building==null || building.Points.Count<3 || MapFeatureStyle.Number(building,"min_height",0)>.3f) return -1;
            float area=0,longest=0,bestRoad=float.MaxValue;
            int selected=-1;
            for(int i=0;i<building.Points.Count;i++)
            {
                var a=building.Points[i];var b=building.Points[(i+1)%building.Points.Count];
                area+=a.x*b.y-b.x*a.y;
                float length=(b-a).magnitude;
                if(length>longest && length>=3.5f){longest=length;selected=i;}
            }
            if(surroundings==null)return selected;
            int fallback=selected;
            for(int edge=0;edge<building.Points.Count;edge++)
            {
                var a=building.Points[edge];var b=building.Points[(edge+1)%building.Points.Count];
                float length=(b-a).magnitude;if(length<3.5f)continue;
                var midpoint=(a+b)*.5f;
                var along=(b-a)/length;
                var outward=new Vector2(along.y,-along.x)*(area>0?1:-1);
                foreach(var road in surroundings)
                {
                    if(road.Kind!="road")continue;
                    for(int segment=1;segment<road.Points.Count;segment++)
                    {
                        var start=road.Points[segment-1];var delta=road.Points[segment]-start;
                        float fraction=delta.sqrMagnitude>.001f?Mathf.Clamp01(Vector2.Dot(midpoint-start,delta)/delta.sqrMagnitude):0;
                        var closest=start+delta*fraction;
                        if(Vector2.Dot(closest-midpoint,outward)<.5f)continue;
                        float distance=(closest-midpoint).magnitude;
                        if(distance>22f || distance>=bestRoad)continue;
                        bestRoad=distance;selected=edge;
                    }
                }
            }
            return bestRoad<float.MaxValue?selected:fallback;
        }

        public static Mesh Build(MapFeature feature, List<MapFeature> surroundings)
        {
            if(feature.Kind!="building" || feature.Points.Count<3 || feature.Height<6) return null;
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            int count=0;
            float storey=MapFeatureStyle.StoreyHeight(feature);
            float area=0;
            int ladderEdge=0; float longest=0;
            for(int i=0;i<feature.Points.Count;i++)
            { var a=feature.Points[i]; var b=feature.Points[(i+1)%feature.Points.Count]; area+=a.x*b.y-b.x*a.y;
                if((b-a).sqrMagnitude>longest) {longest=(b-a).sqrMagnitude;ladderEdge=i;} }
            for(int edge=0;edge<feature.Points.Count && count<24;edge++)
            {
                Vector2 a=feature.Points[edge],b=feature.Points[(edge+1)%feature.Points.Count];
                float length=Vector2.Distance(a,b); if(length<4) continue;
                Vector2 along=(b-a)/length;
                Vector2 outward=new Vector2(along.y,-along.x)*(area>0?1:-1);
                var rotation=Quaternion.LookRotation(new Vector3(outward.x,0,outward.y));
                int bays=Mathf.Clamp(Mathf.FloorToInt(length/4),1,6);
                for(int bay=0;bay<bays && count<24;bay++)
                {
                    Vector2 p=Vector2.Lerp(a,b,(bay+.5f)/bays);
                    // Only the actual ladder edge needs a midpoint corridor.
                    if(edge==ladderEdge && Vector2.Distance(p,(a+b)*.5f)<2.2f) continue;
                    // Reject additions near roads or neighbouring footprints, including courtyards.
                    if(!Clear(p+outward*.6f,feature,surroundings,1.7f)) continue;
                    for(float y=storey;y<feature.Height-2.4f && count<24;y+=storey)
                    {
                        Vector3 origin=new Vector3(p.x,y,p.y);
                        Box(vertices,triangles,origin,rotation,new Vector3(0,0,.48f),new Vector3(2.5f,.16f,1.05f));
                        Box(vertices,triangles,origin,rotation,new Vector3(0,.88f,.96f),new Vector3(2.5f,.07f,.07f));
                        for(int post=0;post<6;post++)
                            Box(vertices,triangles,origin,rotation,new Vector3(-1.2f+post*.48f,.46f,.96f),new Vector3(.045f,.88f,.045f));
                        foreach(float side in new[]{-1f,1f})
                        {
                            Box(vertices,triangles,origin,rotation,new Vector3(side*1.2f,.88f,.48f),new Vector3(.07f,.07f,.96f));
                            Box(vertices,triangles,origin,rotation,new Vector3(side*.86f,1.25f,.045f),new Vector3(.12f,2.5f,.14f));
                        }
                        Box(vertices,triangles,origin,rotation,new Vector3(0,2.5f,.045f),new Vector3(1.84f,.14f,.14f));
                        count++;
                    }
                }
            }
            if(vertices.Count==0) return null;
            var mesh=new Mesh {name="Batched balcony slabs rails and window surrounds"};
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // Window panels have an opaque dark border, warm occupied rooms, and specular reflective glass.
        // Three materials in one batched building mesh for realistic architectural depth.
        public static Mesh BuildWindows(MapFeature f,int entranceEdge=-2,bool storefront=false)
        {
            if(f.Kind!="building" || f.Points.Count<3 || f.Height<3) return null;
            if(entranceEdge==-2)entranceEdge=EntranceEdge(f,null);
            var v=new List<Vector3>(); var frame=new List<int>(); var lit=new List<int>(); var glass=new List<int>();var uv=new List<Vector2>();
            float area=0;
            for(int i=0;i<f.Points.Count;i++) {var a=f.Points[i];var b=f.Points[(i+1)%f.Points.Count];area+=a.x*b.y-b.x*a.y;}
            int rooms=0;
            float storey=MapFeatureStyle.StoreyHeight(f);
            for(int edge=0;edge<f.Points.Count && rooms<144;edge++)
            {
                var a=f.Points[edge];var b=f.Points[(edge+1)%f.Points.Count];
                float length=Vector2.Distance(a,b); if(length<2.5f) continue;
                var along=(b-a)/length; var outward=new Vector2(along.y,-along.x)*(area>0?1:-1);
                var rotation=Quaternion.LookRotation(new Vector3(outward.x,0,outward.y));
                int bays=Mathf.Clamp(Mathf.FloorToInt(length/4f),1,6);
                for(float y=1.25f;y<f.Height-.8f && rooms<144;y+=storey)
                    for(int bay=0;bay<bays && rooms<144;bay++,rooms++)
                    {
                        var p=Vector2.Lerp(a,b,(bay+.5f)/bays);
                        var origin=new Vector3(p.x,y,p.y);
                        if(edge==entranceEdge && bay==bays/2 && y<1.26f)
                        {
                            // A closed, glazed ground-floor entrance replaces one repeated window.
                            Box(v,frame,origin,rotation,new Vector3(0,0,.09f),new Vector3(1.9f,2.4f,.13f),uv);
                            Box(v,glass,origin,rotation,new Vector3(0,0,.18f),new Vector3(1.58f,2.13f,.035f),uv);
                            Box(v,frame,origin,rotation,new Vector3(0,.81f,.21f),new Vector3(1.58f,.06f,.05f),uv);
                            Box(v,frame,origin,rotation,new Vector3(.55f,-.1f,.23f),new Vector3(.055f,.34f,.055f),uv);
                            continue;
                        }
                        if(storefront && y<1.26f)
                        {
                            // Shopfront bays are taller and wider than upper-floor rooms.
                            float width=Mathf.Min(2.55f,length/bays*.86f);
                            Box(v,frame,origin,rotation,new Vector3(0,0,.07f),new Vector3(width,2.4f,.12f),uv);
                            Box(v,glass,origin,rotation,new Vector3(0,0,.15f),new Vector3(width-.27f,2.12f,.035f),uv);
                            Box(v,frame,origin,rotation,new Vector3(0,.86f,.18f),new Vector3(width-.27f,.07f,.05f),uv);
                            continue;
                        }
                        // Outer frame
                        Box(v,frame,origin,rotation,new Vector3(0,0,.045f),new Vector3(1.65f,1.85f,.08f),uv);
                        bool occupied=(bay+edge*3+Mathf.FloorToInt(y)*7)%5<2;
                        // Glass pane: occupied room interior or exterior reflective glass
                        Box(v,occupied?lit:glass,origin,rotation,new Vector3(0,0,.095f),new Vector3(1.49f,1.69f,.025f),uv,Mathf.FloorToInt(Mathf.Abs(p.x*7+p.y*13+y*17))%4);
                        // Center mullion
                        Box(v,frame,origin,rotation,new Vector3(0,0,.12f),new Vector3(.04f,1.7f,.035f),uv);
                        if((edge+bay)%3==0)
                            Box(v,frame,origin,rotation,new Vector3(0,.36f,.12f),new Vector3(1.49f,.035f,.035f),uv);
                    }
            }
            if(v.Count==0) return null;
            var mesh=new Mesh {name="Recessed frames and illuminated rooms",subMeshCount=3};
            mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(frame,0);mesh.SetTriangles(lit,1);mesh.SetTriangles(glass,2);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }

        // A single mesh per nearby building: visible depth without hundreds of objects.
        public static Mesh BuildArchitecture(MapFeature f,int entranceEdge=-2,bool storefront=false)
        {
            if(f.Kind!="building" || f.Points.Count<3 || f.Height<3) return null;
            if(entranceEdge==-2)entranceEdge=EntranceEdge(f,null);
            var v=new List<Vector3>(); var t=new List<int>();
            float area=0;
            for(int i=0;i<f.Points.Count;i++) {var a=f.Points[i];var b=f.Points[(i+1)%f.Points.Count];area+=a.x*b.y-b.x*a.y;}
            float storey=MapFeatureStyle.StoreyHeight(f);
            int rooms=0;
            int ladderEdge=0; float longest=0;
            for(int i=0;i<f.Points.Count;i++)
            {
                var pa=f.Points[i];var pb=f.Points[(i+1)%f.Points.Count];
                if((pb-pa).sqrMagnitude>longest){longest=(pb-pa).sqrMagnitude;ladderEdge=i;}
            }
            for(int edge=0;edge<f.Points.Count && edge<32;edge++)
            {
                var a=f.Points[edge];var b=f.Points[(edge+1)%f.Points.Count];
                float length=Vector2.Distance(a,b);if(length<2.5f) continue;
                var along=(b-a)/length;var outward=new Vector2(along.y,-along.x)*(area>0?1:-1);
                var rotation=Quaternion.LookRotation(new Vector3(outward.x,0,outward.y));
                var mid=(a+b)*.5f;var origin=new Vector3(mid.x,0,mid.y);

                // Ground foundation plinth course
                Box(v,t,origin,rotation,new Vector3(0,.42f,.03f),new Vector3(length,.84f,.12f));
                Box(v,t,origin,rotation,new Vector3(0,.86f,.05f),new Vector3(length,.08f,.18f));
                // Exterior wall cornice trim
                Box(v,t,origin,rotation,new Vector3(0,f.Height-.12f,.025f),new Vector3(length,.18f,.15f));

                // Architectural Roof Parapet & Coping Cap (depth & solid silhouette on sky)
                if(edge!=ladderEdge)
                {
                    Box(v,t,origin,rotation,new Vector3(0,f.Height+.32f,-.10f),new Vector3(length,.65f,.28f));
                    Box(v,t,origin,rotation,new Vector3(0,f.Height+.68f,-.10f),new Vector3(length,.12f,.36f));
                }
                else
                {
                    // Leave 2.2m clearance opening for ladder landing
                    float segLen=(length-2.2f)*.5f;
                    if(segLen>.6f)
                    {
                        Vector3 leftOff=new Vector3(along.x,0,along.y)*(-length*.5f+segLen*.5f);
                        Vector3 rightOff=new Vector3(along.x,0,along.y)*(length*.5f-segLen*.5f);
                        Box(v,t,origin+leftOff,rotation,new Vector3(0,f.Height+.32f,-.10f),new Vector3(segLen,.65f,.28f));
                        Box(v,t,origin+rightOff,rotation,new Vector3(0,f.Height+.32f,-.10f),new Vector3(segLen,.65f,.28f));
                        Box(v,t,origin+leftOff,rotation,new Vector3(0,f.Height+.68f,-.10f),new Vector3(segLen,.12f,.36f));
                        Box(v,t,origin+rightOff,rotation,new Vector3(0,f.Height+.68f,-.10f),new Vector3(segLen,.12f,.36f));
                    }
                }

                for(float y=storey-.3f;y<f.Height-.5f;y+=storey)
                    Box(v,t,origin,rotation,new Vector3(0,y,.015f),new Vector3(length,.07f,.075f));
                int bays=Mathf.Clamp(Mathf.FloorToInt(length/4f),1,6);
                if(storefront)
                    Box(v,t,origin,rotation,new Vector3(0,2.57f,.27f),new Vector3(length,.28f,.62f));
                else if(edge==entranceEdge)
                {
                    var entry=Vector2.Lerp(a,b,(bays/2+.5f)/bays);
                    Box(v,t,new Vector3(entry.x,0,entry.y),rotation,new Vector3(0,2.55f,.37f),new Vector3(2.15f,.14f,.72f));
                }
                for(float y=1.25f;y<f.Height-.8f && rooms<144;y+=storey)
                    for(int bay=0;bay<bays && rooms<144;bay++,rooms++)
                    {
                        if(y<1.26f && (storefront || (edge==entranceEdge && bay==bays/2)))continue;
                        var p=Vector2.Lerp(a,b,(bay+.5f)/bays);var o=new Vector3(p.x,y,p.y);
                        Box(v,t,o,rotation,new Vector3(-.85f,0,.075f),new Vector3(.065f,1.96f,.15f));
                        Box(v,t,o,rotation,new Vector3(.85f,0,.075f),new Vector3(.065f,1.96f,.15f));
                        Box(v,t,o,rotation,new Vector3(0,.95f,.075f),new Vector3(1.77f,.065f,.15f));
                        Box(v,t,o,rotation,new Vector3(0,-.95f,.12f),new Vector3(1.85f,.095f,.24f));
                    }
            }

            // Rooftop elevator / stairwell service bulkhead and mechanical equipment
            if(f.Height>=6f)
            {
                Vector2 centroid=Vector2.zero;
                foreach(var pt in f.Points) centroid+=pt;
                centroid/=f.Points.Count;
                Vector3 bulkOrigin=new Vector3(centroid.x,f.Height,centroid.y);

                // Service bulkhead machine room
                Box(v,t,bulkOrigin,Quaternion.identity,new Vector3(0,1.2f,0),new Vector3(3.6f,2.4f,4.2f));
                Box(v,t,bulkOrigin,Quaternion.identity,new Vector3(0,2.46f,0),new Vector3(3.9f,.14f,4.5f));
                Box(v,t,bulkOrigin,Quaternion.identity,new Vector3(0,1.0f,2.14f),new Vector3(1.1f,2.0f,.08f));
                Box(v,t,bulkOrigin,Quaternion.identity,new Vector3(0,2.05f,2.22f),new Vector3(1.3f,.08f,.24f));

                // Communication antenna mast
                Box(v,t,bulkOrigin,Quaternion.identity,new Vector3(.9f,3.6f,.9f),new Vector3(.08f,2.2f,.08f));
                Box(v,t,bulkOrigin,Quaternion.identity,new Vector3(.9f,4.3f,.9f),new Vector3(.65f,.05f,.05f));

                // Rooftop HVAC condenser units and ductwork
                if(area>60f)
                {
                    Vector3 hvac1=new Vector3(3.5f,0.65f,-0.8f);
                    Box(v,t,bulkOrigin,Quaternion.identity,hvac1,new Vector3(1.8f,1.3f,1.5f));
                    Box(v,t,bulkOrigin,Quaternion.identity,hvac1+Vector3.up*.7f,new Vector3(1.1f,.12f,1.1f));

                    Vector3 hvac2=new Vector3(3.5f,0.65f,1.3f);
                    Box(v,t,bulkOrigin,Quaternion.identity,hvac2,new Vector3(1.8f,1.3f,1.5f));
                    Box(v,t,bulkOrigin,Quaternion.identity,hvac2+Vector3.up*.7f,new Vector3(1.1f,.12f,1.1f));

                    Box(v,t,bulkOrigin,Quaternion.identity,new Vector3(2.3f,0.45f,0.25f),new Vector3(1.4f,0.6f,0.7f));
                }

                // Rooftop water storage tank on steel trestle frame
                if(f.Height>=14f && area>80f)
                {
                    Vector3 tankOrigin=bulkOrigin+new Vector3(-3.8f,0,0);
                    foreach(float sx in new[]{-.85f,.85f})
                        foreach(float sz in new[]{-.85f,.85f})
                            Box(v,t,tankOrigin,Quaternion.identity,new Vector3(sx,0.9f,sz),new Vector3(.10f,1.8f,.10f));
                    Box(v,t,tankOrigin,Quaternion.identity,new Vector3(0,1.75f,0),new Vector3(2.0f,.14f,2.0f));
                    Box(v,t,tankOrigin,Quaternion.identity,new Vector3(0,3.0f,0),new Vector3(2.1f,2.4f,2.1f));
                    Box(v,t,tankOrigin,Quaternion.identity,new Vector3(0,4.3f,0),new Vector3(2.35f,.25f,2.35f));
                }
            }

            if(v.Count==0) return null;
            var mesh=new Mesh {name="Architectural sills surrounds plinths and cornices"};
            mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }

        static bool Clear(Vector2 p,MapFeature owner,List<MapFeature> features,float radius)
        {
            foreach(var f in features)
            {
                if(f==owner || (f.Kind!="building" && f.Kind!="road")) continue;
                if(f.Kind=="building" && Inside(p,f.Points)) return false;
                float clearance=radius+(f.Kind=="road"?MapFeatureStyle.RoadWidth(f)*.5f:0);
                int edges=f.Kind=="road"?f.Points.Count-1:f.Points.Count;
                for(int i=0;i<edges;i++)
                {
                    Vector2 a=f.Points[i],d=f.Points[(i+1)%f.Points.Count]-a;
                    float t=d.sqrMagnitude>.0001f?Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude):0;
                    if((p-a-d*t).sqrMagnitude<clearance*clearance) return false;
                }
            }
            return true;
        }
        static bool Inside(Vector2 p,List<Vector2> polygon)
        {
            bool inside=false;
            for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
            {
                var a=polygon[i]; var b=polygon[j];
                if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
            }
            return inside;
        }
        static void Box(List<Vector3> v,List<int> t,Vector3 origin,Quaternion rotation,Vector3 center,Vector3 size,List<Vector2> uv=null,int roomStyle=0)
        {
            // Separate vertices per face retain crisp architectural normals.
            int[] faces={0,2,3,1,4,5,7,6,0,4,6,2,1,3,7,5,0,1,5,4,2,6,7,3};
            for(int face=0;face<6;face++)
            {
                int start=v.Count;
                for(int k=0;k<4;k++)
                {
                    int c=faces[face*4+k];
                    var corner=new Vector3((c&1)==0?-.5f:.5f,(c&2)==0?-.5f:.5f,(c&4)==0?-.5f:.5f);
                    v.Add(origin+rotation*(center+Vector3.Scale(corner,size)));
                    if(uv!=null)uv.Add(new Vector2((roomStyle+.02f+(corner.x+.5f)*.96f)/4f,.02f+(corner.y+.5f)*.96f));
                }
                t.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
        }
    }
}
