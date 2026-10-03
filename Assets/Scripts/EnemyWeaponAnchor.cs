using UnityEngine;

namespace GeoSniper
{
    /// <summary>
    /// Dynamically anchors enemy weapons to the animated right hand and aims cleanly at the player,
    /// eliminating T-pose binding offsets, mid-air floating, and upward tilt.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class EnemyWeaponAnchor : MonoBehaviour
    {
        public Transform visual;
        public Transform rightHand;
        public Transform leftHand;
        public Transform rightFingers;
        public EnemyBot bot;
        public Vector3 localEulerOffset = Vector3.zero;

        bool initialized;
        Vector3 rightPalm, leftPalm;
        Quaternion rightGripRotation, leftGripRotation;
        Transform support, stock, magazine;

        public void Initialize(Transform visualTransform, Transform rHand, Transform lHand, Transform rFingers, EnemyBot enemyBot, Vector3 eulerOffset = default)
        {
            visual = visualTransform;
            rightHand = rHand;
            leftHand = lHand;
            rightFingers = rFingers;
            bot = enemyBot;
            localEulerOffset = eulerOffset;
            rightPalm = WeaponGripPose.PalmLocal(rightHand);
            leftPalm = WeaponGripPose.PalmLocal(leftHand);
            rightGripRotation = rightHand != null ? Quaternion.Inverse(visual.rotation) * rightHand.rotation : Quaternion.identity;
            leftGripRotation = leftHand != null ? Quaternion.Inverse(visual.rotation) * leftHand.rotation : Quaternion.identity;
            support = transform.Find("WeaponSupport");
            stock = transform.Find("WeaponStock");
            magazine = WeaponGripPose.Bone(transform,"magazine","magwell");
            initialized = true;

            UpdatePose(snap: true);
        }

        void LateUpdate()
        {
            if (!initialized || visual == null) return;
            if (bot != null && bot.isDisarmed) { enabled = false; return; }

            if (bot != null && bot.Actor != null && bot.Actor.IsDead)
            {
                // Fix rifle to hand on death so it naturally falls with the ragdoll/animation
                if (rightHand != null) transform.SetParent(rightHand, true);
                enabled = false;
                return;
            }

            UpdatePose(snap: false);
        }

        void UpdatePose(bool snap)
        {
            bool aiming = bot != null && bot.IsAiming && bot.player != null;
            float reloadPose=bot!=null && bot.IsReloading?Mathf.Sin(bot.ReloadProgress*Mathf.PI):0f;
            Vector3 grip = rightHand != null ? rightHand.TransformPoint(rightPalm)
                : visual.position + visual.up * 1.15f + visual.right * .2f;
            grip-=visual.up*(.10f*reloadPose);
            Quaternion facing = visual.rotation * Quaternion.Euler(8f+reloadPose*22f, -4f, 0f);
            if (aiming)
            {
                var player = bot.player.GetComponent<UrbanPlayer>();
                Vector3 target = bot.player.position + Vector3.up * (player != null && player.IsCrouching ? .85f : 1.35f);
                if ((target-grip).sqrMagnitude > .001f) facing = Quaternion.LookRotation(target-grip, visual.up);
                if (stock != null && rightHand != null && rightHand.parent != null && rightHand.parent.parent != null)
                {
                    Vector3 shoulder = rightHand.parent.parent.position + visual.forward * .06f - visual.up * .04f;
                    grip = shoulder - facing * stock.localPosition;
                }
            }
            // Resolve both hand contacts against the same final weapon pose in this frame.
            transform.SetPositionAndRotation(grip, facing * Quaternion.Euler(localEulerOffset));
            WeaponGripPose.SolveArm(rightHand, rightPalm, grip, facing * rightGripRotation, visual.right-visual.up);
            if (rightHand != null && !aiming)
                transform.position = rightHand.TransformPoint(rightPalm);
            if (support != null)
            {
                Vector3 supportPoint=support.position;
                if(reloadPose>0f)
                {
                    Vector3 magazinePoint=magazine!=null?magazine.position:transform.position-visual.up*.13f;
                    supportPoint=Vector3.Lerp(supportPoint,magazinePoint,reloadPose);
                }
                WeaponGripPose.SolveArm(leftHand, leftPalm, supportPoint, facing * leftGripRotation, -visual.right-visual.up);
            }
        }
    }
}
