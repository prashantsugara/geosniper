using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using GeoSniper;
using Object=UnityEngine.Object;

public static class WeaponPoseChecks
{
    public static void Inspect()
    {
        var log=new StringBuilder();
        foreach(string path in new[]{"Models/Enemies/swat","Models/Enemies/ch35","Models/FPSArms/FPSSniperRifle","Models/Weapons/Barrett50","Models/Weapons/M24Tactical","Models/Weapons/MK12SPR","Models/AK47"})
        {
            var prefab=Resources.Load<GameObject>(path);
            if(prefab==null) {log.AppendLine("MISSING "+path);continue;}
            var o=Object.Instantiate(prefab);
            log.AppendLine("ASSET "+path);
            foreach(var r in o.GetComponentsInChildren<Renderer>(true))
                log.AppendLine("RENDERER "+r.name+" parent="+r.transform.parent?.name+" bounds="+ImportedVisual.RendererBounds(o.transform,r));
            foreach(var t in o.GetComponentsInChildren<Transform>(true))
            {
                string n=t.name.ToLowerInvariant();
                if(n.Contains("hand") || n.Contains("arm") || n.Contains("wrist") || n.Contains("elbow") || n.Contains("hips") || n.Contains("grip") || n.Contains("muzzle"))
                    log.AppendLine("BONE "+t.name+" parent="+t.parent?.name+" p="+o.transform.InverseTransformPoint(t.position).ToString("F4")+" scale="+t.lossyScale);
            }
            foreach(var a in o.GetComponentsInChildren<Animation>(true)) log.AppendLine("ANIMATION ROOT "+a.name);
            Object.DestroyImmediate(o);
        }
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WeaponAssetInspection.txt",log.ToString());
    }
}
