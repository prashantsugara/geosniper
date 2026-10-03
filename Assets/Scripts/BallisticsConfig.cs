using UnityEngine;

namespace GeoSniper
{
    [CreateAssetMenu(fileName = "BallisticsConfig", menuName = "GeoSniper/BallisticsConfig")]
    public class BallisticsConfig : ScriptableObject
    {
        [Header("Mode")]
        [Tooltip("If true, use instant hitscan. If false, use projectile physics.")]
        public bool isArcadeMode = false;

        [Header("Simulation")]
        public float bulletSpeed = 850f;
        public float gravityMultiplier = 1f;
        public float maxRange = 1200f;
        public float baseDamage = 100f;
        [Min(1f)] public float headshotMultiplier = 4f;
        [Range(0f,1f)] public float limbMultiplier = .6f;
        [Min(0f)] public float noiseRadius = 130f;
    }
}
