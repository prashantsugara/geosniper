using UnityEngine;

namespace GeoSniper
{
    public class WeaponSway : MonoBehaviour
    {
        public WeaponConfig config;
        
        [HideInInspector]
        public float breathStamina = 1f;
        [HideInInspector]
        public bool isHoldingBreath = false;
        
        private float time;
        private bool isExhausted = false;
        private float heartbeatTimer = 0f;
        private static AudioClip heartbeatClip;
        private AudioSource audioSource;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;

            if (heartbeatClip == null)
            {
                heartbeatClip = ProceduralAudio.CreateHeartbeat();
            }
        }

        public void UpdateSway(bool isMoving, bool isHoldingBreathInput)
        {
            if (config == null) return;
            
            time += Time.deltaTime * config.swaySpeed * (isMoving ? 1.5f : 1f);

            if (isHoldingBreathInput && !isExhausted && breathStamina > 0)
            {
                isHoldingBreath = true;
                breathStamina -= Time.unscaledDeltaTime / config.breathHoldDuration;

                // Cinematic Slow-Motion Time Dilation (0.35x)
                TimeScaleController.SetBreathHold(0.35f);

                // Dynamic Heartbeat Pulse
                heartbeatTimer -= Time.unscaledDeltaTime;
                float beatInterval = Mathf.Lerp(0.48f, 0.90f, breathStamina);
                if (heartbeatTimer <= 0f)
                {
                    heartbeatTimer = beatInterval;
                    if (audioSource != null && heartbeatClip != null)
                    {
                        audioSource.PlayOneShot(heartbeatClip, Mathf.Lerp(0.85f, 0.55f, breathStamina));
                    }
                }

                if (breathStamina <= 0)
                {
                    breathStamina = 0;
                    isExhausted = true;
                    isHoldingBreath = false;
                }
            }
            else
            {
                isHoldingBreath = false;
                breathStamina += Time.unscaledDeltaTime / config.breathRecoveryTime;
                TimeScaleController.ClearBreathHold();

                if (breathStamina >= 1f)
                {
                    breathStamina = 1f;
                    isExhausted = false;
                }
            }
        }

        private UrbanPlayer cachedPlayer;

        public Vector2 GetSwayOffset(bool isMoving, bool isScoped)
        {
            if (config == null) return Vector2.zero;
            
            float multiplier = isMoving ? config.moveSwayAmount : config.idleSwayAmount;
            
            if (isScoped) multiplier *= 0.5f;

            if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<UrbanPlayer>();
            if (cachedPlayer != null && cachedPlayer.IsCrouching) multiplier *= 0.45f;

            // Complete crosshair stillness when holding breath
            if (isHoldingBreath) multiplier = 0f;
            
            // Mild penalty when exhausted — was 2.6x which caused severe rooftop wiggle
            if (isExhausted) multiplier *= 1.5f;
            
            float x = Mathf.Sin(time) * multiplier;
            float y = Mathf.Cos(time * 0.5f) * multiplier;
            
            return new Vector2(x, y);
        }

        void OnDisable()
        {
            TimeScaleController.ClearBreathHold();
        }
    }
}
