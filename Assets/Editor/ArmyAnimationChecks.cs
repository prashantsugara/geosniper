using System;
using System.IO;
using System.Linq;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ArmyAnimationChecks
{
//  [InitializeOnLoadMethod]
    static void Queue()
    {
        if(SessionState.GetBool("GeoSniper.ArmyClips.1",false)) return;
        SessionState.SetBool("GeoSniper.ArmyClips.1",true);
        EditorApplication.delayCall+=()=> { if(!EditorApplication.isPlayingOrWillChangePlaymode) Run(); };
    }
    [MenuItem("Geo Sniper/Validate Army Animations")]
    public static void Run()
    {
        var report=new StringBuilder();
        var runtimeModels=ModelLibrary.Load("Enemies");
        Require(runtimeModels.Length==2,"Runtime enemy selection must contain only the two rigged FBX models");
        foreach(var prefab in runtimeModels)
        {
            var instance=new GameObject("Runtime animation check");
            try
            {
                var visual=ImportedVisual.CreateEnemy(prefab,instance.transform);
                var bot=instance.AddComponent<EnemyBot>(); bot.Initialize(null,0);
                var driver=instance.AddComponent<ArmyAnimation>();
                Require(driver.Initialize(bot,visual.transform),"Runtime clips failed: "+prefab.name);
                var animation=visual.GetComponentInChildren<Animation>();
                var states=animation.Cast<AnimationState>().ToArray();
                foreach(var clip in new[]{"idle","walk","run","crouch","fire","hit","death"})
                    Require(states.Any(state=>state.name.ToLowerInvariant().Contains(clip)),"Missing "+clip+": "+prefab.name);
                var walk=states.First(state=>state.name.ToLowerInvariant().Contains("walk"));
                var foot=WeaponGripPose.Bone(visual.transform,"leftfoot");
                Require(foot!=null,"Missing left foot: "+prefab.name);
                animation.Play(walk.name);walk.time=0;animation.Sample();
                var start=foot.position;
                walk.time=walk.length*.25f;animation.Sample();
                Require(Vector3.Distance(start,foot.position)>.03f,"Runtime clip does not bind to skeleton: "+prefab.name);
                report.AppendLine("PASS "+prefab.name+": runtime animation binding and foot motion "+Vector3.Distance(start,foot.position).ToString("F3")+" m");
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ArmyAnimationChecks.txt",report.ToString());
            }
        }
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var owner=new GameObject("Patrol validation");
        var target=new GameObject("Patrol target");
        try
        {
            ground.transform.position=new Vector3(30000,-.5f,30000); ground.transform.localScale=new Vector3(20,1,20);
            owner.transform.position=new Vector3(30000,0,30000); target.transform.position=owner.transform.position+Vector3.forward*100;
            var bot=owner.AddComponent<EnemyBot>(); bot.Initialize(target.transform,0);
            Physics.SyncTransforms();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var safe=typeof(EnemyBot).GetMethod("SafeStep",flags);
            Require((bool)safe.Invoke(bot,new object[]{owner.transform.position+Vector3.forward*3}),"Clear patrol ground rejected");
            Require(!(bool)safe.Invoke(bot,new object[]{owner.transform.position+Vector3.forward*12}),"Roof edge accepted");
            bot.InitializePatrol();
            var point=(Vector3)typeof(EnemyBot).GetField("patrolTarget",flags).GetValue(bot);
            Require(Vector3.Distance(point,owner.transform.position)>1,"Patrol did not choose a destination");
            report.AppendLine("PASS patrol: clear ground accepted, ledge rejected, moving destination selected");
        }
        finally
        {
            Object.DestroyImmediate(owner); Object.DestroyImmediate(target); Object.DestroyImmediate(ground);
            File.WriteAllText("Logs/ArmyAnimationChecks.txt",report.ToString());
        }
    }
    static void Require(bool value,string message) { if(!value) throw new Exception(message); }
}
