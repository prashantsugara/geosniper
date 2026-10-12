using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Render mapped white, neutral and terracotta roofs through the mission's actual environment.
public static class DaylightRoofChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly StringBuilder report = new StringBuilder();

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        if (!Application.dataPath.Replace('\\', '/').Contains("/.utmp/"))
            throw new Exception("Run daylight environment checks in an isolated .utmp Unity project.");
        report.Clear();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        var worldRoot = new GameObject("Roof material diagnostic");
        var world = worldRoot.AddComponent<SectorWorld>();
        var createRoof = typeof(SectorWorld).GetMethod("RoofStyled", Private);
        var trackedMaterials = (List<Material>)typeof(SectorWorld).GetField("materials", Private).GetValue(world);
        var materials = new List<Material>();
        try
        {
            var camera = new GameObject("Roof render camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.14f, .18f, .22f);
            camera.transform.position = new Vector3(0, 20, -18);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.aspect = 1280f / 720f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 200;
            camera.allowHDR = true;
            var post = camera.gameObject.AddComponent<MobilePostProcess>();
            post.vignetteIntensity = 0;
            Require(Shader.Find("GeoSniper/MobilePostProcess") != null, "Real postprocess shader is present");

            var light = new GameObject("Roof diagnostic sun").AddComponent<Light>();
            light.type = LightType.Directional;
            var mission = new GameObject("Roof environment driver").AddComponent<UrbanCombatMission>();
            mission.enabled = false;
            typeof(UrbanCombatMission).GetField("cameraView", Private).SetValue(mission, camera);

            string[] surfaces = { "tar", "slate", "roof_tiles", "metal" };
            Color[] tints = { new Color(.42f, .44f, .43f), Color.white, new Color(.55f, .27f, .17f), new Color(.48f, .54f, .58f) };
            var positions = new Vector3[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                var material = (Material)createRoof.Invoke(world, new object[] { tints[i], surfaces[i] });
                materials.Add(material);
                Require(Vector4.Distance(material.color, tints[i]) < .001f, surfaces[i] + " retains its mapped color");
                Require(material.mainTexture != null && material.GetTexture("_BumpMap") != null,
                    surfaces[i] + " retains albedo and normal detail");
                Require(material.GetFloat("_Glossiness") <= .25f, surfaces[i] + " does not have polished roof glare");
                Require(material.GetColor("_EmissionColor").maxColorComponent < .001f, surfaces[i] + " adds no artificial glow");
                positions[i] = new Vector3((i - 1.5f) * 6f, 0, 0);
                var roof = GameObject.CreatePrimitive(PrimitiveType.Plane);
                roof.name = surfaces[i] + " roof";
                roof.transform.position = positions[i];
                roof.transform.localScale = new Vector3(.58f, 1, .58f);
                roof.GetComponent<Renderer>().sharedMaterial = material;
            }
            var alias = (Material)createRoof.Invoke(world, new object[] { tints[2], " TILE " });
            Require(ReferenceEquals(alias, materials[2]), "Tile aliases share the same mapped material");

            ApplyEnvironment(mission, 0);
            // Keep diagnostics free of screen-edge darkening and atmospheric distance haze.
            post.vignetteIntensity = 0;
            RenderSettings.fog = false;
            var day = Capture(camera, "daylight-roofs");
            for (int i = 0; i < surfaces.Length; i++)
            {
                var statistics = Patch(day, camera.WorldToViewportPoint(positions[i]));
                report.AppendLine(surfaces[i] + " day luminance=" + statistics.Mean.ToString("F3")
                    + " variance=" + statistics.Variance.ToString("F6") + " white=" + statistics.WhiteFraction.ToString("F3"));
                Require(statistics.Mean > .10f && statistics.Mean < .92f, surfaces[i] + " daytime roof remains readable");
                Require(statistics.WhiteFraction < .10f, surfaces[i] + " does not become a clipped white patch");
                Require(statistics.Variance > .00002f, surfaces[i] + " visible roof texture survives grading");
            }

            ApplyEnvironment(mission, 1);
            post.vignetteIntensity = 0;
            RenderSettings.fog = false;
            var night = Capture(camera, "night-roofs");
            for (int i = 0; i < surfaces.Length; i++)
            {
                var daytime = Patch(day, camera.WorldToViewportPoint(positions[i]));
                var nighttime = Patch(night, camera.WorldToViewportPoint(positions[i]));
                Require(nighttime.Mean > .002f && nighttime.Mean < daytime.Mean * .65f,
                    surfaces[i] + " remains visible under the original night lighting");
                Require(Vector4.Distance(materials[i].color, tints[i]) < .001f,
                    surfaces[i] + " night mode keeps the original albedo");
            }
        }
        catch (Exception error)
        {
            report.AppendLine("FAIL: " + error);
            throw;
        }
        finally
        {
            // SectorWorld normally releases these during play; release explicitly in Edit mode.
            foreach (var material in trackedMaterials) if (material != null) Object.DestroyImmediate(material);
            trackedMaterials.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/DaylightRoofChecks.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }

    static void ApplyEnvironment(UrbanCombatMission mission, int state)
    {
        typeof(UrbanCombatMission).GetField("envState", Private).SetValue(mission, state);
        typeof(UrbanCombatMission).GetMethod("ApplyEnvironment", Private).Invoke(mission, null);
    }

    struct Statistics { public float Mean, Variance, WhiteFraction; }
    static Statistics Patch(Color32[] pixels, Vector3 viewport)
    {
        const int width = 1280, height = 720, radius = 30;
        int centerX = Mathf.RoundToInt(viewport.x * width), centerY = Mathf.RoundToInt(viewport.y * height);
        float sum = 0, squares = 0, white = 0, count = 0;
        for (int y = centerY - radius; y <= centerY + radius; y++)
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                var pixel = pixels[y * width + x];
                float luminance = (pixel.r * .2126f + pixel.g * .7152f + pixel.b * .0722f) / 255f;
                sum += luminance; squares += luminance * luminance; count++;
                if (luminance > .94f) white++;
            }
        float mean = sum / count;
        return new Statistics { Mean = mean, Variance = Mathf.Max(0, squares / count - mean * mean), WhiteFraction = white / count };
    }

    static Color32[] Capture(Camera camera, string name)
    {
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGBHalf);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
            return image.GetPixels32();
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        report.AppendLine("PASS: " + message);
    }
}
