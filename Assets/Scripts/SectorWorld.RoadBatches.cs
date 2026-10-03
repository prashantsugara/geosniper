using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        sealed class RoadRenderBatch
        {
            public readonly List<Vector3> Vertices=new List<Vector3>();
            public readonly List<Vector2> Uv=new List<Vector2>();
            public readonly List<int> Indices=new List<int>();
            public Material Material;
        }
        readonly Dictionary<(Material,Vector2Int),RoadRenderBatch> roadRenderBatches=new Dictionary<(Material,Vector2Int),RoadRenderBatch>();
        void QueueRoadSurface(List<Vector3> vertices,List<Vector2> uv,List<int> triangles,Material material)
        {
            if(vertices.Count==0 || triangles.Count==0)return;
            var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var vertex in vertices)bounds.Encapsulate(vertex);
            var cell=new Vector2Int(Mathf.FloorToInt(bounds.center.x/64),Mathf.FloorToInt(bounds.center.z/64));
            var key=(material,cell);
            if(!roadRenderBatches.TryGetValue(key,out var batch))roadRenderBatches[key]=batch=new RoadRenderBatch{Material=material};
            if(batch.Vertices.Count+vertices.Count>60000){EmitRoadBatch(batch);roadRenderBatches[key]=batch=new RoadRenderBatch{Material=material};}
            int offset=batch.Vertices.Count;batch.Vertices.AddRange(vertices);batch.Uv.AddRange(uv);
            foreach(int index in triangles)batch.Indices.Add(offset+index);
        }
        void EmitRoadBatch(RoadRenderBatch batch)
        {
            if(batch.Indices.Count==0)return;
            var mesh=new Mesh{name="Spatial road surface batch"};
            if(batch.Vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(batch.Vertices);mesh.SetUVs(0,batch.Uv);mesh.SetTriangles(batch.Indices,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();meshes.Add(mesh);
            var holder=new GameObject("Road surface batch");holder.transform.SetParent(transform,false);
            holder.AddComponent<MeshFilter>().sharedMesh=mesh;holder.AddComponent<MeshRenderer>().sharedMaterial=batch.Material;
        }
        IEnumerator FlushRoadSurfaces()
        {
            float started=Time.realtimeSinceStartup;
            foreach(var batch in roadRenderBatches.Values)
            {
                EmitRoadBatch(batch);
                if(Time.realtimeSinceStartup-started>.003f){yield return null;started=Time.realtimeSinceStartup;}
            }
            roadRenderBatches.Clear();
        }
    }
}
