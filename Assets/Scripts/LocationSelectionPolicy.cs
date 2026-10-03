namespace GeoSniper
{
    // Keep source intent separate from cached coordinates and map data.
    public static class LocationSelectionPolicy
    {
        public static bool RequiresLiveFix(string source) { return source == "gps"; }
        public static T Select<T>(bool requestLive, T selected, T current) where T : class
        {
            return requestLive ? null : selected ?? current;
        }
        public static bool AllowsSavedFallback(bool requestLive) { return !requestLive; }
    }
}
