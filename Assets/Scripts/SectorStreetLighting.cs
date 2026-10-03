using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    // Emissive fixtures are cheap; only the closest lamps cast real light, globally.
    public sealed class SectorStreetLighting : MonoBehaviour
    {
        static readonly List<SectorStreetLighting> sectors=new List<SectorStreetLighting>();
        static readonly List<Light> candidates=new List<Light>();
        static bool night;
        static float nightBlend,transitionSeconds;
        public static float NightBlend=>nightBlend;
        public static bool IsNight => night;
        readonly List<Light> lamps=new List<Light>();
        Material metal,bulb,windows;
        float nextRefresh;
        public static void SetNight(bool enabled,float fadeSeconds=0)
        {
            night=enabled;transitionSeconds=Mathf.Max(0,fadeSeconds);
            if(transitionSeconds<=0)nightBlend=enabled?1:0;
            foreach(var sector in sectors) {sector.ApplyEmission();sector.nextRefresh=0;}
        }
        public static void AdvanceTransition(float seconds)
        {
            if(!float.IsFinite(seconds) || seconds<=0)return;
            float target=night?1:0;
            float next=transitionSeconds>0?Mathf.MoveTowards(nightBlend,target,seconds/transitionSeconds):target;
            if(Mathf.Approximately(next,nightBlend))return;
            nightBlend=next;
            foreach(var sector in sectors)sector.ApplyEmission();
        }
        public void Build(SectorWorld world,Material windowMaterial,Vector2 focus=default)
        {
            windows=windowMaterial;
            metal=new Material(Shader.Find("Standard")){color=new Color(.1f,.12f,.14f)};
            bulb=new Material(Shader.Find("Standard"));bulb.EnableKeyword("_EMISSION");
            if(!sectors.Contains(this)) sectors.Add(this);
            var positions=new List<Vector3>();
            foreach(var road in world.Features)
            {
                if(road.Kind!="road" || MapFeatureStyle.Path(road)) continue;
                for(int i=1;i<road.Points.Count;i++)
                {
                    var a=road.Points[i-1];var delta=road.Points[i]-a;
                    var side=new Vector2(-delta.y,delta.x).normalized;
                    for(float d=10;d<delta.magnitude-5;d+=32)
                    {
                        var p=a+delta.normalized*d+side*(MapFeatureStyle.RoadWidth(road)*.5f+.65f);
                        if(p.sqrMagnitude>300*300 || !StreetPlacement.Clear(world.Features,road,p,delta.normalized,.5f,.5f)) continue;
                        positions.Add(new Vector3(p.x,world.Ground(p.x,p.y)+.35f,p.y));
                    }
                }
            }
            positions.Sort((a,b)=>(new Vector2(a.x,a.z)-focus).sqrMagnitude.CompareTo((new Vector2(b.x,b.z)-focus).sqrMagnitude));
            foreach(var p in positions)
            {
                if(lamps.Count>=32) break;
                bool tooClose=false;
                foreach(var lamp in lamps) if((lamp.transform.localPosition-(p+Vector3.up*5.6f)).sqrMagnitude<12*12) {tooClose=true;break;}
                if(tooClose) continue;
                Part("Streetlight pole",p+Vector3.up*2.8f,new Vector3(.14f,5.6f,.14f),metal);
                Part("Streetlight housing",p+Vector3.up*5.65f,new Vector3(.65f,.18f,.65f),metal);
                Part("Warm lamp diffuser",p+Vector3.up*5.54f,new Vector3(.5f,.035f,.5f),bulb);
                var obj=new GameObject("Streetlight ground illumination");obj.transform.SetParent(transform,false);
                obj.transform.localPosition=p+Vector3.up*5.5f;obj.transform.localRotation=Quaternion.Euler(90,0,0);
                var light=obj.AddComponent<Light>();light.type=LightType.Spot;light.spotAngle=110;light.range=13;
                light.color=new Color(1f,.68f,.34f);light.intensity=3;light.shadows=LightShadows.None;light.enabled=false;
                lamps.Add(light);
            }
            ApplyEmission();
        }
        static bool InBuilding(Vector2 p,List<MapFeature> features)
        {
            foreach(var f in features)
            {
                if(f.Kind!="building") continue;
                bool inside=false;
                for(int i=0,j=f.Points.Count-1;i<f.Points.Count;j=i++)
                {
                    var a=f.Points[i];var b=f.Points[j];
                    if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
                }
                if(inside) return true;
            }
            return false;
        }
        void Part(string label,Vector3 p,Vector3 size,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=label;obj.transform.SetParent(transform,false);
            obj.transform.localPosition=p;obj.transform.localScale=size;
            obj.GetComponent<Collider>().enabled=false;obj.GetComponent<Renderer>().sharedMaterial=material;
        }
        void ApplyEmission()
        {
            if(bulb!=null) {bulb.color=Color.Lerp(new Color(.5f,.52f,.5f),new Color(1,.8f,.5f),nightBlend);bulb.SetColor("_EmissionColor",new Color(1,.6f,.22f)*(2*nightBlend));}
            if(windows!=null)
            {
                windows.color=Color.Lerp(new Color(.14f,.19f,.23f),new Color(.40f,.29f,.16f),nightBlend);
                windows.SetFloat("_Glossiness",Mathf.Lerp(.62f,.28f,nightBlend));
                windows.SetColor("_EmissionColor",new Color(1,.55f,.18f)*(1.4f*nightBlend));
            }
            Shader.SetGlobalColor("_CityNightEmission",new Color(1f,.75f,.42f)*(1.4f*nightBlend));
            foreach(var lamp in lamps) if(lamp!=null){lamp.intensity=3*nightBlend;if(nightBlend<=.001f)lamp.enabled=false;}
        }
        void Update()
        {
            if(sectors.Count==0 || sectors[0]!=this)return;
            AdvanceTransition(Time.unscaledDeltaTime);
            if(Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+.5f;
            RefreshActiveLights(Camera.main);
        }
        public static void RefreshActiveLights(Camera camera)
        {
            candidates.Clear();
            foreach(var sector in sectors) foreach(var lamp in sector.lamps) if(lamp!=null) {lamp.enabled=false;candidates.Add(lamp);}
            if(nightBlend<=.001f || camera==null) return;
            var p=camera.transform.position;
            candidates.Sort((a,b)=>(a.transform.position-p).sqrMagnitude.CompareTo((b.transform.position-p).sqrMagnitude));
            int budget=Application.isMobilePlatform?4:8;
            for(int i=0;i<Mathf.Min(budget,candidates.Count);i++)
                candidates[i].enabled=(candidates[i].transform.position-p).sqrMagnitude<80*80;
        }
        void OnEnable()
        {
            if(!sectors.Contains(this)) sectors.Add(this);
            nextRefresh=0;ApplyEmission();
        }
        void OnDisable()
        {
            foreach(var lamp in lamps) if(lamp!=null) lamp.enabled=false;
            sectors.Remove(this);candidates.Clear();
        }
        void OnDestroy()
        {
            sectors.Remove(this);candidates.Clear();
            if(metal!=null) Destroy(metal);if(bulb!=null) Destroy(bulb);
        }
    }
}
