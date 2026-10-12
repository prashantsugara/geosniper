using System;
using UnityEngine;

namespace GeoSniper.Duel
{
    /// <summary>A pose recorded by the host, rather than a position supplied with a shot.</summary>
    public readonly struct DuelHistoricalPose
    {
        public readonly double Time;
        public readonly Vector3 Position;
        public readonly float Yaw, Pitch;
        public readonly bool Crouched, Alive;

        public DuelHistoricalPose(double time, Vector3 position, float yaw, float pitch, bool crouched, bool alive = true)
        {
            Time = time; Position = position; Yaw = yaw; Pitch = pitch; Crouched = crouched; Alive = alive;
        }

        // The first-person crouch eye is lower than the remote avatar's head hitbox.
        public Vector3 EyePosition => Position + new Vector3(0, Crouched ? .85f : 1.55f, 0);
    }

    /// <summary>Fixed-size pose history. Rewind requests cannot expose positions older than 250 ms.</summary>
    public sealed class DuelPoseHistory
    {
        public const float MaximumRewindSeconds = .25f;
        const int Capacity = 32;
        readonly DuelHistoricalPose[] poses = new DuelHistoricalPose[Capacity];
        int next, count;
        public int Count => count;

        public void Clear() { next = count = 0; }

        public bool Record(double time, Vector3 position, float yaw, float pitch, bool crouched, bool alive = true)
        {
            if (!DuelShotValidator.IsFinite(time) || time < 0 || !DuelShotValidator.IsFinite(position) ||
                !DuelShotValidator.IsFinite(yaw) || !DuelShotValidator.IsFinite(pitch)) return false;
            if (count > 0)
            {
                int last = (next + Capacity - 1) % Capacity;
                if (time < poses[last].Time) return false;
                if (time == poses[last].Time)
                {
                    poses[last] = new DuelHistoricalPose(time, position, yaw, pitch, crouched, alive);
                    return true;
                }
            }
            poses[next] = new DuelHistoricalPose(time, position, yaw, pitch, crouched, alive);
            next = (next + 1) % Capacity;
            count = Math.Min(count + 1, Capacity);
            return true;
        }

        public bool TrySample(double now, float rewindSeconds, out DuelHistoricalPose pose)
        {
            pose = default;
            if (count == 0 || !DuelShotValidator.IsFinite(now) || !DuelShotValidator.IsFinite(rewindSeconds)) return false;
            var newest = poses[(next + Capacity - 1) % Capacity];
            // Missing state updates must not turn into an indefinitely frozen, hittable player.
            if (now < newest.Time || now - newest.Time > MaximumRewindSeconds) return false;
            double requested = now - Math.Max(0, Math.Min(MaximumRewindSeconds, rewindSeconds));
            int first = (next + Capacity - count) % Capacity;
            var before = poses[first];
            if (requested <= before.Time) { pose = before; return true; }
            for (int i = 1; i < count; i++)
            {
                var after = poses[(first + i) % Capacity];
                if (requested <= after.Time)
                {
                    float fraction = (float)((requested - before.Time) / (after.Time - before.Time));
                    pose = new DuelHistoricalPose(requested,
                        before.Position + (after.Position - before.Position) * fraction,
                        LerpAngle(before.Yaw, after.Yaw, fraction), LerpAngle(before.Pitch, after.Pitch, fraction),
                        fraction < .5f ? before.Crouched : after.Crouched,
                        fraction < 1f ? before.Alive : after.Alive);
                    return true;
                }
                before = after;
            }
            pose = newest;
            return true;
        }

        static float LerpAngle(float from, float to, float fraction)
        {
            float delta = (to - from) % 360f;
            if (delta > 180) delta -= 360;
            if (delta < -180) delta += 360;
            return from + delta * fraction;
        }
    }

    public readonly struct DuelValidatedShot
    {
        public readonly int RoundId, ShotId, WeaponIndex;
        public readonly Vector3 Origin, Direction;
        public readonly double Time;
        public DuelValidatedShot(int roundId, int shotId, int weaponIndex, Vector3 origin, Vector3 direction, double time)
        { RoundId = roundId; ShotId = shotId; WeaponIndex = weaponIndex; Origin = origin; Direction = direction; Time = time; }
    }

    public readonly struct DuelValidatedHit
    {
        public readonly byte Zone;
        public readonly float Damage, Distance;
        public readonly Vector3 Point;
        public bool Headshot => Zone == 2;
        public DuelValidatedHit(byte zone, float damage, float distance, Vector3 point)
        { Zone = zone; Damage = damage; Distance = distance; Point = point; }
    }

    /// <summary>Host-owned ammunition, cadence and geometric hit validation. No peer-reported damage is used.</summary>
    public sealed class DuelShotValidator
    {
        public const float MaximumShotOriginDistance = 2.3f;
        public const float MaximumRange = 1200f;
        const double CadenceTolerance = .04;
        static readonly int[] MagazineSizes = { 5, 20, 30, 5, 10, 30, 30, 7 };
        static readonly double[] ShotIntervals = { .55, 1.05, .32, .85, .42, .12, .09, .38 };
        static readonly double[] ReloadDurations = { 2.2, 1.45, 1.1, 1.8, 1.65, 1.7, 1.35, 1.25 };
        readonly int[] ammunition = new int[8];
        int roundId, highestShotId, reloadWeapon = -1;
        double nextShotTime, lastShotTime, reloadEnd;

        public int RoundId => roundId;
        public int HighestShotId => highestShotId;

        public void ResetRound(int newRoundId)
        {
            roundId = newRoundId; highestShotId = 0; reloadWeapon = -1;
            nextShotTime = lastShotTime = double.NegativeInfinity; reloadEnd = 0;
            for (int i = 0; i < ammunition.Length; i++) ammunition[i] = MagazineSizes[i];
        }

        /// <summary>Moves an existing cadence/reload timeline to a synchronized clock without refilling magazines.</summary>
        public bool ShiftClock(double delta)
        {
            if (!IsFinite(delta) || delta < -180 || delta > 180) return false;
            if (IsFinite(nextShotTime)) nextShotTime += delta;
            if (IsFinite(lastShotTime)) lastShotTime += delta;
            if (reloadWeapon >= 0 && IsFinite(reloadEnd)) reloadEnd += delta;
            return true;
        }

        public int GetRemainingAmmo(int weaponIndex, double now)
        {
            CompleteReload(now);
            return ValidWeapon(weaponIndex) ? ammunition[weaponIndex] : 0;
        }

        public bool IsReloading(double now) { CompleteReload(now); return reloadWeapon >= 0; }

        public void GetReloadState(double now, out int weapon, out float remaining)
        {
            weapon = -1; remaining = 0;
            if (!IsFinite(now)) return;
            CompleteReload(now);
            if (reloadWeapon < 0) return;
            weapon = reloadWeapon;
            remaining = (float)Math.Max(0, Math.Min(ReloadDurations[reloadWeapon], reloadEnd - now));
        }

        public float GetShotCooldownRemaining(double now)
        {
            if (!IsFinite(now) || !IsFinite(nextShotTime)) return 0;
            return (float)Math.Max(0, Math.Min(1.05, nextShotTime - now));
        }

        public bool TryBeginReload(int round, int weaponIndex, double now)
        {
            if (round <= 0 || round != roundId || !ValidWeapon(weaponIndex) || !IsFinite(now) || now < 0) return false;
            CompleteReload(now);
            if (reloadWeapon >= 0 || now + CadenceTolerance < nextShotTime || ammunition[weaponIndex] >= MagazineSizes[weaponIndex]) return false;
            reloadWeapon = weaponIndex; reloadEnd = now + ReloadDurations[weaponIndex];
            return true;
        }

        // 'now' is the shot's bounded logical round time, so packets delivered in one reliable batch
        // retain their original cadence. The manager separately bounds it against the host round clock.
        public bool TryAccept(int round, int shotId, int weaponIndex, Vector3 origin, Vector3 direction,
            DuelHistoricalPose shooter, double now, out DuelValidatedShot shot, out string reason)
        {
            shot = default; reason = null;
            if (round <= 0 || round != roundId) { reason = "Wrong round"; return false; }
            if (shotId <= highestShotId) { reason = "Repeated shot"; return false; }
            // An invalid shot cannot be resubmitted later with the same identifier.
            highestShotId = shotId;
            if (!ValidWeapon(weaponIndex)) { reason = "Unknown rifle"; return false; }
            if (!IsFinite(now) || now < 0 || now < lastShotTime) { reason = "Invalid shot time"; return false; }
            if (!shooter.Alive || !IsFinite(shooter.Position)) { reason = "Shooter eliminated"; return false; }
            if (!IsFinite(origin) || !IsFinite(direction)) { reason = "Invalid shot ray"; return false; }
            float magnitudeSquared = Vector3.Dot(direction, direction);
            if (magnitudeSquared < .98f || magnitudeSquared > 1.02f) { reason = "Invalid shot direction"; return false; }
            Vector3 eyeOffset = origin - shooter.EyePosition;
            if (Vector3.Dot(eyeOffset, eyeOffset) > MaximumShotOriginDistance * MaximumShotOriginDistance)
            { reason = "Shot origin too far from player"; return false; }
            CompleteReload(now);
            if (reloadWeapon >= 0) { reason = "Reloading"; return false; }
            if (now + CadenceTolerance < nextShotTime) { reason = "Rifle not ready"; return false; }
            if (ammunition[weaponIndex] <= 0) { reason = "Magazine empty"; return false; }
            ammunition[weaponIndex]--;
            lastShotTime = now; nextShotTime = now + ShotIntervals[weaponIndex];
            shot = new DuelValidatedShot(round, shotId, weaponIndex, origin, direction / (float)Math.Sqrt(magnitudeSquared), now);
            return true;
        }

        void CompleteReload(double now)
        {
            if (reloadWeapon < 0 || !IsFinite(now) || now < reloadEnd) return;
            ammunition[reloadWeapon] = MagazineSizes[reloadWeapon]; reloadWeapon = -1;
        }

        public static float DamageFor(byte zone, int weaponIndex, float penetrationScale = 1f)
        {
            if (!ValidWeapon(weaponIndex) || zone > 2 || !IsFinite(penetrationScale)) return 0;
            float scale = Math.Max(0, Math.Min(1, penetrationScale));
            float[] damage={120,20,45,90,65,28,22,18}; float baseDamage=damage[weaponIndex];
            if(zone==2)return weaponIndex<5?100f:baseDamage*2f*scale;
            return baseDamage * (zone == 0 ? .6f : 1f) * scale;
        }

        public static float RangeFor(int weapon) => weapon==6?220:weapon==7?300:weapon==5?650:weapon==4?1000:weapon==3?1100:MaximumRange;
        public static bool TryIntersect(DuelValidatedShot shot, DuelHistoricalPose target, out DuelValidatedHit hit)
        {
            hit = default;
            if (!target.Alive || !ValidWeapon(shot.WeaponIndex) || !IsFinite(target.Position) || !IsFinite(shot.Origin) || !IsFinite(shot.Direction)) return false;
            float rayLengthSquared = Vector3.Dot(shot.Direction, shot.Direction);
            if (rayLengthSquared < .999f || rayLengthSquared > 1.001f) return false;
            var head = target.Position + new Vector3(0, target.Crouched ? .95f : 1.65f, 0);
            float headDistance = RaySphere(shot.Origin, shot.Direction, head, .24f);
            float center = target.Crouched ? .4f : .74f;
            float halfCylinder = target.Crouched ? .02f : .345f;
            float bodyDistance = RayCapsule(shot.Origin, shot.Direction, target.Position, center - halfCylinder, center + halfCylinder, .38f);
            bool isHead = headDistance <= bodyDistance;
            float distance = isHead ? headDistance : bodyDistance;
            if (!IsFinite(distance) || distance < 0 || distance > RangeFor(shot.WeaponIndex)) return false;
            byte zone = (byte)(isHead ? 2 : 1);
            hit = new DuelValidatedHit(zone, DamageFor(zone, shot.WeaponIndex), distance, shot.Origin + shot.Direction * distance);
            return true;
        }

        static float RaySphere(Vector3 origin, Vector3 direction, Vector3 center, float radius)
        {
            Vector3 offset = origin - center;
            double c = DotPrecise(offset, offset) - (double)radius * radius;
            if (c <= 0) return 0;
            double a = DotPrecise(direction, direction), b = DotPrecise(offset, direction);
            double discriminant = b * b - a * c;
            if (discriminant < 0) return float.PositiveInfinity;
            double distance = (-b - Math.Sqrt(discriminant)) / a;
            return distance >= 0 ? (float)distance : float.PositiveInfinity;
        }

        static float RayCapsule(Vector3 origin, Vector3 direction, Vector3 basePosition, float low, float high, float radius)
        {
            float distance = Math.Min(RaySphere(origin, direction, basePosition + new Vector3(0, low, 0), radius),
                RaySphere(origin, direction, basePosition + new Vector3(0, high, 0), radius));
            Vector3 offset = origin - basePosition;
            double c = (double)offset.x * offset.x + (double)offset.z * offset.z - (double)radius * radius;
            if (c <= 0 && offset.y >= low && offset.y <= high) return 0;
            double a = (double)direction.x * direction.x + (double)direction.z * direction.z;
            if (a < .000001f) return distance;
            double b = (double)offset.x * direction.x + (double)offset.z * direction.z;
            double discriminant = b * b - a * c;
            if (discriminant < 0) return distance;
            double root = Math.Sqrt(discriminant);
            double near = (-b - root) / a, far = (-b + root) / a;
            if (near >= 0 && offset.y + direction.y * near >= low && offset.y + direction.y * near <= high) distance = Math.Min(distance, (float)near);
            if (far >= 0 && offset.y + direction.y * far >= low && offset.y + direction.y * far <= high) distance = Math.Min(distance, (float)far);
            return distance;
        }

        static double DotPrecise(Vector3 a, Vector3 b) => (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z;

        static bool ValidWeapon(int weaponIndex) => weaponIndex >= 0 && weaponIndex < MagazineSizes.Length;
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
