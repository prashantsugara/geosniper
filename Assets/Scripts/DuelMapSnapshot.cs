using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GeoSniper.Duel
{
    // Send the host's actual map, not independently downloaded/cache-dependent geometry.
    public sealed class DuelMapSnapshot
    {
        public GameLocation Location;
        public string Source;
        public List<MapFeature> Features;
        public bool ElevationAvailable;
        public float[] Heights;
        const int MaxPlainBytes = 32 * 1024 * 1024;

        public byte[] Encode()
        {
            using (var plain = new MemoryStream())
            {
                using (var writer = new BinaryWriter(plain, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(1);
                    writer.Write(Location.Latitude); writer.Write(Location.Longitude);
                    WriteText(writer, Location.Label); WriteText(writer, Location.Source); WriteText(writer, Source);
                    writer.Write(ElevationAvailable);
                    writer.Write(Heights.Length);
                    foreach (float height in Heights) writer.Write(height);
                    writer.Write(Features.Count);
                    foreach (var feature in Features)
                    {
                        WriteText(writer, feature.Kind); WriteText(writer, feature.Name); WriteText(writer, feature.Landmark);
                        writer.Write(feature.Height);
                        WriteText(writer, feature.Details?.ToString(Formatting.None));
                        WritePoints(writer, feature.Points);
                        writer.Write(feature.WaterRings.Count);
                        foreach (var ring in feature.WaterRings) WritePoints(writer, ring);
                    }
                }
                if (plain.Length > MaxPlainBytes) throw new InvalidDataException("Selected duel map is too large.");
                using (var compressed = new MemoryStream())
                {
                    using (var gzip = new GZipStream(compressed, System.IO.Compression.CompressionLevel.Fastest, true))
                    { var data = plain.ToArray(); gzip.Write(data, 0, data.Length); }
                    var result = compressed.ToArray();
                    if (result.Length > SniperDuelNetwork.MaxMapBytes) throw new InvalidDataException("Selected duel map is too large to share.");
                    return result;
                }
            }
        }

        public static DuelMapSnapshot Decode(byte[] data)
        {
            if (data == null || data.Length > SniperDuelNetwork.MaxMapBytes) throw new InvalidDataException("Invalid duel map size.");
            using (var plain = new MemoryStream())
            {
                using (var input = new MemoryStream(data))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                {
                    var buffer = new byte[8192]; int length;
                    while ((length = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (plain.Length + length > MaxPlainBytes) throw new InvalidDataException("Duel map exceeds size limit.");
                        plain.Write(buffer, 0, length);
                    }
                }
                plain.Position = 0;
                using (var reader = new BinaryReader(plain))
                {
                    if (reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported duel map format.");
                    var result = new DuelMapSnapshot { Location = new GameLocation() };
                    result.Location.Latitude = reader.ReadDouble(); result.Location.Longitude = reader.ReadDouble();
                    if (!double.IsFinite(result.Location.Latitude) || Math.Abs(result.Location.Latitude) > 90
                        || !double.IsFinite(result.Location.Longitude) || Math.Abs(result.Location.Longitude) > 180)
                        throw new InvalidDataException("Invalid host location.");
                    result.Location.Label = ReadText(reader, 512); result.Location.Source = ReadText(reader, 128);
                    result.Source = ReadText(reader, 1024);
                    result.ElevationAvailable = reader.ReadBoolean();
                    int heights = Count(reader, 4225);
                    if (heights != 4225) throw new InvalidDataException("Invalid terrain grid.");
                    result.Heights = new float[heights];
                    for (int i = 0; i < heights; i++) result.Heights[i] = Number(reader);
                    int count = Count(reader, 50000);
                    result.Features = new List<MapFeature>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var feature = new MapFeature { Kind = ReadText(reader, 256), Name = ReadText(reader, 4096), Landmark = ReadText(reader, 4096), Height = Number(reader) };
                        string details = ReadText(reader, 65536);
                        feature.Details = details == null ? new JObject() : JObject.Parse(details);
                        feature.Points = ReadPoints(reader);
                        int rings = Count(reader, 2048);
                        for (int r = 0; r < rings; r++) feature.WaterRings.Add(ReadPoints(reader));
                        result.Features.Add(feature);
                    }
                    if (plain.Position != plain.Length) throw new InvalidDataException("Unexpected duel map data.");
                    return result;
                }
            }
        }

        static void WriteText(BinaryWriter writer, string text) { writer.Write(text != null); if (text != null) writer.Write(text); }
        static string ReadText(BinaryReader reader, int limit)
        {
            if (!reader.ReadBoolean()) return null;
            string value = reader.ReadString();
            if (value.Length > limit) throw new InvalidDataException("Duel map text exceeds limit.");
            return value;
        }
        static int Count(BinaryReader reader, int limit)
        { int count = reader.ReadInt32(); if (count < 0 || count > limit) throw new InvalidDataException("Invalid duel map count."); return count; }
        static float Number(BinaryReader reader)
        { float value = reader.ReadSingle(); if (!float.IsFinite(value)) throw new InvalidDataException("Invalid duel map coordinate."); return value; }
        static void WritePoints(BinaryWriter writer, List<Vector2> points)
        { writer.Write(points.Count); foreach (var point in points) { writer.Write(point.x); writer.Write(point.y); } }
        static List<Vector2> ReadPoints(BinaryReader reader)
        {
            int count = Count(reader, 500000); var result = new List<Vector2>(count);
            for (int i = 0; i < count; i++) result.Add(new Vector2(Number(reader), Number(reader)));
            return result;
        }
    }
}
