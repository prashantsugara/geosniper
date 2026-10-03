using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeoSniper;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class MapWorldChecks
{
    [MenuItem("Geo Sniper/Validate Map Signs And Buildings")]
    public static void Run()
    {
        Directory.CreateDirectory("Logs");
        var report = new StringBuilder();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            string gradle = "android.buildFeatures {\n    prefab true\n}\n-DANDROID_STL=c++_static\n";
            string fixedGradle = GeoSniper.Editor.MapLibreGradleFix.ConfigureGradle(gradle + "android.buildFeatures { prefab false }");
            Check(fixedGradle.Contains("prefab true") && !fixedGradle.Contains("prefab false"), "Android export preserves Prefab for GameActivity and frame pacing", report);
            Check(fixedGradle.Contains("-DANDROID_STL=c++_shared") && !fixedGradle.Contains("c++_static"), "Android native runtime matches MapLibre shared STL", report);
            Check(GeoSniper.Editor.MapLibreGradleFix.ConfigureGradle(fixedGradle) == fixedGradle, "Android build configuration is idempotent", report);
            SceneManager.SetActiveScene(scene);
            var origin = new GameLocation { Latitude = 0, Longitude = 0 };
            Check(LocationResolver.UsableFix(18.5,73.9,6,1), "Fresh accurate GPS fix accepted", report);
            Check(!LocationResolver.UsableFix(18.5,73.9,6,120) && !LocationResolver.UsableFix(18.5,73.9,-1,1), "Stale or unknown-accuracy GPS fixes rejected", report);
            var elements = new JArray();
            elements.Add(Way("road", "Lane Number 12", new[] { new Vector2(-90, -20), new Vector2(-88, -20), new Vector2(-86, -20), new Vector2(90, -20) }));
            elements.Add(Way("building", "Atlas Hall", new[] { new Vector2(0, 0), new Vector2(12, 0), new Vector2(24, 0), new Vector2(24, 24), new Vector2(0, 24), new Vector2(0, 0) }));
            elements.Add(Way("building", "Boundary House", new[] { new Vector2(305, 0), new Vector2(325, 0), new Vector2(325, 20), new Vector2(305, 20), new Vector2(305, 0) }));
            elements.Add(Node("Shruti Mini Market", new Vector2(5, 5)));
            elements.Add(Node("Second Shop", new Vector2(15, 5)));
            elements.Add(Node("Standalone Kiosk", new Vector2(-35, 25)));
            var features = SectorMap.Parse(new JObject { ["elements"] = elements }.ToString(), origin);
            Check(features.Count(f => f.Kind == "building") == 2, "Boundary and collinear building footprints survive parsing", report);
            var facadeChoices=Enumerable.Range(0,8).Select(i=>SectorWorld.WorldFacadeColor(new Vector2(10000+i*35,10000))).Distinct().Count();
            Check(facadeChoices>=2,"Unmapped facades vary within a neighborhood",report);
            var atlas=features.First(f => f.Name == "Atlas Hall");
            atlas.Details["id"]="atlas-building";
            atlas.Details["has_parts"]="true";
            var annex=new MapFeature { Kind="building", Height=17,
                Details=new JObject { ["is_part"]="true", ["building_id"]="atlas-building", ["min_height"]="12" },
                Points=new List<Vector2> { new Vector2(4,4),new Vector2(10,4),new Vector2(10,10),new Vector2(4,10) } };
            features.Add(annex);
            var focused=WorldDetailPlanner.SelectBuildings(features,new Vector2(315,0),1,400);
            Check(focused.Count==1 && focused.Contains(features.First(f=>f.Name=="Boundary House")),
                "Building details follow sector entry point rather than feature order",report);
            Check(!WorldDetailPlanner.SelectBuildings(features,Vector2.zero,3,400).Contains(annex),
                "Building parts do not exhaust the facade detail budget",report);
            var dense = new JArray(elements);
            for (int i = 0; i < 60; i++) dense.Add(Node("Mapped Shop " + i, new Vector2(-150 + i * 2, 100)));
            Check(SectorMap.Parse(new JObject { ["elements"] = dense }.ToString(), origin).Count(f => f.Kind == "place") == 63, "More than 40 named places survive parsing", report);
            Check(SectorMap.ReadName(JObject.Parse("{\"name:en\":\"English Road Name\"}")) == "English Road Name", "Name fallback preserves the mapped name", report);
            var rows = new JArray();
            for (int i = 0; i < 400; i++) rows.Add(new JObject { ["kind"] = "road", ["name"] = "Road " + i, ["points"] = new JArray(new JArray(0, 0), new JArray(2, 0)) });
            Check(MapServiceClient.Decode(new JObject { ["version"] = 2, ["features"] = rows }.ToString()).Count == 400, "Dense sector cache decodes beyond the old 350-feature limit", report);

            var world = new GameObject("Validation sector").AddComponent<SectorWorld>();
            world.transform.position = new Vector3(10000, 0, 10000);
            world.Generate(features);
            foreach(var collider in world.GetComponentsInChildren<BoxCollider>())
                if(collider.name=="Parked Car")
                    Check(collider.bounds.size.x<8 && collider.bounds.size.y<4 && collider.bounds.size.z<8,
                        "Parked car collider stays vehicle-sized",report);
            Check(world.Buildings.Count == 2, "Parsed building footprints produce closed world meshes", report);
            var signs = world.GetComponentsInChildren<WorldSign>();
            var host = features.First(f => f.Name == "Atlas Hall");
            Check(world.GetComponentsInChildren<MeshFilter>().Any(filter=>filter.name=="building" &&
                filter.transform.parent==world.Buildings[host].transform && filter.GetComponent<MeshCollider>()!=null),
                "Overture building part attaches to its parent and keeps geometry",report);
            var lod=world.Buildings[host].GetComponent<LODGroup>();
            Check(lod!=null && lod.GetLODs().Length==2 && lod.GetLODs()[1].renderers.Length==1 &&
                world.Buildings[host].GetComponent<MeshCollider>()!=null,
                "Nearby architecture has a textured distant LOD with unchanged collision",report);
            var vertices = world.Buildings[host].GetComponent<MeshFilter>().sharedMesh.vertices;
            Check(vertices.All(v => host.Points.Any(p => Vector2.Distance(p,new Vector2(v.x,v.z)) < .001f)), "Building mesh keeps original OSM footprint vertices, without resizing or moving", report);
            Vector3 requestedGround=world.transform.position+new Vector3(-45,0,-80);
            Physics.SyncTransforms();
            foreach(var hit in Physics.RaycastAll(requestedGround+Vector3.up*300,Vector3.down,600,~0,QueryTriggerInteraction.Ignore))
            {
                report.AppendLine("Spawn surface: "+hit.collider.name+" y="+hit.point.y+" normal="+hit.normal);
                var candidate=hit.point+Vector3.up*.12f;
                foreach(var obstacle in Physics.OverlapCapsule(candidate+Vector3.up*.4f,candidate+Vector3.up*1.4f,.4f,~0,QueryTriggerInteraction.Ignore))
                    report.AppendLine("Spawn blocker: "+obstacle.name+" bounds="+obstacle.bounds);
            }
            Check(world.TryFindGeographicSpawn(requestedGround,out var groundSpawn) && Vector2.Distance(new Vector2(groundSpawn.x,groundSpawn.z),new Vector2(requestedGround.x,requestedGround.z))<.001f && groundSpawn.y<1, "Ground spawn keeps the exact requested geographic coordinate", report);
            Vector3 requestedRoof=world.transform.position+new Vector3(12,0,12);
            Check(world.TryFindGeographicSpawn(requestedRoof,out var roofSpawn) && Vector2.Distance(new Vector2(roofSpawn.x,roofSpawn.z),new Vector2(requestedRoof.x,requestedRoof.z))<.001f && roofSpawn.y>3, "Location inside a building uses its roof directly above that coordinate", report);
            var shop = signs.FirstOrDefault(s => s.Caption.Contains("Shruti Mini Market"));
            if (shop != null)
            {
                Check(shop.transform.parent == world.Buildings[host].transform, "Shop sign is attached to its containing OSM building", report);
                Check(shop.Caption.Contains("Atlas Hall") && shop.Caption.Contains("Second Shop"), "Building name and both tenants share one non-overlapping board", report);
                var wallPoint = world.transform.InverseTransformPoint(shop.transform.position);
                Check(!SectorSignage.Contains(new Vector2(wallPoint.x, wallPoint.z), host.Points), "Facade sign lies outside the building", report);
            }
            var standalone = signs.FirstOrDefault(s => s.Caption == "Standalone Kiosk");
            if (standalone != null)
            {
                Check(standalone.GetComponentsInChildren<Transform>().Count(t => t.name == "Sign support") == 2, "Unmatched OSM place has ground-supported sign", report);
            }
            var roadSign = signs.FirstOrDefault(s => s.Caption == "Lane Number 12");
            if (roadSign != null)
            {
                Check(roadSign.GetComponentsInChildren<TextMesh>().Length == 2, "Road sign has independent front and back faces", report);
            }
            foreach (var sign in signs)
            {
                sign.FitText();
                foreach (var text in sign.GetComponentsInChildren<TextMesh>())
                {
                    Check(Mathf.Abs(text.transform.localPosition.z) > WorldSign.Thickness / 2, "Text outside panel: " + sign.Caption.Replace('\n', '/'), report);
                    var bounds = text.GetComponent<Renderer>().localBounds;
                    Check(bounds.size.x * text.transform.localScale.x <= sign.PanelSize.x - .29f, "Text fits panel width", report);
                }
            }
            var camera = new GameObject("Validation camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.1f, .15f, .2f);
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 500;
            camera.fieldOfView = 45;
            var light = new GameObject("Validation sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(40, -35, 0);
            if (roadSign != null)
            {
                Render(camera, roadSign.transform.position + roadSign.transform.forward * 6, roadSign.transform.position, "RoadSignFront", report, true);
                Render(camera, roadSign.transform.position - roadSign.transform.forward * 6, roadSign.transform.position, "RoadSignBack", report, true);
            }
            if (shop != null)
            {
                Render(camera, shop.transform.position + shop.transform.forward * 12, shop.transform.position, "BuildingShopSign", report, true);
            }
            Render(camera,world.transform.position+new Vector3(42,20,-38),
                world.transform.position+new Vector3(14,8,8),"WorldDetail",report,false);
            report.AppendLine("PASS: all map/sign regression checks");
        }
        catch (Exception error) { report.AppendLine("FAIL: " + error); Debug.LogException(error); }
        finally
        {
            if(scene.IsValid()) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            File.WriteAllText("Logs/MapWorldChecks.txt", report.ToString());
        }
    }

    static JObject Way(string kind, string name, Vector2[] points)
    {
        var geometry = new JArray(points.Select(p => new JObject { ["lat"] = p.y / 111320.0, ["lon"] = p.x / 111320.0 }));
        return new JObject { ["type"] = "way", ["tags"] = new JObject { [kind == "road" ? "highway" : "building"] = "yes", ["name"] = name }, ["geometry"] = geometry };
    }
    static JObject Node(string name, Vector2 p) => new JObject { ["type"] = "node", ["lat"] = p.y / 111320.0, ["lon"] = p.x / 111320.0, ["tags"] = new JObject { ["shop"] = "convenience", ["name"] = name } };
    static void Check(bool value, string label, StringBuilder report)
    {
        if (!value) throw new Exception(label);
        report.AppendLine("PASS: " + label);
    }
    static void Render(Camera camera, Vector3 position, Vector3 target, string name, StringBuilder report, bool checkText)
    {
        camera.transform.position = position; camera.transform.LookAt(target);
        var texture = new RenderTexture(960, 540, 24);
        var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
            File.WriteAllBytes("Logs/" + name + ".png", pixels.EncodeToPNG());
            int ink = pixels.GetPixels().Count(c => c.r > .85f && c.g > .8f && c.b < .85f);
            if (checkText) Check(ink > 100, name + " actually renders readable light text (" + ink + " pixels)", report);
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels); }
    }
}
