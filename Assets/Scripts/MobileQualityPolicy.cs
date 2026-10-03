namespace GeoSniper
{
    public enum MobileGraphicsPreset { Performance, Balanced, High }
    public static class MobileQualityPolicy
    {
        public static MobileGraphicsPreset Normalize(int value) => value >= 0 && value <= 2 ? (MobileGraphicsPreset)value : MobileGraphicsPreset.Balanced;
        public static int Cascades(MobileGraphicsPreset preset) => preset == MobileGraphicsPreset.High ? 4 : preset == MobileGraphicsPreset.Performance ? 0 : 2;
        public static float ShadowDistance(MobileGraphicsPreset preset) => preset == MobileGraphicsPreset.High ? 200f : preset == MobileGraphicsPreset.Performance ? 60f : 110f;
        public static float WeatherDensity(MobileGraphicsPreset preset) => preset == MobileGraphicsPreset.High ? 1f : preset == MobileGraphicsPreset.Performance ? .35f : .65f;
        public static int FrameRate(int requested) => requested == 60 ? 60 : 30;
    }
}
