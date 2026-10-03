using System;
using System.Collections;
using UnityEngine;

namespace GeoSniper
{
    public class ExplosiveProp : MonoBehaviour
    {
        public static event Action<Vector3, string> OnAccidentKillNotice;

        public float maxHealth = 50f;
        public float explosionRadius = 9.5f;
        public float explosionDamage = 220f;
        public float explosionForce = 1400f;
        public bool isDetonated = false;

        private float currentHealth;
        private static AudioClip explosionClip;
        bool healthInitialized;

        void Awake()
        {
            currentHealth = maxHealth;
            if (explosionClip == null)
            {
                explosionClip = ProceduralAudio.CreateVehicleExplosion();
            }
        }

        public void TakeDamage(float damage, Vector3 hitPoint)
        {
            if (isDetonated) return;
            // AddComponent invokes Awake before the spawner sets tank/barrel health.
            if(!healthInitialized) {currentHealth=maxHealth;healthInitialized=true;}
            currentHealth -= damage;
            if (currentHealth <= 0)
            {
                StartCoroutine(DetonateSequence(hitPoint));
            }
        }

        private IEnumerator DetonateSequence(Vector3 hitPoint)
        {
            if (isDetonated) yield break;
            isDetonated = true;

            // Micro-delay for fuel tank ignition pop
            yield return new WaitForSeconds(0.08f);

            Vector3 epicenter = transform.position + Vector3.up * 0.8f;

            // 1. Audio
            AudioSource.PlayClipAtPoint(explosionClip, epicenter, 1.0f);

            // 2. Visual Particle System (Zero primitive 3D cubes/spheres)
            SpawnExplosionFX(epicenter);

            // 3. Char / Scorch Material
            ApplyCharredAppearance();

            // 4. Radial Damage & Physics Knockback
            int enemiesKilled = 0;
            Collider[] hits = Physics.OverlapSphere(epicenter, explosionRadius, ~0, QueryTriggerInteraction.Ignore);
            var damaged=new System.Collections.Generic.HashSet<CombatActor>();
            var chained=new System.Collections.Generic.HashSet<ExplosiveProp>();
            var pushed=new System.Collections.Generic.HashSet<Rigidbody>();
            foreach (var col in hits)
            {
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

                // Chain-reaction with other explosive props (e.g. nearby barrels)
                var chainProp = col.GetComponentInParent<ExplosiveProp>();
                if (chainProp != null && chainProp != this && !chainProp.isDetonated && chained.Add(chainProp))
                {
                    chainProp.TakeDamage(100f, epicenter);
                }

                // Enemy & Civilian actors
                var enemy = col.GetComponentInParent<EnemyBot>();
                var actor = col.GetComponentInParent<CombatActor>();
                if (actor != null && !actor.IsDead && damaged.Add(actor))
                {
                    float dist = Vector3.Distance(epicenter, col.transform.position);
                    float falloff = Mathf.Clamp01(1f - (dist / explosionRadius));
                    float damage = explosionDamage * (0.4f + 0.6f * falloff);

                    bool wasAlive = !actor.IsDead;
                    actor.Damage(damage);

                    if (wasAlive && actor.IsDead && enemy != null)
                    {
                        enemiesKilled++;
                    }
                }

                // Physics rigidbodies
                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic && pushed.Add(rb))
                {
                    rb.AddExplosionForce(explosionForce, epicenter, explosionRadius, 2.0f, ForceMode.Impulse);
                }
            }

            if (enemiesKilled > 0)
            {
                OnAccidentKillNotice?.Invoke(epicenter, $"💥 EXPLOSIVE ACCIDENT KILL (x{enemiesKilled}) +{enemiesKilled * 250} XP");
            }
        }

        private void SpawnExplosionFX(Vector3 pos)
        {
            var fxObj = new GameObject("ExplosionFX_" + gameObject.name);
            fxObj.transform.position = pos;

            var ps = fxObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1.8f;
            main.loop = false;
            main.startLifetime = 1.2f;
            main.startSpeed = 16f;
            main.startSize = 3.5f;
            main.startColor = new Color(1f, 0.45f, 0.08f, 0.95f);
            main.gravityModifier = -0.15f; // Smoke rises

            var emission = ps.emission;
            emission.enabled = true;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 45) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.2f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0.0f),
                    new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.3f),
                    new GradientColorKey(new Color(0.15f, 0.15f, 0.15f), 0.7f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.8f, 0.4f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.5f);
            curve.AddKey(0.2f, 1.6f);
            curve.AddKey(1f, 3.2f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var psr = fxObj.GetComponent<ParticleSystemRenderer>();
            var pMat = GetExplosionMaterial();
            if (pMat != null) psr.sharedMaterial = pMat;

            ps.Play();
            Destroy(fxObj, 3.5f);
        }

        private static Material cachedExplosionMat;
        private static Material cachedSootMat;

        private static Material GetExplosionMaterial()
        {
            if (cachedExplosionMat == null)
            {
                var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Sprites/Default");
                if (shader != null) cachedExplosionMat = new Material(shader);
            }
            return cachedExplosionMat;
        }

        private static Material GetSootMaterial()
        {
            if (cachedSootMat == null)
            {
                cachedSootMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                cachedSootMat.color = new Color(0.12f, 0.11f, 0.10f);
                cachedSootMat.SetFloat("_Glossiness", 0.05f);
            }
            return cachedSootMat;
        }

        private void ApplyCharredAppearance()
        {
            var sootMat = GetSootMaterial();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = sootMat;
            }
        }
    }
}
