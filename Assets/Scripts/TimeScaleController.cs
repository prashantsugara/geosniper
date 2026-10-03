using UnityEngine;

namespace GeoSniper
{
    /// <summary>
    /// Centralized controller for Time.timeScale and Time.fixedDeltaTime.
    /// Manages prioritized slow-mo channels (BulletCam > HitCam > BreathHold > Normal)
    /// to prevent slow-motion states from becoming permanently stuck.
    /// </summary>
    public static class TimeScaleController
    {
        const float DefaultFixedDelta = 0.02f;

        static float? _bulletCamScale;
        static float? _hitCamScale;
        static float? _breathHoldScale;

        public static bool IsSlowMoActive => _bulletCamScale.HasValue || _hitCamScale.HasValue || _breathHoldScale.HasValue;

        public static void SetBulletCam(float scale)
        {
            _bulletCamScale = scale;
            Apply();
        }

        public static void ClearBulletCam()
        {
            _bulletCamScale = null;
            Apply();
        }

        public static void SetHitCam(float scale)
        {
            _hitCamScale = scale;
            Apply();
        }

        public static void ClearHitCam()
        {
            _hitCamScale = null;
            Apply();
        }

        public static void SetBreathHold(float scale)
        {
            _breathHoldScale = scale;
            Apply();
        }

        public static void ClearBreathHold()
        {
            _breathHoldScale = null;
            Apply();
        }

        public static void ResetToNormal()
        {
            _bulletCamScale = null;
            _hitCamScale = null;
            _breathHoldScale = null;
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = DefaultFixedDelta;
        }

        static void Apply()
        {
            float target = 1.0f;
            if (_bulletCamScale.HasValue) target = _bulletCamScale.Value;
            else if (_hitCamScale.HasValue) target = _hitCamScale.Value;
            else if (_breathHoldScale.HasValue) target = _breathHoldScale.Value;

            Time.timeScale = target;
            Time.fixedDeltaTime = DefaultFixedDelta * target;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void OnDomainReload()
        {
            ResetToNormal();
        }
    }
}
