namespace UnityEngine {
 public struct Vector3 { public Vector3(float x,float y,float z){} }
 public enum ShadowQuality { HardOnly, All }
 public enum ShadowResolution { Medium, High }
 public static class PlayerPrefs {
  static System.Collections.Generic.Dictionary<string,int> data=new System.Collections.Generic.Dictionary<string,int>();
  public static int GetInt(string k,int d){int v;return data.TryGetValue(k,out v)?v:d;}
  public static void SetInt(string k,int v){data[k]=v;}
  public static void Save(){}
 }
 public static class Application { public static bool isMobilePlatform=true,isEditor=false;public static int targetFrameRate; }
 public static class QualitySettings {
  public static int vSyncCount,shadowCascades,antiAliasing,pixelLightCount;
  public static float shadowDistance,shadowCascade2Split;
  public static Vector3 shadowCascade4Split;
  public static ShadowQuality shadows;public static ShadowResolution shadowResolution;
  public static bool softParticles,realtimeReflectionProbes;
 }
}
namespace GeoSniper {
 public class WorldRainSystem { public static WorldRainSystem Instance=new WorldRainSystem();public int refreshes;public void RefreshQuality(){refreshes++;} }
 public static class GraphicsChecks {
  static void Check(bool value,string message){if(!value)throw new System.Exception(message);}
  public static void Run(){
   MobileGraphics.Apply();
   Check(MobileGraphics.Selected==MobileGraphicsPreset.Balanced,"Fresh install default");
   MobileGraphics.SetFrameRate(60);
   MobileGraphics.Select(MobileGraphicsPreset.Performance);
   Check(UnityEngine.QualitySettings.shadowCascades==0 && UnityEngine.QualitySettings.shadowDistance==60,"Performance not applied");
   Check(UnityEngine.Application.targetFrameRate==60,"Preset overwrote FPS choice");
   UnityEngine.QualitySettings.shadowDistance=999;
   MobileGraphics.Apply();
   Check(UnityEngine.QualitySettings.shadowDistance==60,"Mission reapply lost saved preset");
   Check(MobileGraphics.WeatherDensity<.5f,"Rain not reduced");
   MobileGraphics.Select(MobileGraphicsPreset.High);
   Check(UnityEngine.QualitySettings.shadowCascades==4 && MobileGraphics.WeatherDensity==1,"High not restored");
   Check(WorldRainSystem.Instance.refreshes==2,"Existing rain not refreshed");
   UnityEngine.PlayerPrefs.SetInt("GeoSniper.GraphicsPreset",99);
   UnityEngine.PlayerPrefs.SetInt("GeoSniper.TargetFPS",999);
   MobileGraphics.Apply();
   Check(MobileGraphics.Selected==MobileGraphicsPreset.Balanced && UnityEngine.Application.targetFrameRate==30,"Invalid saved settings not recovered");
  }
 }
}
