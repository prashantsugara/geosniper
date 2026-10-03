using System;
using System.Collections;
using UnityEngine;

namespace GeoSniper.Duel
{
    public sealed class DuelOpponentAvatar : MonoBehaviour
    {
        public string Callsign { get; set; } = "RIVAL";
        public float CurrentHealth { get; private set; } = 100f;
        public bool IsDead => CurrentHealth <= 0;
        public bool IsAimingAtPlayer { get; private set; }

        private GameObject visualRoot;
        private GameObject rifleObject;
        private LineRenderer opticGlint;
        private LineRenderer tracerLine;
        private AudioSource audioSource;
        private CombatActor actor;

        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private float targetPitch;
        private bool remoteIsAiming;
        private bool remoteIsCrouching;

        private const float LerpSpeed = 16f;

        public event Action<byte, float, Vector3> OnLocallyDamaged; // hitZone, damage, point

        public void Initialize(string callsign, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            Callsign = callsign;
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
            targetPosition = spawnPosition;
            targetRotation = spawnRotation;
            CurrentHealth = 100f;

            BuildVisuals();
            BuildHitboxes();
            BuildOpticGlint();
            BuildTracer();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 10f;
            audioSource.maxDistance = 500f;
        }

        private void BuildVisuals()
        {
            // Load enemy character model from Resources
            GameObject prefab = Resources.Load<GameObject>("Models/Enemies/swat") ?? Resources.Load<GameObject>("Models/swat");
            if (prefab == null)
            {
                var models = ModelLibrary.Load("Enemies");
                if (models != null && models.Length > 0) prefab = models[0];
            }

            if (prefab != null)
            {
                visualRoot = Instantiate(prefab, transform);
                visualRoot.transform.localPosition = Vector3.zero;
                visualRoot.transform.localRotation = Quaternion.identity;
                visualRoot.transform.localScale = Vector3.one;
            }
            else
            {
                // Fallback procedural capsule operative if FBX not ready
                visualRoot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visualRoot.transform.SetParent(transform);
                visualRoot.transform.localPosition = Vector3.up * 1.0f;
                var ren = visualRoot.GetComponent<Renderer>();
                if (ren != null) ren.material.color = new Color(0.2f, 0.25f, 0.3f);
            }

            // Attach sniper rifle model
            GameObject riflePrefab = Resources.Load<GameObject>("Models/Weapons/M24Tactical")
                ?? Resources.Load<GameObject>("Models/Weapons/Barrett50")
                ?? Resources.Load<GameObject>("Models/SniperRifle");

            if (riflePrefab != null)
            {
                rifleObject = Instantiate(riflePrefab, transform);
                rifleObject.transform.localPosition = new Vector3(0.25f, 1.35f, 0.45f);
                rifleObject.transform.localRotation = Quaternion.Euler(0, 0, 0);
                rifleObject.transform.localScale = Vector3.one * 0.9f;
            }
        }

        private void BuildHitboxes()
        {
            actor = gameObject.GetComponent<CombatActor>();
            if (actor == null) actor = gameObject.AddComponent<CombatActor>();
            actor.Initialize(100);
            actor.canRegenerate = false;

            // Main body collider
            var bodyCol = gameObject.GetComponent<CapsuleCollider>();
            if (bodyCol == null) bodyCol = gameObject.AddComponent<CapsuleCollider>();
            bodyCol.height = 1.85f;
            bodyCol.radius = 0.38f;
            bodyCol.center = Vector3.up * 0.95f;

            // Head hitbox for 1-shot headshots
            var headObj = new GameObject("Hitbox_Head");
            headObj.transform.SetParent(transform);
            headObj.transform.localPosition = Vector3.up * 1.65f;
            var headCol = headObj.AddComponent<SphereCollider>();
            headCol.radius = 0.24f;
            headObj.tag = "Enemy";

            // Add EnemyBot stub or tag so DamageSystem and SniperHitQuery recognize opponent
            gameObject.tag = "Enemy";
        }

        private void BuildOpticGlint()
        {
            var glintGo = new GameObject("OpticGlint");
            glintGo.transform.SetParent(transform);
            glintGo.transform.localPosition = new Vector3(0.25f, 1.48f, 0.9f);

            opticGlint = glintGo.AddComponent<LineRenderer>();
            opticGlint.positionCount = 2;
            opticGlint.startWidth = 0.65f;
            opticGlint.endWidth = 0.65f;
            opticGlint.useWorldSpace = true;

            var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            mat.color = new Color(1.0f, 0.95f, 0.70f, 0.95f);
            opticGlint.sharedMaterial = mat;
            opticGlint.enabled = false;
        }

        private void BuildTracer()
        {
            var trGo = new GameObject("BulletTracer");
            trGo.transform.SetParent(transform);
            tracerLine = trGo.AddComponent<LineRenderer>();
            tracerLine.positionCount = 2;
            tracerLine.startWidth = 0.08f;
            tracerLine.endWidth = 0.03f;
            tracerLine.useWorldSpace = true;

            var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            mat.color = new Color(1.0f, 0.45f, 0.1f, 0.9f);
            tracerLine.sharedMaterial = mat;
            tracerLine.enabled = false;
        }

        private void Update()
        {
            if (IsDead) return;

            // Smooth position & rotation interpolation
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * LerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * LerpSpeed);

            // Scope Glint Evaluation
            UpdateOpticGlint();
        }

        private void UpdateOpticGlint()
        {
            if (opticGlint == null) return;

            Camera mainCam = Camera.main;
            if (mainCam == null || !remoteIsAiming || IsDead)
            {
                opticGlint.enabled = false;
                IsAimingAtPlayer = false;
                return;
            }

            Vector3 toPlayer = (mainCam.transform.position - transform.position).normalized;
            float facingDot = Vector3.Dot(transform.forward, toPlayer);

            // Opponent is aiming and facing local player within ~25 degrees
            if (facingDot > 0.88f)
            {
                IsAimingAtPlayer = true;
                Vector3 opticPos = transform.position + Vector3.up * (remoteIsCrouching ? 1.0f : 1.45f) + transform.forward * 0.5f;

                // Pulsing flash effect
                float pulse = 0.6f + Mathf.PingPong(Time.time * 6f, 0.4f);
                opticGlint.startWidth = 0.5f * pulse;
                opticGlint.endWidth = 0.5f * pulse;

                Vector3 camRight = mainCam.transform.right * (0.25f * pulse);
                opticGlint.SetPosition(0, opticPos - camRight);
                opticGlint.SetPosition(1, opticPos + camRight);
                opticGlint.enabled = true;
            }
            else
            {
                IsAimingAtPlayer = false;
                opticGlint.enabled = false;
            }
        }

        public void ApplyNetworkState(Vector3 pos, float yaw, float pitch, bool isAiming, bool isCrouching, float hp)
        {
            targetPosition = pos;
            targetRotation = Quaternion.Euler(0, yaw, 0);
            targetPitch = pitch;
            remoteIsAiming = isAiming;
            remoteIsCrouching = isCrouching;
            CurrentHealth = hp;

            if (rifleObject != null)
            {
                rifleObject.transform.localRotation = Quaternion.Euler(-pitch, 0, 0);
                rifleObject.transform.localPosition = new Vector3(0.25f, isCrouching ? 0.95f : 1.35f, 0.45f);
            }
        }

        public void PlayRemoteFire(Vector3 muzzle, Vector3 dir, int weaponIndex)
        {
            // Tracer ray effect
            if (tracerLine != null)
            {
                StopAllCoroutines();
                StartCoroutine(TracerRoutine(muzzle, muzzle + dir * 300f));
            }

            // Gunshot audio
            if (audioSource != null)
            {
                AudioClip shotSound = Resources.Load<AudioClip>("Audio/SniperShot") ?? Resources.Load<AudioClip>("Audio/Gunshot");
                if (shotSound != null)
                {
                    audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                    audioSource.PlayOneShot(shotSound, 1.0f);
                }
            }

            // Supersonic crack if shot passes near local player camera
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 camPos = mainCam.transform.position;
                Vector3 toCam = camPos - muzzle;
                float proj = Vector3.Dot(toCam, dir);
                if (proj > 0)
                {
                    Vector3 closestPoint = muzzle + dir * proj;
                    float missDist = Vector3.Distance(closestPoint, camPos);
                    if (missDist < 5.0f)
                    {
                        AudioClip whizClip = ProceduralAudio.CreateBulletWhiz();
                        if (whizClip != null) AudioSource.PlayClipAtPoint(whizClip, closestPoint, 0.8f);
                    }
                }
            }
        }

        private IEnumerator TracerRoutine(Vector3 start, Vector3 end)
        {
            tracerLine.SetPosition(0, start);
            tracerLine.SetPosition(1, end);
            tracerLine.enabled = true;
            float dur = 0.12f;
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                yield return null;
            }
            tracerLine.enabled = false;
        }

        public void TakeLocalDamage(byte hitZone, float damage, Vector3 point)
        {
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            OnLocallyDamaged?.Invoke(hitZone, damage, point);

            if (CurrentHealth <= 0)
            {
                Die();
            }
        }

        public void Die()
        {
            CurrentHealth = 0;
            if (opticGlint != null) opticGlint.enabled = false;
            if (tracerLine != null) tracerLine.enabled = false;

            // Trigger collapse / ragdoll
            if (visualRoot != null)
            {
                visualRoot.transform.localPosition = Vector3.down * 0.3f;
                visualRoot.transform.localRotation = Quaternion.Euler(85f, 0, 0);
            }
        }

        public void Respawn(Vector3 newPos, Quaternion newRot)
        {
            CurrentHealth = 100f;
            transform.position = newPos;
            transform.rotation = newRot;
            targetPosition = newPos;
            targetRotation = newRot;

            if (visualRoot != null)
            {
                visualRoot.transform.localPosition = Vector3.zero;
                visualRoot.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
