using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CivilianAssetChecks
{
    static int frame;
    static readonly List<GameObject> actors=new List<GameObject>();
    static readonly List<Quaternion> initialLegs=new List<Quaternion>();
    static Camera camera;
    static CivilianAssetChecks(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/review-unity/"))throw new Exception("Use disposable review project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool("GeoSniper.CivilianChecks",true);EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Layer(Transform root){foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;}
    static void Tick()
    {
        if(!SessionState.GetBool("GeoSniper.CivilianChecks",false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        try
        {
            if(frame==0)
            {
                foreach(var game in UnityEngine.Object.FindObjectsByType<GeoSniperGame>(FindObjectsSortMode.None))game.gameObject.SetActive(false);
                RenderSettings.ambientLight=new Color(.65f,.65f,.65f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
                var sun=new GameObject("Civilian review key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.15f;sun.transform.rotation=Quaternion.Euler(35,-30,0);sun.cullingMask=1<<30;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Review floor";floor.layer=30;floor.transform.position=new Vector3(0,-.12f,0);floor.transform.localScale=new Vector3(20,.2f,12);
                floor.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Standard")){color=new Color(.18f,.22f,.25f)};
                var roles=new[]{CivilianRole.Witness,CivilianRole.Resident,CivilianRole.Doctor,CivilianRole.Reporter,CivilianRole.Shopkeeper};
                for(int i=0;i<roles.Length;i++)
                {
                    var actor=new GameObject(roles[i].ToString());actor.transform.position=new Vector3((i-2)*1.8f,0,0);
                    var asset=CivilianAppearance.Model(roles[i],0,"Mina");Check(asset!=null,"Missing "+roles[i]);
                    var visual=ImportedVisual.CreateEnemy(asset,actor.transform);
                    Check(CivilianAppearance.ApplySurfaces(visual.transform),"Missing surfaces "+roles[i]);
                    ImportedVisual.ConfigureCharacterSurfaces(visual.transform);
                    var bot=actor.AddComponent<CivilianBot>();bot.enabled=false;
                    var authored=actor.AddComponent<CivilianAnimation>();Check(authored.Initialize(bot,visual.transform),"Mixamo driver missing "+roles[i]);
                    var gait=actor.AddComponent<EnemyWalkAnimator>();gait.Initialize(bot,visual.transform);
                    Check(authored.Ready,"Authored driver unavailable: "+roles[i]);
                    CivilianAppearance.AddRoleDetails(actor.transform,visual.transform,roles[i]);Layer(actor.transform);
                    var bounds=ImportedVisual.PosedBounds(visual.transform);Check(bounds.size.y>1.5f && bounds.size.y<2.2f && Mathf.Abs(bounds.min.y)<.15f,"Bad scale or grounding "+roles[i]+": "+bounds);
                    foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {Check(skin.bones.Length>20,"Missing rig");foreach(var material in skin.sharedMaterials)Check(material!=null && material.mainTexture!=null,"Grey surface "+roles[i]);}
                    Transform leg=null;foreach(var bone in visual.GetComponentsInChildren<Transform>())if(bone.name.ToLowerInvariant().Replace("_","").Contains("leftthigh") || bone.name.Contains("LeftUpLeg")){leg=bone;break;}
                    Check(leg!=null,"Leg check missing");initialLegs.Add(leg.localRotation);actors.Add(actor);
                    var label=new GameObject("Label").AddComponent<TextMesh>();label.text=roles[i].ToString().ToUpperInvariant();label.anchor=TextAnchor.MiddleCenter;label.fontSize=40;label.characterSize=.042f;label.color=new Color(.7f,.87f,.97f);label.transform.position=actor.transform.position+new Vector3(0,2.22f,.1f);label.transform.rotation=Quaternion.Euler(0,180,0);label.gameObject.layer=30;
                }
                camera=new GameObject("Civilian review camera").AddComponent<Camera>();camera.cullingMask=1<<30;camera.transform.position=new Vector3(0,2.7f,10);camera.transform.LookAt(new Vector3(0,1.05f,0));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.075f,.10f);camera.orthographic=true;camera.orthographicSize=2.7f;
                Check(CivilianAppearance.Model(CivilianRole.Witness,99,"Protect Mina").name=="civilian_carla","Mina uses wrong witness model");
                Capture("CivilianLineup.png");
            }
            else if(frame<70)
            {
                foreach(var actor in actors)
                {
                    var bot=actor.GetComponent<CivilianBot>();typeof(CivilianBot).GetField("<CurrentSpeed>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(bot,2f);
                    actor.transform.position+=Vector3.forward*.005f;
                }
            }
            else if(frame==70)
            {
                for(int i=0;i<actors.Count;i++)
                {
                    Transform leg=null;foreach(var bone in actors[i].GetComponentsInChildren<Transform>())if(bone.name.ToLowerInvariant().Replace("_","").Contains("leftthigh") || bone.name.Contains("LeftUpLeg")){leg=bone;break;}
                    Check(Quaternion.Angle(initialLegs[i],leg.localRotation)>.05f,"Gait did not animate "+actors[i].name);
                }
                Capture("CivilianWalking.png");
                foreach(var actor in actors)actor.GetComponent<CivilianBot>().currentState=CivilianState.Cower;
            }
            else if(frame>95)
            {
                Capture("CivilianFear.png");
                foreach(var actor in actors)Check(actor.GetComponentInChildren<Animation>().IsPlaying("fear"),"Fear reaction missing");
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CivilianChecks.txt","PASS: five civilian roles load; textured skinned meshes; recognized gait rigs; grounded human scale; Mina has female witness model; Mixamo walks change leg poses; authored terrified clips play during cowering.\n");
                SessionState.SetBool("GeoSniper.CivilianChecks",false);EditorApplication.Exit(0);return;
            }
            frame++;
        }
        catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CivilianChecks.txt","FAIL: "+e);SessionState.SetBool("GeoSniper.CivilianChecks",false);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture(string name)
    {
        var rt=new RenderTexture(1600,800,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(1600,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,800),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name,image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);
    }
}

