using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using GeoSniper;
public static class EnemyCoverReview {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static GameObject Box(Vector3 pos,Vector3 scale){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.transform.position=pos;g.transform.localScale=scale;Physics.SyncTransforms();return g;}
 public static void Run(){try{
  var owner=new GameObject("Guard").transform;var player=new GameObject("Player").transform;player.position=new Vector3(0,0,10);
  var floor=Box(new Vector3(0,-.5f,0),new Vector3(30,1,30));var hits=new RaycastHit[16];
  Check(EnemyCoverGeometry.Reachable(Vector3.zero,new Vector3(4,0,0),owner,hits),"Flat clear route rejected");
  var wall=Box(new Vector3(2,1,0),new Vector3(.4f,2,3));
  Check(!EnemyCoverGeometry.Reachable(Vector3.zero,new Vector3(4,0,0),owner,hits),"Route through wall accepted");UnityEngine.Object.DestroyImmediate(wall);
  Check(!EnemyCoverGeometry.Protected(Vector3.zero,new Vector3(0,1.65f,10),owner,player),"Open ground marked cover");
  var cover=Box(new Vector3(0,.65f,3),new Vector3(4,1.3f,.5f));
  Check(EnemyCoverGeometry.Protected(Vector3.zero,new Vector3(0,1.65f,10),owner,player),"Solid cover rejected");
  cover.AddComponent<CombatActor>();
  Check(!EnemyCoverGeometry.Protected(Vector3.zero,new Vector3(0,1.65f,10),owner,player),"Actor used as cover");UnityEngine.Object.DestroyImmediate(cover);UnityEngine.Object.DestroyImmediate(floor);
  Box(new Vector3(0,-.5f,0),new Vector3(1.5f,1,3));Box(new Vector3(4,-.5f,0),new Vector3(1.5f,1,3));
  Check(!EnemyCoverGeometry.Reachable(Vector3.zero,new Vector3(4,0,0),owner,hits),"Roof gap accepted");
  File.WriteAllText("Logs/result.txt","PASS: flat route, wall rejection, exposed ground, solid cover, actor exclusion, roof-gap rejection.");EditorApplication.Exit(0);
 }catch(Exception e){File.WriteAllText("Logs/result.txt","FAIL: "+e);EditorApplication.Exit(1);}}
}
