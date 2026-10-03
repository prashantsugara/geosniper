using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public class BallisticsSystem : MonoBehaviour
    {
        public static event System.Action<Vector3> OnGunshot;
        public static event System.Action<Vector3,float> OnShotNoise;
        public event System.Action<bool> ShotResolved;
        public static Vector3 Wind = new Vector3(1.8f, 0f, 0.4f);
        
        public BallisticsConfig config;
        private List<EnemyBot> enemies;
        private Transform player;
        private Camera cameraView;
        private SniperPresentation weapon;
        System.Action restoreBulletCamera;
        void OnDisable()
        {
            StopAllCoroutines();
            restoreBulletCamera?.Invoke();
        }

        public void Initialize(List<EnemyBot> enemyList, Transform playerTransform, Camera cam, SniperPresentation weaponPresentation)
        {
            enemies = enemyList;
            player = playerTransform;
            cameraView = cam;
            weapon = weaponPresentation;
        }

        public void Fire()
        {
            if (config == null || cameraView == null || isBulletCamActive || config.bulletSpeed<=0 || config.maxRange<=0) return;

            // Start at the eye, not the near clipping plane, so nearby cover still counts.
            Ray ray = new Ray(cameraView.transform.position,cameraView.transform.forward);
            Vector3 muzzle = cameraView.transform.position + cameraView.transform.forward * 1.5f + cameraView.transform.right * 0.15f - cameraView.transform.up * 0.1f;

            if (config.isArcadeMode)
            {
                FireArcade(ray, muzzle);
            }
            else
            {
                StartCoroutine(SimulateProjectile(ray, muzzle));
            }
            
            OnGunshot?.Invoke(muzzle);
            float noise = UrbanCombatMission.IsSoundMasked ? 0f : config.noiseRadius;
            OnShotNoise?.Invoke(muzzle, noise);
        }

        private void FireArcade(Ray ray, Vector3 muzzle)
        {
            bool hasHit = SniperHitQuery.TryCastAimAssist(ray, enemies, player, cameraView.transform, out var hit, config.maxRange);
            var hitCollider = hit.Collider;
            Vector3 impactPoint = ray.origin + ray.direction * config.maxRange;
            bool hitEnemy = false;

            if (hasHit)
            {
                impactPoint = hit.Point;
                float damage = config.baseDamage;
                float remaining = config.maxRange;
                for (int layer=0; layer<4 && SniperHitQuery.TryPenetrate(hit,ray.direction,out var exit); layer++)
                {
                    SpawnGlassShardsFX(hit.Point, ray.direction);
                    remaining -= hit.Distance + Vector3.Distance(hit.Point,exit);
                    damage *= .65f;
                    if (!SniperHitQuery.TryCast(new Ray(exit,ray.direction), enemies, player, cameraView.transform, out var next, remaining))
                    { hitCollider=null; impactPoint=exit+ray.direction*Mathf.Max(0,remaining); break; }
                    hit=next; hitCollider=hit.Collider; impactPoint=hit.Point;
                }
                var info = DamageSystem.ProcessHit(hitCollider, damage, impactPoint, config.headshotMultiplier, config.limbMultiplier);
                var bot = hitCollider != null ? hitCollider.GetComponentInParent<EnemyBot>() : null;
                hitEnemy = bot != null;

                if (hitEnemy && ShouldTriggerBulletCam(bot, info, muzzle, impactPoint))
                {
                    StartCoroutine(TriggerBulletCam(muzzle, impactPoint, hitCollider));
                }
                else if (hitEnemy && bot != null && bot.Actor != null && bot.Actor.IsDead)
                {
                    TimeScaleController.SetHitCam(0.14f);
                }
            }
            if (!hitEnemy)
            {
                SniperPresentation.SpawnSurfaceImpact(impactPoint,hitCollider);
                EnemyBot.NotifyMissedShot(muzzle, impactPoint);
                weapon?.RecordMissImpact(impactPoint);
            }
            weapon?.ShotWithTracer(muzzle, impactPoint, hitEnemy);
            ShotResolved?.Invoke(hitEnemy);
        }

        public static void SpawnGlassShardsFX(Vector3 pos, Vector3 forward)
        {
            var fx = new GameObject("GlassShardsFX");
            fx.transform.position = pos;
            fx.transform.rotation = Quaternion.LookRotation(forward);

            var ps = fx.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.25f;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 16f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.88f, 0.96f, 1f, 0.85f);
            main.gravityModifier = 1.6f;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 22) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.05f;

            var psr = fx.GetComponent<ParticleSystemRenderer>();
            var pMat = GetGlassMaterial();
            if (pMat != null) psr.sharedMaterial = pMat;

            ps.Play();
            Destroy(fx, 0.5f);
        }

        static Material cachedGlassMat;
        static Material GetGlassMaterial()
        {
            if (cachedGlassMat == null)
            {
                var sh = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Sprites/Default");
                if (sh != null)
                {
                    cachedGlassMat = new Material(sh);
                    cachedGlassMat.color = new Color(0.9f, 0.98f, 1f, 0.9f);
                }
            }
            return cachedGlassMat;
        }

        public static bool isBulletCamActive = false;
        public static float bulletCamElapsed = 0f;
        private static int bulletCamAngleIndex = 0;

        private static Mesh cachedBulletMesh;
        private static Mesh CreateProceduralBulletMesh()
        {
            if (cachedBulletMesh != null) return cachedBulletMesh;

            Mesh m = new Mesh { name = "Procedural50BMG" };
            int radialSteps = 24;
            // .50 BMG lathe profile (z along length, r radius in metres)
            // Length: 0.16m, diameter at bearing surface: 0.038m (radius 0.019m)
            var profile = new (float z, float r)[]
            {
                (-0.070f, 0.0001f), // base center cap
                (-0.070f, 0.0130f), // boat-tail base
                (-0.050f, 0.0190f), // boat-tail forward shoulder
                (-0.015f, 0.0190f), // bearing surface / cannelure
                ( 0.010f, 0.0190f), // forward bearing surface tangent
                ( 0.035f, 0.0175f), // ogive curve 1
                ( 0.055f, 0.0145f), // ogive curve 2
                ( 0.072f, 0.0095f), // ogive curve 3
                ( 0.086f, 0.0035f), // spitzer tip neck
                ( 0.090f, 0.0001f)  // sharp tip point (+Z forward)
            };

            int rings = profile.Length;
            int totalVerts = rings * (radialSteps + 1);
            Vector3[] verts = new Vector3[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];
            List<int> tris = new List<int>();

            for (int ring = 0; ring < rings; ring++)
            {
                float z = profile[ring].z;
                float r = profile[ring].r;
                float v = Mathf.InverseLerp(-0.070f, 0.090f, z);

                for (int seg = 0; seg <= radialSteps; seg++)
                {
                    float angle = (float)seg / radialSteps * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    int idx = ring * (radialSteps + 1) + seg;
                    verts[idx] = new Vector3(cos * r, sin * r, z);
                    uvs[idx] = new Vector2((float)seg / radialSteps, v);
                }
            }

            for (int ring = 0; ring < rings - 1; ring++)
            {
                for (int seg = 0; seg < radialSteps; seg++)
                {
                    int curr = ring * (radialSteps + 1) + seg;
                    int next = curr + 1;
                    int above = (ring + 1) * (radialSteps + 1) + seg;
                    int aboveNext = above + 1;

                    tris.Add(curr);
                    tris.Add(next);
                    tris.Add(above);

                    tris.Add(next);
                    tris.Add(aboveNext);
                    tris.Add(above);
                }
            }

            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            cachedBulletMesh = m;
            return cachedBulletMesh;
        }

        private static Material cachedBrassMat;
        private static Material cachedTrailMat;
        private static Material cachedShockwaveMat;
        private static Material cachedBloodMat;

        private static Material GetBrassMaterial()
        {
            if (cachedBrassMat == null)
            {
                cachedBrassMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                cachedBrassMat.color = new Color(0.98f, 0.78f, 0.32f);
                cachedBrassMat.SetFloat("_Metallic", 0.75f);
                cachedBrassMat.SetFloat("_Glossiness", 0.88f);
                cachedBrassMat.EnableKeyword("_EMISSION");
                cachedBrassMat.SetColor("_EmissionColor", new Color(0.24f, 0.16f, 0.05f));
                cachedBrassMat.mainTexture = CreateRiflingTexture();
            }
            return cachedBrassMat;
        }

        private static Texture2D CreateRiflingTexture()
        {
            int w = 128, h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            Color copper = new Color(0.88f, 0.58f, 0.28f);
            Color brass = new Color(0.96f, 0.76f, 0.35f);
            Color groove = new Color(0.42f, 0.26f, 0.12f);
            Color drivingBand = new Color(0.95f, 0.45f, 0.15f);

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                bool isBand = v > 0.18f && v < 0.32f;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    // 6 spiral rifling groove lands etched along the bullet jacket
                    float spiral = Mathf.Repeat(u * 6f + v * 1.8f, 1f);
                    bool inGroove = spiral < 0.22f;

                    Color c = Color.Lerp(copper, brass, Mathf.Sin(u * Mathf.PI * 4f) * 0.2f + 0.5f);
                    if (isBand) c = Color.Lerp(c, drivingBand, 0.85f);
                    if (inGroove) c = Color.Lerp(c, groove, 0.80f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Material GetTrailMaterial()
        {
            if (cachedTrailMat == null)
            {
                cachedTrailMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
                cachedTrailMat.color = new Color(0.85f, 0.92f, 1.0f, 0.38f);
            }
            return cachedTrailMat;
        }

        private static Material GetShockwaveMaterial()
        {
            if (cachedShockwaveMat == null)
            {
                var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Sprites/Default");
                if (shader != null) cachedShockwaveMat = new Material(shader);
            }
            return cachedShockwaveMat;
        }

        private static Material GetBloodMaterial()
        {
            if (cachedBloodMat == null)
            {
                var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Sprites/Default");
                if (shader != null) cachedBloodMat = new Material(shader);
            }
            return cachedBloodMat;
        }

        private IEnumerator TriggerBulletCam(Vector3 startPos, Vector3 targetPos, Collider targetCollider)
        {
            if (isBulletCamActive || cameraView == null) yield break;
            isBulletCamActive = true;
            weapon?.SetVisible(false);
            weapon?.HideTracer();

            TimeScaleController.SetBulletCam(0.05f);

            Camera cam = cameraView;
            var hiddenChildren=new List<GameObject>();
            for (int i = 0; i < cam.transform.childCount; i++)
            {
                var child = cam.transform.GetChild(i);
                if (child.name.Contains("Flash") || child.name.Contains("Muzzle") || child.name.Contains("Sparks") || child.name.Contains("Weapon"))
                {
                    if(child.gameObject.activeSelf) {hiddenChildren.Add(child.gameObject);child.gameObject.SetActive(false);}
                }
            }
            Vector3 worldOrigPos = cam.transform.position;
            Quaternion worldOrigRot = cam.transform.rotation;
            Transform origParent = cam.transform.parent;
            Vector3 origLocalPos = cam.transform.localPosition;
            Quaternion origLocalRot = cam.transform.localRotation;
            float origFOV = cam.fieldOfView;

            GameObject bulletObj = null;
            Light camLight = null;
            bool restored=false;
            restoreBulletCamera=()=>
            {
                if(restored) return;
                restored=true;
                if(bulletObj!=null) Destroy(bulletObj);
                if(camLight!=null) Destroy(camLight);
                if(cam!=null)
                {
                    cam.transform.SetParent(origParent,false);
                    cam.transform.localPosition=origParent!=null?origLocalPos:worldOrigPos;
                    cam.transform.localRotation=origParent!=null?origLocalRot:worldOrigRot;
                    cam.fieldOfView=origFOV;
                }
                foreach(var child in hiddenChildren) if(child!=null) child.SetActive(true);
                weapon?.SetVisible(true);
                TimeScaleController.ClearBulletCam();
                isBulletCamActive=false;restoreBulletCamera=null;
            };

            try
            {
                // Detach camera from player so player movement/sway cannot affect it
                cam.transform.SetParent(null, true);
                cam.fieldOfView = 34f; // Telephoto cinematic lens for clear, dramatic close-up

                // Dedicated cinematic keylight to make copper/brass jacket gleam clearly in any scene lighting
                camLight = cam.gameObject.AddComponent<Light>();
                camLight.type = LightType.Point;
                camLight.range = 8f;
                camLight.intensity = 2.4f;
                camLight.color = new Color(1.0f, 0.94f, 0.82f);

                bulletObj = new GameObject("BulletCam_Bullet");
                Transform bulletVisual = new GameObject("BulletVisual").transform;
                bulletVisual.SetParent(bulletObj.transform, false);

                var bulletMeshObj = new GameObject("Procedural50CalBullet");
                bulletMeshObj.transform.SetParent(bulletVisual, false);
                var mf = bulletMeshObj.AddComponent<MeshFilter>();
                var mr = bulletMeshObj.AddComponent<MeshRenderer>();
                mf.sharedMesh = CreateProceduralBulletMesh();
                mr.sharedMaterial = GetBrassMaterial();
                bulletVisual.localRotation = Quaternion.identity;

                Vector3 flightDir = (targetPos - startPos).normalized;
                Vector3 sideDir = Vector3.Cross(flightDir, Vector3.up).normalized;
                if (sideDir.sqrMagnitude < 0.001f) sideDir = Vector3.right;
                Vector3 upDir = Vector3.Cross(sideDir, flightDir).normalized;

                bulletObj.transform.position = startPos;
                // Align the bullet container perfectly along the flight direction (Z-forward)
                bulletObj.transform.rotation = Quaternion.LookRotation(flightDir);

                // Supersonic vapor trail attached to boat-tail base of bullet
                var trailObj = new GameObject("VaporTrail");
                trailObj.transform.SetParent(bulletObj.transform, false);
                trailObj.transform.localPosition = new Vector3(0f, 0f, -0.07f); // Base of projectile along -Z flight axis
                var trail = trailObj.AddComponent<TrailRenderer>();
                trail.time = 0.25f;
                trail.startWidth = 0.09f;
                trail.endWidth = 0.01f;
                trail.sharedMaterial = GetTrailMaterial();

                // Select 1 of 3 distinct cinematic angles per headshot
                int angleMode = (bulletCamAngleIndex++) % 3;
                Vector3 camOffset;
                Vector3 lookOffset;

                switch (angleMode)
                {
                    case 0:
                        // Front-3/4 Lead: Flying ahead and to the side, looking back at spinning copper tip
                        camOffset = flightDir * 0.28f - sideDir * 0.26f + upDir * 0.08f;
                        lookOffset = Vector3.zero;
                        break;
                    case 1:
                        // Close Flank Profile: Tracking closely along bullet body
                        camOffset = -sideDir * 0.32f + upDir * 0.05f;
                        lookOffset = flightDir * 0.05f;
                        break;
                    default:
                        // High Over-Shoulder Chase: Behind bullet with target framed ahead
                        camOffset = -flightDir * 0.45f - sideDir * 0.16f + upDir * 0.12f;
                        lookOffset = flightDir * 0.5f;
                        break;
                }

                float totalDist = Vector3.Distance(startPos, targetPos);
                float flightDuration = Mathf.Clamp(totalDist / 42f, 1.3f, 2.1f);
                float elapsed = 0f;

                AudioSource audio = cam.GetComponent<AudioSource>();
                if (audio != null) audio.PlayOneShot(ProceduralAudio.CreateBulletWhiz(), 0.85f);

                bool CheckSkip()
                {
                    return Input.GetKeyDown(KeyCode.Space) ||
                           Input.GetMouseButtonDown(0) ||
                           (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
                }

                while (elapsed < flightDuration && bulletObj != null)
                {
                    if (CheckSkip()) break;
                    elapsed += Time.unscaledDeltaTime;
                    bulletCamElapsed = elapsed;
                    float progress = Mathf.Clamp01(elapsed / flightDuration);

                    // Linear bullet flight (constant speed) instead of ease-in/ease-out
                    float p = progress;
                    Vector3 bulletPos = Vector3.Lerp(startPos, targetPos, p);
                    bulletObj.transform.position = bulletPos;

                    // True rifling spin on longitudinal axis (tip-to-tail forward axis)
                    if (bulletVisual != null)
                        bulletVisual.Rotate(0f, 0f, 3200f * Time.unscaledDeltaTime, Space.Self);

                    if (elapsed < 0.2f)
                    {
                        // Smooth transition from player scope to bullet cam
                        float blend = Mathf.SmoothStep(0f, 1f, elapsed / 0.2f);
                        Vector3 targetCamPos = bulletPos + camOffset;
                        Quaternion targetLook = Quaternion.LookRotation((bulletPos + lookOffset) - targetCamPos);
                        
                        cam.transform.position = Vector3.Lerp(worldOrigPos, targetCamPos, blend);
                        cam.transform.rotation = Quaternion.Slerp(worldOrigRot, targetLook, blend);
                    }
                    // In final 15% (near target's head), enter extreme bullet-time slowmo
                    else if (progress > 0.85f)
                    {
                        TimeScaleController.SetBulletCam(0.015f);

                        // Transition camera to frame both bullet tip and target head
                        Vector3 closeCam = targetPos - flightDir * 0.75f - sideDir * 0.45f + Vector3.up * 0.16f;
                        cam.transform.position = Vector3.Lerp(cam.transform.position, closeCam, Time.unscaledDeltaTime * 10f);
                        cam.transform.LookAt(Vector3.Lerp(bulletPos, targetPos, 0.6f));
                    }
                    else
                    {
                        Vector3 targetCamPos = bulletPos + camOffset;
                        cam.transform.position = Vector3.Lerp(cam.transform.position, targetCamPos, Time.unscaledDeltaTime * 18f);
                        cam.transform.LookAt(bulletPos + lookOffset);
                    }

                    yield return null;
                }

                // Phase 3: Impact moment: micro-freeze / ultra-slowmo & anatomical X-Ray
                TimeScaleController.SetBulletCam(0.008f);
                if (targetPos != Vector3.zero)
                {
                    if (targetCollider != null && weapon != null)
                    {
                        var bot = targetCollider.GetComponentInParent<EnemyBot>();
                        if (bot != null)
                        {
                            if (bot.Actor != null && !bot.Actor.IsDead)
                            {
                                bot.Actor.Damage(bot.Actor.Health + 100f, targetPos);
                            }
                            weapon.TriggerXRayHitCam(bot.transform, targetPos);
                        }
                    }
                    SpawnBloodSplatter(targetPos, flightDir);
                    SniperPresentation.SpawnImpactSparks(targetPos);
                    SpawnHeadshotShockwave(targetPos, flightDir);

                    if (audio != null)
                    {
                        audio.PlayOneShot(ProceduralAudio.CreateHeadshotPing(), 1.0f);
                        audio.PlayOneShot(ProceduralAudio.CreateBodyHitThud(), 0.9f);
                    }
                }

                // Micro-freeze impact hold (0.32s realtime)
                float impactHold = 0.32f;
                float impactElapsed = 0f;
                while (impactElapsed < impactHold)
                {
                    if (CheckSkip()) break;
                    impactElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                // Phase 4: Slow-mo follow-through orbiting collapsing ragdoll body (1.35s realtime at 0.12x game time)
                TimeScaleController.SetBulletCam(0.12f);
                float orbitDuration = 1.35f;
                float orbitElapsed = 0f;
                Vector3 orbitCenter = targetPos;
                Vector3 initialCamOffset = cam.transform.position - orbitCenter;
                if (initialCamOffset.magnitude < 1.2f) initialCamOffset = (cam.transform.position - orbitCenter).normalized * 2.2f;

                while (orbitElapsed < orbitDuration)
                {
                    if (CheckSkip()) break;
                    orbitElapsed += Time.unscaledDeltaTime;
                    float orbitFrac = orbitElapsed / orbitDuration;

                    // Smooth cinematic arc around collapsing body
                    Quaternion rot = Quaternion.AngleAxis(Time.unscaledDeltaTime * 24f, Vector3.up);
                    initialCamOffset = rot * initialCamOffset;
                    cam.transform.position = orbitCenter + initialCamOffset;
                    cam.transform.LookAt(orbitCenter + Vector3.up * Mathf.Lerp(0f, -0.45f, orbitFrac));

                    yield return null;
                }
            }
            finally
            {
                restoreBulletCamera?.Invoke();
            }
        }

        public static void SpawnHeadshotShockwave(Vector3 pos, Vector3 dir)
        {
            var fx = new GameObject("HeadshotShockwaveFX");
            fx.transform.position = pos;
            fx.transform.rotation = Quaternion.LookRotation(dir);

            var ps = fx.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = 0.25f;
            main.startSpeed = 8f;
            main.startSize = 0.6f;
            main.startColor = new Color(1f, 0.4f, 0.15f, 0.85f);

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 16) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.2f);
            curve.AddKey(1f, 2.2f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0f), new GradientColorKey(new Color(0.9f, 0.1f, 0.05f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var psr = fx.GetComponent<ParticleSystemRenderer>();
            var mat = GetShockwaveMaterial();
            if (mat != null) psr.sharedMaterial = mat;

            ps.Play();
            Destroy(fx, 0.6f);
        }

        public static void SpawnBloodSplatter(Vector3 position, Vector3? impactDirection = null)
        {
            Vector3 dir = impactDirection ?? Vector3.up;
            var fx = new GameObject("BloodSplatterFX");
            fx.transform.position = position;
            fx.transform.rotation = Quaternion.LookRotation(dir);

            var ps = fx.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 6f;
            main.startSize = 0.22f;
            main.gravityModifier = 0.85f;
            main.startColor = new Color(0.55f, 0.02f, 0.02f, 0.95f);

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 28) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.08f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.65f, 0.02f, 0.02f), 0f), new GradientColorKey(new Color(0.35f, 0.01f, 0.01f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.2f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.6f);
            curve.AddKey(0.3f, 1.2f);
            curve.AddKey(1f, 0.3f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var psr = fx.GetComponent<ParticleSystemRenderer>();
            var mat = GetBloodMaterial();
            if (mat != null) psr.sharedMaterial = mat;

            ps.Play();
            Destroy(fx, 0.85f);
        }

        private IEnumerator SimulateProjectile(Ray ray, Vector3 muzzle)
        {
            Vector3 position = ray.origin;
            // A round retains its fired stats when the player switches rifles.
            float damage = config.baseDamage, headMultiplier = config.headshotMultiplier, limbMultiplier = config.limbMultiplier;
            float gravity = config.gravityMultiplier;
            int penetrations = 0;
            Vector3 velocity = ray.direction * config.bulletSpeed;
            // Auto-calibrate scope zero elevation to aimed target distance so bullet tracks crosshair accurately
            float zeroDist = 100f;
            if (Physics.Raycast(ray, out var rangeHit, config.maxRange))
                zeroDist = Mathf.Clamp(rangeHit.distance, 40f, 400f);
            float zeroTime = zeroDist / Mathf.Max(100f, config.bulletSpeed);
            Vector3 zeroComp = -0.5f * Physics.gravity * config.gravityMultiplier * zeroTime;
            velocity += zeroComp;

            float timeToLive = config.maxRange / config.bulletSpeed;
            float elapsedTime = 0f;
            
            weapon?.Shot(); // Play sound/flash immediately

            while (elapsedTime < timeToLive)
            {
                float dt = Mathf.Min(Time.deltaTime,timeToLive-elapsedTime);
                Vector3 nextPosition = position + velocity * dt;
                
                // Gravity and Atmospheric Wind Drift
                velocity += (Physics.gravity * gravity + Wind * 0.15f) * dt;
                
                // Prioritize collision query along bullet travel step using AimAssist for micro-pixel tolerance
                var step = nextPosition - position;
                if (step.sqrMagnitude > 0 && SniperHitQuery.TryCastAimAssist(new Ray(position, step.normalized), enemies, player, cameraView.transform, out var hit, step.magnitude))
                {
                    var enemy = hit.Collider.GetComponentInParent<EnemyBot>();
                    bool hitEnemy = (enemy != null);

                    if (!hitEnemy && penetrations < 4 && SniperHitQuery.TryPenetrate(hit,step.normalized,out var exit))
                    {
                        SpawnGlassShardsFX(hit.Point,step.normalized);
                        position=exit;
                        velocity*=.92f;
                        damage*=.65f;
                        penetrations++;
                        elapsedTime+=Mathf.Max(dt,Vector3.Distance(hit.Point,exit)/Mathf.Max(1f,velocity.magnitude));
                        yield return null;
                        continue;
                    }
                    var info = DamageSystem.ProcessHit(hit.Collider, damage, hit.Point, headMultiplier, limbMultiplier);

                    if (hitEnemy)
                    {
                        SpawnBloodSplatter(hit.Point, step.normalized);
                        if (ShouldTriggerBulletCam(enemy, info, muzzle, hit.Point))
                        {
                            StartCoroutine(TriggerBulletCam(muzzle, hit.Point, hit.Collider));
                        }
                        else if (enemy != null && enemy.Actor != null && enemy.Actor.IsDead)
                        {
                            TimeScaleController.SetHitCam(0.14f);
                        }
                    }
                    else
                    {
                        SniperPresentation.SpawnSurfaceImpact(hit.Point,hit.Collider);
                        EnemyBot.NotifyMissedShot(muzzle, hit.Point);
                        weapon?.RecordMissImpact(hit.Point);
                    }

                    weapon?.ResolveProjectileVisual(muzzle, hit.Point, hitEnemy);
                    ShotResolved?.Invoke(hitEnemy);
                    yield break;
                }
                
                position = nextPosition;
                elapsedTime += dt;
                yield return null;
            }
            
            // Missed everything
            EnemyBot.NotifyMissedShot(muzzle, position);
            weapon?.RecordMissImpact(position);
            weapon?.ResolveProjectileVisual(muzzle, position, false);
            ShotResolved?.Invoke(false);
        }

        private static float lastBulletCamTime = -30f;
        private bool ShouldTriggerBulletCam(EnemyBot bot, DamageInfo info, Vector3 muzzle, Vector3 impact)
        {
            if (bot == null) return false;
            bool isKill = bot.Actor != null && bot.Actor.IsDead;
            if (!isKill) return false;

            float dist = Vector3.Distance(muzzle, impact);
            int aliveCount = 0;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i] != null && enemies[i].Actor != null && !enemies[i].Actor.IsDead)
                        aliveCount++;
                }
            }

            // 1. Final mission kill / wave-ender always triggers full cinematic bullet cam!
            if (aliveCount == 0) { lastBulletCamTime = Time.time; return true; }

            // 2. High priority hostile threats (Counter-snipers, VIP assassins)
            if (bot.isCounterSniper || bot.isVipAttacker || bot.currentRole == CombatRole.Sniper)
            {
                lastBulletCamTime = Time.time;
                return true;
            }

            // 3. Extreme long-range sniper shots (>95m)
            if (dist >= 95f) { lastBulletCamTime = Time.time; return true; }

            // 4. Rate-limit intermediate headshots to once every 12s so intense firefights stay fast
            if (info.isHeadshot && (Time.time - lastBulletCamTime >= 12f))
            {
                lastBulletCamTime = Time.time;
                return true;
            }

            return false;
        }

        void OnGUI()
        {
            if (!isBulletCamActive) return;

            float barHeight = Screen.height * 0.11f;
            Color oldCol = GUI.color;

            // Cinematic letterbox black bars
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, barHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - barHeight, Screen.width, barHeight), Texture2D.whiteTexture);

            // Skip prompt in bottom letterbox bar
            GUI.color = new Color(0.9f, 0.9f, 0.9f, 0.75f + Mathf.PingPong(Time.unscaledTime * 1.8f, 0.25f));
            GUIStyle skipStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.020f), 11, 16),
                fontStyle = FontStyle.Bold
            };
            skipStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width - 320, Screen.height - barHeight + (barHeight - 26f) * 0.5f, 300, 26), "SPACE / TAP TO SKIP  ►►", skipStyle);

            GUI.color = oldCol;
        }

        void OnDestroy()
        {
            restoreBulletCamera?.Invoke();
            TimeScaleController.ClearBulletCam();
        }
    }
}
