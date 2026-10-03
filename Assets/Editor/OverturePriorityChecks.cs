using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GeoSniper;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class OverturePriorityChecks
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in the isolated review project.");
        if (!MapServiceClient.UsesDirectOverture) throw new InvalidOperationException("Direct Overture is not configured.");
        const string release = "2026-09-23.0";
        var location = new GameLocation { Latitude = 12.123456, Longitude = 54.654321, Label = "CACHE PRIORITY TEST" };
        var sparse = new List<MapFeature> { Road(), Building(1) };
        var rich = new List<MapFeature> { Road() };
        for (int i = 0; i < 32; i++) rich.Add(Building(i));
        string key = "sector-" + SectorMapCache.Key(location) + ".json";
        string generic = Path.Combine(Application.persistentDataPath, "osm-sectors-v2", key);
        string overture = Path.Combine(Application.persistentDataPath, "overture-direct-sectors-v1", key);
        if (File.Exists(generic) || File.Exists(overture))
            throw new InvalidOperationException("Test cache key is already occupied; refusing to replace saved map data.");
        var overtureType = typeof(OvertureDirect);
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var releaseField = overtureType.GetField("release", flags);
        var nextField = overtureType.GetField("nextCheck", flags);
        var verifiedField = overtureType.GetField("verified", flags);
        object previousRelease = releaseField.GetValue(null), previousNext = nextField.GetValue(null), previousVerified = verifiedField.GetValue(null);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(generic));
            File.WriteAllText(generic, new JObject { ["version"] = 2,
                ["latitude"] = location.Latitude, ["longitude"] = location.Longitude,
                ["features"] = Rows(sparse) }.ToString());
            Directory.CreateDirectory(Path.GetDirectoryName(overture));
            File.WriteAllText(overture, new JObject { ["version"] = 2, ["styleVersion"] = 3,
                ["provider"] = "overture", ["release"] = release, ["features"] = Rows(rich) }.ToString());
            releaseField.SetValue(null, release);
            nextField.SetValue(null, DateTime.UtcNow.AddHours(1));
            verifiedField.SetValue(null, true);
            List<MapFeature> loaded = null;
            RunToCompletion(SectorMap.Load(location, "", (features, offline) => loaded = features));
            if (loaded == null || loaded.FindAll(f => f.Kind == "building").Count != 32 ||
                !SectorMap.LastSource.Contains("OVERTURE") || !SectorMap.LastSource.Contains("32 mapped buildings"))
                throw new InvalidOperationException("Sparse generic cache bypassed the denser Overture sector.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/OverturePriorityChecks.txt",
                "PASS: 32 cached Overture buildings preferred over one generic cached building; source and count shown\n");
            Debug.Log("Overture priority checks passed");
        }
        finally
        {
            releaseField.SetValue(null, previousRelease);
            nextField.SetValue(null, previousNext);
            verifiedField.SetValue(null, previousVerified);
            if (File.Exists(generic)) File.Delete(generic);
            if (File.Exists(overture)) File.Delete(overture);
        }
    }

    static MapFeature Road() => new MapFeature { Kind = "road", Height = 3,
        Points = new List<Vector2> { new Vector2(-40, 0), new Vector2(40, 0) } };
    static JArray Rows(List<MapFeature> features)
    {
        var rows = new JArray();
        foreach (var feature in features)
        {
            var points = new JArray();
            foreach (var p in feature.Points) points.Add(new JArray(p.x, p.y));
            rows.Add(new JObject { ["kind"] = feature.Kind, ["height"] = feature.Height,
                ["points"] = points, ["details"] = new JObject() });
        }
        return rows;
    }
    static MapFeature Building(int index)
    {
        float x = -90 + (index % 8) * 20, z = 12 + (index / 8) * 20;
        return new MapFeature { Kind = "building", Height = 12,
            Points = new List<Vector2> { new Vector2(x,z), new Vector2(x+12,z),
                new Vector2(x+12,z+12), new Vector2(x,z+12) } };
    }
    static void RunToCompletion(IEnumerator operation)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(operation);
        int steps = 0;
        while (stack.Count > 0)
        {
            if (++steps > 10000) throw new InvalidOperationException("Map load did not finish without a network request.");
            var step = stack.Peek();
            if (!step.MoveNext()) { (step as IDisposable)?.Dispose(); stack.Pop(); }
            else if (step.Current is IEnumerator nested) stack.Push(nested);
        }
    }
}
