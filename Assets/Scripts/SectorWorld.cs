using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

namespace GeoSniper
{
    public sealed partial class SectorWorld : MonoBehaviour
    {
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        Texture2D facade;
        [System.NonSerialized] public SectorElevation Elevation=new SectorElevation();
        public float Ground(float x,float z)
        {
            return ShapedGround(x,z,RawGround(x,z));
        }
        float RawGround(float x,float z)=>Elevation!=null?Elevation.Sample(transform.position.x+x,transform.position.z+z):0f;
        public readonly List<Vector3> SpawnPoints=new List<Vector3>();
        public readonly List<Vector3> RooftopSpawns=new List<Vector3>();
        public readonly List<MapFeature> Features=new List<MapFeature>();
        public readonly Dictionary<MapFeature,GameObject> Buildings=new Dictionary<MapFeature,GameObject>();
        public readonly List<List<Vector3>> RoadPaths=new List<List<Vector3>>();
        public readonly Dictionary<List<Vector3>,MapFeature> RoadPathFeatures=new Dictionary<List<Vector3>,MapFeature>();
        public string SourceLabel="OSM SECTOR";
        public Vector3 DetailFocus;
        public bool BuildNavigation=true;
        Collider[] spawnSurfaces;
        public bool TryFindGeographicSpawn(Vector3 target, out Vector3 spawn)
        {
            Physics.SyncTransforms();
            spawnSurfaces=GetComponentsInChildren<Collider>();
            if(TrySpawnAt(target,out spawn)) return true;
            // Keep the requested coordinate when possible, then search nearby clear space.
            // Dense footprints and roadside props can obstruct more than three metres.
            foreach(float radius in new[]{.75f,1.5f,3f,6f,12f,24f,48f,96f})
                for(int step=0;step<16;step++)
                {
                    float angle=step*Mathf.PI/8;
                    if(TrySpawnAt(target+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius,out spawn)) return true;
                }
            var nearby=new List<Vector3>(SpawnPoints);
            nearby.Sort((a,b)=>(a-target).sqrMagnitude.CompareTo((b-target).sqrMagnitude));
            foreach(var point in nearby)
            {
                if((point-target).sqrMagnitude>240*240) break;
                if(TrySpawnAt(point,out spawn)) return true;
            }
            // Do not invent a spawn on empty space. Callers can choose a mode-specific
            // recovery position after a valid terrain surface has been generated.
            foreach(var point in SpawnPoints)if(!WaterAt(point) && TrySpawnAt(point,out spawn)) return true;
            foreach(var point in RooftopSpawns)if(!WaterAt(point) && TrySpawnAt(point,out spawn)) return true;
            spawn = default;
            return false;
        }
        bool TrySpawnAt(Vector3 target, out Vector3 spawn)
        {
            spawn=default;
            var local=transform.InverseTransformPoint(target);
            if(Mathf.Abs(local.x)>320 || Mathf.Abs(local.z)>320 || !DryFootprint(target,.5f)) return false;
            float top=transform.position.y+Ground(local.x,local.z)+300;
            var ray=new Ray(new Vector3(target.x,top,target.z),Vector3.down);
            // Query the generated surfaces directly as well as the physics broadphase.
            // A new sector can be queried before its static spatial index is updated.
            var hits=new List<RaycastHit>(Physics.RaycastAll(ray,600,~0,QueryTriggerInteraction.Ignore));
            foreach(var collider in spawnSurfaces)
            {
                if(collider==null || !collider.enabled || collider.isTrigger) continue;
                var bounds=collider.bounds;
                if(target.x<bounds.min.x || target.x>bounds.max.x || target.z<bounds.min.z || target.z>bounds.max.z) continue;
                if(collider.Raycast(ray,out var direct,600)) hits.Add(direct);
            }
            hits.Sort((a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.collider.GetComponentInParent<SectorWorld>()!=this || hit.normal.y<.7f) continue;
                // Reject water and decorative/scenery colliders as landing surfaces.
                string surface=hit.collider.gameObject.name;
                if(surface=="water") continue;
                if(surface=="Local terrain" && IsWaterAt(local.x,local.z)) continue;
                if(surface!="building" && surface!="Local terrain" && surface!="Sloped road" && surface!="park") continue;
                var candidate=hit.point+Vector3.up*.12f;
                bool obstructed=false;
                foreach(var obstacle in Physics.OverlapCapsule(candidate+Vector3.up*.55f,candidate+Vector3.up*1.45f,.32f,~0,QueryTriggerInteraction.Ignore))
                {
                    if(obstacle==hit.collider || obstacle.GetComponentInParent<UrbanPlayer>()!=null) continue;
                    string oName=obstacle.gameObject.name;
                    if(oName=="Local terrain" || oName=="Sloped road" || oName=="park" || oName.Contains("footprint")) continue;
                    obstructed=true;
                    break;
                }
                if(obstructed) continue;
                spawn=candidate;
                return true;
            }
            return false;
        }
        public static List<MapFeature> ActiveFeatures { get; private set; }
        public static readonly List<SectorWorld> LoadedWorlds=new List<SectorWorld>();
        public void ActivateAsPrimarySector()
        {
            ActiveFeatures=Features;
            LoadedWorlds.Remove(this);
            LoadedWorlds.Insert(0,this);
        }
        public sealed class LadderRoute { public SectorWorld Owner; public Vector3 Bottom, Top, Landing; }
        public static readonly List<LadderRoute> Ladders=new List<LadderRoute>();
        Material Material(Color color) => Material(color, 0.12f, 0.0f);
        Material Material(Color color, float glossiness, float metallic)
        {
            var shader=Shader.Find("Standard");
            if(shader==null) throw new System.InvalidOperationException("RENDER_SHADER_MISSING");
            var material = new Material(shader);
            material.color = color;
            material.SetFloat("_Glossiness", glossiness);
            material.SetFloat("_Metallic", metallic);
            material.enableInstancing=true;
            materials.Add(material);
            return material;
        }
        readonly Dictionary<string, Material> styledCache = new Dictionary<string, Material>();
        Material Styled(Color color, bool windows = false)
        {
            string key = ColorUtility.ToHtmlStringRGB(color) + (windows ? "windows" : "solid");
            if (!styledCache.TryGetValue(key, out var value))
            {
                value = Material(color, windows ? 0.26f : 0.12f, windows ? 0.04f : 0.0f);
                if (windows)
                {
                    if (facade != null) value.mainTexture = facade;
                    var norm = BuildFacadeNormalTexture();
                    if (norm != null)
                    {
                        value.SetTexture("_BumpMap", norm);
                        value.EnableKeyword("_NORMALMAP");
                    }
                }
                styledCache[key] = value;
            }
            return value;
        }
        readonly Dictionary<string, Material> roofCache = new Dictionary<string, Material>();
        Material RoofStyled(Color color,string surface)
        {
            string key = ColorUtility.ToHtmlStringRGB(color)+":"+surface;
            if (!roofCache.TryGetValue(key, out var value))
            {
                value = Material(color, 0.18f, 0.02f);
                value.mainTexture = MappedSurfaceTextures.Roof(surface);
                var norm = BuildRoofNormalTexture();
                if (norm != null)
                {
                    value.SetTexture("_BumpMap", norm);
                    value.EnableKeyword("_NORMALMAP");
                }
                roofCache[key] = value;
            }
            return value;
        }
        static readonly Color[] earthFacades={new Color(.76f,.71f,.64f),new Color(.68f,.62f,.54f),new Color(.62f,.38f,.28f)};
        static readonly Color[] coolFacades={new Color(.72f,.75f,.76f),new Color(.58f,.63f,.67f),new Color(.42f,.45f,.48f)};
        static readonly Color[] neutralFacades={new Color(.82f,.82f,.80f),new Color(.68f,.69f,.67f),new Color(.55f,.56f,.54f)};
        public static Color WorldFacadeColor(Vector2 center)
        {
            // Broad neighborhoods share a color family; individual buildings vary
            // within it. World coordinates keep a clipped building stable across sectors.
            float district=Mathf.PerlinNoise(center.x*.0025f+31.7f,center.y*.0025f+83.1f);
            int blockX=Mathf.FloorToInt(center.x/22f),blockY=Mathf.FloorToInt(center.y/22f);
            uint hash=unchecked((uint)(blockX*73856093 ^ blockY*19349663));
            int variant=(int)(hash%3);
            if(district<.34f) return earthFacades[variant];
            if(district>.67f) return coolFacades[variant];
            return neutralFacades[variant];
        }
        public Material SignMaterial(Color color)=>Material(color);
        public void Generate(List<MapFeature> features)
        {
            var job=GenerateAsync(features);
            while(job.MoveNext()) { }
        }
        public System.Collections.IEnumerator GenerateAsync(List<MapFeature> features, System.Action<int,int> progress=null)
        {
            Features.Clear(); Features.AddRange(features); ActiveFeatures=Features; Buildings.Clear(); SpawnPoints.Clear(); RooftopSpawns.Clear(); RoadPaths.Clear(); RoadPathFeatures.Clear();
            if(!LoadedWorlds.Contains(this)) LoadedWorlds.Add(this);
            Ladders.RemoveAll(route=>route.Owner==this);
            // The ground texture contains its own soil/grass tones; a dark material tint
            // multiplied them into a nearly uniform olive field.
            var ground = Material(new Color(.74f,.79f,.70f));
            if (ground.HasProperty("_Glossiness")) ground.SetFloat("_Glossiness", 0.08f);
            var groundTex = BuildGrassGroundTexture();
            if (groundTex != null) ground.mainTexture = groundTex;
            ground.SetTexture("_BumpMap", BuildGrassNormalTexture());
            ground.SetTextureScale("_BumpMap",new Vector2(2,2));
            ground.EnableKeyword("_NORMALMAP");

            facade = BuildArchitecturalFacadeTexture();
            var trim=Material(new Color(.56f,.55f,.52f), 0.24f, 0.05f);
            var paint=Material(new Color(.92f,.88f,.65f));
            var crossingPaint=Material(new Color(.91f,.92f,.88f));
            var ladderMaterial=Material(new Color(.16f,.18f,.18f));
            var shoulder = Material(new Color(.64f,.62f,.57f)); shoulder.SetFloat("_Glossiness", 0.12f); shoulder.mainTexture = BuildConcreteSidewalkTexture();
            shoulder.SetTexture("_BumpMap", BuildConcreteNormalTexture());
            shoulder.EnableKeyword("_NORMALMAP");
            var road = Material(new Color(.86f,.88f,.88f)); road.SetFloat("_Metallic", 0.02f); road.SetFloat("_Glossiness", 0.14f); road.mainTexture = BuildAsphaltRoadTexture();
            road.SetTexture("_BumpMap", BuildAsphaltNormalTexture());
            road.EnableKeyword("_NORMALMAP");
            var pavedRoad=Material(new Color(.82f,.82f,.80f));
            pavedRoad.mainTexture=BuildConcreteSidewalkTexture();pavedRoad.SetFloat("_Glossiness",.10f);
            pavedRoad.SetTexture("_BumpMap", BuildConcreteNormalTexture());
            pavedRoad.EnableKeyword("_NORMALMAP");
            var earthRoad=Material(new Color(.82f,.76f,.64f));
            earthRoad.mainTexture=BuildSoilGroundTexture();earthRoad.SetFloat("_Glossiness",.04f);
            var mudRoad = Material(new Color(0.85f, 0.82f, 0.78f));
            mudRoad.SetFloat("_Metallic", 0.01f); mudRoad.SetFloat("_Glossiness", 0.22f);
            mudRoad.mainTexture = BuildMudRoadTexture();
            mudRoad.SetTexture("_BumpMap", BuildMudRoadNormalTexture());
            mudRoad.EnableKeyword("_NORMALMAP");
            var gravelRoad = Material(new Color(0.88f, 0.86f, 0.82f));
            gravelRoad.SetFloat("_Metallic", 0.0f); gravelRoad.SetFloat("_Glossiness", 0.06f);
            gravelRoad.mainTexture = BuildGravelRoadTexture();
            gravelRoad.SetTexture("_BumpMap", BuildGravelNormalTexture());
            gravelRoad.EnableKeyword("_NORMALMAP");
            var cobbleRoad = Material(new Color(0.84f, 0.83f, 0.82f));
            cobbleRoad.SetFloat("_Metallic", 0.02f); cobbleRoad.SetFloat("_Glossiness", 0.18f);
            cobbleRoad.mainTexture = BuildCobbleRoadTexture();
            cobbleRoad.SetTexture("_BumpMap", BuildCobbleNormalTexture());
            cobbleRoad.EnableKeyword("_NORMALMAP");
            var park = Material(new Color(.30f,.44f,.30f));
            park.SetTexture("_BumpMap", BuildGrassNormalTexture());
            park.EnableKeyword("_NORMALMAP");
            PrepareWater(features);
            IndexGroundRoads(features);
            var junctionMask=new RoadJunctionMask(features);
            var terrainShader=Resources.Load<Shader>("Shaders/TerrainBlend");
            if(terrainShader!=null && terrainShader.isSupported)
            {
                var blended=new Material(terrainShader);materials.Add(blended);
                blended.SetTexture("_GrassTex",groundTex);
                blended.SetTexture("_SoilTex",BuildSoilGroundTexture());
                blended.SetTexture("_ConcreteTex",BuildConcreteSidewalkTexture());
                ground=blended;
            }
            var placeSignFace = Material(new Color(.06f,.13f,.18f));
            BuildGround(ground);
            yield return null;
            Vector3 localFocus=transform.InverseTransformPoint(DetailFocus==Vector3.zero?transform.position:DetailFocus);
            Vector2 focus=new Vector2(localFocus.x,localFocus.z);
            var buildingVisuals=new MappedBuildingVisuals(features,Material,focus);
            int balconyBudget=Application.isMobilePlatform?48:96;
            int windowBudget=Application.isMobilePlatform?64:128;
            var detailedBuildings=WorldDetailPlanner.SelectBuildings(features,focus,windowBudget,300f);
            var shopBuildings=new HashSet<MapFeature>();
            foreach(var place in features)
                if(place.Kind=="place" && SectorSignage.IsShop(place))
                {
                    var host=SectorSignage.FindHost(place,features,features);
                    if(host!=null)shopBuildings.Add(host);
                }
            var balconyBuildings=WorldDetailPlanner.SelectBuildings(features,focus,balconyBudget,220f);
            var rooftopBuildings=WorldDetailPlanner.SelectBuildings(features,focus,Application.isMobilePlatform?24:80,230f);
            var buildingById=new Dictionary<string,GameObject>();
            var buildingParts=new List<KeyValuePair<GameObject,string>>();
            var bldShader=Shader.Find("GeoSniper/RealisticBuilding") ?? Shader.Find("Standard");
            var windowFrame=Material(new Color(.10f,.11f,.13f), 0.52f, 0.72f);
            var warmWindow=new Material(bldShader);
            warmWindow.color=new Color(.95f,.75f,.35f);
            warmWindow.SetColor("_Color",warmWindow.color);
            warmWindow.SetFloat("_GlassGlossiness",.98f);
            warmWindow.SetFloat("_GlassMetallic",.88f);
            warmWindow.SetColor("_GlassTint",new Color(.12f,.10f,.08f,1f));
            warmWindow.SetColor("_EmissionColor",Color.black);
            warmWindow.mainTexture=MappedSurfaceTextures.WindowInteriors();
            warmWindow.enableInstancing=true;
            materials.Add(warmWindow);

            var reflectiveGlass=new Material(bldShader);
            reflectiveGlass.color=new Color(.06f,.09f,.14f);
            reflectiveGlass.SetColor("_Color",reflectiveGlass.color);
            reflectiveGlass.SetFloat("_GlassGlossiness",.98f);
            reflectiveGlass.SetFloat("_GlassMetallic",.88f);
            reflectiveGlass.SetColor("_GlassTint",new Color(.05f,.09f,.14f,1f));
            var pureGlassTex=new Texture2D(2,2,TextureFormat.RGBA32,false);
            pureGlassTex.SetPixels(new[]{new Color(.1f,.15f,.22f,1f),new Color(.1f,.15f,.22f,1f),new Color(.1f,.15f,.22f,1f),new Color(.1f,.15f,.22f,1f)});
            pureGlassTex.Apply();
            reflectiveGlass.mainTexture=pureGlassTex;
            reflectiveGlass.enableInstancing=true;
            materials.Add(reflectiveGlass);
            float sliceStart=Time.realtimeSinceStartup;
            for(int featureIndex=0;featureIndex<features.Count;featureIndex++)
            {
                var f=features[featureIndex];
                progress?.Invoke(featureIndex+1,features.Count);
                if(Time.realtimeSinceStartup-sliceStart>.004f) { yield return null; sliceStart=Time.realtimeSinceStartup; }
                if (f.Kind == "road")
                {
                    List<Vector3> currentPath = null;
                    if (!MapFeatureStyle.Path(f)) {
                        currentPath = new List<Vector3>();
                        RoadPaths.Add(currentPath);
                        RoadPathFeatures[currentPath]=f;
                        currentPath.Add(new Vector3(f.Points[0].x, Ground(f.Points[0].x, f.Points[0].y) + .16f, f.Points[0].y));
                    }
                    for (int i=1; i<f.Points.Count; i++)
                    {
                        if(Time.realtimeSinceStartup-sliceStart>.003f) { yield return null; sliceStart=Time.realtimeSinceStartup; }
                        var a = To3(f.Points[i-1],.08f); var b = To3(f.Points[i],.08f);
                        if (currentPath != null) currentPath.Add(new Vector3(f.Points[i].x, Ground(f.Points[i].x, f.Points[i].y) + .16f, f.Points[i].y));
                        if ((b-a).sqrMagnitude < .01f) continue;
                        float width=MapFeatureStyle.RoadWidth(f);
                        var roadType=MapFeatureStyle.GetRoadType(f);
                        Material roadMat;
                        bool hasCurb=false;
                        string roadSurfaceLabel="Sloped road";
                        switch(roadType)
                        {
                            case MapFeatureStyle.RoadType.MudDirt:
                                roadMat=mudRoad;
                                hasCurb=false;
                                roadSurfaceLabel="Mud road";
                                break;
                            case MapFeatureStyle.RoadType.Gravel:
                                roadMat=gravelRoad;
                                hasCurb=false;
                                roadSurfaceLabel="Gravel road";
                                break;
                            case MapFeatureStyle.RoadType.Cobblestone:
                                roadMat=cobbleRoad;
                                hasCurb=false;
                                roadSurfaceLabel="Cobblestone road";
                                break;
                            case MapFeatureStyle.RoadType.Concrete:
                                roadMat=pavedRoad;
                                hasCurb=!MapFeatureStyle.Path(f);
                                roadSurfaceLabel="Concrete road";
                                break;
                            default:
                                roadMat=road;
                                hasCurb=!MapFeatureStyle.Path(f);
                                roadSurfaceLabel="Asphalt road";
                                break;
                        }
                        Material shoulderMat=(roadType==MapFeatureStyle.RoadType.MudDirt || roadType==MapFeatureStyle.RoadType.Gravel)?earthRoad:shoulder;
                        BuildRoadStrip(a,b,width+(MapFeatureStyle.Path(f)?.5f:2.2f),.10f,shoulderMat,true,roadSurfaceLabel);
                        BuildRoadStrip(a,b,width,.16f,roadMat,false,roadSurfaceLabel);
                        if(hasCurb) BuildRoadCurb(a,b,width,shoulder,f,junctionMask);
                    }
                }
                else
                {
                    if(f.Kind=="water") { BuildWater(f); continue; }
                    if(f.Kind=="place")
                    {
                        continue;
                    }
                    if(f.Points.Count<3) continue;
                    // Strip duplicate closing vertex if present (from raw unclipped features)
                    if((f.Points[0]-f.Points[f.Points.Count-1]).sqrMagnitude<.01f)
                        f.Points.RemoveAt(f.Points.Count-1);
                    // Deduplicate consecutive identical vertices
                    for(int k=f.Points.Count-1;k>0;k--)
                        if((f.Points[k]-f.Points[k-1]).sqrMagnitude<.0001f) f.Points.RemoveAt(k);
                    if(f.Points.Count<3) continue;
                    float area=0;
                    for(int k=0;k<f.Points.Count;k++) area+=Cross(f.Points[k],f.Points[(k+1)%f.Points.Count]);
                    if(area<0) f.Points.Reverse();
                    var triangles = Triangulate(f.Points);
                    if (triangles.Count == 0) continue;
                    var roofTriangles=new List<int>(triangles);

                    if(f.Kind!="building")
                    {
                        // Clean conformal 2D ground surface for parks and water: no vertical walls, no sky-floating
                        var flatGround = new List<Vector3>();
                        var uvGround = new List<Vector2>();
                        var triGround = new List<int>();
                        for(int t=0;t<roofTriangles.Count;t+=3)
                        {
                            var pA = f.Points[roofTriangles[t]];
                            var pB = f.Points[roofTriangles[t+1]];
                            var pC = f.Points[roofTriangles[t+2]];
                            float maxEdge = 140f * 140f;
                            if((pB-pA).sqrMagnitude > maxEdge || (pC-pB).sqrMagnitude > maxEdge || (pA-pC).sqrMagnitude > maxEdge) continue;
                            int idx = flatGround.Count;
                            float yOffset = f.Kind == "water" ? 0.02f : 0.05f;
                            flatGround.Add(new Vector3(pA.x, Ground(pA.x, pA.y) + yOffset, pA.y));
                            flatGround.Add(new Vector3(pB.x, Ground(pB.x, pB.y) + yOffset, pB.y));
                            flatGround.Add(new Vector3(pC.x, Ground(pC.x, pC.y) + yOffset, pC.y));
                            uvGround.Add(pA / 4f);
                            uvGround.Add(pB / 4f);
                            uvGround.Add(pC / 4f);
                            triGround.AddRange(new[]{idx, idx+1, idx+2});
                        }
                        if(triGround.Count==0) continue;
                        var groundMesh = new Mesh { name = f.Kind + " footprint" };
                        groundMesh.SetVertices(flatGround); groundMesh.SetUVs(0, uvGround); groundMesh.SetTriangles(triGround, 0);
                        groundMesh.RecalculateNormals(); groundMesh.RecalculateBounds(); meshes.Add(groundMesh);
                        var gObj = new GameObject(f.Kind); gObj.transform.SetParent(transform, false);
                        gObj.transform.localPosition = Vector3.zero;
                        gObj.AddComponent<MeshFilter>().sharedMesh = groundMesh;
                        var gSurface = park;
                        gObj.AddComponent<MeshRenderer>().sharedMaterial = gSurface;
                        gObj.AddComponent<MeshCollider>().sharedMesh = groundMesh;
                        continue;
                    }

                    Vector2 footprintCenter=Vector2.zero;
                    foreach(var point in f.Points) footprintCenter+=point;
                    footprintCenter/=f.Points.Count;
                    float baseHeight=float.MinValue, bottomHeight=float.MaxValue;
                    foreach(var p in f.Points) { float y=Ground(p.x,p.y); baseHeight=Mathf.Max(baseHeight,y); bottomHeight=Mathf.Min(bottomHeight,y); }
                    baseHeight=Mathf.Max(baseHeight,Ground(footprintCenter.x,footprintCenter.y));
                    for(int edge=0;edge<f.Points.Count;edge++)
                    {
                        Vector2 midpoint=(f.Points[edge]+f.Points[(edge+1)%f.Points.Count])*.5f;
                        baseHeight=Mathf.Max(baseHeight,Ground(midpoint.x,midpoint.y));
                    }
                    // The occupied building sits above terrain; the sealed foundation reaches below it.
                    baseHeight+=.35f;
                    float foundation=Mathf.Max(.1f,baseHeight-bottomHeight+.05f);
                    float minH = MapFeatureStyle.Number(f, "min_height", 0f);
                    float h = Mathf.Max(minH + 3f, f.Height);
                    var vertices = new List<Vector3>();
                    foreach (var p in f.Points) vertices.Add(To3(p,h));
                    int n=vertices.Count;
                    float bottom = minH > 0.1f ? minH : (bottomHeight-baseHeight-1.5f);
                    foreach (var p in f.Points)
                    {
                        vertices.Add(To3(p,bottom));
                    }
                    for(int t=0;t<roofTriangles.Count;t+=3)
                        triangles.AddRange(new[]{roofTriangles[t],roofTriangles[t+1],roofTriangles[t+2]});
                    for(int t=0;t<roofTriangles.Count;t+=3)
                        triangles.AddRange(new[]{roofTriangles[t]+n,roofTriangles[t+2]+n,roofTriangles[t+1]+n});
                    var flat = new List<Vector3>(); var uv = new List<Vector2>();
                    var roofIndices = new List<int>(); var wallIndices = new List<int>();
                    for (int i=0;i<n;i++)
                    {
                        int j=(i+1)%n;
                        triangles.AddRange(new [] {i,j,i+n,j,j+n,i+n});
                    }
                    for(int t=0;t<triangles.Count;t+=3)
                    {
                        var a=vertices[triangles[t]]; var b=vertices[triangles[t+1]]; var c=vertices[triangles[t+2]];
                        bool top=Mathf.Approximately(a.y,h)&&Mathf.Approximately(b.y,h)&&Mathf.Approximately(c.y,h);
                        var indices=top?roofIndices:wallIndices;
                        var normal=Vector3.Cross(b-a,c-a).normalized;
                        foreach(var v in new[]{a,b,c})
                        {
                            indices.Add(flat.Count); flat.Add(v);
                            uv.Add(top?new Vector2(v.x,v.z)/4:MapFeatureStyle.WallUv(f,v,normal));
                        }
                    }
                    var mesh = new Mesh { name = "Sector footprint", subMeshCount=2 };
                    mesh.SetVertices(flat); mesh.SetUVs(0,uv); mesh.SetTriangles(roofIndices,0); mesh.SetTriangles(wallIndices,1); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); meshes.Add(mesh);
                    var obj = new GameObject(f.Kind); obj.transform.SetParent(transform,false);
                    obj.transform.localPosition=Vector3.up*baseHeight;
                    obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                    Color roofColor=new Color(.42f,.44f,.43f);
                    string roofMaterial=MapFeatureStyle.Text(f,"roof_material");
                    if(roofMaterial=="roof_tiles" || roofMaterial=="tile") roofColor=new Color(.55f,.27f,.17f);
                    if(roofMaterial=="metal") roofColor=new Color(.48f,.54f,.58f);
                    if(ColorUtility.TryParseHtmlString((string)f.Details["roof_color"],out var mappedRoof)) roofColor=mappedRoof;
                    var roofSurface=RoofStyled(roofColor,roofMaterial);
                    Vector2 worldCenter=footprintCenter+new Vector2(transform.position.x,transform.position.z);
                    Color wallColor=MapFeatureStyle.BuildingColor(f,WorldFacadeColor(worldCenter));
                    obj.AddComponent<MeshRenderer>().sharedMaterials=new[]{roofSurface,FacadeStyled(f,wallColor,true)};
                    obj.AddComponent<MeshCollider>().sharedMesh=mesh;
                    // Near-field architecture uses the exact footprint shell: imported box
                    // facades can protrude through newly authored windows and balconies.
                    bool isPart=WorldDetailPlanner.IsBuildingPart(f);
                    bool detailedFacade=detailedBuildings.Contains(f);
                    bool importedFacade=!detailedFacade && !isPart && buildingVisuals.TryBuild(f,obj.transform);
                    if(importedFacade)
                    {
                        // Retain the complete collision shell and an exact flat roof;
                        // only replace the repeated window wall rendering.
                        var cap=new Mesh {name="Mapped roof cap"};
                        cap.SetVertices(flat); cap.SetUVs(0,uv); cap.SetTriangles(roofIndices,0);
                        cap.RecalculateNormals(); cap.RecalculateBounds(); meshes.Add(cap);
                        obj.GetComponent<MeshFilter>().sharedMesh=cap;
                        obj.GetComponent<MeshRenderer>().sharedMaterials=new[]{roofSurface};
                    }
                    bool pitched=BuildPitchedRoof(f,obj.transform,h,roofSurface);
                    if(detailedFacade)
                    {
                        // Remove the repetitive painted window texture beneath the 3D panels.
                        obj.GetComponent<MeshRenderer>().sharedMaterials=new[]{roofSurface,FacadeStyled(f,wallColor,false)};
                        int entranceEdge=BuildingFacadeDetails.EntranceEdge(f,features);
                        bool isStorefront=shopBuildings.Contains(f) || MapFeatureStyle.Text(f,"class")=="commercial" || MapFeatureStyle.Text(f,"class")=="retail" || (entranceEdge>=0 && f.Height>=6f && Mathf.Abs(f.Points[0].x*17+f.Points[0].y*31)%5==0);
                        var architecture=BuildingFacadeDetails.BuildArchitecture(f,entranceEdge,isStorefront);
                        if(architecture!=null)
                        {
                            meshes.Add(architecture);
                            var surrounds=new GameObject("Physical facade surrounds and floor bands");
                            surrounds.transform.SetParent(obj.transform,false);
                            surrounds.AddComponent<MeshFilter>().sharedMesh=architecture;
                            surrounds.AddComponent<MeshRenderer>().sharedMaterial=trim;
                        }
                        var windows=BuildingFacadeDetails.BuildWindows(f,entranceEdge,isStorefront);
                        if(windows!=null)
                        {
                            meshes.Add(windows);
                            var panels=new GameObject("Illuminated architectural windows");panels.transform.SetParent(obj.transform,false);
                            panels.AddComponent<MeshFilter>().sharedMesh=windows;
                            panels.AddComponent<MeshRenderer>().sharedMaterials=new[]{windowFrame,warmWindow,reflectiveGlass};
                        }
                    }
                    if(!pitched && detailedFacade && balconyBuildings.Contains(f))
                    {
                        var detail=BuildingFacadeDetails.Build(f,features);
                        if(detail!=null)
                        {
                            meshes.Add(detail);
                            var balconies=new GameObject("Balconies and window surrounds");
                            balconies.transform.SetParent(obj.transform,false);
                            balconies.AddComponent<MeshFilter>().sharedMesh=detail;
                            balconies.AddComponent<MeshRenderer>().sharedMaterial=trim;
                            // Real slabs and rails block shots exactly where their geometry exists.
                            balconies.AddComponent<MeshCollider>().sharedMesh=detail;
                        }
                    }
                    if(detailedFacade) ConfigureBuildingLod(obj,mesh,roofSurface,FacadeStyled(f,wallColor,true));
                    if(f.Kind=="building" && f.Points.Count>2)
                    {
                        if(!isPart)
                        {
                            Buildings[f]=obj;
                            string id=MapFeatureStyle.Text(f,"id");
                            if(id.Length>0) buildingById[id]=obj;
                        }
                        else
                        {
                            string parentId=MapFeatureStyle.Text(f,"building_id");
                            if(parentId.Length>0) buildingParts.Add(new KeyValuePair<GameObject,string>(obj,parentId));
                        }
                        Vector2 center=footprintCenter;
                        // Thick edge colliders follow concave footprints without blocking courtyards.
                        for(int edge=0;edge<f.Points.Count;edge++)
                        {
                            var a=To3(f.Points[edge],(h-foundation)/2); var b=To3(f.Points[(edge+1)%f.Points.Count],(h-foundation)/2);
                            if((b-a).sqrMagnitude<.01f) continue;
                            var wall=new GameObject("Solid wall"); wall.transform.SetParent(obj.transform,false);
                            wall.transform.localPosition=(a+b)/2; wall.transform.localRotation=Quaternion.LookRotation(b-a);
                            wall.AddComponent<BoxCollider>().size=new Vector3(.3f,h+foundation,(b-a).magnitude+.15f);
                        }
                        // A triangle centroid is inside the roof even for an L-shaped building.
                        center=(f.Points[roofTriangles[0]]+f.Points[roofTriangles[1]]+f.Points[roofTriangles[2]])/3;
                        if(!pitched && !isPart)
                        {
                            // Build solid physical perimeter safety barriers around the roof edges
                            int ladderEdge = 0; float longestEdge = 0;
                            for (int i = 0; i < f.Points.Count; i++)
                            {
                                float l = (f.Points[(i + 1) % f.Points.Count] - f.Points[i]).sqrMagnitude;
                                if (l > longestEdge) { longestEdge = l; ladderEdge = i; }
                            }

                            bool hasLadder = rooftopBuildings.Contains(f);
                            for (int edge = 0; edge < f.Points.Count; edge++)
                            {
                                Vector2 pa = f.Points[edge];
                                Vector2 pb = f.Points[(edge + 1) % f.Points.Count];
                                float edgeLen = Vector2.Distance(pa, pb);
                                if (edgeLen < 0.4f) continue;

                                Vector2 along = (pb - pa) / edgeLen;
                                Vector2 mid = (pa + pb) * 0.5f;

                                // Leave 2.2m clearance opening at ladder landing if ladder is on this edge
                                if (edge == ladderEdge && hasLadder && edgeLen > 2.8f)
                                {
                                    float segLen = (edgeLen - 2.2f) * 0.5f;
                                    Vector2 leftMid = pa + along * (segLen * 0.5f);
                                    Vector2 rightMid = pb - along * (segLen * 0.5f);

                                    CreateRooftopEdgeBarrier(obj.transform, leftMid, along, segLen, h, trim);
                                    CreateRooftopEdgeBarrier(obj.transform, rightMid, along, segLen, h, trim);
                                }
                                else
                                {
                                    CreateRooftopEdgeBarrier(obj.transform, mid, along, edgeLen, h, trim);
                                }
                            }

                            // Calculate roof bounds and focus direction
                            float maxRadius = 0f;
                            foreach (var pt in f.Points) maxRadius = Mathf.Max(maxRadius, Vector2.Distance(center, pt));

                            Vector2 focus2D = new Vector2(DetailFocus.x, DetailFocus.z);
                            Vector2 toFocus = (focus2D - center);
                            Vector2 fwdDir = toFocus.sqrMagnitude > 4f ? toFocus.normalized : Vector2.up;

                            // Sniper vantage spawn: placed towards observation edge facing the combat arena
                            float sniperOffsetDist = Mathf.Clamp(maxRadius * 0.35f, 1.2f, 5.0f);
                            Vector2 sniperSpot = center + fwdDir * sniperOffsetDist;

                            // Prop location: offset towards the rear of the roof, safely separated from sniper spawn
                            float propOffsetDist = Mathf.Clamp(maxRadius * 0.45f, 2.5f, 7.5f);
                            Vector2 propSpot = center - fwdDir * propOffsetDist;

                            RooftopSpawns.Add(transform.TransformPoint(new Vector3(sniperSpot.x, baseHeight + h + 0.15f, sniperSpot.y)));
                            if(rooftopBuildings.Contains(f))
                            {
                                BuildLadder(f.Points, baseHeight + h, ladderMaterial);
                                BuildRooftopProps(f.Points, baseHeight + h, propSpot, trim);
                            }
                        }
                    }

                }
            }
            var roadBatchJob=FlushRoadSurfaces();
            while(roadBatchJob.MoveNext())yield return roadBatchJob.Current;
            foreach(var part in buildingParts)
                if(part.Key!=null && buildingById.TryGetValue(part.Value,out var parent))
                    part.Key.transform.SetParent(parent.transform,true);
            var streetMarkings = StreetSurfaceDetails.Build(features, Ground, Application.isMobilePlatform);
            if (streetMarkings != null)
            {
                meshes.Add(streetMarkings);
                var markings = new GameObject("Lane lines and pedestrian crossings");
                markings.transform.SetParent(transform, false);
                markings.AddComponent<MeshFilter>().sharedMesh = streetMarkings;
                markings.AddComponent<MeshRenderer>().sharedMaterials = new[] { paint, crossingPaint };
            }
            var scenery=gameObject.AddComponent<SectorScenery>();
            var sceneryJob=scenery.Generate(features,Elevation,focus);
            while(sceneryJob.MoveNext()) yield return sceneryJob.Current;
            gameObject.AddComponent<SectorStreetLighting>().Build(this,warmWindow,focus);
            yield return null;
            Physics.SyncTransforms();
            yield return null;
            SectorSignage.BuildPlaces(this,placeSignFace,ladderMaterial);
            yield return null;
            BuildRoadSigns(features,focus);
            yield return null;
            // Spawn point calculation â€” yield periodically to avoid frame spike
            float spawnSlice=Time.realtimeSinceStartup;
            foreach(var feature in features)
            {
                if(feature.Kind!="road") continue;
                for(int i=1;i<feature.Points.Count;i++)
                {
                    if(Time.realtimeSinceStartup-spawnSlice>.004f) { yield return null; spawnSlice=Time.realtimeSinceStartup; }
                    var a=To3(feature.Points[i-1],.3f); var b=To3(feature.Points[i],.3f);
                    for(float distance=2;distance<(b-a).magnitude;distance+=12)
                    {
                        if(Time.realtimeSinceStartup-spawnSlice>.003f) { yield return null; spawnSlice=Time.realtimeSinceStartup; }
                        var point=Vector3.Lerp(a,b,distance/(b-a).magnitude);
                        if(IsWaterAt(point.x,point.z))continue;
                        point.y=Ground(point.x,point.z)+.4f;
                        point=transform.TransformPoint(point);
                        bool skyClear = !Physics.Raycast(point + Vector3.up * 0.4f, Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore);
                        if(skyClear && !Physics.CheckCapsule(point+Vector3.up*.5f,point+Vector3.up*1.6f,.4f)) SpawnPoints.Add(point);
                    }
                }
            }
            if (SpawnPoints.Count < 4)
            {
                for (int gx = -60; gx <= 60; gx += 30)
                {
                    for (int gz = -60; gz <= 60; gz += 30)
                    {
                        if(IsWaterAt(gx,gz))continue;
                        Vector3 gPt = new Vector3(gx, Ground(gx, gz) + 0.35f, gz);
                        Vector3 worldPt = transform.TransformPoint(gPt);
                        bool skyClear = !Physics.Raycast(worldPt + Vector3.up * 0.4f, Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore);
                        if (skyClear && !Physics.CheckCapsule(worldPt + Vector3.up * 0.5f, worldPt + Vector3.up * 1.6f, 0.4f))
                            SpawnPoints.Add(worldPt);
                    }
                }
            }
            yield return null;
            var streetPropsJob = BuildStreetPropsAsync(features, trim, paint,focus);
            while(streetPropsJob.MoveNext()) yield return streetPropsJob.Current;
            yield return null;
            if(BuildNavigation)
            {
                var navigation=GetComponent<SectorNavigation>() ?? gameObject.AddComponent<SectorNavigation>();
                if(Application.isPlaying)
                {
                    var navJob=navigation.BuildAsync();
                    while(navJob.MoveNext()) yield return navJob.Current;
                }
                else navigation.Build();
            }
        }
        void BuildGround(Material material)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();var colors=new List<Color32>();
            const int cells=64;const float step=10f;
            var heights=new float[cells+1,cells+1];var groundColors=new Color32[cells+1,cells+1];
            for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++)
            {
                float px=-320+x*step,pz=-320+z*step;
                heights[x,z]=Ground(px,pz);
                groundColors[x,z]=new Color(ShoreBlend(px,pz),RoadsideBlend(px,pz),0,1);
            }
            void Quad(float left,float bottom,float size,Vector3 a,Vector3 b,Vector3 c,Vector3 d,
                Color32 ca,Color32 cb,Color32 cc,Color32 cd)
            {
                int start=vertices.Count;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                colors.Add(ca);colors.Add(cb);colors.Add(cc);colors.Add(cd);
                float wx=transform.position.x+left,wz=transform.position.z+bottom;
                uv.Add(new Vector2(wx,wz)/24f);uv.Add(new Vector2(wx,wz+size)/24f);
                uv.Add(new Vector2(wx+size,wz+size)/24f);uv.Add(new Vector2(wx+size,wz)/24f);
                triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            for(int z=0;z<cells;z++)for(int x=0;x<cells;x++)
            {
                float px=-320+x*step,pz=-320+z*step;
                bool nearBank=groundColors[x,z].r>8 || groundColors[x,z+1].r>8 ||
                    groundColors[x+1,z+1].r>8 || groundColors[x+1,z].r>8;
                Vector3 worldCenter=transform.TransformPoint(new Vector3(px+5,0,pz+5));
                float focusDistance=(worldCenter-DetailFocus).sqrMagnitude;
                int divisions=nearBank?(Application.isMobilePlatform && MobileGraphics.Selected==MobileGraphicsPreset.Performance?2:4):1;
                if(focusDistance>240f*240f)divisions=Mathf.Min(divisions,2);
                if(divisions==1)
                {
                    Quad(px,pz,step,
                        new Vector3(px,heights[x,z],pz),new Vector3(px,heights[x,z+1],pz+step),
                        new Vector3(px+step,heights[x+1,z+1],pz+step),new Vector3(px+step,heights[x+1,z],pz),
                        groundColors[x,z],groundColors[x,z+1],groundColors[x+1,z+1],groundColors[x+1,z]);
                }
                else
                {
                    float small=step/divisions;
                    for(int sz=0;sz<divisions;sz++)for(int sx=0;sx<divisions;sx++)
                    {
                        float left=px+sx*small,bottom=pz+sz*small,right=left+small,top=bottom+small;
                        Quad(left,bottom,small,
                            new Vector3(left,Ground(left,bottom),bottom),new Vector3(left,Ground(left,top),top),
                            new Vector3(right,Ground(right,top),top),new Vector3(right,Ground(right,bottom),bottom),
                            new Color(ShoreBlend(left,bottom),RoadsideBlend(left,bottom),0,1),
                            new Color(ShoreBlend(left,top),RoadsideBlend(left,top),0,1),
                            new Color(ShoreBlend(right,top),RoadsideBlend(right,top),0,1),
                            new Color(ShoreBlend(right,bottom),RoadsideBlend(right,bottom),0,1));
                    }
                }
            }
            var mesh=new Mesh{name="Local terrain"};
            if(vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var obj=new GameObject("Local terrain");obj.transform.SetParent(transform,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
            obj.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        void BuildRoadStrip(Vector3 a,Vector3 b,float width,float lift,Material material,bool collision,string surfaceLabel="Sloped road")
        {
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            Vector3 direction=(b-a).normalized;
            // Slight overlap at every vertex keeps adjacent OSM way segments visually continuous.
            a-=direction*1.4f; b+=direction*1.4f;
            Vector3 side=Vector3.Cross(Vector3.up,direction)*(width/2);
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/5));
            for(int i=0;i<=steps;i++)
            {
                Vector3 p=Vector3.Lerp(a,b,(float)i/steps);
                foreach(int sign in new[]{1,-1})
                {
                    Vector3 v=p+side*sign; v.y=Ground(v.x,v.z)+lift; vertices.Add(v);
                    uv.Add(new Vector2(v.x,v.z)*.5f);
                }
                if(i<steps) { int k=i*2; triangles.AddRange(new[]{k,k+1,k+2,k+1,k+3,k+2}); }
            }
            AddSurface(collision?surfaceLabel:"Lane marking",vertices,uv,triangles,material,collision);
        }

        void BuildRoadCurb(Vector3 a, Vector3 b, float width, Material material,MapFeature road,RoadJunctionMask junctionMask)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();

            Vector3 direction = (b - a).normalized;
            a -= direction * 1.4f; b += direction * 1.4f;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            float halfW = width * 0.5f;
            float curbW = 0.22f;

            int coarseSteps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 5f));
            var samples=new List<float>{0f};
            var heading=new Vector2(direction.x,direction.z);
            // Add detail only beside junctions so a short side street does not
            // remove an entire five-metre curb section.
            for(int i=0;i<coarseSteps;i++)
            {
                float from=(float)i/coarseSteps,to=(float)(i+1)/coarseSteps;
                bool junction=false;
                for(int sign=-1;sign<=1;sign+=2)
                {
                    var offset=side*(halfW*sign);
                    var start=Vector3.Lerp(a,b,from)+offset;
                    var end=Vector3.Lerp(a,b,to)+offset;
                    if(junctionMask.OccupiedAlong(new Vector2(start.x,start.z),new Vector2(end.x,end.z),road,heading))
                    {junction=true;break;}
                }
                int pieces=junction?4:1;
                for(int part=1;part<=pieces;part++)samples.Add(Mathf.Lerp(from,to,(float)part/pieces));
            }
            int steps=samples.Count-1;
            float uvScale=Vector3.Distance(a,b)/5f;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int baseIdx = vertices.Count;
                for (int i = 0; i <= steps; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, samples[i]);
                    Vector3 roadPt = p + side * (halfW * sign);
                    Vector3 curbOuterPt = p + side * ((halfW + curbW) * sign);
                    float gRoad = Ground(roadPt.x, roadPt.z);
                    float gOuter = Ground(curbOuterPt.x, curbOuterPt.z);

                    vertices.Add(new Vector3(roadPt.x, gRoad + 0.16f, roadPt.z));
                    vertices.Add(new Vector3(roadPt.x, gRoad + 0.26f, roadPt.z));
                    vertices.Add(new Vector3(curbOuterPt.x, gOuter + 0.26f, curbOuterPt.z));

                    uv.Add(new Vector2(0f, samples[i]*uvScale));
                    uv.Add(new Vector2(0.5f, samples[i]*uvScale));
                    uv.Add(new Vector2(1f, samples[i]*uvScale));

                    if (i < steps)
                    {
                        Vector3 start=Vector3.Lerp(a,b,samples[i])+side*(halfW*sign);
                        Vector3 end=Vector3.Lerp(a,b,samples[i+1])+side*(halfW*sign);
                        if(junctionMask.OccupiedAlong(new Vector2(start.x,start.z),new Vector2(end.x,end.z),road,heading))continue;
                        int k = baseIdx + i * 3;
                        triangles.Add(k); triangles.Add(k + 1); triangles.Add(k + 3);
                        triangles.Add(k + 1); triangles.Add(k + 4); triangles.Add(k + 3);
                        triangles.Add(k + 1); triangles.Add(k + 2); triangles.Add(k + 4);
                        triangles.Add(k + 2); triangles.Add(k + 5); triangles.Add(k + 4);
                    }
                }
            }
            if (vertices.Count > 0)
            {
                AddSurface("Street curb", vertices, uv, triangles, material, false);
            }
        }
        void AddSurface(string label,List<Vector3> vertices,List<Vector2> uv,List<int> triangles,Material material,bool collision)
        {
            QueueRoadSurface(vertices,uv,triangles,material);
            if(!collision)return;
            // Keep each original collision strip and its name for terrain/traffic queries.
            var mesh=new Mesh{name=label+" collision"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();meshes.Add(mesh);
            var obj=new GameObject(label);obj.transform.SetParent(transform,false);obj.AddComponent<MeshCollider>().sharedMesh=mesh;
        }

        void ConfigureBuildingLod(GameObject building,Mesh shell,Material roofSurface,Material distantWall)
        {
            // Keep the full footprint and its collision at every distance. Only the
            // expensive close-up facade renderers give way to one textured shell.
            var near=building.GetComponentsInChildren<Renderer>(true);
            var distant=new GameObject("Distant textured facade");
            distant.transform.SetParent(building.transform,false);
            distant.AddComponent<MeshFilter>().sharedMesh=shell;
            var distantRenderer=distant.AddComponent<MeshRenderer>();
            distantRenderer.sharedMaterials=new[]{roofSurface,distantWall};
            var group=building.AddComponent<LODGroup>();
            group.fadeMode=LODFadeMode.CrossFade;
            group.SetLODs(new[]{new LOD(.055f,near),new LOD(.0008f,new Renderer[]{distantRenderer})});
            group.RecalculateBounds();
        }
        void BuildRoadSigns(List<MapFeature> features,Vector2 focus)
        {
            SectorSignage.BuildRoads(this,WorldDetailPlanner.RoadsNearFocus(features,focus),Material(new Color(.035f,.19f,.14f)),Material(new Color(.36f,.39f,.4f)));
        }
        bool BuildPitchedRoof(MapFeature feature,Transform parent,float height,Material material)
        {
            string shape=MapFeatureStyle.Text(feature,"roof_shape");
            if((shape!="gabled" && shape!="pyramidal") || feature.Points.Count!=4) return false;
            var points=feature.Points;
            for(int i=0;i<4;i++)
                if(Mathf.Abs(Vector2.Dot((points[(i+1)%4]-points[i]).normalized,(points[(i+2)%4]-points[(i+1)%4]).normalized))>.25f) return false;
            float rise=Mathf.Clamp(MapFeatureStyle.Number(feature,"roof_height",2),.5f,6);
            var vertices=new List<Vector3>(); var indices=new List<int>();
            int start=Vector2.Distance(points[0],points[1])>Vector2.Distance(points[1],points[2])?1:0;
            for(int i=0;i<4;i++) vertices.Add(To3(points[(start+i)%4],height));
            if(shape=="gabled")
            {
                vertices.Add((vertices[0]+vertices[1])*.5f+Vector3.up*rise);
                vertices.Add((vertices[2]+vertices[3])*.5f+Vector3.up*rise);
                indices.AddRange(new[]{0,1,4,2,3,5,1,2,5,1,5,4,3,0,4,3,4,5});
            }
            else
            {
                vertices.Add((vertices[0]+vertices[1]+vertices[2]+vertices[3])*.25f+Vector3.up*rise);
                for(int i=0;i<4;i++) indices.AddRange(new[]{i,(i+1)%4,4});
            }
            // Orient faces outwards, independent of the footprint's original winding.
            Vector3 center=(vertices[0]+vertices[1]+vertices[2]+vertices[3])*.25f+Vector3.up*(rise*.25f);
            for(int i=0;i<indices.Count;i+=3)
            {
                var a=vertices[indices[i]]; var b=vertices[indices[i+1]]; var c=vertices[indices[i+2]];
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),(a+b+c)/3-center)<0) { int swap=indices[i+1]; indices[i+1]=indices[i+2]; indices[i+2]=swap; }
            }
            var mesh=new Mesh{name="Mapped pitched roof"}; mesh.SetVertices(vertices); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var roofObject=new GameObject("Mapped pitched roof"); roofObject.transform.SetParent(parent,false);
            roofObject.AddComponent<MeshFilter>().sharedMesh=mesh; roofObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            roofObject.AddComponent<MeshCollider>().sharedMesh=mesh;
            return true;
        }
        public void BuildLadder(List<Vector2> points,float height,Material material)
        {
            int edge=0; float longest=0;
            for(int i=0;i<points.Count;i++)
            {
                float length=(points[(i+1)%points.Count]-points[i]).sqrMagnitude;
                if(length>longest) { longest=length; edge=i; }
            }
            if(longest<1) return;
            Vector3 a=To3(points[edge],0), b=To3(points[(edge+1)%points.Count],0);
            Vector3 along=(b-a).normalized, outward=Vector3.Cross(Vector3.up,along);
            Vector3 wall=(a+b)/2, foot=wall+outward*.65f;
            float ground=Ground(foot.x+outward.x*.5f,foot.z+outward.z*.5f);
            float ladderBottom=ground+.08f;
            float ladderHeight=height-ladderBottom;
            if(ladderHeight<1) return;
            Quaternion rotation=Quaternion.LookRotation(outward);
            for(int side=-1;side<=1;side+=2)
            {
                var rail=Box("Ladder rail",foot+along*(side*.55f)+Vector3.up*(ladderBottom+ladderHeight/2),new Vector3(.1f,ladderHeight,.1f),material);
                rail.transform.rotation=rotation;
            }
            BuildLadderRungs(foot,rotation,ladderBottom+.25f,height-.05f,material);
            var landing=Box("Ladder roof landing",wall-outward*.55f+Vector3.up*(height+.08f),new Vector3(1.5f,.15f,2.4f),material);
            landing.transform.rotation=rotation;
            Ladders.Add(new LadderRoute { Owner=this,
                Bottom=transform.TransformPoint(foot+outward*.3f+Vector3.up*(ladderBottom+.1f)),
                Top=transform.TransformPoint(foot+outward*.3f+Vector3.up*(height+.7f)),
                Landing=transform.TransformPoint(wall-outward*.7f+Vector3.up*(height+.22f)) });
            var route=Ladders[Ladders.Count-1];
            var linkObject=new GameObject("Rooftop ladder navigation link");
            linkObject.transform.SetParent(transform,false);
            linkObject.transform.position=route.Bottom;
            var link=linkObject.AddComponent<NavMeshLink>();
            link.startPoint=Vector3.zero; link.endPoint=route.Top-route.Bottom;
            link.width=1.2f; link.bidirectional=true; link.costModifier=1f;
        }

        void BuildLadderRungs(Vector3 foot,Quaternion rotation,float bottom,float top,Material material)
        {
            var vertices=new List<Vector3>();
            var triangles=new List<int>();
            for(float y=bottom;y<top;y+=.4f)
            {
                int start=vertices.Count;
                float h=y-foot.y;
                vertices.Add(new Vector3(-.6f,h-.04f,-.05f));
                vertices.Add(new Vector3(.6f,h-.04f,-.05f));
                vertices.Add(new Vector3(.6f,h+.04f,-.05f));
                vertices.Add(new Vector3(-.6f,h+.04f,-.05f));
                vertices.Add(new Vector3(-.6f,h-.04f,.05f));
                vertices.Add(new Vector3(.6f,h-.04f,.05f));
                vertices.Add(new Vector3(.6f,h+.04f,.05f));
                vertices.Add(new Vector3(-.6f,h+.04f,.05f));
                int[] faces={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
                for(int i=0;i<faces.Length;i++) triangles.Add(start+faces[i]);
            }
            if(vertices.Count==0) return;
            var mesh=new Mesh{name="Ladder rungs"};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            meshes.Add(mesh);
            var rungs=new GameObject("Ladder rungs");
            rungs.transform.SetParent(transform,false);
            rungs.transform.localPosition=foot;
            rungs.transform.localRotation=rotation;
            rungs.AddComponent<MeshFilter>().sharedMesh=mesh;
            rungs.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        public bool HasLadder(Vector3 worldPoint) => HasStairs(worldPoint);

        public bool HasStairs(Vector3 worldPoint)
        {
            foreach (var route in Ladders)
            {
                if (route.Owner == this && (Vector3.Distance(route.Landing, worldPoint) < 18f || Vector3.Distance(route.Top, worldPoint) < 18f))
                    return true;
            }
            return false;
        }

        public bool EnsureRooftopLadder(Vector3 worldRoofPoint) => EnsureRooftopStairs(worldRoofPoint);

        public bool EnsureRooftopStairs(Vector3 worldRoofPoint)
        {
            if (HasStairs(worldRoofPoint)) return true;

            Vector3 localPt = transform.InverseTransformPoint(worldRoofPoint);
            Vector2 pt2D = new Vector2(localPt.x, localPt.z);

            MapFeature targetFeature = null;
            float closestDistSq = float.MaxValue;
            foreach (var f in Features)
            {
                if (f.Kind != "building" || f.Points == null || f.Points.Count < 3) continue;
                if (IsPointInPolygon(pt2D, f.Points))
                {
                    targetFeature = f;
                    break;
                }
                Vector2 center = Vector2.zero;
                for (int i = 0; i < f.Points.Count; i++) center += f.Points[i];
                center /= f.Points.Count;
                float d = (center - pt2D).sqrMagnitude;
                if (d < closestDistSq && d < 35f * 35f)
                {
                    closestDistSq = d;
                    targetFeature = f;
                }
            }

            if (targetFeature == null) return false;

            var ladderMaterial = Material(new Color(.16f, .18f, .18f));
            float roofHeight = localPt.y;
            BuildLadder(targetFeature.Points, roofHeight, ladderMaterial);
            return true;
        }

        static bool IsPointInPolygon(Vector2 p, List<Vector2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
            }
            return inside;
        }

        public void BuildStairs(List<Vector2> points, float height, Material material)
        {
            BuildLadder(points, height, material);
        }
        void CreateRooftopEdgeBarrier(Transform parent, Vector2 mid, Vector2 along, float length, float h, Material trim)
        {
            float barrierH = 0.82f;
            var parapet = new GameObject("Rooftop Safety Barrier");
            parapet.transform.SetParent(parent, false);
            parapet.transform.localPosition = new Vector3(mid.x, h + barrierH * 0.5f, mid.y);
            parapet.transform.localRotation = Quaternion.LookRotation(new Vector3(along.x, 0, along.y));
            var col = parapet.AddComponent<BoxCollider>();
            col.size = new Vector3(0.35f, barrierH, length + 0.05f);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Barrier Mesh";
            visual.transform.SetParent(parapet.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.24f, barrierH, length);
            var rend = visual.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = trim;
            var vCol = visual.GetComponent<Collider>();
            if (vCol != null) Destroy(vCol);
        }

        void BuildRooftopProps(List<Vector2> points, float height, Vector2 center, Material material)
        {
            var random = new System.Random((int)(center.x * 100 + center.y * 100));
            int propType = random.Next(0, 8);
            float roofSpan = 0f;
            foreach (var p in points) roofSpan = Mathf.Max(roofSpan, Vector2.Distance(center, p));
            if (roofSpan < 6.0f && (propType == 0 || propType == 6 || propType == 4 || propType == 7))
            {
                propType = 2; // Compact HVAC or vents for small roofs
            }
            var darkMetal = Styled(new Color(.2f, .22f, .25f), false);
            var rustMetal = Styled(new Color(.45f, .28f, .20f), false);
            var sandbagMat = Styled(new Color(.72f, .62f, .45f), false);
            var militaryGreen = Styled(new Color(.24f, .33f,.18f), false);

            if (propType == 0)
            {
                // Water Tower Asset
                Vector3 basePos = new Vector3(center.x, height, center.y);
                float tankRadius = 1.6f, tankHeight = 2.4f, legHeight = 3.2f;
                // 4 Leg pillars
                foreach(var offset in new[]{ new Vector3(-1f,0,-1f), new Vector3(1f,0,-1f), new Vector3(-1f,0,1f), new Vector3(1f,0,1f) })
                {
                    Box("Water Tower Leg", basePos + offset + Vector3.up * (legHeight / 2f), new Vector3(0.2f, legHeight, 0.2f), darkMetal);
                }
                // Tank body
                Box("Water Tower Tank", basePos + Vector3.up * (legHeight + tankHeight / 2f), new Vector3(tankRadius * 2, tankHeight, tankRadius * 2), rustMetal);
                // Conical Roof Cap
                Box("Water Tower Roof", basePos + Vector3.up * (legHeight + tankHeight + 0.3f), new Vector3(tankRadius * 2.2f, 0.6f, tankRadius * 2.2f), material);
            }
            else if (propType == 1)
            {
                // Satellite Dish & Radar Asset
                Vector3 basePos = new Vector3(center.x, height, center.y);
                Box("Dish Pole", basePos + Vector3.up * 1.2f, new Vector3(0.18f, 2.4f, 0.18f), darkMetal);
                var dish = Box("Satellite Dish Reflector", basePos + Vector3.up * 2.2f, new Vector3(1.8f, 1.8f, 0.25f), material);
                dish.transform.rotation = Quaternion.Euler(35, (float)random.NextDouble() * 360f, 0);
                Box("Dish Feed Horn", basePos + Vector3.up * 2.2f + dish.transform.forward * 0.6f, new Vector3(0.12f, 0.12f, 0.8f), darkMetal);
            }
            else if (propType == 2)
            {
                // Dual Fan HVAC Roof Air Conditioner Asset
                Vector3 basePos = new Vector3(center.x, height + 0.75f, center.y);
                Box("HVAC Unit Body", basePos, new Vector3(2.6f, 1.5f, 1.6f), material);
                Box("HVAC Top Vent 1", basePos + Vector3.up * 0.8f + Vector3.left * 0.6f, new Vector3(0.9f, 0.12f, 0.9f), darkMetal);
                Box("HVAC Top Vent 2", basePos + Vector3.up * 0.8f + Vector3.right * 0.6f, new Vector3(0.9f, 0.12f, 0.9f), darkMetal);
                Box("HVAC Side Duct Pipe", basePos + Vector3.right * 1.4f, new Vector3(0.4f, 0.9f, 0.4f), rustMetal);
            }
            else if (propType == 3)
            {
                // Rooftop Sniper Sandbag Nest
                Vector3 basePos = new Vector3(center.x, height + 0.35f, center.y);
                // Crescent sandbag wall
                Box("Sniper Nest Wall L", basePos + Vector3.left * 1.1f, new Vector3(0.45f, 0.7f, 2.2f), sandbagMat);
                Box("Sniper Nest Wall F", basePos + Vector3.forward * 1.1f, new Vector3(2.2f, 0.7f, 0.45f), sandbagMat);
                Box("Sniper Nest Wall R", basePos + Vector3.right * 1.1f, new Vector3(0.45f, 0.7f, 2.2f), sandbagMat);
                // Tactical Ammo Crate inside nest
                Box("Tactical Supply Crate", basePos + Vector3.up * 0.1f, new Vector3(0.7f, 0.5f, 0.5f), militaryGreen);
            }
            else if (propType == 5)
            {
                // Military Comms Mast on rooftop
                Vector3 basePos = new Vector3(center.x, height, center.y);
                Box("Mast Base", basePos + Vector3.up * 0.15f, new Vector3(0.6f, 0.3f, 0.6f), darkMetal);
                Box("Mast Pole", basePos + Vector3.up * 3.0f,  new Vector3(0.12f, 6.0f, 0.12f), darkMetal);
                Box("Mast Arm L", basePos + Vector3.up * 5.5f, new Vector3(2.4f, 0.08f, 0.08f), darkMetal);
                Box("Mast Arm R", basePos + Vector3.up * 4.0f, new Vector3(1.8f, 0.08f, 0.08f), darkMetal);
                Box("Mast Warning Light", basePos + Vector3.up * 6.1f, new Vector3(0.22f, 0.22f, 0.22f), Styled(new Color(1f, .08f, .04f), false));
            }
            else if (propType == 6)
            {
                // Generator Room with exhaust flue
                Vector3 basePos = new Vector3(center.x, height, center.y);
                Box("Generator Housing", basePos + Vector3.up * 1.0f, new Vector3(3.2f, 2.0f, 2.0f), darkMetal);
                Box("Exhaust Flue", basePos + Vector3.up * 2.8f + new Vector3(0.8f, 0, 0), new Vector3(0.25f, 1.6f, 0.25f), rustMetal);
                Box("Fuel Tank", basePos + Vector3.up * 0.5f + new Vector3(-1.6f, 0, 0), new Vector3(1.0f, 1.0f, 1.6f), militaryGreen);
                Box("Fuel Tank Band", basePos + Vector3.up * 0.75f + new Vector3(-1.6f, 0, 0), new Vector3(1.02f, 0.12f, 1.62f), sandbagMat);
            }
            else if (propType == 4 || propType == 7)
            {
                // High-impact 3D Rooftop Advertising Billboard
                BuildRooftopBillboard(center, height, darkMetal, rustMetal, random);
            }
            else
            {
                // Standard Rooftop Machinery & Vent Pipes
                int numProps = random.Next(2, 4);
                for (int i = 0; i < numProps; i++)
                {
                    float offsetX = (float)(random.NextDouble() * 4 - 2);
                    float offsetZ = (float)(random.NextDouble() * 4 - 2);
                    float propWidth = 0.8f + (float)random.NextDouble() * 1.5f;
                    float propHeight = 0.5f + (float)random.NextDouble() * 1.5f;
                    float propDepth = 0.8f + (float)random.NextDouble() * 1.5f;
                    var prop = Box("Rooftop Prop", new Vector3(center.x + offsetX, height + propHeight / 2, center.y + offsetZ), new Vector3(propWidth, propHeight, propDepth), material);
                    prop.transform.localRotation = Quaternion.Euler(0, random.Next(0, 4) * 90f, 0);
                }
            }
        }

        void BuildRooftopBillboard(Vector2 center, float height, Material darkMetal, Material rustMetal, System.Random random)
        {
            Vector3 basePos = new Vector3(center.x, height, center.y);
            Vector3 toFocus = (DetailFocus - basePos);
            toFocus.y = 0;
            Quaternion rot = (toFocus.sqrMagnitude > 4f) 
                ? Quaternion.LookRotation(toFocus.normalized) 
                : Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);

            rot *= Quaternion.Euler(0, (float)(random.NextDouble() * 24f - 12f), 0);

            float legH = 2.4f;
            float boardW = 7.4f;
            float boardH = 3.7f;
            float boardY = legH + (boardH * 0.5f);

            var legL = Box("Billboard Leg L", basePos + rot * new Vector3(-2.6f, legH * 0.5f, 0), new Vector3(0.24f, legH, 0.24f), darkMetal);
            legL.transform.rotation = rot;
            var legR = Box("Billboard Leg R", basePos + rot * new Vector3(2.6f, legH * 0.5f, 0), new Vector3(0.24f, legH, 0.24f), darkMetal);
            legR.transform.rotation = rot;

            var kickL = Box("Billboard Kickstand L", basePos + rot * new Vector3(-2.6f, legH * 0.5f, -0.9f), new Vector3(0.18f, legH * 1.15f, 0.18f), rustMetal);
            kickL.transform.rotation = rot * Quaternion.Euler(30f, 0, 0);
            var kickR = Box("Billboard Kickstand R", basePos + rot * new Vector3(2.6f, legH * 0.5f, -0.9f), new Vector3(0.18f, legH * 1.15f, 0.18f), rustMetal);
            kickR.transform.rotation = rot * Quaternion.Euler(30f, 0, 0);

            var crossBeam = Box("Billboard Truss Cross", basePos + rot * new Vector3(0, legH, 0), new Vector3(boardW * 0.95f, 0.18f, 0.18f), darkMetal);
            crossBeam.transform.rotation = rot;

            var backFrame = Box("Billboard Back Frame", basePos + rot * new Vector3(0, boardY, -0.09f), new Vector3(boardW + 0.1f, boardH + 0.1f, 0.06f), darkMetal);
            backFrame.transform.rotation = rot;

            var catwalk = Box("Billboard Catwalk", basePos + rot * new Vector3(0, legH, 0.45f), new Vector3(boardW + 0.4f, 0.1f, 0.8f), darkMetal);
            catwalk.transform.rotation = rot;
            var rail = Box("Billboard Catwalk Rail", basePos + rot * new Vector3(0, legH + 0.6f, 0.8f), new Vector3(boardW + 0.4f, 0.08f, 0.08f), rustMetal);
            rail.transform.rotation = rot;

            for (int i = -1; i <= 1; i++)
            {
                float xOff = i * 2.4f;
                var arm = Box("Billboard Light Arm", basePos + rot * new Vector3(xOff, boardY + boardH * 0.5f + 0.12f, 0.45f), new Vector3(0.08f, 0.08f, 0.85f), darkMetal);
                arm.transform.rotation = rot;
                var fixture = Box("Billboard Lamp Fixture", basePos + rot * new Vector3(xOff, boardY + boardH * 0.5f + 0.05f, 0.85f), new Vector3(0.38f, 0.14f, 0.28f), rustMetal);
                fixture.transform.rotation = rot;
            }

            var board = Box("Rooftop Billboard Display", basePos + rot * new Vector3(0, boardY, 0), new Vector3(boardW, boardH, 0.12f), darkMetal);
            board.transform.rotation = rot;

            var billboardComp = board.AddComponent<InGameBillboard>();
            billboardComp.Initialize(board.GetComponent<MeshRenderer>(), random.Next(0, 100));
        }

        System.Collections.IEnumerator BuildStreetPropsAsync(List<MapFeature> features, Material metal, Material wood,Vector2 focus)
        {
            var random = new System.Random(888);
            float sliceStart = Time.realtimeSinceStartup;
            int propBudget = Application.isMobilePlatform ? 80 : 380;
            
            // Materials for rich urban and military combat environment assets
            var concreteMat = Styled(new Color(.6f, .62f, .64f), false);
            var hazardYellow = Styled(new Color(.95f, .78f, .12f), false);
            var sandbagMat = Styled(new Color(.72f, .62f, .45f), false);
            var militaryGreen = Styled(new Color(.24f, .33f,.18f), false);
            var orangeMat = Styled(new Color(.95f, .35f, .05f), false);
            var rustMetal = Styled(new Color(.45f, .28f, .20f), false);
            var burntCarMat = Styled(new Color(.12f, .12f, .12f), false);

            var containerColors = new[]{
                Styled(new Color(.65f, .12f, .10f), false), // Freight Red
                Styled(new Color(.10f, .28f, .55f), false), // Freight Blue
                Styled(new Color(.22f, .32f, .22f), false), // Military Olive
                Styled(new Color(.75f, .42f, .12f), false), // Cargo Orange
            };

            var carColors = new[]{
                Material(new Color(.12f,.12f,.14f)),  // Black
                Material(new Color(.85f,.85f,.82f)),  // White
                Material(new Color(.55f,.08f,.06f)),  // Red
                Material(new Color(.15f,.22f,.42f)),  // Dark Blue
                Material(new Color(.35f,.38f,.35f)),  // Grey
            };
            var windowMat = Material(new Color(.15f,.25f,.32f));
            var civicPoints=new List<Vector2>();
            foreach(var site in features)
                if((site.Kind=="place" || site.Kind=="building") && site.Points!=null && site.Points.Count>0 &&
                    (!string.IsNullOrWhiteSpace(site.Name) || !string.IsNullOrWhiteSpace(site.Landmark)))
                    civicPoints.Add(WorldDetailPlanner.Center(site));

            foreach(var feature in WorldDetailPlanner.RoadsNearFocus(features,focus))
            {
                if(propBudget <= 0) break;
                if(feature.Kind!="road") continue;
                float width = MapFeatureStyle.RoadWidth(feature);
                if (width < 3) continue;
                string roadClass=MapFeatureStyle.Text(feature,"class");
                bool civilianStreet=roadClass=="residential" || roadClass=="living_street" || roadClass=="service" ||
                    roadClass=="tertiary" || roadClass=="unclassified";
                
                for(int i=1;i<feature.Points.Count && propBudget > 0;i++)
                {
                    if(Time.realtimeSinceStartup - sliceStart > 0.004f) { yield return null; sliceStart = Time.realtimeSinceStartup; }
                    var a=To3(feature.Points[i-1],0); var b=To3(feature.Points[i],0);
                    Vector3 direction = (b-a).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
                    
                    float length = (b-a).magnitude;
                    for(float distance=5;distance<length-5 && propBudget > 0;distance+=18)
                    {
                        if(Time.realtimeSinceStartup-sliceStart>.003f) { yield return null; sliceStart=Time.realtimeSinceStartup; }
                        Vector3 point=Vector3.Lerp(a,b,distance/length);
                        foreach (float side in new float[] { -1, 1 })
                        {
                            Vector3 propPos = point + right * side * ((width/2) + 1.2f);
                            if(IsWaterAt(propPos.x,propPos.z))continue;
                            if(!StreetPlacement.Clear(features,feature,new Vector2(propPos.x,propPos.z),new Vector2(direction.x,direction.z),1.8f,3.6f))continue;
                            propPos.y = Ground(propPos.x, propPos.z) + 0.1f;
                            
                            // Named public places and residential streets receive everyday
                            // furniture and parked cars instead of random military clutter.
                            bool nearPlace=civicPoints.Exists(p=>(p-new Vector2(propPos.x,propPos.z)).sqrMagnitude<900f);
                            double r = civilianStreet || nearPlace ? .46 + random.NextDouble()*.26 : random.NextDouble();
                            if (r < 0.14)
                            {
                                // Streetlamp with angled fixture
                                StreetBox("Streetlamp Pole", propPos + Vector3.up * 2f, new Vector3(0.15f, 4f, 0.15f), metal);
                                var head = StreetBox("Streetlamp Head", propPos + Vector3.up * 4f - right * side * 0.5f, new Vector3(0.3f, 0.1f, 1.2f), metal);
                                head.transform.rotation = Quaternion.LookRotation(right * side);
                                propBudget--;
                            }
                            else if (r < 0.18)
                            {
                                // Street-level Monopole Commercial Billboard / Sidewalk Ad Kiosk
                                if (width >= 4.5f)
                                {
                                    Vector3 boardPos = propPos + right * side * 1.4f;
                                    float poleH = 5.2f;
                                    float sBoardW = 5.4f;
                                    float sBoardH = 2.7f;
                                    Quaternion bRot = Quaternion.LookRotation(direction);

                                    var pole = StreetBox("Monopole Pillar", boardPos + Vector3.up * (poleH * 0.5f), new Vector3(0.36f, poleH, 0.36f), metal);
                                    pole.transform.rotation = bRot;

                                    var collar = StreetBox("Monopole Collar", boardPos + Vector3.up * (poleH + sBoardH * 0.5f), new Vector3(0.48f, sBoardH * 0.6f, 0.48f), rustMetal);
                                    collar.transform.rotation = bRot;

                                    var sBack = StreetBox("Monopole Back Frame", boardPos + Vector3.up * (poleH + sBoardH * 0.5f) - direction * 0.08f, new Vector3(sBoardW + 0.1f, sBoardH + 0.1f, 0.05f), metal);
                                    sBack.transform.rotation = bRot;

                                    var sBoard = StreetBox("Street Billboard Display", boardPos + Vector3.up * (poleH + sBoardH * 0.5f), new Vector3(sBoardW, sBoardH, 0.10f), metal);
                                    sBoard.transform.rotation = bRot;

                                    var sComp = sBoard.AddComponent<InGameBillboard>();
                                    sComp.Initialize(sBoard.GetComponent<MeshRenderer>(), random.Next(0, 100));
                                    propBudget -= 3;
                                }
                                else
                                {
                                    Vector3 kioskPos = propPos + right * side * 0.4f;
                                    Quaternion kRot = Quaternion.LookRotation(direction);
                                    float kH = 2.4f, kW = 1.25f;

                                    var kBase = StreetBox("Ad Kiosk Base", kioskPos + Vector3.up * 0.08f, new Vector3(kW + 0.1f, 0.16f, 0.32f), metal);
                                    kBase.transform.rotation = kRot;

                                    var kBack = StreetBox("Ad Kiosk Back", kioskPos + Vector3.up * (kH * 0.5f) - direction * 0.06f, new Vector3(kW + 0.04f, kH, 0.06f), metal);
                                    kBack.transform.rotation = kRot;

                                    var kDisplay = StreetBox("Ad Kiosk Display", kioskPos + Vector3.up * (kH * 0.5f), new Vector3(kW, kH * 0.85f, 0.08f), metal);
                                    kDisplay.transform.rotation = kRot;

                                    var kComp = kDisplay.AddComponent<InGameBillboard>();
                                    kComp.Initialize(kDisplay.GetComponent<MeshRenderer>(), random.Next(0, 100));
                                    propBudget -= 2;
                                }
                            }
                            else if (r < 0.24)
                            {
                                // Concrete Jersey Highway Barrier with Hazard Yellow Striping
                                Vector3 barrierPos = propPos + right * side * 0.2f;
                                RoadBarrier(barrierPos, direction, true, concreteMat);
                                propBudget--;
                            }
                            else if (r < 0.28)
                            {
                                // Industrial Shipping Cargo Container (2.4m x 2.4m x 6m)
                                Vector3 containerPos = propPos + right * side * 1.5f;
                                var containerModel = Resources.Load<GameObject>("Models/Container");
                                if (containerModel != null)
                                {
                                    var cObj = new GameObject("Shipping Container Model");
                                    cObj.transform.SetParent(transform, false);
                                    var visual = Instantiate(containerModel, cObj.transform, false);
                                    var containerTex = Resources.Load<Texture2D>("Textures/ContainerDiffuse");
                                    Color[] containerTints = new Color[] {
                                        new Color(0.85f, 0.35f, 0.25f), // Freight Red
                                        new Color(0.40f, 0.65f, 0.85f), // Maritime Blue
                                        new Color(0.55f, 0.68f, 0.45f), // Olive Drab
                                        new Color(0.90f, 0.60f, 0.20f)  // Cargo Gold
                                    };
                                    Color tint = containerTints[random.Next(containerTints.Length)];
                                    Material containerMat = Material(tint);
                                    if (containerTex != null) containerMat.mainTexture = containerTex;
                                    containerMat.SetFloat("_Glossiness", 0.35f);
                                    containerMat.SetFloat("_Metallic", 0.45f);

                                    foreach (var cr in visual.GetComponentsInChildren<Renderer>(true))
                                    {
                                        cr.sharedMaterial = containerMat;
                                    }
                                    var bounds = ImportedVisual.LocalBounds(cObj.transform);
                                    float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                                    if (longest > 0.001f && float.IsFinite(longest))
                                    {
                                        visual.transform.localScale *= (6.0f / longest);
                                        bounds = ImportedVisual.LocalBounds(cObj.transform);
                                        visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                                        bounds = ImportedVisual.LocalBounds(cObj.transform);
                                    }
                                    cObj.transform.rotation = Quaternion.LookRotation(direction);
                                    cObj.transform.localPosition = containerPos;
                                    var col = cObj.AddComponent<BoxCollider>();
                                    col.center = bounds.center;
                                    col.size = bounds.size;
                                }
                                else
                                {
                                    Material containerMat = containerColors[random.Next(containerColors.Length)];
                                    var container = Box("Shipping Container", containerPos + Vector3.up * 1.25f, new Vector3(2.4f, 2.5f, 5.8f), containerMat);
                                    container.transform.rotation = Quaternion.LookRotation(direction);
                                }
                                propBudget -= 2;
                            }
                            else if (r < 0.34)
                            {
                                // Military Sandbag Checkpoint & Supply Crates
                                Vector3 cpPos = propPos;
                                var sandbagWall = Box("Sandbag Fortification", cpPos + Vector3.up * 0.45f, new Vector3(0.5f, 0.9f, 2.2f), sandbagMat);
                                sandbagWall.transform.rotation = Quaternion.LookRotation(direction);
                                var crate = Box("Military Supply Crate", cpPos + Vector3.up * 0.35f + direction * 0.9f, new Vector3(0.75f, 0.7f, 0.75f), militaryGreen);
                                crate.transform.rotation = Quaternion.LookRotation(direction);
                                propBudget -= 2;
                            }
                            else if (r < 0.40)
                            {
                                // Roadside Concrete Highway Barrier with Warning Stripe
                                Vector3 barPos = propPos + right * side * 0.3f;
                                RoadBarrier(barPos, direction, false, concreteMat);
                                propBudget -= 2;
                            }
                            else if (r < 0.46)
                            {
                                // Destroyed / Burned Wrecked Vehicle Frame
                                Vector3 wreckPos = propPos - right * side * 0.5f;
                                var wreckBody = Box("Wrecked Vehicle Chassis", wreckPos + Vector3.up * 0.55f, new Vector3(2.0f, 0.7f, 4.2f), burntCarMat);
                                wreckBody.transform.rotation = Quaternion.LookRotation(direction + right * 0.3f);
                                Box("Oil Drum Barrel", wreckPos + direction * 2.2f + Vector3.up * 0.45f, new Vector3(0.6f, 0.9f, 0.6f), rustMetal);
                                propBudget -= 2;
                            }
                            else if (r < 0.52)
                            {
                                // Bench & Trash Bin
                                var seat = StreetBox("Bench Seat", propPos + Vector3.up * 0.4f, new Vector3(1.8f, 0.1f, 0.6f), wood);
                                seat.transform.rotation = Quaternion.LookRotation(direction);
                                var back = StreetBox("Bench Back", propPos + Vector3.up * 0.8f + right * side * 0.25f, new Vector3(1.8f, 0.4f, 0.1f), wood);
                                back.transform.rotation = Quaternion.LookRotation(direction);
                                StreetBox("Trash Bin", propPos + direction * 1.5f + Vector3.up * 0.5f, new Vector3(0.6f, 1f, 0.6f), metal);
                                propBudget--;
                            }
                            else if (r < 0.72)
                            {
                                // Parked Car Asset
                                Vector3 carPos = point + right * side * (width*.5f+1.4f);
                                if(!StreetPlacement.Clear(features,feature,new Vector2(carPos.x,carPos.z),new Vector2(direction.x,direction.z),2.3f,4.8f))continue;
                                carPos.y = Ground(carPos.x, carPos.z) + 0.12f;
                                Vector3 carAhead = carPos + direction * 2.2f; carAhead.y = Ground(carAhead.x, carAhead.z);
                                Vector3 carBehind = carPos - direction * 2.2f; carBehind.y = Ground(carBehind.x, carBehind.z);
                                Vector3 carSlopeDir = (carAhead - carBehind).normalized;
                                Quaternion carRot = carSlopeDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(carSlopeDir) : Quaternion.LookRotation(direction);
                                bool carTooClose=false;
                                foreach(var existing in GetComponentsInChildren<Transform>(true))
                                    if(existing!=null && existing!=transform && existing.name=="Parked Car" && Vector3.Distance(existing.position,carPos)<5.2f)
                                    { carTooClose=true; break; }
                                if(carTooClose) { propBudget-=1; continue; }
                                var carAssets = ModelLibrary.Load("Cars");
                                var importedCar = (carAssets != null && carAssets.Length > 0) ? carAssets[random.Next(carAssets.Length)] : Resources.Load<GameObject>("Models/PoliceCar");
                                if (importedCar != null)
                                {
                                    var carObj = new GameObject("Parked Car");
                                    carObj.transform.SetParent(transform,false);
                                    var visual = Instantiate(importedCar, carObj.transform, false);
                                    bool isPolice = importedCar.name.ToLowerInvariant().Contains("police");
                                    
                                    if (isPolice)
                                    {
                                        foreach (var vr in visual.GetComponentsInChildren<Renderer>(true))
                                        {
                                            string rName = vr.name.ToLowerInvariant();
                                            bool isWhite = rName.Contains("door") || rName.Contains("roof") || rName.Contains("white");
                                            var pMat = Material(isWhite ? new Color(0.92f, 0.92f, 0.94f) : new Color(0.06f, 0.08f, 0.11f));
                                            pMat.SetFloat("_Glossiness", 0.85f);
                                            vr.sharedMaterial = pMat;
                                        }
                                    }
                                    else
                                    {
                                        var carAtlas = Resources.Load<Texture2D>("Models/Cars/Textures/cars_0");
                                        Material carAtlasMat = null;
                                        if (carAtlas != null)
                                        {
                                            carAtlasMat = Material(Color.white);
                                            carAtlasMat.mainTexture = carAtlas;
                                            carAtlasMat.SetFloat("_Glossiness", 0.65f);
                                        }
                                        Color[] richPaints = new Color[] {
                                            new Color(0.78f, 0.08f, 0.08f), // Crimson Gloss
                                            new Color(0.12f, 0.22f, 0.42f), // Midnight Blue
                                            new Color(0.55f, 0.58f, 0.62f), // Liquid Silver
                                            new Color(0.85f, 0.65f, 0.12f), // Metallic Gold
                                            new Color(0.18f, 0.20f, 0.22f)  // Dark Graphite
                                        };
                                        Material glossPaint = Material(richPaints[random.Next(richPaints.Length)]);
                                        glossPaint.SetFloat("_Glossiness", 0.85f);
                                        glossPaint.SetFloat("_Metallic", 0.5f);

                                        foreach (var vr in visual.GetComponentsInChildren<Renderer>(true))
                                        {
                                            string rName = vr.name.ToLowerInvariant();
                                            if (rName.Contains("glass") || rName.Contains("window") || rName.Contains("windshield"))
                                            {
                                                var gMat = Material(new Color(0.12f, 0.18f, 0.24f));
                                                gMat.SetFloat("_Glossiness", 0.95f);
                                                vr.sharedMaterial = gMat;
                                            }
                                            else
                                            {
                                                vr.sharedMaterial = carAtlasMat != null ? carAtlasMat : glossPaint;
                                            }
                                        }
                                    }
                                     var carBounds = ImportedVisual.AlignVehicle(carObj, visual, false, isPolice);
                                    float longest = Mathf.Max(carBounds.size.x, Mathf.Max(carBounds.size.y, carBounds.size.z));
                                    if(longest<.001f || !float.IsFinite(longest)) { Destroy(carObj); continue; }
                                    visual.transform.localScale *= 4.4f/longest;
                                    carBounds = ImportedVisual.LocalBounds(carObj.transform);
                                    visual.transform.localPosition -= new Vector3(carBounds.center.x, carBounds.min.y, carBounds.center.z);
                                    carBounds = ImportedVisual.LocalBounds(carObj.transform);
                                    carObj.transform.localRotation = carRot;
                                    carObj.transform.localPosition = carPos + Vector3.up * .05f;
                                    var collider = carObj.AddComponent<BoxCollider>();
                                    collider.center = carBounds.center;
                                    collider.size = carBounds.size;
                                }
                                else
                                {
                                    Material paint = carColors[random.Next(carColors.Length)];
                                    var carBody = Box("Car Body", carPos + carRot * (Vector3.up * 0.55f), new Vector3(1.9f, 0.65f, 4.2f), paint);
                                    carBody.transform.rotation = carRot;
                                    var carCabin = Box("Car Cabin", carPos + carRot * (Vector3.up * 1.15f - Vector3.forward * 0.3f), new Vector3(1.7f, 0.55f, 2.2f), windowMat);
                                    carCabin.transform.rotation = carRot;
                                    foreach(var wOffset in new[]{new Vector3(-.85f,.3f,1.2f), new Vector3(.85f,.3f,1.2f), new Vector3(-.85f,.3f,-1.2f), new Vector3(.85f,.3f,-1.2f)})
                                    {
                                        var wheelPos = carPos + carRot * wOffset;
                                        var wheel = Box("Wheel", wheelPos, new Vector3(.22f,.55f,.55f), metal);
                                        wheel.transform.rotation = carRot;
                                    }
                                }
                                propBudget -= 2;
                            }
                            else if (r < 0.74)
                            {
                                // Military Guard Tower â€” tall wooden watchtower
                                Vector3 towerPos = propPos + right * side * 0.8f;
                                float towerH = Ground(towerPos.x, towerPos.z) + 0.1f;
                                towerPos.y = towerH;
                                // 4 angled support legs
                                foreach (var legOff in new[] { new Vector3(-0.8f,0,-0.8f), new Vector3(0.8f,0,-0.8f), new Vector3(-0.8f,0,0.8f), new Vector3(0.8f,0,0.8f) })
                                    Box("Tower Leg", towerPos + legOff + Vector3.up * 2.2f, new Vector3(0.18f, 4.4f, 0.18f), wood);
                                // Platform floor
                                Box("Tower Platform", towerPos + Vector3.up * 4.6f, new Vector3(2.4f, 0.2f, 2.4f), wood);
                                // Guard cabin
                                Box("Tower Cabin", towerPos + Vector3.up * 5.4f, new Vector3(2.0f, 1.6f, 2.0f), militaryGreen);
                                // Peaked roof
                                Box("Tower Roof", towerPos + Vector3.up * 6.4f, new Vector3(2.4f, 0.3f, 2.4f), metal);
                                propBudget -= 4;
                            }
                            else if (r < 0.80)
                            {
                                // Fuel Drum Cluster â€” industrial oil drums
                                Vector3 drumBase = propPos;
                                var barrelModel = Resources.Load<GameObject>("Models/Barrel");
                                var drumOffsets = new[] { Vector3.zero, direction * 0.85f, right * side * 0.75f };
                                foreach (var dOff in drumOffsets)
                                {
                                    if (barrelModel != null)
                                    {
                                        var bObj = new GameObject("Fuel Drum Model");
                                        bObj.transform.SetParent(transform, false);
                                        var visual = Instantiate(barrelModel, bObj.transform, false);
                                        foreach (var br in visual.GetComponentsInChildren<Renderer>(true))
                                        {
                                            if (br.sharedMaterial == null || br.sharedMaterial.mainTexture == null || br.sharedMaterial.color == Color.white)
                                                br.sharedMaterial = rustMetal;
                                        }
                                        var bounds = ImportedVisual.LocalBounds(bObj.transform);
                                        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                                        if (longest > 0.001f && float.IsFinite(longest))
                                        {
                                            visual.transform.localScale *= (0.95f / longest);
                                            bounds = ImportedVisual.LocalBounds(bObj.transform);
                                            visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                                            bounds = ImportedVisual.LocalBounds(bObj.transform);
                                        }
                                        bObj.transform.localPosition = drumBase + dOff;
                                        var col = bObj.AddComponent<BoxCollider>();
                                        col.center = bounds.center;
                                        col.size = bounds.size;
                                        var exp = bObj.AddComponent<ExplosiveProp>();
                                        exp.maxHealth = 40f;
                                        exp.explosionRadius = 8f;
                                        exp.explosionDamage = 240f;
                                    }
                                    else
                                    {
                                        Box("Fuel Drum", drumBase + dOff + Vector3.up * 0.45f, new Vector3(0.56f, 0.9f, 0.56f), rustMetal);
                                        Box("Drum Band", drumBase + dOff + Vector3.up * 0.7f, new Vector3(0.58f, 0.1f, 0.58f), hazardYellow);
                                    }
                                }
                                propBudget -= 2;
                            }
                            else if (r < 0.86)
                            {
                                // Main Battle Tank
                                Vector3 tankPos = propPos + right * side * 2.2f;
                                var tankModel = Resources.Load<GameObject>("Models/Tank");
                                if (tankModel != null)
                                {
                                    var tankObj = new GameObject("Military Tank Model");
                                    tankObj.transform.SetParent(transform, false);
                                    var visual = Instantiate(tankModel, tankObj.transform, false);
                                    var bounds = ImportedVisual.AlignVehicle(tankObj, visual, true, false);
                                    float longest = Mathf.Max(bounds.size.x, bounds.size.z);
                                    if (longest > 0.001f && float.IsFinite(longest))
                                    {
                                        visual.transform.localScale *= (6.5f / longest);
                                        bounds = ImportedVisual.LocalBounds(tankObj.transform);
                                        visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                                        bounds = ImportedVisual.LocalBounds(tankObj.transform);
                                    }
                                    tankObj.transform.rotation = Quaternion.LookRotation(direction + right * ((float)random.NextDouble() * 0.4f - 0.2f));
                                    tankObj.transform.localPosition = tankPos;
                                    var col = tankObj.AddComponent<BoxCollider>();
                                    col.center = bounds.center;
                                    col.size = bounds.size;

                                    // Apply dark olive drab military armor material
                                    var tankMat = Styled(new Color(.20f, .26f, .18f), true);
                                    foreach (var rend in tankObj.GetComponentsInChildren<Renderer>(true))
                                        rend.sharedMaterial = tankMat;
                                }
                                else
                                {
                                    var hull = Box("Tank Hull", tankPos + Vector3.up * 0.55f, new Vector3(3.2f, 1.1f, 5.6f), burntCarMat);
                                    hull.transform.rotation = Quaternion.LookRotation(direction + right * ((float)random.NextDouble() * 0.4f - 0.2f));
                                    var turret = Box("Tank Turret", tankPos + Vector3.up * 1.35f, new Vector3(2.4f, 0.7f, 2.6f), burntCarMat);
                                    turret.transform.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 60f - 30f, 0);
                                    var barrel = Box("Tank Barrel Stub", tankPos + Vector3.up * 1.4f + direction * 2.0f, new Vector3(0.22f, 0.22f, 1.4f), metal);
                                    barrel.transform.rotation = turret.transform.rotation;
                                    Box("Left Track",  tankPos + Vector3.right * -1.7f + Vector3.up * 0.3f, new Vector3(0.6f, 0.55f, 5.8f), metal);
                                    Box("Right Track", tankPos + Vector3.right *  1.7f + Vector3.up * 0.3f, new Vector3(0.6f, 0.55f, 5.8f), metal);
                                }
                                propBudget -= 5;
                            }
                            else if (r < 0.91)
                            {
                                // Military Comms Antenna / Radio Mast
                                Box("Antenna Mast",    propPos + Vector3.up * 5.5f, new Vector3(0.14f, 11f,  0.14f), metal);
                                Box("Antenna Cross 1", propPos + Vector3.up * 9.0f, new Vector3(3.5f,  0.1f, 0.1f),  metal);
                                Box("Antenna Cross 2", propPos + Vector3.up * 7.5f, new Vector3(2.8f,  0.1f, 0.1f),  metal);
                                Box("Antenna Cross 3", propPos + Vector3.up * 6.0f, new Vector3(2.2f,  0.1f, 0.1f),  metal);
                                // Red warning light cube at top
                                Box("Antenna Light",   propPos + Vector3.up *11.2f, new Vector3(0.3f,  0.3f, 0.3f),  Styled(new Color(1f,.1f,.05f), false));
                                propBudget -= 3;
                            }
                            else if (r < 0.94)
                            {
                                // Street Market Stall Row (civilian flavour)
                                float stallW = 2.2f;
                                for (int s = 0; s < 2; s++)
                                {
                                    Vector3 stallPos = propPos + direction * (s * (stallW + 0.4f));
                                    // Frame poles
                                    foreach (var pOff in new[] { new Vector3(-0.95f, 0, -0.8f), new Vector3(0.95f, 0, -0.8f), new Vector3(-0.95f, 0, 0.8f), new Vector3(0.95f, 0, 0.8f) })
                                        StreetBox("Stall Pole", stallPos + pOff + Vector3.up * 1.1f, new Vector3(0.08f, 2.2f, 0.08f), wood);
                                    // Canopy
                                    var canopy = StreetBox("Stall Canopy", stallPos + Vector3.up * 2.3f, new Vector3(stallW + 0.4f, 0.15f, 1.8f),
                                        containerColors[s % containerColors.Length]);
                                    canopy.transform.rotation = Quaternion.LookRotation(direction);
                                    // Counter
                                    StreetBox("Stall Counter", stallPos + Vector3.up * 0.55f, new Vector3(stallW, 0.1f, 0.55f), wood);
                                }
                                propBudget -= 2;
                            }
                            else if (r < 0.97)
                            {
                                // Electrical Transformer Utility Cabinet
                                Vector3 cabPos = propPos + right * side * 0.4f;
                                var utilityBox = Box("Electrical Transformer Cabinet", cabPos + Vector3.up * 0.75f, new Vector3(1.1f, 1.5f, 0.9f), militaryGreen);
                                utilityBox.transform.rotation = Quaternion.LookRotation(direction);
                                var plinth = Box("Transformer Concrete Plinth", cabPos + Vector3.up * 0.08f, new Vector3(1.3f, 0.16f, 1.1f), concreteMat);
                                plinth.transform.rotation = Quaternion.LookRotation(direction);
                                var warningSign = StreetBox("High Voltage Placard", cabPos + Vector3.up * 1.1f + direction * 0.46f, new Vector3(0.24f, 0.24f, 0.02f), hazardYellow);
                                warningSign.transform.rotation = Quaternion.LookRotation(direction);
                                propBudget -= 2;
                            }
                            else
                            {
                                // Concrete Jersey Barrier & Traffic Hazard Cones
                                Vector3 jPos = propPos + right * side * 0.2f;
                                RoadBarrier(jPos, direction, true, concreteMat);
                                for (int c = -1; c <= 1; c += 2)
                                {
                                    Vector3 conePos = jPos + direction * (c * 1.6f);
                                    var coneBase = StreetBox("Traffic Cone Base", conePos + Vector3.up * 0.02f, new Vector3(0.35f, 0.04f, 0.35f), hazardYellow);
                                    var conePillar = StreetBox("Traffic Cone Pillar", conePos + Vector3.up * 0.35f, new Vector3(0.18f, 0.65f, 0.18f), hazardYellow);
                                    coneBase.transform.rotation = Quaternion.LookRotation(direction);
                                    conePillar.transform.rotation = Quaternion.LookRotation(direction);
                                }
                                propBudget -= 2;
                            }
                        }
                    }
                }
            }
        }
        
        GameObject Box(string label, Vector3 position, Vector3 scale, Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=label;
            if(obj.GetComponent<BoxCollider>()==null) obj.AddComponent<BoxCollider>();
            obj.transform.SetParent(transform,false); obj.transform.localPosition=position; obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material; return obj;
        }
        GameObject StreetBox(string label, Vector3 position, Vector3 scale, Material material)
        {
            var obj=Box(label,position,scale,material);
            // Roadside furniture provides visual scale but must not trap the player
            // or invalidate exact geographic spawns.
            obj.GetComponent<Collider>().enabled=false;
            return obj;
        }
        static Vector3 To3(Vector2 p,float y) => new Vector3(p.x,y,p.y);
        static float Cross(Vector2 a, Vector2 b) => a.x*b.y-a.y*b.x;
        public static List<int> Triangulate(List<Vector2> p)
        {
            var indices=new List<int>(); var output=new List<int>(); float area=0;
            for(int i=0;i<p.Count;i++) { indices.Add(i); area+=Cross(p[i],p[(i+1)%p.Count]); }
            bool removed=true;
            while(removed && indices.Count>3)
            {
                removed=false;
                for(int i=0;i<indices.Count;i++)
                {
                    Vector2 a=p[indices[(i+indices.Count-1)%indices.Count]], b=p[indices[i]], c=p[indices[(i+1)%indices.Count]];
                    if((b-a).sqrMagnitude<.0001f || (c-b).sqrMagnitude<.0001f || (Mathf.Abs(Cross(b-a,c-b))<.0001f && Vector2.Dot(b-a,c-b)>=0))
                    { indices.RemoveAt(i); removed=true; break; }
                }
            }
            if(area<0) indices.Reverse();
            int guard=p.Count*p.Count;
            while(indices.Count>2 && guard-->0)
            {
                bool found=false;
                for(int i=0;i<indices.Count;i++)
                {
                    int a=indices[(i+indices.Count-1)%indices.Count], b=indices[i], c=indices[(i+1)%indices.Count];
                    if(Cross(p[b]-p[a],p[c]-p[b])<=.001f) continue;
                    bool contains=false;
                    foreach(int k in indices)
                        if(k!=a && k!=b && k!=c && Cross(p[b]-p[a],p[k]-p[a])>=0 && Cross(p[c]-p[b],p[k]-p[b])>=0 && Cross(p[a]-p[c],p[k]-p[c])>=0) { contains=true; break; }
                    if(contains) continue;
                    // Reverse planar winding because Unity's horizontal plane uses X/Z.
                    output.AddRange(new [] {a,c,b}); indices.RemoveAt(i); found=true; break;
                }
                if(!found)
                {
                    // Fallback fan triangulation for complex/concave footprints so 3D buildings always generate
                    for(int k = 1; k < indices.Count - 1; k++)
                        output.AddRange(new[] { indices[0], indices[k + 1], indices[k] });
                    break;
                }
            }
            return output;
        }
        static Texture2D grassGroundTex;
        static Texture2D asphaltRoadTex;
        static Texture2D concreteSidewalkTex;
        static Texture2D roofTarTex;
        static Texture2D archFacadeTex;
        static Texture2D grassNormalTex;
        static Texture2D asphaltNormalTex;
        static Texture2D concreteNormalTex;
        static Texture2D facadeNormalTex;
        static Texture2D roofNormalTex;
        static Texture2D mudRoadTex;
        static Texture2D mudRoadNormalTex;
        static Texture2D gravelRoadTex;
        static Texture2D gravelRoadNormalTex;
        static Texture2D cobbleRoadTex;
        static Texture2D cobbleRoadNormalTex;

        public static Texture2D CreateNormalMap(int width, int height, System.Func<int, int, float> heightFunc, float strength)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            float[] h = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                    h[row + x] = heightFunc(x, y);
            }
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                int yPrev = ((y - 1 + height) % height) * width;
                int yNext = ((y + 1) % height) * width;
                int yCurr = y * width;
                for (int x = 0; x < width; x++)
                {
                    int xPrev = (x - 1 + width) % width;
                    int xNext = (x + 1) % width;

                    float hL = h[yCurr + xPrev];
                    float hR = h[yCurr + xNext];
                    float hD = h[yPrev + x];
                    float hU = h[yNext + x];

                    float dx = (hR - hL) * strength;
                    float dy = (hU - hD) * strength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;

                    pixels[yCurr + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false);
            return tex;
        }

        public static Texture2D BuildGrassNormalTexture()
        {
            if (grassNormalTex != null) return grassNormalTex;
            grassNormalTex = CreateNormalMap(128, 128, (x, y) => {
                float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
                float fine = ((x * 19 + y * 43) % 11) / 11f;
                return n * 0.9f + fine * 0.1f;
            }, 0.85f);
            return grassNormalTex;
        }

        static float AsphaltGrain(int x,int y)
        {
            unchecked {uint value=(uint)(x&255)*374761393u+(uint)(y&255)*668265263u;
                value=(value^(value>>13))*1274126177u;value^=value>>16;return (value&65535)/65535f;}
        }
        public static Texture2D BuildAsphaltNormalTexture()
        {
            if (asphaltNormalTex != null) return asphaltNormalTex;
            asphaltNormalTex = CreateNormalMap(128, 128, (x, y) => {
                float grain = AsphaltGrain(x,y);
                float noise = Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
                return grain * 0.8f + noise * 0.2f;
            }, .35f);
            return asphaltNormalTex;
        }

        public static Texture2D BuildConcreteNormalTexture()
        {
            if (concreteNormalTex != null) return concreteNormalTex;
            concreteNormalTex = CreateNormalMap(128, 128, (x, y) => {
                float grain = ((x * 17 + y * 31) % 13) / 13f;
                bool isJoint = (x < 4 || x > 124 || y < 4 || y > 124 || (x % 32 < 2) || (y % 32 < 2));
                return isJoint ? -1.8f : grain * 0.4f;
            }, 3.2f);
            return concreteNormalTex;
        }

        public static Texture2D BuildFacadeNormalTexture()
        {
            if (facadeNormalTex != null) return facadeNormalTex;
            facadeNormalTex = CreateNormalMap(256, 256, (x, y) => {
                int wx = x % 128;
                int wy = y % 128;

                // Base plinth band at ground level
                if (y < 8) return 0.8f;

                // Storey floor divider cornice molding
                if (wy < 3 || wy > 125) return 1.2f;
                if (wy >= 3 && wy <= 5) return -0.5f;

                // Architectural window sill ledge
                bool isSill = (wx >= 18 && wx <= 110 && wy >= 13 && wy < 17);
                if (isSill) return 1.8f;

                // Window cavity and frame
                bool isWindow = (wx >= 20 && wx <= 108 && wy >= 17 && wy <= 111);
                if (isWindow)
                {
                    bool isFrame = (wx < 24 || wx > 104 || wy < 21 || wy > 107 || (wx >= 62 && wx <= 66) || (wy >= 62 && wy <= 65));
                    if (isFrame) return 0.45f;
                    return -1.2f; // Window cavity
                }

                // Concrete formwork panel joints and tie-rod holes
                bool isPanelJoint = (wx % 64 < 2) || (wy % 64 < 2);
                if (isPanelJoint) return -0.6f;
                int px = wx % 64, py = wy % 64;
                if ((px == 8 || px == 56) && (py == 8 || py == 56)) return -1.0f;

                return Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.08f;
            }, 1.5f);
            return facadeNormalTex;
        }

        static readonly Dictionary<string, Texture2D> wallNormalCache = new Dictionary<string, Texture2D>();
        public static Texture2D BuildWallNormalTexture(string style)
        {
            style = (style ?? "").ToLowerInvariant();
            if (wallNormalCache.TryGetValue(style, out var found)) return found;

            Texture2D tex;
            if (style.Contains("brick"))
            {
                tex = CreateNormalMap(128, 128, (x, y) => {
                    int row = y / 8;
                    bool isMortar = (y % 8 < 2) || ((x + (row % 2) * 8) % 16 < 2);
                    return isMortar ? -1.0f : Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.12f;
                }, 1.5f);
            }
            else if (style.Contains("stone"))
            {
                tex = CreateNormalMap(128, 128, (x, y) => {
                    int row = y / 16;
                    bool isJoint = (y % 16 < 2) || ((x + (row % 2) * 16) % 32 < 2);
                    return isJoint ? -1.1f : Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.15f;
                }, 1.5f);
            }
            else if (style.Contains("glass"))
            {
                tex = CreateNormalMap(128, 128, (x, y) => {
                    bool isMullion = (x % 32 < 2) || (y % 32 < 2);
                    return isMullion ? 0.9f : 0f;
                }, 1.1f);
            }
            else if (style.Contains("metal"))
            {
                tex = CreateNormalMap(128, 128, (x, y) => {
                    bool isPanel = (x % 64 < 2) || (y % 64 < 2);
                    return isPanel ? -1.1f : 0f;
                }, 1.3f);
            }
            else
            {
                tex = CreateNormalMap(128, 128, (x, y) => {
                    bool isJoint = (y % 64 < 2) || (y < 4);
                    bool isSeam = (x % 64 < 2);
                    if (isJoint || isSeam) return -0.7f;
                    int px = x % 64, py = y % 64;
                    if ((px == 10 || px == 54) && (py == 10 || py == 54)) return -1.0f;
                    return Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.06f;
                }, 1.2f);
            }
            wallNormalCache[style] = tex;
            return tex;
        }

        public static Texture2D BuildRoofNormalTexture()
        {
            if (roofNormalTex != null) return roofNormalTex;
            roofNormalTex = CreateNormalMap(128, 128, (x, y) => {
                float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                float seam = (y % 32 < 2) ? -1.2f : 0f;
                return n * 0.5f + seam;
            }, 2.5f);
            return roofNormalTex;
        }

        public static Texture2D BuildMudRoadTexture()
        {
            if (mudRoadTex != null) return mudRoadTex;
            mudRoadTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            mudRoadTex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float distRut1 = Mathf.Abs(x - 70);
                float distRut2 = Mathf.Abs(x - 186);
                float inRut = Mathf.Clamp01(1f - Mathf.Min(distRut1, distRut2) / 28f);

                float broadNoise = Mathf.PerlinNoise(x * 0.025f + 5.3f, y * 0.025f + 14.1f);
                float puddle = Mathf.PerlinNoise(x * 0.04f + 32f, y * 0.04f + 77f);
                bool isPuddle = inRut > 0.5f && puddle > 0.62f;

                Color dryEarth = new Color(0.38f, 0.31f, 0.22f);
                Color dampMud = new Color(0.24f, 0.19f, 0.14f);
                Color puddleWater = new Color(0.14f, 0.13f, 0.11f);

                Color c = Color.Lerp(dryEarth, dampMud, inRut * 0.65f + broadNoise * 0.35f);
                if (isPuddle) c = puddleWater;

                float grit = ((x * 13 + y * 23) % 17) / 450f;
                c += new Color(grit, grit * 0.8f, grit * 0.6f, 0f);
                pixels[y * 256 + x] = c;
            }
            mudRoadTex.SetPixels(pixels); mudRoadTex.Apply(false); return mudRoadTex;
        }

        public static Texture2D BuildMudRoadNormalTexture()
        {
            if (mudRoadNormalTex != null) return mudRoadNormalTex;
            mudRoadNormalTex = CreateNormalMap(128, 128, (x, y) => {
                float distRut1 = Mathf.Abs(x - 35);
                float distRut2 = Mathf.Abs(x - 93);
                float rutDepression = Mathf.Clamp01(1f - Mathf.Min(distRut1, distRut2) / 14f) * -0.9f;
                float mudBump = Mathf.PerlinNoise(x * 0.14f, y * 0.14f) * 0.6f;
                return rutDepression + mudBump;
            }, 2.4f);
            return mudRoadNormalTex;
        }

        public static Texture2D BuildGravelRoadTexture()
        {
            if (gravelRoadTex != null) return gravelRoadTex;
            gravelRoadTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            gravelRoadTex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float baseSoil = Mathf.PerlinNoise(x * 0.035f + 11.2f, y * 0.035f + 29.8f);
                Color c = Color.Lerp(new Color(0.44f, 0.41f, 0.36f), new Color(0.56f, 0.52f, 0.45f), baseSoil);

                int hash = (x * 37 + y * 59) % 31;
                if (hash == 0) c += new Color(0.16f, 0.16f, 0.15f, 0f);
                else if (hash == 1) c -= new Color(0.12f, 0.12f, 0.11f, 0f);

                float fine = ((x * 19 + y * 31) % 13) / 380f;
                c += new Color(fine, fine, fine, 0f);
                pixels[y * 256 + x] = c;
            }
            gravelRoadTex.SetPixels(pixels); gravelRoadTex.Apply(false); return gravelRoadTex;
        }

        public static Texture2D BuildGravelNormalTexture()
        {
            if (gravelRoadNormalTex != null) return gravelRoadNormalTex;
            gravelRoadNormalTex = CreateNormalMap(128, 128, (x, y) => {
                float pebble = ((x * 29 + y * 47) % 19 == 0) ? 0.7f : 0f;
                float noise = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.4f;
                return pebble + noise;
            }, 2.2f);
            return gravelRoadNormalTex;
        }

        public static Texture2D BuildCobbleRoadTexture()
        {
            if (cobbleRoadTex != null) return cobbleRoadTex;
            cobbleRoadTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            cobbleRoadTex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                int row = y / 24;
                int col = (x + (row % 2) * 16) / 32;
                int localX = (x + (row % 2) * 16) % 32;
                int localY = y % 24;

                bool isJoint = localX < 3 || localY < 3;
                if (isJoint)
                {
                    pixels[y * 256 + x] = new Color(0.19f, 0.18f, 0.17f, 1f);
                }
                else
                {
                    float stoneVar = Mathf.PerlinNoise(col * 2.7f, row * 2.7f);
                    Color stone = Color.Lerp(new Color(0.35f, 0.34f, 0.33f), new Color(0.48f, 0.46f, 0.44f), stoneVar);
                    float grit = ((localX * 11 + localY * 17) % 7) / 280f;
                    pixels[y * 256 + x] = stone + new Color(grit, grit, grit, 0f);
                }
            }
            cobbleRoadTex.SetPixels(pixels); cobbleRoadTex.Apply(false); return cobbleRoadTex;
        }

        public static Texture2D BuildCobbleNormalTexture()
        {
            if (cobbleRoadNormalTex != null) return cobbleRoadNormalTex;
            cobbleRoadNormalTex = CreateNormalMap(128, 128, (x, y) => {
                int row = y / 12;
                int localX = (x + (row % 2) * 8) % 16;
                int localY = y % 12;
                bool isJoint = localX < 2 || localY < 2;
                if (isJoint) return -1.5f;
                float cx = (localX - 8f) / 7f;
                float cy = (localY - 6f) / 5f;
                return Mathf.Max(0f, 1f - (cx * cx + cy * cy));
            }, 3.0f);
            return cobbleRoadNormalTex;
        }

        public static Texture2D BuildGrassGroundTexture()
        {
            if (grassGroundTex != null) return grassGroundTex;
            grassGroundTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            grassGroundTex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float vegetation=Mathf.PerlinNoise(x*.055f+7.1f,y*.055f+19.4f);
                float patch=Mathf.PerlinNoise(x*.016f+42.1f,y*.016f+88.4f);
                float fine=Mathf.PerlinNoise(x*.31f+12.3f,y*.31f+15.7f);
                Color grass=Color.Lerp(new Color(.30f,.38f,.27f),new Color(.42f,.48f,.31f),vegetation);
                float dry=Mathf.SmoothStep(0,1,Mathf.Clamp01((patch-.54f)*3.8f));
                grass=Color.Lerp(grass,new Color(.49f,.43f,.32f),dry*.65f);
                float grain=(fine-.5f)*.065f+((x*13+y*29)%11)/420f;
                pixels[y*256+x]=grass+new Color(grain,grain,grain*.85f,0);
            }
            grassGroundTex.SetPixels(pixels); grassGroundTex.Apply(false); return grassGroundTex;
        }

        public static Texture2D BuildAsphaltRoadTexture()
        {
            if (asphaltRoadTex != null) return asphaltRoadTex;
            asphaltRoadTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            asphaltRoadTex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float grain=AsphaltGrain(x,y)*.035f;
                float micro=AsphaltGrain(x+71,y+129)*.012f;
                float noise=Mathf.PerlinNoise(x*.12f,y*.12f)*.02f;
                float speckle=AsphaltGrain(x+29,y+41)>.975f?.025f:0;
                float v=.075f+grain+micro+noise+speckle;
                pixels[y * 256 + x] = new Color(v, v * 1.01f, v * 1.03f, 1f);
            }
            asphaltRoadTex.SetPixels(pixels); asphaltRoadTex.Apply(false); return asphaltRoadTex;
        }

        public static Texture2D BuildConcreteSidewalkTexture()
        {
            if (concreteSidewalkTex != null) return concreteSidewalkTex;
            concreteSidewalkTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            concreteSidewalkTex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float grain = ((x * 17 + y * 31) % 13) / 260f;
                float mottle = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.05f;
                float speckle = ((x * 31 + y * 47) % 23 == 0) ? 0.035f : 0f;
                float v = 0.54f + grain + mottle - speckle;
                Color c = new Color(v * 1.02f, v, v * 0.94f, 1f);

                bool isJoint = (x < 4 || x > 252 || y < 4 || y > 252 || (x % 64 < 2) || (y % 64 < 2));
                bool isBevel = (!isJoint && ((x % 64 == 2) || (y % 64 == 2)));

                if (isJoint)
                    c = new Color(0.24f, 0.23f, 0.22f, 1f); // Concrete tile expansion joint
                else if (isBevel)
                    c += new Color(0.045f, 0.045f, 0.035f, 0f); // Edge bevel highlight

                pixels[y * 256 + x] = c;
            }
            concreteSidewalkTex.SetPixels(pixels); concreteSidewalkTex.Apply(false); return concreteSidewalkTex;
        }

        public static Texture2D BuildRoofTarTexture()
        {
            if (roofTarTex != null) return roofTarTex;
            roofTarTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            roofTarTex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.09f;
                float grain = ((x * 23 + y * 47) % 17) / 320f;
                Color c = new Color(0.32f + noise + grain, 0.34f + noise + grain, 0.35f + noise + grain, 1f);
                if (y % 64 < 2) c = new Color(0.20f, 0.21f, 0.22f, 1f); // Seam
                pixels[y * 256 + x] = c;
            }
            roofTarTex.SetPixels(pixels); roofTarTex.Apply(false); return roofTarTex;
        }

        public static Texture2D BuildArchitecturalFacadeTexture()
        {
            if (archFacadeTex != null) return archFacadeTex;
            archFacadeTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            archFacadeTex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float grain = ((x * 19 + y * 37) % 17) / 750f;
                Color baseColor = new Color(0.76f + grain, 0.74f + grain, 0.69f + grain, 1f);
                if (y < 12 || (y > 118 && y < 134) || y > 244) baseColor = new Color(0.48f, 0.46f, 0.42f, 1f); // Ledge trims

                bool isWin1 = (x >= 24 && x <= 104 && y >= 24 && y <= 104);
                bool isWin2 = (x >= 152 && x <= 232 && y >= 24 && y <= 104);
                bool isWin3 = (x >= 24 && x <= 104 && y >= 152 && y <= 232);
                bool isWin4 = (x >= 152 && x <= 232 && y >= 152 && y <= 232);

                if (isWin1 || isWin2 || isWin3 || isWin4)
                {
                    int wx = isWin1 || isWin3 ? x - 24 : x - 152;
                    int wy = isWin1 || isWin2 ? y - 24 : y - 152;
                    if (wx < 6 || wx > 74 || wy < 6 || wy > 74 || wx == 39 || wy == 39)
                    {
                        baseColor = new Color(0.14f, 0.15f, 0.16f, 1f); // Crisp dark aluminum frame
                    }
                    else
                    {
                        if (isWin2)
                        {
                            float blind = (wy % 4 < 2) ? 0.04f : 0f;
                            baseColor = new Color(0.38f + blind, 0.30f + blind, 0.18f + blind, 1f); // Warm interior office
                        }
                        else
                        {
                            float normWy = (float)wy / 80f;
                            float spec = Mathf.SmoothStep(0.2f, 0.95f, normWy);
                            Color darkGlass = new Color(0.045f, 0.075f, 0.11f, 1f);
                            Color skyRefl = new Color(0.18f, 0.28f, 0.42f, 1f);
                            baseColor = Color.Lerp(darkGlass, skyRefl, spec);
                        }
                    }
                }
                else
                {
                    bool isMortar = (y % 16 < 2) || ((x + (y / 16) * 16) % 32 < 2);
                    if (isMortar) baseColor = Color.Lerp(baseColor, new Color(0.42f, 0.40f, 0.38f, 1f), 0.35f);
                }
                pixels[y * 256 + x] = baseColor;
            }
            archFacadeTex.SetPixels(pixels); archFacadeTex.Apply(false); return archFacadeTex;
        }

        void OnDestroy()
        {
            LoadedWorlds.Remove(this);
            LoadedWorlds.RemoveAll(w => w == null);
            Ladders.RemoveAll(route => route == null || route.Owner == this || route.Owner == null);
            foreach (var m in materials) if (m != null) Destroy(m);
            materials.Clear();
            foreach (var m in meshes) if (m != null) Destroy(m);
            meshes.Clear();
            if (facade != null) { Destroy(facade); facade = null; }
            styledCache.Clear();
            roofCache.Clear();
            facadeMaterialCache.Clear();
            roadRenderBatches.Clear();
            Buildings.Clear();
            Features.Clear();
            SpawnPoints.Clear();
            RooftopSpawns.Clear();
            RoadPaths.Clear(); RoadPathFeatures.Clear();
            foreach(var texture in waterShoreTextures)if(texture!=null)Destroy(texture);
            waterShoreTextures.Clear();
        }
    }
}
