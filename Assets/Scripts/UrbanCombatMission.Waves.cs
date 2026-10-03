using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        ExtractionWavePlan extractionWaves;
        readonly List<Vector3> reinforcementPoints=new List<Vector3>();
        Vector3 assaultAnchor;
        float waveSpawnCooldown,waveWarningElapsed,waveSpawnBlocked;
        readonly RaycastHit[] waveSightHits=new RaycastHit[24];
        int warnedWave;
        string waveDirection="";

        int LivingHostiles()
        {
            int count=0;
            foreach(var enemy in enemies)if(enemy!=null && enemy.Actor!=null && !enemy.Actor.IsDead)count++;
            return count;
        }

        void UpdateExtractionWaves(float delta)
        {
            if(extractionWaves==null || currentMissionState!=MissionState.InProgress)return;
            extractionWaves.Tick(delta);
            contractModeTimer=extractionWaves.Remaining;
            waveSpawnCooldown=Mathf.Max(0,waveSpawnCooldown-delta);
            waveWarningElapsed+=delta;
            int next=extractionWaves.Wave+1;
            if(next<=ExtractionWavePlan.TotalWaves && extractionWaves.Pending==0 && extractionWaves.NextWaveIn<=4f && warnedWave!=next)
            {
                warnedWave=next;waveWarningElapsed=0;
                // Fixed world directions remain legible if the player turns around.
                shotNotice=$"RADIO: WAVE {next}/3 APPROACHING. CHECK YOUR FLANKS!";
                shotNoticeTime=4f;
                EnemyBot.PlayRadioChirpAt(player.transform.position);
            }
            int alive=LivingHostiles();
            if(waveWarningElapsed>=3f && warnedWave==next && extractionWaves.StartNextWave(alive))
            {
                shotNotice=$"WAVE {extractionWaves.Wave}/3 - REINFORCEMENTS INBOUND";
                shotNoticeTime=3f;
            }
            if(waveSpawnCooldown<=0 && extractionWaves.CanSpawn(alive))
            {
                waveSpawnCooldown=.65f; // At most one imported actor per interval on Android.
                if(TryReinforcementPoint(out var point))
                {
                    int before=enemies.Count;
                    SpawnEnemy(before,point);
                    if(enemies.Count>before)
                    {
                        var enemy=enemies[enemies.Count-1];
                        enemy.name=$"Assault Wave {extractionWaves.Wave} Reinforcement";
                        enemy.speed=3.2f;
                        enemy.BeginAssault(assaultAnchor);
                        extractionWaves.RecordSpawn();
                        waveSpawnBlocked=0;
                        stageTarget=enemies.Count;
                        Vector3 direction=point-player.transform.position;
                        waveDirection=Mathf.Abs(direction.x)>Mathf.Abs(direction.z)?(direction.x>0?"EAST":"WEST"):(direction.z>0?"NORTH":"SOUTH");
                        shotNotice=$"WAVE {extractionWaves.Wave}/3 CONTACTS: {waveDirection}";
                        shotNoticeTime=3f;
                    }
                }
                else
                {
                    waveSpawnBlocked+=.65f;
                    shotNotice="RADIO: APPROACH BLOCKED. RELOCATE TO LET THE ASSAULT ADVANCE.";
                    shotNoticeTime=1f;
                    if(waveSpawnBlocked>=20f)
                        FailMission("No safe reinforcement approach is available. Retry the mission or choose another sector.");
                }
            }
            if(extractionWaves.ReadyForExtraction)
                CallHelicopterExtraction("THREE WAVES SURVIVED! DUSTOFF INBOUND!");
        }

        bool TryReinforcementPoint(out Vector3 point)
        {
            point=default;float best=float.NegativeInfinity;
            Vector3 preferred=extractionWaves.Wave==2?Vector3.right:Vector3.left;
            // Prefer a different approach each wave; maps without that street use
            // the next best safe approach, and the contact message reports it.
            foreach(var sample in reinforcementPoints)
            {
                Vector3 offset=sample-player.transform.position;
                float distance=new Vector2(offset.x,offset.z).magnitude;
                if(distance<35f || distance>110f || !SectorWorld.DryFootprint(sample,.6f))continue;
                float score=Vector3.Dot(offset.normalized,preferred)*40f-Mathf.Abs(distance-65f);
                if(score<=best)continue;
                bool occupied=false;
                foreach(var enemy in enemies)
                    if(enemy!=null && enemy.Actor!=null && !enemy.Actor.IsDead && (sample-enemy.transform.position).sqrMagnitude<64f)
                    {occupied=true;break;}
                if(occupied || !SupportedStandingPoint(sample))continue;
                if(cameraView!=null)
                {
                    Vector3 eye=sample+Vector3.up*1.5f;
                    Vector3 screen=cameraView.WorldToViewportPoint(eye);
                    bool inView=screen.z>0 && screen.x>-.15f && screen.x<1.15f && screen.y>-.15f && screen.y<1.15f;
                    if(inView && !ReinforcementOccluded(cameraView.transform.position,eye))continue;
                }
                if(!IsValidOutdoorSpawn(sample,out var valid) || !SectorWorld.DryFootprint(valid,.6f)
                    || Mathf.Abs(valid.y-sample.y)>1f)continue;
                point=valid;best=score;
            }
            return best>float.NegativeInfinity;
        }

        bool ReinforcementOccluded(Vector3 origin,Vector3 target)
        {
            Vector3 delta=target-origin;
            int hits=Physics.RaycastNonAlloc(origin,delta.normalized,waveSightHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            if(hits>=waveSightHits.Length)return false;
            for(int i=0;i<hits;i++)
                if(waveSightHits[i].collider.GetComponentInParent<CombatActor>()==null
                    && !waveSightHits[i].transform.IsChildOf(player.transform))return true;
            return false;
        }

        string ExtractionObjective()
        {
            if(extractionWaves==null)return $"HOLD POSITION ({Mathf.CeilToInt(contractModeTimer)}s)";
            int alive=LivingHostiles();
            string status=extractionWaves.Pending>0?(alive>=extractionWaves.ActiveCap?"CLEAR HOSTILES FOR REINFORCEMENTS":"REINFORCEMENTS INBOUND"):
                extractionWaves.Wave<3 && extractionWaves.NextWaveIn<=0 && alive>=extractionWaves.ActiveCap?"CLEAR HOSTILES FOR NEXT WAVE":
                extractionWaves.Wave<3?$"NEXT WAVE {Mathf.CeilToInt(extractionWaves.NextWaveIn)}s":"FINAL PUSH";
            return $"WAVE {extractionWaves.Wave}/3 | {alive} HOSTILES | {status} | HOLD {Mathf.CeilToInt(contractModeTimer)}s";
        }
    }
}
