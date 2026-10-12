using System;
using System.IO;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEngine;
public static class MixamoBindingChecks
{
    public static void Run()
    {
        var report=new StringBuilder();
        try
        {
            foreach(var textureName in new[]{"StoryDocument","RelayPlacard"})
            {
                string texturePath="Assets/Resources/Textures/"+textureName+".png";AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                var texture=Resources.Load<Texture2D>("Textures/"+textureName);if(texture==null)throw new Exception("Missing objective texture "+textureName);report.AppendLine("PASS: "+textureName+" texture loads");
            }
            foreach(var name in new[]{"paper_pickup","relay_off"})if(Resources.Load<AudioClip>("Audio/Interactions/"+name)==null)throw new Exception("Missing action sound "+name);
            foreach(var model in new[]{"swat","ch35","civilian_carla","civilian_eric","civilian_doctor"})
            {
                bool enemy=model=="swat" || model=="ch35";
                var asset=Resources.Load<GameObject>("Models/"+(enemy?"Enemies/":"Civilians/")+model);var owner=new GameObject(model+" check");
                var visual=ImportedVisual.CreateEnemy(asset,owner.transform);var rig=ImportedVisual.CharacterAnimationRoot(visual.transform);
                foreach(var action in enemy?new[]{"walk","hit"}:new[]{"walk","run","fear"})
                {
                    var clip=Resources.Load<AnimationClip>("Models/Animations/Mixamo/"+model+"_"+action);if(clip==null)throw new Exception("Missing "+model+" "+action);
                    foreach(var binding in AnimationUtility.GetCurveBindings(clip))if(rig.transform.Find(binding.path)==null)throw new Exception("Unbound bone "+model+": "+binding.path);
                    clip.SampleAnimation(rig,action=="fear"?5f:clip.length*.4f);
                    var bounds=ImportedVisual.PosedBounds(visual.transform);if(bounds.size.y<1.4f || bounds.size.y>2.5f)throw new Exception("Distorted silhouette "+model+" "+action+": "+bounds);
                    report.AppendLine("PASS: "+model+" "+action+" // every rotation curve binds; human scale retained");
                }
                if(enemy)
                {
                    var target=new GameObject("Target");var bot=owner.AddComponent<EnemyBot>();bot.Initialize(target.transform,0);var driver=owner.AddComponent<ArmyAnimation>();if(!driver.Initialize(bot,visual.transform))throw new Exception("Army driver not ready");
                    var privateFields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    if((string)typeof(ArmyAnimation).GetField("walk",privateFields).GetValue(driver)!="mixamo_walk" || (string)typeof(ArmyAnimation).GetField("hit",privateFields).GetValue(driver)!="mixamo_hit")throw new Exception("Army isn't using downloaded Mixamo clips");
                    UnityEngine.Object.DestroyImmediate(target);
                }
                UnityEngine.Object.DestroyImmediate(owner);
            }
            File.WriteAllText("Logs/MixamoBindings.txt",report.ToString());CleanFlowChecks.RunLeak();
        }
        catch(Exception e){File.WriteAllText("Logs/MixamoBindings.txt",report+"FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
