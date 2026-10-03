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
            int rank=0;
            foreach(var other in AllBots)
            {
                if(!SquadEligible(other))continue;
                other.currentRole=count<2?CombatRole.Assault:rank==0?CombatRole.Suppressor:
                    (rank==1 || rank==3)?CombatRole.Flanker:CombatRole.Assault;
                rank++;
            }
        }

        EnemyBot CoveringTeammate()
        {
            foreach(var other in AllBots)
                if(other!=this && SquadEligible(other) && other.currentRole==CombatRole.Suppressor
                    && other.currentState==AIState.Combat && other.playerVisible)return other;
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
            float preferred=(patrolIndex&1)==0?1f:-1f;
            for(int attempt=0;attempt<4;attempt++)
            {
                float sign=(attempt&1)==0?preferred:-preferred;
                Vector3 candidate=transform.position+side*sign*(attempt<2?12f:8f)+toward*3f;
                if(!SafeStep(candidate) || !SectorWorld.DryFootprint(candidate,.6f)
                    || !EnemyCoverGeometry.Reachable(transform.position,candidate,transform,coverPathHits))continue;
                bool occupied=false;
                foreach(var ally in AllBots)
                    if(ally!=this && SquadEligible(ally) && ((ally.transform.position-candidate).sqrMagnitude<9f
                        || (ally.IsFlanking && (ally.flankDestination-candidate).sqrMagnitude<25f)))
                    {occupied=true;break;}
                if(occupied)continue;
                destination=candidate;return true;
            }
            return false;
        }

        bool UpdateFlank(bool visible,float delta)
        {
            if(flankActive)
            {
                flankTime-=delta;
                if(currentRole!=CombatRole.Flanker || CoveringTeammate()==null || flankTime<=0 || blockedTime>.5f
                    || (flankDestination-transform.position).sqrMagnitude<2.25f)
                {
                    flankActive=false;flankCooldown=7f;combatTargetReady=false;combatPause=.45f*ReactionScale;
                    return false;
                }
                // Destination is fixed from observed intel, even if the player hides.
                MoveTowards(flankDestination,1.15f);
                return true;
            }
            if(!visible || currentRole!=CombatRole.Flanker || flankCooldown>0)return false;
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
            currentState=AIState.Combat;stateTimer=12f;
            lastSeenPosition=suspiciousLocation=lastReportedPosition;
            lostSightTime=0;provokedUntil=Time.time+18f;combatTargetReady=false;
            combatPause=1.2f*ReactionScale;fireCooldown=Mathf.Max(fireCooldown,1.5f*ReactionScale);
        }
    }
}
