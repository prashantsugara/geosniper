using System.Collections.Generic;
using System.IO;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolated renderer: never saves or replaces the user's open scene.
public static class CityVisualPreview
{
    public static void Run()
    {
        if(!Application.isBatchMode) throw new System.InvalidOperationException("Run in isolated batch project only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        FacadeDetailChecks.Run();
        var features=new List<MapFeature>();
        for(int i=0;i<6;i++)
        {
            float x=(i%3)*26-26,z=(i/3)*38;
            features.Add(new MapFeature {Kind="building",Height=12+(i%3)*4,Points=new List<Vector2>{
                new Vector2(x-8,z-7),new Vector2(x+8,z-7),new Vector2(x+8,z+7),new Vector2(x-8,z+7)}});
        }
        features.Add(new MapFeature {Kind="road",Points=new List<Vector2>{new Vector2(-65,19),new Vector2(65,19)}});
        var world=new GameObject("Actual sector generator preview").AddComponent<SectorWorld>();
        world.Generate(features);
        var sun=new GameObject("Preview sunlight").AddComponent<Light>();sun.type=LightType.Directional;
        sun.transform.rotation=Quaternion.Euler(32,-35,0);sun.color=new Color(1,.79f,.53f);sun.intensity=1.15f;
        sun.shadows=LightShadows.Soft;
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.28f,.32f,.4f);RenderSettings.fog=false;
        var camera=new GameObject("Preview camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(48,26,-46);camera.transform.LookAt(new Vector3(0,9,8));
        camera.fieldOfView=52;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.32f,.42f,.53f);
        var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;
        Directory.CreateDirectory("Logs");
        foreach(string time in new[]{"day","night"})
        {
            if(time=="night") {sun.intensity=.15f;sun.color=new Color(.3f,.45f,.8f);RenderSettings.ambientLight=new Color(.07f,.1f,.16f);camera.backgroundColor=new Color(.025f,.04f,.08f);}
            SectorStreetLighting.SetNight(time=="night");
            SectorStreetLighting.RefreshActiveLights(camera);
            camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
            File.WriteAllBytes("Logs/City-"+time+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
        }
        camera.targetTexture=null;RenderTexture.active=null;rt.Release();Object.DestroyImmediate(rt);
        Debug.Log("CITY_VISUAL_PREVIEW_COMPLETE");
        EditorApplication.Exit(0);
    }
}
