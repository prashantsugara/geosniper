using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeoSniper;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CampaignProgressionChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool ok,string message) {if(!ok) throw new Exception(message);}
    sealed class SavedProgress : IDisposable
    {
        readonly Dictionary<string,int?> saved=new Dictionary<string,int?>();
        public SavedProgress()
        {
            var keys=new List<string>{"GeoSniper.Credits","GeoSniper.XP","GeoSniper.StageUnlocked","GeoSniper.ActiveNodeId","GeoSniper.DailyClaimed"};
            keys.AddRange(CampaignNodeGraph.Nodes.Select(n=>"GeoSniper.NodeCompleted_"+n.id));
            foreach(string key in keys) {saved[key]=PlayerPrefs.HasKey(key)?(int?)PlayerPrefs.GetInt(key):null;PlayerPrefs.DeleteKey(key);}
        }
        public void Dispose()
        {
            foreach(var item in saved) {if(item.Value.HasValue) PlayerPrefs.SetInt(item.Key,item.Value.Value); else PlayerPrefs.DeleteKey(item.Key);}
            PlayerPrefs.Save();
        }
    }
    public static void Graph()
    {
        using(var saved=new SavedProgress())
        {
            Require(CampaignNodeGraph.Nodes.Length==50,"Campaign must include all 50 authored levels");
            Require(CampaignNodeGraph.Nodes.Select(n=>n.id).Distinct().Count()==50,"Duplicate IDs");
            Require(CampaignNodeGraph.Nodes.Where(n=>!n.isPvPDuel).Select(n=>n.stageIndex).Distinct().Count()==CampaignNodeGraph.Nodes.Count(n=>!n.isPvPDuel),"Duplicate stages");
            Require(CampaignNodeGraph.GetNode(-1)==null && !CampaignNodeGraph.IsNodeUnlocked(99),"Invalid ID resolves to training");
            foreach(var node in CampaignNodeGraph.Nodes)
            {
                Require(CampaignNodeGraph.IsNodeUnlocked(node.id)==(node.id==0),"Fresh save has test-unlocked node "+node.id);
                if(node.id!=0) Require(CampaignNodeGraph.GetNodeState(node.id,node.id)==NodeState.Locked,"Selection bypasses lock");
                foreach(int child in node.nextNodeIds)
                {
                    var next=CampaignNodeGraph.GetNode(child);
                    Require(next!=null && next.id>node.id && next.difficultyTier>=node.difficultyTier,"Non-progressive campaign edge");
                }
            }
            CampaignNodeGraph.CompleteNode(13);
            Require(!CampaignNodeGraph.IsNodeCompleted(13),"Completing locked node bypasses progression");
            CampaignNodeGraph.CompleteNode(0);
            Require(CampaignNodeGraph.IsNodeUnlocked(1) && CampaignNodeGraph.IsNodeUnlocked(2) && !CampaignNodeGraph.IsNodeUnlocked(3),"Opening branch unlock incorrect");
            CampaignNodeGraph.CompleteNode(2);
            Require(CampaignNodeGraph.IsNodeUnlocked(4) && !CampaignNodeGraph.IsNodeUnlocked(3),"Branch unlock does not follow authored graph");
            CampaignNodeGraph.CompleteNode(4);
            Require(CampaignNodeGraph.IsNodeUnlocked(5),"Branch did not unlock convergence");
            PlayerPrefs.SetInt("GeoSniper.NodeCompleted_11",1);
            Require(CampaignNodeGraph.IsNodeUnlocked(11) && CampaignNodeGraph.IsNodeUnlocked(12),"Existing saves lost completed branch");
            Require(PlayerPrefs.GetInt("GeoSniper.Credits",0)==0 && PlayerPrefs.GetInt("GeoSniper.XP",0)==0,"Progress API pays duplicate rewards");
            Require(CampaignNodeGraph.ForStage(1).contractType==CampaignContractType.Overwatch,"VIP node starts wrong mode");
            Require(CampaignNodeGraph.ForStage(9).contractType==CampaignContractType.Stealth,"Stealth node starts wrong mode");
            Require(CampaignNodeGraph.ForStage(49).contractType==CampaignContractType.TargetIdentification,"Finale starts wrong mode");
        }
    }
    public static void Profiles()
    {
        foreach(DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
        {
            var baseline=DifficultyProfile.Create(level);
            string original=JsonUtility.ToJson(baseline);
            try
            {
                foreach(var node in CampaignNodeGraph.Nodes)
                {
                    var p=CampaignProgression.CreateProfile(baseline,node);
                    var retry=CampaignProgression.CreateProfile(baseline,node);
                    try
                    {
                        Require(JsonUtility.ToJson(p)==JsonUtility.ToJson(retry),"Retry compounds campaign multipliers");
                        Require(p.enemyDamage>0 && p.enemyHealth>0 && p.stealthAlertLimit>=1,"Invalid combat profile");
                        foreach(int child in node.nextNodeIds)
                        {
                            var next=CampaignProgression.CreateProfile(baseline,CampaignNodeGraph.GetNode(child));
                            try
                            {
                                Require(next.enemyHealth>=p.enemyHealth && next.enemyDamage>=p.enemyDamage && next.enemySpeed>=p.enemySpeed && next.detectionSpeed>=p.detectionSpeed,"Combat becomes easier along an edge");
                                Require(next.reactionTime<=p.reactionTime && next.missionTime<=p.missionTime && next.vipHealth<=p.vipHealth && next.duelLockSeconds<=p.duelLockSeconds && next.counterSniperLockSeconds<=p.counterSniperLockSeconds && next.stealthAlertLimit<=p.stealthAlertLimit,"Time/awareness curve reverses");
                                Require(CampaignProgression.SurvivalSeconds(CampaignNodeGraph.GetNode(child),next)>=CampaignProgression.SurvivalSeconds(node,p),"Later survival hold is shorter");
                            }
                            finally {Object.DestroyImmediate(next);}
                        }
                        float speed=CampaignProgression.RunnerSpeed(node,p,90,4);
                        Require(speed>0 && speed<=5.8f,"Courier speed out of range");
                        float travel=(90-3*2.5f-5)/(speed*p.enemySpeed);
                        Require(travel>=Mathf.Lerp(22f,10f,CampaignProgression.Tier(node)/(float)CampaignProgression.MaxTier)*p.missionTime-.01f,"Courier escapes before intended reaction window");
                    }
                    finally {Object.DestroyImmediate(p);Object.DestroyImmediate(retry);}
                }
                var standalone=CampaignProgression.CreateProfile(baseline,null);
                try {Require(JsonUtility.ToJson(standalone)==original,"Campaign tuning leaks into standalone mode");}
                finally {Object.DestroyImmediate(standalone);}
                Require(JsonUtility.ToJson(baseline)==original,"Campaign mutates baseline profile");
            }
            finally {Object.DestroyImmediate(baseline);}
        }
        Require(!CampaignProgression.HasCounterSniper(CampaignNodeGraph.GetNode(0)) && !CampaignProgression.HasCounterSniper(CampaignNodeGraph.GetNode(1)),"Counter-sniper introduced too early");
        var casual=DifficultyProfile.Create(DifficultyLevel.Casual);var hardcore=DifficultyProfile.Create(DifficultyLevel.Hardcore);
        try {Require(CampaignProgression.SurvivalSeconds(CampaignNodeGraph.GetNode(6),casual)<CampaignProgression.SurvivalSeconds(CampaignNodeGraph.GetNode(6),hardcore),"Casual survival lasts longer than Hardcore");}
        finally {Object.DestroyImmediate(casual);Object.DestroyImmediate(hardcore);}
    }
    public static void Routes()
    {
        var random=UnityEngine.Random.state;
        try
        {
            var points=new List<Vector3>();for(int i=0;i<6;i++) points.Add(new Vector3(i*25,0,0));
            points.Add(new Vector3(0,0,900)); // disconnected candidate
            for(int seed=0;seed<20;seed++)
            {
                UnityEngine.Random.InitState(seed);
                var route=CampaignProgression.RunnerRoute(points,4,(a,b)=>true);
                Require(route.Count>=2 && route.Count<=4,"Disconnected start produced invalid route");
                Require(route.Distinct().Count()==route.Count && Vector3.Distance(route[0],route.Last())>=20,"Route loops into immediate extraction");
                for(int i=1;i<route.Count;i++) Require(Vector3.Distance(route[i-1],route[i])<60,"Disconnected path leg");
            }
            Require(CampaignProgression.RunnerRoute(points,4,(a,b)=>false).Count<2,"Route crosses blocked corridor");
            Require(CampaignProgression.RunnerRoute(new List<Vector3>{Vector3.zero,new Vector3(30,10,0)},4,(a,b)=>true).Count<2,"Route jumps between floors");
        }
        finally {UnityEngine.Random.state=random;}
    }
    public static void Rewards()
    {
        using(var saved=new SavedProgress())
        {
            var go=new GameObject("Campaign reward check");
            try
            {
                var mission=go.AddComponent<UrbanCombatMission>();
                typeof(UrbanCombatMission).GetField("dailyTarget",Private).SetValue(mission,999);
                typeof(UrbanCombatMission).GetField("campaignNode",Private).SetValue(mission,CampaignNodeGraph.GetNode(0));
                var reward=typeof(UrbanCombatMission).GetMethod("RewardStage",Private);
                reward.Invoke(mission,null);reward.Invoke(mission,null);
                Require(PlayerPrefs.GetInt("GeoSniper.Credits")==200 && PlayerPrefs.GetInt("GeoSniper.XP")==100,"Campaign payout duplicated");
                Require(CampaignNodeGraph.IsNodeCompleted(0),"Victory did not save progress");
                int unlocked=PlayerPrefs.GetInt("GeoSniper.StageUnlocked");
                typeof(UrbanCombatMission).GetField("stageRewarded",Private).SetValue(mission,false);
                typeof(UrbanCombatMission).GetField("campaignNode",Private).SetValue(mission,null);
                PlayerPrefs.SetInt("GeoSniper.ActiveNodeId",13);
                mission.isPvPDuel=true;
                reward.Invoke(mission,null);
                Require(PlayerPrefs.GetInt("GeoSniper.Credits")==700 && PlayerPrefs.GetInt("GeoSniper.XP")==200,"Standalone duel used stale campaign rewards");
                Require(!CampaignNodeGraph.IsNodeCompleted(13) && PlayerPrefs.GetInt("GeoSniper.StageUnlocked")==unlocked,"Standalone duel advances campaign");
            }
            finally {Object.DestroyImmediate(go);}
        }
    }
}
