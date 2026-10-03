using System;

namespace GeoSniper
{
    // Sensor rates are radians/second. Touch deltas are already frame displacements.
    public static class AndroidAimMath
    {
        public static float GyroDelta(float rate, float seconds, float sensitivity)
        {
            if (!float.IsFinite(rate) || !float.IsFinite(seconds) || !float.IsFinite(sensitivity)
                || seconds <= 0 || seconds > .1f || Math.Abs(rate) < .005f) return 0;
            return Math.Clamp(rate, -8f, 8f) * seconds * (180f / (float)Math.PI)
                * Math.Clamp(sensitivity, .2f, 3f);
        }
    }
}
