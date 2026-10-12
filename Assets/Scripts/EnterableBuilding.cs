using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed class EnterableBuilding : MonoBehaviour
    {
        static readonly List<EnterableBuilding> instances=new List<EnterableBuilding>();
        public static IReadOnlyList<EnterableBuilding> All=>instances;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetInstances()=>instances.Clear();
        void OnEnable(){if(!instances.Contains(this))instances.Add(this);}
        void OnDisable()=>instances.Remove(this);
        public Vector3 Entrance;
        public int Floors;
        public float FloorHeight=3f;
        public readonly List<Vector2> Footprint=new List<Vector2>();
        public int FloorAt(Vector3 world)=>Mathf.Clamp(Mathf.FloorToInt((transform.InverseTransformPoint(world).y+.25f)/FloorHeight),0,Mathf.Max(0,Floors-1));
        public bool Contains(Vector3 world){var p=transform.InverseTransformPoint(world);return p.y>=-.5f && p.y<Floors*FloorHeight && Inside(new Vector2(p.x,p.z),Footprint);}
        public bool HasRoofAccess;
        public Vector3 EntranceOutward;
        public Vector3 StairOrigin;
        public readonly List<Vector3> FiringWindows=new List<Vector3>();
        public struct OpenWindow { public Vector3 Center,Outward; public float Width,Sill; }
        public readonly List<OpenWindow> OpenWindows=new List<OpenWindow>();
        public struct EvidenceStation { public Vector3 Standing,Desktop; }
        public readonly List<EvidenceStation> EvidenceStations=new List<EvidenceStation>();
        public static bool FitLobby(MapFeature f,out Vector2 center,out Vector2 along,out Vector2 size)
        {
            center=Vector2.zero; along=Vector2.right; size=Vector2.zero;
            if(f.Kind!="building" || f.Points.Count<3 || f.Height<2.8f) return false;
            float longest=0,area=0;
            for(int i=0;i<f.Points.Count;i++)
            {
                var a=f.Points[i]; var b=f.Points[(i+1)%f.Points.Count]; var edge=b-a;
                if(edge.sqrMagnitude>longest) { longest=edge.sqrMagnitude; along=edge.normalized; }
                area+=a.x*b.y-b.x*a.y;
            }
            Vector2 across=new Vector2(-along.y,along.x),min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue;
            foreach(var p in f.Points)
            {
                var q=new Vector2(Vector2.Dot(p,along),Vector2.Dot(p,across));
                min=Vector2.Min(min,q); max=Vector2.Max(max,q);
            }
            size=max-min; center=along*((min.x+max.x)*.5f)+across*((min.y+max.y)*.5f);
            if(size.x<3 || size.y<3 || WorldDetailPlanner.IsBuildingPart(f) || MapFeatureStyle.Number(f,"min_height",0)>.1f) return false;
            return true;
        }

        public static bool Fit(MapFeature feature,out Vector2 center,out Vector2 along,out Vector2 size)
        {
            if(!MappedBuildingVisuals.TryFit(feature,out center,out along,out size))return false;
            if(feature.Points.Count>32 || WorldDetailPlanner.IsBuildingPart(feature) || feature.Height>24 ||
                MapFeatureStyle.Number(feature,"min_height",0)>.1f || size.x<6 || size.y<6 || size.x>45 || size.y>40)return false;
            return TryStairLayout(Polygon(feature,center,along),size,feature.Height,out _,out _);
        }
        public static List<Vector2> Polygon(MapFeature feature,Vector2 center,Vector2 along)
        {
            var polygon=new List<Vector2>();var across=new Vector2(-along.y,along.x);
            foreach(var point in feature.Points)
            {
                var local=new Vector2(Vector2.Dot(point-center,along),Vector2.Dot(point-center,across));
                if(polygon.Count==0 || (local-polygon[polygon.Count-1]).sqrMagnitude>.001f)polygon.Add(local);
            }
            if(polygon.Count>1 && (polygon[0]-polygon[polygon.Count-1]).sqrMagnitude<.001f)polygon.RemoveAt(polygon.Count-1);
            return polygon;
        }
        public static bool Inside(Vector2 p,List<Vector2> polygon)
        {
            bool inside=false;
            for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
                if((polygon[i].y>p.y)!=(polygon[j].y>p.y) && p.x<(polygon[j].x-polygon[i].x)*(p.y-polygon[i].y)/(polygon[j].y-polygon[i].y)+polygon[i].x)inside=!inside;
            return inside;
        }
        public static bool TryStairLayout(List<Vector2> polygon,Vector2 size,float height,out Vector2 origin,out float run,Vector2? doorway=null)
        {
            origin=Vector2.zero;float storey=height/Mathf.Clamp(Mathf.FloorToInt(height/3),1,6);
            run=Mathf.Min(size.y-2.4f,storey*1.6f);
            // Test the whole stair/landing rectangle, including concave notches in mapped footprints.
            for(float z=0;z<=size.y*.5f;z+=1f)
                foreach(float sign in new[]{1f,-1f})
                    for(float x=size.x*.5f-1.05f;x>=-size.x*.5f+1.05f;x-=1f)
                    {
                        var candidate=new Vector2(x,z*sign);bool clear=true;
                        if(doorway.HasValue && new Rect(candidate.x-1.2f,candidate.y-run*.5f-1f,2.4f,run+2f).Overlaps(new Rect(doorway.Value.x-1.5f,doorway.Value.y-1.5f,3f,3f)))continue;
                        for(float dx=-.9f;clear && dx<=.91f;dx+=.3f)
                            for(float dz=-run*.5f-.8f;clear && dz<=run*.5f+.81f;dz+=.25f)
                                if(!Inside(candidate+new Vector2(dx,dz),polygon))clear=false;
                        if(clear){origin=candidate;return true;}
                    }
            return false;
        }
        public static HashSet<MapFeature> Select(List<MapFeature> features,Vector2 focus)
        {
            var candidates=new List<MapFeature>();
            var distances=new Dictionary<MapFeature,float>();
            foreach(var feature in features)
                if(MappedBuildingVisuals.TryFit(feature,out var center,out _,out _) && (center-focus).sqrMagnitude<400*400 && Fit(feature,out _,out _,out _))
                {candidates.Add(feature);distances[feature]=(center-focus).sqrMagnitude;}
            candidates.Sort((a,b)=>distances[a].CompareTo(distances[b]));
            int budget=Application.isMobilePlatform?8:16;
            return new HashSet<MapFeature>(candidates.GetRange(0,Mathf.Min(budget,candidates.Count)));
        }
    }
}

