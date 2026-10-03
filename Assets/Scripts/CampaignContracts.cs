using UnityEngine;

namespace GeoSniper
{
    public enum CampaignContractType { TargetIdentification, Overwatch, TimedInterception, Stealth, Escape }

    public readonly struct CampaignContract
    {
        public readonly int Level; public readonly CampaignContractType Type; public readonly string Title; public readonly string Objective;
        public CampaignContract(int level,CampaignContractType type,string title,string objective)
        { Level=level;Type=type;Title=title;Objective=objective; }
        static readonly string[] Names = { "Target Elimination", "Overwatch", "Timed Interception", "Stealth Contract", "Escape" };
        static readonly string[] Goals = { "Eliminate the marked scout operative and hostile guards from your rooftop nest.", "Protect the moving friendly and eliminate threats along the route.", "Stop the marked hostile before they leave the district.", "Complete the contract without raising an alarm.", "Reach extraction while search teams close in." };

        public static CampaignContract ForLevel(int level)
        {
            int n = Mathf.Clamp(level, 0, 49);
            var type = (CampaignContractType)(n % 5);
            return new CampaignContract(n, type, Names[(int)type], Goals[(int)type]);
        }
    }
}
