using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        public int NeighborhoodSites { get; private set; }
        public int NeighborhoodVehicles { get; private set; }
        readonly Collider[] dressingHits=new Collider[48];

        IEnumerator BuildNeighborhoodDressing(List<MapFeature> features,Vector2 focus)
        {
            NeighborhoodSites=0;NeighborhoodVehicles=0;
            int budget=Application.isMobilePlatform?18:42;
            var metal=Material(new Color(.22f,.27f,.3f),.45f,.65f);
            var wood=Styled(new Color(.48f,.32f,.18f),false);
            var stone=Styled(new Color(.59f,.58f,.51f),false);
            var green=Material(new Color(.25f,.39f,.19f));
            var fabric=Material(new Color(.72f,.28f,.13f));
            var blue=Material(new Color(.13f,.31f,.47f),.35f,.25f);
            var cream=Material(new Color(.86f,.81f,.66f));
            var leaves=Material(new Color(.13f,.27f,.095f));
            var paving=Material(new Color(.78f,.77f,.71f));
            paving.mainTexture=BuildNeighborhoodPavingTexture();
            paving.SetTexture("_BumpMap",BuildConcreteNormalTexture());paving.EnableKeyword("_NORMALMAP");
            paving.SetFloat("_BumpScale",.15f);
            paving.SetFloat("_Glossiness",.12f);
            var vehicleAtlas=Material(Color.white,.62f,.35f);
            vehicleAtlas.mainTexture=Resources.Load<Texture2D>("Models/Cars/Textures/cars_0");
            var occupied=new List<Vector2>();
            var cars=ModelLibrary.Load("Cars");
            Physics.SyncTransforms();
            foreach(var road in WorldDetailPlanner.RoadsNearFocus(features,focus))
            {
                float roadWidth=MapFeatureStyle.RoadWidth(road);
                if(roadWidth<3 || MapFeatureStyle.Text(road,"class")=="motorway")continue;
                for(int segment=1;segment<road.Points.Count && NeighborhoodSites<budget;segment++)
                {
                    Vector2 delta=road.Points[segment]-road.Points[segment-1];float length=delta.magnitude;
                    if(length<28)continue;
                    Vector2 along=delta/length,right=new Vector2(along.y,-along.x);
                    for(float distance=14;distance<length-12 && NeighborhoodSites<budget;distance+=32)
                    {
                        foreach(int side in new[]{-1,1})
                        {
                            if(NeighborhoodSites>=budget)break;
                            Vector2 center=road.Points[segment-1]+along*distance+right*side*(roadWidth*.5f+5.6f);
                            if((center-focus).sqrMagnitude>220*220 || (center-focus).sqrMagnitude<12*12 || Mathf.Abs(center.x)>305 || Mathf.Abs(center.y)>305)continue;
                            if(occupied.Exists(p=>(p-center).sqrMagnitude<14*14))continue;
                            if(!StreetPlacement.Clear(features,road,center,along,5.4f,10.8f))continue;
                            bool dry=true;
                            foreach(float x in new[]{-2.7f,2.7f})foreach(float z in new[]{-5.4f,5.4f})
                            {var corner=center+right*x+along*z;if(IsWaterAt(corner.x,corner.y) || Mathf.Abs(Ground(corner.x,corner.y)-Ground(center.x,center.y))>.7f)dry=false;}
                            if(!dry)continue;
                            var position=new Vector3(center.x,Ground(center.x,center.y)+.08f,center.y);
                            var rotation=Quaternion.LookRotation(new Vector3(along.x,0,along.y));
                            int hits=Physics.OverlapBoxNonAlloc(transform.TransformPoint(position+Vector3.up*1.6f),new Vector3(2.8f,1.25f,5.5f),dressingHits,transform.rotation*rotation,~0,QueryTriggerInteraction.Ignore);
                            bool clear=hits<dressingHits.Length;
                            for(int i=0;i<hits;i++)
                            {
                                var hit=dressingHits[i];
                                if(hit==null || hit.name=="Local terrain" || hit.name=="park" || hit.name=="Sloped road" || hit.name.Contains("footprint"))continue;
                                clear=false;break;
                            }
                            if(!clear)continue;
                            uint hash=unchecked((uint)(Mathf.RoundToInt(center.x)*73856093 ^ Mathf.RoundToInt(center.y)*19349663));
                            // Cycle accepted sites so grid-aligned roads cannot hash into just one scene type.
                            int kind=NeighborhoodSites%6;
                            var site=new GameObject(new[]{"Neighborhood parking","Market stall","Bus shelter","Delivery stacks","Utility work zone","Garden seating"}[kind]);
                            site.transform.SetParent(transform,false);site.transform.localPosition=position;site.transform.localRotation=rotation;
                            BuildNeighborhoodFoundation(site.transform,kind,side,paving,stone,cream,metal);
                            if(kind==0)
                            {
                                DressingVehicle(site.transform,new Vector3(0,0,-2.9f),cars,hash,vehicleAtlas);
                                DressingVehicle(site.transform,new Vector3(0,0,2.9f),cars,hash+1,vehicleAtlas);
                                DressingBox(site.transform,"Parking bollard",new Vector3(1.8f,.45f,0),new Vector3(.18f,.9f,.18f),metal,true);
                            }
                            else if(kind==1)
                            {
                                DressingBox(site.transform,"Market counter",new Vector3(0,.65f,0),new Vector3(2.8f,1.3f,1.1f),wood,true);
                                DressingBox(site.transform,"Striped canopy",new Vector3(0,2.4f,0),new Vector3(3.3f,.14f,1.9f),fabric,false);
                                foreach(float x in new[]{-1.5f,1.5f})DressingBox(site.transform,"Canopy post",new Vector3(x,1.2f,.7f),new Vector3(.08f,2.4f,.08f),metal,false);
                                for(int i=0;i<4;i++)DressingBox(site.transform,"Produce crate",new Vector3(-1.05f+i*.7f,1.42f,0),new Vector3(.6f,.22f,.65f),i%2==0?green:wood,false);
                                for(int i=0;i<3;i++)DressingBox(site.transform,"Supply box",new Vector3(1.8f,.25f+i*.48f,1.9f),new Vector3(.8f,.46f,.8f),wood,i==0);
                            }
                            else if(kind==2)
                            {
                                DressingBox(site.transform,"Shelter roof",new Vector3(0,2.55f,0),new Vector3(2.7f,.18f,4.2f),blue,false);
                                foreach(float z in new[]{-1.8f,1.8f})DressingBox(site.transform,"Shelter end",new Vector3(1.1f,1.25f,z),new Vector3(.15f,2.5f,.16f),metal,true);
                                DressingBox(site.transform,"Shelter bench",new Vector3(.55f,.5f,0),new Vector3(.7f,.16f,3.2f),wood,false);
                                DressingBox(site.transform,"Shelter back",new Vector3(1.1f,1.35f,0),new Vector3(.12f,2.4f,3.8f),blue,true);
                                DressingBox(site.transform,"Bus stop sign",new Vector3(-1.3f,2.1f,-2.4f),new Vector3(.08f,.7f,.65f),blue,false);
                            }
                            else if(kind==3)
                            {
                                for(int stack=0;stack<3;stack++)
                                {
                                    float z=-2.5f+stack*2.5f;
                                    DressingBox(site.transform,"Shipping pallet",new Vector3(.7f,.13f,z),new Vector3(1.6f,.26f,1.6f),wood,false);
                                    DressingBox(site.transform,"Cargo stack",new Vector3(.7f,.75f,z),new Vector3(1.4f,1.2f,1.4f),stack==1?blue:wood,true);
                                    DressingBox(site.transform,"Cargo strap",new Vector3(.7f,1.37f,z),new Vector3(.12f,.04f,1.4f),metal,false);
                                }
                            }
                            else if(kind==4)
                            {
                                DressingBox(site.transform,"Utility cabinet",new Vector3(.8f,.9f,1.8f),new Vector3(1.3f,1.8f,.65f),metal,true);
                                DressingBox(site.transform,"Work barricade",new Vector3(0,.65f,-1.6f),new Vector3(2.4f,1.3f,.5f),stone,true);
                                for(int i=0;i<4;i++)DressingBox(site.transform,"Hazard panel",new Vector3(-.9f+i*.6f,.9f,-1.87f),new Vector3(.25f,.45f,.03f),fabric,false);
                                DressingBox(site.transform,"Cable reel",new Vector3(1.1f,.55f,-3.5f),new Vector3(1.1f,1.1f,.75f),wood,true);
                            }
                            else
                            {
                                foreach(float z in new[]{-2.2f,2.2f})
                                {
                                    DressingBox(site.transform,"Concrete planter",new Vector3(.4f,.38f,z),new Vector3(2f,.76f,1.1f),stone,true);
                                }
                                DressingBox(site.transform,"Courtyard bench",new Vector3(.5f,.5f,0),new Vector3(.7f,.15f,2.4f),wood,false);
                                DressingBox(site.transform,"Bench back",new Vector3(.85f,.85f,0),new Vector3(.12f,.55f,2.4f),wood,false);
                            }
                            AddNeighborhoodDetail(site.transform,kind,metal,wood,stone,green,fabric,blue,cream,leaves);
                            AddDownloadedStreetProps(site.transform,kind);
                            CombineDressing(site.transform);
                            occupied.Add(center);NeighborhoodSites++;
                            yield return null;
                        }
                    }
                }
                if(NeighborhoodSites>=budget)break;
            }
        }

        void AddNeighborhoodDetail(Transform site,int kind,Material metal,Material wood,Material stone,Material green,Material fabric,Material blue,Material cream,Material leaves)
        {
            if(kind==0)
            {
                DressingBox(site,"Parking sign post",new Vector3(2,1.2f,3.7f),new Vector3(.07f,2.4f,.07f),metal,false);
                DressingBox(site,"Parking sign face",new Vector3(2,2.25f,3.7f),new Vector3(.7f,.65f,.06f),blue,false);
                DressingBox(site,"Parking sign inset",new Vector3(2,2.25f,3.66f),new Vector3(.48f,.43f,.02f),cream,false);
            }
            else if(kind==1)
            {
                for(int i=0;i<6;i++)DressingBox(site,"Canopy stripe",new Vector3(-1.4f+i*.55f,2.48f,0),new Vector3(.23f,.025f,1.9f),cream,false);
                DressingBox(site,"Canopy hanging valance",new Vector3(0,2.28f,-.9f),new Vector3(3.3f,.28f,.06f),fabric,false);
                for(int i=0;i<4;i++)for(int j=0;j<3;j++)
                    DressingFoliage(site,"Market produce",new Vector3(-1.2f+i*.7f+j*.12f,1.58f,-.12f+j*.12f),new Vector3(.16f,.16f,.16f),i%2==0?fabric:green);
                for(int i=0;i<5;i++)DressingBox(site,"Counter timber slat",new Vector3(-1.1f+i*.55f,.65f,-.56f),new Vector3(.04f,1.2f,.03f),cream,false);
            }
            else if(kind==2)
            {
                foreach(float z in new[]{-1.8f,0,1.8f})DressingBox(site,"Shelter frame",new Vector3(1,1.35f,z),new Vector3(.09f,2.4f,.09f),metal,false);
                foreach(float z in new[]{-1.2f,1.2f})DressingBox(site,"Bench support",new Vector3(.55f,.25f,z),new Vector3(.6f,.5f,.12f),metal,false);
                DressingBox(site,"Route timetable",new Vector3(.98f,1.65f,-.8f),new Vector3(.035f,.7f,.45f),cream,false);
                DressingBox(site,"Stop sign pole",new Vector3(-1.3f,1.1f,-2.4f),new Vector3(.08f,2.2f,.08f),metal,false);
            }
            else if(kind==3)
            {
                for(int i=0;i<3;i++)
                {
                    float z=-2.5f+i*2.5f;
                    DressingBox(site,"Cargo label",new Vector3(.7f,.85f,z-.71f),new Vector3(.32f,.23f,.025f),cream,false);
                    foreach(float x in new[]{.05f,1.35f})DressingBox(site,"Cargo corner fitting",new Vector3(x,.75f,z-.71f),new Vector3(.08f,1.2f,.05f),metal,false);
                }
            }
            else if(kind==4)
            {
                for(int i=0;i<5;i++)DressingBox(site,"Cabinet ventilation",new Vector3(.8f,1.1f+i*.08f,1.46f),new Vector3(.85f,.025f,.025f),blue,false);
                DressingBox(site,"Cabinet handle",new Vector3(1.2f,.8f,1.44f),new Vector3(.06f,.24f,.04f),cream,false);
            }
            else
            {
                foreach(float z in new[]{-2.2f,2.2f})
                {
                    foreach(float edge in new[]{-.12f,1f})DressingBox(site,"Planter stone rim",new Vector3(.4f,.77f,z+edge-.44f),new Vector3(2.1f,.12f,.13f),stone,false);
                    var plants=site.gameObject.AddComponent<RealisticVegetation>();
                    for(int i=0;i<3;i++)
                        if(!plants.Add(i%2==0?"LowShrub":"ConeShrub",new Vector3(-.2f+i*.6f,.82f,z),.65f,i*65))
                            DressingFoliage(site,"Planter foliage",new Vector3(-.2f+i*.6f,1.15f,z),new Vector3(.8f,.7f,.85f),leaves);
                    plants.Flush();
                }
                foreach(float z in new[]{-.85f,.85f})DressingBox(site,"Bench iron leg",new Vector3(.5f,.23f,z),new Vector3(.6f,.45f,.1f),metal,false);
            }
        }
        void DressingFoliage(Transform parent,string label,Vector3 position,Vector3 size,Material surface)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Sphere);part.name=label;
            part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=size;
            part.GetComponent<Renderer>().sharedMaterial=surface;part.GetComponent<Collider>().enabled=false;
        }

        void DressingBox(Transform parent,string label,Vector3 position,Vector3 size,Material material,bool solid)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=label;
            part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=size;
            part.GetComponent<Renderer>().sharedMaterial=material;
            part.GetComponent<Collider>().enabled=false;
            if(solid){var collider=parent.gameObject.AddComponent<BoxCollider>();collider.center=position;collider.size=size;}
        }
        void CombineDressing(Transform root)
        {
            var batches=new Dictionary<Material,List<CombineInstance>>();var parts=new List<GameObject>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.GetComponentInParent<DressingVehicleMarker>()!=null)continue;
                if(filter.GetComponentInParent<DownloadedStreetPropMarker>()!=null)continue;
                if(filter.GetComponent<VegetationBatchMarker>()!=null)continue;
                if(filter.GetComponent<NeighborhoodSurfaceMarker>()!=null)continue;
                var renderer=filter.GetComponent<Renderer>();if(renderer==null || filter.sharedMesh==null)continue;
                var material=renderer.sharedMaterial;if(!batches.TryGetValue(material,out var batch))batches[material]=batch=new List<CombineInstance>();
                batch.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});parts.Add(filter.gameObject);
            }
            foreach(var batch in batches)
            {
                var mesh=new Mesh{name="Combined neighborhood props"};mesh.CombineMeshes(batch.Value.ToArray(),true,true);meshes.Add(mesh);
                var obj=new GameObject("Batched street furniture");obj.transform.SetParent(root,false);
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=batch.Key;renderer.shadowCastingMode=ShadowCastingMode.On;
            }
            foreach(var part in parts){part.SetActive(false);if(Application.isPlaying)Destroy(part);else DestroyImmediate(part);}
        }
        void DressingVehicle(Transform parent,Vector3 offset,GameObject[] cars,uint seed,Material surface)
        {
            if(cars.Length==0)return;
            var holder=new GameObject("Parked neighborhood vehicle");holder.transform.SetParent(parent,false);holder.AddComponent<DressingVehicleMarker>();
            var model=Instantiate(cars[seed%(uint)cars.Length],holder.transform,false);
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            var bounds=ImportedVisual.AlignVehicle(holder,model,false,model.name.ToLowerInvariant().Contains("police"));
            float longest=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            if(!float.IsFinite(longest) || longest<.001f){if(Application.isPlaying)Destroy(holder);else DestroyImmediate(holder);return;}
            model.transform.localScale*=4.4f/longest;bounds=ImportedVisual.LocalBounds(holder.transform);
            model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            bounds=ImportedVisual.LocalBounds(holder.transform);
            if(model.name.ToLowerInvariant().Contains("police"))PoliceVehicleVisual.Apply(model);
            else foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                if(renderer.sharedMaterial==null || renderer.sharedMaterial.mainTexture==null)renderer.sharedMaterial=surface;
            holder.transform.localPosition=offset;holder.transform.localRotation=Quaternion.Euler(0,seed%2==0?0:180,0);
            var box=holder.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;
            NeighborhoodVehicles++;
        }
    }
    // Marks imported vehicle meshes so street-furniture batching retains their authored materials.
    public sealed class DressingVehicleMarker : MonoBehaviour { }
}
