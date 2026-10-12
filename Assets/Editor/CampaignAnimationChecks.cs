using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Real rig sampling catches full-body hit/fire clips replacing moving feet.
public static class CampaignAnimationChecks
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly StringBuilder report = new StringBuilder();

    static void Set(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, Fields)
            ?? target.GetType().GetField("<" + name + ">k__BackingField", Fields);
        if (field == null) throw new Exception("Missing test field " + name);
        field.SetValue(target, value);
    }
    static T Read<T>(object target, string name)
        => (T)target.GetType().GetField(name, Fields).GetValue(target);
    static void Invoke(object target, string name, params object[] arguments)
        => target.GetType().GetMethod(name, Fields).Invoke(target, arguments);
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        report.AppendLine("PASS: " + message);
    }

    struct Pose
    {
        public Vector3 LeftFoot, RightFoot, Hips;
        public Quaternion LeftThigh, RightThigh;
        public Quaternion[] Upper;
    }
    static Pose Capture(Transform visual)
    {
        var upper = new[] { "spine", "head", "leftarm", "rightarm", "lefthand", "righthand" };
        return new Pose {
            LeftFoot = WeaponGripPose.Bone(visual, "leftfoot").position,
            RightFoot = WeaponGripPose.Bone(visual, "rightfoot").position,
            Hips = WeaponGripPose.Bone(visual, "hips").localPosition,
            LeftThigh = WeaponGripPose.Bone(visual, "leftupleg").localRotation,
            RightThigh = WeaponGripPose.Bone(visual, "rightupleg").localRotation,
            Upper = upper.Select(name => WeaponGripPose.Bone(visual, name).localRotation).ToArray()
        };
    }
    static float UpperDifference(Pose a, Pose b)
    {
        float result = 0;
        for (int i = 0; i < a.Upper.Length; i++) result = Mathf.Max(result, Quaternion.Angle(a.Upper[i], b.Upper[i]));
        return result;
    }
    static bool SameLegs(Pose a, Pose b)
        => Vector3.Distance(a.LeftFoot, b.LeftFoot) < .002f
            && Vector3.Distance(a.RightFoot, b.RightFoot) < .002f
            && Vector3.Distance(a.Hips, b.Hips) < .002f
            && Quaternion.Angle(a.LeftThigh, b.LeftThigh) < .15f
            && Quaternion.Angle(a.RightThigh, b.RightThigh) < .15f;
    static bool FeetMoved(Pose a, Pose b)
        => Mathf.Max(Vector3.Distance(a.LeftFoot, b.LeftFoot), Vector3.Distance(a.RightFoot, b.RightFoot)) > .025f
            && Mathf.Max(Quaternion.Angle(a.LeftThigh, b.LeftThigh), Quaternion.Angle(a.RightThigh, b.RightThigh)) > 2f;

    static Pose Sample(Animation animation, ArmyAnimation driver, Transform visual,
        AnimationState walk, float phase, string action = null)
    {
        animation.Stop();
        animation.Play(walk.name);
        walk.weight = 1; walk.time = walk.length * phase;
        if (action != null)
        {
            animation.Play(action, PlayMode.StopSameLayer);
            var state = animation[action];
            state.weight = 1; state.time = state.length * .35f;
        }
        animation.Sample();
        Invoke(driver, "LateUpdate");
        return Capture(visual);
    }

    static void Authored(GameObject prefab)
    {
        var owner = new GameObject("Campaign authored reaction check " + prefab.name);
        try
        {
            var visual = ImportedVisual.CreateEnemy(prefab, owner.transform).transform;
            var bot = owner.AddComponent<EnemyBot>(); bot.Initialize(null, 0);
            bot.currentState = AIState.Combat;
            Set(bot, "CurrentSpeed", 1.4f);
            var driver = owner.AddComponent<ArmyAnimation>();
            Require(driver.Initialize(bot, visual), prefab.name + ": authored clips bind to the actual campaign rig");
            var animation = visual.GetComponentInChildren<Animation>();
            var walkName = Read<string>(driver, "walk");
            var fireName = Read<string>(driver, "fire");
            var hitName = Read<string>(driver, "hit");
            var walk = animation[walkName];
            Require(animation[fireName].layer == 1 && animation[hitName].layer == 1,
                prefab.name + ": fire and hit use the upper action layer");
            Require(WeaponGripPose.Bone(visual, "spine") == Read<Transform>(driver, "actionSpine"),
                prefab.name + ": the action mask starts at the spine, above the hips and legs");

            Invoke(driver, "Update");
            driver.Fire();
            Require(Read<string>(driver, "current") == walkName && Read<string>(driver, "currentAction") == fireName
                    && animation.IsPlaying(walkName),
                prefab.name + ": shooting while walking preserves the selected locomotion");
            bot.Actor.Damage(8f);
            bot.currentState = AIState.Combat; Set(bot, "CurrentSpeed", 1.4f);
            Invoke(driver, "Update");
            Require(Read<string>(driver, "current") == walkName && Read<string>(driver, "currentAction") == hitName
                    && animation.IsPlaying(walkName),
                prefab.name + ": receiving a hit preserves the selected locomotion");

            var baseline = Sample(animation, driver, visual, walk, .10f);
            foreach (var action in new[] { fireName, hitName })
            {
                var first = Sample(animation, driver, visual, walk, .10f, action);
                var second = Sample(animation, driver, visual, walk, .35f, action);
                Require(SameLegs(baseline, first) && UpperDifference(baseline, first) > .2f,
                    prefab.name + ": " + action + " animates the upper body without replacing feet or hips");
                Require(FeetMoved(first, second),
                    prefab.name + ": walking feet keep moving during " + action);
            }

            var before = Capture(visual);
            var fallback = owner.AddComponent<EnemyWalkAnimator>();
            fallback.Initialize(bot, visual); Invoke(fallback, "LateUpdate");
            var after = Capture(visual);
            Require(SameLegs(before, after) && UpperDifference(before, after) < .15f && !fallback.HasValidLegs,
                prefab.name + ": fallback initialization and updates preserve an active authored rig");
        }
        finally { Object.DestroyImmediate(owner); }
    }

    static Pose FallbackSample(EnemyWalkAnimator driver, Transform visual, float phase, float hit, float shot)
    {
        // Edit mode has no simulation delta. Seed phase/pulse magnitude, then sample
        // the production bone posing path rather than inventing test-only animations.
        Set(driver, "phase", phase); Set(driver, "blend", 1f);
        Set(driver, "hitPose", hit); Set(driver, "shotPose", shot);
        Invoke(driver, "LateUpdate");
        return Capture(visual);
    }
    static void Fallback(GameObject prefab)
    {
        var owner = new GameObject("Campaign fallback reaction check " + prefab.name);
        try
        {
            var visual = ImportedVisual.CreateEnemy(prefab, owner.transform).transform;
            foreach (var animation in visual.GetComponentsInChildren<Animation>(true))
            { animation.Stop(); animation.enabled = false; }
            foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var bot = owner.AddComponent<EnemyBot>(); bot.Initialize(null, 0);
            bot.currentState = AIState.Patrol; Set(bot, "CurrentSpeed", 1.4f);
            Set(bot, "HitReactionSide", 1f);
            var driver = owner.AddComponent<EnemyWalkAnimator>(); driver.Initialize(bot, visual);
            Require(driver.HasValidLegs, prefab.name + ": fallback resolves both actual leg chains");
            Set(bot, "HitReactionStrength", 0f); Set(bot, "ShotReactionStrength", 0f);
            var baseline = FallbackSample(driver, visual, .1f, 0f, 0f);
            Set(bot, "HitReactionStrength", 1f);
            var flinched = FallbackSample(driver, visual, .1f, 1f, 0f);
            var flinchedStep = FallbackSample(driver, visual, .35f, 1f, 0f);
            Require(UpperDifference(baseline, flinched) > 4f,
                prefab.name + ": fallback hit impulse visibly moves the head and torso");
            Require(FeetMoved(flinched, flinchedStep),
                prefab.name + ": fallback gait continues with a hit impulse layered over it");
            Set(bot, "ShotReactionStrength", 1f);
            Invoke(bot, "TickReaction", .6f);
            Require(bot.HitReactionStrength == 0f && bot.ShotReactionStrength == 0f,
                prefab.name + ": reaction and recoil source impulses expire");
            var restored = FallbackSample(driver, visual, .1f, 0f, 0f);
            Require(SameLegs(baseline, restored) && UpperDifference(baseline, restored) < .15f,
                prefab.name + ": expired fallback impulses restore the baseline pose without bone drift");
        }
        finally { Object.DestroyImmediate(owner); }
    }

    static void Disarmed(GameObject prefab)
    {
        var owner=new GameObject("Disarmed rig "+prefab.name);
        try
        {
            var visual=ImportedVisual.CreateEnemy(prefab,owner.transform).transform;
            var bot=owner.AddComponent<EnemyBot>();bot.Initialize(null,0);
            var driver=owner.AddComponent<ArmyAnimation>();Require(driver.Initialize(bot,visual),"Disarm rig binds authored clips");
            var animation=visual.GetComponentInChildren<Animation>();
            string fire=Read<string>(driver,"fire");animation.Play(fire);animation[fire].time=animation[fire].length*.3f;animation.Sample();
            var armed=Capture(visual);
            bot.Disarm();Invoke(bot,"LateUpdate");var unarmed=Capture(visual);
            Require(SameLegs(armed,unarmed),prefab.name+": disarm pose preserves the authored legs");
            Require(UpperDifference(armed,unarmed)>5,prefab.name+": disarm replaces the rifle-holding arm pose");
            foreach(string name in new[]{"lefthand","righthand"})
            {
                var hand=WeaponGripPose.Bone(visual,name);
                Require(float.IsFinite(hand.position.x) && float.IsFinite(hand.position.y) && float.IsFinite(hand.position.z),prefab.name+": unarmed hand pose stays finite");
                Require(Vector3.Dot(hand.position-owner.transform.position,owner.transform.forward)<.4f,prefab.name+": unarmed hand no longer extends along an invisible barrel");
            }
            Set(bot,"weaponPickupHold",.8f);Invoke(bot,"LateUpdate");
            Require(bot.RecoveringWeapon && bot.IsCrouched,prefab.name+": weapon pickup has a distinct crouched reach state");
        }
        finally{Object.DestroyImmediate(owner);}
    }
    [MenuItem("Geo Sniper/Validate Campaign Reaction Animations")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        if (!Application.dataPath.Replace('\\', '/').Contains("/.utmp/duel-unity/") && !Application.dataPath.Replace('\\', '/').Contains("/.utmp/review-unity/"))
            throw new Exception("Use the isolated Unity verification project.");
        report.Clear();
        try
        {
            var prefabs = ModelLibrary.Load("Enemies");
            Require(prefabs.Length == 2, "both actual campaign enemy rigs are available");
            foreach (var prefab in prefabs) { Authored(prefab); Fallback(prefab); Disarmed(prefab); }
            Debug.Log("PASS: campaign reaction animation checks");
        }
        finally
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/CampaignAnimationChecks.txt", report.ToString());
        }
    }
}
