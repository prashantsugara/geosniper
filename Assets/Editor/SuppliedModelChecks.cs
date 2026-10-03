using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SuppliedModelChecks
{
    [MenuItem("Geo Sniper/Validate Supplied Models")]
    public static void Validate()
    {
        var report = new StringBuilder();
        foreach(var category in new[]{"Enemies", "Civilians", "Cars"})
        {
            var models = Resources.LoadAll<GameObject>("Models/" + category);
            if(models.Length == 0) throw new Exception("No imported models: " + category);
            foreach(var model in models)
            {
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                if(renderers.Length == 0) throw new Exception("No geometry: " + model.name);
                int textures = 0;
                foreach(var renderer in renderers)
                    foreach(var material in renderer.sharedMaterials)
                    {
                        if(material == null || material.shader == null)
                            throw new Exception("Missing material: " + model.name);
                        if(material.mainTexture != null) textures++;
                    }
                report.AppendLine(category + "/" + model.name + ": " + renderers.Length
                    + " renderers, " + textures + " textured materials");
            }
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/SuppliedModelChecks.txt", report.ToString());
        Debug.Log(report.ToString());
    }
}
