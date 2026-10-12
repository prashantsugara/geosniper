using System;
using System.Reflection;
using GeoSniper;
using UnityEngine;

public static class AdFlowChecks
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run()
    {
        var root=new GameObject("Inactive ad lifecycle test");root.SetActive(false);
        try
        {
            var manager=root.AddComponent<AdManager>();
            manager.customRewardedId="production-reward";manager.customInterstitialId="production-interstitial";
            manager.customNativeId="production-native";
            if(manager.GetRewardedAdUnitId()!=AdManager.TestRewardedIdAndroid || manager.GetInterstitialAdUnitId()!=AdManager.TestInterstitialIdAndroid
                || manager.GetNativeAdUnitId()!=AdManager.TestNativeIdAndroid)
                throw new Exception("Test mode does not override custom IDs");
            manager.testMode=false;
            if(manager.GetRewardedAdUnitId()!="production-reward" || manager.GetNativeAdUnitId()!="production-native") throw new Exception("Production ID selection failed");
            var nativeSize=typeof(AdManager).GetMethod("NativeScreenSize",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(var screen in new[]{new Rect(0,0,1600,720),new Rect(40,20,2320,1040),new Rect(0,0,720,1600)})
            {
                var size=(GoogleMobileAds.Api.AdSize)nativeSize.Invoke(null,new object[]{screen,2f});
                if(size==null || size.Width*2>screen.width-24 || size.Height*2>screen.height*.4f || size.Width>480 || size.Height>120)
                    throw new Exception("Native template does not fit the device safe area");
            }
            if(nativeSize.Invoke(null,new object[]{new Rect(0,0,480,240),2f})!=null)
                throw new Exception("Native overlay would obscure navigation on a small screen");
            foreach(bool success in new[]{false,true})
            {
                int completions=0;bool granted=false;
                typeof(AdManager).GetField("waiting",Flags).SetValue(manager,true);
                typeof(AdManager).GetField("rewardCallback",Flags).SetValue(manager,(Action<bool>)(value=>{completions++;granted=value;}));
                bool rejected=false;
                manager.ShowRewardedAd(value=>rejected=!value);
                if(!rejected || completions!=0) throw new Exception("Concurrent ad request replaced active reward");
                var finish=typeof(AdManager).GetMethod("FinishReward",Flags);
                finish.Invoke(manager,new object[]{success});finish.Invoke(manager,new object[]{success});
                if(completions!=1 || granted!=success || manager.IsBusy) throw new Exception("Reward completion not exactly once");
            }
            int closed=0;
            if(manager.ShowInterstitialIfReady(()=>closed++) || closed!=1)
                throw new Exception("Unavailable interstitial blocked navigation");
            var duelCount=typeof(AdManager).GetField("completedDuelMatches",Flags);
            var duelDue=typeof(AdManager).GetMethod("DuelExitAdDue",Flags);
            var lastFullScreen=typeof(AdManager).GetField("lastFullScreenAd",Flags);
            var lastInterstitial=typeof(AdManager).GetField("lastInterstitial",Flags);
            bool Due(float now)=>(bool)duelDue.Invoke(manager,new object[]{now});
            manager.NotifyDuelMatchCompleted();
            if(Due(200)) throw new Exception("First completed duel can show an interstitial");
            manager.NotifyDuelMatchCompleted();
            if(!Due(200)) throw new Exception("Second completed duel cannot qualify for an exit ad");
            lastFullScreen.SetValue(manager,100f);
            if(Due(279.99f) || !Due(280)) throw new Exception("Duel ads do not respect the 180 second full-screen cooldown boundary");
            lastFullScreen.SetValue(manager,-999f);lastInterstitial.SetValue(manager,100f);
            if(Due(279.99f) || !Due(280)) throw new Exception("A campaign interstitial does not defer a duel ad");
            manager.interstitialCooldownSeconds=240;
            if(Due(339.99f) || !Due(340)) throw new Exception("Duel ad ignores a stricter configured global cooldown");
            manager.interstitialCooldownSeconds=90;lastInterstitial.SetValue(manager,-999f);
            int duelExits=0;
            if(manager.ShowDuelExitInterstitialIfReady(()=>duelExits++) || duelExits!=1
                || (int)duelCount.GetValue(manager)!=2 || manager.IsBusy)
                throw new Exception("No-fill duel exit blocks navigation or consumes completed matches");
            typeof(AdManager).GetField("showing",Flags).SetValue(manager,true);
            if(manager.ShowDuelExitInterstitialIfReady(()=>duelExits++) || duelExits!=2)
                throw new Exception("A busy ad blocks the duel exit fallback");
            typeof(AdManager).GetField("showing",Flags).SetValue(manager,false);
            var queueNative=typeof(AdManager).GetMethod("QueueNativeLoad",Flags);
            var pendingNative=(System.Collections.Concurrent.ConcurrentQueue<Action>)typeof(AdManager).GetField("nativeLoadCallbacks",Flags).GetValue(manager);
            int applied=0,released=0;
            Action apply=()=>applied++,release=()=>released++;
            queueNative.Invoke(manager,new object[]{apply,release});
            if(!pendingNative.TryDequeue(out var result)) throw new Exception("Native load was not queued");
            result();
            if(applied!=1 || released!=0) throw new Exception("Live native load was discarded");
            queueNative.Invoke(manager,new object[]{apply,release});
            var singleton=typeof(AdManager).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
            var previous=singleton.GetValue(null);
            singleton.SetValue(null,manager);
            try {typeof(AdManager).GetMethod("OnDestroy",Flags).Invoke(manager,null);}
            finally {singleton.SetValue(null,previous);}
            queueNative.Invoke(manager,new object[]{apply,release});
            if(applied!=1 || released!=2 || !pendingNative.IsEmpty)
                throw new Exception("Pending or late native loads survived manager disposal");
            Debug.Log("PASS: Ad lifecycle, native layout and duel interstitial cadence/cooldown/no-fill checks");
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }
}
