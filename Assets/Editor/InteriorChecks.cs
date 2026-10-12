using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeoSniper;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
public static class InteriorChecks
{
    public static void Run()
    {
        if(!Application.isBatchMode)throw new Exception("Run InteriorChecks in an isolated batch-mode project.");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Interior validation");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        root.transform.position=new Vector3(10000,0,10000);
        var world=root.AddComponent<SectorWorld>();world.BuildNavigation=false;world.DetailFocus=root.transform.position;
        var building=new MapFeature{Kind="building",Height=9,Details=new JObject{["id"]="interior-test"},Points=new List<Vector2>{new Vector2(-7,6),new Vector2(7,6),new Vector2(7,18),new Vector2(-7,18)}};
        var features=new List<MapFeature>{new MapFeature{Kind="road",Details=new JObject{["class"]="residential"},Points=new List<Vector2>{new Vector2(-80,0),new Vector2(80,0)}},building};
        try
        {
            world.Generate(features);Physics.SyncTransforms();
            var interior=root.GetComponentInChildren<EnterableBuilding>();Check(interior!=null,"No playable interior");Check(interior.Floors==3,"Upper floors missing");
            Check(!world.Buildings[building].GetComponent<MeshCollider>().enabled,"Sealed shell still blocks interiors");
            Check(world.Buildings[building].GetComponentsInChildren<Transform>().All(t=>t.name!="Solid wall"),"Old wall blocker remains");
            // A standing capsule can cross the door; wall flanks still block movement.
            Vector3 outward=new Vector3(interior.Entrance.x,0,interior.Entrance.z).normalized;
            for(int step=-6;step<=6;step++)
            {
                Vector3 foot=interior.transform.TransformPoint(interior.Entrance+outward*step*.15f+Vector3.up*.06f);
                Check(!Physics.CheckCapsule(foot+Vector3.up*.4f,foot+Vector3.up*1.4f,.32f,~0,QueryTriggerInteraction.Ignore),"Doorway blocks standing player");
            }
            var window=interior.FiringWindows[0];
            Vector3 normal=new Vector3(window.x,0,window.z);normal=Mathf.Abs(window.x)>6.8f?new Vector3(Mathf.Sign(window.x),0,0):new Vector3(0,0,Mathf.Sign(window.z));
            Vector3 direction=interior.transform.TransformDirection(normal);
            Vector3 origin=interior.transform.TransformPoint(window-normal*.7f);
            var target=GameObject.CreatePrimitive(PrimitiveType.Cube);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(target,scene);target.name="Exterior target";target.transform.position=origin+direction*8;target.transform.localScale=Vector3.one;
            Physics.SyncTransforms();bool shot=Physics.Raycast(origin,direction,out var hit,10);Check(shot && hit.collider.gameObject==target,"Window blocks outgoing shot: "+(shot?hit.collider.name+" at "+hit.distance:"no hit"));
            Check(SniperHitQuery.TryCast(new Ray(origin,direction),null,null,null,out var sniperHit,10) && sniperHit.Collider.gameObject==target,"Gameplay shot cannot pass through the window");
            Vector3 sill=interior.transform.TransformPoint(window-normal*.7f-Vector3.up*1.1f);
            Check(Physics.Raycast(sill,direction,out var blocked,2) && blocked.collider.transform.IsChildOf(interior.transform),"Solid sill does not stop shots");
            Check(!SniperHitQuery.IsPenetrable(blocked.collider),"Masonry incorrectly allows bullet penetration");
            var stairs=interior.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Interior stairs").ToArray();Check(stairs.Length>30,"Stairs missing");
            foreach(var stair in stairs)Check(stair.size.y*stair.transform.lossyScale.y>0,"Invalid stair collider");
            var walker=new GameObject("Standing player probe");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(walker,scene);
            var body=walker.AddComponent<CharacterController>();body.height=1.8f;body.radius=.32f;body.center=Vector3.up*.9f;body.stepOffset=.45f;body.slopeLimit=50;body.skinWidth=.03f;
            walker.transform.position=interior.transform.TransformPoint(interior.StairOrigin+new Vector3(0,.03f,-2.9f));Physics.SyncTransforms();
            for(int move=0;move<85;move++)body.Move(interior.transform.TransformDirection(Vector3.forward)*.07f+Vector3.down*.04f);
            Check(interior.transform.InverseTransformPoint(walker.transform.position).y>2.8f,"Standing player cannot climb stairs");
            for(int level=1;level<3;level++){walker.transform.position=interior.transform.TransformPoint(interior.StairOrigin+new Vector3(0,level*3+.03f,-2.9f));Physics.SyncTransforms();for(int move=0;move<85;move++)body.Move(interior.transform.TransformDirection(Vector3.forward)*.07f+Vector3.down*.04f);Check(interior.transform.InverseTransformPoint(walker.transform.position).y>(level+1)*3-.2f,"Upper stair flight blocks player");}
            walker.transform.position=interior.transform.TransformPoint(interior.Entrance+outward*1.1f+Vector3.down*.32f);Physics.SyncTransforms();
            for(int move=0;move<35;move++)body.Move(interior.transform.TransformDirection(-outward)*.07f+Vector3.down*.04f);
            Check(Vector3.Dot(interior.transform.InverseTransformPoint(walker.transform.position)-interior.Entrance,outward)<-.8f,"Player cannot walk through exterior doorway");
            Object.DestroyImmediate(walker);
            foreach(string name in new[]{"HouseInteriorSF","HouseLayoutCG"}){var asset=Resources.Load<GameObject>("Models/Buildings/"+name);Check(asset!=null,"Downloaded house missing "+name);Check(asset.GetComponentsInChildren<MeshRenderer>().Length==1,"House mesh was not consolidated "+name);Check(asset.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount)>100,"House geometry missing "+name);}
            var camera=new GameObject("Interior camera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.64f,.74f,.81f);camera.nearClipPlane=.05f;
            camera.transform.position=interior.transform.TransformPoint(new Vector3(-3,1.7f,-3));camera.transform.LookAt(interior.transform.TransformPoint(new Vector3(0,1.7f,-7)));
            var light=new GameObject("Daylight").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(40,-30,0);
            var targetTexture=RenderTexture.GetTemporary(1280,720,24);var previous=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=targetTexture;camera.Render();RenderTexture.active=targetTexture;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/InteriorWindow.png",image.EncodeToPNG());
                camera.transform.position=interior.transform.TransformPoint(new Vector3(2,1.7f,-4));camera.transform.LookAt(interior.transform.TransformPoint(new Vector3(5,2.5f,0)));camera.Render();image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/InteriorStairs.png",image.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(targetTexture);Object.DestroyImmediate(image);}
            File.WriteAllText("Logs/InteriorChecks.txt","PASS: three floors, standing doorway clearance, open-window shots, blocked wall/sill shots, stairs and downloaded house assets.");
        }
        finally{EditorSceneManager.CloseScene(scene,true);}
        StreetBlockChecks.Run();
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
