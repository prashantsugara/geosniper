using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RuntimeBodyChecks
{
    [MenuItem("Geo Sniper/Check Spawned Bodies %#F8")]
    public static void Run()
    {
        Directory.CreateDirectory("Logs");
        var report=new StringBuilder(DateTime.Now.ToString("O")+"\n");
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var target=new GameObject("Test player").AddComponent<UrbanPlayer>();
            var mission=new GameObject("Spawn test mission").AddComponent<UrbanCombatMission>();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(UrbanCombatMission).GetField("player",flags).SetValue(mission,target);
            typeof(UrbanCombatMission).GetMethod("EnsureEnemyMaterials",flags).Invoke(mission,null);
            var camera=new GameObject("Test camera").AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.22f,.28f,.33f);
            camera.nearClipPlane=.05f; camera.farClipPlane=30;
            var sun=new GameObject("Test sun").AddComponent<Light>();
            sun.type=LightType.Directional; sun.intensity=1.4f; sun.transform.rotation=Quaternion.Euler(30,-30,0);
            var models=ModelLibrary.Load("Enemies");
            report.AppendLine("Runtime models: "+string.Join(",",models.Select(m=>AssetDatabase.GetAssetPath(m))));
            for(int index=0;index<models.Length;index++)
            {
                Vector3 origin=new Vector3(12000+index*20,10,-9000);
                typeof(UrbanCombatMission).GetMethod("SpawnEnemy",flags).Invoke(mission,new object[]{index,(Vector3?)origin});
                var bot=Object.FindObjectsByType<EnemyBot>(FindObjectsSortMode.None).First(b=>b.gameObject.scene==scene && Vector3.Distance(b.transform.position,origin)<1);
                var animation=bot.GetComponentInChildren<Animation>();
                report.AppendLine("BOT "+index+" visual transforms: "+string.Join("; ",bot.GetComponentsInChildren<SkinnedMeshRenderer>().Select(s=>s.name+" local="+s.transform.localPosition+" scale="+s.transform.lossyScale+" bounds="+s.bounds)));
                foreach(AnimationState clip in animation)
                {
                  foreach(float sample in new[]{0f,.25f,.5f,.75f,1f})
                  {
                    animation.Stop(); animation.Play(clip.name); clip.time=clip.length*sample; animation.Sample();
                    var bounds=new Bounds(); bool initialized=false;
                    foreach(var skin in bot.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mesh=new Mesh(); skin.BakeMesh(mesh,false);
                        foreach(var vertex in mesh.vertices)
                        {
                            var p=bot.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex));
                            if(!initialized) { bounds=new Bounds(p,Vector3.zero); initialized=true; } else bounds.Encapsulate(p);
                        }
                        Object.DestroyImmediate(mesh);
                    }
                    report.AppendLine(clip.name+" sample="+sample+" baked body: "+bounds);
                    if(clip.name.ToLowerInvariant().Contains("idle") && sample==0) Render(camera,origin,index);
                    if(!initialized || !float.IsFinite(bounds.size.sqrMagnitude)
                        || bounds.size.y<.2f || bounds.size.y>2.5f
                        || bounds.size.x>2.5f || bounds.size.z>2.5f || bounds.center.magnitude>3)
                        throw new Exception("Deformed spawned body: "+models[index].name+" "+clip.name+" sample="+sample+" "+bounds);
                    // A skin with inconsistent bind transforms can look correct at
                    // the origin but stretch when placed on a geographic map.
                    foreach(var skin in bot.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mesh=new Mesh();
                        try
                        {
                            skin.BakeMesh(mesh,false);
                            var far=mesh.vertices;
                            for(int v=0;v<far.Length;v++) far[v]=bot.transform.InverseTransformPoint(skin.transform.TransformPoint(far[v]));
                            bot.transform.position=Vector3.zero;
                            animation.Sample(); skin.BakeMesh(mesh,false);
                            var near=mesh.vertices;
                            for(int v=0;v<near.Length;v++)
                            {
                                var point=bot.transform.InverseTransformPoint(skin.transform.TransformPoint(near[v]));
                                if(Vector3.Distance(point,far[v])>.02f)
                                    throw new Exception("Skin changes shape with world position: "+skin.name+" "+clip.name);
                            }
                        }
                        finally { bot.transform.position=origin; animation.Sample(); Object.DestroyImmediate(mesh); }
                    }
                  }
                }
            }
        }
        catch(Exception error) { report.AppendLine("FAIL: "+error); Debug.LogException(error); throw; }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true); File.WriteAllText("Logs/RuntimeBodyChecks.txt",report.ToString()); }
    }
    static void Render(Camera camera,Vector3 origin,int index)
    {
        camera.transform.position=origin+new Vector3(2,1.5f,4); camera.transform.LookAt(origin+Vector3.up);
        var rt=new RenderTexture(640,640,24); var pixels=new Texture2D(640,640,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,640,640),0,0); pixels.Apply(); File.WriteAllBytes("Logs/RuntimeEnemy"+index+".png",pixels.EncodeToPNG()); }
        finally { camera.targetTexture=null; RenderTexture.active=previous; Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels); }
    }
}
