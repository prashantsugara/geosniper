using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public enum NodeState
    {
        Locked,
        Available,
        Completed,
        Current
    }

    [System.Serializable]
    public class LevelNode
    {
        public int id;
        public string title;
        public string codeName;
        public string modeTag;
        public Color modeColor;
        public bool isPvPDuel;
        public int stageIndex;
        public int difficultyTier;
        public CampaignContractType contractType;
        public Vector2 gridPos;
        public int[] nextNodeIds;
        public string briefing;
        public string objective,storyOutcome,bonusGoal;
        public int enemyCount;
        public int rewardCash;
        public int rewardXP;
    }

    public static class CampaignNodeGraph
    {
        public static readonly LevelNode[] Nodes = BuildAllNodes();

        static LevelNode[] BuildAllNodes()
        {
            var nodes=new LevelNode[50];
            var palette=new[]{new Color(.35f,.95f,.45f),new Color(0,.88f,1),new Color(1,.55f,.15f),new Color(.72f,.42f,1),new Color(.95f,.25f,.55f),new Color(.25f,.8f,.95f),new Color(1,.65f,.2f),new Color(.3f,.95f,.65f)};
            for(int i=0;i<nodes.Length;i++)
            {
                var story=CampaignStory.Get(i);bool duel=story.type=="RivalDuel";var type=CampaignStory.Contract(i);
                nodes[i]=new LevelNode
                {
                    id=i,title=story.title,codeName=CampaignStory.Chapter(i),briefing=story.briefing,
                    objective=story.objective,storyOutcome=story.outcome,bonusGoal=story.bonus,
                    modeTag=duel?"RIVAL MARKSMAN":type==CampaignContractType.EvidenceRecovery?"RECOVER & EXTRACT":type==CampaignContractType.Sabotage?"DISABLE & WITHDRAW":type==CampaignContractType.Rescue?"RESCUE & ESCORT":type.ToString().ToUpperInvariant(),
                    modeColor=duel?new Color(1,.72f,.1f):palette[(int)type],
                    isPvPDuel=duel,stageIndex=duel?-1:i,contractType=type,difficultyTier=i>=48?10:i/5,
                    gridPos=new Vector2(duel?0:i%2==0?-1.2f:1.2f,i*.65f),nextNodeIds=i==49?new int[0]:new[]{i+1},
                    enemyCount=duel?1:Mathf.Clamp(2+(int)(i*.14f),2,8),
                    rewardCash=i==49?5000:Mathf.RoundToInt((200+i*95)*(duel?1.35f:1)),
                    rewardXP=i==49?2500:Mathf.RoundToInt((100+i*48)*(duel?1.3f:1))
                };
            }
            return nodes;
        }
        public static LevelNode GetNode(int id)
        {
            if (id < 0 || id >= Nodes.Length) return null;
            return Nodes[id];
        }

        public static bool IsNodeCompleted(int id)
        {
            return PlayerPrefs.GetInt("GeoSniper.NodeCompleted_" + id, 0) == 1;
        }

        public static bool IsNodeUnlocked(int id)
        {
            if (id < 0 || id >= Nodes.Length) return false;
            if (id == 0) return true;
            if (IsNodeCompleted(id)) return true;

            // Check if any parent node is completed
            for (int i = 0; i < Nodes.Length; i++)
            {
                var n = Nodes[i];
                if (n.nextNodeIds != null)
                {
                    for (int k = 0; k < n.nextNodeIds.Length; k++)
                    {
                        if (n.nextNodeIds[k] == id && IsNodeCompleted(n.id))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static NodeState GetNodeState(int id, int selectedId)
        {
            bool completed = IsNodeCompleted(id);
            bool unlocked = IsNodeUnlocked(id);

            if (!unlocked) return NodeState.Locked;
            if (id == selectedId) return NodeState.Current;
            if (completed) return NodeState.Completed;
            if (unlocked) return NodeState.Available;
            return NodeState.Locked;
        }

        // Progress only. UrbanCombatMission is the single authority for reward payment.
        public static void CompleteNode(int id)
        {
            var node = GetNode(id);
            if (node == null || !IsNodeUnlocked(id)) return;
            PlayerPrefs.SetInt("GeoSniper.NodeCompleted_" + id, 1);
            if (node.stageIndex >= 0)
            {
                PlayerPrefs.SetInt("GeoSniper.StageUnlocked", Mathf.Max(PlayerPrefs.GetInt("GeoSniper.StageUnlocked", 0), node.stageIndex + 1));
            }
            PlayerPrefs.Save();
        }

        public static LevelNode ForStage(int index)
        {
            foreach (var node in Nodes)
            {
                if (!node.isPvPDuel && node.stageIndex == index) return node;
            }
            if (index >= 0 && index < Nodes.Length) return Nodes[index];
            return null;
        }

        public static int GetCompletedCount()
        {
            int count = 0;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (IsNodeCompleted(Nodes[i].id)) count++;
            }
            return count;
        }

        public static int TotalNodeCount => Nodes.Length;
    }
}
