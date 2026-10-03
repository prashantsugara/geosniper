using UnityEngine;

namespace GeoSniper
{
    // Fallback gait for characters without authored locomotion clips.
    public sealed class EnemyWalkAnimator : MonoBehaviour
    {
        sealed class Leg
        {
            public Transform Hip,Knee,Foot;
            public Quaternion HipRest,KneeRest,FootRest;
            public Vector3 HipAxis,KneeAxis,FootAxis;
            public Vector3 LocalFoot,Plant,SwingStart,Landing;
            public float Upper,Lower,PhaseOffset,Previous;
            public bool Valid=>Hip!=null && Knee!=null && Foot!=null && Hip!=Knee;
        }
        EnemyBot bot;
        CivilianBot civilian;
        Transform modelRoot;
        Leg left,right;
        Vector3 previousPosition;
        float phase,blend;
        public bool HasValidLegs => left!=null && right!=null && left.Valid && right.Valid;
        Transform leftArm, rightArm;
        Quaternion leftArmRest, rightArmRest;
        Vector3 rootRestPosition,leftArmAxis,rightArmAxis;
        Quaternion rootRestRotation;
        Transform enemyWeapon;
        Vector3 weaponRestPosition;
        Quaternion weaponRestRotation;
        Transform weaponLeftHand;
        Transform spine;
        Quaternion spineRest;
        Transform head;
        Quaternion headRest;
        Vector3 headYawAxis;
        float idlePhase,idleLook;
        void CaptureRoot()
        {
            rootRestPosition=modelRoot.localPosition; rootRestRotation=modelRoot.localRotation;
            previousPosition=transform.position; currentYaw=transform.eulerAngles.y;
        }

        public void Initialize(EnemyBot owner,Transform visual)
        {
            bot=owner; modelRoot=visual;
            if(visual==null || visual==transform) return;
            CaptureRoot();
            left=CreateLeg("left",0); right=CreateLeg("right",.5f);
            enemyWeapon = FindBone("enemy tactical rifle", "attached_ak47");
            leftArm = FindBone("leftarm", "leftshoulder", "l_upperarm", "l_arm", "upperarml", "arml", "bipupperarml");
            rightArm = FindBone("rightarm", "rightshoulder", "r_upperarm", "r_arm", "upperarmr", "armr", "bipupperarmr");
            if (leftArm != null) leftArmRest = leftArm.localRotation;
            if (rightArm != null) rightArmRest = rightArm.localRotation;
            if (leftArm != null) leftArmAxis = leftArm.InverseTransformDirection(transform.right).normalized;
            if (rightArm != null) rightArmAxis = rightArm.InverseTransformDirection(transform.right).normalized;
            if (enemyWeapon != null)
            {
                weaponRestPosition = enemyWeapon.localPosition;
                weaponRestRotation = enemyWeapon.localRotation;
                weaponLeftHand = FindBone("lefthand", "hand_l", "left_hand", "l_wrist", "mixamoriglefthand");
            }
            spine = FindBone("spine1", "spine01", "chest", "spine", "torso", "upperbody");
            if (spine != null) spineRest = spine.localRotation;
            CaptureHead();
            previousPosition=transform.position;
        }
        public void Initialize(CivilianBot owner,Transform visual)
        {
            civilian=owner; modelRoot=visual;
            if(visual==null || visual==transform) return;
            CaptureRoot();
            left=CreateLeg("left",0); right=CreateLeg("right",.5f);
            leftArm = FindBone("leftarm", "leftshoulder", "l_upperarm", "l_arm", "upperarml", "arml", "bipupperarml");
            rightArm = FindBone("rightarm", "rightshoulder", "r_upperarm", "r_arm", "upperarmr", "armr", "bipupperarmr");
            RelaxArm(leftArm,-1);
            RelaxArm(rightArm,1);
            if(leftArm!=null) leftArmRest=leftArm.localRotation;
            if(rightArm!=null) rightArmRest=rightArm.localRotation;
            if(leftArm!=null) leftArmAxis=leftArm.InverseTransformDirection(transform.right).normalized;
            if(rightArm!=null) rightArmAxis=rightArm.InverseTransformDirection(transform.right).normalized;
            spine = FindBone("spine1", "spine01", "chest", "spine", "torso", "upperbody");
            if (spine != null) spineRest = spine.localRotation;
            CaptureHead();
            previousPosition=transform.position;
        }
        void CaptureHead()
        {
            head=FindBone("head");
            if(head==null)return;
            headRest=head.localRotation;
            headYawAxis=head.InverseTransformDirection(transform.up).normalized;
            idlePhase=Random.Range(0f,6.28f);
        }
        Leg CreateLeg(string side, float offset)
        {
            string sShort = side == "left" ? "l" : "r";
            Transform hip = FindBone(side + "upleg", side + "thigh", side + "upperleg", "biphip" + sShort, "hip" + sShort, "thigh" + sShort);
            Transform knee = FindKneeBone(side, hip);
            Transform foot = FindBone(side + "foot", side + "ankle", side + "toe", "bipfoot" + sShort, "foot" + sShort, "ankle" + sShort);

            if (hip == null || knee == null || foot == null) return null;
            var leg = new Leg { Hip = hip, Knee = knee, Foot = foot, PhaseOffset = offset, Previous = offset };
            
            // Align thigh to hang naturally downwards (eliminates reverse-V splay)
            Vector3 thighDir = (knee.position - hip.position).normalized;
            Vector3 desiredDir = (Vector3.down * 0.98f + (side == "left" ? Vector3.left : Vector3.right) * 0.04f).normalized;
            Vector3 desiredWorld = transform.TransformDirection(desiredDir);
            if (thighDir.sqrMagnitude > 0.01f && desiredWorld.sqrMagnitude > 0.01f)
            {
                hip.rotation = Quaternion.FromToRotation(thighDir, desiredWorld) * hip.rotation;
            }

            leg.HipRest = leg.Hip.localRotation; leg.KneeRest = leg.Knee.localRotation;
            leg.FootRest = leg.Foot.localRotation;
            leg.HipAxis=leg.Hip.InverseTransformDirection(transform.right).normalized;
            leg.KneeAxis=leg.Knee.InverseTransformDirection(transform.right).normalized;
            leg.FootAxis=leg.Foot.InverseTransformDirection(transform.right).normalized;
            leg.LocalFoot = transform.InverseTransformPoint(leg.Foot.position);
            var hipLocal = transform.InverseTransformPoint(leg.Hip.position);
            leg.LocalFoot.x = hipLocal.x;
            leg.Upper = Vector3.Distance(leg.Hip.position, leg.Knee.position);
            leg.Lower = Vector3.Distance(leg.Knee.position, leg.Foot.position);
            if (leg.Upper < 0.05f || leg.Lower < 0.05f) {leg.Foot=null; return leg;}
            leg.Plant = leg.SwingStart = leg.Landing = transform.TransformPoint(leg.LocalFoot);
            return leg;
        }

        Transform FindKneeBone(string side, Transform hip)
        {
            if (modelRoot == null) return null;
            string sShort = side == "left" ? "l" : "r";
            foreach (var child in modelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child == hip) continue;
                string name = child.name.ToLowerInvariant().Replace(":", "").Replace("_", "").Replace(".", "");
                bool isSide = name.Contains(side) || (name.EndsWith(sShort) || name.Contains(sShort + "1") || name.Contains(sShort + "2") || name.Contains(sShort + "3") || name.Contains(sShort + "4") || name.Contains(sShort + "0") || name.Contains("bipknee" + sShort) || name.Contains("knee" + sShort));
                if (isSide && (name.Contains("knee") || name.Contains("lowerleg") || name.Contains("shin") || (name.Contains("leg") && !name.Contains("upleg") && !name.Contains("upper") && !name.Contains("thigh"))))
                {
                    return child;
                }
            }
            return null;
        }

        Transform FindBone(params string[] tokens)
        {
            if (modelRoot == null) return null;
            var bones=modelRoot.GetComponentsInChildren<Transform>(true);
            // Prefer explicit anatomical names over broad fallback aliases, regardless of hierarchy order.
            foreach(var token in tokens)
            foreach (var child in bones)
            {
                string name = child.name.ToLowerInvariant().Replace(":", "").Replace("_", "").Replace(".", "");
                if (name.Contains(token.Replace("_",""))) return child;
            }
            return null;
        }
        void RelaxArm(Transform arm,float side)
        {
            if(arm==null) return;
            foreach(Transform child in arm)
            {
                Vector3 direction=child.position-arm.position;
                if(direction.magnitude<.05f) continue;
                // Only lower a near-horizontal bind-pose arm. Preserve already-authored resting poses.
                if(Mathf.Abs(Vector3.Dot(direction.normalized,transform.up))<.45f)
                    arm.rotation=Quaternion.FromToRotation(direction.normalized,
                        transform.TransformDirection(new Vector3(side*.18f,-1,.06f)).normalized)*arm.rotation;
                break;
            }
        }
        Vector3 smoothedVelocity;
        float currentYaw;
        float smoothedTurnRate;
        float deathBlend;
        float coverBlend;

        void Update()
        {
            if (bot == null && civilian == null) return;
            bool dead=bot!=null && bot.Actor!=null && bot.Actor.IsDead;
            if(dead && modelRoot!=null)
            {
                deathBlend=Mathf.MoveTowards(deathBlend,1f,Time.deltaTime*3.2f);
                float side=bot.FlinchDirection.x>=0?1f:-1f;
                modelRoot.localPosition=Vector3.Lerp(rootRestPosition,rootRestPosition+Vector3.down*.72f,deathBlend);
                modelRoot.localRotation=rootRestRotation*Quaternion.Euler(72f*deathBlend,0,side*18f*deathBlend);
                return;
            }
            Vector3 movement = transform.position - previousPosition; movement.y = 0;
            previousPosition = transform.position;
            
            Vector3 velocity = movement / Mathf.Max(Time.deltaTime, 0.001f);
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, Time.deltaTime * 5f);
            
            float targetYaw = transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(currentYaw, targetYaw);
            currentYaw = targetYaw;
            float turnRate = yawDelta / Mathf.Max(Time.deltaTime, 0.001f);
            smoothedTurnRate = Mathf.Lerp(smoothedTurnRate, turnRate, Time.deltaTime * 6f);
            
            float curSpeed = (bot != null) ? bot.CurrentSpeed : (civilian != null ? civilian.CurrentSpeed : 0f);
            bool moving = false;
            if (bot != null) moving = !bot.Suspended && curSpeed > .08f;
            else if (civilian != null) moving = !civilian.Suspended && curSpeed > .08f;

            // Natural human stride cadence (1.2 Hz walk to 2.8 Hz sprint)
            float strideRate = Mathf.Clamp(curSpeed * 0.82f, 1.2f, 2.8f);
            if (moving) phase = Mathf.Repeat(phase + Time.deltaTime * strideRate, 1f);
            blend = Mathf.MoveTowards(blend, moving ? 1f : 0f, Time.deltaTime * 5f);

            bool combat = bot != null && (bot.currentState == AIState.Combat || bot.currentState == AIState.TakeCover || bot.isCounterSniper);
            bool sprinting = curSpeed > 3.4f || (bot != null && (bot.coverSubState == CoverSubState.Sprinting || bot.isFugitiveRunner));

            // Fluent momentum and banking system
            if (modelRoot != null && modelRoot!=transform)
            {
                float bob = moving ? -Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * (sprinting ? 0.038f : 0.024f) * blend : 0f;
                float limpRoll = 0f;
                if (bot != null && bot.isHobbled && moving)
                {
                    // Asymmetric limping gait: heavy drop when stepping on injured leg
                    float limpCycle = Mathf.Sin(phase * Mathf.PI * 2f);
                    bob += (limpCycle > 0 ? -0.055f : 0.015f) * blend;
                    limpRoll = 7f * blend;
                }
                bool takingCover = bot != null && (bot.IsCrouchingInCover || (bot.currentState == AIState.TakeCover && bot.coverSubState == CoverSubState.Crouching));
                bool civilianCowering = civilian != null && civilian.currentState == CivilianState.Cower;
                coverBlend = Mathf.MoveTowards(coverBlend, (takingCover || civilianCowering) ? 1f : 0f, Time.deltaTime * 4.5f);
                
                float bankRoll = -Mathf.Clamp(smoothedTurnRate * 0.045f, -10f, 10f) * Mathf.Clamp01(curSpeed / 2f);
                float stepRoll = moving ? Mathf.Sin(phase * Mathf.PI * 2f) * 1.1f * blend : 0f;
                float totalRoll = bankRoll + stepRoll + limpRoll;
                
                float localForwardAccel = Vector3.Dot(transform.forward, velocity - smoothedVelocity);
                float pitch = Mathf.Clamp(curSpeed * .65f, 0f, 6f) + Mathf.Clamp(localForwardAccel * -0.3f, -6f, 6f);
                
                float hipYaw = moving ? Mathf.Sin(phase * Mathf.PI * 2f) * 4.2f * blend : 0f;
                float coverLean = takingCover ? Mathf.Sin(Time.time * 3.5f) * 1.2f : 0f;
                
                float flinchPitch = 0f, flinchRoll = 0f;
                if (bot != null && bot.StaggerProgress > 0f)
                {
                    float p = bot.StaggerProgress;
                    float magnitude = Mathf.Sin(p * Mathf.PI);
                    Vector3 localFlinchDir = transform.InverseTransformDirection(bot.FlinchDirection);
                    flinchPitch = -localFlinchDir.z * 40f * magnitude;
                    flinchRoll = localFlinchDir.x * 25f * magnitude;
                }
                
                modelRoot.localPosition = rootRestPosition + Vector3.up * (bob - .20f * coverBlend);
                modelRoot.localRotation = rootRestRotation * Quaternion.Euler(Mathf.Clamp(pitch + flinchPitch, -8f, 8f) + 6f * coverBlend, hipYaw, totalRoll + flinchRoll + coverLean);

                // Natural Thoracic Counter-Rotation & Spine Posture
                if (spine != null)
                {
                    float forwardHunch = (combat ? 3.5f : 1.5f) + (sprinting ? 2.5f : 0f) + (coverBlend * (civilianCowering ? 17f : 4f));
                    float spineCounterYaw = -hipYaw * 0.45f;
                    Vector3 localPitch = spine.InverseTransformDirection(transform.right).normalized;
                    Vector3 localYaw = spine.InverseTransformDirection(transform.up).normalized;
                    spine.localRotation = spineRest * Quaternion.AngleAxis(forwardHunch, localPitch) * Quaternion.AngleAxis(spineCounterYaw, localYaw);
                }
            }

            if (left != null && right != null && left.Valid && right.Valid)
            {
                AnimateLeg(left);
                AnimateLeg(right);
            }

            if (civilian != null && leftArm != null && rightArm != null)
            {
                Vector3 lArmPitchAxis = leftArmAxis;
                Vector3 rArmPitchAxis = rightArmAxis;

                // Natural, gentle arm swing opposing legs
                float armAngle = Mathf.Sin(phase * Mathf.PI * 2f) * 16f * blend;
                float panic = civilian.currentState == CivilianState.Flee ? 1f : 0f;
                float cower = civilian.currentState == CivilianState.Cower ? coverBlend : 0f;
                float social = civilian.currentState==CivilianState.Idle && civilian.SocialPartner!=null ? 1f : 0f;
                float gesture=social*Mathf.Max(0f,Mathf.Sin(Time.time*2.2f+idlePhase))*12f;
                leftArm.localRotation = leftArmRest * Quaternion.AngleAxis(-armAngle*(1f+panic*.65f)-cower*58f, lArmPitchAxis);
                rightArm.localRotation = rightArmRest * Quaternion.AngleAxis(armAngle*(1f+panic*.65f)-cower*58f-gesture, rArmPitchAxis);
            }
            else if (bot != null && bot.isDisarmed && leftArm != null && rightArm != null)
            {
                // Clutch chest / torso in pain
                float clutchAngle = 42f;
                leftArm.localRotation = leftArmRest * Quaternion.Euler(clutchAngle, 20f, 0f);
                rightArm.localRotation = rightArmRest * Quaternion.Euler(clutchAngle, -20f, 0f);
            }

            PoseEnemyWeapon(moving, coverBlend, curSpeed);
            if(head!=null)
            {
                float desired=0f;
                if(civilian!=null && civilian.currentState==CivilianState.Idle)
                {
                    if(civilian.SocialPartner!=null)
                    {
                        Vector3 local=transform.InverseTransformDirection(civilian.SocialPartner.transform.position-transform.position);
                        desired=Mathf.Clamp(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,-28f,28f);
                    }
                    else desired=Mathf.Sin(Time.time*.7f+idlePhase)*13f;
                }
                else if(bot!=null && !combat && !moving)desired=Mathf.Sin(Time.time*.6f+idlePhase)*9f;
                idleLook=Mathf.MoveTowards(idleLook,desired,Time.deltaTime*65f);
                head.localRotation=headRest*Quaternion.AngleAxis(idleLook,headYawAxis);
            }
        }

        void PoseEnemyWeapon(bool moving, float cover, float curSpeed)
        {
            if (bot == null || enemyWeapon == null || bot.Actor == null || bot.Actor.IsDead) return;
            if (bot.isDisarmed) return;
            bool combat = bot.currentState == AIState.Combat || bot.currentState == AIState.TakeCover || bot.isCounterSniper;
            bool sprinting = curSpeed > 3.4f || bot.coverSubState == CoverSubState.Sprinting || bot.isFugitiveRunner;
            float reloadPose=bot.IsReloading?Mathf.Sin(bot.ReloadProgress*Mathf.PI):0f;
            float ready = combat && !bot.IsReloading ? 1f : 0f;
            
            // Tactical sway and pump
            float sway = moving ? (sprinting ? Mathf.Sin(phase * Mathf.PI * 2f) * 2.5f : Mathf.Sin(phase * Mathf.PI * 2f) * 1.2f) : Mathf.Sin(Time.time * 1.3f) * 0.4f;
            
            bool parentedToHand = enemyWeapon.parent != null && enemyWeapon.parent != modelRoot && enemyWeapon.parent != transform;
            if (parentedToHand)
            {
                // Weapon is attached directly to the right hand bone: preserve solid palm grip
                enemyWeapon.localPosition = weaponRestPosition;
                float readyPitch = ready * (-3f + sway)+reloadPose*19f;
                enemyWeapon.localRotation = weaponRestRotation * Quaternion.Euler(readyPitch, 0f, 0f);
            }
            else
            {
                Vector3 lowOffset = new Vector3(-0.02f, -0.04f, -0.02f);
                Vector3 combatOffset = new Vector3(0.01f, 0.01f, 0.015f);
                Vector3 crouchOffset = new Vector3(0.01f, -0.05f, 0.03f) * cover;
                
                Vector3 targetLocalPos = weaponRestPosition + Vector3.Lerp(lowOffset, combatOffset, ready) + crouchOffset-Vector3.up*.08f*reloadPose;
                enemyWeapon.localPosition = Vector3.Lerp(enemyWeapon.localPosition, targetLocalPos, Time.deltaTime * 8.5f);
                
                Quaternion lowReadyRot = weaponRestRotation * Quaternion.Euler(16f, -8f, 10f);
                Quaternion combatReadyRot = weaponRestRotation * Quaternion.Euler(ready * (-2f + sway) + cover * 8f+reloadPose*19f, ready * 1.5f, ready * -2f);
                Quaternion targetRot = Quaternion.Slerp(lowReadyRot, combatReadyRot, ready);
                
                if (sprinting)
                {
                    targetRot *= Quaternion.Euler(Mathf.Sin(phase * Mathf.PI * 2f) * 4f, Mathf.Cos(phase * Mathf.PI) * 3f, 0f);
                }
                
                enemyWeapon.localRotation = Quaternion.Slerp(enemyWeapon.localRotation, targetRot, Time.deltaTime * 8.5f);
            }
            
            if (weaponLeftHand != null && combat && !bot.IsReloading)
            {
                Vector3 foreEnd = enemyWeapon.TransformPoint(new Vector3(.20f, 0f, 0f));
                Vector3 toForeEnd = foreEnd - weaponLeftHand.position;
                if (toForeEnd.sqrMagnitude > .0001f)
                    weaponLeftHand.rotation = Quaternion.Slerp(weaponLeftHand.rotation, Quaternion.LookRotation(toForeEnd.normalized, transform.up), Time.deltaTime * 5f);
            }
            if (leftArm != null && rightArm != null)
            {
                float carry = combat ? 1f : .32f;
                float walkSway = moving ? Mathf.Sin(phase * Mathf.PI * 2f) * (sprinting ? 4f : 2f) : 0f;
                leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation,
                    leftArmRest * Quaternion.AngleAxis((-6f - cover * 8f) * carry - walkSway-reloadPose*24f, leftArmAxis), Time.deltaTime * 6.5f);
                rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation,
                    rightArmRest * Quaternion.AngleAxis((-4f - cover * 6f) * carry + walkSway * .35f, rightArmAxis), Time.deltaTime * 6.5f);
            }
        }

        void AnimateLeg(Leg leg)
        {
            if (!leg.Valid) return;
            float cycle = Mathf.Repeat(phase + leg.PhaseOffset, 1f);
            
            Vector3 hipPitchAxis = leg.HipAxis;
            Vector3 kneePitchAxis = leg.KneeAxis;
            Vector3 footPitchAxis = leg.FootAxis;

            float hipAngle = 0f;
            float kneeAngle = 0f;
            float footAngle = 0f;

            if (cycle < 0.5f)
            {
                // Stance phase (0.0-0.5): Heel strike (forward) to toe push-off (backward)
                float t = cycle / 0.5f;
                hipAngle = Mathf.Lerp(-22f, 18f, Mathf.SmoothStep(0f, 1f, t)) * blend;
                
                // Loading response shock absorption: knee flexes around t=0.2 then extends for push
                float impactKnee = Mathf.Sin(t * Mathf.PI) * (t < 0.35f ? 16f : 8f);
                kneeAngle = impactKnee * blend;
                
                // Heel strike (-10 deg) rolling through flat to toe push-off (+16 deg)
                footAngle = Mathf.Lerp(-10f, 16f, t) * blend;
            }
            else
            {
                // Swing phase (0.5-1.0): Leg lifts, knee bends, swings forward
                float t = (cycle - 0.5f) / 0.5f;
                hipAngle = Mathf.Lerp(18f, -22f, Mathf.SmoothStep(0f, 1f, t)) * blend;
                
                // Dynamic knee lift: higher when sprinting
                float maxKneeLift = (bot != null && bot.CurrentSpeed > 3.2f) ? 58f : 46f;
                kneeAngle = Mathf.Sin(t * Mathf.PI) * maxKneeLift * blend;
                
                // Ankle clears ground (dorsiflexion) then prepares for heel strike
                footAngle = -Mathf.Sin(t * Mathf.PI) * 14f * blend;
            }

            // Crouch stance leg adjustments
            if (coverBlend > 0.01f)
            {
                hipAngle -= 24f * coverBlend;
                kneeAngle += 44f * coverBlend;
                footAngle += 14f * coverBlend;
            }

            leg.Hip.localRotation = leg.HipRest * Quaternion.AngleAxis(hipAngle, hipPitchAxis);
            leg.Knee.localRotation = leg.KneeRest * Quaternion.AngleAxis(kneeAngle, kneePitchAxis);
            leg.Foot.localRotation = leg.FootRest * Quaternion.AngleAxis(-footAngle, footPitchAxis);
        }

        public bool EnablePhysicalRagdoll(Vector3 impactDir, Vector3 impactPos)
        {
            if (modelRoot == null) return false;

            Transform hips = FindBone("hips", "pelvis", "bip01", "root");
            if (hips == null && left != null && left.Hip != null) hips = left.Hip.parent;
            if (hips == null) hips = modelRoot;

            Transform[] keyBones = new Transform[] {
                hips,
                spine,
                FindBone("head", "neck"),
                left != null ? left.Hip : null,
                left != null ? left.Knee : null,
                right != null ? right.Hip : null,
                right != null ? right.Knee : null,
                leftArm,
                rightArm
            };

            bool addedAny = false;
            Rigidbody hipsRb = null;

            foreach (var b in keyBones)
            {
                if (b == null) continue;
                var col = b.GetComponent<Collider>();
                if (col == null)
                {
                    var sc = b.gameObject.AddComponent<SphereCollider>();
                    sc.radius = (b == spine || b == hips) ? 0.22f : 0.13f;
                    col = sc;
                }
                col.enabled = true;

                var rb = b.GetComponent<Rigidbody>();
                if (rb == null) rb = b.gameObject.AddComponent<Rigidbody>();
                rb.mass = (b == hips || b == spine) ? 18f : (b == leftArm || b == rightArm) ? 6f : 8f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.isKinematic = false;

                if (b == hips) hipsRb = rb;

                if (b != hips && b.parent != null)
                {
                    var parentRb = b.parent.GetComponentInParent<Rigidbody>();
                    if (parentRb != null && b.GetComponent<Joint>() == null)
                    {
                        var joint = b.gameObject.AddComponent<HingeJoint>();
                        joint.connectedBody = parentRb;
                        joint.axis = Vector3.right;
                        joint.useLimits = true;
                        JointLimits limits = joint.limits;
                        limits.min = -60f;
                        limits.max = 60f;
                        joint.limits = limits;
                    }
                }

                addedAny = true;
            }

            if (addedAny)
            {
                this.enabled = false;
                var rootCol = GetComponent<Collider>();
                if (rootCol != null) rootCol.enabled = false;

                if (hipsRb != null)
                {
                    hipsRb.AddForce(impactDir * UnityEngine.Random.Range(240f, 360f) + Vector3.up * 115f, ForceMode.Impulse);
                    hipsRb.AddTorque(UnityEngine.Random.insideUnitSphere * 180f, ForceMode.Impulse);
                }
                return true;
            }
            return false;
        }
    }
}
