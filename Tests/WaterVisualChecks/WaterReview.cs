using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GeoSniper;
public static class WaterReview {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static List<Vector2> Box(float x,float y,float w,float h)=>new List<Vector2>{new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h)};
 static float Area(List<Vector2> t){float sum=0;for(int i=0;i<t.Count;i+=3){var a=t[i+1]-t[i];var b=t[i+2]-t[i];sum+=Mathf.Abs(a.x*b.y-a.y*b.x)*.5f;}return sum;}
 static void Capture(Camera cam,string name){var rt=new RenderTexture(960,540,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var t=new Texture2D(960,540,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,960,540),0,0);t.Apply();File.WriteAllBytes("Logs/"+name+".png",t.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);}
 public static void Run(){try {
 var rings=new List<List<Vector2>>{Box(-30,-25,60,50),Box(-6,-5,12,10)};
 var triangles=WaterGeometry.Triangles(rings);Check(Mathf.Abs(Area(triangles)-2880)<.01f,"Island area");
 for(int i=0;i<triangles.Count;i+=3)Check(WaterGeometry.Contains(rings,(triangles[i]+triangles[i+1]+triangles[i+2])/3),"Triangle fills island");
 Check(!WaterGeometry.Contains(rings,Vector2.zero),"Island submerged");
 var clipped=WaterGeometry.Clip(Box(-1000,-1000,2000,2000));Check(Mathf.Abs(Area(WaterGeometry.Triangles(new List<List<Vector2>>{clipped}))-409600)<1,"Large lake clipping");
 var concave=new List<Vector2>{new Vector2(0,0),new Vector2(10,0),new Vector2(10,2),new Vector2(2,2),new Vector2(2,10),new Vector2(0,10)};
 Check(Mathf.Abs(Area(WaterGeometry.Triangles(new List<List<Vector2>>{concave}))-36)<.01f,"Concavity");
 var river=WaterGeometry.Ribbon(new List<Vector2>{new Vector2(-100,0),new Vector2(100,0)},10f);
 Check(Mathf.Abs(Area(WaterGeometry.Triangles(new List<List<Vector2>>{river}))-2000)<.01f,"Mapped river ribbon width");
 Check(WaterGeometry.Decode(WaterGeometry.Encode(rings)).Count==2,"Cache island roundtrip");
 bool rejected=false;try{WaterGeometry.Decode(WaterGeometry.Encode(new List<List<Vector2>>{Box(900,0,1,1)}));}catch(FormatException){rejected=true;}Check(rejected,"Malformed bounds accepted");
 var world=new GameObject("Water sector").AddComponent<SectorWorld>();world.Review(new List<MapFeature>{new MapFeature{Kind="water",Points=rings[0],WaterRings=rings}});
 Check(SectorWorld.WaterAt(new Vector3(20,0,0)),"Lake missed by placement mask");
 Check(!SectorWorld.WaterAt(Vector3.zero),"Dry island rejected by placement mask");
 Check(!SectorWorld.WaterAt(new Vector3(40,0,0)),"Dry shore marked as water");
 Check(world.ReviewGround(20,0)<.08f,"Lake bed above water");
 Check(world.ReviewGround(0,0)>.08f,"Island bank submerged");
 Check(world.ReviewGround(30.1f,0)>.08f,"Outer bank submerged");
 Check(Mathf.Abs(world.ReviewGround(42,0))<.01f,"Dry terrain never returns to elevation");
 var connected=new GameObject("Connected water sector").AddComponent<SectorWorld>();connected.transform.position=new Vector3(640,0,0);connected.BaseHeight=5;
 var identity=new Newtonsoft.Json.Linq.JObject{["water_id"]="fixture-lake"};
 var linked=new List<List<Vector2>>{Box(-30,-25,60,50)};
 var first=new GameObject("Linked first sector").AddComponent<SectorWorld>();
 first.Review(new List<MapFeature>{new MapFeature{Kind="water",Points=linked[0],WaterRings=linked,Details=identity}});
 connected.Review(new List<MapFeature>{new MapFeature{Kind="water",Points=linked[0],WaterRings=linked,Details=identity}});
 float firstY=first.GetComponentInChildren<MeshFilter>().sharedMesh.vertices[0].y;
 float nextY=connected.GetComponentInChildren<MeshFilter>().sharedMesh.vertices[0].y;
 Check(Mathf.Abs(firstY-nextY)<.001f,"Connected sectors have different water heights");
 Check(world.transform.Find("Shore foam")!=null,"Missing mapped shoreline foam");
 var foamShader=Resources.Load<Shader>("Shaders/WaterFoam");
 Check(foamShader!=null && foamShader.isSupported && !ShaderUtil.ShaderHasError(foamShader),"Foam shader failed");
 Check(MappedSurfaceTextures.Facade("brick",true)!=MappedSurfaceTextures.Facade("wood",true),"Facade variants missing");
 Check(MappedSurfaceTextures.Roof("metal")!=MappedSurfaceTextures.Roof("tile"),"Roof variants missing");
 Check(TrafficVehicle.SafeSpeed(2f)==0 && TrafficVehicle.SafeSpeed(20f)>TrafficVehicle.SafeSpeed(8f),"Traffic braking envelope");
 var terrainShader=Resources.Load<Shader>("Shaders/TerrainBlend");
 Check(terrainShader!=null && terrainShader.isSupported && !ShaderUtil.ShaderHasError(terrainShader),"Terrain blend shader unavailable");
 var textureGrass=new Texture2D(1,1);textureGrass.SetPixel(0,0,new Color(.25f,.42f,.18f));textureGrass.Apply();
 var textureSoil=new Texture2D(1,1);textureSoil.SetPixel(0,0,new Color(.48f,.32f,.18f));textureSoil.Apply();
 var textureStone=new Texture2D(1,1);textureStone.SetPixel(0,0,new Color(.55f,.55f,.54f));textureStone.Apply();
 var terrainMat=new Material(terrainShader);terrainMat.SetTexture("_GrassTex",textureGrass);terrainMat.SetTexture("_SoilTex",textureSoil);terrainMat.SetTexture("_ConcreteTex",textureStone);
 for(int n=0;n<3;n++) {
  var mesh=new Mesh();float left=n*12-18;
  mesh.vertices=new[]{new Vector3(left,0,65),new Vector3(left,0,75),new Vector3(left+10,0,75),new Vector3(left+10,0,65)};
  mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
  var blend=n==0?new Color(0,0,0):n==1?new Color(1,0,0):new Color(0,1,0);
  mesh.colors=new[]{blend,blend,blend,blend};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();
  var patch=new GameObject("Terrain material "+n);patch.AddComponent<MeshFilter>().sharedMesh=mesh;patch.AddComponent<MeshRenderer>().sharedMaterial=terrainMat;
 }
 var groundCamera=new GameObject("Terrain camera").AddComponent<Camera>();groundCamera.transform.position=new Vector3(0,30,70);groundCamera.transform.rotation=Quaternion.Euler(90,0,0);
 groundCamera.clearFlags=CameraClearFlags.SolidColor;groundCamera.backgroundColor=Color.black;groundCamera.orthographic=true;groundCamera.orthographicSize=15;
 var lightForGround=new GameObject("Ground sun").AddComponent<Light>();lightForGround.type=LightType.Directional;lightForGround.transform.rotation=Quaternion.Euler(45,30,0);
 Capture(groundCamera,"terrain-material-blend");
 var terrainMesh=new Mesh();var terrainVerts=new List<Vector3>();var terrainColors=new List<Color>();var terrainUvs=new List<Vector2>();var terrainIndices=new List<int>();
 for(int z=0;z<=40;z++)for(int x=0;x<=40;x++){
  float px=-50+x*2.5f,pz=-50+z*2.5f;
  terrainVerts.Add(new Vector3(px,world.ReviewGround(px,pz),pz));terrainColors.Add(new Color(world.ReviewShore(px,pz),0,0,1));terrainUvs.Add(new Vector2(px,pz)/24);
 }
 for(int z=0;z<40;z++)for(int x=0;x<40;x++){int i=z*41+x;terrainIndices.AddRange(new[]{i,i+41,i+42,i,i+42,i+1});}
 terrainMesh.SetVertices(terrainVerts);terrainMesh.SetColors(terrainColors);terrainMesh.SetUVs(0,terrainUvs);terrainMesh.SetTriangles(terrainIndices,0);terrainMesh.RecalculateNormals();
 var bank=new GameObject("Bank terrain");bank.AddComponent<MeshFilter>().sharedMesh=terrainMesh;bank.AddComponent<MeshRenderer>().sharedMaterial=terrainMat;

 var mapFeatures=new List<MapFeature>{
  new MapFeature{Kind="water",Points=rings[0],WaterRings=rings},
  new MapFeature{Kind="park",Points=Box(85,85,45,45)},
  new MapFeature{Kind="road",Points=new List<Vector2>{new Vector2(-90,90),new Vector2(90,90)}}};
 float plantingHeight;
 Check(!SectorScenery.CanPlant(new Vector2(20,0),mapFeatures,out plantingHeight),"Plants allowed in lake");
 Check(SectorScenery.CanPlant(Vector2.zero,mapFeatures,out plantingHeight),"Dry island cannot grow plants");
 Check(SectorScenery.CanPlant(new Vector2(105,105),mapFeatures,out plantingHeight) && plantingHeight>.1f,"Park planting lost");
 Check(!SectorScenery.CanPlant(new Vector2(0,90),mapFeatures,out plantingHeight),"Plants on road");
 var sceneryObject=new GameObject("Scenery");sceneryObject.transform.SetParent(world.transform,false);
 var scenery=sceneryObject.AddComponent<SectorScenery>();var job=scenery.Generate(mapFeatures,null,Vector2.zero);
 while(job.MoveNext()){}
 var detail=scenery.GetComponentsInChildren<SceneryDistanceCull>();Check(detail.Length>0,"No distance-cullable scenery");
 bool hasShrub=false,hasStone=false;
 foreach(var item in detail){hasShrub|=item.name=="Shrub patch";hasStone|=item.name=="Stone patch";}
 Check(hasShrub && hasStone,"Missing batched shrubs or stones");
 var water=world.GetComponentInChildren<MeshRenderer>();Check(water!=null,"No water surface");Check(water.sharedMaterial.shader.isSupported,"Unsupported shader");Check(!ShaderUtil.ShaderHasError(water.sharedMaterial.shader),"Shader compiler errors");
 var land=GameObject.CreatePrimitive(PrimitiveType.Cube);land.transform.position=new Vector3(0,-1,0);land.transform.localScale=new Vector3(100,1,100);land.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Standard")){color=new Color(.27f,.32f,.19f)};
 land.name="Local terrain";
 var facadeRules=new MapFeature{Kind="building",Height=12,Details=new Newtonsoft.Json.Linq.JObject{{"num_floors","4"}}};
 Check(MapFeatureStyle.StoreyHeight(facadeRules)==3,"Mapped storey height lost");
 var diagonalNormal=new Vector3(1,0,1).normalized;var wallAlong=Vector3.Cross(Vector3.up,diagonalNormal).normalized;
 Vector2 startUv=MapFeatureStyle.WallUv(facadeRules,Vector3.zero,diagonalNormal);
 Vector2 endUv=MapFeatureStyle.WallUv(facadeRules,wallAlong*8+Vector3.up*3,diagonalNormal);
 Check(Mathf.Abs(endUv.x-startUv.x-1)<.001f && Mathf.Abs(endUv.y-startUv.y-.5f)<.001f,"Diagonal facade or floor texture scale incorrect");
 var roadRules=new MapFeature{Kind="road",Details=new Newtonsoft.Json.Linq.JObject{{"width","6"},{"oneway","-1"}}};
 Check(MapFeatureStyle.TrafficDirection(roadRules)==-1,"Reverse one-way direction lost");
 Check(MapFeatureStyle.TrafficLaneOffset(roadRules)==0,"One-way lane not centered");
 roadRules.Details["oneway"]="no";roadRules.Details["driving_side"]="left";
 Check(MapFeatureStyle.TrafficDirection(roadRules)==0 && MapFeatureStyle.TrafficLaneOffset(roadRules)==-1.5f,"Left-side lane offset lost");
 roadRules.Details.Remove("oneway");roadRules.Details["junction"]="roundabout";
 Check(MapFeatureStyle.TrafficDirection(roadRules)==1,"Roundabout direction lost");
 var horizontalRoad=new MapFeature{Kind="road",Points=new List<Vector2>{new Vector2(-40,0),new Vector2(40,0)}};
 var verticalRoad=new MapFeature{Kind="road",Points=new List<Vector2>{new Vector2(0,-40),new Vector2(0,40)}};
 Check(!StreetPlacement.Clear(new List<MapFeature>{horizontalRoad,verticalRoad},horizontalRoad,new Vector2(0,5),Vector2.right,2.3f,4.8f),"Car placement blocked crossing road");
 var parkingBuilding=new MapFeature{Kind="building",Points=new List<Vector2>{new Vector2(10,4),new Vector2(20,4),new Vector2(20,10),new Vector2(10,10)}};
 Check(!StreetPlacement.Clear(new List<MapFeature>{parkingBuilding},horizontalRoad,new Vector2(12,5),Vector2.right,2.3f,4.8f),"Car placed inside building");
 Check(StreetPlacement.Clear(new List<MapFeature>{parkingBuilding},horizontalRoad,new Vector2(25,5),Vector2.right,2.3f,4.8f),"Clear roadside placement rejected");
 var roadMask=new RoadJunctionMask(new List<MapFeature>{horizontalRoad,verticalRoad});
 Check(roadMask.Occupied(new Vector2(0,3),horizontalRoad),"Curb would cross adjoining road");
 Check(!roadMask.Occupied(new Vector2(20,3),horizontalRoad),"Curb removed away from junction");
 verticalRoad.Details["level"]="1";Check(!roadMask.Occupied(new Vector2(0,3),horizontalRoad),"Overpass removed ground-level curb");verticalRoad.Details.Remove("level");
 var straightRoad=new MapFeature{Kind="road",Points=new List<Vector2>{new Vector2(40,0),new Vector2(80,0)}};
 Check(!new RoadJunctionMask(new List<MapFeature>{horizontalRoad,straightRoad}).Occupied(new Vector2(40,3),horizontalRoad),"Straight road split created curb gap");
 var restoredRules=MapFeatureStyle.ReadDetails(new Newtonsoft.Json.Linq.JObject{{"oneway","-1"},{"driving_side","left"},{"junction","roundabout"}});
 Check((string)restoredRules["oneway"]=="-1" && (string)restoredRules["driving_side"]=="left","Cached road rules lost");
 var routeA=new TrafficRoadRoutes.Road{Points=new List<Vector3>{new Vector3(0,0,0),new Vector3(0,0,20)},Feature=roadRules};
 var routeB=new TrafficRoadRoutes.Road{Points=new List<Vector3>{new Vector3(0,0,20),new Vector3(0,0,40)},Feature=roadRules};
 var routeC=new TrafficRoadRoutes.Road{Points=new List<Vector3>{new Vector3(0,5,40),new Vector3(0,5,60)},Feature=roadRules};
 var connectedRoute=new TrafficRoadRoutes(new List<TrafficRoadRoutes.Road>{routeA,routeB,routeC}).Build(routeA,0,1);
 Check(connectedRoute.Count==3 && connectedRoute[connectedRoute.Count-1].z==40,"Connected routing failed or joined elevated road");
 var prohibited=new TrafficRoadRoutes.Road{Points=new List<Vector3>{new Vector3(0,0,40),new Vector3(0,0,20)},Feature=roadRules};
 var blockedRoute=new TrafficRoadRoutes(new List<TrafficRoadRoutes.Road>{routeA,prohibited}).Build(routeA,0,1);
 Check(blockedRoute.Count==2,"Connected route entered one-way road backwards");
 var cross=new TrafficRoadRoutes.Road{Points=new List<Vector3>{new Vector3(-10,0,10),new Vector3(10,0,10)},Feature=roadRules};
 Check(new TrafficRoadRoutes(new List<TrafficRoadRoutes.Road>{routeA,cross}).Build(routeA,0,1).Count==2,"Invented junction at crossing lines");
 Check(TrafficVehicle.CrossingSpeed(new Vector3(0,0,-6),Vector3.forward,6,2,new Vector3(-6,0,0),Vector3.right,6,1)<6,"Side traffic did not trigger yielding");
 Check(float.IsPositiveInfinity(TrafficVehicle.CrossingSpeed(new Vector3(-10,0,0),Vector3.right,6,1,new Vector3(0,0,-10),Vector3.forward,6,2)),"Both crossing cars yielded");
 Check(float.IsPositiveInfinity(TrafficVehicle.CrossingSpeed(new Vector3(0,0,-10),Vector3.forward,6,2,new Vector3(-10,5,0),Vector3.right,6,1)),"Yielded to grade-separated road");
 Check(float.IsPositiveInfinity(TrafficVehicle.CrossingSpeed(new Vector3(0,0,-10),Vector3.forward,6,2,new Vector3(5,0,0),Vector3.right,6,1)),"Did not release after crossing cleared");
 var lampObject=new GameObject("Lamp regression");var lamps=lampObject.AddComponent<TrafficBrakeLights>();lamps.Configure(new Bounds(new Vector3(0,.75f,0),new Vector3(2,1.5f,4.5f)));
 var lampRenderer=lampObject.GetComponentInChildren<MeshRenderer>();var lampProperties=new MaterialPropertyBlock();
 lamps.SetState(true,true);lampRenderer.GetPropertyBlock(lampProperties);Check(lampProperties.GetColor("_EmissionColor").r>2,"Brake lamps did not brighten");
 lamps.SetState(false,false);lampRenderer.GetPropertyBlock(lampProperties);Check(lampProperties.GetColor("_EmissionColor").r==0,"Destroyed vehicle lamps still emit");
 Check(lampObject.GetComponentsInChildren<Collider>().Length==0,"Brake lamps added collision geometry");
 UnityEngine.Object.DestroyImmediate(lampObject);
 var trafficObject=new GameObject("Traffic regression");var traffic=trafficObject.AddComponent<TrafficVehicle>();
 Physics.SyncTransforms();traffic.Initialize(new List<Vector3>{new Vector3(40,0,-20),new Vector3(40,0,20)},0);
 Check(traffic.CurrentSpeed==0,"Traffic spawned at full speed");
 var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.transform.position=new Vector3(41.35f,.5f,0);barrier.transform.localScale=new Vector3(3,2,1);
 Physics.SyncTransforms();
 var simulate=typeof(TrafficVehicle).GetMethod("Simulate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 for(int i=0;i<150;i++)simulate.Invoke(traffic,new object[]{.02f});
 Check(traffic.transform.position.z>-19f && traffic.transform.position.z<-1f,"Traffic did not approach and stop before obstacle");
 typeof(TrafficVehicle).GetField("isFleeing",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(traffic,true);
 for(int i=0;i<150;i++)simulate.Invoke(traffic,new object[]{.02f});
 Check(traffic.transform.position.z<-1f,"Fleeing traffic crossed obstacle");
 UnityEngine.Object.DestroyImmediate(trafficObject);UnityEngine.Object.DestroyImmediate(barrier);
 var turnGround=GameObject.CreatePrimitive(PrimitiveType.Cube);turnGround.name="Local terrain";turnGround.transform.position=new Vector3(200,-1,0);turnGround.transform.localScale=new Vector3(100,1,100);
 var turnObject=new GameObject("Turn regression");var turning=turnObject.AddComponent<TrafficVehicle>();Physics.SyncTransforms();
 turning.Initialize(new List<Vector3>{new Vector3(200,0,-20),new Vector3(200,0,0),new Vector3(220,0,0)},0,1,0);
 float maxLaneError=0;
 for(int tick=0;tick<1000;tick++){
  simulate.Invoke(turning,new object[]{.02f});
  Vector3 point=turning.transform.position;
  float laneError=Mathf.Min(Vector3.Distance(new Vector3(point.x,0,point.z),new Vector3(200,0,Mathf.Clamp(point.z,-20,0))),Vector3.Distance(new Vector3(point.x,0,point.z),new Vector3(Mathf.Clamp(point.x,200,220),0,0)));
  maxLaneError=Mathf.Max(maxLaneError,laneError);
 }
 Check(turning.transform.position.x>216 && Mathf.Abs(turning.transform.position.z)<2,"Car failed to complete right-angle route");
 Check(maxLaneError<3.5f,"Car left road corridor during turn: "+maxLaneError);
 UnityEngine.Object.DestroyImmediate(turnObject);UnityEngine.Object.DestroyImmediate(turnGround);
 var island=GameObject.CreatePrimitive(PrimitiveType.Cube);island.transform.position=new Vector3(0,-.2f,0);island.transform.localScale=new Vector3(12,.6f,10);island.GetComponent<Renderer>().sharedMaterial=land.GetComponent<Renderer>().sharedMaterial;
 lightForGround.enabled=false;
 var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,30,0);sun.intensity=1.2f;RenderSettings.ambientLight=new Color(.35f,.4f,.45f);
 var cam=new GameObject("Camera").AddComponent<Camera>();cam.transform.position=new Vector3(42,28,-46);cam.transform.LookAt(Vector3.zero);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.5f,.65f,.75f);Capture(cam,"mapped-lake-island");Capture(cam,"shoreline-surface");
 QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=150;sun.shadows=LightShadows.Hard;
 var shadowPost=GameObject.CreatePrimitive(PrimitiveType.Cube);shadowPost.transform.position=new Vector3(12,5,3);shadowPost.transform.localScale=new Vector3(3,10,3);
 Capture(cam,"water-shadows");UnityEngine.Object.DestroyImmediate(shadowPost);
 var treePreview=new GameObject("Tree silhouette preview");
 var addTree=typeof(SectorScenery).GetMethod("AddTree",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
 var treeVertices=new List<Vector3>();var treeIndices=new List<int>();var treeUV=new List<Vector2>();
 var leaves=new List<Vector3>();var leafIndices=new List<int>();var leafUV=new List<Vector2>();var leafNormals=new List<Vector3>();
 for(int t=0;t<4;t++)addTree.Invoke(null,new object[]{new Vector3(120+t*7,0,120),4f,treeVertices,treeIndices,treeUV,leaves,leafIndices,leafUV,leafNormals});
 for(int part=0;part<2;part++){
  var holder=new GameObject(part==0?"Trunks":"Crowns");holder.transform.SetParent(treePreview.transform);
  var mesh=new Mesh();mesh.SetVertices(part==0?treeVertices:leaves);mesh.SetTriangles(part==0?treeIndices:leafIndices,0);mesh.SetUVs(0,part==0?treeUV:leafUV);
  if(part==0)mesh.RecalculateNormals();else mesh.SetNormals(leafNormals);
  holder.AddComponent<MeshFilter>().sharedMesh=mesh;
  holder.AddComponent<MeshRenderer>().sharedMaterial=new Material(Shader.Find("Standard")){color=part==0?new Color(.35f,.25f,.16f):new Color(.88f,.92f,.85f),mainTexture=part==0?SectorScenery.BuildBarkTexture():SectorScenery.BuildFoliageTexture()};
 }
 foreach(var previewRenderer in treePreview.GetComponentsInChildren<MeshRenderer>())previewRenderer.sharedMaterial.SetFloat("_Glossiness",.12f);
 var treeCamera=new GameObject("Tree camera").AddComponent<Camera>();treeCamera.transform.position=new Vector3(131,6,99);treeCamera.transform.LookAt(new Vector3(131,3,120));treeCamera.fieldOfView=65;
 Capture(treeCamera,"tree-silhouettes");UnityEngine.Object.DestroyImmediate(treePreview);UnityEngine.Object.DestroyImmediate(treeCamera.gameObject);
 Check(AssetCalibration.TryGet("audi.001(Clone)",out var variantCalibration) && variantCalibration.rotation.y==180,"Numbered car variant lost orientation calibration");
 var carPreview=new GameObject("Car asset preview");
 var carNames=new[]{"Cars/audi","Cars/audi.001","Cars/Ford","PoliceCar"};
 for(int carIndex=0;carIndex<carNames.Length;carIndex++){
  var template=Resources.Load<GameObject>("Models/"+carNames[carIndex]);Check(template!=null,"Missing car preview asset");
  var root=new GameObject("Preview car");root.transform.SetParent(carPreview.transform);root.transform.position=new Vector3(120+carIndex*6,0,150);
  var visual=UnityEngine.Object.Instantiate(template,root.transform,false);
  var body=ImportedVisual.AlignVehicle(root,visual,false,carIndex==3);visual.transform.localScale*=4.5f/Mathf.Max(body.size.x,body.size.z);
  body=ImportedVisual.LocalBounds(root.transform);visual.transform.localPosition-=new Vector3(body.center.x,body.min.y,body.center.z);body=ImportedVisual.LocalBounds(root.transform);
  var brake=root.AddComponent<TrafficBrakeLights>();brake.Configure(body,template.name);brake.SetState(true,true);
  var closeCamera=new GameObject("Car close camera").AddComponent<Camera>();closeCamera.fieldOfView=50;
  closeCamera.transform.position=root.transform.position+new Vector3(0,2,-6);closeCamera.transform.LookAt(root.transform.position+Vector3.up*.8f);Capture(closeCamera,"car-"+carIndex+"-rear");
  closeCamera.transform.position=root.transform.position+new Vector3(0,2,6);closeCamera.transform.LookAt(root.transform.position+Vector3.up*.8f);Capture(closeCamera,"car-"+carIndex+"-front");UnityEngine.Object.DestroyImmediate(closeCamera.gameObject);

 }
 var carCamera=new GameObject("Car rear camera").AddComponent<Camera>();carCamera.transform.position=new Vector3(129,5,135);carCamera.transform.LookAt(new Vector3(129,1,150));carCamera.fieldOfView=75;
 Capture(carCamera,"car-brake-fit");UnityEngine.Object.DestroyImmediate(carPreview);UnityEngine.Object.DestroyImmediate(carCamera.gameObject);
 var styles=new[]{"brick","stone","wood","metal","glass"};
 for(int i=0;i<styles.Length;i++){
  var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.transform.position=new Vector3(120+i*8,5,120);block.transform.localScale=new Vector3(6,10,4);
  var mat=new Material(Shader.Find("Standard")){color=new Color(.80f,.76f,.69f),mainTexture=MappedSurfaceTextures.Facade(styles[i],true)};
  mat.mainTextureScale=new Vector2(1,2);block.GetComponent<Renderer>().sharedMaterial=mat;
 }
 var facadeCamera=new GameObject("Facade camera").AddComponent<Camera>();facadeCamera.transform.position=new Vector3(136,9,88);facadeCamera.transform.LookAt(new Vector3(136,5,120));facadeCamera.fieldOfView=65;
 Capture(facadeCamera,"building-materials");
 File.WriteAllText("Logs/result.txt","PASS: large lake clipping, island cutout triangle area/containment, concavity, cache roundtrip, invalid bounds, dry island and shore placement mask, shoreline banks, connected height, terrain shader, scenery placement and distance culling, mapped facade and roof textures, normal and fleeing traffic obstacle braking, production water mesh and shader render.");EditorApplication.Exit(0);
 }catch(Exception e){File.WriteAllText("Logs/result.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}}
}
