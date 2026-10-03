using UnityEngine;

namespace GeoSniper
{
    public static class AndroidHitHaptics
    {
        static float nextPulse;
        public static bool Enabled => PlayerPrefs.GetInt("GeoSniper.HitHaptics",0)==1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { nextPulse=0; }
        public static void ConfirmHit()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!Enabled || !Application.isFocused || Time.realtimeSinceStartup<nextPulse) return;
            nextPulse=Time.realtimeSinceStartup+.12f;
            try
            {
                using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    activity.Call("runOnUiThread",new AndroidJavaRunnable(()=>{
                        try
                        {
                            using(var owner=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                            using(var current=owner.GetStatic<AndroidJavaObject>("currentActivity"))
                            using(var window=current.Call<AndroidJavaObject>("getWindow"))
                            using(var view=window.Call<AndroidJavaObject>("getDecorView"))
                                if(view.Call<bool>("hasWindowFocus")) view.Call<bool>("performHapticFeedback",6);
                        }
                        catch(AndroidJavaException) { /* Optional device feedback must never interrupt combat. */ }
                    }));
            }
            catch(AndroidJavaException) { }
#endif
        }
    }
}
