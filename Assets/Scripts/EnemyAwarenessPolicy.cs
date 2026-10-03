using System;
namespace GeoSniper
{
    public static class EnemyAwarenessPolicy
    {
        public const float LostSightGrace=2.5f;
        public const float SearchDuration=4.5f;
        public static float Detection(float current,bool visible,float distanceFraction,bool crouched,float difficulty,float delta)
        {
            if(!float.IsFinite(current)) current=0;
            if(!float.IsFinite(delta) || delta<=0) return Math.Clamp(current,0,1);
            float range=float.IsFinite(distanceFraction)?Math.Clamp(distanceFraction,0,1):1;
            float scale=float.IsFinite(difficulty)?Math.Clamp(difficulty,.1f,4f):1;
            float rate=visible?(1.1f-.92f*range)*(crouched?.45f:1)*scale:-.3f;
            return Math.Clamp(current+rate*Math.Min(delta,.1f),0,1);
        }
    }
}
