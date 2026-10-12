using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace GeoSniper
{
    public sealed class RealisticVegetation : MonoBehaviour
    {
        sealed class Batch
        {
            public readonly List<CombineInstance> Parts=new List<CombineInstance>();
            public Material Surface;
            public int Vertices;
            public float Distance;
        }
        readonly Dictionary<string,Batch> batches=new Dictionary<string,Batch>();
        readonly Dictionary<string,Material> surfaces=new Dictionary<string,Material>();
        readonly Dictionary<string,GameObject> sources=new Dictionary<string,GameObject>();
        readonly List<Mesh> meshes=new List<Mesh>();
        public bool Add(string name,Vector3 position,float height,float yaw=0)
        {
            if(!sources.TryGetValue(name,out var source))sources[name]=source=Resources.Load<GameObject>("Models/Vegetation/"+name);
            if(source==null)return false;
            var matrix=Matrix4x4.TRS(position,Quaternion.Euler(0,yaw,0),Vector3.one*height);
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var renderer=filter.GetComponent<Renderer>();
                if(mesh==null || renderer==null || renderer.sharedMaterials.Length==0)continue;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var original=renderer.sharedMaterials[Mathf.Min(sub,renderer.sharedMaterials.Length-1)];
                    string atlas=name=="GrassClump" || name=="LeafyGroundPlant"?"GrassAtlas":original==null?"ShrubGreen":original.name.Split('.')[0];
                    if(!surfaces.TryGetValue(atlas,out var surface))
                    {
                        var texture=Resources.Load<Texture2D>("Models/Vegetation/"+atlas);
                        surface=new Material(Shader.Find("GeoSniper/Vegetation")){name="Vegetation "+atlas,mainTexture=texture,enableInstancing=true};
                        surface.color=texture==null?new Color(.23f,.34f,.13f):
                            atlas=="LeafPhoto"?new Color(.58f,.53f,.44f):
                            atlas=="BarkPhoto"?new Color(.8f,.48f,.15f):
                            atlas=="LeafAtlas"?new Color(.42f,.7f,.28f):Color.white;
                        surface.SetFloat("_Cutoff",texture==null?0:.35f);surfaces[atlas]=surface;
                    }
                    string key=atlas+"/"+Mathf.FloorToInt(position.x/64)+"/"+Mathf.FloorToInt(position.z/64)+"/"+(name.Contains("Tree")?"tree":"ground");
                    if(!batches.TryGetValue(key,out var batch))batches[key]=batch=new Batch{Surface=surface,Distance=name.Contains("Tree")?180:100};
                    if(batch.Vertices+mesh.vertexCount>60000)Emit(batch);
                    // FBX roots can carry the unit/axis conversion. Retain that transform.
                    batch.Parts.Add(new CombineInstance{mesh=mesh,subMeshIndex=sub,transform=matrix*filter.transform.localToWorldMatrix});
                    batch.Vertices+=mesh.vertexCount;
                }
            }
            return true;
        }
        public void Flush(){foreach(var batch in batches.Values)Emit(batch);}
        void Emit(Batch batch)
        {
            if(batch.Parts.Count==0)return;
            var mesh=new Mesh{name="CGTrader vegetation batch",indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(batch.Parts.ToArray(),true,true);mesh.RecalculateBounds();
            if(Application.isMobilePlatform)mesh.UploadMeshData(true);meshes.Add(mesh);
            var obj=new GameObject("Realistic vegetation");obj.transform.SetParent(transform,false);
            obj.AddComponent<VegetationBatchMarker>();
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=batch.Surface;
            renderer.shadowCastingMode=batch.Distance>100?ShadowCastingMode.On:ShadowCastingMode.Off;
            obj.AddComponent<SceneryDistanceCull>().MaxDistance=batch.Distance;
            batch.Parts.Clear();batch.Vertices=0;
        }
        void OnDestroy()
        {
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            foreach(var material in surfaces.Values)if(material!=null)Destroy(material);
        }
    }
    public sealed class VegetationBatchMarker : MonoBehaviour { }
}
