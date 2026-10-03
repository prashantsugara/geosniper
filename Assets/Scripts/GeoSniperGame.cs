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
            get => PlayerPrefs.GetInt("GeoSniper.DebugMode", 1) == 1;
            set => PlayerPrefs.SetInt("GeoSniper.DebugMode", value ? 1 : 0);
#else
            get => false;
            set { }
#endif
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) PlayerPrefs.Save();
        }
        float loadingDeadline;
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
        bool economyRewardOffer;
        string economyRewardType="";
        int economyRewardAmount;
        string economyNotice="";
        // A mission can request location from any lobby screen. Keep that intent while the
        // privacy prompt is visible so accepting location does not silently switch modes.
        bool resumeMissionAfterLocationConsent;

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
            if(tab==LobbyTab.Location && !ReleaseConfiguration.Current.liveMapsEnabled) tab=LobbyTab.Home;
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

        public void DeployPvPDuel(bool asHost, string roomCodeOrIp = "", bool isPracticeBot = false)
        {
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
                    GeoSniper.Duel.SniperDuelNetwork.Instance?.StartHost();
                }
                else if (!string.IsNullOrEmpty(roomCodeOrIp))
                {
                    GeoSniper.Duel.SniperDuelNetwork.Instance?.StartClient(roomCodeOrIp);
                }
            }

            // If an existing sector is already loaded in memory, reuse it immediately
            if (world != null && cachedSectorReady && world.SpawnPoints != null && world.SpawnPoints.Count > 0)
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

        void ExecuteDeploy(bool useLiveLocation=false)
        {
            loadFailed=false; status=""; mapNotice="";
            int sel = Mathf.Clamp(PlayerPrefs.GetInt("GeoSniper.SelectedMode",0),0,3);
            if (sel == 3)
            {
                DeployPvPDuel(false, "", true);
                return;
            }
            PrepareDeployment(sel);
            StartPreferredLocationMission(useLiveLocation);
        }

        void PrepareDeployment(int selectedMode)
        {
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
            GameLocation location=useLiveLocation?null:PreferredLocation();
            if(location!=null)
            {
                StartCoroutine(Play(location));
                return;
            }
            resumeMissionAfterLocationConsent=true;
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
                mission.BeginPvPDuel(campaignNodeToStart);
                if (pendingMultiplayerDuel && GeoSniper.Duel.SniperDuelManager.Instance != null)
                {
                    GeoSniper.Duel.SniperDuelManager.Instance.StartDuelMatch(mission, pendingDuelHost, pendingDuelTarget);
                    pendingMultiplayerDuel = false;
                }
            }
            else if (stageIndexToStart >= 0) mission.BeginStage(stageIndexToStart);
            else if (rangeToStart) mission.BeginRange();

            if (activeLocation != null && geographicMap && mission != null && mission.Player != null)
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
            ready = true;
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
            if (!PlayerPrefs.HasKey("GeoSniper.WeaponUnlocked_1"))
            {
                PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_1", 1);
                PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_0", 0);
                PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_2", 0);
                if (PlayerPrefs.GetInt("GeoSniper.SelectedWeapon", 0) == 0 && PlayerPrefs.GetInt("GeoSniper.WeaponUnlocked_0", 0) == 0 && !IsDebugMode)
                {
                    PlayerPrefs.SetInt("GeoSniper.SelectedWeapon", 1);
                }
                PlayerPrefs.Save();
            }
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
            keyLight.intensity = 1.05f;
            keyLight.color = new Color(1f, 0.96f, 0.92f);
            keyLight.transform.rotation = Quaternion.Euler(24f, -28f, 0f);

            // Rim / Edge Light for Silhouette (Crisp Daylight Rim, no blue blowout)
            var rimLightObj = new GameObject("LobbyRimLight");
            rimLightObj.transform.SetParent(armoryRoom.transform, false);
            var rimLight = rimLightObj.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.intensity = 0.50f;
            rimLight.color = new Color(0.85f, 0.88f, 0.92f);
            rimLight.transform.rotation = Quaternion.Euler(-15f, 155f, 0f);

            // Soft Fill Light from camera angle
            var fillLightObj = new GameObject("LobbyFillLight");
            fillLightObj.transform.SetParent(armoryRoom.transform, false);
            var fillLight = fillLightObj.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.35f;
            fillLight.color = new Color(0.75f, 0.76f, 0.80f);
            fillLight.transform.rotation = Quaternion.Euler(15f, 40f, 0f);

            // Ambient light for natural shadow fill
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.20f, 0.23f);

            // Staging Pedestal Platform
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "StagingPedestal";
            pedestal.transform.SetParent(armoryRoom.transform, false);
            pedestal.transform.position = new Vector3(0.05f, 0.02f, 3.1f);
            pedestal.transform.localScale = new Vector3(2.4f, 0.04f, 2.4f);
            var pedMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Diffuse"));
            pedMat.color = new Color(0.07f, 0.09f, 0.12f);
            if (pedMat.HasProperty("_Metallic")) pedMat.SetFloat("_Metallic", 0.75f);
            if (pedMat.HasProperty("_Glossiness")) pedMat.SetFloat("_Glossiness", 0.75f);
            pedestal.GetComponent<Renderer>().sharedMaterial = pedMat;
            var pedCol = pedestal.GetComponent<Collider>();
            if (pedCol != null) Destroy(pedCol);

            // Pedestal Glowing Rim Ring
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "PedestalRim";
            ring.transform.SetParent(pedestal.transform, false);
            ring.transform.localPosition = new Vector3(0, 0.48f, 0);
            ring.transform.localScale = new Vector3(1.02f, 0.15f, 1.02f);
            var ringMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            ringMat.color = new Color(0.35f, 0.25f, 0.11f);
            lobbyStageMaterials.Add(pedMat);
            lobbyStageMaterials.Add(ringMat);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;
            var ringCol = ring.GetComponent<Collider>();
            if (ringCol != null) Destroy(ringCol);

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
            if (root == null) return;
            var gunmetalShader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");
            var metalMat = new Material(gunmetalShader) { color = new Color(0.12f, 0.13f, 0.14f) };
            if (metalMat.HasProperty("_Metallic")) metalMat.SetFloat("_Metallic", 0.60f);
            if (metalMat.HasProperty("_Glossiness")) metalMat.SetFloat("_Glossiness", 0.40f);

            var polyMat = new Material(gunmetalShader) { color = new Color(0.15f, 0.16f, 0.15f) };
            if (polyMat.HasProperty("_Metallic")) polyMat.SetFloat("_Metallic", 0.0f);
            if (polyMat.HasProperty("_Glossiness")) polyMat.SetFloat("_Glossiness", 0.15f);

            var coatedOpticShader = Shader.Find("GeoSniper/CoatedOptic") ?? gunmetalShader;
            var glassMat = new Material(coatedOpticShader) { color = new Color(0.035f, 0.14f, 0.22f) };
            if (glassMat.HasProperty("_Metallic")) glassMat.SetFloat("_Metallic", 0.85f);
            if (glassMat.HasProperty("_Glossiness")) glassMat.SetFloat("_Glossiness", 0.98f);

            lobbyRifleMaterials.Add(metalMat); lobbyRifleMaterials.Add(polyMat); lobbyRifleMaterials.Add(glassMat);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                string n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("lens") || n.Contains("glass") || n.Contains("optic"))
                    r.sharedMaterial = glassMat;
                else if (r.sharedMaterial != null && r.sharedMaterial.mainTexture != null)
                    continue;
                else if (n.Contains("stock") || n.Contains("grip") || n.Contains("pad") || n.Contains("handle"))
                    r.sharedMaterial = polyMat;
                else
                    r.sharedMaterial = metalMat;
            }
        }

        void UpdateLobbyRifle()
        {
            int curWpn = PlayerPrefs.GetInt("GeoSniper.SelectedWeapon", 1);
            if (curWpn == lastLobbyWeapon && lobbyRifle != null) return;
            lastLobbyWeapon = curWpn;

            if (lobbyRifle != null) { Destroy(lobbyRifle); lobbyRifle = null; }
            foreach (var material in lobbyRifleMaterials) if (material != null) Destroy(material);
            lobbyRifleMaterials.Clear();
            if (armoryRoom == null) return;

            string[] riflePaths = { "Models/Weapons/Barrett50", "Models/Weapons/M24Tactical", "Models/Weapons/MK12SPR" };
            if (curWpn < 0 || curWpn >= riflePaths.Length) curWpn = 1;
            var prefab = Resources.Load<GameObject>(riflePaths[curWpn]);
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
            if(!ReleaseConfiguration.Current.liveMapsEnabled) {SwitchTab(LobbyTab.Home);yield break;}
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
                status="PREPARING MISSION...";
                startupStage="PLAYER";
                yield return ResumeLoadedSector();
                yield break;
            }
            DiscardCachedSector();

            startupStage="MAP DOWNLOAD";
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
            loading=true; loadingDeadline=Time.realtimeSinceStartup+120;
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
            if (geographicMap || activeLocation != null || LocationSelectionPolicy.RequiresLiveFix(PlayerPrefs.GetString("GeoSniper.LocationSource", "")))
                StartPreferredLocationMission(false);
            else StartOffline(true);
        }

        void StartOffline(bool preserveContract=false)
        {
            bool reuse=world!=null && cachedSectorReady && cachedSectorOffline && world.SpawnPoints.Count>0;
            if(!preserveContract) {campaignNodeToStart=-1; stageIndexToStart=-1; isPvPDuel=false; rangeToStart=false;}
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
            StartCoroutine(BuildSector(SectorMap.Offline()));
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
            lobbyMusic.volume=.42f*Mathf.Clamp01(PlayerPrefs.GetFloat("GeoSniper.MasterVolume",1f));
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
            try { DrawLobbyGUI(); }
            finally { GUI.matrix = previous; }
        }

        void DrawLobbyGUI()
        {
            if (ready) return;
            if(ReleaseConfiguration.Current.analyticsEnabled && !GameAnalyticsManager.HasConsentChoice) GameAnalyticsManager.SetAnalyticsConsent(true);
            if(showingPrivacy || showingLocationConsent) {DrawPrivacyPanel();return;}
            if (choosingLocation) currentTab = LobbyTab.Location;
            if (choosingStage) currentTab = LobbyTab.Campaign;
            if (showingArmory) currentTab = LobbyTab.Armory;
            if (showingRewards) currentTab = LobbyTab.Rewards;
            if (showingSettings) currentTab = LobbyTab.Settings;
            if (!loading && !showingModeSelector && currentTab == LobbyTab.Home) DrawMainMenu(0,0);
            else DrawCommandPages();
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

            int selectedMode = PlayerPrefs.GetInt("GeoSniper.SelectedMode", 0);

            float cardY = panel.y + 48f;
            float cardH = 82f;
            float spacing = 8f;

            string[] modeNames = { "1. CAMPAIGN (STORY GPS)", "2. REAL-WORLD GPS OPS", "3. GPS RANGE", "4. SNIPER DUEL / AI RIVAL" };
            string[] modeBadges = { "STORY GPS", "SATELLITE", "LIVE RANGE", "1v1 GPS" };
            string[] modeDescs = {
                "Infiltrate urban districts across tactical contracts. Complete objectives, eliminate targets, and earn stars.",
                "Generate real 3D OSM buildings at your live GPS coordinates or search any global city in the world.",
                "Train with realistic ballistics, zeroing, wind adjustment, and moving targets in your live sector.",
                "High-stakes rooftop duel against a rival sniper marksman. Locate and neutralize before they spot you."
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
                if (isCur)
                {
                    CommandGUI.DrawBadge(actionBtn, " ACTIVE MODE", CommandGUI.AccentGold, 11);
                }
                else
                {
                    if (CommandGUI.DrawButton(actionBtn, "SELECT", true, 12) || CommandGUI.IsClicked(r))
                    {
                        PlayerPrefs.SetInt("GeoSniper.SelectedMode", i);
                        PlayerPrefs.Save();
                        showingModeSelector = false;
                    }
                }
            }
        }

        void DrawRewardsModal(float w, float h)
        {
            CommandGUI.DrawPanel(new Rect(0, 0, w, h), CommandGUI.ThemeCard, Color.clear, 0);

            float pW = Mathf.Min(520f, w - 24f);
            float pH = Mathf.Min(540f, h - 65f);
            Rect panel = new Rect((w - pW) / 2f, 10f, pW, pH);
            CommandGUI.DrawPanel(panel, CommandGUI.ThemeCard, CommandGUI.AccentGold, 2f);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = CommandGUI.AccentGold }
            };
            GUI.Label(new Rect(panel.x + 18, panel.y + 12, 320, 26), "TACTICAL REWARDS & VAULT", titleStyle);

            if (CommandGUI.DrawButton(new Rect(panel.xMax - 95, panel.y + 10, 80, 34), " CLOSE", false, 12))
            {
                SwitchTab(LobbyTab.Home);
            }

            int credits = PlayerPrefs.GetInt("GeoSniper.Credits", 0);
            int gold = PlayerPrefs.GetInt("GeoSniper.Gold", 0);
            int bountyStash = PlayerPrefs.GetInt("GeoSniper.BountyStash", 0);
            string todayStr = System.DateTime.UtcNow.ToString("yyyyMMdd");
            bool airdropReady = PlayerPrefs.GetString("GeoSniper.LastAirdropDate", "") != todayStr;
            int adClaimsToday = PlayerPrefs.GetString("GeoSniper.LastRewardAdDate", "") == todayStr ? PlayerPrefs.GetInt("GeoSniper.RewardAdsToday", 0) : 0;
            bool rewardAdReady = adClaimsToday < 3;

            // 1. BOUNTY VAULT CARD
            Rect vaultCard = new Rect(panel.x + 16, panel.y + 46, panel.width - 32, 88);
            CommandGUI.DrawPanel(vaultCard, CommandGUI.ThemeCard, CommandGUI.AccentGold, 1.2f);
            CommandGUI.DrawBadge(new Rect(vaultCard.x + 12, vaultCard.y + 8, 106, 16), "HEADSHOT VAULT", CommandGUI.AccentGold, 9);
            GUI.Label(new Rect(vaultCard.x + 12, vaultCard.y + 26, 220, 22), "$" + bountyStash.ToString("N0"), new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = CommandGUI.AccentGold } });
            GUI.Label(new Rect(vaultCard.x + 12, vaultCard.y + 50, vaultCard.width - 150, 34), "Earned from precision headshots in combat missions. Tap claim to deposit into bank.", new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, normal = { textColor = Color.white } });

            Rect claimVaultBtn = new Rect(vaultCard.xMax - 130, vaultCard.y + 24, 116, 40);
            bool canClaimVault = bountyStash > 0;
            if (CommandGUI.DrawButton(claimVaultBtn, canClaimVault ? "CLAIM CASH" : "EMPTY", canClaimVault, 11))
            {
                if (canClaimVault)
                {
                    PlayerPrefs.SetInt("GeoSniper.Credits", credits + bountyStash);
                    PlayerPrefs.SetInt("GeoSniper.BountyStash", 0);
                    PlayerPrefs.Save();
                }
            }

            // 2. DAILY SUPPLY DROP CARD
            Rect dropCard = new Rect(panel.x + 16, panel.y + 140, panel.width - 32, 88);
            CommandGUI.DrawPanel(dropCard, CommandGUI.ThemeCard, CommandGUI.AccentCyan, 1.2f);
            CommandGUI.DrawBadge(new Rect(dropCard.x + 12, dropCard.y + 8, 116, 16), "DAILY SUPPLY DROP", CommandGUI.AccentCyan, 9);
            GUI.Label(new Rect(dropCard.x + 12, dropCard.y + 26, 220, 22), "+100 GOLD ", new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = CommandGUI.AccentCyan } });
            GUI.Label(new Rect(dropCard.x + 12, dropCard.y + 50, dropCard.width - 150, 34), "Airdropped daily tactical provisions. Resets every 24 hours.", new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, normal = { textColor = Color.white } });

            Rect claimDropBtn = new Rect(dropCard.xMax - 130, dropCard.y + 24, 116, 40);
            if (airdropReady)
            {
                if (CommandGUI.DrawGreenPlayButton(claimDropBtn, "CLAIM 100 "))
                {
                    PlayerPrefs.SetInt("GeoSniper.Gold", gold + 100);
                    PlayerPrefs.SetString("GeoSniper.LastAirdropDate", todayStr);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                CommandGUI.DrawBadge(claimDropBtn, "CLAIMED TODAY ", new Color(0.4f, 0.6f, 0.4f), 10);
            }

            // 3. SPONSORED TACTICAL AIRDROP CARD (Rewarded Ad)
            Rect adCard = new Rect(panel.x + 16, panel.y + 234, panel.width - 32, 88);
            CommandGUI.DrawPanel(adCard, CommandGUI.ThemeCard, new Color(0.85f, 0.45f, 1.0f), 1.2f);
            CommandGUI.DrawBadge(new Rect(adCard.x + 12, adCard.y + 8, 140, 16), " SPONSORED DROP", new Color(0.85f, 0.45f, 1.0f), 9);
            GUI.Label(new Rect(adCard.x + 12, adCard.y + 26, 220, 22), "+$1,500 VAULT CASH ", new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = CommandGUI.AccentGold } });
            GUI.Label(new Rect(adCard.x + 12, adCard.y + 50, adCard.width - 150, 34), rewardAdReady ? "Watch a sponsor transmission to claim +$1,500 cash. 3 claims per day." : "Daily sponsored cash limit reached. Return tomorrow.", new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, normal = { textColor = Color.white } });

            Rect watchAdBtn = new Rect(adCard.xMax - 130, adCard.y + 24, 116, 40);
            if (rewardAdReady && CommandGUI.DrawButton(watchAdBtn, " WATCH AD", true, 11))
            {
                AdManager.Instance.ShowRewardedAd((success) =>
                {
                    if (success)
                    {
                        int count=PlayerPrefs.GetString("GeoSniper.LastRewardAdDate", "") == todayStr ? PlayerPrefs.GetInt("GeoSniper.RewardAdsToday", 0) : 0;
                        if(count>=3) return;
                        PlayerPrefs.SetInt("GeoSniper.Credits", PlayerPrefs.GetInt("GeoSniper.Credits", 0) + 1500);
                        PlayerPrefs.SetString("GeoSniper.LastRewardAdDate", todayStr);
                        PlayerPrefs.SetInt("GeoSniper.RewardAdsToday", count+1);
                        PlayerPrefs.Save();
                    }
                }, "lobby_vault_sponsor");
            }
            else if(!rewardAdReady) CommandGUI.DrawBadge(watchAdBtn,"LIMIT REACHED",CommandGUI.Muted,10);

            // 4. GOOGLE PLAY STORE REVIEW & RATING CARD
            Rect rateCard = new Rect(panel.x + 16, panel.y + 328, panel.width - 32, 88);
            CommandGUI.DrawPanel(rateCard, CommandGUI.ThemeCard, CommandGUI.AccentGold, 1.2f);
            CommandGUI.DrawBadge(new Rect(rateCard.x + 12, rateCard.y + 8, 150, 16), "GOOGLE PLAY REVIEW", CommandGUI.AccentGold, 9);
            GUI.Label(new Rect(rateCard.x + 12, rateCard.y + 26, 220, 22), "SHARE YOUR FEEDBACK", new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = CommandGUI.AccentGold } });
            GUI.Label(new Rect(rateCard.x + 12, rateCard.y + 50, rateCard.width - 150, 34), "Leave an honest review on Google Play. Reviews are optional and never affect rewards.", new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, normal = { textColor = Color.white } });

            Rect rateBtn = new Rect(rateCard.xMax - 130, rateCard.y + 24, 116, 40);
            if (CommandGUI.DrawButton(rateBtn,"REVIEW",false,11))
            {
                AdManager.OpenPlayStoreRating();
            }

            if (CommandGUI.DrawButton(new Rect(panel.x + (panel.width - 180) / 2f, panel.yMax - 46, 180, 36), "BACK TO LOBBY", false, 12))
            {
                SwitchTab(LobbyTab.Home);
            }

            string[] tabIcons = { "", "", "", "", "" };
            string[] tabTitles = { "HOME", "CAMPAIGN", "ARMORY", "SECTOR", "VAULT" };
            int rTab = CommandGUI.DrawTabBar(new Rect(0, h - 56f, w, 56f), tabIcons, tabTitles, 4);
            if (rTab != 4) SwitchTab((LobbyTab)rTab);
        }

        void DrawArmoryModal(float w, float h)
        {
            EnsureLobbyTextures();
            CommandGUI.DrawPanel(new Rect(0, 0, w, h), CommandGUI.ThemeCard, Color.clear, 0);

            float pW = Mathf.Min(820f, w - 24f);
            float pH = Mathf.Min(560f, h - 70f);
            Rect panel = new Rect((w - pW) / 2f, 10f, pW, pH);
            CommandGUI.DrawPanel(panel, CommandGUI.ThemeCard, CommandGUI.AccentCyan, 2f);

            int credits = PlayerPrefs.GetInt("GeoSniper.Credits", 0);
            int gold = PlayerPrefs.GetInt("GeoSniper.Gold", 0);

            // Title Header
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = CommandGUI.AccentCyan }
            };
            GUI.Label(new Rect(panel.x + 18, panel.y + 12, 340, 26), "ARMORY / WEAPON UPGRADES", titleStyle);
            if (IsDebugMode)
            {
                CommandGUI.DrawBadge(new Rect(panel.x + 315, panel.y + 15, 150, 20), "DEBUG: ALL GUNS OPEN", CommandGUI.AccentGreen, 9);
            }

            // Header live balances
            Rect cashHeaderRect = new Rect(panel.xMax - 260, panel.y + 10, 110, 30);
            CommandGUI.DrawDarkPill(cashHeaderRect, "$", credits.ToString("N0"), CommandGUI.AccentGold);
            Rect goldHeaderRect = new Rect(panel.xMax - 144, panel.y + 10, 94, 30);
            CommandGUI.DrawDarkPill(goldHeaderRect, "◆", gold.ToString(), CommandGUI.AccentGold);

            // Close button
            if (CommandGUI.DrawButton(new Rect(panel.xMax - 48, panel.y + 8, 38, 34), "X", false, 14))
            {
                SwitchTab(LobbyTab.Home);
            }

            // Initialize default ownership if first run
            if (!PlayerPrefs.HasKey("GeoSniper.WeaponUnlocked_1"))
            {
                PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_1", 1); // M24 free starter
                PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_0", 0); // Barrett costs gold
                PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_2", 0); // MK12 costs cash
                if (PlayerPrefs.GetInt("GeoSniper.SelectedWeapon", 0) == 0 && PlayerPrefs.GetInt("GeoSniper.WeaponUnlocked_0", 0) == 0 && !IsDebugMode)
                {
                    PlayerPrefs.SetInt("GeoSniper.SelectedWeapon", 1);
                }
                PlayerPrefs.Save();
            }

            int selectedWeapon = PlayerPrefs.GetInt("GeoSniper.SelectedWeapon", 1);

            string[] names = { "BARRETT .50 CAL", "M24 TACTICAL SWS", "MK12 SPR DMR" };
            string[] classes = { "HEAVY ANTI-MATERIEL", "PRECISION BOLT-ACTION", "RAPID MARKSMAN" };
            float[] baseDamages = { 120f, 85f, 45f };
            int[] baseMags = { 5, 20, 30 };
            string[][] scopeLabels = new string[][] {
                new string[] { "10x MIL-DOT", "16x ULTRA", "24x EXTREME" },
                new string[] { "6x TACTICAL", "12x PRECISION", "18x HIGH-POWER" },
                new string[] { "4x RAPID", "8x RECON", "12x MARCO" }
            };
            Texture2D[] gunTextures = { barrettCardTex, m24CardTex, mk12CardTex };

            float cardY = panel.y + 48f;
            float cardH = 146f;
            float spacing = 8f;

            for (int i = 0; i < 3; i++)
            {
                bool isUnlocked = IsDebugMode || PlayerPrefs.GetInt("GeoSniper.WeaponUnlocked_" + i, i == 1 ? 1 : 0) == 1;
                bool isEq = isUnlocked && selectedWeapon == i;

                Rect r = new Rect(panel.x + 14, cardY + i * (cardH + spacing), panel.width - 28, cardH);
                CommandGUI.DrawPanel(r, CommandGUI.ThemeCard, isEq ? CommandGUI.AccentGold : isUnlocked ? CommandGUI.ThemeBorder : CommandGUI.ThemeCard, isEq ? 2f : 1f);

                // --- LEFT: HIGH-RES WEAPON CARD VISUAL ---
                Rect imgRect = new Rect(r.x + 8, r.y + 8, 205, 130);
                CommandGUI.DrawPanel(imgRect, Color.black, CommandGUI.ThemeBorder, 1f);
                if (gunTextures[i] != null)
                {
                    GUI.DrawTexture(imgRect, gunTextures[i], ScaleMode.ScaleAndCrop);
                }
                else if (weaponCardTex != null)
                {
                    GUI.DrawTexture(imgRect, weaponCardTex, ScaleMode.ScaleAndCrop);
                }

                // --- CENTER: STATS & UPGRADES ---
                float statX = r.x + 222f;

                // Name & Class Tag
                Color nameCol = isEq ? CommandGUI.AccentGold : isUnlocked ? Color.white : CommandGUI.Muted;
                GUI.Label(new Rect(statX, r.y + 6, 200, 20), names[i], new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = nameCol } });
                CommandGUI.DrawBadge(new Rect(statX + 205, r.y + 6, 120, 18), classes[i], isUnlocked ? CommandGUI.AccentCyan : new Color(0.85f, 0.45f, 0.45f), 9);

                int dmgLvl = PlayerPrefs.GetInt("GeoSniper.WpnDmgLvl_" + i, 0);
                int scopeLvl = PlayerPrefs.GetInt("GeoSniper.WpnScopeLvl_" + i, 0);
                int magLvl = PlayerPrefs.GetInt("GeoSniper.WpnMagLvl_" + i, 0);

                float curDmg = baseDamages[i] * (1f + dmgLvl * 0.20f);
                int curMag = baseMags[i] + magLvl * (i == 0 ? 2 : 5);
                string curScope = scopeLabels[i][Mathf.Clamp(scopeLvl, 0, 2)];

                // 1. GUN POWER (DAMAGE)
                float row1Y = r.y + 32f;
                Rect pwrPips = new Rect(statX, row1Y + 3, 52, 10);
                CommandGUI.DrawPipRating(pwrPips, dmgLvl + 1, 3, CommandGUI.AccentGold);
                GUI.Label(new Rect(statX + 58, row1Y, 170, 18), $"POWER: {curDmg:0} DMG (LV {dmgLvl + 1}/3)", new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = CommandGUI.AccentGold } });
                if (isUnlocked)
                {
                    Rect dmgBtn = new Rect(statX + 235, row1Y - 4, 118, 28);
                    if (dmgLvl >= 2)
                    {
                        CommandGUI.DrawBadge(dmgBtn, "MAX POWER", CommandGUI.AccentGold, 10);
                    }
                    else
                    {
                        int dmgCost = (dmgLvl + 1) * 1500;
                        bool canAfford = credits >= dmgCost;
                        if (CommandGUI.DrawButton(dmgBtn, $" +20% (${dmgCost:N0})", canAfford, 10))
                        {
                            if (canAfford)
                            {
                                PlayerPrefs.SetInt("GeoSniper.Credits", credits - dmgCost);
                                PlayerPrefs.SetInt("GeoSniper.WpnDmgLvl_" + i, dmgLvl + 1);
                                PlayerPrefs.Save();
                                GameAnalyticsManager.TrackWeaponUpgrade(i, "Damage", dmgLvl + 1, dmgCost);
                            }
                            else OpenEconomyRewardOffer("cash",dmgCost-credits);
                        }
                    }
                }

                // 2. OPTIC SCOPE ZOOM
                float row2Y = r.y + 64f;
                Rect scpPips = new Rect(statX, row2Y + 3, 52, 10);
                CommandGUI.DrawPipRating(scpPips, scopeLvl + 1, 3, CommandGUI.AccentCyan);
                GUI.Label(new Rect(statX + 58, row2Y, 170, 18), $"SCOPE: {curScope} (LV {scopeLvl + 1}/3)", new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = CommandGUI.AccentCyan } });
                if (isUnlocked)
                {
                    Rect scpBtn = new Rect(statX + 235, row2Y - 4, 118, 28);
                    if (scopeLvl >= 2)
                    {
                        CommandGUI.DrawBadge(scpBtn, "MAX SCOPE", CommandGUI.AccentCyan, 10);
                    }
                    else
                    {
                        int scpCost = (scopeLvl + 1) * 1200;
                        bool canAfford = credits >= scpCost;
                        if (CommandGUI.DrawButton(scpBtn, $" ZOOM (${scpCost:N0})", canAfford, 10))
                        {
                            if (canAfford)
                            {
                                PlayerPrefs.SetInt("GeoSniper.Credits", credits - scpCost);
                                PlayerPrefs.SetInt("GeoSniper.WpnScopeLvl_" + i, scopeLvl + 1);
                                PlayerPrefs.Save();
                                GameAnalyticsManager.TrackWeaponUpgrade(i, "Scope", scopeLvl + 1, scpCost);
                            }
                            else OpenEconomyRewardOffer("cash",scpCost-credits);
                        }
                    }
                }

                // 3. MAG CAPACITY
                float row3Y = r.y + 96f;
                Rect magPips = new Rect(statX, row3Y + 3, 52, 10);
                CommandGUI.DrawPipRating(magPips, magLvl + 1, 3, CommandGUI.AccentGold);
                GUI.Label(new Rect(statX + 58, row3Y, 170, 18), $"MAG: {curMag} RDS (LV {magLvl + 1}/3)", new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = CommandGUI.AccentGold } });
                if (isUnlocked)
                {
                    Rect magBtn = new Rect(statX + 235, row3Y - 4, 118, 28);
                    if (magLvl >= 2)
                    {
                        CommandGUI.DrawBadge(magBtn, "MAX MAG", CommandGUI.AccentGold, 10);
                    }
                    else
                    {
                        int magCost = (magLvl + 1) * 1000;
                        bool canAfford = credits >= magCost;
                        if (CommandGUI.DrawButton(magBtn, $" +MAG (${magCost:N0})", canAfford, 10))
                        {
                            if (canAfford)
                            {
                                PlayerPrefs.SetInt("GeoSniper.Credits", credits - magCost);
                                PlayerPrefs.SetInt("GeoSniper.WpnMagLvl_" + i, magLvl + 1);
                                PlayerPrefs.Save();
                                GameAnalyticsManager.TrackWeaponUpgrade(i, "Magazine", magLvl + 1, magCost);
                            }
                            else OpenEconomyRewardOffer("cash",magCost-credits);
                        }
                    }
                }

                // --- RIGHT: EQUIP / UNLOCK ACTION ---
                if (isUnlocked)
                {
                    Rect eqBtn = new Rect(r.xMax - 144, r.y + 44, 132, 52);
                    if (isEq)
                    {
                        CommandGUI.DrawBadge(eqBtn, " EQUIPPED", CommandGUI.AccentGold, 13);
                    }
                    else
                    {
                        if (CommandGUI.DrawButton(eqBtn, "EQUIP RIFLE", true, 12))
                        {
                            PlayerPrefs.SetInt("GeoSniper.SelectedWeapon", i);
                            PlayerPrefs.Save();
                        }
                    }
                }
                else
                {
                    if (i == 0) // Barrett .50 Cal costs 150 Gold
                    {
                        Rect buyGoldBtn = new Rect(r.xMax - 144, r.y + 42, 132, 54);
                        bool canAffordGold = gold >= 150;
                        if (CommandGUI.DrawGoldStoreButton(buyGoldBtn, canAffordGold ? "UNLOCK\n300 GOLD" : "NEED\n300 GOLD"))
                        {
                            if (canAffordGold)
                            {
                                PlayerPrefs.SetInt("GeoSniper.Gold", gold - 300);
                                PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_0", 1);
                                PlayerPrefs.SetInt("GeoSniper.SelectedWeapon", 0);
                                PlayerPrefs.Save();
                            }
                            else OpenEconomyRewardOffer("gold",300-gold);
                        }
                    }
                    else if (i == 2) // MK12 SPR DMR costs $2,500 Cash
                    {
                        Rect buyCashBtn = new Rect(r.xMax - 144, r.y + 42, 132, 54);
                        bool canAffordCash = credits >= 7500;
                        if (CommandGUI.DrawGreenPlayButton(buyCashBtn, canAffordCash ? "BUY\n$7,500 " : "NEED\n$7,500 "))
                        {
                            if (canAffordCash)
                            {
                                PlayerPrefs.SetInt("GeoSniper.Credits", credits - 7500);
                                PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_2", 1);
                                PlayerPrefs.SetInt("GeoSniper.SelectedWeapon", 2);
                                PlayerPrefs.Save();
                            }
                            else OpenEconomyRewardOffer("cash",7500-credits);
                        }
                    }
                }
            }

            if(economyRewardOffer) DrawEconomyRewardOffer(w,h);

            // Bottom Return Button
            if (CommandGUI.DrawButton(new Rect(panel.x + (panel.width - 180) / 2f, panel.yMax - 44, 180, 36), "BACK TO LOBBY", false, 12))
            {
                SwitchTab(LobbyTab.Home);
            }

            string[] tabIcons = { "", "", "", "", "" };
            string[] tabTitles = { "HOME", "CAMPAIGN", "ARMORY", "SECTOR", "VAULT" };
            int aTab = CommandGUI.DrawTabBar(new Rect(0, h - 56f, w, 56f), tabIcons, tabTitles, 2);
            if (aTab != 2) SwitchTab((LobbyTab)aTab);
        }

        void OpenEconomyRewardOffer(string type,int shortfall)
        {
            economyRewardType=type;
            economyRewardAmount=type=="gold"?50:1500;
            economyNotice="You are short by "+shortfall.ToString("N0")+" "+(type=="gold"?"GOLD":"CASH")+". Watch an ad to earn currency; the purchase still requires a second confirmation.";
            economyRewardOffer=true;
        }

        void DrawEconomyRewardOffer(float w,float h)
        {
            GUI.color=new Color(.01f,.015f,.02f,.82f);GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);GUI.color=Color.white;
            float pw=Mathf.Min(480f,w-32f),ph=190f;Rect p=new Rect((w-pw)/2f,(h-ph)/2f,pw,ph);
            CommandGUI.DrawPanel(p,CommandGUI.ThemeCard,CommandGUI.AccentGold,2f);
            GUI.Label(new Rect(p.x+20,p.y+18,p.width-40,28),"INSUFFICIENT FUNDS",new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=CommandGUI.AccentGold}});
            GUI.Label(new Rect(p.x+28,p.y+54,p.width-56,48),economyNotice,new GUIStyle(GUI.skin.label){fontSize=11,wordWrap=true,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}});
            if(CommandGUI.DrawButton(new Rect(p.x+24,p.y+120,p.width/2-32,42),"WATCH AD",true,12))
            {
                economyRewardOffer=false;
                string type=economyRewardType;int amount=economyRewardAmount;string day=System.DateTime.UtcNow.ToString("yyyyMMdd");
                int count=PlayerPrefs.GetString("GeoSniper.LastRewardAdDate","")==day?PlayerPrefs.GetInt("GeoSniper.RewardAdsToday",0):0;
                if(count<3) AdManager.Instance.ShowRewardedAd(success=>{if(success){if(type=="gold")PlayerPrefs.SetInt("GeoSniper.Gold",PlayerPrefs.GetInt("GeoSniper.Gold",0)+amount);else PlayerPrefs.SetInt("GeoSniper.Credits",PlayerPrefs.GetInt("GeoSniper.Credits",0)+amount);PlayerPrefs.SetString("GeoSniper.LastRewardAdDate",day);PlayerPrefs.SetInt("GeoSniper.RewardAdsToday",count+1);PlayerPrefs.Save();}},"armory_shortfall");
                else economyNotice="Daily rewarded-ad limit reached.";
            }
            if(CommandGUI.DrawButton(new Rect(p.x+p.width/2+8,p.y+120,p.width/2-32,42),"CANCEL",false,12)) economyRewardOffer=false;
        }

        void DrawSettingsModal(float w, float h)
        {
            if (showingAimSettings) { DrawAimSettings(w,h); return; }
            if (showingGraphicsSettings) { DrawGraphicsSettings(w,h); return; }
            CommandGUI.DrawPanel(new Rect(0, 0, w, h), CommandGUI.ThemeCard, Color.clear, 0);

            float pW = Mathf.Min(480f, w - 40f);
            float pH = Mathf.Min(535f, h - 30f);
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

            // Animated Progress bar
            float progress = Mathf.PingPong(Time.realtimeSinceStartup * 0.4f, 0.9f) + 0.1f;
            if (startupStage == "MAP DOWNLOAD") progress = 0.35f;
            else if (startupStage == "WORLD") progress = 0.75f;
            else if (startupStage == "PLAYER") progress = 0.95f;

            CommandGUI.DrawProgressBar(new Rect(centerPanel.x + 30, centerPanel.y + 155, centerPanel.width - 60, 20), progress, CommandGUI.AccentCyan, CommandGUI.ThemeCard);

            if (mapNotice != "")
            {
                var noticeStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, normal = { textColor = CommandGUI.Muted } };
                GUI.Label(new Rect(centerPanel.x + 30, centerPanel.y + 182, centerPanel.width - 60, 24), mapNotice, noticeStyle);
            }

            if (CommandGUI.DrawButton(new Rect(centerPanel.x + 90, centerPanel.yMax - 55, centerPanel.width - 180, 40), "SWITCH TO OFFLINE OPS", false, 13))
            {
                StartOffline(true);
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
        void DrawLocationPicker(float w, float h)
        {
            CommandGUI.DrawPanel(new Rect(0, 0, w, h), CommandGUI.ThemeCard, Color.clear, 0);

            float panelW = Mathf.Min(900, w - 40);
            float panelH = h - 74;
            Rect panel = new Rect((w - panelW) / 2f, 10, panelW, panelH);
            CommandGUI.DrawPanel(panel, CommandGUI.ThemeCard, CommandGUI.ThemeBorder, 2f);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = CommandGUI.AccentCyan }
            };
            GUI.Label(new Rect(panel.x + 20, panel.y + 18, 420, 30), "WHERE DO YOU WANT TO PLAY?", titleStyle);

            if (CommandGUI.DrawButton(new Rect(panel.xMax - 110, panel.y + 15, 95, 36), " LOBBY", false, 14))
            {
                SwitchTab(LobbyTab.Home);
            }

            bool livePreferred=LocationSelectionPolicy.RequiresLiveFix(PlayerPrefs.GetString("GeoSniper.LocationSource",""));
            string savedPlace=activeLocation!=null?activeLocation.Label:PlayerPrefs.GetString("GeoSniper.SavedLabel","");
            var bodyStyle=new GUIStyle(GUI.skin.label) {fontSize=15,wordWrap=true,normal={textColor=CommandGUI.Text}};
            var smallStyle=new GUIStyle(bodyStyle) {fontSize=13,normal={textColor=CommandGUI.Muted}};
            string sourceSummary=livePreferred?"SELECTED SOURCE: LIVE GPS - refreshed when you deploy"
                :string.IsNullOrWhiteSpace(savedPlace)?"NO LOCATION SELECTED - choose an option below"
                :"SELECTED SOURCE: CHOSEN PLACE - not your live GPS";
            GUI.Label(new Rect(panel.x+20,panel.y+56,panel.width-40,26),sourceSummary,bodyStyle);
            string detail=livePreferred?"Saved map data and your location source are separate. GPS determines where you play."
                :string.IsNullOrWhiteSpace(savedPlace)?"Play around you, or search another neighbourhood."
                :"Place: "+savedPlace;
            GUI.Label(new Rect(panel.x+20,panel.y+84,panel.width-40,38),detail,smallStyle);

            float cardWidth=(panel.width-52)/2;
            var gpsCard=new Rect(panel.x+20,panel.y+132,cardWidth,158);
            var searchCard=new Rect(gpsCard.xMax+12,gpsCard.y,cardWidth,158);
            CommandGUI.Fill(gpsCard,CommandGUI.ThemeBg);
            CommandGUI.Fill(new Rect(gpsCard.x,gpsCard.y,4,gpsCard.height),CommandGUI.AccentGold);
            CommandGUI.Fill(searchCard,CommandGUI.ThemeBg);
            GUI.Label(new Rect(gpsCard.x+16,gpsCard.y+12,cardWidth-32,26),"01 / MY LIVE LOCATION",bodyStyle);
            GUI.Label(new Rect(gpsCard.x+16,gpsCard.y+44,cardWidth-32,50),"Use this phone's GPS. Replaces the previously chosen place.",smallStyle);
            if(CommandGUI.DrawButton(new Rect(gpsCard.x+16,gpsCard.yMax-56,cardWidth-32,44),"USE MY LIVE LOCATION",true,16))
            {
                resumeMissionAfterLocationConsent=false;
                showingLocationConsent=true;
            }
            GUI.Label(new Rect(searchCard.x+16,searchCard.y+12,cardWidth-32,26),"02 / CHOOSE A PLACE",bodyStyle);
            GUI.Label(new Rect(searchCard.x+16,searchCard.y+44,cardWidth-32,50),"Search any city or neighbourhood. No device location permission needed.",smallStyle);
            if(CommandGUI.DrawButton(new Rect(searchCard.x+16,searchCard.yMax-56,cardWidth-32,44),"SEARCH A PLACE",false,16))
                GUI.FocusControl("WorldMapPlaceSearch");

            // Keep both source choices above the independently scrolling results.
            GUI.Label(new Rect(panel.x+20,panel.y+302,panel.width-40,22),"SEARCH BY PLACE NAME AND CITY",smallStyle);

            GUI.SetNextControlName("WorldMapPlaceSearch");
            string search = GUI.TextField(new Rect(panel.x + 20, panel.y + 330, panel.width - 150, 42), locationSearch, 120, new GUIStyle(GUI.skin.textField) { fontSize = 16, alignment = TextAnchor.MiddleLeft });
            if (search != locationSearch) { locationSearch = search; locationScroll = Vector2.zero; }

            GUI.enabled = !searching && Time.realtimeSinceStartup >= nextSearch;
            if (CommandGUI.DrawButton(new Rect(panel.xMax - 120, panel.y + 330, 100, 42), "SEARCH", false, 14))
            {
                StartCoroutine(SearchPlaces());
            }
            GUI.enabled = true;

            // Recent Locations
            GUI.Label(new Rect(panel.x + 20, panel.y + 383, 130, 24), "RECENT SEARCHES", smallStyle);
            
            string recentStr = PlayerPrefs.GetString("GeoSniper.RecentLocations", "");
            string[] recent = recentStr.Split(new char[]{'|'}, System.StringSplitOptions.RemoveEmptyEntries);
            float pX = panel.x + 150;
            foreach (var p in recent)
            {
                if (CommandGUI.DrawButton(new Rect(pX, panel.y + 380, 110, 30), p.Length>16?p.Substring(0,15)+"...":p, false, 11))
                {
                    locationSearch = p;
                    StartCoroutine(SearchPlaces());
                }
                pX += 116;
                if (pX > panel.xMax - 120) break;
            }

            // Search Results Scroll List
            var matches = locations ?? new System.Collections.Generic.List<GameLocation>();
            float listY = panel.y + 422;
            float listH = Mathf.Max(52,panel.height - 486);

            locationScroll = GUI.BeginScrollView(new Rect(panel.x + 20, listY, panel.width - 40, listH), locationScroll, new Rect(0, 0, panel.width - 65, Mathf.Max(listH, matches.Count * 52)));
            GameLocation chosen = null;
            if(matches.Count==0)
                GUI.Label(new Rect(8,8,panel.width-90,44),searching?"Searching...":"Search results appear here. Your saved searches do not override live GPS.",smallStyle);

            for (int i = 0; i < matches.Count; i++)
            {
                Rect itemRect = new Rect(0, i * 52, panel.width - 65, 46);
                if (CommandGUI.DrawButton(itemRect, matches[i].Label, false, 14))
                {
                    chosen = matches[i];
                }
            }
            GUI.EndScrollView();

            var errStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.85f, 0.9f) }
            };
            errStyle.wordWrap=true;
            GUI.Label(new Rect(panel.x + 20, panel.yMax - 56, panel.width - 40, 48), locationError, errStyle);

            string[] tabIcons = { "", "", "", "", "" };
            string[] tabTitles = { "HOME", "CAMPAIGN", "ARMORY", "SECTOR", "VAULT" };
            int locTab = CommandGUI.DrawTabBar(new Rect(0, h - 56f, w, 56f), tabIcons, tabTitles, 3);
            if (locTab != 3) SwitchTab((LobbyTab)locTab);

            if (chosen != null) {rangeToStart=false;campaignNodeToStart=-1;stageIndexToStart=-1;isPvPDuel=false;StartCoroutine(Play(chosen));}
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
        void OnDisable() { Application.lowMemory-=ReleaseLobbySectorOnLowMemory; StopAllCoroutines(); Input.location.Stop(); }
    }
}
