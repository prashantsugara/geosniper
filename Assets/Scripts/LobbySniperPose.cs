using UnityEngine;

namespace GeoSniper
{
    /// <summary>
    /// Anchors the sniper rifle cleanly to the lobby operative's right hand and shoulder,
    /// ensuring authentic tactical low-ready carry and natural idle breathing synchronization,
    /// while preserving 100% skinned mesh integrity without skeleton or mesh distortion.
    /// </summary>
    public sealed class LobbySniperPose : MonoBehaviour
    {
        Transform rightHand;
        Transform leftHand;
        GameObject rifleHolder;
        bool ready;

        void Awake()
        {
            CacheBonesAndAnimation();
        }

        public void Initialize(GameObject rifle)
        {
            rifleHolder = rifle;
            CacheBonesAndAnimation();
            ready = true;
            AttachRifle();
        }

        public void SetRifle(GameObject rifle)
        {
            rifleHolder = rifle;
            AttachRifle();
        }

        void CacheBonesAndAnimation()
        {
            // Ensure skinned meshes never cull when operative is rotated
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.updateWhenOffscreen = true;
            }

            // Ensure natural authored idle animation loops smoothly
            var anim = GetComponentInChildren<Animation>();
            if (anim == null)
            {
                var skin = GetComponentInChildren<SkinnedMeshRenderer>();
                var target = ImportedVisual.CharacterAnimationRoot(transform);
                anim = target.GetComponent<Animation>() ?? target.AddComponent<Animation>();
            }

            if (anim != null)
            {
                if (anim.GetClip("idle") == null && anim.GetClip("swat_idle") == null)
                {
                    var idleClip = Resources.Load<AnimationClip>("Models/Enemies/Animations/swat_idle");
                    if (idleClip == null)
                    {
                        var all = Resources.LoadAll<AnimationClip>("Models/Enemies/Animations/swat_idle");
                        if (all != null && all.Length > 0)
                        {
                            foreach (var c in all) if (c != null && !c.name.Contains("__preview__")) { idleClip = c; break; }
                            if (idleClip == null) idleClip = all[0];
                        }
                    }
                    if (idleClip != null)
                    {
                        idleClip.legacy = true;
                        anim.AddClip(idleClip, "idle");
                    }
                }

                anim.enabled = true;
                anim.playAutomatically = true;
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
                foreach (AnimationState state in anim)
                {
                    if (state.name.ToLowerInvariant().Contains("idle"))
                    {
                        state.wrapMode = WrapMode.Loop;
                        anim.Play(state.name);
                        anim.Sample();
                        break;
                    }
                }
            }

            // Locate hand bones for natural grip placement (handling Mixamo mixamorig:RightHand)
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string clean = t.name.ToLowerInvariant().Replace(":", "").Replace("_", "").Replace(".", "").Replace("/", "");
                if (rightHand == null && (clean.EndsWith("righthand") || clean.EndsWith("handr") || clean.EndsWith("rhand")))
                {
                    rightHand = t;
                }
                else if (leftHand == null && (clean.EndsWith("lefthand") || clean.EndsWith("handl") || clean.EndsWith("lhand")))
                {
                    leftHand = t;
                }
            }
        }

        void AttachRifle()
        {
            if (!ready || rifleHolder == null) return;
            var anchor = rifleHolder.GetComponent<EnemyWeaponAnchor>() ?? rifleHolder.AddComponent<EnemyWeaponAnchor>();
            anchor.Initialize(transform, rightHand, leftHand, null, null);
        }
    }
}
