using UnityEngine;

namespace GeoSniper
{
    [DefaultExecutionOrder(50)]
    public sealed class ArmyAnimation : MonoBehaviour
    {
        Animation clips;
        EnemyBot bot;
        CombatActor health;
        string idle, walk, run, crouch, fire, hit, death, current;
        float previousHealth, actionUntil, hitReactUntil;
        bool dying;
        Transform visualRoot;
        Quaternion visualRestRotation;
        float previousYaw,turnBank;
        public bool Ready { get; private set; }

        static AnimationClip resIdle, resWalk, resRun, resCrouch, resFire, resHit, resDeath;
        static bool resLoaded;

        static void EnsureClips()
        {
            if (resLoaded) return;
            resLoaded = true;
            resIdle = LoadClip("Models/Enemies/Animations/swat_idle");
            resWalk = LoadClip("Models/Enemies/Animations/swat_walk");
            resRun = LoadClip("Models/Enemies/Animations/swat_run");
            resCrouch = LoadClip("Models/Enemies/Animations/swat_crouch_idle");
            resFire = LoadClip("Models/Enemies/Animations/swat_fire");
            resHit = LoadClip("Models/Enemies/Animations/swat_hit");
            resDeath = LoadClip("Models/Enemies/Animations/swat_death");
        }

        static AnimationClip LoadClip(string path)
        {
            var all = Resources.LoadAll<AnimationClip>(path);
            if (all != null && all.Length > 0)
            {
                foreach (var c in all)
                {
                    if (c != null && !c.name.Contains("__preview__")) return c;
                }
                return all[0];
            }
            return Resources.Load<AnimationClip>(path);
        }

        public bool Initialize(EnemyBot owner, Transform visual)
        {
            bot = owner;
            health = owner.GetComponent<CombatActor>();
            previousHealth = health != null ? health.Health : 100f;

            if (visual == null) return false;
            visualRoot=visual;
            visualRestRotation=visual.localRotation;
            previousYaw=transform.eulerAngles.y;

            clips = visual.GetComponentInChildren<Animation>();
            if (clips == null)
            {
                var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
                var target = ImportedVisual.CharacterAnimationRoot(visual);
                clips = target.GetComponent<Animation>();
                if (clips == null) clips = target.AddComponent<Animation>();
            }

            EnsureClips();

            if (resIdle != null && clips.GetClip("idle") == null) { resIdle.legacy = true; clips.AddClip(resIdle, "idle"); }
            if (resWalk != null && clips.GetClip("walk") == null) { resWalk.legacy = true; clips.AddClip(resWalk, "walk"); }
            if (resRun != null && clips.GetClip("run") == null) { resRun.legacy = true; clips.AddClip(resRun, "run"); }
            if (resCrouch != null && clips.GetClip("crouch_idle") == null) { resCrouch.legacy = true; clips.AddClip(resCrouch, "crouch_idle"); }
            if (resFire != null && clips.GetClip("fire") == null) { resFire.legacy = true; clips.AddClip(resFire, "fire"); }
            if (resHit != null && clips.GetClip("hit") == null) { resHit.legacy = true; clips.AddClip(resHit, "hit"); }
            if (resDeath != null && clips.GetClip("death") == null) { resDeath.legacy = true; clips.AddClip(resDeath, "death"); }

            clips.playAutomatically = false;
            clips.cullingType = AnimationCullingType.AlwaysAnimate;

            foreach (AnimationState state in clips)
            {
                string name = state.name.ToLowerInvariant();
                if (name.Contains("crouch")) crouch = state.name;
                else if (name.Contains("run")) run = state.name;
                else if (name.Contains("idle")) idle = state.name;
                else if (name.Contains("walk")) walk = state.name;
                else if (name.Contains("fire")) fire = state.name;
                else if (name.Contains("hit")) hit = state.name;
                else if (name.Contains("death")) death = state.name;
                state.wrapMode = (name.Contains("idle") || name.Contains("walk") || name.Contains("run") || name.Contains("crouch")) ? WrapMode.Loop : WrapMode.ClampForever;
            }

            Ready = idle != null && walk != null;
            if (Ready)
            {
                clips.Stop();
                clips.Play(idle);
                if(clips[idle]!=null && clips[idle].length>0f)
                    clips[idle].time=Random.Range(0f,clips[idle].length);
                clips.Sample();
                current = idle;
                hips = FindHips(visual);
                if (hips != null) initialHipsLocalPos = hips.localPosition;
                head=WeaponGripPose.Bone(visual,"head");
                if(head!=null){headRest=head.localRotation;headYawAxis=head.InverseTransformDirection(transform.up).normalized;}
                idleLookPhase=Random.Range(0f,6.28f);
            }
            return Ready;
        }

        static Transform FindHips(Transform t)
        {
            if (t == null) return null;
            string n = t.name.ToLowerInvariant();
            if (n.Contains("hips") || n.Contains("pelvis") || n.Contains("rootnode")) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindHips(t.GetChild(i));
                if (found != null) return found;
            }
            return null;
        }

        Transform hips;
        Vector3 initialHipsLocalPos;
        Transform head;
        Quaternion headRest;
        Vector3 headYawAxis;
        float idleLookPhase;

        void Play(string name)
        {
            if (string.IsNullOrEmpty(name) || current == name || clips[name] == null) return;
            current = name;
            clips.CrossFade(name, name==hit || name==fire ? .06f : .18f);
        }

        public void Fire()
        {
            if (!Ready || dying || Time.time<hitReactUntil || fire == null || clips[fire] == null
                || (bot != null && (bot.CurrentSpeed > 0.15f || bot.Suspended || bot.isDisarmed || bot.IsReloading))) return;
            current = null;
            clips[fire].time = 0;
            Play(fire);
            actionUntil = Time.time + 0.22f;
        }

        void LateUpdate()
        {
            if (dying) return;
            if(visualRoot!=null && bot!=null)
            {
                float yaw=transform.eulerAngles.y;
                float turn=Mathf.DeltaAngle(previousYaw,yaw);
                previousYaw=yaw;
                float desired=Mathf.Clamp(-turn/Mathf.Max(.001f,Time.deltaTime)*.007f,-5f,5f)*Mathf.Clamp01(bot.CurrentSpeed/2f);
                turnBank=Mathf.MoveTowards(turnBank,desired,Time.deltaTime*35f);
                visualRoot.localRotation=visualRestRotation*Quaternion.Euler(0f,0f,turnBank);
            }
            // Authored Mixamo locomotion clips contain forward translation on the Hips bone.
            // Pin horizontal root motion so translation is driven 100% by CharacterController,
            // preventing the mesh from walking away from the collider and snapping back on loop/idle.
            if(hips!=null)
            {
                Vector3 pos = hips.localPosition;
                pos.x = initialHipsLocalPos.x;
                pos.z = initialHipsLocalPos.z;
                hips.localPosition = pos;
            }
            if(head!=null && bot!=null && !bot.Suspended && current==idle && !bot.IsAiming && bot.CurrentSpeed<.15f)
                head.localRotation=headRest*Quaternion.AngleAxis(Mathf.Sin(Time.time*.55f+idleLookPhase)*8f,headYawAxis);
        }

        void Update()
        {
            if (!Ready || health == null) return;

            if (health.IsDead)
            {
                if (!dying)
                {
                    dying = true;
                    current = null;
                    if (death != null && clips[death] != null)
                    {
                        clips[death].time = 0;
                        Play(death);
                    }
                }
                return;
            }

            if (health.Health < previousHealth && hit != null && clips[hit] != null)
            {
                current = null;
                clips[hit].time = 0;
                Play(hit);
                hitReactUntil = Time.time + Mathf.Clamp(clips[hit].length*.65f,.18f,.45f);
                actionUntil = hitReactUntil;
            }
            previousHealth = health.Health;

            if (Time.time < actionUntil) return;

            bool isCrouched = bot != null && bot.IsCrouched;
            bool isSprinting = bot != null && bot.IsSprinting;
            bool moving = bot != null && !bot.Suspended && bot.CurrentSpeed > 0.05f;

            if (isCrouched && !moving && crouch != null)
            {
                Play(crouch);
            }
            else if (moving)
            {
                if ((isSprinting || bot.CurrentSpeed>2.5f) && run != null)
                {
                    Play(run);
                    if (clips[run] != null) clips[run].speed = Mathf.Clamp(bot.CurrentSpeed / 2.6f, 0.2f, 2f);
                }
                else
                {
                    Play(walk);
                    if (walk != null && clips[walk] != null) clips[walk].speed = Mathf.Clamp(bot.CurrentSpeed / 1.4f, 0.08f, 1.8f);
                }
            }
            else
            {
                Play(idle);
            }
        }
    }
}
