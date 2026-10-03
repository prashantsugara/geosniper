using System;

namespace GeoSniper
{
    public static class MapCachePolicy
    {
        // Reuse nearby GPS fixes, not a substantial move into another sector.
        // A query radius does not guarantee complete geometry around a shifted origin.
        public const double MaxReuseDistance = 40;
        public static bool CanReuse(double distance)
        {
            return !double.IsNaN(distance) && distance >= 0 && distance <= MaxReuseDistance;
        }
    }
}
