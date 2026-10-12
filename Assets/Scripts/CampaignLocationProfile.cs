using UnityEngine;
namespace GeoSniper
{
    public enum CampaignLocationKind { Apartments, FieldCamp, Stronghold, ShoppingGallery, FreightDepot, DuelRoof }
    public readonly struct CampaignLocationProfile
    {
        public readonly int Stage;
        public readonly CampaignLocationKind Kind;
        public readonly string Name;
        public CampaignLocationProfile(int stage,CampaignLocationKind kind,string name){Stage=stage;Kind=kind;Name=name;}
        public string Family=>Kind==CampaignLocationKind.Apartments?"APARTMENT BLOCK":Kind==CampaignLocationKind.FieldCamp?"FIELD CAMP":Kind==CampaignLocationKind.Stronghold?"FORTIFIED COMPOUND":Kind==CampaignLocationKind.ShoppingGallery?"SHOPPING GALLERY":Kind==CampaignLocationKind.FreightDepot?"FREIGHT DEPOT":"RIVAL ROOFTOPS";
        public static CampaignLocationProfile ForStage(int stage)
        {
            if(stage<0)return new CampaignLocationProfile(-1,CampaignLocationKind.Stronghold,"Training Stronghold");
            stage=Mathf.Clamp(stage,0,49);return new CampaignLocationProfile(stage,Kinds[stage],Names[stage]);
        }
        static readonly CampaignLocationKind A=CampaignLocationKind.Apartments,C=CampaignLocationKind.FieldCamp,S=CampaignLocationKind.Stronghold,M=CampaignLocationKind.ShoppingGallery,D=CampaignLocationKind.FreightDepot,R=CampaignLocationKind.DuelRoof;
        static readonly CampaignLocationKind[] Kinds={A,D,M,C,A,C,M,A,S,R,D,C,M,C,D,A,C,M,S,R,A,D,M,S,D,C,A,M,D,R,C,D,M,S,C,A,M,D,C,R,A,D,M,S,C,A,C,M,D,R};
        static readonly string[] Names={
            "Canal Apartments","Survey Loading Yard","Arcade Exchange","Survey Tent Camp","Archive Residences",
            "Relay Field Camp","East Market Gallery","Witness Apartments","North Gate Compound","Ghost Rooftops",
            "Manifest Depot","Medical Tent Station","Paymaster Plaza","Communications Camp","Courier Freight Yard",
            "Vacant Residences","Evacuation Camp","Civic Shopping Gallery","Burn Notice Compound","Warden Rooftops",
            "Press Apartments","Switchboard Depot","Newsroom Arcade","Hunter Compound","Receipt Loading Yard",
            "Listening Camp","Recording Apartments","Civic Mall","Broadcast Freight Yard","Viper Rooftops",
            "Siege Field Camp","Lockout Depot","Dispatch Shopping Gallery","Command Compound","Survey Rescue Camp",
            "Route Apartments","Announcement Mall","Service Freight Yard","Corridor Camp","Titan Rooftops",
            "Channel Apartments","Relay Freight Depot","Contract Exchange","Final Guard Compound","Homecoming Camp",
            "Records Apartments","Quiet Approach Camp","Public Witness Mall","Last Light Depot","Meridian Rooftops"
        };
    }
}
