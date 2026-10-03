using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using GeoSniper;
public static class WeatherReview {
 static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static Texture2D Capture(Camera cam,string name){var rt=new RenderTexture(960,540,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var t=new Texture2D(960,540,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,960,540),0,0);t.Apply();File.WriteAllBytes("Logs/"+name+".png",t.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);return t;}
 public static void Run(){
  try{
   var cam=new GameObject("Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.transform.position=new Vector3(0,2,0);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.15f,.2f,.27f);cam.fieldOfView=65;
   var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.position=new Vector3(0,-.5f,10);ground.transform.localScale=new Vector3(80,1,80);
   var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,20,0);
   WorldRainSystem.EnsureSystem(cam.transform);var system=WorldRainSystem.Instance;
   // Batch Edit mode does not automatically call Awake on a newly added runtime component.
   if(typeof(WorldRainSystem).GetField("_rainParticleSystem",flags).GetValue(system)==null) typeof(WorldRainSystem).GetMethod("Awake",flags).Invoke(system,null);
   var ps=(ParticleSystem)typeof(WorldRainSystem).GetField("_rainParticleSystem",flags).GetValue(system);
   system.SetRainActive(false);var dry=Capture(cam,"dry").GetPixels32();
   foreach(float density in new[]{.35f,1f}){
    MobileGraphics.WeatherDensity=density;system.SetRainActive(true,true);ps.Simulate(1f,true,true,true);
    Check(ps.particleCount>100,"No live rain particles");Check(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported,"Weather shader unsupported");
    var wet=Capture(cam,density<1?"rain-performance":"rain-high").GetPixels32();int changed=0;
    for(int i=0;i<wet.Length;i++) if(Math.Abs(wet[i].r-dry[i].r)+Math.Abs(wet[i].g-dry[i].g)+Math.Abs(wet[i].b-dry[i].b)>25)changed++;
    Check(changed>200,"Rain not visible in rendered image: "+changed);Debug.Log("VISIBLE RAIN density="+density+" particles="+ps.particleCount+" pixels="+changed);
    system.SetRainActive(false);Check(ps.particleCount==0,"Rain did not clear");
   }
   system.SetRainActive(true,true);typeof(WorldRainSystem).GetField("lightningUntil",flags).SetValue(system,Time.time+.32f);typeof(WorldRainSystem).GetField("nextLightning",flags).SetValue(system,Time.time+10);
   typeof(WorldRainSystem).GetMethod("LateUpdate",flags).Invoke(system,null);
   var flash=(Light)typeof(WorldRainSystem).GetField("stormLight",flags).GetValue(system);Check(flash.enabled && flash.intensity>0,"Storm flash absent");Capture(cam,"storm-flash");system.SetRainActive(false);Check(!flash.enabled,"Flash stuck on");
   File.WriteAllText("Logs/result.txt","PASS: rendered rain at Performance/High, particle clear, shader support, storm flash and clear.");EditorApplication.Exit(0);
  }catch(Exception e){File.WriteAllText("Logs/result.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
