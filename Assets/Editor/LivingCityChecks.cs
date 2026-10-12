using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

public static class LivingCityChecks
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Invoke(object obj,string method)=>obj.GetType().GetMethod(method,Flags).Invoke(obj,null);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        var report=new StringBuilder();int code=0;
        try
        {
            foreach(var name in new[]{"civilian_carla","civilian_eric","civilian_doctor"})
            {
                var owner=new GameObject("Idle check");
                try
                {
                    var prefab=Resources.Load<GameObject>("Models/Civilians/"+name);Check(prefab!=null,name+" missing");
                    var visual=ImportedVisual.CreateEnemy(prefab,owner.transform).transform;
                    var bot=owner.AddComponent<CivilianBot>();
                    var animation=owner.AddComponent<CivilianAnimation>();Check(animation.Initialize(bot,visual),name+" clips unavailable");
                    Invoke(animation,"Update");
                    typeof(CivilianAnimation).GetField("idlePhase",Flags).SetValue(animation,0f);Invoke(animation,"LateUpdate");
                    var head=WeaponGripPose.Bone(visual,"head");Check(head!=null,name+" head missing");var first=head.localRotation;
                    Invoke(animation,"Update");typeof(CivilianAnimation).GetField("idlePhase",Flags).SetValue(animation,2f);Invoke(animation,"LateUpdate");
                    Check(Quaternion.Angle(first,head.localRotation)>.5f,name+" idle head frozen");
                    var second=head.localRotation;Invoke(animation,"Update");Invoke(animation,"LateUpdate");
                    Check(Quaternion.Angle(second,head.localRotation)<.1f,name+" idle rotation accumulates");
                    report.AppendLine("PASS: "+name+" idle moves and repeated sampling has no rotation drift");
                }
                finally{Object.DestroyImmediate(owner);}
            }
            foreach(var name in new[]{"swat","ch35"})
            {
                var prefab=Resources.Load<GameObject>("Models/Enemies/"+name);
                if(prefab==null)prefab=Resources.Load<GameObject>("Models/Characters/"+name);
                Check(prefab!=null,name+" enemy missing");var owner=new GameObject("Ragdoll check");
                try
                {
                    var visual=ImportedVisual.CreateEnemy(prefab,owner.transform).transform;
                    var bot=owner.AddComponent<EnemyBot>();bot.Initialize(null,0);
                    var authored=owner.AddComponent<ArmyAnimation>();authored.Initialize(bot,visual);
                    var gait=owner.AddComponent<EnemyWalkAnimator>();gait.Initialize(bot,visual);
                    Check(gait.EnablePhysicalRagdoll(Vector3.forward,owner.transform.position),name+" ragdoll unavailable");
                    var bodies=visual.GetComponentsInChildren<Rigidbody>();var joints=visual.GetComponentsInChildren<CharacterJoint>();
                    report.AppendLine(name+" bodies: "+string.Join(",",bodies.Select(b=>b.name+":"+(b.GetComponent<Joint>()!=null))));
                    Check(bodies.Length>=9 && joints.Length>=bodies.Length-1,name+" incomplete skeletal joints: bodies="+bodies.Length+", joints="+joints.Length);
                    Check(!authored.enabled && visual.GetComponentsInChildren<Animation>().All(a=>!a.enabled),name+" animation still drives corpse");
                    Check(bodies.All(b=>b.maxAngularVelocity<=5 && b.solverIterations>=12),name+" unstable body settings");
                    var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.position=new Vector3(0,-.25f,0);ground.transform.localScale=new Vector3(30,.5f,30);
                    var previousMode=Physics.simulationMode;
                    try
                    {
                        Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();
                        for(int frame=0;frame<150;frame++)Physics.Simulate(.02f);
                        Check(bodies.All(b=>float.IsFinite(b.position.y) && b.position.y>-.5f && b.linearVelocity.magnitude<10),name+" body explodes or falls through ground");
                        Check(joints.All(j=>Vector3.Distance(j.transform.TransformPoint(j.anchor),j.connectedBody.transform.TransformPoint(j.connectedAnchor))<.4f),name+" joints separate during collapse");
                        Check(bodies.All(b=>b.GetComponent<Collider>().bounds.size.magnitude>.2f),name+" collider shrunk by FBX scale");
                        var camera=new GameObject("Collapse capture").AddComponent<Camera>();
                        try
                        {
                            RenderSettings.ambientLight=new Color(.6f,.6f,.6f);camera.transform.position=new Vector3(3,2,-4);camera.transform.LookAt(new Vector3(0,.4f,0));
                            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.1f,.14f);
                            var rt=new RenderTexture(900,600,24);var tex=new Texture2D(900,600,TextureFormat.RGB24,false);var previous=RenderTexture.active;
                            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,900,600),0,0);tex.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/Death-"+name+".png",tex.EncodeToPNG());}
                            finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);}
                        }
                        finally{Object.DestroyImmediate(camera.gameObject);}
                    }
                    finally{Physics.simulationMode=previousMode;Object.DestroyImmediate(ground);}
                    report.AppendLine("PASS: "+name+" death uses "+bodies.Length+" bodies, "+joints.Length+" connected joints and releases animation ownership");
                }
                finally{Object.DestroyImmediate(owner);}
            }
            var sources=new System.Collections.Generic.List<NavMeshBuildSource>();
            foreach(var center in new[]{Vector3.zero,new Vector3(30,0,0)})sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(12,.2f,12),transform=Matrix4x4.TRS(center,Quaternion.identity,Vector3.one),area=0});
            var data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),sources,new Bounds(new Vector3(15,0,0),new Vector3(60,10,30)),Vector3.zero,Quaternion.identity);
            var nav=NavMesh.AddNavMeshData(data);var walker=new GameObject("Disconnected route check");
            try
            {
                var civilian=walker.AddComponent<CivilianBot>();
                var reachable=typeof(CivilianBot).GetMethod("Reachable",Flags);
                Check((bool)reachable.Invoke(civilian,new object[]{new Vector3(3,0,0)}),"reachable street rejected");
                Check(!(bool)reachable.Invoke(civilian,new object[]{new Vector3(30,0,0)}),"disconnected patrol accepted");
                report.AppendLine("PASS: civilian route selection accepts connected ground and rejects disconnected islands");
            }
            finally{nav.Remove();Object.DestroyImmediate(data);Object.DestroyImmediate(walker);}
        }
        catch(Exception ex){report.AppendLine("FAIL: "+ex);code=1;}
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/LivingCityChecks.txt",report.ToString());Debug.Log(report.ToString());EditorApplication.Exit(code);
    }
}
