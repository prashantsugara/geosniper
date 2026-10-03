using UnityEngine;

namespace GeoSniper
{
    public class WorldRainSystem : MonoBehaviour
    {
        public static WorldRainSystem Instance { get; private set; }

        private ParticleSystem _rainParticleSystem;
        private ParticleSystem _splashParticleSystem;
        private AudioSource _rainAudioSource;
        private Transform _targetCamera;
        private bool _isRainActive = false;
        private bool _isHeavy;
        private Material rainMaterial, splashMaterial;
        private Texture2D rainTexture;
        private Light stormLight;
        private float nextLightning, lightningUntil;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance=null; }
        public void RefreshQuality() => SetRainActive(_isRainActive,_isHeavy);
        private float _targetVolume = 0f;
        private float _currentVolume = 0f;

        public static void EnsureSystem(Transform cameraTransform)
        {
            if (Instance == null)
            {
                var rainObj = new GameObject("WorldRainSystem");
                Instance = rainObj.AddComponent<WorldRainSystem>();
            }
            if (cameraTransform != null) Instance._targetCamera = cameraTransform;
        }

        public static void SetRain(bool active, bool isHeavy = false)
        {
            if (Instance != null)
            {
                Instance.SetRainActive(active, isHeavy);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if(Application.isPlaying) DontDestroyOnLoad(gameObject);

            SetupAudio();
            SetupRainParticles();
            SetupSplashParticles();
            var lightning=new GameObject("Storm light");
            lightning.transform.SetParent(transform,false);
            lightning.transform.rotation=Quaternion.Euler(65,-25,0);
            stormLight=lightning.AddComponent<Light>();
            stormLight.type=LightType.Directional;
            stormLight.color=new Color(.72f,.82f,1f);
            stormLight.shadows=LightShadows.None;
            stormLight.enabled=false;
        }

        private void SetupAudio()
        {
            _rainAudioSource = gameObject.AddComponent<AudioSource>();
            _rainAudioSource.clip = ProceduralAudio.CreateRainLoop();
            _rainAudioSource.loop = true;
            _rainAudioSource.spatialBlend = 0.15f; // Mostly ambient stereo
            _rainAudioSource.volume = 0f;
            _rainAudioSource.playOnAwake = false;
        }

        private void SetupRainParticles()
        {
            var rainObj = new GameObject("RainParticleEmitter");
            rainObj.transform.SetParent(transform, false);

            _rainParticleSystem = rainObj.AddComponent<ParticleSystem>();
            var main = _rainParticleSystem.main;
            main.playOnAwake = false;
            main.maxParticles = 2400;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = 0.075f;
            main.startColor = new Color(0.78f, 0.87f, 1f, 0.72f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _rainParticleSystem.emission;
            emission.rateOverTime = 1200f;
            emission.enabled = true;

            var shape = _rainParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 2f, 24f);
            shape.rotation = Vector3.zero;

            var vel = _rainParticleSystem.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-3f, -1f); // Gentle wind angle
            vel.y = new ParticleSystem.MinMaxCurve(-22f, -18f);
            vel.z = new ParticleSystem.MinMaxCurve(1f, 3f);

            var psr = rainObj.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.cameraVelocityScale = 0f;
            psr.velocityScale = 0.06f;
            psr.lengthScale = 1.6f;

            rainMaterial = CreateWeatherMaterial();
            rainTexture = CreateRaindropTexture();
            rainMaterial.mainTexture = rainTexture;
            psr.sharedMaterial = rainMaterial;
            _rainParticleSystem.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        static Material CreateWeatherMaterial()
        {
            var shader=Resources.Load<Shader>("Shaders/WorldRain");
            if(shader==null) throw new System.InvalidOperationException("Missing bundled WorldRain shader");
            return new Material(shader);
        }

        private static Texture2D CreateRaindropTexture()
        {
            var tex = new Texture2D(4, 32, TextureFormat.RGBA32, false);
            Color[] px = new Color[4 * 32];
            for (int y = 0; y < 32; y++)
            {
                float t = (float)y / 31f;
                // Soft needle gradient, tapering smoothly at head and tail
                float alpha = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 1.8f);
                for (int x = 0; x < 4; x++)
                {
                    float xDist = Mathf.Abs(x - 1.5f) / 1.5f;
                    float a = alpha * Mathf.Clamp01(1f - xDist * 0.8f);
                    px[y * 4 + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        private void SetupSplashParticles()
        {
            var splashObj = new GameObject("SplashParticleEmitter");
            splashObj.transform.SetParent(transform, false);

            _splashParticleSystem = splashObj.AddComponent<ParticleSystem>();
            var main = _splashParticleSystem.main;
            main.playOnAwake = false;
            main.maxParticles = 500;
            main.startLifetime = 0.18f;
            main.startSpeed = 1.8f;
            main.startSize = 0.08f;
            main.startColor = new Color(0.75f, 0.82f, 0.92f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _splashParticleSystem.emission;
            emission.rateOverTime = 250f;
            emission.enabled = true;

            var shape = _splashParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 22f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var psr = splashObj.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;

            splashMaterial=CreateWeatherMaterial();
            splashMaterial.mainTexture=rainTexture;
            psr.sharedMaterial=splashMaterial;
            _splashParticleSystem.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void SetRainActive(bool active, bool isHeavy = false)
        {
            bool starting=active && !_isRainActive;
            bool startingStorm=active && isHeavy && (!_isRainActive || !_isHeavy);
            _isRainActive = active;
            _isHeavy = isHeavy;
            _targetVolume = active ? (isHeavy ? 0.45f : 0.3f) : 0f;

            PositionEmitters();
            if(startingStorm) nextLightning=Time.time+Random.Range(3f,6f);
            if(!active || !isHeavy) { lightningUntil=0; if(stormLight!=null) stormLight.enabled=false; }
            if (_rainParticleSystem != null)
            {
                var velocity=_rainParticleSystem.velocityOverLifetime;
                velocity.x=new ParticleSystem.MinMaxCurve(isHeavy?-8f:-3f,isHeavy?-5f:-1f);
                var emission = _rainParticleSystem.emission;
                emission.rateOverTime = active ? (isHeavy ? 1400f : 900f) * MobileGraphics.WeatherDensity : 0f;

                if (active && !_rainParticleSystem.isPlaying)
                {
                    if(starting) _rainParticleSystem.Simulate(.55f,true,true,true);
                    _rainParticleSystem.Play();
                }
                else if (!active)
                {
                    _rainParticleSystem.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (_splashParticleSystem != null)
            {
                var splashEmission = _splashParticleSystem.emission;
                splashEmission.rateOverTime = active ? (isHeavy ? 450f : 250f) * MobileGraphics.WeatherDensity : 0f;

                if (active && !_splashParticleSystem.isPlaying)
                {
                    _splashParticleSystem.Play();
                }
                else if (!active)
                {
                    _splashParticleSystem.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (active && _rainAudioSource != null && !_rainAudioSource.isPlaying)
            {
                _rainAudioSource.Play();
            }
        }

        private void PositionEmitters()
        {
            if (_targetCamera == null)
            {
                var mainCam = Camera.main;
                if (mainCam != null) _targetCamera = mainCam.transform;
            }

            if (_targetCamera != null)
            {
                // Position rain volume directly above camera
                Vector3 camPos = _targetCamera.position;
                if (_rainParticleSystem != null)
                {
                    _rainParticleSystem.transform.position = camPos + Vector3.up * 8f + Vector3.ProjectOnPlane(_targetCamera.forward,Vector3.up).normalized * 5f;
                }

                if (_splashParticleSystem != null)
                {
                    // Snap splashes to ground below camera
                    // No assumed ground height: avoid airborne splashes on rooftops or while falling.
                    bool surface=Physics.Raycast(camPos,Vector3.down,out var hit,8f,~0,QueryTriggerInteraction.Ignore);
                    _splashParticleSystem.GetComponent<ParticleSystemRenderer>().enabled=surface;
                    if(surface) _splashParticleSystem.transform.position=hit.point+Vector3.up*.04f;
                }
            }

        }

        private void LateUpdate()
        {
            PositionEmitters();
            if(_isRainActive && _isHeavy && _targetCamera!=null && Time.timeScale>0)
            {
                if(Time.time>=nextLightning)
                {
                    lightningUntil=Time.time+.32f;
                    nextLightning=Time.time+Random.Range(8f,15f);
                }
                stormLight.enabled=Time.time<lightningUntil;
                stormLight.intensity=Mathf.Clamp01((lightningUntil-Time.time)/.32f)*1.8f;
            }
            else if(stormLight!=null) stormLight.enabled=false;
            // Audio crossfade
            _currentVolume = Mathf.MoveTowards(_currentVolume, _targetVolume, Time.deltaTime * 0.8f);
            if (_rainAudioSource != null)
            {
                _rainAudioSource.volume = _currentVolume;
                if (_currentVolume <= 0.001f && !_isRainActive && _rainAudioSource.isPlaying)
                {
                    _rainAudioSource.Stop();
                }
            }
        }
        void OnDestroy()
        {
            if(Instance==this) Instance=null;
            if(rainMaterial!=null) Destroy(rainMaterial);
            if(splashMaterial!=null) Destroy(splashMaterial);
            if(rainTexture!=null) Destroy(rainTexture);
        }
    }
}

