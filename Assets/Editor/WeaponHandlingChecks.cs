using System;
using System.IO;
using System.Text;
using GeoSniper;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class WeaponHandlingChecks
{
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/review-unity/"))throw new Exception("Use disposable review project");
        var report=new StringBuilder();
        try
        {
            float reference=0;
            foreach(int fps in new[]{30,60,120})
            {
                float x=0,v=52,peak=0;
                for(int frame=0;frame<fps;frame++){WeaponHandlingMotion.Recover(ref x,ref v,1f/fps);peak=Mathf.Max(peak,x);if(frame==fps/2-1){if(fps==30)reference=x;else Require(Mathf.Abs(x-reference)<.00001f,"Recoil varies with frame rate");}}
                Require(peak>.5f && peak<1 && Mathf.Abs(x)<.001f,"Recoil doesn't settle");
            }
            Require(WeaponHandlingMotion.ReloadHand(0)==0 && WeaponHandlingMotion.ReloadHand(1)==0,"Reload doesn't return hands to grip");
            Require(WeaponHandlingMotion.BoltHand(0)==0 && WeaponHandlingMotion.BoltHand(1)==0 && WeaponHandlingMotion.BoltPull(1)==0,"Bolt hand doesn't return to trigger");
            Require(WeaponHandlingMotion.BoltPull(.48f)>.95f && WeaponHandlingMotion.BoltPull(.2f)==0,"Bolt pull must follow handle lift");
            Require(WeaponHandlingMotion.BoltAction(1) && WeaponHandlingMotion.BoltAction(3) && !WeaponHandlingMotion.BoltAction(0),"Semi-auto classified as manual bolt");
            Require(Mathf.Abs(WeaponHandlingMotion.BoltDuration(1)-WeaponCatalog.Get(1).Cadence)<.001f,"Starter bolt timing differs from fire cadence");
            report.AppendLine("PASS: recoil settles consistently at 30/60/120 fps; reload starts and ends on grip");
            report.AppendLine("PASS: manual bolt lift/pull/return phases match starter cadence; semi-auto uses immediate ejection");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Weapon preview camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.2f);camera.fieldOfView=65;
            var light=new GameObject("Weapon preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(40,-30,0);
            RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
            var presentation=new GameObject("Weapon handling preview").AddComponent<SniperPresentation>();presentation.Initialize(camera);
            Directory.CreateDirectory("Logs");
            for(int index=0;index<WeaponCatalog.Count;index++)
            {
                presentation.EquipWeaponModel(index);
                var pose=camera.GetComponentInChildren<FirstPersonGripPose>(true);Require(pose!=null,"Missing arms for "+index);
                var transforms=pose.GetComponentsInChildren<Transform>(true);
                foreach(float phase in new[]{0f,.35f,.55f,.9f,1f})
                {
                    presentation.ReloadSound(1.8f);presentation.Animate(false,phase==1?0:1.8f*(1-phase),false);
                    pose.SetHandling(phase,WeaponHandlingMotion.BoltHand(phase),WeaponHandlingMotion.BoltPull(phase));pose.Apply();
                    foreach(var bone in transforms)Require(float.IsFinite(bone.position.x) && float.IsFinite(bone.rotation.x),"Invalid arm pose "+index);
                    if(index==1)Capture(camera,"WeaponReload-"+Mathf.RoundToInt(phase*100)+".png");
                }
                presentation.ReloadSound(4.5f);presentation.Animate(false,4.5f*(1-.61f),false);
                var soundPhase=typeof(SniperPresentation).GetField("reloadSoundPhase",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                Require((int)soundPhase.GetValue(presentation)==2,"Magazine clicks do not track actual reload duration");
                presentation.Animate(false,0,false);
                Require((int)soundPhase.GetValue(presentation)==3,"Reload latch missing at completion");
                pose.SetHandling(1,1);pose.Apply();
                report.AppendLine("PASS: "+WeaponCatalog.Get(index).Name+" reload/bolt hand poses remain finite");
            }
            File.WriteAllText("Logs/WeaponHandlingChecks.txt",report.ToString());Debug.Log(report.ToString());
            if(SessionState.GetBool("GeoSniper.HandlingThenCampaign",false)){SessionState.SetBool("GeoSniper.HandlingThenCampaign",false);CleanFlowChecks.RunLeak();}
            else EditorApplication.Exit(0);
        }
        catch(Exception error){report.AppendLine("FAIL: "+error);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WeaponHandlingChecks.txt",report.ToString());Debug.LogException(error);EditorApplication.Exit(1);}
    }
    public static void RunCampaign(){SessionState.SetBool("GeoSniper.HandlingThenCampaign",true);Run();}
    static void Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(1280,720,24);var previous=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/"+name,image.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
    }
}
