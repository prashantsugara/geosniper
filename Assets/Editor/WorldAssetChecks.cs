using System;
using System.IO;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class WorldAssetChecks
{
    [MenuItem("Geo Sniper/Check Building Assets %#F9")]
    public static void Run()
    {
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var assets=ModelLibrary.Load("Buildings");
            for(int i=0;i<assets.Length;i++)
            {
                var wrapper=new GameObject("Building review");
                Object.Instantiate(assets[i],wrapper.transform,false);
                var bounds=ImportedVisual.LocalBounds(wrapper.transform);
                wrapper.transform.localScale=Vector3.one*12/bounds.size.y;
                wrapper.transform.position=new Vector3((i-1.5f)*21,0,0)-Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),wrapper.transform.localScale);
                var mat=new Material(Shader.Find("Standard")); mat.mainTexture=Resources.Load<Texture2D>("Models/Buildings/"+assets[i].name+"_albedo");
                foreach(var r in wrapper.GetComponentsInChildren<Renderer>()) r.sharedMaterial=mat;
            }
            var camera=new GameObject("Review camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(40,28,66); camera.transform.LookAt(new Vector3(0,6,0));
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.54f,.65f,.69f);
            camera.fieldOfView=55;
            var sun=new GameObject("Review sun").AddComponent<Light>(); sun.type=LightType.Directional;
            sun.intensity=1.2f; sun.transform.rotation=Quaternion.Euler(40,-40,0);
            var rt=new RenderTexture(1280,720,24); var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var old=RenderTexture.active;
            try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply(); File.WriteAllBytes("Logs/BuildingAssets.png",pixels.EncodeToPNG()); }
            finally { RenderTexture.active=old; camera.targetTexture=null; Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels); }
        }
        catch(Exception e) { File.WriteAllText("Logs/WorldAssetChecks.txt",e.ToString()); throw; }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true); }
    }
}
