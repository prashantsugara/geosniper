using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GeoSniper;

public static class GameplayGeometryAudit
{
    public static void Run()
    {
        var report=new StringBuilder("# Imported gameplay geometry audit\n\n");
        report.AppendLine("Generated: "+DateTime.UtcNow.ToString("O"));
        report.AppendLine("\nSource mesh inventory; missing materials may be assigned at runtime. Triangle totals are per model, not a frame-time estimate.\n");
        report.AppendLine("| Model | Vertices | Triangles | Renderers | Missing material slots | Rig problems | LOD group |\n|---|---:|---:|---:|---:|---:|---|");
        int models=0,invalid=0;
        foreach(var guid in AssetDatabase.FindAssets("t:Model",new[]{"Assets/Resources/Models"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(model==null)continue;
            models++;long vertices=0,triangles=0;int missing=0,rig=0;
            var meshes=new HashSet<Mesh>();var renderers=model.GetComponentsInChildren<Renderer>(true);
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true)) if(filter.sharedMesh!=null)meshes.Add(filter.sharedMesh);
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(skin.sharedMesh==null) {rig++;continue;} meshes.Add(skin.sharedMesh);
                foreach(var bone in skin.bones) if(bone==null)rig++;
            }
            foreach(var mesh in meshes)
            {
                vertices+=mesh.vertexCount;
                for(int i=0;i<mesh.subMeshCount;i++) if(mesh.GetTopology(i)==MeshTopology.Triangles)triangles+=mesh.GetIndexCount(i)/3;
                var size=mesh.bounds.size;
                // Flat architectural surfaces are legitimate. Reject empty/non-finite meshes, not planes.
                if(mesh.vertexCount==0 || !float.IsFinite(size.sqrMagnitude) || size.sqrMagnitude<1e-12f)invalid++;
            }
            foreach(var renderer in renderers) foreach(var material in renderer.sharedMaterials)
                if(material==null || material.shader==null)missing++;
            report.AppendLine($"| {path} | {vertices} | {triangles} | {renderers.Length} | {missing} | {rig} | {(model.GetComponentInChildren<LODGroup>(true)!=null?"yes":"no")} |");
        }
        report.AppendLine($"\nModels inspected: {models}. Empty/non-finite mesh bounds: {invalid}.\n");
        report.AppendLine("Not certified by this inventory: self-intersections, texture UV quality, terrain contact during every animation, mobile GPU cost, every generated city footprint, or device-specific rendering. These require visual/device tests.");
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GameplayGeometryAudit.md",report.ToString());
        if(invalid>0)throw new Exception("Invalid source mesh bounds; see GameplayGeometryAudit.md");
    }
}
