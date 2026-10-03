using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        sealed class WaterArea { public MapFeature feature;public List<List<Vector2>> rings;public float level;public Rect bounds; }
        sealed class NamedWaterLevel { public string name;public Rect worldBounds;public float level; }
        static readonly Dictionary<string,float> sharedWaterLevels=new Dictionary<string,float>();
        static readonly List<NamedWaterLevel> namedWaterLevels=new List<NamedWaterLevel>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetWaterLevels() { sharedWaterLevels.Clear();namedWaterLevels.Clear(); }
        Material mappedWaterMaterial,waterFoamMaterial;
        readonly List<WaterArea> waterAreas=new List<WaterArea>();
        bool IsWaterAt(float x,float z)
        {
            foreach(var area in waterAreas)if(area.bounds.Contains(new Vector2(x,z))&&WaterGeometry.Contains(area.rings,new Vector2(x,z)))return true;
            return false;
        }
        public bool CoversWater(Vector3 worldPoint)
        {
            var local=transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.x)<=320f && Mathf.Abs(local.z)<=320f && IsWaterAt(local.x,local.z);
        }
        public static bool WaterAt(Vector3 worldPoint)
        {
            foreach(var world in LoadedWorlds) if(world!=null && world.CoversWater(worldPoint))return true;
            return false;
        }
        public static bool DryFootprint(Vector3 worldPoint,float radius)
        {
            if(WaterAt(worldPoint))return false;
            if(radius<=0f)return true;
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI/6f;
                if(WaterAt(worldPoint+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius)))return false;
            }
            return true;
        }
        void PrepareWater(List<MapFeature> features)
        {
            waterAreas.Clear();
            foreach(var feature in features)
            {
                if(feature.Kind!="water" || feature.Points.Count<3)continue;
                var rings=feature.WaterRings.Count>0?feature.WaterRings:new List<List<Vector2>>{WaterGeometry.Clip(feature.Points)};
                var heights=new List<float>();float minX=320,minZ=320,maxX=-320,maxZ=-320;
                foreach(var ring in rings)foreach(var point in ring)
                { heights.Add(RawGround(point.x,point.y));minX=Mathf.Min(minX,point.x);maxX=Mathf.Max(maxX,point.x);minZ=Mathf.Min(minZ,point.y);maxZ=Mathf.Max(maxZ,point.y); }
                if(heights.Count<3)continue;
                heights.Sort();
                float level=heights[heights.Count/2]+.08f;
                var worldBounds=Rect.MinMaxRect(transform.position.x+minX,transform.position.z+minZ,
                    transform.position.x+maxX,transform.position.z+maxZ);
                level=ResolveWaterLevel(MapFeatureStyle.Text(feature,"water_id"),feature.Name,worldBounds,level);
                waterAreas.Add(new WaterArea{feature=feature,rings=rings,level=level,bounds=Rect.MinMaxRect(minX,minZ,maxX,maxZ)});
            }
        }
        public static float ResolveWaterLevel(string id,string name,Rect worldBounds,float measured)
        {
            if(!string.IsNullOrEmpty(id))
            {
                if(!id.StartsWith("osm:"))id="overture:"+id;
                if(sharedWaterLevels.TryGetValue(id,out float shared))return shared;
                if(sharedWaterLevels.Count>1000)sharedWaterLevels.Clear();
                sharedWaterLevels[id]=measured;
                return measured;
            }
            // Older OSM caches lack water_id. A matching name only identifies the
            // same water body when the clipped world-space bounds actually touch.
            name=(name??"").Trim().ToLowerInvariant();
            if(name.Length==0)return measured;
            for(int i=0;i<namedWaterLevels.Count;i++)
            {
                var previous=namedWaterLevels[i];
                if(previous.name==name && worldBounds.xMin<=previous.worldBounds.xMax+2f &&
                   worldBounds.xMax>=previous.worldBounds.xMin-2f &&
                   worldBounds.yMin<=previous.worldBounds.yMax+2f &&
                   worldBounds.yMax>=previous.worldBounds.yMin-2f)
                {
                    // Record each clipped tile so the match propagates along a long lake.
                    if(namedWaterLevels.Count>1000)namedWaterLevels.Clear();
                    namedWaterLevels.Add(new NamedWaterLevel{name=name,worldBounds=worldBounds,level=previous.level});
                    return previous.level;
                }
            }
            if(namedWaterLevels.Count>1000)namedWaterLevels.Clear();
            namedWaterLevels.Add(new NamedWaterLevel{name=name,worldBounds=worldBounds,level=measured});
            return measured;
        }
        float ShapedGround(float x,float z,float raw)
        {
            var point=new Vector2(x,z);
            float height=raw;
            const float bankDistance=32f;
            foreach(var area in waterAreas)
            {
                if(x<area.bounds.xMin-bankDistance || x>area.bounds.xMax+bankDistance ||
                   z<area.bounds.yMin-bankDistance || z>area.bounds.yMax+bankDistance)continue;
                bool inside=area.bounds.Contains(point) && WaterGeometry.Contains(area.rings,point);
                float distance=WaterGeometry.DistanceToBoundary(area.rings,point,bankDistance);
                if(!inside && distance>=bankDistance)continue;
                if(inside)
                {
                    // A shallow edge descends to the lake bed without exposing terrain above water.
                    float t=Mathf.Clamp01(distance/12f);t=t*t*(3f-2f*t);
                    float bed=Mathf.Lerp(area.level-.12f,area.level-.55f,t);
                    height=Mathf.Min(height,bed);
                }
                else
                {
                    // Coarse DEM can put the mapped shoreline below a flat lake.
                    // Keep the immediate dry bank above water, then blend to raw terrain.
                    float t=Mathf.Clamp01((distance-3f)/(bankDistance-3f));t=t*t*(3f-2f*t);
                    float bank=Mathf.Lerp(area.level+.12f,raw,t);
                    height=bank;
                }
            }
            return height;
        }
        float ShoreBlend(float x,float z)
        {
            var point=new Vector2(x,z);
            foreach(var area in waterAreas)
            {
                if(x<area.bounds.xMin-14 || x>area.bounds.xMax+14 || z<area.bounds.yMin-14 || z>area.bounds.yMax+14)continue;
                float distance=WaterGeometry.DistanceToBoundary(area.rings,point,14f);
                if(distance<14f && !(area.bounds.Contains(point)&&WaterGeometry.Contains(area.rings,point)))
                    return 1f-Mathf.Clamp01(distance/14f);
            }
            return 0f;
        }
        readonly List<Texture2D> waterShoreTextures=new List<Texture2D>();
        void BuildWater(MapFeature feature)
        {
            var area=waterAreas.Find(a=>a.feature==feature);if(area==null)return;
            List<Vector2> triangles;
            try { triangles=WaterGeometry.Triangles(area.rings); }
            catch(System.FormatException e){Debug.LogWarning("Water geometry skipped: "+e.Message);return;}
            if(triangles.Count==0)return;
            var vertices=new List<Vector3>();var indices=new List<int>();var uv=new List<Vector2>();
            foreach(var point in triangles)
            {
                indices.Add(vertices.Count);vertices.Add(new Vector3(point.x,area.level,point.y));
                uv.Add(new Vector2((point.x-area.bounds.xMin)/Mathf.Max(.01f,area.bounds.width),(point.y-area.bounds.yMin)/Mathf.Max(.01f,area.bounds.height)));
            }
            var mesh=new Mesh{name="Mapped water with island cutouts"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var shoreTexture=new Texture2D(64,64,TextureFormat.RGB24,false){name="Shore distance",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[64*64];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                Vector2 point=new Vector2(area.bounds.xMin+area.bounds.width*(x+.5f)/64f,area.bounds.yMin+area.bounds.height*(y+.5f)/64f);
                float distance=100f;
                foreach(var ring in area.rings)for(int edge=0;edge<ring.Count;edge++)
                {
                    Vector2 a=ring[edge],b=ring[(edge+1)%ring.Count];
                    if((Mathf.Abs(a.x)>319.9f && Mathf.Abs(b.x)>319.9f && a.x*b.x>0) ||
                       (Mathf.Abs(a.y)>319.9f && Mathf.Abs(b.y)>319.9f && a.y*b.y>0))continue;
                    Vector2 ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(point-a,ab)/Mathf.Max(.0001f,ab.sqrMagnitude));
                    distance=Mathf.Min(distance,Vector2.Distance(point,a+ab*t));
                }
                float shade=Mathf.SmoothStep(0,1,distance/18f);pixels[y*64+x]=new Color(shade,shade,shade,1);
            }
            shoreTexture.SetPixels(pixels);shoreTexture.Apply(false,true);waterShoreTextures.Add(shoreTexture);
            var shader=Resources.Load<Shader>("Shaders/MappedWater");
            if(shader==null){Debug.LogWarning("Mapped water shader missing");return;}
            if(mappedWaterMaterial==null){mappedWaterMaterial=new Material(shader);materials.Add(mappedWaterMaterial);}
            var obj=new GameObject("water");obj.transform.SetParent(transform,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=mappedWaterMaterial;
            var properties=new MaterialPropertyBlock();properties.SetTexture("_ShoreDepth",shoreTexture);renderer.SetPropertyBlock(properties);
            obj.AddComponent<MeshCollider>().sharedMesh=mesh;
            BuildFoam(area);
        }
        void BuildFoam(WaterArea area)
        {
            var shader=Resources.Load<Shader>("Shaders/WaterFoam");
            if(shader==null || !shader.isSupported)return;
            var vertices=new List<Vector3>();var colors=new List<Color32>();var indices=new List<int>();
            foreach(var ring in area.rings)for(int i=0;i<ring.Count;i++)
            {
                Vector2 a=ring[i],b=ring[(i+1)%ring.Count];var delta=b-a;
                if(delta.sqrMagnitude<1f)continue;
                // Clipped sector edges and adjoining mapped water are not shorelines.
                if((Mathf.Abs(a.x)>=319.99f && Mathf.Abs(b.x)>=319.99f) ||
                    (Mathf.Abs(a.y)>=319.99f && Mathf.Abs(b.y)>=319.99f))continue;
                var normal=new Vector2(-delta.y,delta.x).normalized;
                var mid=(a+b)*.5f;
                bool left=IsWaterAt(mid.x+normal.x*.35f,mid.y+normal.y*.35f);
                bool right=IsWaterAt(mid.x-normal.x*.35f,mid.y-normal.y*.35f);
                if(left==right)continue;
                var inward=normal*(left?1f:-1f)*1.7f;
                int start=vertices.Count;float y=area.level+.018f;
                vertices.Add(new Vector3(a.x,y,a.y));vertices.Add(new Vector3(b.x,y,b.y));
                vertices.Add(new Vector3(b.x+inward.x,y,b.y+inward.y));
                vertices.Add(new Vector3(a.x+inward.x,y,a.y+inward.y));
                colors.Add(new Color(1,1,1,.60f));colors.Add(new Color(1,1,1,.60f));
                colors.Add(new Color(1,1,1,0));colors.Add(new Color(1,1,1,0));
                indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            if(vertices.Count==0)return;
            var mesh=new Mesh{name="Shore foam"};mesh.SetVertices(vertices);mesh.SetColors(colors);
            mesh.SetTriangles(indices,0);mesh.RecalculateBounds();meshes.Add(mesh);
            if(waterFoamMaterial==null){waterFoamMaterial=new Material(shader);materials.Add(waterFoamMaterial);}
            var obj=new GameObject("Shore foam");obj.transform.SetParent(transform,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=waterFoamMaterial;
        }
    }
}
