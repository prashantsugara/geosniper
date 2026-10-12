using UnityEngine;

namespace GeoSniper
{
    public sealed partial class EnemyBot
    {
        float squadRoleTimer,flankCooldown,flankTime;
        bool flankActive;
        Vector3 flankDestination;
        public bool IsFlanking => flankActive && currentState==AIState.Combat && actor!=null && !actor.IsDead;

        bool SquadEligible(EnemyBot other)
        {
            return other!=null && other.isActiveAndEnabled && other.actor!=null && !other.actor.IsDead
                && !other.Suspended && !other.isDisarmed && !other.isCounterSniper && !other.isFugitiveRunner
                && !other.isVipAttacker && !other.ObservationPatrol && other.player==player && other.Alerts==Alerts
                && (other.currentState==AIState.Combat || other.currentState==AIState.TakeCover)
                && (other.transform.position-transform.position).sqrMagnitude<75f*75f;
        }

        void UpdateSquadRole(float delta)
        {
            squadRoleTimer-=delta;
            if(squadRoleTimer>0 || !SquadEligible(this))return;
            squadRoleTimer=1.5f;
            // Registry order is stable; one nearby coordinator assigns the group.
            EnemyBot leader=null;
            foreach(var other in AllBots)if(SquadEligible(other)){leader=other;break;}
            if(leader!=this)return;
            int count=0;
            foreach(var other in AllBots)if(SquadEligible(other))count++;
            // One flank at a time; the rest retain distinct assault lanes.
            // Pick a guard that can currently shoot to cover the advance.
            EnemyBot suppressor=null,flanker=null;
            foreach(var other in AllBots)
                if(SquadEligible(other) && other.playerVisible && !other.IsReloading && other.suppressionTimer<=0
                    && other.actor.Health>other.actor.maxHealth*.35f && !other.flankActive)
                {suppressor=other;break;}
            if(suppressor==null)suppressor=leader;
            foreach(var other in AllBots)
                if(SquadEligible(other) && other!=suppressor && other.flankActive){flanker=other;break;}
            if(flanker==null)
                foreach(var other in AllBots)
                    if(SquadEligible(other) && other!=suppressor && !other.IsReloading && !other.isHobbled
                        && other.suppressionTimer<=0 && other.actor.Health>other.actor.maxHealth*.45f)
                    {flanker=other;break;}
            foreach(var other in AllBots)
            {
                if(!SquadEligible(other))continue;
                other.currentRole=count<2?CombatRole.Assault:other==suppressor?CombatRole.Suppressor:
                    other==flanker?CombatRole.Flanker:CombatRole.Assault;
            }
        }

        EnemyBot CoveringTeammate()
        {
            foreach(var other in AllBots)
                if(other!=this && SquadEligible(other) && other.currentRole==CombatRole.Suppressor
                    && other.currentState==AIState.Combat && other.playerVisible && !other.IsReloading
                    && other.suppressionTimer<=0 && other.actor.Health>other.actor.maxHealth*.35f)return other;
            return null;
        }

        bool HasAdvancingTeammate()
        {
            foreach(var other in AllBots)if(other!=this && SquadEligible(other) && other.IsFlanking)return true;
            return false;
        }

        bool TryFlankDestination(Vector3 threat,out Vector3 destination)
        {
            destination=transform.position;
            Vector3 toward=threat-transform.position;toward.y=0;
            if(toward.magnitude<14f || toward.magnitude>110f)return false;
            toward.Normalize();
            Vector3 side=Vector3.Cross(Vector3.up,toward);
            float preferred=preferredCombatSide;
            for(int attempt=0;attempt<4;attempt++)
            {
                float sign=(attempt&1)==0?preferred:-preferred;
                Vector3 candidate=transform.position+side*sign*Random.Range(attempt<2?10f:6f,attempt<2?14f:9f)+toward*Random.Range(1f,4f);
                if(!IsTacticalDestinationSafe(candidate,3f))continue;
                destination=candidate;return true;
            }
            return false;
        }

        bool UpdateFlank(bool visible,float delta)
        {
            if(flankActive)
            {
                flankTime-=delta;
                if(!visible || currentRole!=CombatRole.Flanker || CoveringTeammate()==null || flankTime<=0 || blockedTime>.5f
                    || suppressionTimer>0 || IsReloading || actor.Health<=actor.maxHealth*.4f
                    || (flankDestination-transform.position).sqrMagnitude<2.25f)
                {
                    flankActive=false;flankCooldown=7f;combatTargetReady=false;combatPause=.45f*ReactionScale;
                    return false;
                }
                // Keep the selected lane fixed while the observed target moves.
                MoveTowards(flankDestination,1.15f,false);
                return true;
            }
            if(!visible || currentRole!=CombatRole.Flanker || flankCooldown>0 || HasAdvancingTeammate()
                || suppressionTimer>0 || IsReloading || isHobbled || actor.Health<=actor.maxHealth*.45f)return false;
            flankCooldown=3f;
            var support=CoveringTeammate();
            if(support==null || !TryFlankDestination(lastSeenPosition,out flankDestination))return false;
            flankActive=true;flankTime=6f;blockedTime=0;combatTargetReady=false;
            support.fireCooldown=Mathf.Min(support.fireCooldown,.7f);
            support.TriggerBark("SUPPRESSING! MOVE!",2.5f);
            TriggerBark("FLANKING! CHANGING ANGLE!",3f);
            return true;
        }

        public void BeginAssault(Vector3 lastReportedPosition)
        {
            if(PassiveTrainingTarget)return;
            ObservationPatrol=false;
            ResetPlayerTactics();
            currentState=AIState.Combat;stateTimer=12f;
            lastSeenPosition=suspiciousLocation=lastReportedPosition;
            lostSightTime=0;provokedUntil=Time.time+18f;combatTargetReady=false;
            combatPause=1.2f*ReactionScale;fireCooldown=Mathf.Max(fireCooldown,1.5f*ReactionScale);
        }
    }
}
