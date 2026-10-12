using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        readonly List<EnemyBot> strongholdGuards=new List<EnemyBot>();
        ReferenceStronghold levelStronghold;
        bool viewingStronghold;
        Vector3 strongholdReturnPosition, strongholdReturnLook;
        public ReferenceStronghold LevelStronghold=>levelStronghold;
        public int StrongholdGuardsRemaining
        {
            get{int count=0;foreach(var bot in strongholdGuards)if(bot!=null && !bot.Actor.IsDead)count++;return count;}
        }
        void ClearStrongholdEncounter()
        {
            ReturnFromStrongholdView();
            if(levelStronghold!=null)levelStronghold.ClearCover();
            foreach(var bot in strongholdGuards)if(bot!=null){bot.gameObject.SetActive(false);Destroy(bot.gameObject);}
            strongholdGuards.Clear();levelStronghold=null;
        }
        public bool ViewStronghold()
        {
            if(levelStronghold==null || player==null || player.InputBlocked || player.Health.IsDead || mapOpen || BallisticsSystem.isBulletCamActive)return false;
            if(!viewingStronghold)
            {
                strongholdReturnPosition=player.transform.position;
                strongholdReturnLook=cameraView.transform.position+cameraView.transform.forward*100;
            }
            player.ExitAim();
            player.Place(levelStronghold.Vantage+Vector3.up*.1f);
            player.Face(levelStronghold.transform.position+Vector3.up*5,true);
            viewingStronghold=true;
            return true;
        }
        public void ReturnFromStrongholdView()
        {
            if(!viewingStronghold)return;
            viewingStronghold=false;
            if(player==null)return;
            player.ExitAim();player.Place(strongholdReturnPosition);player.Face(strongholdReturnLook,true);
        }
        void PrepareStrongholdEncounter()
        {
            if(player==null || SectorWorld.LoadedWorlds.Count==0)return;
            if(isPvPDuel)
            {
                // Remove the preceding campaign site when switching to an original rooftop duel.
                if(stageMode)ReferenceStronghold.Ensure(SectorWorld.LoadedWorlds[0],player.transform.position,stageIndex);
                return;
            }
            levelStronghold=ReferenceStronghold.Ensure(SectorWorld.LoadedWorlds[0],player.transform.position,stageMode?stageIndex:-1);
            if(levelStronghold==null)return;
            levelStronghold.ClearCover();
            ballistics?.SetAuxiliaryTargets(strongholdGuards);
            var candidates=new List<Vector3>(levelStronghold.Perches);
            candidates.RemoveAll(p=>Physics.Linecast(levelStronghold.Vantage+Vector3.up*1.65f,levelStronghold.transform.TransformPoint(p)+Vector3.up*1.4f)
                || Physics.Linecast(levelStronghold.Vantage+Vector3.up*1.65f,levelStronghold.transform.TransformPoint(p)+Vector3.up*1.65f));
            candidates.Sort((a,b)=>a.y.CompareTo(b.y));
            int budget=Application.isMobilePlatform?2:3;
            var gallery=levelStronghold.Layout?.firing;
            if(gallery!=null)
            {
                var bays=candidates.FindAll(p=>System.Array.Exists(gallery,q=>(p-q).sqrMagnitude<.01f));
                if(bays.Count>=budget)
                {
                    var floors=new List<List<Vector3>>();
                    foreach(var bay in bays)
                    {
                        var floor=floors.Find(group=>Mathf.Abs(group[0].y-bay.y)<.5f);
                        if(floor==null){floor=new List<Vector3>();floors.Add(floor);}floor.Add(bay);
                    }
                    // Spread the opening targets vertically, with varied bays per stage.
                    // A flat stride through all positions could put two guards on one floor.
                    var ordered=new List<Vector3>();
                    for(int slot=0;slot<budget;slot++)
                    {
                        int level=budget>1?slot*(floors.Count-1)/(budget-1):0;
                        var floor=floors[level];var bay=floor[(stageIndex+slot)%floor.Count];
                        if(!ordered.Contains(bay))ordered.Add(bay);
                    }
                    foreach(var bay in bays)if(!ordered.Contains(bay))ordered.Add(bay);
                    candidates=ordered;
                }
            }
            for(int i=0;i<candidates.Count && strongholdGuards.Count<budget;i++)
            {
                var local=candidates[i];
                var at=levelStronghold.transform.TransformPoint(local);
                if(Physics.Linecast(levelStronghold.Vantage+Vector3.up*1.65f,at+Vector3.up*1.6f))continue;
                int before=enemies.Count;SpawnEnemy(90+i,at,false);
                if(enemies.Count==before)continue;
                var bot=enemies[enemies.Count-1];enemies.RemoveAt(enemies.Count-1);
                // Optional guards use normal ballistics/feedback without replacing
                // story targets, VIPs, evidence stations or contract completion counts.
                bot.transform.SetParent(transform,true);bot.ConfigureAsCounterSniper();bot.Suspended=true;
                // The imported static building was added after sector navigation.
                // Counter-snipers stand on its source collision, not a sampled street NavMesh.
                var navigation=bot.GetComponent<UnityEngine.AI.NavMeshAgent>();if(navigation!=null)navigation.enabled=false;
                var body=bot.GetComponent<CharacterController>();if(body!=null)body.enabled=false;
                bot.transform.position=at;if(body!=null)body.enabled=true;
                bot.transform.LookAt(new Vector3(levelStronghold.Vantage.x,at.y,levelStronghold.Vantage.z));
                strongholdGuards.Add(bot);
                var toward=levelStronghold.Vantage-at;toward.y=0;
                levelStronghold.AddCover(at+toward.normalized*.85f);
            }
        }
        void UpdateStrongholdEncounter()
        {
            bool active=(currentMissionState==MissionState.InProgress || currentMissionState==MissionState.Extraction || freeRoam && !rangeMode && currentMissionState==MissionState.None) && !victoryPending && !mapOpen && !BallisticsSystem.isBulletCamActive && player!=null && !player.InputBlocked;
            foreach(var bot in strongholdGuards)if(bot!=null)
            {
                bot.Suspended=!active;
                if(bot.Actor.IsDead && countedDeaths.Add(bot))StartCoroutine(RemoveFallenEnemy(bot));
            }
        }
        void DrawStrongholdEncounter(float width,float height)
        {
            if(levelStronghold==null || player==null || mapOpen || !(currentMissionState==MissionState.InProgress || freeRoam && !rangeMode && currentMissionState==MissionState.None))return;
            int remaining=StrongholdGuardsRemaining;
            var style=new GUIStyle(GUI.skin.label){fontSize=12,alignment=TextAnchor.MiddleRight};style.normal.textColor=new Color(.92f,.80f,.53f);
            if(!player.Scoped)
            {
                var button=new GUIStyle(GUI.skin.button){fontSize=14,alignment=TextAnchor.MiddleCenter};
                if(GUI.Button(new Rect(width-290,196,275,42),viewingStronghold?"RETURN TO MISSION":"VIEW "+(levelStronghold.Profile.Kind==CampaignLocationKind.Stronghold?"STRONGHOLD":"SITE")+"  >",button))
                {
                    if(viewingStronghold)ReturnFromStrongholdView();else ViewStronghold();
                }
                GUI.Label(new Rect(width-360,240,345,24),levelStronghold.Profile.Family+" / "+remaining+" GUARDS",style);
            }
            foreach(var bot in strongholdGuards)
            {
                if(bot==null || bot.Actor.IsDead || !bot.CanShowAwareness(cameraView))continue;
                var point=cameraView.WorldToViewportPoint(bot.transform.position+Vector3.up*2.2f);
                var marker=new GUIStyle(style){alignment=TextAnchor.MiddleCenter,fontSize=16};marker.normal.textColor=new Color(.94f,.24f,.17f);
                GUI.Label(new Rect(point.x*width-12,(1-point.y)*height-12,24,24),"▼",marker);
            }
        }
    }
}
