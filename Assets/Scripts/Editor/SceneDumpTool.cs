using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using GeoSniper;

public class SceneDumpTool
{
    static string scratchDir = @"C:\Users\Geeta Sugara\.gemini\antigravity-ide\brain\680b4043-41ec-4d22-9781-a15910333197\scratch";

    [MenuItem("Tools/Dump Scene Data")]
    public static void DumpSceneData()
    {
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var targetObjects = allObjects.Select(g => new {
            name = g.name,
            position = new float[] { g.transform.position.x, g.transform.position.y, g.transform.position.z },
            rotation = new float[] { g.transform.rotation.eulerAngles.x, g.transform.rotation.eulerAngles.y, g.transform.rotation.eulerAngles.z },
            scale = new float[] { g.transform.localScale.x, g.transform.localScale.y, g.transform.localScale.z },
            parent = g.transform.parent != null ? g.transform.parent.name : "null"
        }).ToArray();

        string json = Newtonsoft.Json.JsonConvert.SerializeObject(targetObjects, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("scene_dump.json", json);
        Debug.Log("Dumped scene data to scene_dump.json");
    }

    [MenuItem("Tools/Weapons/Capture Screenshot")]
    public static void CaptureScreenshot()
    {
        string path = Path.Combine(scratchDir, "auto_screen.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log("[SceneDumpTool] Screenshot saved to " + path);
    }

    [MenuItem("Tools/Weapons/Equip Barrett .50")]
    public static void EquipBarrett()
    {
        var sniper = Object.FindFirstObjectByType<SniperPresentation>();
        if (sniper != null) sniper.EquipWeaponModel(0);
        CaptureScreenshot();
    }

    [MenuItem("Tools/Weapons/Equip M24 TAC")]
    public static void EquipM24()
    {
        var sniper = Object.FindFirstObjectByType<SniperPresentation>();
        if (sniper != null) sniper.EquipWeaponModel(1);
        CaptureScreenshot();
    }

    [MenuItem("Tools/Weapons/Equip MK12 SPR")]
    public static void EquipMK12()
    {
        var sniper = Object.FindFirstObjectByType<SniperPresentation>();
        if (sniper != null) sniper.EquipWeaponModel(2);
        CaptureScreenshot();
    }
}
