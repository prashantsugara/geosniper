using System;

namespace GeoSniper
{
    public enum EnemyThreatReaction { None, Investigate, TakeCover }

    /// <summary>Bounded perception and shooting decisions, independent of scene state.</summary>
    public static class EnemyReactionPolicy
    {
        public const float NearPassRadius = 2.5f;
        public const float NearbyImpactRadius = 6f;
        public const float AudibleImpactRadius = 18f;

        // The supplied endpoint must be the actual first impact, never a ray continued
        // through a wall. Points before the muzzle or beyond that impact are excluded.
        public static float FlightDistance(float px, float py, float pz,
            float ax, float ay, float az, float bx, float by, float bz)
        {
            if (!Finite(px, py, pz) || !Finite(ax, ay, az) || !Finite(bx, by, bz)) return float.MaxValue;
            double dx = (double)bx - ax, dy = (double)by - ay, dz = (double)bz - az;
            double lengthSquared = dx * dx + dy * dy + dz * dz;
            if (lengthSquared < .0001) return float.MaxValue;
            double tx = (double)px - ax, ty = (double)py - ay, tz = (double)pz - az;
            double t = (tx * dx + ty * dy + tz * dz) / lengthSquared;
            if (t < 0 || t > 1) return float.MaxValue;
            tx -= dx * t; ty -= dy * t; tz -= dz * t;
            double distance = Math.Sqrt(tx * tx + ty * ty + tz * tz);
            return distance < float.MaxValue ? (float)distance : float.MaxValue;
        }

        public static EnemyThreatReaction ClassifyShotThreat(float flightDistance, float impactDistance, bool impactAudible = true)
        {
            bool nearPass = ValidDistance(flightDistance) && flightDistance <= NearPassRadius;
            bool nearImpact = ValidDistance(impactDistance) && impactDistance <= NearbyImpactRadius;
            if (nearPass || nearImpact) return EnemyThreatReaction.TakeCover;
            return impactAudible && ValidDistance(impactDistance) && impactDistance <= AudibleImpactRadius
                ? EnemyThreatReaction.Investigate : EnemyThreatReaction.None;
        }

        public static EnemyThreatReaction ClassifyGunshot(float distance, float noiseRadius, bool sourceVisible)
        {
            if (!ValidDistance(distance) || !ValidDistance(noiseRadius) || noiseRadius <= 0 || distance > noiseRadius)
                return EnemyThreatReaction.None;
            // Hearing a gun reports a place to investigate; it does not confer visual lock.
            return sourceVisible ? EnemyThreatReaction.TakeCover : EnemyThreatReaction.Investigate;
        }

        public static float AcquisitionDelay(float reactionScale, float distance, float random01, bool alreadyAlert = false)
        {
            float scale = float.IsFinite(reactionScale) ? Math.Clamp(reactionScale, .4f, 3f) : 1f;
            float range = ValidDistance(distance) ? Math.Clamp(distance / 120f, 0f, 1f) : 1f;
            float variation = float.IsFinite(random01) ? Math.Clamp(random01, 0f, 1f) : .5f;
            float delay = (.35f + .25f * variation + .18f * range) * scale;
            if (alreadyAlert) delay *= .7f;
            return Math.Clamp(delay, .18f, 1.8f);
        }

        public static bool CanFire(bool visible, bool alive, bool armed, bool reloading,
            float staggerSeconds, float suppressionSeconds, float acquisitionSeconds, float aimErrorDegrees, float maxAimAngle = 12f)
        {
            return visible && alive && armed && !reloading && Ready(staggerSeconds)
                && Ready(suppressionSeconds) && Ready(acquisitionSeconds)
                && float.IsFinite(aimErrorDegrees) && aimErrorDegrees >= 0
                && float.IsFinite(maxAimAngle) && maxAimAngle > 0
                && aimErrorDegrees <= Math.Clamp(maxAimAngle, 1f, 35f);
        }

        public static float SuppressionSeconds(float current, bool nearPass, float scale = 1f)
        {
            float remaining = float.IsFinite(current) ? Math.Clamp(current, 0f, 3.5f) : 0f;
            float boundedScale = float.IsFinite(scale) ? Math.Clamp(scale, .4f, 2f) : 1f;
            // A fresh threat refreshes the response instead of adding unbounded lockout.
            return Math.Max(remaining, Math.Clamp((nearPass ? 1.25f : .75f) * boundedScale, .3f, 2.5f));
        }

        public static float AccuracyMultiplier(float movementSpeed, float suppressionSeconds, float woundPenalty = 0f)
        {
            float movement = float.IsFinite(movementSpeed) ? Math.Clamp(movementSpeed / 5f, 0f, 1f) : 1f;
            float suppression = float.IsFinite(suppressionSeconds) ? Math.Clamp(suppressionSeconds / 2f, 0f, 1f) : 1f;
            float wound = float.IsFinite(woundPenalty) ? Math.Clamp(woundPenalty, 0f, .8f) : .8f;
            return Math.Clamp((1f - .55f * movement) * (1f - .5f * suppression) * (1f - wound), .1f, 1f);
        }

        static bool Finite(float x, float y, float z) => float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z);
        static bool ValidDistance(float value) => float.IsFinite(value) && value >= 0f;
        static bool Ready(float value) => float.IsFinite(value) && value <= 0f;
    }
}
