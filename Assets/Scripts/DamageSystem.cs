using System;
using UnityEngine;

namespace GeoSniper
{
    public struct DamageInfo
    {
        public float baseDamage;
        public float multiplier;
        public Vector3 hitPoint;
        public string bodyPart;
        public bool isHeadshot;
        
        public float TotalDamage => baseDamage * multiplier;
    }

    public static class DamageSystem
    {
        public static event Action<DamageInfo> OnDamageDealt;
        public static event Action<EnemyBot> OnEnemyKilled;
        public static event Action<CivilianBot> OnCivilianKilled;

        public static DamageInfo ProcessHit(Collider hitCollider, float baseDamage, Vector3 hitPoint, float headMultiplier=2f, float limbMultiplier=.6f, Vector3? sourcePosition=null, bool lethalHeadshots=true)
        {
            if (hitCollider == null) return default;

            // 1. Check for explosive environmental props (cars, fuel tanks, barrels)
            var explosive = hitCollider.GetComponentInParent<ExplosiveProp>();
            if (explosive != null && !explosive.isDetonated && !(GeoSniper.Duel.SniperDuelManager.ActiveInstance?.UsesDuelLoadout ?? false))
            {
                explosive.TakeDamage(baseDamage, hitPoint);
                return new DamageInfo { baseDamage = baseDamage, multiplier = 1f, hitPoint = hitPoint, bodyPart = "prop", isHeadshot = false };
            }

            var rival = hitCollider.GetComponentInParent<GeoSniper.Duel.DuelOpponentAvatar>();
            if (rival != null)
            {
                if (rival.IsDead) return default;
                bool head = hitCollider.gameObject.name == "Hitbox_Head";
                var info = new DamageInfo {baseDamage = head ? 100f : baseDamage, multiplier = 1f,
                    hitPoint = hitPoint, bodyPart = head ? "head" : "torso", isHeadshot = head};
                // Hit effects are predicted; damage and confirmation arrive from the host.
                return info;
            }

            // 2. Check for Enemy Bots
            var bot = hitCollider.GetComponentInParent<EnemyBot>();
            if (bot != null)
            {
                var actor = bot.GetComponent<CombatActor>();
                if (actor != null && !actor.IsDead)
                {
                    lethalHeadshots &= actor.AllowExecutionHeadshots;
                    var hitboxes = bot.GetComponentInChildren<EnemyHitboxes>();
                    string bodyPart = "body";
                    float multiplier = 1f;

                    float effectiveLimbMult = Mathf.Max(0.75f, limbMultiplier);
                    if (hitboxes != null)
                    {
                        bodyPart = hitboxes.GetBodyPart(hitCollider, hitPoint);
                        if (bodyPart == "head") multiplier = lethalHeadshots?Mathf.Max(headMultiplier,4f):headMultiplier;
                        else if (bodyPart.Contains("arm") || bodyPart.Contains("leg") || bodyPart.Contains("foot") || bodyPart.Contains("hand")) multiplier = effectiveLimbMult;
                        else multiplier = 1f;
                    }
                    else
                    {
                        Vector3 local = bot.transform.InverseTransformPoint(hitPoint);
                        if (local.y >= 1.45f) { bodyPart = "head"; multiplier = lethalHeadshots?Mathf.Max(headMultiplier,4f):headMultiplier; }
                        else if (local.y < 0.85f) { bodyPart = (local.x < 0) ? "leftleg" : "rightleg"; multiplier = effectiveLimbMult; }
                        else if (Mathf.Abs(local.x) > 0.20f) { bodyPart = (local.x < 0) ? "leftarm" : "rightarm"; multiplier = effectiveLimbMult; }
                        else { bodyPart = "torso"; multiplier = 1f; }
                    }

                    bool isHeadshot = bodyPart == "head";
                    float totalDmg = baseDamage * multiplier;
                    if (isHeadshot && lethalHeadshots)
                    {
                        // Sniper rifle headshots are always lethal executions (1-shot instant kill)
                        totalDmg = Mathf.Max(totalDmg, actor.Health + 100f);
                    }

                    var info = new DamageInfo
                    {
                        baseDamage = totalDmg / Mathf.Max(0.01f, multiplier),
                        multiplier = multiplier,
                        hitPoint = hitPoint,
                        bodyPart = bodyPart,
                        isHeadshot = isHeadshot
                    };

                    actor.Damage(totalDmg, sourcePosition ?? Vector3.zero);
                    bot.RegisterAnatomicalHit(info);
                    OnDamageDealt?.Invoke(info);

                    CombatImpactAudio.Play(info.isHeadshot);

                    return info;
                }
            }

            // 3. Check for Civilians
            var civilian = hitCollider.GetComponentInParent<CivilianBot>();
            if (civilian != null)
            {
                var actor = civilian.GetComponent<CombatActor>();
                if (actor != null && !actor.IsDead)
                {
                    actor.Damage(baseDamage);
                    return new DamageInfo { baseDamage = baseDamage, multiplier = 1f, hitPoint = hitPoint, bodyPart = "body", isHeadshot = false };
                }
            }

            return default;
        }

        public static void ConfirmDuelHit(byte zone, float damage, Vector3 point)
        {
            if (zone > 2 || !float.IsFinite(damage) || damage <= 0) return;
            CombatImpactAudio.Play(zone==2);
            OnDamageDealt?.Invoke(new DamageInfo {baseDamage=damage, multiplier=1, hitPoint=point,
                bodyPart=zone==2 ? "head" : zone==0 ? "limb" : "torso", isHeadshot=zone==2});
        }

        public static void NotifyKill(EnemyBot bot)
        {
            OnEnemyKilled?.Invoke(bot);
        }
        public static void NotifyCivilianKill(CivilianBot civilian) => OnCivilianKilled?.Invoke(civilian);
    }
}
