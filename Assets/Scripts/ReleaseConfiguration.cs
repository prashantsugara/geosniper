using System;
using UnityEngine;

namespace GeoSniper
{
    [Serializable]
    public sealed class ReleaseConfiguration
    {
        public string publisherName="";
        public string supportEmail="";
        public string privacyPolicyUrl="";
        public bool adsEnabled=true;
        public bool useTestAds=true;
        public string rewardedAdUnitId="";
        public string interstitialAdUnitId="";
        public string rewardedInterstitialAdUnitId="";
        public string nativeAdUnitId="ca-app-pub-2796051533157572/2500637800";
        public bool liveMapsEnabled=true;
        public bool assetRightsVerified=false;
        public bool privacyAndDataSafetyReviewed=false;
        public bool deviceTestingCompleted=false;
        public bool audienceAndContentRatingReviewed=false;
        public bool mapProviderUsageReviewed=false;
        public bool analyticsEnabled=true;
        public string firebaseAppId="1:676740822554:android:88356df370653949b9c3a3";
        public string firebaseProjectId="geosniper-e58dc";
        static ReleaseConfiguration current;
        public static ReleaseConfiguration Current
        {
            get
            {
                if(current!=null) return current;
                var asset=Resources.Load<TextAsset>("ReleaseConfiguration");
                try {current=asset!=null?JsonUtility.FromJson<ReleaseConfiguration>(asset.text):new ReleaseConfiguration();}
                catch(Exception e) {Debug.LogWarning("Release configuration invalid: "+e.GetType().Name);current=new ReleaseConfiguration();}
                return current;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>current=null;
        public static bool ValidHttps(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri) && uri.Scheme==Uri.UriSchemeHttps && !uri.IsLoopback;
    }
}
