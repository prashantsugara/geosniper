using System;
using System.Collections.Concurrent;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace GeoSniper
{
    // Official SDK ads, including SDK preview ads in the Editor. No timed reward simulation.
    public class AdManager : MonoBehaviour
    {
        static AdManager instance;
        public static AdManager Instance {
            get {
                if(instance==null) instance=FindAnyObjectByType<AdManager>();
                if(instance==null) instance=new GameObject("GeoSniper_AdManager").AddComponent<AdManager>();
                return instance;
            }
        }
        public const string TestAppIdAndroid = "ca-app-pub-3940256099942544~3347511713";
        public const string TestRewardedIdAndroid = "ca-app-pub-3940256099942544/5224354917";
        public const string TestInterstitialIdAndroid = "ca-app-pub-3940256099942544/1033173712";
        public const string TestRewardedInterstitialIdAndroid = "ca-app-pub-3940256099942544/5354046379";
        public const string TestBannerIdAndroid = "ca-app-pub-3940256099942544/6300978111";
        public const string TestNativeIdAndroid = "ca-app-pub-3940256099942544/2247696110";

        public const string TestAppIdIOS = "ca-app-pub-3940256099942544~1458002511";
        public const string TestRewardedIdIOS = "ca-app-pub-3940256099942544/1712485313";
        public const string TestInterstitialIdIOS = "ca-app-pub-3940256099942544/4411468910";
        public const string TestRewardedInterstitialIdIOS = "ca-app-pub-3940256099942544/6978759866";
        public const string TestBannerIdIOS = "ca-app-pub-3940256099942544/2934735716";
        public const string TestNativeIdIOS = "ca-app-pub-3940256099942544/3986624511";


        public string customRewardedId="",customInterstitialId="",customRewardedInterstitialId="",customBannerId="",customNativeId="ca-app-pub-2796051533157572/2500637800";
        public bool testMode=true;
        public int stagesPerInterstitial=1;
        public float interstitialCooldownSeconds=90;
        public string Status {get;private set;}="Initializing ads...";
        public bool IsBusy=>waiting || showing;
        readonly ConcurrentQueue<Action> callbacks=new ConcurrentQueue<Action>();
        RewardedAd rewarded;
        InterstitialAd interstitial;
        RewardedInterstitialAd rewardedInterstitial;
        bool initialized,initializing,loadingReward,loadingInterstitial,loadingRewardedInterstitial,loadingBanner,waiting,showing,earned,disposed,bannerShowing;
        BannerView banner;
        NativeOverlayAd nativeBillboardAd;
        bool loadingNativeAd, nativeAdShowing;
        Vector2Int currentNativePos = new Vector2Int(-9999, -9999);
        float rewardRetry,interstitialRetry,rewardedInterstitialRetry,deadline,noticeUntil,lastInterstitial=-999;
        float rewardRetryDelay = 60f, interstitialRetryDelay = 60f, rewardedInterstitialRetryDelay = 60f;
        float nativeAdRetry, nativeAdRetryDelay = 25f;
        int completedStages;
        Action<bool> rewardCallback;
        Action closeCallback;
        Action<bool> rewardedInterstitialCallback;
        bool HasRewardedInterstitialUnit => testMode || !string.IsNullOrWhiteSpace(customRewardedInterstitialId);
        bool gatheringConsent,privacyOpen;
        int consentEpoch;
        public bool PrivacyOptionsRequired=>ConsentInformation.PrivacyOptionsRequirementStatus==PrivacyOptionsRequirementStatus.Required;
        bool AdsAllowed=>ReleaseConfiguration.Current.adsEnabled && !privacyOpen && ConsentInformation.CanRequestAds();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Preload() { _=Instance; }
        void Awake() {
            if(instance!=null && instance!=this) {Destroy(gameObject);return;}
            instance=this;DontDestroyOnLoad(gameObject);
            var config=ReleaseConfiguration.Current;
            testMode=config.useTestAds || Debug.isDebugBuild || Application.isEditor;
            customRewardedId=config.rewardedAdUnitId;customInterstitialId=config.interstitialAdUnitId;
            customRewardedInterstitialId=config.rewardedInterstitialAdUnitId;customNativeId=config.nativeAdUnitId;
            if(config.adsEnabled) GatherConsent();
        }
        void GatherConsent()
        {
            if(gatheringConsent || !ReleaseConfiguration.Current.adsEnabled) return;
            gatheringConsent=true;
            Status="Checking ad privacy choices...";
            try
            {
                ConsentInformation.Update(new ConsentRequestParameters(),error=>callbacks.Enqueue(()=>{
                    if(disposed) return;
                    if(error!=null) {ConsentFinished(error);return;}
                    try {ConsentForm.LoadAndShowConsentFormIfRequired(formError=>callbacks.Enqueue(()=>ConsentFinished(formError)));}
                    catch(Exception e) {gatheringConsent=false;Debug.LogException(e);Notice("Ad privacy check unavailable. Please retry later.");if(waiting) FinishReward(false);}
                }));
            }
            catch(Exception e) {gatheringConsent=false;Debug.LogException(e);Notice("Ad privacy check unavailable. Please retry later.");if(waiting) FinishReward(false);}
        }
        void ConsentFinished(FormError error)
        {
            if(disposed) return;
            gatheringConsent=false;
            if(error!=null) Debug.LogWarning("[AdManager] Consent: "+error.Message);
            if(AdsAllowed) {InitializeAds();LoadRewarded();LoadInterstitial();if(HasRewardedInterstitialUnit) LoadRewardedInterstitial();LoadNativeBillboardAd();}
            else {Status="Ads unavailable with the current privacy choices.";if(waiting){Notice(Status);FinishReward(false);}}
        }
        // ── BANNER AD (DISABLED IN LOBBY & FOOTER) ────────────────────────
        public void LoadAndShowBanner()
        {
            // Disabled: Banners permanently removed from lobby and footers per design
            return;
        }
        public void HideBanner() { banner?.Hide(); bannerShowing = false; }
        public void DestroyBanner() { banner?.Destroy(); banner = null; bannerShowing = false; loadingBanner = false; }
        public void RemoveAllLobbyAds()
        {
            DestroyBanner();
            HideNativeBillboardAd();
        }

        public void NotifyNativeBillboardImpression()
        {
            Debug.Log("[AdManager] In-game 3D billboard impression recorded.");
        }

        public void ShowPrivacyOptions()
        {
            if(IsBusy || gatheringConsent || privacyOpen) return;
            if(!PrivacyOptionsRequired) {Notice("No additional ad privacy choices are required for this region.");return;}
            privacyOpen=true;consentEpoch++;
            rewarded?.Destroy();rewarded=null;interstitial?.Destroy();interstitial=null;rewardedInterstitial?.Destroy();rewardedInterstitial=null;
            try {ConsentForm.ShowPrivacyOptionsForm(error=>callbacks.Enqueue(()=>{
                if(disposed) return;
                privacyOpen=false;
                if(error!=null) Notice("Privacy options could not open. Please retry.");
                if(AdsAllowed) {InitializeAds();LoadRewarded();LoadInterstitial();if(HasRewardedInterstitialUnit) LoadRewardedInterstitial();}
            }));}
            catch(Exception e) {privacyOpen=false;Debug.LogException(e);Notice("Privacy options could not open. Please retry.");}
        }
        void InitializeAds() {
            if(!AdsAllowed || initialized || initializing) return;
            initializing=true;
            try {
                MobileAds.Initialize(status=>callbacks.Enqueue(()=>{
                    if(disposed) return;
                    initializing=false;
                    if(status==null) {Notice("Ads could not initialize. Please retry.");return;}
                    initialized=true;
                    Debug.Log("[AdManager] SDK initialized; rewarded unit: "+GetRewardedAdUnitId());
                    LoadRewarded();LoadInterstitial();if(HasRewardedInterstitialUnit) LoadRewardedInterstitial();
                    LoadNativeBillboardAd();
                }));
            } catch(Exception e) {initializing=false;Debug.LogException(e);Notice("Ads unavailable. Please retry.");}
        }
        public bool IsNativeBillboardAdLoaded => nativeBillboardAd != null;
        public bool IsLoadingNativeAd => loadingNativeAd;
        public int NativeAdTemplateWidth  { get { if(nativeBillboardAd == null) return 320; float w = nativeBillboardAd.GetTemplateWidthInPixels();  return w >= 32 ? (int)w : 320; } }
        public int NativeAdTemplateHeight { get { if(nativeBillboardAd == null) return 90;  float h = nativeBillboardAd.GetTemplateHeightInPixels(); return h >= 16 ? (int)h : 90;  } }
        public string GetRewardedAdUnitId()=>!testMode && !string.IsNullOrWhiteSpace(customRewardedId)?customRewardedId:
#if UNITY_IOS
            TestRewardedIdIOS;
#else
            TestRewardedIdAndroid;
#endif
        public string GetInterstitialAdUnitId()=>!testMode && !string.IsNullOrWhiteSpace(customInterstitialId)?customInterstitialId:
#if UNITY_IOS
            TestInterstitialIdIOS;
#else
            TestInterstitialIdAndroid;
#endif
        public string GetRewardedInterstitialAdUnitId()=>!testMode && !string.IsNullOrWhiteSpace(customRewardedInterstitialId)?customRewardedInterstitialId:
#if UNITY_IOS
            TestRewardedInterstitialIdIOS;
#else
            TestRewardedInterstitialIdAndroid;
#endif
        public string GetBannerAdUnitId()=>!testMode && !string.IsNullOrWhiteSpace(customBannerId)?customBannerId:
#if UNITY_IOS
            TestBannerIdIOS;
#else
            TestBannerIdAndroid;
#endif
        public string GetNativeAdUnitId()=>!string.IsNullOrWhiteSpace(customNativeId)?customNativeId:
#if UNITY_IOS
            TestNativeIdIOS;
#else
            TestNativeIdAndroid;
#endif

        public void LoadNativeBillboardAd(Action<bool> onComplete = null)
        {
            if(!AdsAllowed) { onComplete?.Invoke(false); return; }
            if(nativeBillboardAd != null) { onComplete?.Invoke(true); return; }
            if(loadingNativeAd) { onComplete?.Invoke(false); return; }

            loadingNativeAd = true;
            try
            {
                var options = new NativeAdOptions
                {
                    AdChoicesPlacement = AdChoicesPlacement.TopRightCorner,
                    MediaAspectRatio = MediaAspectRatio.Landscape,
                };
                var request = new AdRequest();
                NativeOverlayAd.Load(GetNativeAdUnitId(), request, options, (ad, error) =>
                {
                    callbacks.Enqueue(() =>
                    {
                        loadingNativeAd = false;
                        if(disposed || !AdsAllowed || error != null || ad == null)
                        {
                            ad?.Destroy();
                            nativeAdRetry = Time.unscaledTime + nativeAdRetryDelay;
                            nativeAdRetryDelay = Mathf.Min(nativeAdRetryDelay * 1.5f, 120f);
                            Debug.LogWarning("[AdManager] Native billboard ad load failed: " + error);
                            onComplete?.Invoke(false);
                            return;
                        }

                        nativeAdRetryDelay = 25f;
                        nativeBillboardAd = ad;
                        ConfigureNativeAdEvents(ad);

                        var style = new NativeTemplateStyle
                        {
                            TemplateId = NativeTemplateId.Small
                        };

                        try
                        {
                            nativeBillboardAd.RenderTemplate(style, AdPosition.Bottom);
                            nativeBillboardAd.Hide();
                        }
                        catch(Exception ex)
                        {
                            Debug.LogWarning("[AdManager] Native RenderTemplate: " + ex.Message);
                        }
                        nativeAdShowing = false;
                        onComplete?.Invoke(true);
                    });
                });
            }
            catch(Exception ex)
            {
                loadingNativeAd = false;
                nativeAdRetry = Time.unscaledTime + nativeAdRetryDelay;
                nativeAdRetryDelay = Mathf.Min(nativeAdRetryDelay * 1.5f, 120f);
                Debug.LogWarning("[AdManager] LoadNativeBillboardAd exception: " + ex.Message);
                onComplete?.Invoke(false);
            }
        }

        void ConfigureNativeAdEvents(NativeOverlayAd ad)
        {
            ad.OnAdImpressionRecorded += () => callbacks.Enqueue(() =>
            {
                Debug.Log("[AdManager] AdMob native billboard ad impression recorded.");
                int cash = PlayerPrefs.GetInt("GeoSniper.Credits", 0);
                PlayerPrefs.SetInt("GeoSniper.Credits", cash + 250);
                PlayerPrefs.Save();
                var mission = FindAnyObjectByType<UrbanCombatMission>();
                if(mission != null) mission.ShowNotification("🎯 SPONSOR INTEL LOGGED (+ $250)", 2.5f);
            });

            ad.OnAdClicked += () => callbacks.Enqueue(() =>
            {
                Debug.Log("[AdManager] AdMob native billboard ad clicked.");
                int cash = PlayerPrefs.GetInt("GeoSniper.Credits", 0);
                PlayerPrefs.SetInt("GeoSniper.Credits", cash + 500);
                PlayerPrefs.Save();
                var mission = FindAnyObjectByType<UrbanCombatMission>();
                if(mission != null) mission.ShowNotification("🎯 SPONSOR INTEL ACQUIRED (+ $500)", 3.0f);
            });

            ad.OnAdPaid += (AdValue val) => callbacks.Enqueue(() =>
            {
                Debug.Log($"[AdManager] Native billboard ad paid: {val.Value} {val.CurrencyCode}");
            });
        }

        public void ShowNativeScreenAd(AdPosition position = AdPosition.Bottom)
        {
            if(!AdsAllowed) return;
            if(nativeBillboardAd == null)
            {
                if(!loadingNativeAd) LoadNativeBillboardAd();
                return;
            }

            try
            {
                nativeBillboardAd.SetTemplatePosition(position);
                if(!nativeAdShowing)
                {
                    nativeBillboardAd.Show();
                    nativeAdShowing = true;
                }
            }
            catch(Exception ex)
            {
                Debug.LogWarning("[AdManager] ShowNativeScreenAd error: " + ex.Message);
            }
        }

        public void ShowNativeBillboardAdAt(int screenX, int screenY)
        {
            // Legacy billboard positioning redirect to standard clean screen ad
            ShowNativeScreenAd(AdPosition.Bottom);
        }

        public void HideNativeScreenAd()
        {
            if(nativeBillboardAd != null && nativeAdShowing)
            {
                try
                {
                    nativeBillboardAd.Hide();
                }
                catch(Exception ex)
                {
                    Debug.LogWarning("[AdManager] HideNativeScreenAd error: " + ex.Message);
                }
                nativeAdShowing = false;
                currentNativePos = new Vector2Int(-9999, -9999);
            }
        }

        public void HideNativeBillboardAd() => HideNativeScreenAd();

        public void DestroyNativeBillboardAd()
        {
            if(nativeBillboardAd != null)
            {
                try { nativeBillboardAd.Destroy(); } catch {}
                nativeBillboardAd = null;
            }
            nativeAdShowing = false;
            loadingNativeAd = false;
            currentNativePos = new Vector2Int(-9999, -9999);
            nativeAdRetry = Time.unscaledTime + 5f;
        }
        void LoadRewarded() {
            if(!AdsAllowed || !initialized || loadingReward || rewarded!=null || showing) return;
            loadingReward=true;
            int epoch=consentEpoch;
            try {
                RewardedAd.Load(GetRewardedAdUnitId(),new AdRequest(),(ad,error)=>callbacks.Enqueue(()=>{
                    loadingReward=false;
                    if(disposed || epoch!=consentEpoch || !AdsAllowed) {ad?.Destroy();return;}
                    if(error!=null || ad==null) {
                        ad?.Destroy();
                        rewardRetry=Time.unscaledTime+rewardRetryDelay;
                        rewardRetryDelay=Mathf.Min(rewardRetryDelay*1.6f,300f);
                        Debug.LogWarning("[AdManager] Rewarded load failed: "+error);
                        if(waiting) {Notice("Ad unavailable. Check your connection and retry.");FinishReward(false);}
                        return;
                    }
                    rewardRetryDelay=60f;
                    rewarded=ad;
                    ad.OnAdFullScreenContentClosed+=()=>callbacks.Enqueue(()=>{if(rewarded==ad) FinishReward(earned);});
                    ad.OnAdFullScreenContentFailed+=failure=>callbacks.Enqueue(()=>{
                        if(rewarded!=ad) return;
                        Debug.LogWarning("[AdManager] Rewarded show failed: "+failure);
                        Notice("Ad could not open. Please retry.");FinishReward(false);
                    });
                    if(waiting) PresentReward();
                }));
            } catch(Exception e) {
                loadingReward=false;
                rewardRetry=Time.unscaledTime+rewardRetryDelay;
                rewardRetryDelay=Mathf.Min(rewardRetryDelay*1.6f,300f);
                Debug.LogException(e);
                if(waiting) {Notice("Ad unavailable. Please retry.");FinishReward(false);}
            }
        }
        void LoadInterstitial() {
            if(!AdsAllowed || !initialized || loadingInterstitial || interstitial!=null || showing) return;
            loadingInterstitial=true;
            int epoch=consentEpoch;
            try {
                InterstitialAd.Load(GetInterstitialAdUnitId(),new AdRequest(),(ad,error)=>callbacks.Enqueue(()=>{
                    loadingInterstitial=false;
                    if(disposed || epoch!=consentEpoch || !AdsAllowed) {ad?.Destroy();return;}
                    if(error!=null || ad==null) {
                        ad?.Destroy();
                        interstitialRetry=Time.unscaledTime+interstitialRetryDelay;
                        interstitialRetryDelay=Mathf.Min(interstitialRetryDelay*1.6f,300f);
                        Debug.LogWarning("[AdManager] Interstitial load failed: "+error);return;
                    }
                    interstitialRetryDelay=60f;
                    interstitial=ad;
                    ad.OnAdFullScreenContentClosed+=()=>callbacks.Enqueue(()=>{if(interstitial==ad) FinishInterstitial();});
                    ad.OnAdFullScreenContentFailed+=failure=>callbacks.Enqueue(()=>{
                        if(interstitial!=ad) return;
                        Debug.LogWarning("[AdManager] Interstitial show failed: "+failure);FinishInterstitial();
                    });
                }));
            } catch(Exception e) {
                loadingInterstitial=false;
                interstitialRetry=Time.unscaledTime+interstitialRetryDelay;
                interstitialRetryDelay=Mathf.Min(interstitialRetryDelay*1.6f,300f);
                Debug.LogException(e);
            }
        }
        void LoadRewardedInterstitial() {
            if(!AdsAllowed || !initialized || loadingRewardedInterstitial || rewardedInterstitial!=null || showing || !HasRewardedInterstitialUnit) return;
            loadingRewardedInterstitial=true;
            int epoch=consentEpoch;
            try {
                RewardedInterstitialAd.Load(GetRewardedInterstitialAdUnitId(),new AdRequest(),(ad,error)=>callbacks.Enqueue(()=>{
                    loadingRewardedInterstitial=false;
                    if(disposed || epoch!=consentEpoch || !AdsAllowed) {ad?.Destroy();return;}
                    if(error!=null || ad==null) {
                        ad?.Destroy();
                        rewardedInterstitialRetry=Time.unscaledTime+rewardedInterstitialRetryDelay;
                        rewardedInterstitialRetryDelay=Mathf.Min(rewardedInterstitialRetryDelay*1.6f,300f);
                        Debug.LogWarning("[AdManager] Rewarded Interstitial load failed: "+error);return;
                    }
                    rewardedInterstitialRetryDelay=60f;
                    rewardedInterstitial=ad;
                    ad.OnAdFullScreenContentClosed+=()=>callbacks.Enqueue(()=>{if(rewardedInterstitial==ad) FinishRewardedInterstitial(earned);});
                    ad.OnAdFullScreenContentFailed+=failure=>callbacks.Enqueue(()=>{
                        if(rewardedInterstitial!=ad) return;
                        Debug.LogWarning("[AdManager] Rewarded Interstitial show failed: "+failure);FinishRewardedInterstitial(false);
                    });
                }));
            } catch(Exception e) {
                loadingRewardedInterstitial=false;
                rewardedInterstitialRetry=Time.unscaledTime+rewardedInterstitialRetryDelay;
                rewardedInterstitialRetryDelay=Mathf.Min(rewardedInterstitialRetryDelay*1.6f,300f);
                Debug.LogException(e);
            }
        }
        public void ShowRewardedAd(Action<bool> onComplete,string placementTag="general_reward") {
            if(!ReleaseConfiguration.Current.adsEnabled) {Notice("Sponsored rewards are currently unavailable.");onComplete?.Invoke(false);return;}
            if(IsBusy) {onComplete?.Invoke(false);return;}
            rewardCallback=onComplete;earned=false;waiting=true;deadline=Time.unscaledTime+25;
            Status="Loading ad...";noticeUntil=0;
            Debug.Log("[AdManager] Reward requested: "+placementTag);
            if(!AdsAllowed) GatherConsent();else InitializeAds();
            if(rewarded!=null && rewarded.CanShowAd()) PresentReward();
            else {rewarded?.Destroy();rewarded=null;LoadRewarded();}
        }
        void PresentReward() {
            if(!AdsAllowed || !waiting || rewarded==null || !rewarded.CanShowAd()) return;
            waiting=false;showing=true;
            var shown=rewarded;
            try {shown.Show(reward=>callbacks.Enqueue(()=>{if(rewarded==shown && showing) earned=true;}));}
            catch(Exception e) {Debug.LogException(e);Notice("Ad could not open. Please retry.");FinishReward(false);}
        }
        void FinishReward(bool success) {
            if(!waiting && !showing && rewardCallback==null) return;
            GameAnalyticsManager.TrackAdWatched("rewarded", success);
            var callback=rewardCallback;rewardCallback=null;waiting=false;showing=false;
            rewarded?.Destroy();rewarded=null;earned=false;
            try {callback?.Invoke(success);} finally {if(!disposed && Time.unscaledTime>=rewardRetry) LoadRewarded();}
        }
        public void NotifyStageCompleted()=>completedStages++;
        public bool ShowInterstitialIfReady(Action onClosed=null,string placementTag="transition") {
            if(AdsAllowed && !IsBusy && completedStages>=Mathf.Max(1,stagesPerInterstitial)
                && Time.unscaledTime-lastInterstitial>=interstitialCooldownSeconds) {
                if(rewardedInterstitial!=null && rewardedInterstitial.CanShowAd()) {
                    ShowRewardedInterstitial(success=>{
                        if(success) {
                            try {
                                PlayerPrefs.SetInt("GeoSniper.Credits", PlayerPrefs.GetInt("GeoSniper.Credits", 0) + 500);
                                PlayerPrefs.Save();
                            } catch {}
                        }
                        onClosed?.Invoke();
                    },placementTag);
                    return true;
                }
                if(interstitial!=null && interstitial.CanShowAd()) {
                    ShowInterstitial(onClosed,placementTag);
                    return true;
                }
            }
            onClosed?.Invoke();return false;
        }
        public void ShowInterstitial(Action onClosed=null,string placementTag="transition") {
            if(!AdsAllowed || IsBusy || interstitial==null || !interstitial.CanShowAd()) {
                if(!IsBusy) {interstitial?.Destroy();interstitial=null;LoadInterstitial();}
                onClosed?.Invoke();return;
            }
            closeCallback=onClosed;showing=true;completedStages=0;lastInterstitial=Time.unscaledTime;
            try {interstitial.Show();} catch(Exception e) {Debug.LogException(e);FinishInterstitial();}
        }
        void FinishInterstitial() {
            var callback=closeCallback;closeCallback=null;showing=false;
            interstitial?.Destroy();interstitial=null;
            try {callback?.Invoke();} finally {if(!disposed) LoadInterstitial();}
        }
        public bool ShowRewardedInterstitialIfReady(Action<bool> onComplete=null,string placementTag="stage_transition") {
            if(AdsAllowed && !IsBusy && completedStages>=Mathf.Max(1,stagesPerInterstitial)
                && Time.unscaledTime-lastInterstitial>=interstitialCooldownSeconds
                && rewardedInterstitial!=null && rewardedInterstitial.CanShowAd()) {
                ShowRewardedInterstitial(onComplete,placementTag);
                return true;
            }
            return false;
        }
        public void ShowRewardedInterstitial(Action<bool> onComplete=null,string placementTag="stage_transition") {
            if(!AdsAllowed || IsBusy || rewardedInterstitial==null || !rewardedInterstitial.CanShowAd()) {
                if(!IsBusy) {rewardedInterstitial?.Destroy();rewardedInterstitial=null;if(HasRewardedInterstitialUnit) LoadRewardedInterstitial();}
                onComplete?.Invoke(false);
                return;
            }
            rewardedInterstitialCallback=onComplete;
            earned=false;
            showing=true;
            completedStages=0;
            lastInterstitial=Time.unscaledTime;
            var shown=rewardedInterstitial;
            try {
                shown.Show(reward=>callbacks.Enqueue(()=>{if(rewardedInterstitial==shown && showing) earned=true;}));
            } catch(Exception e) {
                Debug.LogException(e);
                FinishRewardedInterstitial(false);
            }
        }
        void FinishRewardedInterstitial(bool success) {
            if(!showing && rewardedInterstitialCallback==null) return;
            var callback=rewardedInterstitialCallback;
            rewardedInterstitialCallback=null;
            showing=false;
            rewardedInterstitial?.Destroy();
            rewardedInterstitial=null;
            earned=false;
            try {callback?.Invoke(success);}
            finally {if(!disposed && HasRewardedInterstitialUnit && Time.unscaledTime>=rewardedInterstitialRetry) LoadRewardedInterstitial();}
        }
        void Notice(string message) {Status=message;noticeUntil=Time.unscaledTime+7;}
        void Update() {
            while(callbacks.TryDequeue(out var callback)) {try {callback();} catch(Exception e) {Debug.LogException(e);}}
            if(waiting && Time.unscaledTime>=deadline) {
                Notice("Ad loading timed out. Check your connection and retry.");FinishReward(false);
            }
            if(initialized && !IsBusy) {
                if(rewarded==null && !loadingReward && Time.unscaledTime>=rewardRetry) LoadRewarded();
                if(interstitial==null && !loadingInterstitial && Time.unscaledTime>=interstitialRetry) LoadInterstitial();
                if(rewardedInterstitial==null && !loadingRewardedInterstitial && HasRewardedInterstitialUnit && Time.unscaledTime>=rewardedInterstitialRetry) LoadRewardedInterstitial();
                if(nativeBillboardAd==null && !loadingNativeAd && Time.unscaledTime>=nativeAdRetry) LoadNativeBillboardAd();
            }
        }
        void OnDestroy() {
            if(instance!=this) return;
            disposed=true;instance=null;rewarded?.Destroy();interstitial?.Destroy();rewardedInterstitial?.Destroy();banner?.Destroy();banner=null;
            rewardCallback?.Invoke(false);closeCallback?.Invoke();rewardedInterstitialCallback?.Invoke(false);
        }
        void OnGUI() {
            if(!waiting && Time.unscaledTime>=noticeUntil) return;
            var matrix=GUI.matrix;int depth=GUI.depth;var color=GUI.color;
            try {
                GUI.depth=-1000;GUI.color=Color.white;
                float scale=Mathf.Max(.01f,Mathf.Min(Screen.width/700f,Screen.height/400f));
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
                float w=Screen.width/scale,h=Screen.height/scale;
                CommandGUI.Fill(new Rect(0,0,w,h),new Color(.02f,.03f,.04f,.95f));
                var rect=new Rect((w-540)/2,(h-180)/2,540,180);
                CommandGUI.Fill(rect,CommandGUI.ThemeCard);
                GUI.Label(new Rect(rect.x+24,rect.y+22,492,70),Status,new GUIStyle(GUI.skin.label){
                    fontSize=20,wordWrap=true,normal={textColor=CommandGUI.Text}});
                if(CommandGUI.DrawButton(new Rect(rect.x+24,rect.y+110,492,46),waiting?"CANCEL":"CLOSE",true,14)) {
                    noticeUntil=0;if(waiting) FinishReward(false);
                }
                GUI.Button(new Rect(0,0,w,h),GUIContent.none,GUIStyle.none);
            } finally {GUI.matrix=matrix;GUI.depth=depth;GUI.color=color;}
        }
        // Opening the store never claims a rating was submitted or grants currency.
        public static void OpenPlayStoreRating()
        {
            Application.OpenURL("https://play.google.com/store/apps/details?id="+Uri.EscapeDataString(Application.identifier));
        }
    }
}
