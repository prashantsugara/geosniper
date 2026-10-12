using UnityEngine;

namespace GeoSniper
{
    // Fallback gait for characters without authored locomotion clips.
    [DefaultExecutionOrder(80)]
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
        Vector3 headYawAxis,headPitchAxis,headRollAxis,spinePitchAxis,spineYawAxis,spineRollAxis;
        float idlePhase,idleLook,aimBlend,hitPose,shotPose;
        ArmyAnimation authoredArmy;
        CivilianAnimation authoredCivilian;
        Animator[] authoredAnimators;
        EnemyWeaponAnchor weaponAnchor;
        float nextWeaponLookup;

        bool AuthoredPoseActive
        {
            get
            {
                if(authoredArmy!=null && authoredArmy.enabled && authoredArmy.Ready) return true;
                if(authoredCivilian!=null && authoredCivilian.enabled && authoredCivilian.Ready)return true;
                if(authoredAnimators!=null)
                    foreach(var animator in authoredAnimators)
                        if(animator!=null && animator.enabled && animator.gameObject.activeInHierarchy
                            && animator.runtimeAnimatorController!=null) return true;
                return false;
            }
        }
        void CaptureDrivers()
        {
            authoredArmy=GetComponent<ArmyAnimation>();
            authoredCivilian=GetComponent<CivilianAnimation>();
            authoredAnimators=modelRoot.GetComponentsInChildren<Animator>(true);
        }
        void CaptureSpine()
        {
            spine=FindBone("spine1", "spine01", "chest", "spine", "torso", "upperbody");
            if(spine==null)return;
            spineRest=spine.localRotation;
            spinePitchAxis=spine.InverseTransformDirection(transform.right).normalized;
            spineYawAxis=spine.InverseTransformDirection(transform.up).normalized;
            spineRollAxis=spine.InverseTransformDirection(transform.forward).normalized;
        }
        void CaptureWeapon()
        {
            if(bot==null || bot.isDisarmed || modelRoot==null)return;
            enemyWeapon=FindBone("enemy tactical rifle", "attached_ak47");
            if(enemyWeapon==null)return;
            weaponRestPosition=enemyWeapon.localPosition;
            weaponRestRotation=enemyWeapon.localRotation;
            weaponLeftHand=FindBone("lefthand", "hand_l", "left_hand", "l_wrist", "mixamoriglefthand");
            weaponAnchor=enemyWeapon.GetComponent<EnemyWeaponAnchor>();
        }
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
            CaptureDrivers();
            // A fallback component must never realign or overwrite an authored rig.
            if(AuthoredPoseActive)return;
            left=CreateLeg("left",0); right=CreateLeg("right",.5f);
            leftArm = FindBone("leftarm", "leftshoulder", "l_upperarm", "l_arm", "upperarml", "arml", "bipupperarml");
            rightArm = FindBone("rightarm", "rightshoulder", "r_upperarm", "r_arm", "upperarmr", "armr", "bipupperarmr");
            if (leftArm != null) leftArmRest = leftArm.localRotation;
            if (rightArm != null) rightArmRest = rightArm.localRotation;
            if (leftArm != null) leftArmAxis = leftArm.InverseTransformDirection(transform.right).normalized;
            if (rightArm != null) rightArmAxis = rightArm.InverseTransformDirection(transform.right).normalized;
            CaptureWeapon();
            CaptureSpine();
            CaptureHead();
            previousPosition=transform.position;
        }
        public void Initialize(CivilianBot owner,Transform visual)
        {
            civilian=owner; modelRoot=visual;
            if(visual==null || visual==transform) return;
            CaptureRoot();
            CaptureDrivers();
            if(AuthoredPoseActive)return;
            left=CreateLeg("left",0); right=CreateLeg("right",.5f);
            leftArm = FindBone("leftarm", "leftshoulder", "l_upperarm", "l_arm", "upperarml", "arml", "bipupperarml");
            rightArm = FindBone("rightarm", "rightshoulder", "r_upperarm", "r_arm", "upperarmr", "armr", "bipupperarmr");
            RelaxArm(leftArm,-1);
            RelaxArm(rightArm,1);
            if(leftArm!=null) leftArmRest=leftArm.localRotation;
            if(rightArm!=null) rightArmRest=rightArm.localRotation;
            if(leftArm!=null) leftArmAxis=leftArm.InverseTransformDirection(transform.right).normalized;
            if(rightArm!=null) rightArmAxis=rightArm.InverseTransformDirection(transform.right).normalized;
            CaptureSpine();
            CaptureHead();
            previousPosition=transform.position;
        }
        void CaptureHead()
        {
            head=FindBone("head");
            if(head==null)return;
            headRest=head.localRotation;
            headYawAxis=head.InverseTransformDirection(transform.up).normalized;
            headPitchAxis=head.InverseTransformDirection(transform.right).normalized;
            headRollAxis=head.InverseTransformDirection(transform.forward).normalized;
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
                // Lower T- and A-pose arms; retain already-down resting arms.
                if(Mathf.Abs(Vector3.Dot(direction.normalized,transform.up))<.92f)
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

        void LateUpdate()
        {
            if ((bot == null && civilian == null) || modelRoot==null || AuthoredPoseActive) return;
            // The bot moves in Update. Rebuild the fallback pose after movement, before
            // EnemyBot's aim correction and the weapon anchor's final hand contacts.
            float delta=Mathf.Max(0f,Time.deltaTime);
            if(bot!=null)
            {
                aimBlend=Mathf.MoveTowards(aimBlend,bot.IsAiming?1f:0f,delta*6f);
                float response=1f-Mathf.Exp(-delta*32f);
                hitPose=Mathf.Lerp(hitPose,Mathf.Clamp01(bot.HitReactionStrength),response);
                shotPose=Mathf.Lerp(shotPose,Mathf.Clamp01(bot.ShotReactionStrength),response);
                // Enemy weapons are built after the fallback driver is initialized.
                if(enemyWeapon==null && Time.time>=nextWeaponLookup)
                {nextWeaponLookup=Time.time+.5f;CaptureWeapon();}
            }
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
            // Respawns and mission placement are not a sprint or a hard impact.
            if(movement.sqrMagnitude>4f)
            {movement=Vector3.zero;smoothedVelocity=Vector3.zero;smoothedTurnRate=0f;}
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
            bool sprinting = curSpeed > 3.4f || (bot != null && (bot.IsSprinting || bot.isFugitiveRunner));

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
                bool takingCover = bot != null && bot.IsCrouched;
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
                if (bot != null)
                {
                    // Disarm/leg injuries use a longer stagger than a normal hit.
                    // Clamp before sin so these impulses never reverse direction.
                    float stagger=Mathf.Sin(Mathf.Clamp01(bot.StaggerProgress)*Mathf.PI);
                    float magnitude=Mathf.Max(hitPose,stagger*.45f);
                    Vector3 localFlinchDir=transform.InverseTransformDirection(bot.FlinchDirection);
                    flinchPitch=-Mathf.Clamp(localFlinchDir.z,-1f,1f)*8f*magnitude;
                    flinchRoll=Mathf.Clamp(bot.HitReactionSide,-1f,1f)*5f*magnitude;
                }

                modelRoot.localPosition = rootRestPosition + Vector3.up * (bob - .20f * coverBlend)
                    +Vector3.back*(.025f*hitPose);
                modelRoot.localRotation = rootRestRotation * Quaternion.Euler(Mathf.Clamp(pitch,-8f,8f)+flinchPitch+6f*coverBlend,hipYaw,totalRoll+flinchRoll+coverLean);

                // Keep recoil/flinch in the upper body so a reaction does not replace gait.
                if (spine != null)
                {
                    float forwardHunch = (bot!=null?Mathf.Lerp(1.5f,3.5f,aimBlend):(combat?3.5f:1.5f))
                        + (sprinting ? 2.5f : 0f) + (coverBlend * (civilianCowering ? 17f : 4f));
                    float hitSide=bot!=null?Mathf.Clamp(bot.HitReactionSide,-1f,1f):0f;
                    spine.localRotation=spineRest
                        *Quaternion.AngleAxis(forwardHunch-hitPose*13f-shotPose*3f,spinePitchAxis)
                        *Quaternion.AngleAxis(-hipYaw*.45f+hitSide*hitPose*9f,spineYawAxis)
                        *Quaternion.AngleAxis(hitSide*hitPose*6f,spineRollAxis);
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
                float hitSide=bot!=null?Mathf.Clamp(bot.HitReactionSide,-1f,1f):0f;
                head.localRotation=headRest*Quaternion.AngleAxis(idleLook+hitSide*hitPose*5f,headYawAxis)
                    *Quaternion.AngleAxis(-hitPose*9f-shotPose*2f,headPitchAxis)
                    *Quaternion.AngleAxis(hitSide*hitPose*5f,headRollAxis);
            }
        }

        void PoseEnemyWeapon(bool moving, float cover, float curSpeed)
        {
            if (bot == null || enemyWeapon == null || bot.Actor == null || bot.Actor.IsDead) return;
            if (bot.isDisarmed) return;
            bool combat = bot.IsAiming;
            bool sprinting = curSpeed > 3.4f || bot.IsSprinting || bot.isFugitiveRunner;
            float reloadPose=bot.IsReloading?Mathf.Sin(bot.ReloadProgress*Mathf.PI):0f;
            float ready = aimBlend;
            
            // Tactical sway and pump
            float sway = moving ? (sprinting ? Mathf.Sin(phase * Mathf.PI * 2f) * 2.5f : Mathf.Sin(phase * Mathf.PI * 2f) * 1.2f) : Mathf.Sin(Time.time * 1.3f) * 0.4f;
            
            bool parentedToHand = enemyWeapon.parent != null && enemyWeapon.parent != modelRoot && enemyWeapon.parent != transform;
            if (weaponAnchor!=null && weaponAnchor.enabled)
            {
                // The anchor solves both palm contacts later in LateUpdate.
            }
            else if (parentedToHand)
            {
                // Weapon is attached directly to the right hand bone: preserve solid palm grip
                enemyWeapon.localPosition = weaponRestPosition;
                float readyPitch = ready * (-3f + sway)+reloadPose*19f-shotPose*3f;
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
                Quaternion combatReadyRot = weaponRestRotation * Quaternion.Euler(ready * (-2f + sway) + cover * 8f+reloadPose*19f-shotPose*3f, ready * 1.5f, ready * -2f);
                Quaternion targetRot = Quaternion.Slerp(lowReadyRot, combatReadyRot, ready);
                
                if (sprinting)
                {
                    targetRot *= Quaternion.Euler(Mathf.Sin(phase * Mathf.PI * 2f) * 4f, Mathf.Cos(phase * Mathf.PI) * 3f, 0f);
                }
                
                enemyWeapon.localRotation = Quaternion.Slerp(enemyWeapon.localRotation, targetRot, Time.deltaTime * 8.5f);
            }
            
            if (weaponLeftHand != null && combat && !bot.IsReloading && (weaponAnchor==null || !weaponAnchor.enabled))
            {
                Vector3 foreEnd = enemyWeapon.TransformPoint(new Vector3(.20f, 0f, 0f));
                Vector3 toForeEnd = foreEnd - weaponLeftHand.position;
                if (toForeEnd.sqrMagnitude > .0001f)
                    weaponLeftHand.rotation = Quaternion.Slerp(weaponLeftHand.rotation, Quaternion.LookRotation(toForeEnd.normalized, transform.up), Time.deltaTime * 5f);
            }
            if (leftArm != null && rightArm != null)
            {
                float carry = Mathf.Lerp(.32f,1f,ready);
                float walkSway = moving ? Mathf.Sin(phase * Mathf.PI * 2f) * (sprinting ? 4f : 2f) : 0f;
                leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation,
                    leftArmRest * Quaternion.AngleAxis((-6f - cover * 8f) * carry - walkSway-reloadPose*24f-hitPose*7f-shotPose*3f, leftArmAxis), Time.deltaTime * 6.5f);
                rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation,
                    rightArmRest * Quaternion.AngleAxis((-4f - cover * 6f) * carry + walkSway * .35f-hitPose*5f-shotPose*4f, rightArmAxis), Time.deltaTime * 6.5f);
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

        Transform FindRagdollBone(params string[] tokens)
        {
            // Mesh objects can contain names such as Soldier_head; only deforming bones belong in the skeleton.
            foreach(var token in tokens)
                foreach(var skin in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    foreach(var bone in skin.bones)
                    {
                        if(bone==null)continue;
                        string name=bone.name.ToLowerInvariant().Replace(":","").Replace("_","").Replace(".","");
                        if(name.EndsWith(token.Replace("_","")))return bone;
                    }
            return null;
        }
        public bool EnablePhysicalRagdoll(Vector3 impactDir, Vector3 impactPos)
        {
            if (modelRoot == null) return false;

            Transform hips = FindRagdollBone("hips", "pelvis", "bip01", "root");
            if (hips == null && left != null && left.Hip != null) hips = left.Hip.parent;
            if (hips == null) hips = modelRoot;

            Transform[] keyBones = new Transform[] {
                hips,
                FindRagdollBone("spine2", "spine1", "spine", "chest"),
                FindRagdollBone("head", "neck"),
                FindRagdollBone("leftupleg", "thigh_l", "leftthigh"),
                FindRagdollBone("leftleg", "calf_l", "leftcalf"),
                FindRagdollBone("rightupleg", "thigh_r", "rightthigh"),
                FindRagdollBone("rightleg", "calf_r", "rightcalf"),
                FindRagdollBone("leftarm", "upperarm_l"),
                FindRagdollBone("rightarm", "upperarm_r"),
                FindRagdollBone("leftforearm", "lowerarm_l"),
                FindRagdollBone("rightforearm", "lowerarm_r"),
                FindRagdollBone("leftfoot", "foot_l"),
                FindRagdollBone("rightfoot", "foot_r")
            };

            bool addedAny = false;
            Rigidbody hipsRb = null;
            var bones=new System.Collections.Generic.List<Transform>();

            foreach (var b in keyBones)
            {
                if (b == null || bones.Contains(b)) continue;
                bones.Add(b);
                var col = b.GetComponent<Collider>();
                if (col == null)
                {
                    var sc = b.gameObject.AddComponent<SphereCollider>();
                    Vector3 scale=b.lossyScale;
                    float boneScale=Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                    sc.radius = (b == hips ? 0.18f : 0.11f)/Mathf.Max(.0001f,boneScale);
                    col = sc;
                }
                col.enabled = true;

                var rb = b.GetComponent<Rigidbody>();
                if (rb == null) rb = b.gameObject.AddComponent<Rigidbody>();
                rb.mass = b == hips ? 18f : b==keyBones[1] ? 12f : 4f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.linearDamping=.15f;rb.angularDamping=1.2f;rb.maxAngularVelocity=5f;
                rb.solverIterations=12;rb.solverVelocityIterations=6;
                rb.isKinematic = false;

                if (b == hips) hipsRb = rb;

                addedAny = true;
            }
            // Build joints after every body exists, including authored rigs which skip fallback gait setup.
            foreach(var b in bones)
            {
                if (b != hips && b.parent != null)
                {
                    var parentRb = b.parent.GetComponentInParent<Rigidbody>();
                    if (parentRb != null && b.GetComponent<Joint>() == null)
                    {
                        var joint = b.gameObject.AddComponent<CharacterJoint>();
                        joint.connectedBody = parentRb;
                        joint.axis = b.InverseTransformDirection(transform.right);
                        joint.swingAxis = b.InverseTransformDirection(transform.forward);
                        joint.lowTwistLimit=new SoftJointLimit{limit=-25};
                        joint.highTwistLimit=new SoftJointLimit{limit=25};
                        joint.swing1Limit=new SoftJointLimit{limit=45};
                        joint.swing2Limit=new SoftJointLimit{limit=30};
                        joint.enableProjection=true;joint.projectionDistance=.08f;joint.projectionAngle=15;
                    }
                }

            }

            if (addedAny)
            {
                this.enabled = false;
                var army=GetComponent<ArmyAnimation>();if(army!=null)army.enabled=false;
                foreach(var animation in modelRoot.GetComponentsInChildren<Animation>(true)){animation.Stop();animation.enabled=false;}
                foreach(var animator in modelRoot.GetComponentsInChildren<Animator>(true))animator.enabled=false;
                // Overlapping neighboring bone spheres must not push the skeleton apart.
                for(int a=0;a<bones.Count;a++)for(int b=a+1;b<bones.Count;b++)
                    Physics.IgnoreCollision(bones[a].GetComponent<Collider>(),bones[b].GetComponent<Collider>());
                var rootCol = GetComponent<Collider>();
                if (rootCol != null) rootCol.enabled = false;

                if (hipsRb != null)
                {
                    hipsRb.AddForce(impactDir.normalized*9f, ForceMode.Impulse);
                    hipsRb.AddTorque(Vector3.Cross(impactDir,Vector3.up)*.8f, ForceMode.Impulse);
                }
                return true;
            }
            return false;
        }
    }
}
