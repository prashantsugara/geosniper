using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    // Asset dressing never changes map footprints or their gameplay colliders.
    public sealed class MappedBuildingVisuals
    {
        readonly GameObject[] assets;
        readonly Material[] surfaces;
        readonly HashSet<MapFeature> selected=new HashSet<MapFeature>();
        public MappedBuildingVisuals(List<MapFeature> features,System.Func<Color,Material> material,Vector2 focus)
        {
            assets=ModelLibrary.Load("Buildings"); surfaces=new Material[assets.Length];
            for(int i=0;i<assets.Length;i++)
            {
                surfaces[i]=material(Color.white);
                var tex = Resources.Load<Texture2D>("Models/Buildings/"+assets[i].name+"_albedo");
                if (tex != null) surfaces[i].mainTexture = tex;
                if (surfaces[i].HasProperty("_Metallic")) surfaces[i].SetFloat("_Metallic", 0.15f);
                if (surfaces[i].HasProperty("_Glossiness")) surfaces[i].SetFloat("_Glossiness", 0.48f);
            }
            var compatible=features.FindAll(f=>TryFit(f,out _,out _,out _));
            compatible.Sort((a,b)=>(Center(a)-focus).sqrMagnitude.CompareTo((Center(b)-focus).sqrMagnitude));
            int budget=Application.isMobilePlatform?320:640;
            for(int i=0;i<Mathf.Min(budget,compatible.Count);i++) selected.Add(compatible[i]);
        }
        static Vector2 Center(MapFeature feature)
        { Vector2 center=Vector2.zero; foreach(var p in feature.Points) center+=p; return center/feature.Points.Count; }

        public static bool TryFit(MapFeature f,out Vector2 center,out Vector2 along,out Vector2 size)
        {
            center=Vector2.zero; along=Vector2.right; size=Vector2.zero;
            if(f.Kind!="building" || f.Points.Count<3 || f.Height<4 || f.Height>70) return false;
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
            if(size.x<4 || size.y<4 || size.x>75 || size.y>75 || size.x/size.y>4.5f || size.y/size.x>4.5f) return false;
            return true;
        }

        public bool TryBuild(MapFeature feature,Transform parent)
        {
            if(assets.Length==0 || !selected.Contains(feature) || !TryFit(feature,out var center,out var along,out var size)) return false;
            int hash=Mathf.RoundToInt(center.x*13+center.y*7);
            int index=(hash&int.MaxValue)%assets.Length;
            if(surfaces[index].mainTexture==null) return false;
            var wrapper=new GameObject("Mapped asset facade"); wrapper.transform.SetParent(parent,false);
            Object.Instantiate(assets[index],wrapper.transform,false);
            var bounds=ImportedVisual.LocalBounds(wrapper.transform);
            if(!WeaponGeometry.Usable(bounds))
            {
                wrapper.SetActive(false);
                if(Application.isPlaying) Object.Destroy(wrapper); else Object.DestroyImmediate(wrapper);
                return false; // Let SectorWorld keep its procedural facade/collision fallback.
            }
            var scale=new Vector3(size.x/bounds.size.x,feature.Height/bounds.size.y,size.y/bounds.size.z);
            var rotation=Quaternion.LookRotation(new Vector3(-along.y,0,along.x));
            wrapper.transform.localRotation=rotation;
            wrapper.transform.localScale=scale;
            wrapper.transform.localPosition=new Vector3(center.x,0,center.y)-rotation*Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),scale);
            foreach(var r in wrapper.GetComponentsInChildren<Renderer>()) r.sharedMaterial=surfaces[index];
            return true;
        }
    }
}
