using UnityEngine;

namespace GeoSniper
{
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "GeoSniper/WeaponConfig")]
    public class WeaponConfig : ScriptableObject
    {
        [Header("General")]
        public float fireRate = 0.32f;
        public float reloadTime = 1.4f;
        public int magazineSize = 20;

        [Header("Recoil")]
        public float recoilPitch = 9f;
        public float recoilYaw = 1f;
        public float recoilRecovery = 6f;

        [Header("Sway")]
        public float idleSwayAmount = 0.5f;
        public float moveSwayAmount = 2.0f;
        public float swaySpeed = 1.0f;

        [Header("Breath")]
        public float breathHoldDuration = 4.0f;
        public float breathRecoveryTime = 3.0f;

        [Header("Scope")]
        public float scopeTransitionSpeed = 10f;
        public float hipFOV = 65f;
        public float[] zoomLevels = { 24f, 12f, 6f };
    }
}
