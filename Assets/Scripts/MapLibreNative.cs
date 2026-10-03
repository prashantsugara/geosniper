using UnityEngine;

namespace GeoSniper
{
    // Thin bridge to the Android MapLibre overlay. The Unity map remains the fallback.
    public static class MapLibreNative
    {
        public static bool Open(GameLocation location, string callbackObject, float heading, GameLocation worldOrigin=null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var bridge=new AndroidJavaClass("com.geosniper.map.MapLibreBridge"))
                {
                    string style=worldOrigin==null?"":StreetMapStyle.Loaded(worldOrigin);
                    return bridge.CallStatic<bool>("open", location.Latitude, location.Longitude, callbackObject, heading,style);
                }
            }
            catch(System.Exception error)
            {
                Debug.LogWarning("MapLibre unavailable: "+error.GetType().Name);
            }
#endif
            return false;
        }

        public static void Close()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var bridge=new AndroidJavaClass("com.geosniper.map.MapLibreBridge")) bridge.CallStatic("close");
            }
            catch(System.Exception error) { Debug.LogWarning("MapLibre close failed: "+error.GetType().Name); }
#endif
        }
    }
}
