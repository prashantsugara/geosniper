using System;
using UnityEngine;
namespace GeoSniper
{
    [Serializable] public sealed class StoryOperation
    {
        public string title,type,briefing,objective,outcome,bonus;
        public int interactions;
        public bool windowStart;
    }
    public static class CampaignStory
    {
        public const string Title="OPERATION GLASS MERIDIAN";
        static readonly string[] Chapters={"THE WRONG ADDRESS","PAPER TRAIL","THE LEAK","CITY UNDER SIEGE","LAST LIGHT"};
        [Serializable] sealed class StoryFile {public StoryOperation[] operations;}
        static StoryOperation[] operations;
        public static StoryOperation Get(int id)
        {
            if(operations==null)
            {
                var asset=Resources.Load<TextAsset>("Campaign/Story");
                operations=asset!=null?JsonUtility.FromJson<StoryFile>(asset.text).operations:null;
                if(operations==null || operations.Length!=50)throw new InvalidOperationException("Glass Meridian requires 50 authored operations.");
            }
            return id>=0 && id<operations.Length?operations[id]:null;
        }
        public static string Chapter(int id)=>"CHAPTER "+(Mathf.Clamp(id,0,49)/10+1)+" // "+Chapters[Mathf.Clamp(id,0,49)/10];
        public static CampaignContractType Contract(int id)=>Get(id).type=="RivalDuel"?CampaignContractType.TargetIdentification:(CampaignContractType)Enum.Parse(typeof(CampaignContractType),Get(id).type);
        public static string BonusDescription(string bonus)=>bonus=="Precision"?"Land a headshot with at least 70% accuracy."
            :bonus=="Ghost"?"Finish without raising the alert meter."
            :bonus=="Swift"?"Complete the operation within three minutes."
            :"Finish with at least 75% health.";
    }
}
