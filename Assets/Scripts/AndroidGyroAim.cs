using UnityEngine;

namespace GeoSniper
{
    // Relative rate aiming never snaps the camera to the phone's absolute attitude.
    public sealed class AndroidGyroAim : MonoBehaviour
    {
        bool ownsSensor, previousEnabled, previousCompensation;
        bool paused, focused = true;
        float previousInterval, readyAt;
        public static bool Available => Application.platform == RuntimePlatform.Android && SystemInfo.supportsGyroscope;

        public Vector2 Read(bool scoped, bool blocked)
        {
            if (!Available || !scoped || blocked || paused || !focused || Time.timeScale <= 0
                || PlayerPrefs.GetInt("GeoSniper.GyroAim", 0) != 1)
            {
                ReleaseSensor();
                return Vector2.zero;
            }
            if (!ownsSensor)
            {
                previousEnabled = Input.gyro.enabled;
                previousInterval = Input.gyro.updateInterval;
                previousCompensation = Input.compensateSensors;
                Input.compensateSensors = true;
                Input.gyro.updateInterval = 1f / 60f;
                Input.gyro.enabled = true;
                ownsSensor = true;
                readyAt = Time.realtimeSinceStartup + .15f;
            }
            if (Time.realtimeSinceStartup < readyAt) return Vector2.zero;
            // Unity compensates sensor axes for landscape orientation.
            Vector3 rate = Input.gyro.rotationRateUnbiased;
            float speed = PlayerPrefs.GetFloat("GeoSniper.GyroSpeed", 1f);
            return new Vector2(-AndroidAimMath.GyroDelta(rate.y, Time.unscaledDeltaTime, speed),
                AndroidAimMath.GyroDelta(rate.x, Time.unscaledDeltaTime, speed));
        }
        void ReleaseSensor()
        {
            if (!ownsSensor) return;
            Input.gyro.enabled = previousEnabled;
            Input.gyro.updateInterval = previousInterval;
            Input.compensateSensors = previousCompensation;
            ownsSensor = false;
        }
        void OnApplicationPause(bool value) { paused = value; if (value) ReleaseSensor(); }
        void OnApplicationFocus(bool value) { focused = value; if (!value) ReleaseSensor(); }
        void OnDisable() { ReleaseSensor(); }
    }
}
