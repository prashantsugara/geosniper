using System;
using System.IO;
using System.Reflection;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class PresentationChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/review-unity/"))
            throw new Exception("Use the isolated review project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Directory.CreateDirectory("Logs");
        var mission=new GameObject("Preview material owner").AddComponent<UrbanCombatMission>();
        typeof(UrbanCombatMission).GetMethod("EnsureEnemyMaterials",Private).Invoke(mission,null);
        var models=ModelLibrary.Load("Enemies");
        for(int i=0;i<models.Length;i++)
        {
            var root=new GameObject("Character preview "+i);
            root.transform.position=new Vector3((i-(models.Length-1)*.5f)*1.7f,0,0);
            root.transform.rotation=Quaternion.Euler(0,165,0);
            var visual=ImportedVisual.CreateEnemy(models[i],root.transform);
            typeof(UrbanCombatMission).GetMethod("ApplyEnemyMaterials",Private).Invoke(mission,new object[]{visual.transform});
            ImportedVisual.ConfigureCharacterSurfaces(visual.transform);
            var bot=root.AddComponent<EnemyBot>();bot.Initialize(null,i);
            var driver=root.AddComponent<ArmyAnimation>();
            if(!driver.Initialize(bot,visual.transform))throw new Exception("Missing animation: "+models[i].name);
            typeof(EnemyBot).GetField("<CurrentSpeed>k__BackingField",Private).SetValue(bot,.28f);
            typeof(ArmyAnimation).GetMethod("Update",Private).Invoke(driver,null);
            var animation=visual.GetComponentInChildren<Animation>();
            if(Mathf.Abs(animation["walk"].speed-.2f)>.01f)throw new Exception("Slow locomotion still foot-slides");
            typeof(EnemyBot).GetField("<CurrentSpeed>k__BackingField",Private).SetValue(bot,0f);
            bot.Actor.Damage(1f);
            typeof(ArmyAnimation).GetMethod("Update",Private).Invoke(driver,null);
            var pose=typeof(ArmyAnimation).GetField("current",Private);
            var hitPose=pose.GetValue(driver);driver.Fire();
            if(!Equals(hitPose,pose.GetValue(driver)))throw new Exception("Firing interrupts a hit reaction");
            typeof(ArmyAnimation).GetField("hitReactUntil",Private).SetValue(driver,-1f);
            pose.SetValue(driver,"idle");bot.Suspended=true;driver.Fire();
            if((string)pose.GetValue(driver)!="idle")throw new Exception("Suspended actor plays firing pose");
            bot.Suspended=false;
            animation.Stop();animation.Play(i==0?"idle":"walk");
            animation[i==0?"idle":"walk"].time=.35f;animation.Sample();
            typeof(ArmyAnimation).GetMethod("LateUpdate",Private).Invoke(driver,null);
            typeof(UrbanCombatMission).GetMethod("BuildEnemyWeapon",Private).Invoke(mission,new object[]{root,visual.transform,bot});
        }
        var sample=GameObject.CreatePrimitive(PrimitiveType.Cube);sample.name="Cloth test";
        var source=new Material(Shader.Find("Standard"));source.SetFloat("_Metallic",.8f);
        sample.GetComponent<Renderer>().sharedMaterial=source;
        ImportedVisual.ConfigureCharacterSurfaces(sample.transform);
        var block=new MaterialPropertyBlock();sample.GetComponent<Renderer>().GetPropertyBlock(block,0);
        if(block.GetFloat("_Metallic")!=0 || source.GetFloat("_Metallic")!=.8f)
            throw new Exception("Character finish changed shared material");
        sample.transform.position=Vector3.right*100;
        for(int i=0;i<40;i++)SniperPresentation.SpawnSurfaceImpact(sample.transform.position,sample.GetComponent<Collider>());
        var pool=(System.Collections.ICollection)typeof(SniperPresentation).GetField("impactPool",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        if(pool.Count>12)throw new Exception("Impact pool exceeded mobile cap");
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*2;
        var floorMat=new Material(Shader.Find("Standard")){color=new Color(.24f,.27f,.28f)};
        floor.GetComponent<Renderer>().sharedMaterial=floorMat;
        var sun=new GameObject("Preview sun").AddComponent<Light>();sun.type=LightType.Directional;
        sun.intensity=1.25f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(40,-35,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.5f,.57f,.65f);
        RenderSettings.ambientEquatorColor=new Color(.32f,.34f,.36f);
        RenderSettings.ambientGroundColor=new Color(.18f,.17f,.16f);
        var camera=new GameObject("Preview camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(0,1.35f,-4.7f);camera.transform.LookAt(new Vector3(0,1.05f,0));
        camera.fieldOfView=38;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.19f,.22f);
        var rt=new RenderTexture(1280,800,24);camera.targetTexture=rt;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var capture=new Texture2D(1280,800,TextureFormat.RGB24,false);
        capture.ReadPixels(new Rect(0,0,1280,800),0,0);capture.Apply();
        File.WriteAllBytes("Logs/character-presentation.png",capture.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;
        Object.DestroyImmediate(rt);Object.DestroyImmediate(capture);
        File.WriteAllText("Logs/PresentationChecks.txt","PASS: slow locomotion speed, shared material preservation, bounded surface effects, character render\n");
        Debug.Log("PASS: presentation checks and character render");
    }
}
