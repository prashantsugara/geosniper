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
            if(manager.GetRewardedAdUnitId()!=AdManager.TestRewardedIdAndroid || manager.GetInterstitialAdUnitId()!=AdManager.TestInterstitialIdAndroid)
                throw new Exception("Test mode does not override custom IDs");
            manager.testMode=false;
            if(manager.GetRewardedAdUnitId()!="production-reward") throw new Exception("Production ID selection failed");
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
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }
}
