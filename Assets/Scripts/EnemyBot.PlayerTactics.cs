using UnityEngine;

namespace GeoSniper
{
    public sealed partial class EnemyBot
    {
        Vector3 tacticalAnchor;
        bool tacticalAnchorSet;
        float defensiveMoveCooldown;
        int tacticalStep;
        float preferredCombatSide=1f,combatTemperament=.5f;
        Vector3 previousCombatDestination;
        bool hasPreviousCombatDestination;

        // Combat movement uses only the position we can currently observe. The
        // caller retains responsibility for aiming, shooting and searching.
        bool UpdatePlayerTactics(bool visible, float distance, float delta)
        {
            if (isCounterSniper || isVipAttacker || isFugitiveRunner || ObservationPatrol
                || Suspended || actor == null || actor.IsDead || isDisarmed) return false;
            defensiveMoveCooldown = Mathf.Max(0f, defensiveMoveCooldown - delta);
            flankCooldown = Mathf.Max(0f, flankCooldown - delta);
            if (!visible)
            {
                // Never finish a flank by following the player's hidden position.
                ResetPlayerTactics();
                return false;
            }
            if (!tacticalAnchorSet)
            {
                tacticalAnchor = transform.position;
                tacticalAnchorSet = true;
            }

            bool wounded = actor.Health <= actor.maxHealth * .4f;
            if ((suppressionTimer > 0f || wounded || IsReloading) && defensiveMoveCooldown <= 0f)
            {
                defensiveMoveCooldown = wounded ? 9f : 6f;
                combatTargetReady = false;
                flankActive = false;
                currentState = AIState.TakeCover;
                stateTimer = Mathf.Max(stateTimer, wounded ? 6f : 4f);
                ChooseCoverTarget(lastSeenPosition - transform.position);
                TriggerBark(wounded ? "HIT! FALLING BACK!" : "TAKING COVER!", 2f);
                return true;
            }

            if (UpdateFlank(visible, delta)) return true;
            if (currentRole == CombatRole.Suppressor && HasAdvancingTeammate())
            {
                combatTargetReady = false;
                MoveTowards(transform.position, 0f, false);
                return true;
            }

            // A brief settle between short movements makes bursts readable, while
            // avoiding the old long stationary pause after every shot.
            bool pressured=Time.time<underFireUntil || distance<8f;
            combatPause = Mathf.Min(combatPause, (pressured?.45f:2.4f) * ReactionScale);
            if (combatPause > 0f)
            {
                combatPause = Mathf.Max(0f, combatPause - delta);
                MoveTowards(transform.position, 0f, false);
                return true;
            }

            if (!combatTargetReady && !TryChoosePlayerCombatStep(distance))
            {
                combatPause = .65f * ReactionScale;
                MoveTowards(transform.position, 0f, false);
                return true;
            }
            MoveTowards(combatTarget, currentRole == CombatRole.Suppressor ? .8f : 1.05f, false);
            combatTargetTime -= delta;
            if ((combatTarget - transform.position).sqrMagnitude <= .6f * .6f
                || combatTargetTime <= 0f || blockedTime > .35f)
            {
                combatTargetReady = false;
                combatPause = (pressured?Random.Range(.18f,.65f):Random.Range(.9f,2.2f)) * ReactionScale;
                blockedTime = 0f;
            }
            return true;
        }

        bool TryChoosePlayerCombatStep(float distance)
        {
            if (!tacticalAnchorSet)
            {
                tacticalAnchor = transform.position;
                tacticalAnchorSet = true;
            }
            Vector3 toward = lastSeenPosition - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude < .01f) return false;
            toward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toward);
            tacticalStep++;
            if(Random.value<.2f)preferredCombatSide=-preferredCombatSide;
            float step = currentRole == CombatRole.Suppressor ? Random.Range(1.5f,2.2f) : Random.Range(2.2f,4.5f);
            float spacing = distance < 10f ? -3f : distance > Mathf.Max(30f, engagementRange * .8f) ? 2f : Mathf.Lerp(-1f,1f,combatTemperament);
            Vector3 chosen=default;float bestScore=float.NegativeInfinity;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float attemptSign = (attempt & 1) == 0 ? preferredCombatSide : -preferredCombatSide;
                float amount = attempt < 2 ? step : attempt < 4 ? step * .6f : step*.25f;
                Vector3 candidate = transform.position + side * (attemptSign * amount) + toward * (attempt>=4?spacing*1.5f:spacing);
                Vector3 fromAnchor = candidate - tacticalAnchor;
                fromAnchor.y = 0f;
                // Patrol guards contest a firing area instead of chasing the
                // player indefinitely or drifting across an entire rooftop.
                if (fromAnchor.sqrMagnitude > 10f * 10f) continue;
                if (!IsTacticalDestinationSafe(candidate, 2.5f)) continue;
                bool lane=EnemyCoverGeometry.ClearShot(candidate+Vector3.up*1.55f,lastSeenPosition+Vector3.up*1.1f,transform,player,coverPathHits);
                bool sheltered=EnemyCoverGeometry.Protected(candidate,lastSeenPosition+Vector3.up*1.55f,transform,player);
                float score=(lane?2f:-1f)+(sheltered?1.5f:0f)+(attemptSign==preferredCombatSide?.5f:0f)+Random.Range(0f,1.2f);
                if(hasPreviousCombatDestination && (candidate-previousCombatDestination).sqrMagnitude<4f)score-=4f;
                if(score<=bestScore)continue;
                bestScore=score;chosen=candidate;
            }
            if(float.IsNegativeInfinity(bestScore))return false;
            previousCombatDestination=transform.position;hasPreviousCombatDestination=true;
            combatTarget=chosen;combatTargetReady=true;combatTargetTime=Random.Range(1.2f,2.4f);blockedTime=0;
            return true;
        }

        bool IsTacticalDestinationSafe(Vector3 destination, float separation)
        {
            if (!SafeStep(destination) || !SectorWorld.DryFootprint(destination, .6f)
                || !EnemyCoverGeometry.Reachable(transform.position, destination, transform, coverPathHits)) return false;
            float separationSquared = separation * separation;
            foreach (var other in AllBots)
            {
                if (other == null || other == this || !other.isActiveAndEnabled
                    || other.actor == null || other.actor.IsDead || other.Suspended) continue;
                if ((other.transform.position - destination).sqrMagnitude < separationSquared
                    || (other.currentState == AIState.TakeCover && (other.coverTargetPos - destination).sqrMagnitude < separationSquared)
                    || (other.currentState == AIState.Combat && other.combatTargetReady && (other.combatTarget - destination).sqrMagnitude < separationSquared)
                    || (other.flankActive && (other.flankDestination - destination).sqrMagnitude < separationSquared)) return false;
            }
            return true;
        }

        void ResetPlayerTactics()
        {
            if (flankActive) flankCooldown = Mathf.Max(flankCooldown, 5f);
            tacticalAnchorSet = false;
            combatTargetReady = false;
            flankActive = false;
        }
    }
}
