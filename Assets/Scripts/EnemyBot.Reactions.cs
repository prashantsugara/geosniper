using UnityEngine;

namespace GeoSniper
{
    public sealed partial class EnemyBot
    {
        public float HitReactionStrength { get; private set; }
        public float HitReactionSide { get; private set; } = 1f;
        public float ShotReactionStrength { get; private set; }
        float acquisitionRemaining, staggerDuration = .35f;
        bool hadVisualContact, aimKnowledgeReady, burstActive;
        int burstGeneration;
        Vector3 observedAim, observedPlayerPosition, observedVelocity;
        Vector3 committedSniperAim;
        bool sniperAimCommitted;
        float nextThreatReaction,underFireUntil;
        readonly RaycastHit[] bulletHits = new RaycastHit[32];

        float ResponsiveMoveSpeed => isCounterSniper || isFugitiveRunner || isHobbled || PassiveTrainingTarget
            || currentState==AIState.Patrol || currentState==AIState.Idle ? speed : Mathf.Max(speed,2.6f);

        bool ClosePlayerVisible(float range)
        {
            if(player==null || (player.GetComponent<CombatActor>()?.IsDead ?? false))return false;
            Vector3 delta=player.position-transform.position;
            if(delta.magnitude>range)return false;
            Vector3 flat=delta;flat.y=0;
            if(delta.magnitude>2.5f && Vector3.Angle(transform.forward,flat)>85f)return false;
            return SourceVisible(player.position+Vector3.up*((player.GetComponent<UrbanPlayer>()?.IsCrouching??false)?.85f:1.3f));
        }

        void TickReaction(float delta)
        {
            HitReactionStrength = Mathf.MoveTowards(HitReactionStrength, 0, delta * 4f);
            ShotReactionStrength = Mathf.MoveTowards(ShotReactionStrength, 0, delta * 14f);
            suppressionTimer = Mathf.Max(0, suppressionTimer - delta);
            acquisitionRemaining = Mathf.Max(0, acquisitionRemaining - delta);
            if (BarkTimer > 0) BarkTimer = Mathf.Max(0, BarkTimer - delta);
            if (muzzleFlash != null && muzzleFlashTimer > 0)
            {
                muzzleFlashTimer = Mathf.Max(0, muzzleFlashTimer - delta);
                muzzleFlash.intensity = 3.5f * muzzleFlashTimer / .06f;
            }
        }

        void SetStagger(float seconds)
        {
            staggerTimer = Mathf.Max(staggerTimer, seconds);
            staggerDuration = Mathf.Max(staggerDuration, staggerTimer);
        }

        void UpdateAimKnowledge(bool visible, Vector3 eyeTarget, float distance, float delta)
        {
            if (visible)
            {
                if (!hadVisualContact || !aimKnowledgeReady)
                {
                    acquisitionRemaining = Mathf.Max(acquisitionRemaining,
                        EnemyReactionPolicy.AcquisitionDelay(ReactionScale, distance, Random.value, currentState == AIState.Combat || currentState == AIState.TakeCover));
                    observedAim = eyeTarget; observedPlayerPosition = player.position; observedVelocity = Vector3.zero;
                    aimKnowledgeReady = true;
                }
                else
                {
                    Vector3 velocity = (player.position - observedPlayerPosition) / Mathf.Max(.001f, delta);
                    observedVelocity = Vector3.Lerp(observedVelocity, Vector3.ClampMagnitude(velocity, 12f), 1f - Mathf.Exp(-5f * delta));
                    float response = Mathf.Clamp(8f / Mathf.Max(.4f, ReactionScale), 4f, 14f);
                    observedAim = Vector3.Lerp(observedAim, eyeTarget, 1f - Mathf.Exp(-response * delta));
                    observedPlayerPosition = player.position;
                }
            }
            else
            {
                if (hadVisualContact) ResetPlayerTactics();
                observedVelocity = Vector3.zero;
            }
            hadVisualContact = visible;
        }

        bool CanShootObservedTarget(float aimAngle = 12f)
        {
            Vector3 flat = lastSeenPosition - transform.position; flat.y = 0;
            return !burstActive && player != null && !(player.GetComponent<CombatActor>()?.IsDead ?? false)
                && EnemyReactionPolicy.CanFire(playerVisible, actor != null && !actor.IsDead,
                !isDisarmed, IsReloading, staggerTimer, suppressionTimer, acquisitionRemaining,
                flat.sqrMagnitude > .001f ? Vector3.Angle(transform.forward, flat) : 0, aimAngle);
        }

        bool SourceVisible(Vector3 source)
            => EnemyCoverGeometry.ClearShot(transform.position + Vector3.up * (IsCrouched ? .95f : 1.55f), source, transform, player, coverPathHits);

        bool ImpactVisible(Vector3 point)
        {
            Vector3 eye = transform.position + Vector3.up * (IsCrouched ? .95f : 1.55f);
            Vector3 delta = point - eye;
            // The struck surface is the endpoint, rather than cover between us and the impact.
            Vector3 endpoint = point - delta.normalized * Mathf.Min(.05f, delta.magnitude);
            return EnemyCoverGeometry.ClearShot(eye, endpoint, transform, player, coverPathHits);
        }

        void CancelSniperLock()
        {
            sniperAimTimer = 0; sniperAimCommitted = false;
            if (sniperLaser != null) sniperLaser.enabled = false;
            if (scopeGlintObj != null) scopeGlintObj.SetActive(false);
            if (ActiveAimingCounterSniper == this) ActiveAimingCounterSniper = null;
        }

        Vector3 EstimateThreat(Vector3 source)
        {
            // Only a current visual observation supplies an exact player position.
            if (playerVisible && player != null && SourceVisible(player.position + Vector3.up * 1.1f)) return player.position;
            // Sound/damage reveal a direction and rough area, rather than a moving target behind walls.
            Vector2 uncertainty=Random.insideUnitCircle*5f;
            return source + new Vector3(uncertainty.x,0,uncertainty.y);
        }

        void ReactToShotThreat(Vector3 source, bool takeCover, bool nearPass)
        {
            if (actor == null || actor.IsDead || Suspended || PassiveTrainingTarget) return;
            ObservationPatrol=false;
            if(takeCover)underFireUntil=Time.time+3f;
            if (takeCover) suppressionTimer = EnemyReactionPolicy.SuppressionSeconds(suppressionTimer, nearPass, ReactionScale);
            if (Time.time < nextThreatReaction) return;
            nextThreatReaction = Time.time + .45f;
            Vector3 remembered = EstimateThreat(source);
            suspiciousLocation = remembered;
            if (!playerVisible) lastSeenPosition = remembered;
            provokedUntil = Time.time + 14f;
            acquisitionRemaining = Mathf.Max(acquisitionRemaining, EnemyReactionPolicy.AcquisitionDelay(ReactionScale, Vector3.Distance(source, transform.position), .5f, true));
            searching = false; stateTimer = takeCover ? 7f : 6f;
            nextVisionCheck = 0;
            if (takeCover)
            {
                detectionProgress = Mathf.Max(detectionProgress, .8f);
                ResetPlayerTactics();
                if (!isCounterSniper && !isFugitiveRunner)
                {
                    bool choose = currentState != AIState.TakeCover;
                    currentState = AIState.TakeCover;
                    if (choose) ChooseCoverTarget(remembered - transform.position);
                    if (coverSubState == CoverSubState.Peeking)
                    {
                        coverSubState = CoverSubState.Crouching; returningToCover = true;
                    }
                }
                CancelSniperLock();
                TriggerBark("INCOMING! GET TO COVER!", 2f);
            }
            else if (currentState != AIState.Combat && currentState != AIState.TakeCover)
            {
                detectionProgress = Mathf.Max(detectionProgress, .35f);
                currentState = AIState.Suspicious;
                TriggerBark("GUNSHOT! CHECK THAT AREA.", 2f);
            }
        }

        Vector3 BulletDirection(Vector3 origin, Vector3 aim, float spreadDegrees)
        {
            Vector3 forward = (aim - origin).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < .01f) right = transform.right;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            Vector2 scatter = Random.insideUnitCircle * Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
            return (forward + right * scatter.x + up * scatter.y).normalized;
        }

        bool ResolveEnemyBullet(Vector3 origin, Vector3 direction, float range, float damage, out Vector3 impact)
        {
            impact = origin + direction * range;
            if (player == null || !FinitePoint(origin) || !FinitePoint(direction) || direction.sqrMagnitude < .99f || !float.IsFinite(range) || range <= 0) return false;
            int count = Physics.RaycastNonAlloc(origin, direction, bulletHits, range, ~0, QueryTriggerInteraction.Ignore);
            if (count >= bulletHits.Length) return false;
            float nearest = range;
            Collider collider = null;
            for (int i = 0; i < count; i++)
            {
                var candidate = bulletHits[i];
                if (candidate.transform == transform || candidate.transform.IsChildOf(transform) || candidate.distance >= nearest) continue;
                nearest = candidate.distance; collider = candidate.collider; impact = candidate.point;
            }
            bool playerHit = collider != null && (collider.transform == player || collider.transform.IsChildOf(player));
            if (playerHit) player.GetComponent<CombatActor>()?.Damage(damage, origin);
            else
            {
                if (collider != null) SniperPresentation.SpawnSurfaceImpact(impact, collider);
                Vector3 chest = player.position + Vector3.up * ((player.GetComponent<UrbanPlayer>()?.IsCrouching ?? false) ? .6f : 1.1f);
                float pass = EnemyReactionPolicy.FlightDistance(chest.x,chest.y,chest.z,origin.x,origin.y,origin.z,impact.x,impact.y,impact.z);
                if (pass <= 1.5f) player.GetComponent<UrbanPlayer>()?.ApplyRecoil(Random.Range(3f, 7f));
            }
            return playerHit;
        }

        static bool FinitePoint(Vector3 point) => float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
    }
}
