using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GeoSniper
{
    public sealed class GameLocation
    {
        public double Latitude, Longitude;
        public string Source, Label;
        public float AccuracyMeters;
        public static GameLocation Default => new GameLocation { Latitude=35.6762,Longitude=139.6503,Source="default",Label="TOKYO" };
    }
    public sealed class MapFeature
    {
        public string Kind,Name,Landmark;
        public float Height;
        public JObject Details=new JObject();
        public List<Vector2> Points=new List<Vector2>();
        public List<List<Vector2>> WaterRings=new List<List<Vector2>>();
    }
}
