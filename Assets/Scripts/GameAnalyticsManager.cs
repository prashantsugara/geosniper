using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UnityConsent;

namespace GeoSniper
{
    // Local playtime is always available. Cloud collection starts only after the
    // player opts in from Privacy & Support.
    public sealed class GameAnalyticsManager : MonoBehaviour
    {
        const string PrefConsent="GeoSniper.Analytics.Consent";
        const string PrefTotalPlaytime="GeoSniper.Analytics.TotalPlaytimeSec";
        const string PrefSessionCount="GeoSniper.Analytics.SessionCount";
        static GameAnalyticsManager instance;
        static bool firebaseReady,unityReady;
        float lastSaved;
        string firebaseStatus="Not initialized",unityStatus="Not initialized";

        public static GameAnalyticsManager Instance=>instance;
        public static bool ConsentGranted=>PlayerPrefs.GetInt(PrefConsent,0)==1;
        public static bool HasConsentChoice=>PlayerPrefs.HasKey(PrefConsent);
        public static bool FirebaseReady=>firebaseReady;
        public static bool UnityReady=>unityReady;
        public static bool IsConfigured=>firebaseReady || unityReady;
        public static string LastStatus=>instance==null?"Inactive":"Google: "+instance.firebaseStatus+" | Unity: "+instance.unityStatus;
        public static int TotalSessions=>PlayerPrefs.GetInt(PrefSessionCount,0);
        public static float TotalPlaytimeSeconds=>PlayerPrefs.GetFloat(PrefTotalPlaytime,0f)+(instance!=null?Mathf.Max(0f,Time.realtimeSinceStartup-instance.lastSaved):0f);
        public static float AverageSessionSeconds=>TotalPlaytimeSeconds/Mathf.Max(1,TotalSessions);
        public static string TotalPlaytimeFormatted=>FormatDuration(TotalPlaytimeSeconds);
        public static string AverageSessionFormatted=>FormatDuration(AverageSessionSeconds);
        public static string ClientId{get{const string k="GeoSniper.Analytics.ClientId";if(!PlayerPrefs.HasKey(k)){PlayerPrefs.SetString(k,Guid.NewGuid().ToString());PlayerPrefs.Save();}return PlayerPrefs.GetString(k);}}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){instance=null;firebaseReady=false;unityReady=false;}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInitialize()
        {
            if(instance!=null) return;
            var go=new GameObject("GameAnalyticsManager");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<GameAnalyticsManager>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void CleanupStaleAnalyticsObjects()
        {
            try
            {
                var objs = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < objs.Length; i++)
                {
                    if (objs[i] != null && objs[i].name == "AnalyticsContainer")
                    {
                        if (Application.isPlaying)
                            Destroy(objs[i]);
                        else
                            DestroyImmediate(objs[i]);
                    }
                }
            }
            catch {}
        }

        void Awake()
        {
            if(instance!=null && instance!=this){Destroy(gameObject);return;}
            instance=this;
            DontDestroyOnLoad(gameObject);
            lastSaved=Time.realtimeSinceStartup;
            PlayerPrefs.SetInt(PrefSessionCount,TotalSessions+1);
            PlayerPrefs.Save();
            if(!ReleaseConfiguration.Current.analyticsEnabled)
            {
                firebaseStatus=unityStatus="Disabled in release configuration";
                return;
            }
            InitializeFirebase();
            InitializeUnity();
        }

        void InitializeFirebase()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            firebaseStatus="Checking dependencies";
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task=>
            {
                if(task.IsFaulted || task.IsCanceled || task.Result!=DependencyStatus.Available)
                {
                    firebaseStatus="Dependency check failed";
                    Debug.LogWarning("Firebase Analytics dependencies unavailable: "+(task.Exception?.GetBaseException().Message??(task.IsCanceled?"Canceled":task.Result.ToString())));
                    return;
                }
                try
                {
                    var app=FirebaseApp.DefaultInstance;
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(ConsentGranted);
                    firebaseReady=true;
                    firebaseStatus=ConsentGranted?"SDK ready; collection enabled":"SDK ready; awaiting consent";
                }
                catch(Exception ex){firebaseStatus="Initialization failed";Debug.LogWarning("Firebase Analytics: "+ex.Message);}
            });
#else
            firebaseStatus="Android build required for delivery test";
#endif
        }

        async void InitializeUnity()
        {
            if(string.IsNullOrWhiteSpace(Application.cloudProjectId))
            {
                unityStatus="Link a Unity Cloud project";
                return;
            }
            try
            {
                unityStatus="Initializing";
                await UnityServices.InitializeAsync();
                unityReady=true;
                ApplyUnityConsent();
                unityStatus=ConsentGranted?"SDK ready; collection enabled":"SDK ready; awaiting consent";
            }
            catch(Exception ex){unityStatus="Initialization failed";Debug.LogWarning("Unity Analytics: "+ex.Message);}
        }

        static void ApplyUnityConsent()
        {
            if(!unityReady) return;
            var state=EndUserConsent.GetConsentState();
            state.AnalyticsIntent=ConsentGranted?UnityEngine.UnityConsent.ConsentStatus.Granted:UnityEngine.UnityConsent.ConsentStatus.Denied;
            EndUserConsent.SetConsentState(state);
        }

        public static void SetAnalyticsConsent(bool allowed)
        {
            bool wasGranted=ConsentGranted;
            PlayerPrefs.SetInt(PrefConsent,allowed?1:2);
            PlayerPrefs.Save();
            if(instance==null) return;
            if(firebaseReady)
            {
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(allowed);
                if(wasGranted && !allowed) FirebaseAnalytics.ResetAnalyticsData();
                instance.firebaseStatus=allowed?"SDK ready; collection enabled":"SDK ready; collection disabled";
            }
            if(unityReady)
            {
                ApplyUnityConsent();
                if(wasGranted && !allowed)
                {
                    try{AnalyticsService.Instance.RequestDataDeletion();}
                    catch(Exception ex){Debug.LogWarning("Unity Analytics deletion request: "+ex.Message);}
                }
                instance.unityStatus=allowed?"SDK ready; collection enabled":"SDK ready; collection disabled";
            }
        }

        void Update(){if(Time.realtimeSinceStartup-lastSaved>=30f) SavePlaytime();}
        void OnApplicationPause(bool paused){if(paused) SavePlaytime();else lastSaved=Time.realtimeSinceStartup;}
        void OnApplicationQuit(){SavePlaytime();}
        void SavePlaytime()
        {
            float now=Time.realtimeSinceStartup;
            PlayerPrefs.SetFloat(PrefTotalPlaytime,PlayerPrefs.GetFloat(PrefTotalPlaytime,0f)+Mathf.Max(0f,now-lastSaved));
            lastSaved=now;
            PlayerPrefs.Save();
        }

        public static void TrackMissionStart(int stageIndex,string contractType,int weaponIndex)
        {
            TrackCustomEvent("level_start",new Dictionary<string,object>{{"level_name","Stage_"+(stageIndex+1)},{"stage_index",stageIndex+1},{"contract_type",contractType??"Standard"},{"weapon_index",weaponIndex}});
        }
        public static void TrackMissionEnd(int stageIndex,string contractType,bool success,float durationSec,int stars,int headshots,float accuracy,string failReason="")
        {
            var p=new Dictionary<string,object>{{"level_name","Stage_"+(stageIndex+1)},{"stage_index",stageIndex+1},{"contract_type",contractType??"Standard"},{"success",success?1:0},{"duration_sec",Mathf.RoundToInt(durationSec)},{"stars",stars},{"headshots",headshots},{"accuracy_pct",Mathf.RoundToInt(accuracy*100f)}};
            if(!success && !string.IsNullOrEmpty(failReason)) p["fail_reason"]=failReason.Length>40?failReason.Substring(0,40):failReason;
            TrackCustomEvent("level_end",p);
        }
        public static void TrackWeaponUpgrade(int weaponIndex,string upgradeType,int newLevel,int cost)
        {
            TrackCustomEvent("spend_virtual_currency",new Dictionary<string,object>{{"weapon_index",weaponIndex},{"item_name","Rifle_"+weaponIndex+"_"+upgradeType},{"upgrade_type",upgradeType},{"new_level",newLevel},{"value",cost},{"virtual_currency_name","Cash"}});
        }
        public static void TrackAdWatched(string placementTag,bool success)
        {
            TrackCustomEvent("ad_reward_view",new Dictionary<string,object>{{"ad_placement",placementTag??"general"},{"success",success?1:0}});
        }
        public static void TrackCustomEvent(string eventName,Dictionary<string,object> parameters=null)
        {
            if(!ConsentGranted || !ReleaseConfiguration.Current.analyticsEnabled || string.IsNullOrWhiteSpace(eventName)) return;
            if(firebaseReady)
            {
                try
                {
                    var values=new List<Parameter>();
                    if(parameters!=null) foreach(var pair in parameters)
                    {
                        if(string.IsNullOrWhiteSpace(pair.Key) || pair.Value==null) continue;
                        if(pair.Value is int i) values.Add(new Parameter(pair.Key,(long)i));
                        else if(pair.Value is long l) values.Add(new Parameter(pair.Key,l));
                        else if(pair.Value is float f) values.Add(new Parameter(pair.Key,(double)f));
                        else if(pair.Value is double d) values.Add(new Parameter(pair.Key,d));
                        else values.Add(new Parameter(pair.Key,pair.Value.ToString()));
                    }
                    FirebaseAnalytics.LogEvent(eventName,values.ToArray());
                }
                catch(Exception ex){Debug.LogWarning("Firebase Analytics event "+eventName+": "+ex.Message);}
            }
            if(unityReady)
            {
                try
                {
                    // Matching custom-event schemas must be created in Unity Event Manager.
                    var ev=new CustomEvent(eventName);
                    if(parameters!=null) foreach(var pair in parameters)
                    {
                        if(string.IsNullOrWhiteSpace(pair.Key) || pair.Value==null) continue;
                        ev.Add(pair.Key,pair.Value);
                    }
                    AnalyticsService.Instance.RecordEvent(ev);
                }
                catch(Exception ex){Debug.LogWarning("Unity Analytics event "+eventName+": "+ex.Message);}
            }
        }

        static string FormatDuration(float seconds)
        {
            int total=Mathf.Max(0,Mathf.RoundToInt(seconds));
            return total>=3600?total/3600+"h "+(total%3600/60).ToString("D2")+"m":total/60+"m "+(total%60).ToString("D2")+"s";
        }
    }
}
