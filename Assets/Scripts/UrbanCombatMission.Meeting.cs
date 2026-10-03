using UnityEngine;

namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        MeetingEncounter meeting;
        bool MeetingMode => meeting!=null && stageMode && stageIndex==0 && activeContract.Type==CampaignContractType.TargetIdentification;
        void SetupMeeting()
        {
            meeting=new MeetingEncounter();
            foreach(var bot in enemies)
            {
                bot.ReportBodies=false;
                bot.ObservationPatrol=true;
                if(bot!=contractTarget) {bot.speed=1.2f;bot.patrolRadius=3;bot.InitializePatrol();}
            }
        }
        void UpdateMeeting()
        {
            if(!MeetingMode || currentMissionState!=MissionState.InProgress || contractTarget==null || cameraView==null) return;
            bool observing=false;
            if(player.Scoped)
            {
                var point=contractTarget.transform.position+Vector3.up*1.3f;
                var viewport=cameraView.WorldToViewportPoint(point);
                if(viewport.z>0 && Mathf.Abs(viewport.x-.5f)<.12f && Mathf.Abs(viewport.y-.5f)<.18f)
                {
                    observing=!Physics.Linecast(cameraView.transform.position,point,out var hit,~(1<<2),QueryTriggerInteraction.Ignore)
                        || hit.collider.GetComponentInParent<EnemyBot>()==contractTarget;
                }
            }
            meeting.Tick(Time.deltaTime,observing,mapOpen || BallisticsSystem.isBulletCamActive);
            if(!meeting.Identified && enemies.Count>1 && enemies[1]!=null && !mapOpen)
            {
                Vector3 direction=enemies[1].transform.position-contractTarget.transform.position;direction.y=0;
                if(direction.sqrMagnitude>.01f) contractTarget.transform.rotation=Quaternion.Slerp(contractTarget.transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*2);
            }
        }
        string MeetingObjective => meeting.Elapsed<8?"OBSERVE THE MEETING • HOLD FIRE":!meeting.Identified?
            "SCOPE THE STATIONARY CONTACT • IDENTIFY "+Mathf.FloorToInt(meeting.Observation/3*100)+"%":
            "CONTACT CONFIRMED • TAKE A CLEAR SHOT • GUARDS OPTIONAL";
    }
}
