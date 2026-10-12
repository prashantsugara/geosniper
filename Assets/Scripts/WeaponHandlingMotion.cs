using UnityEngine;
namespace GeoSniper
{
    public static class WeaponHandlingMotion
    {
        public static float Ease(float start,float end,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,t));
        public static float ReloadEnvelope(float t)=>Ease(0,.18f,t)*(1-Ease(.78f,1,t));
        public static float ReloadHand(float t)=>Ease(.12f,.28f,t)*(1-Ease(.65f,.88f,t));
        public static bool BoltAction(int index)=>index==1 || index==3;
        public static float BoltDuration(int index)=>Mathf.Clamp(WeaponCatalog.Get(index).Cadence,.65f,1.2f);
        public static float BoltHand(float t)=>Ease(.12f,.25f,t)*(1-Ease(.72f,.94f,t));
        public static float BoltPull(float t)=>Ease(.25f,.42f,t)*(1-Ease(.52f,.72f,t));
        // Exact critically damped integration keeps recoil recovery consistent at 30/60/120 fps.
        public static void Recover(ref float position,ref float velocity,float dt)
        {
            const float frequency=24;
            dt=Mathf.Max(0,dt);
            float decay=Mathf.Exp(-frequency*dt),term=velocity+frequency*position;
            position=(position+term*dt)*decay;
            velocity=(velocity-frequency*term*dt)*decay;
        }
    }
}
