using UnityEngine;

namespace GeoSniper
{
    public static class MobileGraphics
    {
        const string Key = "GeoSniper.GraphicsPreset";
        public static MobileGraphicsPreset Selected => MobileQualityPolicy.Normalize(PlayerPrefs.GetInt(Key,1));
        public static float WeatherDensity => Application.isMobilePlatform || Application.isEditor ? MobileQualityPolicy.WeatherDensity(Selected) : 1f;
        public static void Select(MobileGraphicsPreset preset)
        {
            PlayerPrefs.SetInt(Key,(int)MobileQualityPolicy.Normalize((int)preset));
            PlayerPrefs.Save();
            Apply();
            WorldRainSystem.Instance?.RefreshQuality();
        }
        public static void SetFrameRate(int requested)
        {
            int fps=MobileQualityPolicy.FrameRate(requested);
            PlayerPrefs.SetInt("GeoSniper.TargetFPS",fps);
            PlayerPrefs.Save();
            Application.targetFrameRate=fps;
        }
        public static void Apply()
        {
            Application.targetFrameRate=MobileQualityPolicy.FrameRate(PlayerPrefs.GetInt("GeoSniper.TargetFPS",30));
            QualitySettings.shadowCascade4Split=new Vector3(.06f,.18f,.45f);
            if (!Application.isMobilePlatform && !Application.isEditor)
            {
                QualitySettings.shadowCascades=4;
                QualitySettings.shadowDistance=350;
                QualitySettings.antiAliasing=2;
                return;
            }
            var preset=Selected;
            QualitySettings.vSyncCount=0;
            QualitySettings.shadows=preset==MobileGraphicsPreset.Performance?ShadowQuality.HardOnly:ShadowQuality.All;
            QualitySettings.shadowResolution=preset==MobileGraphicsPreset.High?ShadowResolution.High:ShadowResolution.Medium;
            QualitySettings.shadowCascades=MobileQualityPolicy.Cascades(preset);
            QualitySettings.shadowCascade2Split=.3f;
            QualitySettings.shadowDistance=MobileQualityPolicy.ShadowDistance(preset);
            QualitySettings.antiAliasing=preset==MobileGraphicsPreset.Performance?0:2;
            QualitySettings.pixelLightCount=preset==MobileGraphicsPreset.High?4:preset==MobileGraphicsPreset.Performance?1:2;
            QualitySettings.softParticles=preset==MobileGraphicsPreset.High;
            QualitySettings.realtimeReflectionProbes=preset==MobileGraphicsPreset.High;
        }
    }
}
