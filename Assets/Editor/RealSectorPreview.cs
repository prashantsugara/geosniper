using System;
using System.IO;
using GeoSniper;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RealSectorPreview
{
 static readonly System.Collections.Generic.List<string> treeLodRows=new System.Collections.Generic.List<string>();
 public static void Run()
 {
  if(!Application.isBatchMode)throw new InvalidOperationException("Isolated batch only");
  Directory.CreateDirectory("Logs");
  try {
   treeLodRows.Clear();
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var features=MapServiceClient.Decode(File.ReadAllText("real-sector.json"));
   var heightSample=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("real-sector.json"));
   var heightRows=(Newtonsoft.Json.Linq.JArray)heightSample["features"];int sampleIndex=-1;
   for(int i=0;i<heightRows.Count;i++)if((string)heightRows[i]["kind"]=="building"){sampleIndex=i;break;}
   if(sampleIndex>=0){
    heightRows[sampleIndex]["height"]=150;var decoded=MapServiceClient.Decode(heightSample.ToString());
    if(decoded[sampleIndex].Height!=150)throw new Exception("Cached tower height was flattened");
    decoded[sampleIndex].Details["num_floors"]="50";
    if(Mathf.Abs(MapFeatureStyle.StoreyHeight(decoded[sampleIndex])-3)>.001f)throw new Exception("Tower floor spacing incorrect");
    heightRows[sampleIndex]["height"]=999;decoded=MapServiceClient.Decode(heightSample.ToString());
    if(decoded[sampleIndex].Height!=MapFeatureStyle.MaxBuildingHeight)throw new Exception("Height safety bound lost");
   }
   var timer=System.Diagnostics.Stopwatch.StartNew();
   var world=new GameObject("Cached real sector").AddComponent<SectorWorld>();world.Generate(features);timer.Stop();
   var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(45,-35,0);
   QualitySettings.shadows=UnityEngine.ShadowQuality.All;QualitySettings.shadowDistance=180;
   RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.48f,.58f,.72f);RenderSettings.ambientEquatorColor=new Color(.38f,.44f,.48f);RenderSettings.ambientGroundColor=new Color(.20f,.22f,.24f);
   RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=450f;RenderSettings.fogEndDistance=1800f;RenderSettings.fogColor=new Color(.68f,.78f,.88f);
   var camera=new GameObject("Sector camera").AddComponent<Camera>();camera.farClipPlane=1800;camera.fieldOfView=55;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.68f,.78f,.88f);camera.allowHDR=true;MobilePostProcess.Ensure(camera);
   AtmosphericSky.Create(camera);
   camera.transform.position=new Vector3(430,420,-430);camera.transform.LookAt(Vector3.zero);Capture(camera,"real-sector-overview");
   RenderSettings.fogStartDistance=60f;RenderSettings.fogEndDistance=550f;
   var road=features.Find(f=>f.Kind=="road" && f.Points.Count>1 && MapFeatureStyle.RoadWidth(f)>=6 && f.Points[0].sqrMagnitude<120*120);
   if(road!=null){var a=road.Points[0];var b=road.Points[1];var direction=(b-a).normalized;camera.transform.position=new Vector3(a.x,2,a.y);camera.transform.LookAt(camera.transform.position+new Vector3(direction.x,.03f,direction.y)*20);Capture(camera,"real-sector-street");}
   var woodland=features.Find(f=>f.Kind=="park" && (MapFeatureStyle.Text(f,"landuse")=="forest" || MapFeatureStyle.Text(f,"subtype")=="forest" || MapFeatureStyle.Text(f,"natural")=="wood"));
   if(woodland!=null){var center=SectorSignage.Center(woodland.Points);camera.transform.position=new Vector3(center.x,world.Ground(center.x,center.y)+1.8f,center.y);camera.transform.LookAt(camera.transform.position+new Vector3(.5f,.02f,1f)*30f);Capture(camera,"real-sector-woodland");}
   Material windowMaterial=null;foreach(var r in world.GetComponentsInChildren<MeshRenderer>())if(r.name=="Illuminated architectural windows" && r.sharedMaterials.Length>1){windowMaterial=r.sharedMaterials[1];break;}
   if(windowMaterial!=null && windowMaterial.GetColor("_EmissionColor").maxColorComponent>.01f)throw new Exception("Daytime windows remain emissive");
   SectorStreetLighting.SetNight(true,4f);SectorStreetLighting.AdvanceTransition(1f);
   if(Mathf.Abs(SectorStreetLighting.NightBlend-.25f)>.001f)throw new Exception("Dusk transition did not advance gradually");
   if(windowMaterial!=null && Mathf.Abs(windowMaterial.GetColor("_EmissionColor").r-.35f)>.001f)throw new Exception("Window emission did not follow dusk blend");
   SectorStreetLighting.SetNight(false,4f);SectorStreetLighting.AdvanceTransition(.5f);
   if(Mathf.Abs(SectorStreetLighting.NightBlend-.125f)>.001f)throw new Exception("Interrupted dusk transition snapped");
   SectorStreetLighting.SetNight(true);SectorStreetLighting.RefreshActiveLights(camera);
   if(windowMaterial!=null && windowMaterial.GetColor("_EmissionColor").r<1)throw new Exception("Night windows did not illuminate");
   sun.intensity=.12f;sun.color=new Color(.38f,.48f,.72f);RenderSettings.ambientSkyColor=new Color(.045f,.065f,.12f);RenderSettings.ambientEquatorColor=new Color(.03f,.045f,.08f);RenderSettings.ambientGroundColor=new Color(.015f,.02f,.035f);camera.backgroundColor=new Color(.025f,.04f,.075f);
   RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=30f;RenderSettings.fogEndDistance=350f;RenderSettings.fogColor=new Color(.025f,.04f,.075f);
   AtmosphericSky.Instance?.SetWeatherAtmosphere(0,false);
   Capture(camera,"real-sector-night");SectorStreetLighting.SetNight(false);
   long triangles=0;int meshes=0;foreach(var filter in world.GetComponentsInChildren<MeshFilter>()){if(filter.sharedMesh==null)continue;meshes++;triangles+=filter.sharedMesh.triangles.Length/3;}
   var breakdown=new System.Collections.Generic.Dictionary<string,int>();
   foreach(var renderer in world.GetComponentsInChildren<MeshRenderer>()){string name=renderer.gameObject.name;breakdown[name]=breakdown.TryGetValue(name,out int count)?count+1:1;}
   var rows=new System.Collections.Generic.List<string>();foreach(var item in breakdown)rows.Add(item.Key+": "+item.Value);rows.Sort();File.WriteAllLines("Logs/real-sector-renderers.txt",rows);
   File.WriteAllLines("Logs/real-sector-tree-lod.txt",treeLodRows);
   File.WriteAllText("Logs/real-sector-result.txt","PASS: production generator rendered cached sector. Features="+features.Count+", meshes="+meshes+", triangles="+triangles+", generationMs="+timer.ElapsedMilliseconds+". Flat elevation; desktop batch metrics are not Android profiling.");
   EditorApplication.Exit(0);
  } catch(Exception e){File.WriteAllText("Logs/real-sector-result.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
 }
 static void Capture(Camera camera,string name){AtmosphericSky.Instance?.SyncCamera(camera);foreach(var cull in UnityEngine.Object.FindObjectsByType<SceneryDistanceCull>(FindObjectsSortMode.None))cull.Refresh(camera);RecordTreeLod(name);var rt=new RenderTexture(1400,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1400,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,900),0,0);tex.Apply();File.WriteAllBytes("Logs/"+name+".png",tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
 static void RecordTreeLod(string name)
 {
  long near=0,far=0,allNear=0,allFar=0;int nearRenderers=0,farRenderers=0;
  foreach(var cull in UnityEngine.Object.FindObjectsByType<SceneryDistanceCull>(FindObjectsSortMode.None))
  {
   var filter=cull.GetComponent<MeshFilter>();var renderer=cull.GetComponent<MeshRenderer>();
   if(filter==null || filter.sharedMesh==null || renderer==null)continue;
   bool distant=renderer.gameObject.name.StartsWith("Distant tree",StringComparison.Ordinal);
   bool close=renderer.gameObject.name.Contains("tree canopy") || renderer.gameObject.name.Contains("tree trunks");
   if(!distant && !close)continue;
   long triangles=filter.sharedMesh.triangles.Length/3;
   if(distant){allFar+=triangles;if(renderer.enabled){far+=triangles;farRenderers++;}}
   else{allNear+=triangles;if(renderer.enabled){near+=triangles;nearRenderers++;}}
  }
  treeLodRows.Add(name+": enabled near="+near+" triangles/"+nearRenderers+" renderers, distant="+far+" triangles/"+farRenderers+" renderers; totals near="+allNear+", distant="+allFar+". Enabled bounds only; frustum and GPU cost excluded.");
 }
}
