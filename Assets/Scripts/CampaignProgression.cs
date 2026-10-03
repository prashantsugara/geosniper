using UnityEngine;
using System.Collections.Generic;

namespace GeoSniper
{
    // Campaign tier is authored independently of node IDs and screen positions.
    // User difficulty and campaign progression compose; retries always start from a clean snapshot.
    public static class CampaignProgression
    {
        public const int MaxTier = 10;
        public static int Tier(LevelNode node) => node == null ? 5 : Mathf.Clamp(node.difficultyTier,0,MaxTier);
        public static string Label(LevelNode node)
        {
            int tier=Tier(node);
            return tier==0?"TRAINING":tier<=2?"RECON":tier<=4?"STANDARD":tier<=6?"HARDENED":tier<=8?"VETERAN":"APEX BLACK OPS";
        }
        public static bool HasCounterSniper(LevelNode node) => node==null || Tier(node)>=2;
        public static float SurvivalSeconds(LevelNode node,DifficultyProfile profile)
            // Fair survival hold: between 25s and 65s, rewarding tactical rooftop defense without overwhelming hordes.
            => Mathf.Lerp(25f,65f,Tier(node)/(float)MaxTier)/Mathf.Max(.5f,profile.missionTime);
        public static float RunnerSpeed(LevelNode node,DifficultyProfile profile,float routeLength,int pointCount)
        {
            float t=Tier(node)/(float)MaxTier;
            // Account for waypoint rounding and the extraction radius, not just the timer.
            float usableDistance=Mathf.Max(1f,routeLength-2.5f*(pointCount-1)-5f);
            float reactionWindow=Mathf.Lerp(26f,16f,t)*profile.missionTime;
            return Mathf.Min(Mathf.Lerp(2.8f,4.4f,t),usableDistance/(reactionWindow*Mathf.Max(.1f,profile.enemySpeed)));
        }
        public static List<Vector3> RunnerRoute(List<Vector3> points,int maxPoints,System.Func<Vector3,Vector3,bool> clearLeg)
        {
            var best=new List<Vector3>();
            if(points==null || points.Count<2) return best;
            // Try alternate starts so a single isolated street never creates instant failure.
            int start=Random.Range(0,points.Count);
            for(int attempt=0;attempt<Mathf.Min(points.Count,32);attempt++)
            {
                var route=new List<Vector3>{points[(start+attempt)%points.Count]};
                while(route.Count<maxPoints)
                {
                    Vector3 current=route[route.Count-1];
                    int next=-1;float nearest=60f;
                    for(int i=0;i<points.Count;i++)
                    {
                        float d=Vector3.Distance(current,points[i]);
                        if(d<20f || d>=nearest || route.Contains(points[i]) || Mathf.Abs(current.y-points[i].y)>2f) continue;
                        if(Vector3.Distance(route[0],points[i])<20f || !clearLeg(current,points[i])) continue;
                        next=i;nearest=d;
                    }
                    if(next<0) break;
                    route.Add(points[next]);
                }
                if(route.Count>best.Count) best=route;
                if(best.Count>=maxPoints) break;
            }
            return best;
        }
        public static DifficultyProfile CreateProfile(DifficultyProfile baseline,LevelNode node)
        {
            var profile=Object.Instantiate(baseline);
            if(node==null) return profile; // Standalone modes retain their selected difficulty.
            float t=Tier(node)/(float)MaxTier;
            profile.name=baseline.name+" / campaign tier "+Tier(node);
            profile.enemyHealth*=Mathf.Lerp(.75f,1.15f,t);
            profile.enemyDamage*=Mathf.Lerp(.45f,1.40f,t);
            profile.enemySpeed*=Mathf.Lerp(.78f,1.10f,t);
            profile.detectionSpeed*=Mathf.Lerp(.50f,1.25f,t);
            profile.reactionTime*=Mathf.Lerp(1.80f,.85f,t);
            profile.missionTime*=Mathf.Lerp(1.50f,1.00f,t);
            profile.vipHealth*=Mathf.Lerp(1.70f,1.00f,t);
            profile.duelLockSeconds*=Mathf.Lerp(1.80f,.90f,t);
            profile.counterSniperLockSeconds*=Mathf.Lerp(1.75f,.95f,t);
            profile.stealthAlertLimit = Mathf.Max(3, Mathf.RoundToInt(baseline.stealthAlertLimit * Mathf.Lerp(1.25f, 0.80f, t)));
            return profile;
        }
    }
}
