using System;
using System.IO;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEngine;

public static class EnemyScopeRegressionChecks
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/review-unity/"))throw new Exception("Use disposable review project");
        var report=new StringBuilder();Directory.CreateDirectory("Logs");
        try
        {
            foreach(bool crouched in new[]{false,true})
            {
                float awareness=0;
                for(int i=0;i<15;i++)awareness=EnemyAwarenessPolicy.Detection(awareness,true,3f/65,crouched,1,1f/60,3);
                Require(awareness>=1,"Exposed close player is not recognised in .25 seconds");
                Require(EnemyAwarenessPolicy.Detection(0,false,.01f,crouched,1,.1f,1)==0,"Wall-hidden nearby player gains detection");
            }
            report.AppendLine("PASS: close standing/crouched player detected in .25 seconds; hidden proximity grants no detection");
            var config=ScriptableObject.CreateInstance<WeaponConfig>();config.zoomLevels=new[]{24f,12f,6f};
            Require(Mathf.Abs(ScopeOptics.ZoomFov(config,0)-24)<.001f && Mathf.Abs(ScopeOptics.ZoomFov(config,1)-6)<.001f,"Zoom endpoints differ from optic limits");
            float previous=float.MaxValue;
            for(int i=0;i<=100;i++)
            {
                float fov=ScopeOptics.ZoomFov(config,i/100f);Require(fov<=previous && fov>=5.99f,"Zoom is discontinuous or out of range");previous=fov;
                Require(Mathf.Abs(ScopeOptics.FovForMagnification(65,ScopeOptics.Magnification(65,fov))-fov)<.001f,"Optical magnification does not match camera FOV");
            }
            float reference=0;
            foreach(int fps in new[]{30,60,120})
            {
                float fov=65;for(int i=0;i<fps/2;i++)fov=ScopeOptics.SmoothFov(fov,6,10,1f/fps);
                if(fps==30)reference=fov;else Require(Mathf.Abs(fov-reference)<.0001f,"Zoom settling depends on frame rate");
            }
            Require(ScopeOptics.AimScale(6)<ScopeOptics.AimScale(14),"High zoom does not reduce aim sensitivity");
            Require(ScopeOptics.LensPixel(0,0).a==0 && ScopeOptics.LensPixel(.99f,.1f).a==1,"Lens hides its aim centre or leaks at its outer edge");
            UnityEngine.Object.DestroyImmediate(config);
            report.AppendLine("PASS: zoom is continuous across all levels, camera/readout magnification agrees, settling matches 30/60/120 fps, high zoom scales aiming");
            var randomState=UnityEngine.Random.state;
            try{CampaignEnemyReactionChecks.RunPerception();}
            finally{UnityEngine.Random.state=randomState;}
            report.AppendLine("PASS: physical enemy detection, damage reactions, squad movement and wall occlusion regressions");
            File.WriteAllText("Logs/EnemyScopeRegressionChecks.txt",report.ToString());
            SessionState.SetBool("GeoSniper.EnemyScopeCapture",true);CleanFlowChecks.RunLeak();
        }
        catch(Exception error)
        {
            report.AppendLine("FAIL: "+error);File.WriteAllText("Logs/EnemyScopeRegressionChecks.txt",report.ToString());Debug.LogException(error);EditorApplication.Exit(1);
        }
    }
}
