using UnityEngine;

namespace GeoSniper
{
    public enum CampaignContractType { TargetIdentification, Overwatch, TimedInterception, Stealth, Escape, EvidenceRecovery, Sabotage, Rescue }

    public readonly struct CampaignContract
    {
        public readonly int Level; public readonly CampaignContractType Type; public readonly string Title; public readonly string Objective;
        public CampaignContract(int level,CampaignContractType type,string title,string objective)
        { Level=level;Type=type;Title=title;Objective=objective; }

        public static CampaignContract ForLevel(int level)
        {
            int n = Mathf.Clamp(level, 0, 49);
            var story=CampaignStory.Get(n);
            return new CampaignContract(n, CampaignStory.Contract(n), story.title, story.objective);
        }
    }
}
