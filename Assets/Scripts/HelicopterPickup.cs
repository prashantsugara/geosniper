using System;
using UnityEngine;

namespace GeoSniper
{
    public class HelicopterPickup : MonoBehaviour
    {
        public enum HeliState
        {
            Inbound,
            Hovering,
            Boarding,
            Boarded,
            Outbound
        }

        [Header("State")]
        public HeliState State = HeliState.Inbound;
        public Vector3 LandingPoint;
        public Action OnExtracted;

        [Header("Flight Parameters")]
        public float InboundSpeed = 22f;
        public float OutboundSpeed = 28f;
        public float MainRotorDegPerSec = 1560f;
        public float TailRotorDegPerSec = 2800f;

        // Rotor transforms & axes
        private Transform _mainRotor;
        private Transform _tailRotor;
        private Vector3 _mainRotorAxis = Vector3.up;
        private Vector3 _tailRotorAxis = Vector3.right;

        // Audio
        private AudioSource _rotorAudioSource;
        private AudioSource _radioAudioSource;

        // Inbound flight interpolation
        private Vector3 _spawnPos;
        private Vector3 _targetTouchdown;
        private float _flightProgress = 0f;
        private float _flightDuration = 5.0f;

        // Hover & Rope
        private float _hoverTimer = 0f;
        private GameObject _smokeFlare;
        private LineRenderer _fastRope;
        private GameObject _ropeEndMarker;
        private Light _searchlight;
        private Light _tailStrobe;
        private Light _navPort;
        private Light _navStarboard;
        private float _strobeTimer = 0f;

        // Boarding / Hoisting
        private UrbanPlayer _targetPlayer;
        private Vector3 _playerClimbStart;
        private float _climbProgress = 0f;
        private const float ClimbDuration = 2.0f;

        // Outbound
        private Vector3 _departStartPos;
        private Vector3 _departTargetPos;
        private float _outboundProgress = 0f;

        public static HelicopterPickup Spawn(Vector3 lzPoint, Action onExtractedCallback = null)
        {
            var heliObj = new GameObject("Helicopter_Extraction_Dustoff");
            var pickup = heliObj.AddComponent<HelicopterPickup>();
            pickup.Initialize(lzPoint, onExtractedCallback);
            return pickup;
        }

        public void Initialize(Vector3 lzPoint, Action onExtractedCallback)
        {
            LandingPoint = lzPoint;
            OnExtracted = onExtractedCallback;
            _targetPlayer = FindObjectOfType<UrbanPlayer>();

            // Tactical hover altitude: 8.5m above the LZ ground/rooftop
            _targetTouchdown = LandingPoint + Vector3.up * 8.5f;

            // Inbound spawn: approach from high open sky in front of the player/LZ
            Vector3 approachDir = Vector3.forward;
            if (Camera.main != null)
            {
                approachDir = Camera.main.transform.forward;
                approachDir.y = 0f;
                if (approachDir.sqrMagnitude < 0.01f) approachDir = Vector3.forward;
                approachDir.Normalize();
            }
            Vector3 cross = Vector3.Cross(Vector3.up, approachDir).normalized;
            // Spawn 85m ahead and 36m up so player clearly sees the helicopter fly into the LZ
            _spawnPos = _targetTouchdown + approachDir * 80f + cross * 22f + Vector3.up * 35f;

            transform.position = _spawnPos;
            Vector3 flyHeading = (_targetTouchdown - _spawnPos).normalized;
            transform.rotation = Quaternion.LookRotation(new Vector3(flyHeading.x, 0, flyHeading.z), Vector3.up);

            // Load and instantiate 3D Model
            SetupModel();

            // Setup 3D Spatial Audio
            SetupAudio();

            // Setup Tactical Searchlight & Navigation Strobes
            SetupLighting();

            // Spawn Green Smoke Flare at LZ ground
            SpawnLzSmokeFlare(LandingPoint);

            // Play incoming radio transmission
            PlayRadioCall();

            State = HeliState.Inbound;
            _flightProgress = 0f;
        }

        private void SetupModel()
        {
            GameObject modelPrefab = Resources.Load<GameObject>("Models/Helicopter_BlackHawk");
            GameObject visual = null;

            if (modelPrefab != null)
            {
                visual = Instantiate(modelPrefab, transform, false);
                visual.name = "BlackHawk_Visual";
            }

            // Verify if visual actually has renderers
            bool hasValidRenderers = visual != null && visual.GetComponentsInChildren<MeshRenderer>(true).Length > 0;
            if (!hasValidRenderers)
            {
                if (visual != null) Destroy(visual);
                visual = CreateProceduralHelicopterVisual();
                visual.transform.SetParent(transform, false);
            }

            // Normalize scale and center fuselage at root transform
            Bounds b = ImportedVisual.LocalBounds(visual.transform);
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (longest > 0.05f && float.IsFinite(longest))
            {
                if (longest < 10f || longest > 22f)
                {
                    float targetScale = 15.5f / longest;
                    visual.transform.localScale = Vector3.one * targetScale;
                    b = ImportedVisual.LocalBounds(visual.transform);
                }
                // Center model so fuselage midpoint aligns with transform.position
                visual.transform.localPosition = -b.center;
            }

            // Apply camo materials & textures unconditionally with guaranteed opaque depth write
            ApplyHelicopterMaterials(visual);

            // Locate or create Main Rotor and Tail Rotor transforms
            LocateRotors(visual);
        }

        private void LocateRotors(GameObject visual)
        {
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (_mainRotor == null && (n.Contains("main rotor") || n.Contains("prop_7") || n.Contains("object_10") || (n.Contains("rotor") && !n.Contains("tail"))))
                {
                    _mainRotor = t;
                    _mainRotorAxis = DetermineSpinAxis(t, Vector3.up);
                }
                else if (_tailRotor == null && (n.Contains("tail rotor") || n.Contains("tail") || n.Contains("object_4")))
                {
                    _tailRotor = t;
                    _tailRotorAxis = DetermineSpinAxis(t, Vector3.right);
                }
            }

            // Fallback by relative position
            if (_mainRotor == null || _tailRotor == null)
            {
                var renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers)
                {
                    string n = r.name.ToLowerInvariant();
                    if (_mainRotor == null && (n.Contains("rotor") || n.Contains("prop") || r.bounds.center.y > transform.position.y + 1.2f))
                    {
                        _mainRotor = r.transform;
                        _mainRotorAxis = Vector3.up;
                    }
                    else if (_tailRotor == null && (n.Contains("tail") || r.bounds.center.z < transform.position.z - 3.5f))
                    {
                        _tailRotor = r.transform;
                        _tailRotorAxis = Vector3.right;
                    }
                }
            }

            // If main rotor still not found, add high-fidelity 4-blade spinning rotor on top of fuselage
            if (_mainRotor == null)
            {
                var rotorHub = new GameObject("Procedural_MainRotor");
                rotorHub.transform.SetParent(visual.transform, false);
                rotorHub.transform.localPosition = new Vector3(0f, 2.3f, 0.5f);
                var bladeMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                bladeMat.color = new Color(0.12f, 0.12f, 0.12f);
                bladeMat.SetFloat("_Mode", 0f);
                bladeMat.SetInt("_ZWrite", 1);
                bladeMat.renderQueue = 2000;
                for (int b = 0; b < 4; b++)
                {
                    var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blade.name = "Blade_" + b;
                    blade.transform.SetParent(rotorHub.transform, false);
                    blade.transform.localScale = new Vector3(0.35f, 0.05f, 7.2f);
                    blade.transform.localRotation = Quaternion.Euler(0, b * 90f, 0);
                    blade.GetComponent<MeshRenderer>().sharedMaterial = bladeMat;
                    var c = blade.GetComponent<Collider>();
                    if (c != null) Destroy(c);
                }
                _mainRotor = rotorHub.transform;
                _mainRotorAxis = Vector3.up;
            }

            if (_tailRotor == null)
            {
                var tailHub = new GameObject("Procedural_TailRotor");
                tailHub.transform.SetParent(visual.transform, false);
                tailHub.transform.localPosition = new Vector3(0.35f, 2.4f, -7.8f);
                var bladeMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                bladeMat.color = new Color(0.12f, 0.12f, 0.12f);
                bladeMat.SetFloat("_Mode", 0f);
                bladeMat.SetInt("_ZWrite", 1);
                bladeMat.renderQueue = 2000;
                for (int b = 0; b < 4; b++)
                {
                    var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blade.name = "TailBlade_" + b;
                    blade.transform.SetParent(tailHub.transform, false);
                    blade.transform.localScale = new Vector3(0.04f, 0.22f, 1.4f);
                    blade.transform.localRotation = Quaternion.Euler(b * 90f, 0, 0);
                    blade.GetComponent<MeshRenderer>().sharedMaterial = bladeMat;
                    var c = blade.GetComponent<Collider>();
                    if (c != null) Destroy(c);
                }
                _tailRotor = tailHub.transform;
                _tailRotorAxis = Vector3.right;
            }
        }

        private Vector3 DetermineSpinAxis(Transform t, Vector3 defaultAxis)
        {
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                if (size.y < size.x * 0.4f && size.y < size.z * 0.4f) return Vector3.up;
                if (size.x < size.y * 0.4f && size.x < size.z * 0.4f) return Vector3.right;
                if (size.z < size.x * 0.4f && size.z < size.y * 0.4f) return Vector3.forward;
            }
            return defaultAxis;
        }

        private void ApplyHelicopterMaterials(GameObject visual)
        {
            var diffuse = Resources.Load<Texture2D>("Models/Textures/UH60M_diffuse");
            var normal = Resources.Load<Texture2D>("Models/Textures/UH60M_normal");
            var spec = Resources.Load<Texture2D>("Models/Textures/UH60M_specularGlossiness");
            var occ = Resources.Load<Texture2D>("Models/Textures/UH60M_occlusion");

            Shader shader = Shader.Find("Standard") ?? Shader.Find("Mobile/Bumped Diffuse") ?? Shader.Find("Diffuse");

            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                r.enabled = true;
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;

                    if (shader != null) m.shader = shader;

                    // Force Opaque mode: Prevents PNG alpha channel from causing transparent or invisible mesh
                    m.SetFloat("_Mode", 0f);
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    m.SetInt("_ZWrite", 1);
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.DisableKeyword("_ALPHABLEND_ON");
                    m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.renderQueue = 2000;

                    if (diffuse != null)
                    {
                        m.mainTexture = diffuse;
                        m.color = Color.white;
                    }
                    else
                    {
                        m.color = new Color(0.24f, 0.28f, 0.24f); // Tactical olive drab
                    }

                    if (m.HasProperty("_BumpMap") && normal != null)
                    {
                        m.EnableKeyword("_NORMALMAP");
                        m.SetTexture("_BumpMap", normal);
                    }

                    if (m.HasProperty("_OcclusionMap") && occ != null)
                        m.SetTexture("_OcclusionMap", occ);

                    if (m.HasProperty("_SpecGlossMap") && spec != null)
                        m.SetTexture("_SpecGlossMap", spec);

                    if (m.HasProperty("_Glossiness"))
                        m.SetFloat("_Glossiness", 0.35f);
                    if (m.HasProperty("_Metallic"))
                        m.SetFloat("_Metallic", 0.35f);
                }
                r.materials = mats;
            }
        }

        private void SetupAudio()
        {
            // Rotor 3D Audio Source
            _rotorAudioSource = gameObject.AddComponent<AudioSource>();
            _rotorAudioSource.clip = ProceduralAudio.CreateHelicopterRotorLoop();
            _rotorAudioSource.loop = true;
            _rotorAudioSource.spatialBlend = 0.85f; // Realistic 3D spatial sound
            _rotorAudioSource.rolloffMode = AudioRolloffMode.Linear;
            _rotorAudioSource.minDistance = 20f;
            _rotorAudioSource.maxDistance = 350f;
            _rotorAudioSource.volume = 1.0f;
            _rotorAudioSource.pitch = 1.0f;
            _rotorAudioSource.dopplerLevel = 0f; // Zero Doppler shift avoids weird frequency warping during flight
            _rotorAudioSource.Play();

            // Radio Audio Source
            _radioAudioSource = gameObject.AddComponent<AudioSource>();
            _radioAudioSource.spatialBlend = 0f; // Pilot radio stays intelligible while the aircraft approaches
            _radioAudioSource.volume = 1.0f;
        }

        private void SetupLighting()
        {
            // 1. High-intensity downward tactical searchlight illuminating the LZ & rope
            var spotObj = new GameObject("Heli_Searchlight");
            spotObj.transform.SetParent(transform, false);
            spotObj.transform.localPosition = new Vector3(0f, -0.6f, 3.2f);
            spotObj.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);

            _searchlight = spotObj.AddComponent<Light>();
            _searchlight.type = LightType.Spot;
            _searchlight.color = new Color(0.95f, 0.98f, 1.0f);
            _searchlight.spotAngle = 55f;
            _searchlight.range = 50f;
            _searchlight.intensity = 3.5f;

            // 2. Warm amber interior cabin light illuminating doorway, fast-rope anchor, and boarding operative
            var cabinLightObj = new GameObject("Heli_CabinInteriorLight");
            cabinLightObj.transform.SetParent(transform, false);
            cabinLightObj.transform.localPosition = new Vector3(0.5f, 0.2f, 0.2f);
            var cabinLight = cabinLightObj.AddComponent<Light>();
            cabinLight.type = LightType.Point;
            cabinLight.color = new Color(1.0f, 0.72f, 0.35f);
            cabinLight.range = 10f;
            cabinLight.intensity = 3.2f;

            // 3. Fuselage belly wash light ensuring helicopter body, skids, and underside are brightly visible in dark night
            var bellyLightObj = new GameObject("Heli_FuselageWashLight");
            bellyLightObj.transform.SetParent(transform, false);
            bellyLightObj.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            var bellyLight = bellyLightObj.AddComponent<Light>();
            bellyLight.type = LightType.Point;
            bellyLight.color = new Color(0.85f, 0.92f, 1.0f);
            bellyLight.range = 16f;
            bellyLight.intensity = 2.4f;

            // 4. Anti-collision red flashing beacon
            var strobeObj = new GameObject("Heli_TailStrobe");
            strobeObj.transform.SetParent(transform, false);
            strobeObj.transform.localPosition = new Vector3(0f, 2.4f, -8.5f);

            _tailStrobe = strobeObj.AddComponent<Light>();
            _tailStrobe.type = LightType.Point;
            _tailStrobe.color = Color.red;
            _tailStrobe.range = 16f;
            _tailStrobe.intensity = 2.5f;

            // 5. Port (red) and Starboard (green) navigation lights
            var portObj = new GameObject("Heli_Nav_Port");
            portObj.transform.SetParent(transform, false);
            portObj.transform.localPosition = new Vector3(-2.2f, 0.4f, 0.5f);
            _navPort = portObj.AddComponent<Light>();
            _navPort.type = LightType.Point;
            _navPort.color = new Color(1f, 0.1f, 0.1f);
            _navPort.range = 6f;
            _navPort.intensity = 1.8f;

            var starObj = new GameObject("Heli_Nav_Starboard");
            starObj.transform.SetParent(transform, false);
            starObj.transform.localPosition = new Vector3(2.2f, 0.4f, 0.5f);
            _navStarboard = starObj.AddComponent<Light>();
            _navStarboard.type = LightType.Point;
            _navStarboard.color = new Color(0.1f, 1f, 0.2f);
            _navStarboard.range = 6f;
            _navStarboard.intensity = 1.8f;
        }

        private void PlayRadioCall()
        {
            if (_radioAudioSource == null) return;
            var radioClip = ProceduralAudio.CreateExtractionRadio();
            if (radioClip != null) StartCoroutine(TransmitRadioCall(radioClip));
        }

        private System.Collections.IEnumerator TransmitRadioCall(AudioClip clip)
        {
            float deadline = Time.time + 8f;
            while (Time.time < deadline)
            {
                if (RadioCommsChannel.PlayTransmission(_radioAudioSource, clip, RadioCommsChannel.Priority.High,
                    volume: 1f, silenceCooldownAfter: 3f)) yield break;
                yield return new WaitForSeconds(.4f);
            }
        }

        private void SpawnLzSmokeFlare(Vector3 pos)
        {
            _smokeFlare = new GameObject("LZ_GreenSmokeFlare");
            _smokeFlare.transform.position = pos + Vector3.up * 0.05f;

            var light = _smokeFlare.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.2f, 1f, 0.35f);
            light.range = 14f;
            light.intensity = 2.5f;

            for (int i = 0; i < 5; i++)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "SmokeRing_" + i;
                ring.transform.SetParent(_smokeFlare.transform, false);
                ring.transform.localPosition = Vector3.up * (0.4f + i * 0.7f);
                ring.transform.localScale = new Vector3(0.5f + i * 0.35f, 0.04f, 0.5f + i * 0.35f);

                var c = ring.GetComponent<Collider>();
                if (c != null) Destroy(c);

                var mr = ring.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Sprites/Default"));
                mat.color = new Color(0.2f, 0.95f, 0.3f, Mathf.Max(0.12f, 0.45f - i * 0.08f));
                mr.sharedMaterial = mat;
            }
        }

        private Vector3 CabinDoorPos => transform.position + transform.right * 1.25f - transform.up * 0.45f + transform.forward * 0.25f;
        private Vector3 RopeGroundPos => LandingPoint + Vector3.up * 0.12f;

        private void DeployFastRope()
        {
            if (_fastRope != null) return;

            var ropeObj = new GameObject("FastRope_Extraction");
            ropeObj.transform.SetParent(transform, false);

            _fastRope = ropeObj.AddComponent<LineRenderer>();
            _fastRope.useWorldSpace = true;
            _fastRope.positionCount = 16;
            _fastRope.startWidth = 0.09f;
            _fastRope.endWidth = 0.09f;

            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse") ?? Shader.Find("Sprites/Default"));
            mat.color = new Color(0.65f, 0.58f, 0.45f); // Braided tactical rope khaki
            _fastRope.sharedMaterial = mat;

            // Weighted metal carabiner/ring at the bottom
            _ropeEndMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _ropeEndMarker.name = "Rope_EndRing";
            _ropeEndMarker.transform.SetParent(transform, true);
            _ropeEndMarker.transform.localScale = new Vector3(0.45f, 0.08f, 0.45f);
            var col = _ropeEndMarker.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var ringMr = _ropeEndMarker.GetComponent<MeshRenderer>();
            var ringMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
            ringMat.color = new Color(0.85f, 0.72f, 0.25f); // Brass carabiner ring
            ringMr.sharedMaterial = ringMat;

            UpdateRopePositions();
        }

        private void UpdateRopePositions()
        {
            if (_fastRope == null) return;

            Vector3 top = CabinDoorPos;
            Vector3 bottom = RopeGroundPos;

            int count = _fastRope.positionCount;
            for (int i = 0; i < count; i++)
            {
                float frac = (float)i / (count - 1);
                Vector3 pt = Vector3.Lerp(top, bottom, frac);

                // Rotor downwash aerodynamic sway
                if (frac > 0.06f && frac < 0.94f)
                {
                    float wave = Mathf.Sin(Time.time * 6.5f + i * 0.5f) * Mathf.Sin(frac * Mathf.PI) * 0.14f;
                    Vector3 swayDir = (transform.right * 0.4f + transform.forward * 0.6f).normalized;
                    pt += swayDir * wave;
                }

                _fastRope.SetPosition(i, pt);
            }

            if (_ropeEndMarker != null)
            {
                _ropeEndMarker.transform.position = bottom;
                _ropeEndMarker.transform.rotation = Quaternion.Euler(0, Time.time * 45f, 0);
            }
        }

        private void Update()
        {
            RotateRotors();

            // Anti-collision strobe pulsing (on 0.12s, off 0.88s)
            _strobeTimer += Time.deltaTime;
            if (_tailStrobe != null)
            {
                _tailStrobe.enabled = (_strobeTimer % 1.0f) < 0.12f;
            }

            switch (State)
            {
                case HeliState.Inbound:
                    UpdateInbound();
                    break;
                case HeliState.Hovering:
                    UpdateHovering();
                    break;
                case HeliState.Boarding:
                    UpdateBoarding();
                    break;
                case HeliState.Boarded:
                    // Handled by transition to Outbound
                    break;
                case HeliState.Outbound:
                    UpdateOutbound();
                    break;
            }
        }

        private void RotateRotors()
        {
            float dt = Time.deltaTime;
            float rotorMult = (State == HeliState.Outbound) ? 1.25f : 1.0f;

            if (_mainRotor != null)
            {
                _mainRotor.Rotate(_mainRotorAxis, MainRotorDegPerSec * rotorMult * dt, Space.Self);
            }

            if (_tailRotor != null)
            {
                _tailRotor.Rotate(_tailRotorAxis, TailRotorDegPerSec * rotorMult * dt, Space.Self);
            }
        }

        private void UpdateInbound()
        {
            _flightProgress += Time.deltaTime / _flightDuration;
            float t = Mathf.Clamp01(_flightProgress);

            float ease = 1f - Mathf.Pow(1f - t, 2.5f);
            Vector3 currentPos = Vector3.Lerp(_spawnPos, _targetTouchdown, ease);

            // Arc descent
            float arc = Mathf.Sin(t * Mathf.PI) * 3.5f;
            currentPos.y += arc;
            transform.position = currentPos;

            Vector3 moveDir = (_targetTouchdown - transform.position).normalized;
            if (moveDir.sqrMagnitude > 0.001f)
            {
                float pitchAngle = Mathf.Lerp(12f, -3f, t);
                Quaternion targetRot = Quaternion.LookRotation(new Vector3(moveDir.x, 0, moveDir.z).normalized, Vector3.up);
                targetRot *= Quaternion.Euler(pitchAngle, 0, 0);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3.5f);
            }

            if (_rotorAudioSource != null)
            {
                _rotorAudioSource.pitch = Mathf.Lerp(0.96f, 1.02f, t);
            }

            if (t >= 1.0f)
            {
                State = HeliState.Hovering;
                transform.position = _targetTouchdown;
                transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
                if (_rotorAudioSource != null) _rotorAudioSource.pitch = 1.0f;

                // Deploy fast-rope down to the ground/rooftop
                DeployFastRope();
            }
        }

        private void UpdateHovering()
        {
            _hoverTimer += Time.deltaTime;

            // Organic hover sway
            float hoverY = Mathf.Sin(_hoverTimer * 1.8f) * 0.15f;
            float hoverRoll = Mathf.Sin(_hoverTimer * 1.2f) * 0.8f;
            transform.position = _targetTouchdown + Vector3.up * hoverY;
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, hoverRoll);

            // Update rope sway in rotor wash
            UpdateRopePositions();

            // Pulse LZ smoke flare light
            if (_smokeFlare != null)
            {
                var l = _smokeFlare.GetComponent<Light>();
                if (l != null) l.intensity = 2.2f + Mathf.PingPong(Time.time * 2f, 1.0f);
            }

            // Proximity check with player
            if (_targetPlayer == null) _targetPlayer = FindObjectOfType<UrbanPlayer>();
            if (_targetPlayer != null && !_targetPlayer.Health.IsDead)
            {
                float distToRope = Vector3.Distance(new Vector3(_targetPlayer.transform.position.x, 0, _targetPlayer.transform.position.z),
                                                   new Vector3(LandingPoint.x, 0, LandingPoint.z));

                // Auto-grab if directly at the rope base
                if (distToRope < 1.8f)
                {
                    TriggerBoarding();
                }
                else if (distToRope < 4.8f && Input.GetKeyDown(KeyCode.E))
                {
                    TriggerBoarding();
                }
            }
        }

        private void OnGUI()
        {
            if (State != HeliState.Hovering) return;
            if (_targetPlayer == null) _targetPlayer = FindObjectOfType<UrbanPlayer>();
            if (_targetPlayer == null || _targetPlayer.Health.IsDead) return;

            float distToRope = Vector3.Distance(new Vector3(_targetPlayer.transform.position.x, 0, _targetPlayer.transform.position.z),
                                               new Vector3(LandingPoint.x, 0, LandingPoint.z));

            if (distToRope < 4.8f)
            {
                float w = Screen.width;
                float h = Screen.height;

                Rect promptRect = new Rect(w / 2f - 180f, h - 165f, 360f, 32f);
                TacticalGUI.DrawPanel(promptRect, new Color(0.04f, 0.16f, 0.08f, 0.94f), TacticalGUI.AccentGreen, 1.5f);
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = TacticalGUI.AccentGreen }
                };
                GUI.Label(promptRect, "🪢 PRESS [E] TO CLIMB EXTRACTION ROPE", style);

                Rect btnRect = new Rect(w / 2f - 110f, h - 125f, 220f, 46f);
                if (TacticalGUI.DrawGreenPlayButton(btnRect, "🚁 CLIMB ROPE"))
                {
                    TriggerBoarding();
                }
            }
        }

        public void TriggerBoarding()
        {
            if (State == HeliState.Boarding || State == HeliState.Boarded || State == HeliState.Outbound) return;

            State = HeliState.Boarding;
            _climbProgress = 0f;
            if (_targetPlayer == null) _targetPlayer = FindObjectOfType<UrbanPlayer>();
            if (_targetPlayer != null)
            {
                _targetPlayer.InputBlocked = true;
                var cc = _targetPlayer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                _playerClimbStart = _targetPlayer.transform.position;
            }

            // The inbound call already announced the aircraft; avoid replaying the same line on boarding.
        }

        private void UpdateBoarding()
        {
            _climbProgress += Time.deltaTime / ClimbDuration;
            float t = Mathf.Clamp01(_climbProgress);
            float ease = Mathf.SmoothStep(0f, 1f, t);

            if (_targetPlayer != null)
            {
                Vector3 cabinPos = CabinDoorPos;
                _targetPlayer.transform.position = Vector3.Lerp(_playerClimbStart, cabinPos, ease);
            }

            UpdateRopePositions();

            if (t >= 1.0f)
            {
                State = HeliState.Boarded;
                StartOutbound();
            }
        }

        private void StartOutbound()
        {
            State = HeliState.Outbound;
            _departStartPos = transform.position;

            Vector3 forwardFlight = transform.forward * 140f + Vector3.up * 65f;
            _departTargetPos = _departStartPos + forwardFlight;

            if (_rotorAudioSource != null)
            {
                _rotorAudioSource.pitch = 1.18f; // Full collective throttle
            }

            // Retract rope
            if (_fastRope != null) Destroy(_fastRope.gameObject);
            if (_ropeEndMarker != null) Destroy(_ropeEndMarker);
            if (_smokeFlare != null) Destroy(_smokeFlare, 3f);

            _outboundProgress = 0f;
            OnExtracted?.Invoke();
        }

        private void UpdateOutbound()
        {
            _outboundProgress += Time.deltaTime / 4.5f;
            float t = Mathf.Clamp01(_outboundProgress);

            float ease = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(_departStartPos, _departTargetPos, ease);

            Vector3 flightDir = (_departTargetPos - _departStartPos).normalized;
            Quaternion climbRot = Quaternion.LookRotation(new Vector3(flightDir.x, 0, flightDir.z), Vector3.up);
            climbRot *= Quaternion.Euler(15f, 0, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, climbRot, Time.deltaTime * 4f);

            if (_rotorAudioSource != null)
            {
                _rotorAudioSource.volume = Mathf.Lerp(1.0f, 0.05f, t);
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private GameObject CreateProceduralHelicopterVisual()
        {
            var root = new GameObject("Procedural_BlackHawk");

            var darkCamoMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
            darkCamoMat.color = new Color(0.18f, 0.22f, 0.18f); // Olive drab
            darkCamoMat.SetFloat("_Glossiness", 0.45f);

            var blackMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
            blackMat.color = new Color(0.08f, 0.08f, 0.08f);

            // 1. Fuselage
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Fuselage";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(2.4f, 7.5f, 2.2f);
            body.transform.localRotation = Quaternion.Euler(90, 0, 0);
            body.GetComponent<MeshRenderer>().sharedMaterial = darkCamoMat;

            // 2. Cockpit canopy nose
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "CockpitCanopy";
            canopy.transform.SetParent(root.transform, false);
            canopy.transform.localScale = new Vector3(2.2f, 1.8f, 2.4f);
            canopy.transform.localPosition = new Vector3(0, 0.2f, 3.2f);
            canopy.GetComponent<MeshRenderer>().sharedMaterial = blackMat;

            // 3. Tail Boom
            var tail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tail.name = "TailBoom";
            tail.transform.SetParent(root.transform, false);
            tail.transform.localScale = new Vector3(0.7f, 4.2f, 0.7f);
            tail.transform.localPosition = new Vector3(0, 0.6f, -6.0f);
            tail.transform.localRotation = Quaternion.Euler(90, 0, 0);
            tail.GetComponent<MeshRenderer>().sharedMaterial = darkCamoMat;

            // 4. Vertical Tail Fin
            var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fin.name = "VerticalTailFin";
            fin.transform.SetParent(root.transform, false);
            fin.transform.localScale = new Vector3(0.2f, 2.4f, 1.4f);
            fin.transform.localPosition = new Vector3(0, 1.8f, -9.5f);
            fin.GetComponent<MeshRenderer>().sharedMaterial = darkCamoMat;

            // 5. Main Rotor Hub & Blades
            var rotorObj = new GameObject("MainRotor");
            rotorObj.transform.SetParent(root.transform, false);
            rotorObj.transform.localPosition = new Vector3(0, 1.8f, 0.5f);
            for (int b = 0; b < 4; b++)
            {
                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = "Blade_" + b;
                blade.transform.SetParent(rotorObj.transform, false);
                blade.transform.localScale = new Vector3(0.35f, 0.05f, 6.8f);
                blade.transform.localRotation = Quaternion.Euler(0, b * 90f, 0);
                blade.GetComponent<MeshRenderer>().sharedMaterial = blackMat;
            }

            // 6. Tail Rotor
            var tailRotorObj = new GameObject("TailRotor");
            tailRotorObj.transform.SetParent(root.transform, false);
            tailRotorObj.transform.localPosition = new Vector3(0.4f, 2.4f, -9.8f);
            for (int b = 0; b < 4; b++)
            {
                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = "TailBlade_" + b;
                blade.transform.SetParent(tailRotorObj.transform, false);
                blade.transform.localScale = new Vector3(0.04f, 0.2f, 1.4f);
                blade.transform.localRotation = Quaternion.Euler(b * 90f, 0, 0);
                blade.GetComponent<MeshRenderer>().sharedMaterial = blackMat;
            }

            // Clean colliders from visual representation
            foreach (var col in root.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }

            return root;
        }

        private void OnDestroy()
        {
            if (_smokeFlare != null) Destroy(_smokeFlare);
            if (_fastRope != null) Destroy(_fastRope.gameObject);
            if (_ropeEndMarker != null) Destroy(_ropeEndMarker);
        }
    }
}
