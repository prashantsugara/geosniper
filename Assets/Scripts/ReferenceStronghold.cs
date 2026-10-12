using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    // Perches belong to actual supported balcony/roof geometry, not floating markers.
    public sealed partial class ReferenceStronghold : MonoBehaviour
    {
        public readonly List<Vector3> Perches=new List<Vector3>();
        public Vector3 Vantage;
        [System.Serializable]
        public sealed class GalleryLayout
        {
            public Vector3[] stairs, firing, doors, floors;
            public int[] stairStarts;
        }
        public GalleryLayout Layout {get;private set;}
        readonly List<Material> surfaces=new List<Material>();
        readonly List<GameObject> cover=new List<GameObject>();
        Material sandbagSurface;
        public int SupplyCrates {get;private set;}
        public CampaignLocationProfile Profile {get;private set;}
        MapFeature reservedFootprint;
        public static ReferenceStronghold Ensure(SectorWorld world,Vector3 focus,int stage=-1)
        {
            var profile=CampaignLocationProfile.ForStage(stage);
            var existing=world.GetComponentInChildren<ReferenceStronghold>();
            if(existing!=null)
            {
                if(existing.Profile.Stage==profile.Stage)return existing;
                if(existing.reservedFootprint!=null){world.Features.Remove(existing.reservedFootprint);world.Buildings.Remove(existing.reservedFootprint);}
                existing.gameObject.SetActive(false);Destroy(existing.gameObject);Physics.SyncTransforms();
            }
            if(profile.Kind==CampaignLocationKind.DuelRoof)return null;
            var prefab=profile.Kind==CampaignLocationKind.Stronghold?Resources.Load<GameObject>("Models/Stronghold/AfghanStronghold"):null;
            if(profile.Kind==CampaignLocationKind.Stronghold && prefab==null)return null;
            Vector3 position=Vector3.zero;float best=float.MaxValue;bool found=false;
            var local=world.transform.InverseTransformPoint(focus);
            var placementFeatures=new List<MapFeature>(world.Features);
            foreach(var park in world.Features)if(park.Kind=="park")placementFeatures.Add(new MapFeature{Kind="building",Points=park.Points});
            for(int x=-224;x<=224;x+=16)for(int z=-224;z<=224;z+=16)
            {
                var center=new Vector2(x,z);float distance=Vector2.Distance(center,new Vector2(local.x,local.z));
                if(distance>=best || distance<65 || distance>240 || Mathf.Abs(x)<65 && Mathf.Abs(z)<65 || !StreetPlacement.Clear(placementFeatures,null,center,Vector2.up,22,29))continue;
                bool clear=true;float ground=world.Ground(x,z);
                foreach(var offset in new[]{new Vector2(-11,-14.5f),new Vector2(-11,14.5f),new Vector2(11,-14.5f),new Vector2(11,14.5f)})
                {
                    var p=center+offset;
                    if(Mathf.Abs(world.Ground(p.x,p.y)-ground)>.35f)clear=false;
                }
                var plot=world.transform.TransformPoint(new Vector3(x,ground+5.3f,z));
                if(clear && Physics.CheckBox(plot,new Vector3(10.5f,5,14),world.transform.rotation,~0,QueryTriggerInteraction.Ignore))clear=false;
                if(!clear || distance>=best)continue;best=distance;position=new Vector3(x,ground+.035f,z);found=true;
            }
            if(!found)return null;
            var host=new GameObject(profile.Name);host.transform.SetParent(world.transform,false);host.transform.localPosition=position;
            // A half turn keeps the reserved footprint while exposing the galleries
            // to the existing daylight sun instead of presenting a shadowed rear wall.
            host.transform.localRotation=Quaternion.Euler(0,180,0);
            var stronghold=host.AddComponent<ReferenceStronghold>();
            stronghold.Profile=profile;
            if(profile.Kind==CampaignLocationKind.Stronghold)
            {
            var visual=Instantiate(prefab,host.transform,false);stronghold.Bind(visual);
            var layout=Resources.Load<TextAsset>("Models/Stronghold/GalleryLayout");
            if(layout!=null)stronghold.Layout=JsonUtility.FromJson<GalleryLayout>(layout.text);
            foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
            {var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;}
            }
            else stronghold.BuildLocation();
            stronghold.AddArchitecturalLights(world);
            Physics.SyncTransforms();stronghold.AddSupplyProps();Physics.SyncTransforms();stronghold.FindPerches();stronghold.FindVantage(world,focus);
            // Reserve the footprint for map display and later placement checks.
            // Native source stairs supply access; procedural building generation is skipped.
            var footprint=new MapFeature{Kind="building",Name=profile.Name,Height=10,
                Details=new Newtonsoft.Json.Linq.JObject{["scenery_only"]="true",["reference_stronghold"]="true"},
                Points=new List<Vector2>{new Vector2(position.x-10.4f,position.z-13.5f),new Vector2(position.x+10.4f,position.z-13.5f),new Vector2(position.x+10.4f,position.z+13.5f),new Vector2(position.x-10.4f,position.z+13.5f)}};
            stronghold.reservedFootprint=footprint;world.Features.Add(footprint);world.Buildings[footprint]=host;
            return stronghold;
        }
        Material Surface(string name,string atlas,Color tint,float roughness=.85f)
        {
            var surface=new Material(Shader.Find("Standard")){name=name,color=tint};surfaces.Add(surface);
            surface.SetFloat("_Glossiness",1-roughness);surface.mainTexture=Resources.Load<Texture2D>("Models/Stronghold/"+atlas+"_albedo");
            var packed=Resources.Load<Texture2D>("Models/Stronghold/"+atlas+"_metallic");
            if(packed!=null){surface.SetTexture("_MetallicGlossMap",packed);surface.SetFloat("_GlossMapScale",1);surface.EnableKeyword("_METALLICGLOSSMAP");}
            var orm=Resources.Load<Texture2D>("Models/Stronghold/"+atlas+"_occlusion");
            if(orm!=null){surface.SetTexture("_OcclusionMap",orm);surface.SetFloat("_OcclusionStrength",.3f);}
            var normal=Resources.Load<Texture2D>("Models/Stronghold/"+atlas+"_normal");
            if(normal!=null){surface.SetTexture("_BumpMap",normal);surface.SetFloat("_BumpScale",.35f);surface.EnableKeyword("_NORMALMAP");}
            return surface;
        }
        void Bind(GameObject visual)
        {
            var wall=Surface("Licensed weathered plaster","Plaster",new Color(.84f,.76f,.62f));
            var floor=Surface("Dusty stone terrace","Plaster",new Color(.58f,.51f,.39f));
            var stone=Surface("Licensed sandstone masonry","Stone",new Color(.94f,.88f,.76f));
            var wood=Surface("Licensed weathered timber","Timber",new Color(.46f,.34f,.22f));
            var metal=Surface("Weathered painted AC and handrails","",new Color(.39f,.38f,.32f),.67f);metal.SetFloat("_Metallic",.25f);
            var dark=Surface("Recessed fans and utility cables","",new Color(.07f,.065f,.05f));
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                var mats=renderer.sharedMaterials;
                for(int i=0;i<mats.Length;i++)
                {
                    // Mesh names survive importers that replace FBX material slots.
                    string name=(renderer.name+" "+(mats[i]!=null?mats[i].name:"")).ToLowerInvariant();
                    mats[i]=name.Contains("wood")?wood:name.Contains("stone")?stone:name.Contains("metal")?metal:name.Contains("dark")?dark:name.Contains("floor")?floor:wall;
                }
                renderer.sharedMaterials=mats;
            }
        }
        void FindPerches()
        {
            if(Layout?.firing!=null)foreach(var p in Layout.firing)
            {
                var at=transform.TransformPoint(p);
                if(!Physics.Raycast(at+Vector3.up*.2f,Vector3.down,out var support,.5f,~0,QueryTriggerInteraction.Ignore) || !support.transform.IsChildOf(transform))continue;
                if(!Physics.CheckCapsule(at+Vector3.up*.4f,at+Vector3.up*1.4f,.3f))Perches.Add(p);
            }
            for(float x=-7;x<=7;x+=2.8f)for(float z=-11.2f;z<=11.2f;z+=2.8f)for(float level=5;level<=14;level+=3)
            {
                var origin=transform.TransformPoint(new Vector3(x,level,z));
                if(!Physics.Raycast(origin,Vector3.down,out var hit,3.2f,~0,QueryTriggerInteraction.Ignore) || !hit.collider.transform.IsChildOf(transform) || hit.normal.y<.95f)continue;
                var p=transform.InverseTransformPoint(hit.point)+Vector3.up*.08f;
                if(p.y<3 || p.y>9.5f || Perches.Exists(other=>(other-p).sqrMagnitude<3*3))continue;
                if(Physics.CheckCapsule(transform.TransformPoint(p+Vector3.up*.4f),transform.TransformPoint(p+Vector3.up*1.4f),.3f))continue;
                Perches.Add(p);
            }
        }
        void AddSupplyProps()
        {
            var prefab=Resources.Load<GameObject>("Models/NeighborhoodProps/DeliveryCrate");
            if(prefab==null || Layout?.floors==null)return;
            // Reuse the licensed 108-triangle timber crate and its source atlas.
            var timber=new Material(Shader.Find("Standard")){name="Sketchfab weathered supply crates",color=new Color(.78f,.69f,.54f),
                mainTexture=Resources.Load<Texture2D>("Models/NeighborhoodProps/Mat.2_baseColor")};
            timber.SetFloat("_Glossiness",.12f);surfaces.Add(timber);
            var locations=Profile.Kind==CampaignLocationKind.Stronghold?new List<Vector3>():supplySites;
            if(Profile.Kind==CampaignLocationKind.Stronghold)foreach(var floor in Layout.floors)locations.Add(new Vector3(6.65f,floor.y,-10.9f));
            for(int floor=0;floor<locations.Count;floor++)
            {
                // Storage sits at the gallery's far end, away from its doors,
                // centre walkway, firing bays and exterior stair landing.
                var local=locations[floor];
                var at=transform.TransformPoint(local);
                if(!Physics.Raycast(at+Vector3.up*.2f,Vector3.down,out var support,.5f,~0,QueryTriggerInteraction.Ignore)
                    || !support.transform.IsChildOf(transform) || support.normal.y<.95f)continue;
                for(int box=0;box<2;box++)
                {
                    var prop=Instantiate(prefab,transform,false);prop.name="Licensed balcony supply crate";
                    prop.transform.position=support.point+Vector3.up*(.025f+box*.85f);
                    prop.transform.localRotation=Quaternion.Euler(0,box==0?8:-7,0);
                    foreach(var renderer in prop.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=timber;
                    foreach(var collider in prop.GetComponentsInChildren<Collider>())Destroy(collider);
                    // FBX root scale can be 100 despite a .85m world footprint.
                    // Fit collision in imported local space instead of multiplying
                    // nominal metre dimensions by that root scale.
                    var bounds=new Bounds();bool hasBounds=false;
                    foreach(var filter in prop.GetComponentsInChildren<MeshFilter>())
                    {
                        var meshBounds=filter.sharedMesh.bounds;
                        for(int corner=0;corner<8;corner++)
                        {
                            var offset=new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                            var point=prop.transform.InverseTransformPoint(filter.transform.TransformPoint(meshBounds.center+Vector3.Scale(meshBounds.extents,offset)));
                            if(!hasBounds){bounds=new Bounds(point,Vector3.zero);hasBounds=true;}else bounds.Encapsulate(point);
                        }
                    }
                    var collision=prop.AddComponent<BoxCollider>();collision.center=bounds.center;collision.size=bounds.size;
                    SupplyCrates++;
                }
            }
            AddCover(transform.TransformPoint(new Vector3(-4.3f,Layout.floors[0].y,-11.3f)),true);
        }
        void AddArchitecturalLights(SectorWorld world)
        {
            var lighting=world.GetComponentInChildren<SectorStreetLighting>();if(lighting==null)return;
            var fixture=new Material(Shader.Find("Standard")){name="Stronghold brass wall fixtures",color=new Color(.13f,.10f,.065f)};surfaces.Add(fixture);
            for(int side=-1;side<=1;side+=2)
            {
                var mount=new Vector3(side*5.5f,3.65f,-13.05f);
                if(Profile.Kind==CampaignLocationKind.Apartments)mount=new Vector3(side*6.2f,3.0f,-2.7f);
                if(Profile.Kind==CampaignLocationKind.ShoppingGallery)mount=new Vector3(side*8.85f,2.8f,-11.1f);
                if(Profile.Kind==CampaignLocationKind.FreightDepot)mount=new Vector3(side<0?-7.8f:.2f,3.65f,-5.45f);
                if(Profile.Kind==CampaignLocationKind.FieldCamp)
                {
                    mount=new Vector3(side*6.8f,2.8f,-10.3f);
                    var pole=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pole.name="Camp lamp pole";pole.transform.SetParent(transform,false);pole.transform.localPosition=new Vector3(mount.x,1.4f,mount.z+.12f);pole.transform.localScale=new Vector3(.07f,1.4f,.07f);pole.GetComponent<Renderer>().sharedMaterial=fixture;pole.GetComponent<Collider>().enabled=false;
                }
                var housing=GameObject.CreatePrimitive(PrimitiveType.Cube);housing.name="Arcade wall sconce";housing.transform.SetParent(transform,false);
                housing.transform.localPosition=mount;housing.transform.localScale=new Vector3(.18f,.34f,.17f);
                housing.GetComponent<Renderer>().sharedMaterial=fixture;housing.GetComponent<Collider>().enabled=false;
                var obj=new GameObject("Stronghold warm facade wash");obj.transform.SetParent(transform,false);
                obj.transform.localPosition=mount+new Vector3(0,-.2f,-.3f);obj.transform.localRotation=Quaternion.LookRotation(new Vector3(0,-1,.3f));
                var lamp=obj.AddComponent<Light>();lamp.type=LightType.Spot;lamp.spotAngle=115;lamp.range=11;lamp.color=new Color(1,.66f,.31f);
                lighting.RegisterArchitecturalLamp(lamp);
            }
        }
        public int VisibleFrom(Vector3 eyes)
        {
            int count=0;foreach(var p in Perches)
                if(!Physics.Linecast(eyes,transform.TransformPoint(p+Vector3.up*1.4f),~0,QueryTriggerInteraction.Ignore)
                    && !Physics.Linecast(eyes,transform.TransformPoint(p+Vector3.up*1.65f),~0,QueryTriggerInteraction.Ignore))count++;
            return count;
        }
        void FindVantage(SectorWorld world,Vector3 focus)
        {
            Vantage=focus;float best=float.MinValue;
            void Consider(Vector3 position)
            {
                var eyes=position+Vector3.up*1.65f;
                int visible=VisibleFrom(eyes);if(visible<2)return;
                bool facade=!Physics.Linecast(eyes,transform.position+Vector3.up*3,out var obstruction,~0,QueryTriggerInteraction.Ignore) || obstruction.transform.IsChildOf(transform);
                // The open galleries face local -Z. Showing the rear blank
                // wall hid the reference silhouette even after direct deployment.
                var facing=transform.InverseTransformPoint(position);facing.y=0;
                // A slight angle reveals gallery depth and the stepped rear shell.
                float front=facing.sqrMagnitude>.01f?Vector3.Dot(facing.normalized,new Vector3(-.5f,0,-.866f)):0;
                float score=Mathf.Min(visible,4)*3+(facade?20:-20)+front*25-Mathf.Abs(Vector3.Distance(position,transform.position)-40)*.08f;
                if(score>best){best=score;Vantage=position;}
            }
            Consider(focus);
            foreach(var roof in world.RooftopSpawns)
            {
                float distance=Vector3.Distance(roof,transform.position);if(distance<35 || distance>175)continue;
                Consider(roof);
                var toward=transform.position-roof;toward.y=0;toward.Normalize();
                for(int step=4;step<=12;step+=4)
                {
                    var edge=roof+toward*step;
                    if(!Physics.Raycast(edge+Vector3.up*2,Vector3.down,out var support,4,~0,QueryTriggerInteraction.Ignore) || Mathf.Abs(support.point.y-roof.y)>1)continue;
                    var at=support.point+Vector3.up*.1f;
                    if(!Physics.CheckCapsule(at+Vector3.up*.4f,at+Vector3.up*1.4f,.32f))Consider(at);
                }
            }
            for(int angle=0;angle<360;angle+=30)
            {
                var direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
                var at=transform.position+direction*32;
                if(!Physics.Raycast(at+Vector3.up*30,Vector3.down,out var support,40,~0,QueryTriggerInteraction.Ignore) || support.normal.y<.95f)continue;
                at=support.point+Vector3.up*.1f;
                if(SectorWorld.DryFootprint(at,.6f) && !Physics.CheckCapsule(at+Vector3.up*.4f,at+Vector3.up*1.4f,.32f))Consider(at);
            }
        }
        public void AddCover(Vector3 perch,bool permanent=false)
        {
            var prefab=Resources.Load<GameObject>("Models/Stronghold/Sandbag");if(prefab==null)return;
            if(!Physics.Raycast(perch+Vector3.up*.35f,Vector3.down,out var support,1,~0,QueryTriggerInteraction.Ignore) || !support.transform.IsChildOf(transform) || support.normal.y<.95f)return;
            perch.y=support.point.y+.015f;
            if(sandbagSurface==null){sandbagSurface=Surface("Licensed woven sandbag","Sandbag_surface_0",new Color(2.4f,2.2f,1.8f));sandbagSurface.SetFloat("_GlossMapScale",.35f);}
            for(int row=0;row<2;row++)
            {
                var bag=Instantiate(prefab,transform,false);bag.name=permanent?"Entrance supply sandbag":"Guard sandbag cover";bag.transform.position=perch+Vector3.up*(row*.25f);if(!permanent)cover.Add(bag);
                foreach(var renderer in bag.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=sandbagSurface;
                foreach(var filter in bag.GetComponentsInChildren<MeshFilter>()){var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;}
            }
        }
        public void ClearCover(){foreach(var bag in cover)if(bag!=null){bag.SetActive(false);Destroy(bag);}cover.Clear();}
        void OnDestroy(){foreach(var surface in surfaces)if(surface!=null)Destroy(surface);foreach(var mesh in locationMeshes)if(mesh!=null)Destroy(mesh);}
    }
}
