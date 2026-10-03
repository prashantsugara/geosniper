using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeoSniper
{
    // Vegetation follows the sector elevation without adding collision obstacles.
    public sealed class SectorScenery : MonoBehaviour
    {
        public enum PlantingKind { Open, Meadow, Scrub, Park, Woodland, Sports }
        enum TreeForm { Rounded, Spreading, Columnar }
        sealed class TreeCell
        {
            public readonly List<Vector3> trunk=new List<Vector3>(), canopy=new List<Vector3>(), light=new List<Vector3>();
            public readonly List<int> trunkIndices=new List<int>(), canopyIndices=new List<int>(), lightIndices=new List<int>();
            public readonly List<Vector2> trunkUVs=new List<Vector2>(), canopyUVs=new List<Vector2>(), lightUVs=new List<Vector2>();
            public readonly List<Vector3> canopyNormals=new List<Vector3>(), lightNormals=new List<Vector3>();
            public readonly List<Vector3> distantTrunk=new List<Vector3>(), distantCanopy=new List<Vector3>();
            public readonly List<int> distantTrunkIndices=new List<int>(), distantCanopyIndices=new List<int>();
            public readonly List<Vector2> distantTrunkUVs=new List<Vector2>(), distantCanopyUVs=new List<Vector2>();
            public readonly List<Vector3> distantCanopyNormals=new List<Vector3>();
        }
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Material> materials=new List<Material>();

        static Texture2D barkTex;
        static Texture2D barkNormalTex;
        static Texture2D foliageTex;
        static Texture2D foliageNormalTex;

        public static Texture2D BuildBarkTexture()
        {
            if (barkTex != null) return barkTex;
            barkTex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            barkTex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float verticalGrain = Mathf.PerlinNoise(x * 0.22f, y * 0.035f);
                float furrows = Mathf.Abs(Mathf.Sin(x * 0.35f + Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 3f));
                float micro = ((x * 19 + y * 43) % 11) / 130f;
                float v = Mathf.Lerp(0.20f, 0.36f, verticalGrain) - furrows * 0.08f + micro;
                pixels[y * 128 + x] = new Color(v * 1.05f, v * 0.82f, v * 0.58f, 1f);
            }
            barkTex.SetPixels(pixels); barkTex.Apply(false); return barkTex;
        }

        public static Texture2D BuildBarkNormalTexture()
        {
            if (barkNormalTex != null) return barkNormalTex;
            barkNormalTex = SectorWorld.CreateNormalMap(128, 128, (x, y) => {
                float n = Mathf.PerlinNoise(x * 0.25f, y * 0.04f);
                float furrow = -Mathf.Abs(Mathf.Sin(x * 0.35f)) * 0.7f;
                return n * 0.5f + furrow;
            }, 2.8f);
            return barkNormalTex;
        }

        public static Texture2D BuildFoliageTexture()
        {
            if (foliageTex != null) return foliageTex;
            foliageTex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            foliageTex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float leafClumps = Mathf.PerlinNoise(x * 0.09f + 12.3f, y * 0.09f + 45.6f);
                float fineDetail = Mathf.PerlinNoise(x * 0.26f + 78.9f, y * 0.26f + 21.4f) * 0.16f;
                float depth = Mathf.PerlinNoise(x * 0.035f + 14.2f, y * 0.035f + 33.1f);
                Color deep = new Color(0.18f, 0.28f, 0.14f);
                Color bright = new Color(0.34f, 0.45f, 0.22f);
                Color c = Color.Lerp(deep, bright, leafClumps + fineDetail);
                c *= Mathf.Lerp(0.82f, 1.14f, depth);
                pixels[y * 128 + x] = c;
            }
            foliageTex.SetPixels(pixels); foliageTex.Apply(false); return foliageTex;
        }

        public static Texture2D BuildFoliageNormalTexture()
        {
            if (foliageNormalTex != null) return foliageNormalTex;
            foliageNormalTex = SectorWorld.CreateNormalMap(128, 128, (x, y) => {
                float n1 = Mathf.PerlinNoise(x * 0.12f + 5.7f, y * 0.12f + 9.2f);
                float n2 = Mathf.PerlinNoise(x * 0.28f + 14.1f, y * 0.28f + 28.3f) * 0.35f;
                return n1 * 0.7f + n2;
            }, 1.0f);
            return foliageNormalTex;
        }

        Material Surface(Color color)
        {
            var shader=Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse");
            var material=new Material(shader!=null?shader:Shader.Find("Sprites/Default"));
            material.color=color;
            material.SetFloat("_Glossiness",0);
            materials.Add(material);
            return material;
        }

        public IEnumerator Generate(List<MapFeature> features,SectorElevation elevation=null,Vector2 focus=default)
        {
            var grass=Surface(new Color(.43f,.47f,.31f));
            var shrubs=Surface(new Color(.82f,.91f,.78f));
            shrubs.mainTexture=BuildFoliageTexture();
            var stones=Surface(new Color(.43f,.42f,.37f));
            var reeds=Surface(new Color(.54f,.57f,.35f));
            var sector=GetComponentInParent<SectorWorld>();
            var treeTrunk=Surface(new Color(.85f,.82f,.80f));
            treeTrunk.SetFloat("_Glossiness", 0.12f);
            treeTrunk.SetFloat("_Metallic", 0.0f);
            treeTrunk.mainTexture = BuildBarkTexture();
            treeTrunk.SetTexture("_BumpMap", BuildBarkNormalTexture());
            treeTrunk.EnableKeyword("_NORMALMAP");

            var treeCanopy=Surface(new Color(.88f,.92f,.85f));
            treeCanopy.SetFloat("_Glossiness", 0.08f);
            treeCanopy.SetFloat("_Metallic", 0.0f);
            treeCanopy.mainTexture = BuildFoliageTexture();
            treeCanopy.SetTexture("_BumpMap", BuildFoliageNormalTexture());
            treeCanopy.EnableKeyword("_NORMALMAP");

            var treeCanopyLight=Surface(new Color(1.05f,1.10f,.94f));
            treeCanopyLight.SetFloat("_Glossiness", 0.08f);
            treeCanopyLight.SetFloat("_Metallic", 0.0f);
            treeCanopyLight.mainTexture = BuildFoliageTexture();
            treeCanopyLight.SetTexture("_BumpMap", BuildFoliageNormalTexture());
            treeCanopyLight.EnableKeyword("_NORMALMAP");
            yield return null;

            var vertices=new List<Vector3>();
            var shrubVertices=new List<Vector3>();var shrubIndices=new List<int>();var shrubUVs=new List<Vector2>();
            var stoneVertices=new List<Vector3>();var stoneIndices=new List<int>();
            var reedVertices=new List<Vector3>();var reedIndices=new List<int>();
            var indices=new List<int>();
            var treeCells=new Dictionary<Vector2Int,TreeCell>();
            var random=new System.Random(871);
            var bounds=new List<Rect>();
            foreach(var feature in features)
            {
                Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
                foreach(var p in feature.Points) { min=Vector2.Min(min,p); max=Vector2.Max(max,p); }
                float padding=feature.Kind=="road"?Mathf.Max(8,MapFeatureStyle.RoadWidth(feature)*.5f+4):8;
                bounds.Add(Rect.MinMaxRect(min.x-padding,min.y-padding,max.x+padding,max.y+padding));
            }
            // Grass samples used to scan every map feature, thousands of times per
            // sector. Index padded bounds once so each sample checks only its cell.
            const float cellSize=32f;
            var spatial=new Dictionary<Vector2Int,List<int>>();
            for(int i=0;i<bounds.Count;i++)
            {
                if(i>0 && i%64==0) yield return null;
                var area=bounds[i];
                int minX=Mathf.Max(-10,Mathf.FloorToInt(area.xMin/cellSize));
                int maxX=Mathf.Min(10,Mathf.FloorToInt(area.xMax/cellSize));
                int minY=Mathf.Max(-10,Mathf.FloorToInt(area.yMin/cellSize));
                int maxY=Mathf.Min(10,Mathf.FloorToInt(area.yMax/cellSize));
                for(int cy=minY;cy<=maxY;cy++) for(int cx=minX;cx<=maxX;cx++)
                {
                    var cell=new Vector2Int(cx,cy);
                    if(!spatial.TryGetValue(cell,out var members)) spatial[cell]=members=new List<int>();
                    members.Add(i);
                }
            }
            var nearby=new List<MapFeature>();
            int clumps=0, trees=0;
            var planted=new List<Vector2>();
            int treeBudget=Application.isMobilePlatform?
                (MobileGraphics.Selected==MobileGraphicsPreset.Performance?120:MobileGraphics.Selected==MobileGraphicsPreset.High?260:190):450;
            bool mappedWoodland=features.Exists(f=>f.Kind=="park" && LandUse(f)==PlantingKind.Woodland);
            int grassStep=Application.isMobilePlatform?
                (MobileGraphics.Selected==MobileGraphicsPreset.Performance?12:MobileGraphics.Selected==MobileGraphicsPreset.High?6:8):8;
            var candidates=new List<Vector2>();
            // Give nearby streets a readable tree line before filling empty lots.
            foreach(var feature in features)
                if(feature.Kind=="road" && !MapFeatureStyle.Path(feature))
                    for(int i=1;i<feature.Points.Count;i++)
                    {
                        var a=feature.Points[i-1]; var delta=feature.Points[i]-a;
                        var side=new Vector2(-delta.y,delta.x).normalized*(MapFeatureStyle.RoadWidth(feature)*.5f+4);
                        for(float distance=12;distance<delta.magnitude;distance+=26)
                            foreach(int sign in new[]{-1,1}) candidates.Add(a+delta.normalized*distance+side*sign);
                    }
            candidates.Sort((a,b)=>(a-focus).sqrMagnitude.CompareTo((b-focus).sqrMagnitude));
            float sliceStart=Time.realtimeSinceStartup;
            foreach(var p in candidates)
            {
                if(Time.realtimeSinceStartup-sliceStart>.003f) { yield return null; sliceStart=Time.realtimeSinceStartup; }
                if(trees>=treeBudget*(mappedWoodland?2:3)/4) break;
                if(Mathf.Abs(p.x)>310 || Mathf.Abs(p.y)>310 || planted.Exists(other=>(other-p).sqrMagnitude<100)) continue;
                CollectNearby(p,features,bounds,spatial,nearby);
                if(!CanPlant(p,nearby,out float y,out var roadsideKind) || roadsideKind==PlantingKind.Sports) continue;
                var root=new Vector3(p.x,y+(sector!=null?sector.Ground(p.x,p.y):elevation?.Sample(transform.position.x+p.x,transform.position.z+p.y)??0),p.y);
                bool lightCanopy=random.NextDouble()<.38;
                AddTreeToCell(treeCells,root,3.1f+(float)random.NextDouble()*1.3f,lightCanopy,roadsideKind);
                planted.Add(p); trees++;
            }
            // Fixed budget and small batches avoid thousands of grass GameObjects on phones.
            for(int z=-312;z<=312;z+=grassStep)
            {
                for(int x=-312;x<=312;x+=grassStep)
                {
                    Vector2 p=new Vector2(x+(float)random.NextDouble()*5,z+(float)random.NextDouble()*5);
                    CollectNearby(p,features,bounds,spatial,nearby);
                    if(!CanPlant(p,nearby,out float y,out var planting) || planting==PlantingKind.Sports) continue;
                    float terrain=sector!=null?sector.Ground(p.x,p.y):elevation?.Sample(transform.position.x+p.x,transform.position.z+p.y)??0;
                    // Batched geometry lets the sector feel inhabited by greenery
                    // without creating a GameObject for each tree.
                    if(trees<treeBudget && random.NextDouble()<TreeProbability(planting) && !planted.Exists(other=>(other-p).sqrMagnitude<100))
                    {
                        Vector3 root=new Vector3(p.x,y+terrain,p.y);
                        bool lightCanopy=random.NextDouble()<.38;
                        float height=planting==PlantingKind.Woodland?5.5f+(float)random.NextDouble()*3f:
                            2.8f+(float)random.NextDouble()*1.8f;
                        AddTreeToCell(treeCells,root,height,lightCanopy,planting);
                        trees++; planted.Add(p);
                    }
                    if(random.NextDouble()<ShrubProbability(planting))
                        AddShrub(new Vector3(p.x,y+terrain,p.y),.45f+(float)random.NextDouble()*.55f,shrubVertices,shrubIndices,shrubUVs);
                    else if(y<.1f && random.NextDouble()<.018f)
                        AddStone(new Vector3(p.x,y+terrain,p.y),.25f+(float)random.NextDouble()*.35f,stoneVertices,stoneIndices);
                    float coverage=(Mathf.Sin(p.x*.035f)+Mathf.Cos(p.y*.041f))*.5f;
                    if(random.NextDouble()>(coverage>.2f?.70f:.25f)) continue;
                    for(int blade=0;blade<4;blade++)
                    {
                        float angle=(float)random.NextDouble()*Mathf.PI*2;
                        Vector3 root=new Vector3(p.x+(float)random.NextDouble()*1.7f,y,p.y+(float)random.NextDouble()*1.7f);
                        root.y+=terrain;
                        float height=.12f+(float)random.NextDouble()*.20f;
                        Vector3 width=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.09f;
                        int start=vertices.Count;
                        vertices.Add(root-width); vertices.Add(root+width);
                        vertices.Add(root+Vector3.up*height+width*.7f);
                        // Separate back vertices keep opposing faces from cancelling normals.
                        vertices.Add(vertices[start+2]); vertices.Add(vertices[start+1]); vertices.Add(vertices[start]);
                        indices.AddRange(new[]{start,start+1,start+2,start+3,start+4,start+5});
                    }
                    clumps++;
                    if(clumps%180==0)
                    {
                        MakeMesh("Grass patch",vertices,indices,grass);
                        if(shrubVertices.Count>0){MakeMesh("Shrub patch",shrubVertices,shrubIndices,shrubs,shrubUVs);shrubVertices.Clear();shrubIndices.Clear();shrubUVs.Clear();}
                        if(stoneVertices.Count>0){MakeMesh("Stone patch",stoneVertices,stoneIndices,stones);stoneVertices.Clear();stoneIndices.Clear();}
                        vertices.Clear(); indices.Clear();
                    }
                }
                if((z+312)%32==0 || Time.realtimeSinceStartup-sliceStart>.0035f) { yield return null; sliceStart=Time.realtimeSinceStartup; }
            }
            int reedBudget=Application.isMobilePlatform?90:180;
            foreach(var feature in features)
            {
                if(reedBudget<=0)break;
                if(feature.Kind!="water")continue;
                var rings=feature.WaterRings.Count>0?feature.WaterRings:new List<List<Vector2>>{feature.Points};
                foreach(var ring in rings)for(int i=0;i<ring.Count && reedBudget>0;i++)
                {
                    var a=ring[i];var b=ring[(i+1)%ring.Count];var delta=b-a;
                    float length=delta.magnitude;if(length<6)continue;
                    var normal=new Vector2(-delta.y,delta.x).normalized;
                    for(float distance=5;distance<length-4 && reedBudget>0;distance+=11)
                    {
                        if(Time.realtimeSinceStartup-sliceStart>.003f){yield return null;sliceStart=Time.realtimeSinceStartup;}
                        var shore=a+delta*(distance/length);
                        bool left=WaterGeometry.Contains(rings,shore+normal*.4f);
                        bool right=WaterGeometry.Contains(rings,shore-normal*.4f);
                        if(left==right)continue;
                        var p=shore+normal*(left?-4.2f:4.2f);
                        if(Mathf.Abs(p.x)>310 || Mathf.Abs(p.y)>310)continue;
                        CollectNearby(p,features,bounds,spatial,nearby);
                        if(!CanPlant(p,nearby,out float y,out var shoreKind) || shoreKind==PlantingKind.Sports)continue;
                        float ground=sector!=null?sector.Ground(p.x,p.y):elevation?.Sample(transform.position.x+p.x,transform.position.z+p.y)??0;
                        AddReedClump(new Vector3(p.x,y+ground,p.y),reedVertices,reedIndices);
                        reedBudget--;
                    }
                }
            }
            if(vertices.Count>0) MakeMesh("Grass patch",vertices,indices,grass);
            if(shrubVertices.Count>0) MakeMesh("Shrub patch",shrubVertices,shrubIndices,shrubs,shrubUVs);
            if(stoneVertices.Count>0) MakeMesh("Stone patch",stoneVertices,stoneIndices,stones);
            if(reedVertices.Count>0) MakeMesh("Reed patch",reedVertices,reedIndices,reeds);
            foreach(var cell in treeCells.Values)
            {
                if(cell.trunk.Count>0)MakeMesh("Local tree trunks",cell.trunk,cell.trunkIndices,treeTrunk,cell.trunkUVs,null,0,180);
                if(cell.canopy.Count>0)MakeMesh("Local tree canopy",cell.canopy,cell.canopyIndices,treeCanopy,cell.canopyUVs,cell.canopyNormals,0,180);
                if(cell.light.Count>0)MakeMesh("Light tree canopy",cell.light,cell.lightIndices,treeCanopyLight,cell.lightUVs,cell.lightNormals,0,180);
                if(cell.distantTrunk.Count>0)MakeMesh("Distant tree trunks",cell.distantTrunk,cell.distantTrunkIndices,treeTrunk,cell.distantTrunkUVs,null,160,1400);
                if(cell.distantCanopy.Count>0)MakeMesh("Distant tree canopy",cell.distantCanopy,cell.distantCanopyIndices,treeCanopy,cell.distantCanopyUVs,cell.distantCanopyNormals,160,1400);
            }
        }

        static void AddReedClump(Vector3 root,List<Vector3> vertices,List<int> indices)
        {
            for(int blade=0;blade<5;blade++)
            {
                float angle=blade*1.2566f;
                var offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.22f;
                var side=new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle))*.045f;
                var basePoint=root+offset;int start=vertices.Count;
                vertices.Add(basePoint-side);vertices.Add(basePoint+side);
                vertices.Add(basePoint+Vector3.up*(.65f+blade*.09f)+offset*.35f);
                vertices.Add(vertices[start+2]);vertices.Add(vertices[start+1]);vertices.Add(vertices[start]);
                for(int k=0;k<6;k++)indices.Add(start+k);
            }
        }

        static void AddShrub(Vector3 root,float radius,List<Vector3> vertices,List<int> indices,List<Vector2> uvs)
        {
            int start=vertices.Count;
            const int sides=8;
            for(int ring=0;ring<4;ring++)
            {
                float width=ring==0?.72f:ring==1?1f:ring==2?.56f:.06f;
                float height=ring==0?0:ring==1?.42f:ring==2?1.03f:1.26f;
                for(int side=0;side<=sides;side++)
                {
                    float angle=side*Mathf.PI*2/sides;
                    float variation=1f+Mathf.Sin(angle*3f+root.x*.37f+root.z*.23f)*.08f;
                    vertices.Add(root+new Vector3(Mathf.Cos(angle)*radius*width*variation,
                        radius*height,Mathf.Sin(angle)*radius*width*variation));
                    uvs.Add(new Vector2(side/(float)sides*2f,ring/3f*2f));
                }
            }
            for(int ring=0;ring<3;ring++)for(int side=0;side<sides;side++)
            {
                int a=start+ring*(sides+1)+side,b=a+1,c=a+sides+1,d=c+1;
                indices.Add(a);indices.Add(c);indices.Add(b);
                indices.Add(b);indices.Add(c);indices.Add(d);
            }
        }
        static void AddStone(Vector3 root,float radius,List<Vector3> vertices,List<int> indices)
        {
            for(int n=0;n<8;n++)
            {
                float a=n*Mathf.PI/4f,b=(n+1)*Mathf.PI/4f;int i=vertices.Count;
                vertices.Add(root+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));
                vertices.Add(root+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius));
                vertices.Add(root+Vector3.up*radius*.7f);indices.Add(i);indices.Add(i+1);indices.Add(i+2);
            }
        }
        static void CollectNearby(Vector2 point,List<MapFeature> features,List<Rect> bounds,
            Dictionary<Vector2Int,List<int>> spatial,List<MapFeature> nearby)
        {
            nearby.Clear();
            var cell=new Vector2Int(Mathf.FloorToInt(point.x/32f),Mathf.FloorToInt(point.y/32f));
            if(!spatial.TryGetValue(cell,out var members)) return;
            foreach(int index in members)
                if(bounds[index].Contains(point)) nearby.Add(features[index]);
        }

        static void AddTreeToCell(Dictionary<Vector2Int,TreeCell> cells,Vector3 root,float height,bool light,PlantingKind planting)
        {
            var key=new Vector2Int(Mathf.FloorToInt((root.x+320f)/128f),Mathf.FloorToInt((root.z+320f)/128f));
            if(!cells.TryGetValue(key,out var cell))cells[key]=cell=new TreeCell();
            float formSeed=Mathf.PerlinNoise(root.x*.097f+15f,root.z*.097f+33f);
            TreeForm form=planting==PlantingKind.Woodland && formSeed<.27f?TreeForm.Columnar:
                formSeed>.76f?TreeForm.Spreading:TreeForm.Rounded;
            AddTree(root,height,cell.trunk,cell.trunkIndices,cell.trunkUVs,
                light?cell.light:cell.canopy,light?cell.lightIndices:cell.canopyIndices,
                light?cell.lightUVs:cell.canopyUVs,light?cell.lightNormals:cell.canopyNormals,form);
            AddDistantTree(root,height,cell,form);
        }

        static void AddDistantTree(Vector3 root,float height,TreeCell cell,TreeForm form)
        {
            float width=.16f+height*.028f;
            int first=cell.distantTrunk.Count;
            for(int level=0;level<2;level++)for(int side=0;side<=4;side++)
            {
                float angle=side*Mathf.PI*.5f;
                float radius=width*(level==0?1.2f:.55f);
                cell.distantTrunk.Add(root+new Vector3(Mathf.Cos(angle)*radius,height*.78f*level,Mathf.Sin(angle)*radius));
                cell.distantTrunkUVs.Add(new Vector2(side/4f,level));
            }
            for(int side=0;side<4;side++)
            {
                int a=first+side,b=a+1,c=a+5,d=c+1;
                cell.distantTrunkIndices.Add(a);cell.distantTrunkIndices.Add(b);cell.distantTrunkIndices.Add(c);
                cell.distantTrunkIndices.Add(b);cell.distantTrunkIndices.Add(d);cell.distantTrunkIndices.Add(c);
            }
            float shape=Mathf.PerlinNoise(root.x*.173f+81f,root.z*.173f+29f);
            float crownWidth=height*Mathf.Lerp(.38f,.65f,shape);
            float stretch=Mathf.Lerp(1.35f,.80f,shape);
            if(form==TreeForm.Columnar){crownWidth*=.66f;stretch*=1.48f;}
            else if(form==TreeForm.Spreading){crownWidth*=1.18f;stretch*=.76f;}
            var top=root+Vector3.up*height;
            AddCrown(top,new Vector3(crownWidth*.9f,crownWidth*.9f*stretch,crownWidth*.9f),
                cell.distantCanopy,cell.distantCanopyIndices,cell.distantCanopyUVs,cell.distantCanopyNormals,true);
            AddCrown(top+new Vector3(crownWidth*.42f,-height*.14f,0),
                new Vector3(crownWidth*.55f,crownWidth*.53f*stretch,crownWidth*.5f),
                cell.distantCanopy,cell.distantCanopyIndices,cell.distantCanopyUVs,cell.distantCanopyNormals,true);
        }

        static void AddTree(Vector3 root, float height, List<Vector3> trunk, List<int> trunkIndices, List<Vector2> trunkUVs,
            List<Vector3> canopy, List<int> canopyIndices, List<Vector2> canopyUVs, List<Vector3> canopyNormals,TreeForm form)
        {
            float trunkWidth = 0.16f + height * 0.028f;
            // Position-seeded proportions remain stable when a streamed sector is rebuilt.
            float shape=Mathf.PerlinNoise(root.x*.173f+81f,root.z*.173f+29f);
            float canopyWidth = height * Mathf.Lerp(.38f,.65f,shape);
            float crownStretch=Mathf.Lerp(1.35f,.80f,shape);
            if(form==TreeForm.Columnar){canopyWidth*=.66f;crownStretch*=1.48f;}
            else if(form==TreeForm.Spreading){canopyWidth*=1.18f;crownStretch*=.76f;}
            Quaternion crownRotation=Quaternion.Euler(0,Mathf.Repeat(root.x*31.7f+root.z*73.3f,360f),0);
            Vector3 lean=crownRotation*new Vector3(height*.045f,0,0);

            // 8-sided tapered trunk with root flare and 4 vertical levels
            int sides = 8;
            int levels = 4;
            int startTrunk = trunk.Count;

            for (int l = 0; l < levels; l++)
            {
                float t = (float)l / (levels - 1);
                float y = t * (height * 0.82f);
                float r = trunkWidth * Mathf.Lerp(1.4f, 0.6f, Mathf.Pow(t, 0.7f));

                for (int s = 0; s < sides; s++)
                {
                    float angle = (float)s / sides * Mathf.PI * 2f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                    trunk.Add(root + offset + lean*t*t);
                    trunkUVs.Add(new Vector2((float)s / sides, y * 0.5f));
                }
            }

            for (int l = 0; l < levels - 1; l++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int nextS = (s + 1) % sides;
                    int cur = startTrunk + l * sides + s;
                    int curNext = startTrunk + l * sides + nextS;
                    int up = startTrunk + (l + 1) * sides + s;
                    int upNext = startTrunk + (l + 1) * sides + nextS;

                    trunkIndices.Add(cur); trunkIndices.Add(curNext); trunkIndices.Add(up);
                    trunkIndices.Add(curNext); trunkIndices.Add(upNext); trunkIndices.Add(up);
                }
            }

            // Two upper angled branch limbs splitting into canopy
            Vector3 forkPos = root + Vector3.up * (height * 0.78f)+lean;
            for (int b = -1; b <= 1; b += 2)
            {
                Vector3 branchEnd = forkPos + crownRotation*new Vector3(b * (canopyWidth * 0.35f), height * 0.20f, b * 0.12f);
                AddBranchLimb(forkPos, branchEnd, trunkWidth * 0.45f, trunkWidth * 0.2f, trunk, trunkIndices, trunkUVs);
            }

            // Asymmetric clusters vary the silhouette without adding vertices or materials.
            Vector3 top = root + Vector3.up * height+lean;
            AddCrown(top + Vector3.up * .15f, new Vector3(canopyWidth*.68f,canopyWidth*.76f*crownStretch,canopyWidth*.68f), canopy,canopyIndices,canopyUVs,canopyNormals);
            for(int cluster=0;cluster<7;cluster++)
            {
                float phase=cluster*2.39996f+shape*3f;
                float spread=canopyWidth*Mathf.Lerp(.52f,.76f,(cluster%3)/2f);
                Vector3 offset=crownRotation*new Vector3(Mathf.Cos(phase)*spread,
                    height*Mathf.Lerp(-.26f,.16f,cluster/6f),Mathf.Sin(phase)*spread);
                float size=canopyWidth*Mathf.Lerp(.58f,.43f,cluster/6f);
                AddCrown(top+offset,new Vector3(size,size*crownStretch*Mathf.Lerp(.8f,1.15f,(cluster%3)/2f),size*.78f),canopy,canopyIndices,canopyUVs,canopyNormals);
            }
        }

        static void AddBranchLimb(Vector3 start, Vector3 end, float startR, float endR, List<Vector3> trunk, List<int> trunkIndices, List<Vector2> trunkUVs)
        {
            Vector3 dir = (end - start).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            Vector3 up = Vector3.Cross(dir, side).normalized;
            int startIdx = trunk.Count;

            for (int s = 0; s < 6; s++)
            {
                float angle = (float)s / 6f * Mathf.PI * 2f;
                Vector3 radial = (side * Mathf.Cos(angle) + up * Mathf.Sin(angle));
                trunk.Add(start + radial * startR);
                trunkUVs.Add(new Vector2((float)s / 6f, 0f));
            }
            for (int s = 0; s < 6; s++)
            {
                float angle = (float)s / 6f * Mathf.PI * 2f;
                Vector3 radial = (side * Mathf.Cos(angle) + up * Mathf.Sin(angle));
                trunk.Add(end + radial * endR);
                trunkUVs.Add(new Vector2((float)s / 6f, 1f));
            }
            for (int s = 0; s < 6; s++)
            {
                int nextS = (s + 1) % 6;
                int cur = startIdx + s;
                int curNext = startIdx + nextS;
                int upIdx = startIdx + 6 + s;
                int upNext = startIdx + 6 + nextS;

                trunkIndices.Add(cur); trunkIndices.Add(curNext); trunkIndices.Add(upIdx);
                trunkIndices.Add(curNext); trunkIndices.Add(upNext); trunkIndices.Add(upIdx);
            }
        }

        static void AddCrown(Vector3 center, Vector3 radius, List<Vector3> vertices, List<int> indices, List<Vector2> uvs, List<Vector3> normals,bool simplified=false)
        {
            int rings = simplified?3:5;
            int segments = simplified?6:9;
            int startIdx = vertices.Count;

            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                float lat = -Mathf.PI * 0.5f + t * Mathf.PI;
                float sinLat = Mathf.Sin(lat);
                float cosLat = Mathf.Cos(lat);

                for (int s = 0; s <= segments; s++)
                {
                    float u = (float)s / segments;
                    float lon = u * Mathf.PI * 2f;
                    float sinLon = Mathf.Sin(lon);
                    float cosLon = Mathf.Cos(lon);

                    Vector3 unitDir = new Vector3(cosLat * cosLon, sinLat, cosLat * sinLon);
                    // Sample direction rather than vertex index: UV seams and poles stay closed.
                    float bump=1f+Mathf.Sin(unitDir.x*11f+unitDir.y*7f+center.x*.37f)*.045f
                        +Mathf.Cos(unitDir.z*13f-unitDir.y*9f+center.z*.41f)*.035f;
                    Vector3 pos = center + Vector3.Scale(radius * bump, unitDir);

                    vertices.Add(pos);
                    normals.Add(new Vector3(unitDir.x/radius.x,unitDir.y/radius.y,unitDir.z/radius.z).normalized);
                    uvs.Add(new Vector2(u * 2f, t * 2f));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int cur = startIdx + r * (segments + 1) + s;
                    int next = cur + 1;
                    int up = cur + (segments + 1);
                    int upNext = up + 1;

                    indices.Add(cur); indices.Add(up); indices.Add(next);
                    indices.Add(next); indices.Add(up); indices.Add(upNext);
                }
            }
            // Sparse pointed leaf tips break the solid outline, sharing the crown material.
            // Six double-sided blades per cluster avoid alpha overdraw on mobile GPUs.
            for(int leaf=0;leaf<(simplified?0:6);leaf++)
            {
                float angle=leaf*2.39996f+center.x*.71f+center.z*.39f;
                float elevation=Mathf.Sin(leaf*1.73f+center.z)*.65f;
                Vector3 outward=new Vector3(Mathf.Cos(angle),elevation,Mathf.Sin(angle)).normalized;
                float shell=1f+Mathf.Sin(outward.x*11f+outward.y*7f+center.x*.37f)*.045f
                    +Mathf.Cos(outward.z*13f-outward.y*9f+center.z*.41f)*.035f;
                Vector3 anchor=center+Vector3.Scale(radius,outward)*(shell-.05f);
                Vector3 side=Vector3.Cross(outward,Vector3.up).normalized;
                float length=Mathf.Clamp(radius.magnitude*.14f,.16f,.38f);
                Vector3 tip=anchor+outward*length;
                Vector3 middle=anchor+outward*length*.48f;
                int first=vertices.Count;
                vertices.Add(anchor-outward*.12f);vertices.Add(middle+side*length*.24f);
                vertices.Add(tip);vertices.Add(middle-side*length*.24f);
                for(int n=0;n<4;n++)normals.Add((outward+Vector3.up*.45f).normalized);
                uvs.Add(new Vector2(.1f,.1f));uvs.Add(new Vector2(.4f,.5f));uvs.Add(new Vector2(.9f,.9f));uvs.Add(new Vector2(.6f,.5f));
                indices.AddRange(new[]{first,first+1,first+2,first,first+2,first+3,
                    first+2,first+1,first,first+3,first+2,first});
            }
        }

        static PlantingKind LandUse(MapFeature feature)
        {
            string subtype=MapFeatureStyle.Text(feature,"subtype");
            string natural=MapFeatureStyle.Text(feature,"natural");
            string landuse=MapFeatureStyle.Text(feature,"landuse");
            if(subtype=="pitch")return PlantingKind.Sports;
            if(subtype=="forest" || natural=="wood" || landuse=="forest")return PlantingKind.Woodland;
            if(natural=="scrub")return PlantingKind.Scrub;
            if(landuse=="meadow" || landuse=="grass" || landuse=="orchard")return PlantingKind.Meadow;
            return PlantingKind.Park;
        }
        static float TreeProbability(PlantingKind kind)
        {
            switch(kind)
            {
                case PlantingKind.Woodland:return .28f;
                case PlantingKind.Park:return .09f;
                case PlantingKind.Scrub:return .035f;
                case PlantingKind.Meadow:return .018f;
                default:return .025f;
            }
        }
        static float ShrubProbability(PlantingKind kind)
        {
            switch(kind)
            {
                case PlantingKind.Woodland:return .18f;
                case PlantingKind.Scrub:return .28f;
                case PlantingKind.Park:return .11f;
                case PlantingKind.Meadow:return .04f;
                default:return .023f;
            }
        }
        public static bool CanPlant(Vector2 point,List<MapFeature> features,out float groundHeight)
            =>CanPlant(point,features,out groundHeight,out _);
        public static bool CanPlant(Vector2 point,List<MapFeature> features,out float groundHeight,out PlantingKind kind)
        {
            groundHeight=.025f;
            kind=PlantingKind.Open;
            foreach(var feature in features)
            {
                var points=feature.Points;
                if(points.Count<2) continue;
                bool area=feature.Kind!="road";
                if(area && (feature.Kind=="water" && feature.WaterRings.Count>0?
                    WaterGeometry.Contains(feature.WaterRings,point):Contains(point,points)))
                {
                    if(feature.Kind!="park") return false;
                    groundHeight=.18f;
                    var mapped=LandUse(feature);
                    if(mapped>kind)kind=mapped;
                }
                float clearance=feature.Kind=="road"?MapFeatureStyle.RoadWidth(feature)*.5f+1.2f:3;
                if(feature.Kind=="park") continue;
                var rings=feature.Kind=="water" && feature.WaterRings.Count>0?feature.WaterRings:new List<List<Vector2>>{points};
                foreach(var ring in rings)for(int i=0;i<(area?ring.Count:ring.Count-1);i++)
                {
                    Vector2 a=ring[i], b=ring[(i+1)%ring.Count], delta=b-a;
                    float t=delta.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(point-a,delta)/delta.sqrMagnitude):0;
                    if((point-(a+delta*t)).sqrMagnitude<clearance*clearance) return false;
                }
            }
            return true;
        }

        static bool Contains(Vector2 point,List<Vector2> polygon)
        {
            bool inside=false;
            for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
            {
                Vector2 a=polygon[i], b=polygon[j];
                if((a.y>point.y)!=(b.y>point.y) && point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
            }
            return inside;
        }

        void BuildRidge(string label,float inner,float crest,float outer,float height,Material slopes,int seed,Material peaks=null)
        {
            const int segments=80;
            var random=new System.Random(seed);
            var ring=new Vector3[4,segments];
            for(int i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;
                Vector3 direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                float peak=height*(.35f+(float)random.NextDouble()*.65f);
                ring[0,i]=direction*inner+Vector3.down*2;
                ring[1,i]=direction*(inner+(crest-inner)*.55f)+Vector3.up*(peak*.3f);
                ring[2,i]=direction*(crest+(float)random.NextDouble()*55)+Vector3.up*peak;
                ring[3,i]=direction*outer+Vector3.down*3;
            }
            for(int band=0;band<3;band++)
            {
                var vertices=new List<Vector3>(); var indices=new List<int>();
                for(int i=0;i<segments;i++)
                {
                    int j=(i+1)%segments;
                    AddFace(vertices,indices,ring[band,i],ring[band,j],ring[band+1,i]);
                    AddFace(vertices,indices,ring[band,j],ring[band+1,j],ring[band+1,i]);
                }
                MakeMesh(label,vertices,indices,band==1 && peaks!=null?peaks:slopes);
            }
        }

        static void AddFace(List<Vector3> vertices,List<int> indices,Vector3 a,Vector3 b,Vector3 c)
        {
            if(Vector3.Cross(b-a,c-a).y<0) { var swap=b; b=c; c=swap; }
            int start=vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            indices.AddRange(new[]{start,start+1,start+2});
        }

        void MakeMesh(string label,List<Vector3> vertices,List<int> indices,Material material,List<Vector2> uvs=null,List<Vector3> normals=null,float minDistance=0,float maxDistance=0)
        {
            if (vertices == null || vertices.Count == 0 || indices == null || indices.Count == 0) return;
            var mesh=new Mesh { name=label };
            if (vertices.Count > 60000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(vertices);
            if (uvs != null && uvs.Count == vertices.Count) mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices,0);
            if (normals != null && normals.Count == vertices.Count) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            if(uvs!=null && uvs.Count==vertices.Count)mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            // Scenery has no colliders or runtime mesh edits. Keep its GPU data, but
            // discard the duplicate CPU copy on memory-constrained devices.
            if (Application.isMobilePlatform) mesh.UploadMeshData(true);
            meshes.Add(mesh);
            var obj=new GameObject(label); obj.transform.SetParent(transform,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            bool isTree = label.Contains("tree") || label.Contains("canopy") || label.Contains("trunk");
            renderer.shadowCastingMode = isTree ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows=true;
            if(label=="Grass patch" || label=="Shrub patch" || label=="Stone patch" || label=="Reed patch")
                obj.AddComponent<SceneryDistanceCull>().MaxDistance=label=="Grass patch"?110f:150f;
            else if(maxDistance>0)
            {
                var cull=obj.AddComponent<SceneryDistanceCull>();
                cull.MinDistance=minDistance;cull.MaxDistance=maxDistance;
                if(minDistance>0)renderer.enabled=false;
            }
        }

        void OnDestroy()
        {
            foreach(var mesh in meshes) if(mesh != null) Destroy(mesh);
            meshes.Clear();
            foreach(var material in materials) if(material != null) Destroy(material);
            materials.Clear();
        }
    }
}
