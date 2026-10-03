using UnityEngine;

namespace GeoSniper
{
    public sealed class CombatActor : MonoBehaviour
    {
        public float maxHealth=100;
        public bool IsDead { get; private set; }
        public bool Invulnerable;
        public float Health { get; private set; }
        
        public event System.Action<CombatActor> OnDeath;
        public event System.Action<float> OnDamaged;

        public bool canRegenerate = false;
        float lastDamageTime;

        void Awake() { Health = maxHealth; }
        public void Initialize(float health)
        {
            maxHealth=health;Health=health;IsDead=false;lastDamageTime=Time.time;
            var collider=GetComponent<Collider>();if(collider!=null) collider.enabled=true;
        }

        public Vector3 LastDamageSourcePos { get; private set; }

        public void Damage(float amount)
        {
            Damage(amount, Vector3.zero);
        }

        public void Damage(float amount, Vector3 sourcePos)
        {
            if (IsDead || Invulnerable || !float.IsFinite(amount) || amount <= 0) return;
            Health = Mathf.Max(0, Health - amount);
            lastDamageTime = Time.time;
            if (sourcePos != Vector3.zero) LastDamageSourcePos = sourcePos;
            bool lethal = Health <= 0;
            if (lethal) IsDead = true;
            OnDamaged?.Invoke(amount);
            if (lethal) Die();
        }

        public void DamageWithInfo(DamageInfo info)
        {
            Damage(info.TotalDamage, info.hitPoint);
        }

        void Die()
        {
            IsDead=true; 
            var c=GetComponent<Collider>(); if(c!=null)c.enabled=false; 
            OnDeath?.Invoke(this);
        }

        public void Heal(float amount){ if(!IsDead) Health=Mathf.Min(maxHealth,Health+amount); }
        
        void Update()
        {
            if (canRegenerate && !IsDead && Health < maxHealth)
            {
                if (Time.time - lastDamageTime > 5f)
                {
                    Health = Mathf.Min(maxHealth, Health + 10f * Time.deltaTime);
                }
            }
        }
    }
}
