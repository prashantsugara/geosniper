using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GeoSniper
{
    public static class CountryCapitalDatabase
    {
        public static System.Collections.Generic.List<GameLocation> All()
        {
            var result=new System.Collections.Generic.List<GameLocation>();
            var asset=Resources.Load<TextAsset>("CountryCapitals");
            if(asset==null) return result;
            foreach(var c in JArray.Parse(asset.text))
            {
                var ll=c["capitalInfo"]?["latlng"] as JArray;
                if(ll==null || ll.Count!=2) continue;
                double lat=(double)ll[0], lon=(double)ll[1];
                if(!LocationResolver.Valid(lat,lon)) continue;
                result.Add(new GameLocation { Latitude=lat, Longitude=lon, Source="selected",
                    Label=(string)c["capital"]?[0]+" / "+(string)c["name"]?["common"] });
            }
            result.Sort((a,b)=>string.Compare(a.Label,b.Label,StringComparison.OrdinalIgnoreCase));
            return result;
        }
        public static GameLocation Find(string code)
        {
            var asset = Resources.Load<TextAsset>("CountryCapitals");
            if (asset == null || string.IsNullOrEmpty(code)) return null;
            foreach (var c in JArray.Parse(asset.text))
            {
                if (!string.Equals((string)c["cca2"], code.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                var ll = c["capitalInfo"]?["latlng"] as JArray;
                if (ll == null || ll.Count != 2) return null;
                return new GameLocation { Latitude = (double)ll[0], Longitude = (double)ll[1], Source = "country_capital", Label = ((string)c["name"]?["common"] + " / " + (string)c["capital"]?[0]).ToUpperInvariant() };
            }
            return null;
        }
    }

    public sealed class LocationResolver
    {
        public IEnumerator Resolve(Action<string> status, Action<GameLocation> complete)
        {
            status("LOCATING PLAYER...");
            bool permitted = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            const string coarse = "android.permission.ACCESS_COARSE_LOCATION";
            const string fine = "android.permission.ACCESS_FINE_LOCATION";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(fine))
            {
                bool answered = false;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += permission => { if (permission == fine) answered = true; };
                callbacks.PermissionDenied += permission => { if (permission == fine) answered = true; };
                // Android 12+ requires both permissions in the same precise-location request.
                UnityEngine.Android.Permission.RequestUserPermissions(new[] { coarse, fine }, callbacks);
                float deadline = Time.realtimeSinceStartup + 20;
                while (!answered && Time.realtimeSinceStartup < deadline) yield return null;
            }
            permitted = UnityEngine.Android.Permission.HasUserAuthorizedPermission(fine)
                || UnityEngine.Android.Permission.HasUserAuthorizedPermission(coarse);
#elif UNITY_IOS && !UNITY_EDITOR
            permitted = true;
#endif
            if (permitted && Input.location.isEnabledByUser)
            {
                Input.location.Start(5, 1);
                float deadline = Time.realtimeSinceStartup + 18;
                float firstUsableFixAt = -1f;
                GameLocation best = null;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (Input.location.status == LocationServiceStatus.Failed) break;
                    if (Input.location.status == LocationServiceStatus.Running)
                    {
                        var gps = Input.location.lastData;
                        double age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 - gps.timestamp;
                        if (UsableFix(gps.latitude, gps.longitude, gps.horizontalAccuracy, age)
                            && (best == null || gps.horizontalAccuracy < best.AccuracyMeters))
                        {
                            if (firstUsableFixAt < 0f) firstUsableFixAt = Time.realtimeSinceStartup;
                            best = new GameLocation { Latitude = gps.latitude, Longitude = gps.longitude,
                                Source = "gps", AccuracyMeters = gps.horizontalAccuracy,
                                Label = "LOCAL SECTOR / GPS +/- " + Mathf.CeilToInt(gps.horizontalAccuracy) + " m" };
                            status(best.Label + "\nREFINING LOCATION...");
                            if (best.AccuracyMeters <= 10) break;
                        }
                        // A usable coarse fix is better than repeatedly
                        // holding the player on the loading screen for the
                        // full GPS timeout when precision stops improving.
                        if (best != null && best.AccuracyMeters <= 100
                            && Time.realtimeSinceStartup - firstUsableFixAt >= 4f) break;
                    }
                    yield return null;
                }
                Input.location.Stop();
                if (best != null && best.AccuracyMeters <= 100) { complete(best); yield break; }
            }
            status("ACCURATE LOCATION UNAVAILABLE\nEnable precise location and retry, or choose a place.");
            complete(null);
        }
        public static bool UsableFix(double lat, double lon, float accuracy, double ageSeconds) =>
            Valid(lat, lon) && accuracy > 0 && !float.IsInfinity(accuracy) && ageSeconds >= -5 && ageSeconds <= 30;
        public static bool Valid(double lat, double lon) => !double.IsNaN(lat) && !double.IsNaN(lon) && Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180;
    }
}
