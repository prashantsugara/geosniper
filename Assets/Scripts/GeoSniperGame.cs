using System.Collections;
using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame : MonoBehaviour
    {
        public string overpassEndpoint = "https://lz4.overpass-api.de/api/interpreter";
        public bool enableElevationForGameplay = true;
        string status = "EXPLORE YOUR SECTOR";
        string startupStage="WORLD";
        string mapNotice="";
        bool geographicMap;
        bool loading, ready;
        public static bool IsDebugMode
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            get => PlayerPrefs.GetInt("GeoSniper.DebugMode", 0) == 1;
            set => PlayerPrefs.SetInt("GeoSniper.DebugMode", value ? 1 : 0);
#else
            get => false;
            set { }
#endif
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if(pauseStatus && loading)TrackDeployment("deployment_backgrounded","");
            if(pauseStatus) loadingPausedAt=Time.realtimeSinceStartup;
            else ResumeLoadingAfterPause(Time.realtimeSinceStartup);
            if (pauseStatus) PlayerPrefs.Save();
        }
        void ResumeLoadingAfterPause(float resumedAt)
        {
            if(loadingPausedAt<0)return;
            if(loading)
            {
                float paused=Mathf.Max(0,resumedAt-loadingPausedAt);
                loadingDeadline+=paused;
                worldLoadingStarted+=paused;
            }
            loadingPausedAt=-1;
        }
        float loadingDeadline;
        float loadingPausedAt=-1,worldLoadingStarted;
        Camera view;
        float yaw=30, pitch=48, distance=230;
        SectorWorld world;
        bool cachedSectorReady,cachedSectorOffline;
        bool cachedSectorFromGps;
        bool cachedSectorElevationEnabled;
        double cachedSectorLatitude,cachedSectorLongitude;
        UrbanCombatMission mission;
        SectorStreamer streamer;
        GameLocation activeLocation;
        bool choosingLocation;
        bool choosingStage;
        int selectedNodeId;
        Vector2 stageScroll;
        bool stageDragging;
        Vector2 stageDragStartPos;
        Vector2 stageDragStartScroll;
        float stageScrollVelocity;
        bool stageDragMoved;
        int stageIndexToStart = -1;
        int campaignNodeToStart = -1;
        bool loadFailed;
        string locationSearch="", locationError="Search for a city, neighbourhood, street or landmark.";
        public string placeSearchEndpoint="https://photon.komoot.io/api/";
        bool searching;
        float nextSearch;
        Vector2 locationScroll;
        System.Collections.Generic.List<GameLocation> locations;
        private static GeoSniperGame instance;
        public static GeoSniperGame Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<GeoSniperGame>();
                return instance;
            }
            private set => instance = value;
        }
        private bool pendingMultiplayerDuel;
        private bool pendingDuelHost;
        private string pendingDuelTarget = "";
        bool isPvPDuel;
        bool rangeToStart;
        bool showingArmory;
        bool showingSettings;
        AudioSource lobbyMusic;
        AudioClip lobbyMusicClip;
        // A mission can request location from any lobby screen. Keep that intent while the
        // privacy prompt is visible so accepting location does not silently switch modes.
        bool resumeMissionAfterLocationConsent;
        bool duelLocationSelection;

        public void OpenDuelLobby()
        {
            if (loading || ready) return;
            SwitchTab(LobbyTab.Home);
            duelLocationSelection = false;
            showingLocationConsent = false;
            resumeMissionAfterLocationConsent = false;
            loadFailed = false;
            PlayerPrefs.SetInt("GeoSniper.SelectedMode", 3);
            PlayerPrefs.Save();
            GeoSniper.Duel.SniperDuelHUD.Instance.ShowLobbyModal = true;
        }

        void ClearDuelDeploymentIntent()
        {
            if (GeoSniper.Duel.SniperDuelHUD.LobbyModalOpen)
                GeoSniper.Duel.SniperDuelHUD.Instance.ShowLobbyModal = false;
            var network = GeoSniper.Duel.SniperDuelNetwork.ActiveInstance;
            var duel = GeoSniper.Duel.SniperDuelManager.ActiveInstance;
            if (duel != null)
                duel.ResetSession("Switched from duel to another game mode.");
            else if (network != null && (network.IsHost || network.IsConnected || network.IsConnecting || network.IsReconnecting))
                network.Disconnect("Switched from duel to another game mode.");
            pendingMultiplayerDuel = false; pendingDuelHost = false; pendingDuelTarget = "";
            duelLocationSelection = false;
            resumeMissionAfterLocationConsent = false; showingLocationConsent = false;
        }

        void SelectLobbyMode(int mode)
        {
            mode = Mathf.Clamp(mode, 0, 3);
            if (mode == 3) { OpenDuelLobby(); return; }
            ClearDuelDeploymentIntent();
            PlayerPrefs.SetInt("GeoSniper.SelectedMode", mode);
            PlayerPrefs.Save();
            loadFailed = false;
            SwitchTab(mode == 0 ? LobbyTab.Campaign : LobbyTab.Home);
        }

        public void ChooseDuelLocation(bool useLiveLocation)
        {
            GeoSniper.Duel.SniperDuelHUD.Instance.ShowLobbyModal = false;
            duelLocationSelection = true;
            if (useLiveLocation) showingLocationConsent = true;
            else SwitchTab(LobbyTab.Location);
        }

        public enum LobbyTab
        {
            Home = 0,
            Campaign = 1,
            Armory = 2,
            Location = 3,
            Rewards = 4,
            Settings = 5
        }
        public LobbyTab currentTab = LobbyTab.Home;

        public void SwitchTab(LobbyTab tab)
        {
            if(tab==LobbyTab.Armory && currentTab!=LobbyTab.Armory) armoryCategory=-1;
            if(CustomizingTouchControls) EndTouchControlLayout(false);
            if(tab==LobbyTab.Location && !ReleaseConfiguration.Current.liveMapsEnabled) tab=LobbyTab.Home;
            if (tab == LobbyTab.Campaign || (duelLocationSelection && tab != LobbyTab.Location && tab != LobbyTab.Home))
                ClearDuelDeploymentIntent();
            if(tab==LobbyTab.Home){campaignLocationEdit=false;campaignBriefingReviewed=false;returnToCampaignFromArmory=false;}
            currentTab = tab;
            choosingStage = (tab == LobbyTab.Campaign);
            showingArmory = (tab == LobbyTab.Armory);
            choosingLocation = (tab == LobbyTab.Location);
            showingRewards = (tab == LobbyTab.Rewards);
            showingSettings = (tab == LobbyTab.Settings);
            showingModeSelector = false;
            if (tab == LobbyTab.Campaign) PlayBriefingAudio(selectedNodeId);
            else StopBriefingAudio();
        }

        public void DeployPvPDuel(bool asHost, string roomCodeOrIp = "", bool isPracticeBot = false, GameLocation hostLocation = null, bool useLiveLocation = false)
        {
            if (loading) return;
            if (asHost && !isPracticeBot && hostLocation == null && !useLiveLocation)
            { ChooseDuelLocation(false); return; }
            duelLocationSelection = false;
            PlayerPrefs.SetInt("GeoSniper.SelectedMode", 3);
            PlayerPrefs.Save();
            loadFailed = false; status = ""; mapNotice = "";
            isPvPDuel = true;
            rangeToStart = false;
            campaignNodeToStart = -1;
            stageIndexToStart = -1;

            pendingMultiplayerDuel = !isPracticeBot;
            pendingDuelHost = asHost;
            pendingDuelTarget = roomCodeOrIp;

            if (isPracticeBot)
            {
                GeoSniper.Duel.SniperDuelNetwork.Instance?.Disconnect();
            }
            else
            {
                if (asHost)
                {
                    if (!GeoSniper.Duel.SniperDuelNetwork.Instance.IsHost && !GeoSniper.Duel.SniperDuelNetwork.Instance.StartHost())
                    { LoadingFailed(new System.InvalidOperationException(GeoSniper.Duel.SniperDuelNetwork.Instance.LastDisconnectReason)); return; }
                }
                else if (!string.IsNullOrEmpty(roomCodeOrIp))
                {
                    if (!GeoSniper.Duel.SniperDuelNetwork.Instance.StartClient(roomCodeOrIp))
                    { LoadingFailed(new System.InvalidOperationException(GeoSniper.Duel.SniperDuelNetwork.Instance.LastDisconnectReason)); return; }
                }
            }

            if (!isPracticeBot)
            {
                StartCoroutine(asHost ? LoadHostDuelMap(hostLocation, useLiveLocation) : LoadGuestDuelMap());
                return;
            }
            // If an existing sector is already loaded in memory, reuse it immediately
            if (isPracticeBot && world != null && cachedSectorReady && world.SpawnPoints != null && world.SpawnPoints.Count > 0)
            {
                if (armoryRoom != null) armoryRoom.SetActive(false);
                if (sectorSun != null) sectorSun.enabled = true;
                StartCoroutine(ResumeLoadedSector());
            }
            else
            {
                // Instant deterministic rooftop arena (0 network latency, 0 OSM failure, 100% reliable)
                StartOffline(preserveContract: true);
            }
        }

        void PrepareSharedDuelLoading()
        {
            if (armoryRoom != null) armoryRoom.SetActive(false);
            if (sectorSun != null) sectorSun.enabled = true;
            loading = true; ready = false; loadFailed = false; choosingLocation = false;
            startupStage = "DUEL MAP"; loadingDeadline = Time.realtimeSinceStartup + 180;
            if (mission != null) { mission.enabled = false; Destroy(mission); mission = null; }
            if (streamer != null) { streamer.StopAndPreserveFirstSector(); Destroy(streamer); streamer = null; }
            DiscardCachedSector();
        }

        IEnumerator LoadHostDuelMap(GameLocation location, bool useLiveLocation)
        {
            PrepareSharedDuelLoading();
            var relay = GeoSniper.Duel.SniperDuelNetwork.Instance;
            status = "CREATING UNITY RELAY ROOM...";
            while(relay.IsRelayStarting) yield return null;
            if(!relay.IsHost) { LoadingFailed(new System.InvalidOperationException(relay.LastDisconnectReason)); yield break; }
            status = useLiveLocation ? "FINDING HOST'S LIVE LOCATION..." : "LOADING HOST'S CHOSEN LOCATION...";
            if (useLiveLocation) yield return new LocationResolver().Resolve(message => status = message, result => location = result);
            if (location == null)
            {
                loading = false; duelLocationSelection = true;
                locationError = "No accurate GPS fix. Retry live location or choose a place for your duel.";
                SwitchTab(LobbyTab.Location);
                yield break;
            }
            activeLocation = location;
            loadingDeadline = Time.realtimeSinceStartup + SectorMap.LoadTimeoutSeconds;
            System.Collections.Generic.List<MapFeature> features = null;
            bool offline = true;
            yield return SectorMap.Load(location, overpassEndpoint, (map, fallback) => { features = map; offline = fallback; }, message => status = message, refresh: false);
            if (features == null || offline)
            {
                LoadingFailed(new System.InvalidOperationException("The selected location's map could not be loaded. Choose another place or retry."));
                yield break;
            }
            geographicMap = true; mapNotice = SectorMap.LastSource;
            world = new GameObject("Host duel sector").AddComponent<SectorWorld>();
            world.SourceLabel = mapNotice;
            loadingDeadline = Time.realtimeSinceStartup + 90;
            if (enableElevationForGameplay) yield return SectorElevation.Load(location, message => status = message, elevation => world.Elevation = elevation);
            byte[] mapData;
            try
            {
                mapData = new GeoSniper.Duel.DuelMapSnapshot
                {
                    Location = location, Source = mapNotice, Features = features,
                    ElevationAvailable = world.Elevation.Available, Heights = world.Elevation.Heights
                }.Encode();
                GeoSniper.Duel.SniperDuelNetwork.Instance.SetHostMap(mapData, location.Latitude, location.Longitude);
            }
            catch (System.Exception error) { LoadingFailed(error); yield break; }
            SectorElevation.LastNotice = world.Elevation.Notice;
            yield return BuildSector(features);
        }

        IEnumerator LoadGuestDuelMap()
        {
            PrepareSharedDuelLoading();
            var network = GeoSniper.Duel.SniperDuelNetwork.Instance;
            loadingDeadline = Time.realtimeSinceStartup + 600;
            status = "WAITING FOR THE HOST TO CHOOSE AND SHARE THE DUEL LOCATION...";
            while (network.SharedMapData == null && (network.IsConnected || network.IsConnecting)) yield return null;
            if (network.SharedMapData == null)
            { LoadingFailed(new System.InvalidOperationException(network.LastDisconnectReason)); yield break; }
            GeoSniper.Duel.DuelMapSnapshot snapshot;
            try { snapshot = GeoSniper.Duel.DuelMapSnapshot.Decode(network.SharedMapData); }
            catch (System.Exception error) { LoadingFailed(error); yield break; }
            activeLocation = snapshot.Location;
            geographicMap = true; mapNotice = snapshot.Source;
            status = "LOADING HOST'S LOCATION: " + activeLocation.Label;
            world = new GameObject("Shared host duel sector").AddComponent<SectorWorld>();
            world.SourceLabel = snapshot.Source;
            world.Elevation.Available = snapshot.ElevationAvailable;
            System.Array.Copy(snapshot.Heights, world.Elevation.Heights, snapshot.Heights.Length);
            world.Elevation.Notice = "Terrain shared by duel host";
            SectorElevation.LastNotice = world.Elevation.Notice;
            yield return BuildSector(snapshot.Features);
        }

        void ExecuteDeploy(bool useLiveLocation=false)
        {
            loadFailed=false; status=""; mapNotice="";
            int sel = Mathf.Clamp(PlayerPrefs.GetInt("GeoSniper.SelectedMode",0),0,3);
            if (sel == 3)
            {
                OpenDuelLobby();
                return;
            }
            PrepareDeployment(sel);
            StartPreferredLocationMission(useLiveLocation);
        }

        void PrepareDeployment(int selectedMode)
        {
            if (selectedMode != 3) ClearDuelDeploymentIntent();
            rangeToStart = selectedMode == 2;
            if (selectedMode == 0) // Campaign
            {
                int defaultNode = GetDefaultSelectedNodeId();
                var node = CampaignNodeGraph.GetNode(defaultNode);
                PlayerPrefs.SetInt("GeoSniper.ActiveNodeId", defaultNode);
                campaignNodeToStart=defaultNode;
                isPvPDuel = node != null && node.isPvPDuel;
                stageIndexToStart = node != null ? node.stageIndex : 0;
            }
            else if (selectedMode == 1) // GPS Sector
            {
                campaignNodeToStart=-1;
                isPvPDuel = false;
                stageIndexToStart = -1;
            }
            else if (selectedMode == 2) // Firing Range
            {
                campaignNodeToStart = -1;
                stageIndexToStart = -1;
                isPvPDuel = false;
            }
            else if (selectedMode == 3) // PvP Duel
            {
                campaignNodeToStart=-1;
                isPvPDuel = true;
                stageIndexToStart = -1;
            }
        }

        GameLocation PreferredLocation()
        {
            // Live is a source choice, not a preference for whichever map is cached.
            if(LocationSelectionPolicy.RequiresLiveFix(PlayerPrefs.GetString("GeoSniper.LocationSource","")))
            {
                // A campaign retry or next level in this session can use the
                // sector already built from this device's GPS. The explicit
                // "Use my live location" action still requests a new fix.
                return cachedSectorReady && cachedSectorFromGps && !cachedSectorOffline
                    && world != null && activeLocation != null && activeLocation.Source == "gps"
                    ? activeLocation : null;
            }
            if (activeLocation != null) return activeLocation;
            if (PlayerPrefs.HasKey("GeoSniper.SavedLat") && PlayerPrefs.HasKey("GeoSniper.SavedLon"))
            {
                double latitude=PlayerPrefs.GetFloat("GeoSniper.SavedLat");
                double longitude=PlayerPrefs.GetFloat("GeoSniper.SavedLon");
                string label=PlayerPrefs.GetString("GeoSniper.SavedLabel", "SAVED GPS SECTOR");
                // Migration for builds that persisted GameLocation.Default (Tokyo) as
                // if it were the player's GPS sector. Do not remove real Tokyo searches.
                if(string.Equals(label, "TOKYO", System.StringComparison.OrdinalIgnoreCase)
                    && System.Math.Abs(latitude-35.6762)<0.0002
                    && System.Math.Abs(longitude-139.6503)<0.0002)
                {
                    PlayerPrefs.DeleteKey("GeoSniper.SavedLat");
                    PlayerPrefs.DeleteKey("GeoSniper.SavedLon");
                    PlayerPrefs.DeleteKey("GeoSniper.SavedLabel");
                    PlayerPrefs.Save();
                    return null;
                }
                return new GameLocation
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Label = label,
                    Source = "saved_cache"
                };
            }
            // Never substitute a city on another continent for an unavailable GPS fix.
            // First-run missions must obtain consent and a fresh device location, or let
            // the player select a named place from the World Map screen.
            if (isPvPDuel) return GameLocation.Default;
            return null;
        }

        void StartPreferredLocationMission(bool useLiveLocation=false)
        {
            if(CheckCampaignWeaponReadiness(useLiveLocation)) return;
            GameLocation location=useLiveLocation?null:PreferredLocation();
            if(location!=null)
            {
                StartCoroutine(Play(location));
                return;
            }
            resumeMissionAfterLocationConsent=true;
            if(campaignBriefingReviewed)
            {
                // An explicit GPS source choice already expresses the player's intent.
                // LocationResolver still obtains the operating system's permission.
                if(LocationSelectionPolicy.RequiresLiveFix(PlayerPrefs.GetString("GeoSniper.LocationSource","")))
                {resumeMissionAfterLocationConsent=false;StartCoroutine(Play(null,true));return;}
                campaignLocationEdit=true;SwitchTab(LobbyTab.Location);return;
            }
            showingLocationConsent=true;
        }

        void StartMission()
        {
            EnsureMainCamera();
            if (world == null)
            {
                world = new GameObject("Safe Sector").AddComponent<SectorWorld>();
            }
            Vector3? spawn = null;
            if (geographicMap)
            {
                if (world.TryFindGeographicSpawn(Vector3.zero, out var exactSpawn))
                    spawn = exactSpawn;
                else
                {
                    throw new System.InvalidOperationException("NO SAFE DRY LAND IN LOADED SECTOR");
                }
            }
            else
            {
                spawn = new Vector3(0, world.Ground(0, 0) + 1.2f, 0);
            }

            if (mission != null)
            {
                Destroy(mission);
                mission = null;
            }

            mission = gameObject.AddComponent<UrbanCombatMission>(); 
            var safeSpawns = (world != null && world.SpawnPoints != null) ? world.SpawnPoints : new System.Collections.Generic.List<Vector3>();
            var safeRoofs = (world != null && world.RooftopSpawns != null) ? world.RooftopSpawns : new System.Collections.Generic.List<Vector3>();

            bool prepareContract=stageIndexToStart>=0 || isPvPDuel;
            mission.Begin(view, safeSpawns, safeRoofs, !prepareContract, activeLocation, geographicMap, spawn, prepareContract);
            if (isPvPDuel)
            {
                mission.BeginPvPDuel(campaignNodeToStart, pendingMultiplayerDuel);
                if (pendingMultiplayerDuel && GeoSniper.Duel.SniperDuelManager.Instance != null)
                {
                    GeoSniper.Duel.SniperDuelManager.Instance.StartDuelMatch(mission, pendingDuelHost, pendingDuelTarget);
                    pendingMultiplayerDuel = false;
                }
            }
            else if (stageIndexToStart >= 0)
            {
                mission.BeginStage(stageIndexToStart);
                if(campaignBriefingReviewed && mission.currentMissionState==MissionState.Briefing)mission.StartPreparedCampaign();
            }
            else if (rangeToStart) mission.BeginRange();
            campaignBriefingReviewed=false;campaignLocationEdit=false;

            if (!isPvPDuel && activeLocation != null && geographicMap && mission != null && mission.Player != null)
            {
                try
                {
                    if (streamer != null) { streamer.StopAndPreserveFirstSector(); Destroy(streamer); streamer = null; }
                    streamer = gameObject.AddComponent<SectorStreamer>();
                    streamer.Begin(mission.Player, activeLocation, overpassEndpoint, world, enableElevationForGameplay);
                }
                catch (System.Exception exStreamer)
                {
                    Debug.LogWarning("[GeoSniper] Streamer auto-recovered: " + exStreamer.Message);
                }
            }

            loading = false;
            EndDeploymentTracking("deployment_completed");
            ready = true;
            GameAnalyticsManager.TrackCustomEvent("first_playable_frame",new System.Collections.Generic.Dictionary<string,object>{
                {"stage_index",stageIndexToStart+1},
                {"duration_sec",Mathf.RoundToInt(Time.realtimeSinceStartup-deploymentStartedAt)},
                {"attempt_id",GameAnalyticsManager.CurrentDeploymentAttempt}
            });
            loadFailed = false;
            status = "";
            mapNotice = "";
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<GeoSniperGame>() == null) new GameObject("Geo Sniper").AddComponent<GeoSniperGame>();
        }
        void EnsureMainCamera()
        {
            if (view == null) view = Camera.main;
            if (view == null)
            {
                var camGo = GameObject.FindWithTag("MainCamera");
                if (camGo != null) view = camGo.GetComponent<Camera>();
            }
            if (view == null)
            {
                var existing = FindAnyObjectByType<Camera>();
                if (existing != null && existing.name != "TopViewMapCamera") view = existing;
            }
            if (view == null)
            {
                var camObj = new GameObject("Sector Main Camera");
                camObj.tag = "MainCamera";
                view = camObj.AddComponent<Camera>();
            }
            view.enabled = true;
            view.targetTexture = null;
            view.targetDisplay = 0;
            view.farClipPlane = 2200;
            view.clearFlags = CameraClearFlags.Depth;
            view.backgroundColor = RenderSettings.fogColor;
            view.allowHDR = true;
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                view.gameObject.AddComponent<AudioListener>();
            }
            MobilePostProcess.Ensure(view);
        }

        void Awake()
        {
            Instance = this;
            Input.backButtonLeavesApp = false;
            Application.runInBackground = true;
            enableElevationForGameplay = PlayerPrefs.GetInt("GeoSniper.RealElevation", 1) == 1;
            Application.targetFrameRate=PlayerPrefs.GetInt("GeoSniper.TargetFPS",30)==60?60:30;
            AudioListener.volume=Mathf.Clamp01(PlayerPrefs.GetFloat("GeoSniper.MasterVolume",1f));
            EnsureMainCamera();
            AtmosphericSky.Create(view);
            var sunObj = new GameObject("Sector Sun");
            sectorSun = sunObj.AddComponent<Light>();
            sectorSun.type = LightType.Directional;
            sectorSun.intensity = 1.35f;
            sectorSun.color = new Color(1, .94f, .84f);
            sectorSun.transform.rotation = Quaternion.Euler(38, -35, 0);
            sectorSun.shadows = LightShadows.Soft;
            sectorSun.shadowStrength = .85f;
            sectorSun.enabled = false;
            MobileGraphics.Apply();
            RenderSettings.ambientLight=new Color(.55f,.58f,.61f);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.48f,.62f,.76f);
            RenderSettings.ambientEquatorColor=new Color(.48f,.47f,.41f);
            RenderSettings.ambientGroundColor=new Color(.22f,.24f,.26f);
            RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=new Color(.76f,.82f,.88f);
            RenderSettings.fogDensity=0.0014f;
            EnsureMainCamera();
            WeaponCatalog.EnsureStarterLoadout();
            BuildArmory();
        }

        void OnEnable() { Application.lowMemory+=ReleaseLobbySectorOnLowMemory; }

        void ReleaseLobbySectorOnLowMemory()
        {
            if(!loading && !ready) DiscardCachedSector();
        }

        Light sectorSun;
        GameObject armoryRoom;
        GameObject lobbySoldier;
        GameObject lobbyRifle;
        float lobbyYaw = 0f;
        int lastLobbyWeapon = -1;

        void BuildArmory()
        {
            if (armoryRoom != null) { Destroy(armoryRoom); armoryRoom = null; }
            ReleaseLobbyStageMaterials();
            if (ready) return;
            if (sectorSun != null) sectorSun.enabled = false;

            armoryRoom = new GameObject("LobbyArmory3D");
            BuildLobbyBackdrop();
            
            EnsureMainCamera();
            if (view != null)
            {
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = new Color(0.01f, 0.01f, 0.015f);  // near-black — operator lighting pops
                FrameLobbyOperator();
            }

            // Key Light for Soldier (Warm Tactical Studio Highlight)
            var keyLightObj = new GameObject("LobbyKeyLight");
            keyLightObj.transform.SetParent(armoryRoom.transform, false);
            var keyLight = keyLightObj.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.15f;
            keyLight.color = new Color(1f, 0.68f, 0.38f);
            keyLight.transform.rotation = Quaternion.Euler(24f, -28f, 0f);

            // Rim / Edge Light for Silhouette (Crisp Daylight Rim, no blue blowout)
            var rimLightObj = new GameObject("LobbyRimLight");
            rimLightObj.transform.SetParent(armoryRoom.transform, false);
            var rimLight = rimLightObj.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.intensity = 0.85f;
            rimLight.color = new Color(0.22f, 0.70f, 1f);
            rimLight.transform.rotation = Quaternion.Euler(-15f, 155f, 0f);

            // Soft Fill Light from camera angle
            var fillLightObj = new GameObject("LobbyFillLight");
            fillLightObj.transform.SetParent(armoryRoom.transform, false);
            var fillLight = fillLightObj.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.35f;
            fillLight.color = new Color(0.55f, 0.73f, 0.92f);
            fillLight.transform.rotation = Quaternion.Euler(15f, 40f, 0f);

            // Ambient light for natural shadow fill
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.28f, 0.34f);

            // 3D Soldier Operative
            var soldierPrefab = Resources.Load<GameObject>("Models/Enemies/swat") 
                ?? Resources.Load<GameObject>("Models/Enemies/ch35");
            if (soldierPrefab != null)
            {
                // Rotate the non-animated wrapper; imported idle clips must not overwrite inspection yaw.
                lobbySoldier = ImportedVisual.CreateEnemy(soldierPrefab, armoryRoom.transform);
                lobbySoldier.name = "LobbySoldierOperative";
                lobbySoldier.transform.localPosition = new Vector3(.05f, .04f, 3.1f);
                lobbySoldier.transform.localRotation = Quaternion.Euler(0, 150f, 0);

                foreach (var skin in lobbySoldier.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.updateWhenOffscreen = true;
                }

                var anim = lobbySoldier.GetComponentInChildren<Animation>();
                if (anim == null)
                {
                    var skin = lobbySoldier.GetComponentInChildren<SkinnedMeshRenderer>();
                    var target = ImportedVisual.CharacterAnimationRoot(lobbySoldier.transform);
                    anim = target.GetComponent<Animation>() ?? target.AddComponent<Animation>();
                }
                if (anim != null)
                {
                    if (anim.GetClip("idle") == null && anim.GetClip("swat_idle") == null)
                    {
                        var idleClip = Resources.Load<AnimationClip>("Models/Enemies/Animations/swat_idle");
                        if (idleClip == null)
                        {
                            var all = Resources.LoadAll<AnimationClip>("Models/Enemies/Animations/swat_idle");
                            if (all != null && all.Length > 0)
                            {
                                foreach (var c in all) if (c != null && !c.name.Contains("__preview__")) { idleClip = c; break; }
                                if (idleClip == null) idleClip = all[0];
                            }
                        }
                        if (idleClip != null)
                        {
                            idleClip.legacy = true;
                            anim.AddClip(idleClip, "idle");
                        }
                    }

                    anim.enabled = true;
                    anim.playAutomatically = true;
                    anim.cullingType = AnimationCullingType.AlwaysAnimate;
                    foreach (AnimationState state in anim)
                    {
                        if (state.name.ToLowerInvariant().Contains("idle"))
                        {
                            state.wrapMode = WrapMode.Loop;
                            anim.Play(state.name);
                            anim.Sample();
                            break;
                        }
                    }
                }

                SetupLobbyCharacterMaterials(lobbySoldier);
            }

            UpdateLobbyRifle();

            if (lobbySoldier != null)
            {
                var pose = lobbySoldier.GetComponent<LobbySniperPose>() ?? lobbySoldier.AddComponent<LobbySniperPose>();
                pose.Initialize(lobbyRifle);
            }
        }

        void SetupLobbyCharacterMaterials(GameObject soldier)
        {
            if (soldier == null) return;
            var renderers = soldier.GetComponentsInChildren<Renderer>(true);
            
            var swatBodyTex = Resources.Load<Texture2D>("Models/Enemies/Textures/Soldier_Body_diffuse");
            var swatHeadTex = Resources.Load<Texture2D>("Models/Enemies/Textures/Soldier_head_diffuse");
            var swatBodyNorm = Resources.Load<Texture2D>("Models/Enemies/Textures/Soldier_Body_normal");
            var swatHeadNorm = Resources.Load<Texture2D>("Models/Enemies/Textures/Soldier_head_normal");

            var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                string rName = r.name.ToLowerInvariant();

                Material mat = new Material(shader);
                mat.name = "OperativeMat_" + r.gameObject.name;
                lobbyStageMaterials.Add(mat);

                if (rName.Contains("head"))
                {
                    mat.mainTexture = swatHeadTex;
                    if (swatHeadNorm != null)
                    {
                        mat.SetTexture("_BumpMap", swatHeadNorm);
                        mat.EnableKeyword("_NORMALMAP");
                    }
                    // The SWAT head has separate helmet (slot 0) and face (slot 1) meshes.
                    mat.color = new Color(0.72f, 0.67f, 0.62f);
                    mat.SetFloat("_Metallic", 0.04f);
                    mat.SetFloat("_Glossiness", 0.12f);
                    if (r is SkinnedMeshRenderer headSkin && headSkin.sharedMesh != null && headSkin.sharedMesh.subMeshCount >= 2)
                    {
                        var helmet = new Material(shader) { name = "OperativeMat_Helmet", mainTexture = swatBodyTex,
                            color = new Color(0.48f, 0.52f, 0.55f) };
                        if (swatBodyNorm != null)
                        {
                            helmet.SetTexture("_BumpMap", swatBodyNorm);
                            helmet.EnableKeyword("_NORMALMAP");
                        }
                        helmet.SetFloat("_Metallic", 0.03f);
                        helmet.SetFloat("_Glossiness", 0.12f);
                        lobbyStageMaterials.Add(helmet);
                        r.sharedMaterials = new[] { helmet, mat };
                        continue;
                    }
                }
                else
                {
                    mat.mainTexture = swatBodyTex;
                    if (swatBodyNorm != null)
                    {
                        mat.SetTexture("_BumpMap", swatBodyNorm);
                        mat.EnableKeyword("_NORMALMAP");
                    }
                    // Stealth tactical dark navy / charcoal uniform (eliminates cartoonish bright cyan)
                    mat.color = new Color(0.32f, 0.35f, 0.38f);
                    mat.SetFloat("_Metallic", 0.03f);
                    mat.SetFloat("_Glossiness", 0.16f);
                }

                r.sharedMaterial = mat;
            }
        }

        void ApplyLobbyRifleMaterials(Transform root)
        {
            if(root==null) return;
            WeaponAuthoredMaterials.Apply(root);
            WeaponAuthoredMaterials.ApplyFinish(root);
        }

        void UpdateLobbyRifle()
        {
            int curWpn = WeaponCatalog.SelectedOwnedWeapon;
            if (curWpn == lastLobbyWeapon && lobbyRifle != null) return;
            lastLobbyWeapon = curWpn;

            if (lobbyRifle != null) { Destroy(lobbyRifle); lobbyRifle = null; }
            foreach (var material in lobbyRifleMaterials) if (material != null) Destroy(material);
            lobbyRifleMaterials.Clear();
            if (armoryRoom == null) return;

            var prefab = Resources.Load<GameObject>("Models/Weapons/"+WeaponCatalog.Get(curWpn).Model);
            if (prefab != null)
            {
                // Create clean rifle holder to handle pivot alignment and positioning
                lobbyRifle = new GameObject("LobbyRifleHolder");
                lobbyRifle.transform.SetParent(armoryRoom.transform, false);

                var model = Instantiate(prefab, lobbyRifle.transform, false);

                ImportedVisual.SanitizeWeaponModel(model.transform);
                ApplyLobbyRifleMaterials(model.transform);

                WeaponGeometry.Configure(model.transform, lobbyRifle.transform, curWpn);

                // Parent to soldier root so rifle scale/transforms remain clean without wrist distortion
                lobbyRifle.transform.SetParent(lobbySoldier != null ? lobbySoldier.transform : armoryRoom.transform, true);
                var pose = lobbySoldier != null ? lobbySoldier.GetComponent<LobbySniperPose>() : null;
                if (pose != null) pose.SetRifle(lobbyRifle);
            }
        }

        void UpdateLobbyOperative()
        {
            UpdateLobbyRifle();
            if (lobbySoldier != null)
            {
                lobbySoldier.transform.localRotation = Quaternion.Euler(0f, 150f + lobbyYaw, 0f);
            }
        }
        
        bool CanReuseSector(GameLocation location)
        {
            if(world==null || !cachedSectorReady || cachedSectorOffline || location==null
                || cachedSectorElevationEnabled!=enableElevationForGameplay
                || world.SpawnPoints.Count==0)return false;
            double north=(location.Latitude-cachedSectorLatitude)*111320.0;
            double east=(location.Longitude-cachedSectorLongitude)*111320.0
                *System.Math.Cos(cachedSectorLatitude*System.Math.PI/180.0);
            double allowed=cachedSectorFromGps && location.Source=="gps"
                ? MapCachePolicy.MaxReuseDistance : 1.5;
            return north*north+east*east<=allowed*allowed;
        }

        void RememberSector()
        {
            cachedSectorReady=world!=null && world.SpawnPoints.Count>0;
            cachedSectorOffline=!geographicMap;
            cachedSectorFromGps=activeLocation!=null && activeLocation.Source=="gps";
            cachedSectorElevationEnabled=enableElevationForGameplay;
            if(activeLocation!=null)
            {
                cachedSectorLatitude=activeLocation.Latitude;
                cachedSectorLongitude=activeLocation.Longitude;
            }
        }

        void DiscardCachedSector()
        {
            cachedSectorReady=false;
            if(world==null)return;
            SectorWorld.LoadedWorlds.Remove(world);
            SectorWorld.Ladders.RemoveAll(route=>route.Owner==world);
            world.gameObject.SetActive(false);
            Destroy(world.gameObject);
            world=null;
        }

        IEnumerator ResumeLoadedSector()
        {
            startupStage="PLAYER";
            loading=true;
            loadingDeadline=Time.realtimeSinceStartup+30f;
            if(world==null) { LoadingFailed(new System.InvalidOperationException("CACHED_SECTOR_MISSING")); yield break; }
            world.gameObject.SetActive(true);
            world.ActivateAsPrimarySector();
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            yield return null;
            try { StartMission(); }
            catch(System.Exception error) { LoadingFailed(error); }
        }

        IEnumerator Play(GameLocation selected=null,bool requestDeviceLocation=false)
        {
            // Drive nested map/elevation routines here so their exceptions reach recovery.
            var pending=new System.Collections.Generic.Stack<System.Collections.IEnumerator>();
            pending.Push(PlayCore(selected,requestDeviceLocation));
            while(pending.Count>0)
            {
                object next=null;bool moved=false;System.Exception failure=null;
                try { moved=pending.Peek().MoveNext();if(moved)next=pending.Peek().Current; }
                catch(System.Exception error){failure=error;}
                if(failure!=null){LoadingFailed(failure);yield break;}
                if(!moved){(pending.Pop() as System.IDisposable)?.Dispose();continue;}
                if(next is System.Collections.IEnumerator nested && !(next is CustomYieldInstruction))
                {pending.Push(nested);yield return null;}
                else yield return next;
            }
        }

        IEnumerator PlayCore(GameLocation selected,bool requestDeviceLocation)
        {
            if(!ReleaseConfiguration.Current.liveMapsEnabled) {SwitchTab(LobbyTab.Home);yield break;}
            BeginDeploymentTracking(requestDeviceLocation?"gps":"selected");
            if (armoryRoom != null) armoryRoom.SetActive(false);
            if (sectorSun != null) sectorSun.enabled = true;
            choosingLocation=false;
            loading=true; ready=false; loadFailed=false; startupStage="LOCATION";
            loadingDeadline=Time.realtimeSinceStartup+58;
            if(requestDeviceLocation)
            {
                PlayerPrefs.SetString("GeoSniper.LocationSource","gps");
                PlayerPrefs.Save();
            }
            GameLocation location=LocationSelectionPolicy.Select(requestDeviceLocation,selected,activeLocation);
            if(location==null && requestDeviceLocation) yield return new LocationResolver().Resolve(s=>status=s,l=>location=l);
            if(location==null && LocationSelectionPolicy.AllowsSavedFallback(requestDeviceLocation))
            {
                if(PlayerPrefs.HasKey("GeoSniper.SavedLat") && PlayerPrefs.HasKey("GeoSniper.SavedLon"))
                {
                    location = new GameLocation
                    {
                        Latitude = PlayerPrefs.GetFloat("GeoSniper.SavedLat"),
                        Longitude = PlayerPrefs.GetFloat("GeoSniper.SavedLon"),
                        Label = PlayerPrefs.GetString("GeoSniper.SavedLabel", "CACHED HOME SECTOR"),
                        Source = "saved_cache"
                    };
                }
            }
            if(location==null)
            {
                if(stageIndexToStart>=0 && stageIndexToStart<3 && !isPvPDuel)
                {
                    Debug.LogWarning("[GeoSniper] GPS fix unavailable for early campaign stage, recovering with bundled sector.");
                    StartOffline(true);
                    yield break;
                }
                EndDeploymentTracking("deployment_failed","location_unavailable");
                loading=false; ready=false; choosingLocation=true;
                locations=locations ?? new System.Collections.Generic.List<GameLocation>();
                locationError="No accurate GPS fix. Enable precise location and retry, or search for your place.";
                yield break;
            }
            activeLocation=location;
            PlayerPrefs.SetString("GeoSniper.LocationSource",requestDeviceLocation || location.Source=="gps"?"gps":"selected");
            PlayerPrefs.SetFloat("GeoSniper.SavedLat", (float)location.Latitude);
            PlayerPrefs.SetFloat("GeoSniper.SavedLon", (float)location.Longitude);
            if (!string.IsNullOrEmpty(location.Label)) PlayerPrefs.SetString("GeoSniper.SavedLabel", location.Label);
            PlayerPrefs.Save();
            
            // Save to recent locations if it has a valid label
            if (location != null && !string.IsNullOrEmpty(location.Label))
            {
                string label = location.Label.Replace("|", ""); // Sanitize
                string recentStr = PlayerPrefs.GetString("GeoSniper.RecentLocations", "");
                var recent = new System.Collections.Generic.List<string>(recentStr.Split(new char[]{'|'}, System.StringSplitOptions.RemoveEmptyEntries));
                recent.RemoveAll(x => x.ToLowerInvariant() == label.ToLowerInvariant());
                recent.Insert(0, label);
                if (recent.Count > 4) recent.RemoveRange(4, recent.Count - 4);
                PlayerPrefs.SetString("GeoSniper.RecentLocations", string.Join("|", recent));
                PlayerPrefs.Save();
            }

            if(CanReuseSector(location))
            {
                TrackDeployment("map_load_completed","cached_sector");
                status="PREPARING MISSION...";
                startupStage="PLAYER";
                yield return ResumeLoadedSector();
                yield break;
            }
            DiscardCachedSector();

            startupStage="MAP DOWNLOAD";
            TrackDeployment("map_load_started","");
            loadingDeadline=Time.realtimeSinceStartup+SectorMap.LoadTimeoutSeconds;
            status=location.Label+"\nLOADING MAP SECTOR...";
            mapNotice="";
            System.Collections.Generic.List<MapFeature> map=null;
            yield return SectorMap.Load(location,overpassEndpoint,(features,offline)=>
            {
                map=features;
                geographicMap=!offline;
                mapNotice=SectorMap.LastSource;
            },message=>{ status=message; mapNotice=message; },refresh:false);
            if(map==null)
            {
                LoadingFailed(new System.InvalidOperationException("Map download failed: " + SectorMap.LastFailure));
                yield break;
            }
            TrackDeployment("map_load_completed",geographicMap?"download_or_disk_cache":"offline_fallback");
            world=new GameObject("Sector").AddComponent<SectorWorld>();
            world.SourceLabel=mapNotice;
            loadingDeadline=Time.realtimeSinceStartup+90;
            // A fictional offline grid has no geographic origin to attach DEM heights to.
            if(enableElevationForGameplay && map!=null && mapNotice.IndexOf("OFFLINE",System.StringComparison.OrdinalIgnoreCase)<0)
                yield return SectorElevation.Load(location,s=>status=s,e=>world.Elevation=e);
            else if(!enableElevationForGameplay)
                world.Elevation.Notice="Stable flat gameplay terrain";
            SectorElevation.LastNotice=world.Elevation.Notice;
            yield return BuildSector(map);
        }
        IEnumerator BuildSector(System.Collections.Generic.List<MapFeature> features)
        {
            startupStage="WORLD";
            loading=true; worldLoadingStarted=Time.realtimeSinceStartup;
            loadingDeadline=WorldLoadingBudget.Deadline(worldLoadingStarted,worldLoadingStarted);
            SectorWorld.ResetWaterLevels();
            var job=world.GenerateAsync(features,(done,total)=>{status="BUILDING SECTOR " + done + " / " + total;});
            while(true)
            {
                if(Time.realtimeSinceStartup>loadingDeadline)
                {
                    LoadingFailed(new System.TimeoutException("WORLD_GENERATION_TIMEOUT"));
                    yield break;
                }
                bool more;
                try { more=job.MoveNext(); }
                catch(System.Exception error) { LoadingFailed(error); yield break; }
                // Generation deliberately yields while finishing roads, scenery and
                // navigation. Keep its progress alive without allowing endless loading.
                loadingDeadline=WorldLoadingBudget.Deadline(worldLoadingStarted,Time.realtimeSinceStartup);
                if(!more) break;
                yield return job.Current;
            }
            status="SPAWNING PLAYER...";
            startupStage="PLAYER";
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            yield return null;
            try 
            { 
                StartMission(); 
                if(ready) RememberSector();
            }
            catch(System.Exception error) { LoadingFailed(error); }
        }

        void LoadingFailed(System.Exception error)
        {
            EndDeploymentTracking("deployment_failed",DeploymentFailureReason(error,startupStage));
            if (pendingMultiplayerDuel) GeoSniper.Duel.SniperDuelNetwork.Instance.Disconnect("Duel map could not be prepared: " + error.Message);
            StopAllCoroutines();
            Input.location.Stop();
            Debug.LogError("Sector startup failed at " + startupStage + ": " + error);
            loading = false; ready = false; loadFailed = true;
            status = "DEPLOYMENT FAILED";
            mapNotice = startupStage == "MAP DOWNLOAD" ? "The map could not be downloaded. Check your connection and retry."
                : "The selected sector could not be prepared. Please retry.";
            if (mission != null) { mission.enabled = false; Destroy(mission); mission = null; }
            if (streamer != null) { Destroy(streamer); streamer = null; }
            if (world != null) { world.gameObject.SetActive(false); Destroy(world.gameObject); world = null; }
            cachedSectorReady=false;
            SwitchTab(LobbyTab.Home);
            if (armoryRoom != null) armoryRoom.SetActive(true);
        }

        void RetryDeployment()
        {
            if (isPvPDuel && pendingMultiplayerDuel)
            { DeployPvPDuel(pendingDuelHost, pendingDuelTarget, false, activeLocation); return; }
            if (geographicMap || activeLocation != null || LocationSelectionPolicy.RequiresLiveFix(PlayerPrefs.GetString("GeoSniper.LocationSource", "")))
                StartPreferredLocationMission(false);
            else StartOffline(true);
        }

        void DeployBundledRecovery()
        {
            if(pendingMultiplayerDuel)return;
            // Replay the selected contract rather than silently switching to free practice.
            campaignBriefingReviewed=stageIndexToStart>=0;
            StartOffline(true);
        }

        void StartOffline(bool preserveContract=false)
        {
            bool reuse=!pendingMultiplayerDuel && world!=null && cachedSectorReady && cachedSectorOffline && world.SpawnPoints.Count>0;
            if(!preserveContract) {campaignNodeToStart=-1; stageIndexToStart=-1; isPvPDuel=false; rangeToStart=false;}
            BeginDeploymentTracking("offline");
            if(armoryRoom!=null) armoryRoom.SetActive(false);
            loading=true;startupStage="WORLD";loadingDeadline=Time.realtimeSinceStartup+45;
            StopAllCoroutines(); Input.location.Stop();
            activeLocation = null; geographicMap = false; ready = false; loadFailed = false; choosingLocation = false;
            mapNotice = "OFFLINE PRACTICE";
            if (mission != null) { Destroy(mission); mission = null; }
            if (streamer != null) { streamer.StopAndPreserveFirstSector(); Destroy(streamer); streamer = null; }
            if(reuse)
            {
                status="PREPARING MISSION...";
                StartCoroutine(ResumeLoadedSector());
                return;
            }
            DiscardCachedSector();
            world = new GameObject("Offline Sector").AddComponent<SectorWorld>();
            world.SourceLabel = "OFFLINE PRACTICE";
            SectorElevation.LastNotice = "Offline practice - level terrain";
            StartCoroutine(BuildSector(preserveContract && stageIndexToStart>=0 && stageIndexToStart<3?SectorMap.Training():SectorMap.Offline()));
        }
        void Update()
        {
            EnsureMainCamera();
            bool lobbyActive=!loading && !ready;
            if(lobbyActive) StartLobbyMusic();
            else if(lobbyMusic!=null && lobbyMusic.isPlaying) { lobbyMusic.Stop(); StopBriefingAudio(); }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (currentTab != LobbyTab.Home) SwitchTab(LobbyTab.Home);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.BackQuote))
            {
                IsDebugMode = !IsDebugMode;
            }
#endif
            if(loading && Time.realtimeSinceStartup>loadingDeadline)
            {
                StopAllCoroutines(); Input.location.Stop();
                LoadingFailed(new System.TimeoutException("LOADING_TIMEOUT"));
            }
            if(ready) return;
            if(view!=null)
            {
                if (loading || ready)
                {
                    if (armoryRoom != null && armoryRoom.activeSelf) armoryRoom.SetActive(false);
                    if (sectorSun != null) sectorSun.enabled = true;
                    RenderSettings.fog = true;
                    view.clearFlags = CameraClearFlags.Depth;
                    view.fieldOfView = 65f;
                    view.transform.position=Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-distance);
                    view.transform.LookAt(Vector3.zero);
                }
                else
                {
                    RenderSettings.fog = false;
                    view.clearFlags = CameraClearFlags.SolidColor;
                    view.backgroundColor = new Color(0.035f, 0.05f, 0.08f);
                    if (world != null && world.gameObject.activeSelf) world.gameObject.SetActive(false);
                    if (armoryRoom != null)
                    {
                        if (!armoryRoom.activeSelf) armoryRoom.SetActive(true);
                        if (sectorSun != null) sectorSun.enabled = false;
                        FrameLobbyOperator();
                        UpdateLobbyOperative();
                    }
                }
            }
        }

        void OnLobbyOpen()
        {
            // Native ads and banners permanently removed from lobby & footer
            AdManager.Instance.RemoveAllLobbyAds();
        }

        void StartLobbyMusic()
        {
            if(lobbyMusic==null)
            {
                lobbyMusic=gameObject.AddComponent<AudioSource>();
                lobbyMusic.playOnAwake=false; lobbyMusic.loop=true; lobbyMusic.spatialBlend=0f;
                lobbyMusic.ignoreListenerPause=false; lobbyMusic.volume=.42f;
            }
            if(lobbyMusicClip==null)
                lobbyMusicClip=Resources.Load<AudioClip>("music/A_Single_Point") ?? Resources.Load<AudioClip>("A_Single_Point") ?? ProceduralAudio.CreateAmbientMusic();
            lobbyMusic.volume=(StoryNarrator.Playing(gameObject)?.1f:.42f)*Mathf.Clamp01(PlayerPrefs.GetFloat("GeoSniper.MasterVolume",1f));
            if(lobbyMusic.clip!=lobbyMusicClip) lobbyMusic.clip=lobbyMusicClip;
            if(lobbyMusicClip!=null && Application.isFocused && !lobbyMusic.isPlaying) lobbyMusic.Play();
        }

        Texture2D lobbyBgTex;
        Texture2D weaponCardTex;
        Texture2D playerAvatarTex;
        Texture2D eventCampaignTex;
        Texture2D barrettCardTex;
        Texture2D m24CardTex;
        Texture2D mk12CardTex;

        bool showingModeSelector;
        bool showingRewards;

        void EnsureLobbyTextures()
        {
            if (lobbyBgTex == null) lobbyBgTex = CommandGUI.LoadTexture("Textures/LobbyBackground");
            if (weaponCardTex == null) weaponCardTex = CommandGUI.LoadTexture("Textures/WeaponCard");
            if (playerAvatarTex == null) playerAvatarTex = CommandGUI.LoadTexture("Textures/PlayerAvatar");
            if (eventCampaignTex == null) eventCampaignTex = CommandGUI.LoadTexture("Textures/EventCampaign");
            if (barrettCardTex == null) barrettCardTex = CommandGUI.LoadTexture("Textures/Barrett50Card");
            if (m24CardTex == null) m24CardTex = CommandGUI.LoadTexture("Textures/M24Card");
            if (mk12CardTex == null) mk12CardTex = CommandGUI.LoadTexture("Textures/MK12Card");
        }

        void OnGUI()
        {
            if (FindAnyObjectByType<LobbyScreenshotAutomation>() == null)
            {
                gameObject.AddComponent<LobbyScreenshotAutomation>();
            }
            var previous = GUI.matrix;
            bool previousEnabled = GUI.enabled;
            try
            {
                GUI.enabled = previousEnabled && !GeoSniper.Duel.SniperDuelHUD.LobbyModalOpen;
                DrawLobbyGUI();
            }
            finally { GUI.matrix = previous; GUI.enabled = previousEnabled; }
        }

        void DrawLobbyGUI()
        {
            if (ready) return;
            if(ReleaseConfiguration.Current.analyticsEnabled && !GameAnalyticsManager.HasConsentChoice) GameAnalyticsManager.SetAnalyticsConsent(true);
            if(showingPrivacy || showingLocationConsent) {DrawPrivacyPanel();return;}
            if(CustomizingTouchControls)
            {
                Rect controlSafe = Screen.safeArea;
                float controlScale = CommandGUI.GetCanvasScale(controlSafe, out float controlWidth, out float controlHeight);
                GUI.matrix = Matrix4x4.TRS(new Vector3(controlSafe.x, Screen.height-controlSafe.yMax, 0),
                    Quaternion.identity, new Vector3(controlScale,controlScale,1));
                DrawTouchControlLayout(controlWidth,controlHeight);
                return;
            }
            if (choosingLocation) currentTab = LobbyTab.Location;
            if (choosingStage) currentTab = LobbyTab.Campaign;
            if (showingArmory) currentTab = LobbyTab.Armory;
            if (showingRewards) currentTab = LobbyTab.Rewards;
            if (showingSettings) currentTab = LobbyTab.Settings;
            bool pageEnabled=GUI.enabled;
            GUI.enabled=pageEnabled && !EconomyModalOpen;
            if (!loading && !showingModeSelector && currentTab == LobbyTab.Home) DrawMainMenu(0,0);
            else DrawCommandPages();
            GUI.enabled=pageEnabled;
            DrawEconomyModal();
        }

        void DrawModeSelectorModal(float w, float h)
        {
            CommandGUI.DrawPanel(new Rect(0, 0, w, h), CommandGUI.ThemeCard, Color.clear, 0);

            float pW = Mathf.Min(820f, w - 24f);
            float pH = Mathf.Min(440f, h - 24f);
            Rect panel = new Rect((w - pW) / 2f, (h - pH) / 2f, pW, pH);
            CommandGUI.DrawPanel(panel, CommandGUI.ThemeCard, CommandGUI.AccentCyan, 2f);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = CommandGUI.AccentCyan }
            };
            GUI.Label(new Rect(panel.x + 18, panel.y + 12, 320, 26), "SELECT GAME MODE", titleStyle);

            if (CommandGUI.DrawButton(new Rect(panel.xMax - 90, panel.y + 10, 75, 28), " CLOSE", false, 11))
            {
                showingModeSelector = false;
            }

            int selectedMode = Mathf.Clamp(PlayerPrefs.GetInt("GeoSniper.SelectedMode", 0), 0, 3);

            float cardY = panel.y + 48f;
            float cardH = 82f;
            float spacing = 8f;

            string[] modeNames = { "1. CAMPAIGN", "2. REAL-WORLD GPS OPS", "3. GPS RANGE", "4. 1v1 MULTIPLAYER DUEL" };
            string[] modeBadges = { "CAMPAIGN", "SATELLITE", "LIVE RANGE", "1v1 ONLINE" };
            string[] modeDescs = {
                "Infiltrate urban districts across tactical contracts. Complete objectives, eliminate targets, and earn stars.",
                "Generate real 3D OSM buildings at your live GPS coordinates or search any global city in the world.",
                "Train with realistic ballistics, zeroing, wind adjustment, and moving targets in your live sector.",
                "Host a room or join a friend's code. Both players fight on the host's chosen map. Solo practice is available inside."
            };

            for (int i = 0; i < 4; i++)
            {
                bool isCur = selectedMode == i;
                Rect r = new Rect(panel.x + 16, cardY + i * (cardH + spacing), panel.width - 32, cardH);
                CommandGUI.DrawPanel(r, isCur ? CommandGUI.ThemeCard : CommandGUI.ThemeCard, isCur ? CommandGUI.AccentCyan : CommandGUI.ThemeBorder, isCur ? 2f : 1f);

                CommandGUI.DrawBadge(new Rect(r.x + 14, r.y + 10, 80, 16), modeBadges[i], isCur ? CommandGUI.AccentCyan : CommandGUI.AccentGold, 9);
                GUI.Label(new Rect(r.x + 102, r.y + 8, 300, 20), modeNames[i], new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } });
                GUI.Label(new Rect(r.x + 14, r.y + 30, r.width - 170, 44), modeDescs[i], new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, normal = { textColor = CommandGUI.Muted } });

                Rect actionBtn = new Rect(r.xMax - 130, r.y + 22, 116, 38);
                string action = i == 0 ? "OPEN CAMPAIGN" : i == 3 ? "OPEN 1v1 ROOM" : isCur ? "USE MODE" : "SELECT";
                if (CommandGUI.DrawButton(actionBtn, action, isCur, 11) || CommandGUI.IsClicked(r))
                {
                    SelectLobbyMode(i);
                }
            }
        }

        void DrawRewardsModal(float w, float h) => DrawEconomyPage(w,h,false);

        void DrawArmoryModal(float w, float h) => DrawEconomyPage(w,h,true);

        void DrawSettingsModal(float w, float h)
        {
            if (showingAimSettings) { DrawAimSettings(w,h); return; }
            if (showingGraphicsSettings) { DrawGraphicsSettings(w,h); return; }
            CommandGUI.DrawPanel(new Rect(0, 0, w, h), CommandGUI.ThemeCard, Color.clear, 0);

            float pW = Mathf.Min(480f, w - 40f);
            float pH = Mathf.Min(580f, h - 20f);
            Rect panel = new Rect((w - pW) / 2f, (h - pH) / 2f, pW, pH);
            CommandGUI.DrawPanel(panel, CommandGUI.ThemeCard, CommandGUI.AccentCyan, 2f);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = CommandGUI.AccentCyan }
            };
            GUI.Label(new Rect(panel.x, panel.y + 16, panel.width, 26), "SETTINGS", titleStyle);

            var lblStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };

            // Master volume
            GUI.Label(new Rect(panel.x + 30, panel.y + 56, 140, 24), "MASTER AUDIO", lblStyle);
            AudioListener.volume = CommandGUI.HorizontalSlider(new Rect(panel.x + 180, panel.y + 62, panel.width - 220, 20), AudioListener.volume, 0f, 1f);
            PlayerPrefs.SetFloat("GeoSniper.MasterVolume",AudioListener.volume);

            // Independent look and scoped sensitivity; existing players keep their current feel.
            float sens = PlayerPrefs.GetFloat("GeoSniper.LookSens", 1.0f);
            GUI.Label(new Rect(panel.x + 30, panel.y + 90, 140, 24), $"LOOK SPEED: {sens:0.0}", lblStyle);
            sens = CommandGUI.HorizontalSlider(new Rect(panel.x + 180, panel.y + 96, panel.width - 220, 20), sens, 0.3f, 2.5f);
            PlayerPrefs.SetFloat("GeoSniper.LookSens", sens);

            float scopeSens=PlayerPrefs.GetFloat("GeoSniper.ScopeSens",sens);
            GUI.Label(new Rect(panel.x+30,panel.y+118,140,24), $"SCOPE SPEED: {scopeSens:0.0}",lblStyle);
            scopeSens=CommandGUI.HorizontalSlider(new Rect(panel.x+180,panel.y+124,panel.width-220,20),scopeSens,.3f,2.5f);
            PlayerPrefs.SetFloat("GeoSniper.ScopeSens",scopeSens);

            // Target Framerate
            GUI.Label(new Rect(panel.x + 30, panel.y + 148, 140, 24), "TARGET FPS", lblStyle);
            int fps = Application.targetFrameRate;
            string fpsText = fps > 30 ? "60 FPS [HIGH]" : "30 FPS [BATTERY]";
            if (CommandGUI.DrawButton(new Rect(panel.x + 180, panel.y + 143, 160, 32), fpsText, fps > 30, 11))
            {
                MobileGraphics.SetFrameRate(fps > 30 ? 30 : 60);
            }

            GUI.Label(new Rect(panel.x + 30, panel.y + 194, 140, 24), "3D TERRAIN", lblStyle);
            string elevText = enableElevationForGameplay ? "REAL 3D DEM [ON]" : "FLAT LEVEL [OFF]";
            if (CommandGUI.DrawButton(new Rect(panel.x + 180, panel.y + 189, 160, 32), elevText, enableElevationForGameplay, 11))
            {
                enableElevationForGameplay = !enableElevationForGameplay;
                PlayerPrefs.SetInt("GeoSniper.RealElevation", enableElevationForGameplay ? 1 : 0);
            }

            GUI.Label(new Rect(panel.x+30,panel.y+238,140,24),"DIFFICULTY",lblStyle);
            if(CommandGUI.DrawButton(new Rect(panel.x+180,panel.y+233,160,32),DifficultyProfile.Selected.ToString().ToUpperInvariant(),true,11))
                DifficultyProfile.Selected=(DifficultyLevel)(((int)DifficultyProfile.Selected+1)%3);

            if(CommandGUI.DrawButton(new Rect(panel.x+30,panel.y+278,panel.width-60,32),"PRIVACY, SUPPORT & CREDITS",false,11))
                showingPrivacy=true;

            if(ReleaseConfiguration.Current.adsEnabled && CommandGUI.DrawButton(new Rect(panel.x+30,panel.y+316,panel.width-60,32),"AD PRIVACY CHOICES",false,11))
                AdManager.Instance.ShowPrivacyOptions();

            if(CommandGUI.DrawButton(new Rect(panel.x+30,panel.y+358,panel.width-60,48),"TOUCH & GYRO AIMING",true,14))
                showingAimSettings=true;

            if(CommandGUI.DrawButton(new Rect(panel.x+30,panel.y+416,panel.width-60,48),"GRAPHICS & PERFORMANCE",false,14))
                showingGraphicsSettings=true;

            if(CommandGUI.DrawButton(new Rect(panel.x+30,panel.y+474,panel.width-60,32),
                CombatHudPreferences.TacticalDetails ? "COMBAT HUD: DETAILED" : "COMBAT HUD: CLEAN",
                CombatHudPreferences.TacticalDetails,12))
                CombatHudPreferences.TacticalDetails=!CombatHudPreferences.TacticalDetails;

            if (CommandGUI.DrawButton(new Rect(panel.x + (panel.width - 160) / 2f, panel.yMax - 50, 160, 38), "SAVE & CLOSE", true, 13))
            {
                PlayerPrefs.Save();
                SwitchTab(LobbyTab.Home);
            }
        }

        void DrawLoadingScreen(float w, float h)
        {
            CommandGUI.DrawPanel(new Rect(0, 0, w, h), CommandGUI.ThemeCard, Color.clear, 0);

            float pW = 480, pH = 280;
            Rect centerPanel = new Rect((w - pW) / 2f, (h - pH) / 2f, pW, pH);
            CommandGUI.DrawPanel(centerPanel, CommandGUI.ThemeCard, CommandGUI.AccentCyan, 2f);

            // Radar Scanner Visual
            float radarSize = 70;
            Vector2 radarCenter = new Vector2(centerPanel.x + 50, centerPanel.y + 60);
            CommandGUI.DrawCircle(new Rect(radarCenter.x - radarSize / 2f, radarCenter.y - radarSize / 2f, radarSize, radarSize), new Color(.94f,.68f,.30f,.16f));

            float angle = (Time.realtimeSinceStartup * 180f) % 360f;
            Vector2 scanDir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * (radarSize / 2f);
            GUI.color = CommandGUI.AccentCyan;
            GUI.DrawTexture(new Rect(radarCenter.x, radarCenter.y, scanDir.x, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Loading Status text
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = CommandGUI.AccentCyan }
            };
            GUI.Label(new Rect(centerPanel.x + 100, centerPanel.y + 30, centerPanel.width - 120, 30), "INITIALIZING SECTOR...", titleStyle);

            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(centerPanel.x + 30, centerPanel.y + 95, centerPanel.width - 60, 50), status, statusStyle);

            // Stage indicator; reflects preparation steps rather than download percentage.
            float progress = 0.1f;
            if (startupStage == "MAP DOWNLOAD") progress = 0.35f;
            else if (startupStage == "WORLD") progress = 0.75f;
            else if (startupStage == "PLAYER") progress = 0.95f;

            CommandGUI.DrawProgressBar(new Rect(centerPanel.x + 30, centerPanel.y + 155, centerPanel.width - 60, 20), progress, CommandGUI.AccentCyan, CommandGUI.ThemeCard);

            GUI.Label(new Rect(centerPanel.x+30,centerPanel.y+180,centerPanel.width-60,24),startupStage+(deploymentTracked?" / "+Mathf.FloorToInt(Time.realtimeSinceStartup-deploymentStartedAt)+"s elapsed":""),statusStyle);
            if (CommandGUI.DrawButton(new Rect(centerPanel.x + 90, centerPanel.yMax - 55, centerPanel.width - 180, 40), pendingMultiplayerDuel ? "CANCEL DUEL & RETURN" : "SWITCH TO OFFLINE OPS", false, 13))
            {
                if (pendingMultiplayerDuel)
                {
                    StopAllCoroutines(); Input.location.Stop();
                    GeoSniper.Duel.SniperDuelNetwork.Instance.Disconnect("Duel loading cancelled.");
                    pendingMultiplayerDuel = false; isPvPDuel = false; duelLocationSelection = false;
                    loading = ready = false;
                    DiscardCachedSector();
                    SwitchTab(LobbyTab.Home);
                    GeoSniper.Duel.SniperDuelHUD.Instance.ShowLobbyModal = true;
                }
                else StartOffline(true);
            }
        }

        int GetDefaultSelectedNodeId()
        {
            for (int i = 0; i < CampaignNodeGraph.Nodes.Length; i++)
            {
                int id = CampaignNodeGraph.Nodes[i].id;
                if (CampaignNodeGraph.IsNodeUnlocked(id) && !CampaignNodeGraph.IsNodeCompleted(id))
                {
                    return id;
                }
            }
            return 0;
        }

        public void ReturnToLobby(int justCompletedNodeId = -1) => ReturnToLevelMap(justCompletedNodeId);
        public void ReturnToLevelMap(int justCompletedNodeId = -1)
        {
            ClearDuelDeploymentIntent();
            if (mission != null && mission.MultiplayerDuel)
            { activeLocation = null; cachedSectorReady = false; }
            if (mission != null)
            {
                mission.enabled = false;
                Destroy(mission);
                mission = null;
            }
            if (streamer != null)
            {
                streamer.StopAndPreserveFirstSector();
                Destroy(streamer);
                streamer = null;
            }
            if (world != null)
            {
                world.gameObject.SetActive(false);
            }
            ready = false;
            loading = false;

            if (PlayerPrefs.GetInt("GeoSniper.OpenArmoryOnExit", 0) == 1)
            {
                PlayerPrefs.DeleteKey("GeoSniper.OpenArmoryOnExit");
                PlayerPrefs.Save();
                SwitchTab(LobbyTab.Armory);
                OnLobbyOpen();
            }
            else
            {
                SwitchTab(LobbyTab.Campaign);
                OnLobbyOpen();
            }

            int targetNode = -1;
            if (justCompletedNodeId >= 0)
            {
                var prevNode = CampaignNodeGraph.GetNode(justCompletedNodeId);
                if (prevNode != null && prevNode.nextNodeIds != null)
                {
                    foreach (int nid in prevNode.nextNodeIds)
                    {
                        if (CampaignNodeGraph.IsNodeUnlocked(nid) && !CampaignNodeGraph.IsNodeCompleted(nid))
                        {
                            targetNode = nid;
                            break;
                        }
                    }
                }
            }
            if (targetNode < 0) targetNode = GetDefaultSelectedNodeId();
            selectedNodeId = targetNode;
            if(currentTab==LobbyTab.Campaign)PlayBriefingAudio(selectedNodeId,true);

            var selNode = CampaignNodeGraph.GetNode(selectedNodeId);
            if (selNode != null)
            {
                float spacingY = 110f;  // matches DrawStageSelector
                float startY = 56f;
                stageScroll.y = Mathf.Max(0, (startY + selNode.gridPos.y * spacingY) - 160f);
            }
        }

        void DrawStageSelector(float w, float h)
        {
            DrawCampaignScreen(w, h);
        }
        void DrawLocationPicker(float w,float h)
        {
            float width=Mathf.Min(880,w-40);var panel=new Rect((w-width)/2,18,width,h-38);
            CommandGUI.Fill(panel,CommandGUI.ThemeCard);
            var body=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true,normal={textColor=CommandGUI.Text}};
            var small=new GUIStyle(body){fontSize=13,normal={textColor=CommandGUI.Muted}};
            GUI.Label(new Rect(panel.x+24,panel.y+20,width-190,38),duelLocationSelection?"DUEL LOCATION":"CHOOSE A LOCATION",new GUIStyle(body){fontSize=23,fontStyle=FontStyle.Bold});
            if(CommandGUI.DrawButton(new Rect(panel.xMax-134,panel.y+18,110,36),"BACK",false,13))
            {
                if(campaignLocationEdit){campaignLocationEdit=false;SwitchTab(LobbyTab.Campaign);}
                else {SwitchTab(LobbyTab.Home);if(duelLocationSelection){duelLocationSelection=false;GeoSniper.Duel.SniperDuelHUD.Instance.ShowLobbyModal=true;}}
            }
            GUI.Label(new Rect(panel.x+24,panel.y+70,width-48,36),"GPS is optional. Map providers receive the requested area and your IP address. No background location is used. You can search a place instead.",small);
            if(CommandGUI.DrawButton(new Rect(panel.x+24,panel.y+115,width-48,46),"USE MY LIVE LOCATION",false,14))
            {
                if(campaignLocationEdit && !campaignBriefingReviewed)
                {PlayerPrefs.SetString("GeoSniper.LocationSource","gps");PlayerPrefs.Save();activeLocation=null;campaignLocationEdit=false;SwitchTab(LobbyTab.Campaign);}
                else if(campaignLocationEdit && campaignBriefingReviewed){resumeMissionAfterLocationConsent=false;StartCoroutine(Play(null,true));}
                else {resumeMissionAfterLocationConsent=campaignLocationEdit || resumeMissionAfterLocationConsent;showingLocationConsent=true;}
            }
            GUI.Label(new Rect(panel.x+24,panel.y+180,width-48,22),"OR SEARCH A CITY, NEIGHBOURHOOD OR LANDMARK",small);
            GUI.SetNextControlName("WorldMapPlaceSearch");
            string query=GUI.TextField(new Rect(panel.x+24,panel.y+210,width-180,44),locationSearch,120,new GUIStyle(GUI.skin.textField){fontSize=17,alignment=TextAnchor.MiddleLeft});
            if(query!=locationSearch)locationSearch=query;
            if(CommandGUI.DrawButton(new Rect(panel.xMax-146,panel.y+210,122,44),"SEARCH",false,13) && !searching)StartCoroutine(SearchPlaces());
            float listHeight=Mathf.Max(70,panel.height-335);var matches=locations ?? new System.Collections.Generic.List<GameLocation>();
            locationScroll=GUI.BeginScrollView(new Rect(panel.x+24,panel.y+272,width-48,listHeight),locationScroll,new Rect(0,0,width-70,Mathf.Max(listHeight,matches.Count*52)));
            GameLocation chosen=null;
            if(matches.Count==0)GUI.Label(new Rect(8,8,width-100,44),searching?"Searching...":"Search results appear here.",small);
            for(int i=0;i<matches.Count;i++)if(CommandGUI.DrawButton(new Rect(0,i*52,width-72,46),matches[i].Label,false,14))chosen=matches[i];
            GUI.EndScrollView();
            GUI.Label(new Rect(panel.x+24,panel.yMax-51,width-48,42),locationError,small);
            if(chosen!=null)
            {
                if(duelLocationSelection)DeployPvPDuel(true,hostLocation:chosen);
                else if(campaignLocationEdit)SelectCampaignPlace(chosen);
                else if(resumeMissionAfterLocationConsent){resumeMissionAfterLocationConsent=false;StartCoroutine(Play(chosen));}
                else {rangeToStart=false;campaignNodeToStart=-1;stageIndexToStart=-1;isPvPDuel=false;StartCoroutine(Play(chosen));}
            }
        }
        IEnumerator SearchPlaces()
        {
            string query=locationSearch.Trim();
            if(query.Length<3) { locationError="Enter at least three characters and include the city for better results."; yield break; }
            searching=true; nextSearch=Time.realtimeSinceStartup+2;
            if (locations == null) locations = new System.Collections.Generic.List<GameLocation>();
            locations.Clear(); locationError="Searching places...";
            using(var request=UnityEngine.Networking.UnityWebRequest.Get(placeSearchEndpoint+"?limit=10&q="+UnityEngine.Networking.UnityWebRequest.EscapeURL(query)))
            {
                request.timeout=12;
                yield return request.SendWebRequest();
                if(request.result!=UnityEngine.Networking.UnityWebRequest.Result.Success)
                    locationError="Search unavailable. Check your connection and try again.";
                else try
                {
                    var features=Newtonsoft.Json.Linq.JObject.Parse(request.downloadHandler.text)["features"];
                    var candidates = new System.Collections.Generic.List<System.Tuple<int, GameLocation>>();
                    foreach(var feature in features)
                    {
                        var coords=feature["geometry"]?["coordinates"] as Newtonsoft.Json.Linq.JArray;
                        if(coords==null || coords.Count<2) continue;
                        double lon=(double)coords[0], lat=(double)coords[1];
                        if(!LocationResolver.Valid(lat,lon)) continue;
                        var p=feature["properties"];
                        string type = ((string)p?["type"] ?? "").ToLowerInvariant();
                        string osmVal = ((string)p?["osm_value"] ?? "").ToLowerInvariant();
                        int priority = (type == "city" || type == "town" || type == "village" || type == "suburb" || type == "neighbourhood" || osmVal == "town" || osmVal == "city") ? 0
                            : (type == "county" || osmVal == "administrative") ? 3 : 1;
                        var names=new System.Collections.Generic.List<string>();
                        string nameStr = (string)p?["name"];
                        if(!string.IsNullOrWhiteSpace(nameStr))
                        {
                            if(priority == 3) nameStr += " (District)";
                            else if(priority == 0 && (type == "town" || osmVal == "town")) nameStr += " (Town)";
                            names.Add(nameStr);
                        }
                        foreach(string key in new[]{"city","state","country"})
                        { string value=(string)p?[key]; if(!string.IsNullOrWhiteSpace(value) && !names.Contains(value)) names.Add(value); }
                        if(names.Count>0) candidates.Add(System.Tuple.Create(priority, new GameLocation { Latitude=lat,Longitude=lon,Source="selected",Label=string.Join(", ",names) }));
                    }
                    candidates.Sort((a,b)=>a.Item1.CompareTo(b.Item1));
                    foreach(var c in candidates) locations.Add(c.Item2);
                    locationError=locations.Count==0?"No places found. Try adding the city or country.":"Tap a place to explore its sector.";
                }
                catch(System.Exception) { locationError="Could not read search results. Please try again."; }
            }
            searching=false;
        }
        void OnDisable() { StopBriefingAudio(); if(CustomizingTouchControls) EndTouchControlLayout(false); Application.lowMemory-=ReleaseLobbySectorOnLowMemory; StopAllCoroutines(); Input.location.Stop(); }
    }
}
