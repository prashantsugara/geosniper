using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class MixamoBake
{
    static string Key(string s)=>s.Replace("mixamorig:","").Replace("mixamorig","").Replace("_","").ToLowerInvariant();
    static readonly string[][] aliases={
        new[]{"Hips","hip"},new[]{"Spine","spine"},new[]{"Spine1","spine.001"},new[]{"Spine2","spine.002"},new[]{"Neck","neck"},new[]{"Head","head"},
        new[]{"LeftShoulder","shoulderl"},new[]{"LeftArm","leftarm"},new[]{"LeftForeArm","leftforearm"},new[]{"LeftHand","handl"},new[]{"RightShoulder","shoulderr"},new[]{"RightArm","rightarm"},new[]{"RightForeArm","rightforearm"},new[]{"RightHand","handr"},
        new[]{"LeftUpLeg","leftthigh"},new[]{"LeftLeg","leftshin"},new[]{"LeftFoot","leftfoot"},new[]{"LeftToeBase","balll"},new[]{"RightUpLeg","rightthigh"},new[]{"RightLeg","rightshin"},new[]{"RightFoot","rightfoot"},new[]{"RightToeBase","ballr"}};
    static Transform Find(GameObject o,string[] names){foreach(var t in o.GetComponentsInChildren<Transform>())foreach(var n in names)if(Key(t.name)==Key(n))return t;return null;}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/review-unity/"))throw new Exception("Use review project");
        SessionState.SetBool("GeoSniper.CivilianChecks",false);SessionState.SetBool("GeoSniper.CleanFlowChecks",false);SessionState.SetBool("GeoSniper.CampaignPlayChecks",false);
        string folder="Assets/Resources/Models/Animations/Mixamo";Directory.CreateDirectory(folder);
        var report=new List<string>();
        foreach(var motion in new[]{"enemy_walk","enemy_hit","civilian_walk","civilian_fear","civilian_run"})
        {
            string input="Assets/MixamoSource/"+motion+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(input);importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.SaveAndReimport();
            AnimationClip sourceClip=null;foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(input))if(asset is AnimationClip c && !c.name.Contains("preview")){sourceClip=c;break;}
            if(sourceClip==null)throw new Exception("No clip "+motion);
            foreach(var model in motion.StartsWith("enemy")?new[]{"swat","ch35"}:new[]{"civilian_carla","civilian_eric","civilian_doctor"})
            {
                var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(input));
                string modelPath="Assets/Resources/Models/"+(motion.StartsWith("enemy")?"Enemies/":"Civilians/")+model+".fbx";
                var target=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                foreach(var a in target.GetComponentsInChildren<Animation>())a.enabled=false;
                var sourceBones=new List<Transform>();var targetBones=new List<Transform>();
                foreach(var names in aliases){var a=Find(source,names);var b=Find(target,names);if(a!=null && b!=null){sourceBones.Add(a);targetBones.Add(b);}}
                if(targetBones.Count<18)throw new Exception("Incomplete retarget "+model+": "+targetBones.Count);
                var all=target.GetComponentsInChildren<Transform>();var rest=new Quaternion[all.Length];for(int i=0;i<all.Length;i++)rest[i]=all[i].localRotation;
                var sourceRest=new Quaternion[sourceBones.Count];var targetRest=new Quaternion[targetBones.Count];for(int i=0;i<sourceBones.Count;i++){sourceRest[i]=sourceBones[i].rotation;targetRest[i]=targetBones[i].rotation;}
                var curves=new AnimationCurve[targetBones.Count,4];for(int i=0;i<targetBones.Count;i++)for(int j=0;j<4;j++)curves[i,j]=new AnimationCurve();
                int frames=Mathf.CeilToInt(sourceClip.length*30);
                for(int f=0;f<=frames;f++)
                {
                    float time=Mathf.Min(f/30f,sourceClip.length);sourceClip.SampleAnimation(source,time);
                    for(int i=0;i<all.Length;i++)all[i].localRotation=rest[i];
                    for(int i=0;i<targetBones.Count;i++)
                    {
                        var a=sourceBones[i];var b=targetBones[i];
                        Transform ac=null,bc=null;
                        for(int k=0;k<sourceBones.Count;k++)if(sourceBones[k].parent==a && targetBones[k].parent==b){ac=sourceBones[k];bc=targetBones[k];break;}
                        if(ac==null && Key(a.name)!="head" && !Key(a.name).Contains("hand") && !Key(a.name).Contains("toe") && a.childCount>0 && b.childCount>0){ac=a.GetChild(0);bc=b.GetChild(0);}
                        if(Key(a.name)=="head" || Key(a.name)=="neck"){b.rotation=a.rotation*Quaternion.Inverse(sourceRest[i])*targetRest[i];}
                        else if(ac!=null && bc!=null){var from=bc.position-b.position;var to=ac.position-a.position;if(from.sqrMagnitude>.00001f && to.sqrMagnitude>.00001f)b.rotation=Quaternion.FromToRotation(from,to)*b.rotation;}
                        var q=b.localRotation;for(int j=0;j<4;j++)curves[i,j].AddKey(time,q[j]);
                    }
                }
                var clip=new AnimationClip{legacy=true,frameRate=30,name=model+"_"+motion.Substring(motion.IndexOf('_')+1)};
                for(int i=0;i<targetBones.Count;i++)for(int j=0;j<4;j++)clip.SetCurve(AnimationUtility.CalculateTransformPath(targetBones[i],target.transform),typeof(Transform),"localRotation."+"xyzw"[j],curves[i,j]);
                clip.EnsureQuaternionContinuity();clip.wrapMode=(motion.EndsWith("walk") || motion.EndsWith("run"))?WrapMode.Loop:WrapMode.ClampForever;
                string path=folder+"/"+clip.name+".anim";if(AssetDatabase.LoadAssetAtPath<AnimationClip>(path)!=null)AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(clip,path);
                report.Add("PASS: "+clip.name+" // "+targetBones.Count+" bones // "+frames+" frames");
                UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(target);
            }
        }
        AssetDatabase.SaveAssets();File.WriteAllLines("Logs/MixamoBake.txt",report);EditorApplication.Exit(0);
    }
}
