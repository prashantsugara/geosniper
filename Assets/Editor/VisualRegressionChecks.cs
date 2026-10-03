using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class VisualRegressionChecks
{
    [MenuItem("Geo Sniper/Check Enemy And Sign Visuals")]
    public static void Run()
    {
        Directory.CreateDirectory("Logs");
        var scene=EditorSceneManager.NewPreviewScene();
        var previous=SceneManager.GetActiveScene();
        var report=new System.Text.StringBuilder();
        try
        {
            SceneManager.SetActiveScene(scene);
            var camera=new GameObject("Visual check camera").AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.27f,.33f,.38f);
            camera.nearClipPlane=.05f; camera.farClipPlane=60;
            var light=new GameObject("Preview light").AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=1.4f;
            light.transform.rotation=Quaternion.Euler(35,-25,0);

            var enemyRoot=new GameObject("Enemy regression");
            var enemyAsset=Resources.Load<GameObject>("Models/Enemies/army_character_1");
            if(enemyAsset==null) enemyAsset=Resources.Load<GameObject>("Models/Enemies/army_character_2");
            if(enemyAsset==null) throw new Exception("Missing imported enemy model");
            ImportedVisual.CreateEnemy(enemyAsset,enemyRoot.transform);
            var enemyBounds=ImportedVisual.LocalBounds(enemyRoot.transform);
            Check(Mathf.Abs(enemyBounds.min.y)<.01f && Mathf.Abs(enemyBounds.size.y-1.9f)<.01f,"Enemy feet at ground and height 1.9m",report);
            var enemyRenderers=enemyRoot.GetComponentsInChildren<Renderer>();
            Check(enemyRenderers.Length>=10,"Detailed enemy has visible gear meshes",report);
            var head=enemyRenderers.FirstOrDefault(r=>r.name.ToLowerInvariant().Contains("head") || (r.sharedMaterial!=null && r.sharedMaterial.name.ToLowerInvariant().Contains("face")));
            if(head!=null) Check(head.bounds.center.y>1.4f,"Enemy head upright",report);
            Render(camera,new Vector3(2.7f,1.8f,-4),new Vector3(0,1,0),"EnemyPreview");
            enemyRoot.SetActive(false);

            var rifleAsset=Resources.Load<GameObject>("Models/GeoSniperRifleProvided");
            if(rifleAsset==null) rifleAsset=Resources.Load<GameObject>("Models/GeoSniperRifle");
            if(rifleAsset==null) throw new Exception("Missing imported rifle model");
            var rifle=Object.Instantiate(rifleAsset);
            var rifleBounds=ImportedVisual.LocalBounds(rifle.transform);
            // Use world bounds too: import axis transforms must be retained by the game.
            Check(rifleBounds.size.z>rifleBounds.size.x*4 && rifleBounds.size.z>rifleBounds.size.y*4,"Rifle points forward along Z",report);
            Render(camera,new Vector3(2,.8f,1.9f),new Vector3(0,0,.2f),"RiflePreview");
            rifle.SetActive(false);

            var sector=new GameObject("Sign regression").AddComponent<SectorWorld>();
            typeof(SectorWorld).GetMethod("BuildRoadSigns",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sector,new object[]{
                new List<MapFeature>{new MapFeature {Kind="road",Name="Lane Number 12",Points=new List<Vector2>{new Vector2(0,-15),new Vector2(0,15)}}}
            });
            var texts=sector.GetComponentsInChildren<TextMesh>();
            Check(texts.Length==2,"One text face per side",report);
            foreach(var text in texts)
            {
                Check(text.GetComponent<Renderer>().sharedMaterial.shader.name=="GeoSniper/RoadSignText","Text uses the depth-tested sign shader",report);
                Check(Mathf.Abs(text.transform.localPosition.z)>WorldSign.Thickness/2,"Text outside board surface",report);
            }
            Vector3 center=texts[0].transform.parent.position;
            Render(camera,center+Vector3.back*9,center,"SignFrontPreview");
            Render(camera,center+Vector3.forward*9,center,"SignBackPreview");
            report.AppendLine("PASS");
        }
        catch(Exception error) { report.AppendLine("FAIL: "+error); Debug.LogException(error); }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllText("Logs/VisualRegressionChecks.txt",report.ToString());
        }
    }
    static void Check(bool value,string label,System.Text.StringBuilder report)
    {
        if(!value) throw new Exception(label);
        report.AppendLine("PASS: "+label);
    }
    static void Render(Camera camera,Vector3 position,Vector3 target,string name)
    {
        camera.transform.position=position; camera.transform.LookAt(target);
        var texture=new RenderTexture(800,600,24);
        var pixels=new Texture2D(800,600,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=texture; camera.Render(); RenderTexture.active=texture;
            pixels.ReadPixels(new Rect(0,0,800,600),0,0); pixels.Apply();
            File.WriteAllBytes("Logs/"+name+".png",pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=null; RenderTexture.active=previous;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels);
        }
    }
}
