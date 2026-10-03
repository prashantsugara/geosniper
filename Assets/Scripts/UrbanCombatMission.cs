using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GeoSniper
{
    public enum MissionState { None, Briefing, InProgress, Extraction, Complete, Failed }
    
    public sealed partial class UrbanCombatMission : MonoBehaviour
    {
        readonly List<EnemyBot> enemies=new List<EnemyBot>(); readonly List<CivilianBot> civilians=new List<CivilianBot>(); Camera cameraView; UrbanPlayer player; float fireCooldown; float fireBufferTimer; int defeated; AudioSource audioSource, hqRadioSource; AudioClip ambientMusic;
        readonly HashSet<EnemyBot> countedDeaths=new HashSet<EnemyBot>();
        readonly MissionAlertState alerts=new MissionAlertState();
        DifficultyProfile difficulty;
        DifficultyProfile baselineDifficulty;
        LevelNode campaignNode;
        int CampaignThreatBudget => campaignNode != null ? campaignNode.enemyCount : 5;
        bool CampaignCounterSniper => CampaignProgression.HasCounterSniper(campaignNode);
        public DifficultyProfile Difficulty => difficulty;
        readonly Dictionary<(Material,Texture),Material> civilianMaterialCache=new Dictionary<(Material,Texture),Material>();
        readonly List<Material> ownedMaterials=new List<Material>();
        int ammo=20;
        readonly int[] weaponAmmo = { -1, -1, -1 };
        bool rangeMode;
        float reloadTimer;
        float chamberTimer;
        List<Vector3> mapSpawns, rooftopSpawns;
        public List<Vector3> RooftopSpawns => rooftopSpawns;
        SniperPresentation weapon;
        BallisticsSystem ballistics;
        WeaponSway sway;
        bool freeRoam;
        readonly WaypointPin waypointPin=new WaypointPin();
        bool hasWaypointPin { get=>waypointPin.Active; set {if(!value) waypointPin.Clear();} }
        Vector3 waypointPinPos => waypointPin.Position;
        float uavTimer;
        float uavCooldown;
        int envState = 0;
        bool manualEnvironment;
        Light environmentSun;
        float damageFeedbackTimer;
        float damageFeedbackStrength;
        Vector3 lastThreatWorldPos;
        float threatArcTimer;
        bool mapOpen;
        float mapClosedCooldown = 0f;
        Texture2D gameplayOverview;
        float overviewSpan;
        int overviewSectors;
        Vector3 overviewOrigin;
        bool nativeMapOpen;
        bool restarting;
        float hitFlash;
        float shotNoticeTime;
        string shotNotice;
        float accoladeTimer;
        string accoladeTitle = "";
        string accoladeSubtitle = "";
        float lastKillTime;
        int multiKillStreak;
        bool lastShotWasHeadshot;
        EnemyBot nearbyKnifeTarget;
        bool isCurrentKillKnife;
        GameLocation mapLocation;
        bool geographicMap;
        bool stageMode;
        bool choosingStage;
        public bool isPvPDuel;
        EnemyBot duelRivalSniper;
        float duelLockTimer;
        Vector3 duelRivalHome;
        public Vector3 DuelRivalPosition => duelRivalHome;
        const float DuelMinimumSeparation = 78f;
        float duelStrafePhase;
        int duelLevel;
        LineRenderer duelLaserLine;
        public MissionState currentMissionState;
        public static bool IsSoundMasked => activeSoundMaskTimer > 0f;
        public static string ActiveSoundMaskName => activeSoundMaskName;
        public static float SoundMaskRemaining => activeSoundMaskTimer;
        static float activeSoundMaskTimer = 0f;
        static string activeSoundMaskName = "";
        float soundMaskInterval = 28f;
        bool reconBinosActive = false;
        Vector3 extractionPoint;
        GameObject extractionMarker;
        HelicopterPickup extractionHelicopter;
        GameObject missionDynamicBillboard;
        int stageIndex;
        int stageTarget;
        const int StageCount = 50;
        CampaignContract activeContract;
        CivilianBot contractVIP;
        EnemyBot contractCounterSniper;
        float contractModeTimer;
        readonly List<Vector3> contractVipPath = new List<Vector3>();
        int dailyTarget,dailyProgress;
        bool stageRewarded;
        bool stageRewardsDoubled;
        bool hasUsedAdRevive;
        bool victorySoundPlayed;
        float victoryScreenTimer;
        string missionFailureReason;
        bool victoryPending;
        float victoryPendingTimer;
        bool showingDebriefing;
        int rewardCash;
        int rewardXP;
        int rewardStars = 3;
        string victoryHeader = "VICTORY!";
        string victorySubheader = "MISSION ACCOMPLISHED";
        int totalShotsFired;
        int totalHitsScored;
        int totalHeadshotsScored;
        float missionDuration;
        string dailyKey;
        float mapSpan=450;
        Material enemyJacket, enemySkin, enemyArmor, enemyHighlight, enemyDark, enemyWeapon, enemyWeaponWood;
        Material civilianShirt, civilianPants;

        void ZoomMap(float factor) { mapSpan=Mathf.Clamp(mapSpan*factor,60,2400); }
        Rect TacticalRect()
        {
            float s=Mathf.Max(.4f, Mathf.Min(Screen.height/720f, Screen.width/800f));
            float w=Screen.width/s,h=Screen.height/s;
            float panel=w>=1000?280:210;
            return new Rect(panel+16,58,w-panel-28,Mathf.Max(80,h-96));
        }
        void UpdateMapZoom()
        {
            if(!mapOpen) return;
            float s=Mathf.Max(.4f, Mathf.Min(Screen.height/720f, Screen.width/800f));
            Rect rect=TacticalRect();
            if(Input.touchCount==2)
            {
                Touch a=Input.GetTouch(0), b=Input.GetTouch(1);
                Vector2 pa=new Vector2(a.position.x,Screen.height-a.position.y)/s;
                Vector2 pb=new Vector2(b.position.x,Screen.height-b.position.y)/s;
                if(rect.Contains(pa) && rect.Contains(pb) && a.phase!=TouchPhase.Began && b.phase!=TouchPhase.Began)
                {
                    float current=Vector2.Distance(a.position,b.position);
                    float previous=Vector2.Distance(a.position-a.deltaPosition,b.position-b.deltaPosition);
                    if(current>10 && previous>10) ZoomMap(previous/current);
                }
            }
            else if(Input.touchCount==0)
            {
                Vector2 p=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y)/s;
                if(rect.Contains(p)) ZoomMap(Mathf.Pow(1.2f,-Input.mouseScrollDelta.y));
            }
        }
        readonly HashSet<string> mapNamesDrawn=new HashSet<string>();
        readonly List<EnemyBot> radarEnemies=new List<EnemyBot>();
        readonly List<Vector2> radarMarkers=new List<Vector2>();
        public UrbanPlayer Player => player;
        public void Begin(Camera camera, List<Vector3> spawnPoints=null, List<Vector3> roofs=null, bool explore=true, GameLocation location=null, bool realMap=true, Vector3? geographicSpawn=null, bool prepareStage=false)
        {
            RadioCommsChannel.ResetMissionDispatch();
            EnsureEnemyMaterials();
            alerts.Reset();
            if(difficulty!=null) Destroy(difficulty);
            if(baselineDifficulty!=null) Destroy(baselineDifficulty);
            baselineDifficulty=DifficultyProfile.Create(DifficultyProfile.Selected);
            difficulty=Instantiate(baselineDifficulty);
            campaignNode=null;
            isPvPDuel=false;
            if(duelLaserLine!=null) duelLaserLine.enabled=false;
            mapSpawns=spawnPoints; rooftopSpawns=roofs;
            freeRoam=explore;
            mapLocation=location;
            geographicMap=realMap;
            if(!explore && (spawnPoints==null || spawnPoints.Count<2))
            {
                var safeSpawns = new List<Vector3>();
                if (spawnPoints != null) safeSpawns.AddRange(spawnPoints);
                var world = SectorWorld.LoadedWorlds.Count > 0 ? SectorWorld.LoadedWorlds[0] : null;
                for (int x = -40; x <= 40; x += 25)
                    for (int z = -40; z <= 40; z += 25)
                    {
                        float gy = world != null ? world.Ground(x, z) : 0f;
                        safeSpawns.Add(new Vector3(x, gy + 0.25f, z));
                    }
                mapSpawns = spawnPoints = safeSpawns;
            }
            cameraView=camera; cameraView.transform.SetParent(null);
            if(cameraView.GetComponent<AudioListener>()==null) cameraView.gameObject.AddComponent<AudioListener>();
            WorldRainSystem.EnsureSystem(cameraView != null ? cameraView.transform : null);
            AudioListener.volume=Mathf.Clamp01(PlayerPrefs.GetFloat("GeoSniper.MasterVolume",1f));
            var p=new GameObject("Player"); player=p.AddComponent<UrbanPlayer>(); player.Initialize(cameraView);
            player.Health.OnDamaged-=HandlePlayerDamaged;
            player.Health.OnDamaged+=HandlePlayerDamaged;
            if(weapon==null) weapon=gameObject.AddComponent<SniperPresentation>();
            
            // Phase 1 integrations
            if(weapon.Config!=null) Destroy(weapon.Config);
            if(ballistics!=null && ballistics.config!=null) Destroy(ballistics.config);
            var weaponAsset=Resources.Load<WeaponConfig>("WeaponConfig");
            var weaponConfig=weaponAsset!=null?Instantiate(weaponAsset):ScriptableObject.CreateInstance<WeaponConfig>();
            var ballisticsAsset=Resources.Load<BallisticsConfig>("BallisticsConfig");
            var ballisticsConfig=ballisticsAsset!=null?Instantiate(ballisticsAsset):ScriptableObject.CreateInstance<BallisticsConfig>();
            int selectedWpn = Mathf.Clamp(PlayerPrefs.GetInt("GeoSniper.SelectedWeapon", 1),0,2);
            if (!IsWeaponOwned(selectedWpn)) selectedWpn = IsWeaponOwned(1) ? 1 : 0;
            PlayerPrefs.SetInt("GeoSniper.SelectedWeapon", selectedWpn);
            ApplyWeaponConfiguration(selectedWpn, ballisticsConfig, weaponConfig);
            for (int i=0;i<weaponAmmo.Length;i++) weaponAmmo[i]=-1;

            weapon.Config = weaponConfig;
            weapon.CurrentWeaponIndex = selectedWpn;
            weapon.Initialize(cameraView);
            
            if(sway==null) sway=gameObject.AddComponent<WeaponSway>();
            sway.config = weaponConfig;
            weapon.SetSway(sway);
            
            if(ballistics==null) ballistics=gameObject.AddComponent<BallisticsSystem>();
            ballistics.config = ballisticsConfig;
            ballistics.Initialize(enemies, player.transform, cameraView, weapon);
            ballistics.ShotResolved-=HandleShotResolved;
            ballistics.ShotResolved+=HandleShotResolved;
            
            DamageSystem.OnDamageDealt -= HandleDamageDealt;
            DamageSystem.OnDamageDealt += HandleDamageDealt;
            DamageSystem.OnEnemyKilled -= HandleEnemyKilled;
            DamageSystem.OnEnemyKilled += HandleEnemyKilled;
            DamageSystem.OnCivilianKilled -= HandleCivilianKilled;
            DamageSystem.OnCivilianKilled += HandleCivilianKilled;
            ExplosiveProp.OnAccidentKillNotice -= HandleAccidentKill;
            ExplosiveProp.OnAccidentKillNotice += HandleAccidentKill;
            totalShotsFired = 0; totalHitsScored = 0; totalHeadshotsScored = 0; missionDuration = 0f; victorySoundPlayed = false; victoryScreenTimer = 0f;
            
            if(audioSource==null) audioSource=gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend=0f; audioSource.loop=true; audioSource.playOnAwake=false; audioSource.ignoreListenerPause=false; audioSource.volume=.62f;
            if(ambientMusic==null)
            {
                ambientMusic = Resources.Load<AudioClip>("music/A_Single_Point") ?? Resources.Load<AudioClip>("A_Single_Point") ?? ProceduralAudio.CreateAmbientMusic();
            }
            if(ambientMusic!=null && audioSource.clip!=ambientMusic) audioSource.clip=ambientMusic;
            if(ambientMusic!=null && Application.isFocused && !audioSource.isPlaying) audioSource.Play();
            
            if (mapLocation != null)
            {
                var weatherCtrl = gameObject.GetComponent<LiveWeatherController>() ?? gameObject.AddComponent<LiveWeatherController>();
                weatherCtrl.FetchWeather(mapLocation.Latitude, mapLocation.Longitude, (weatherCode, isDay) => 
                {
                    if (!manualEnvironment)
                    {
                        if (weatherCode >= 51) envState = isDay ? 2 : 3;
                        else envState = isDay ? 0 : 1;
                        ApplyEnvironment();
                    }
                });
            }
            ApplyEnvironment();

            if(prepareStage)
            {
                if(mapSpawns==null) BuildTraversalRoute();
                else player.Place(geographicSpawn??ChooseStart(mapSpawns));
                return;
            }
            if(freeRoam)
            {
                if(geographicSpawn.HasValue) player.Place(geographicSpawn.Value);
                else if(spawnPoints==null) BuildTraversalRoute();
                else player.Place(ChooseStart(rooftopSpawns!=null && rooftopSpawns.Count>0?rooftopSpawns:spawnPoints));
                // World-sector patrols fight back. GPS Range explicitly restores
                // invulnerability in BeginRange for non-lethal practice.
                player.Health.Invulnerable=false;
                SpawnFreeRoamPatrols();
            }
            else if(spawnPoints==null) { BuildTraversalRoute(); for(int i=0;i<8;i++) SpawnEnemy(i); }
            else if(envState==2)
            {
                var start=roofs!=null && roofs.Count>0 ? roofs[0] : spawnPoints[0];
                player.Place(start);
                foreach(var point in spawnPoints)
                {
                    if(enemies.Count>=8) break;
                    if(Vector3.Distance(point,player.transform.position)<65) continue;
                    bool occupied=false;
                    foreach(var enemy in enemies) if(Vector3.Distance(point,enemy.transform.position)<12) occupied=true;
                    if(!occupied) SpawnEnemy(enemies.Count,point+Vector3.up);
                }
                if(enemies.Count==0)
                {
                    Vector3 fwd = player.transform.forward;
                    if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                    SpawnEnemy(0, player.transform.position + fwd * 70f + Vector3.up);
                    SpawnEnemy(1, player.transform.position + fwd * 85f + Vector3.Cross(Vector3.up, fwd) * 12f + Vector3.up);
                }
            }

            if (!prepareStage)
            {
                PlayHQDispatchSound();
                EnsureMissionBillboard();
            }
        }
        Vector3 ChooseStart(List<Vector3> candidates)
        {
            if(candidates==null || candidates.Count==0) return Vector3.zero;
            Vector3 best=candidates[0]; float bestDistance=float.MaxValue;
            foreach(var candidate in candidates)
            {
                float distance=candidate.x*candidate.x+candidate.z*candidate.z;
                if(distance<bestDistance) { best=candidate; bestDistance=distance; }
            }
            return best;
        }
        void OpenMap()
        {
            if(mapOpen || restarting) return;
            AdManager.Instance?.HideNativeBillboardAd();
            mapOpen=true;
            mapBrowseCenter=player!=null?player.transform.position:Vector3.zero;
            mapDragging=false;
            foreach(var bot in enemies) if(bot!=null) bot.Suspended=true;
            // The in-game map owns pin selection on both desktop and touch devices.
            nativeMapOpen=false;
        }
        void CloseMap()
        {
            if(nativeMapOpen) MapLibreNative.Close();
            nativeMapOpen=false; mapOpen=false; mapSearchFocused=false; mapDragging=false;
            mapClosedCooldown = 0.45f;
            foreach(var bot in enemies) if(bot!=null) bot.Suspended=false;
        }
        [UnityEngine.Scripting.Preserve]
        public void OnNativeMapClosed(string unused) { nativeMapOpen=false; mapOpen=false; }
        [UnityEngine.Scripting.Preserve]
        public void OnNativeMapFailed(string reason) { nativeMapOpen=false; }
        public void Restart()
        {
            if(!restarting) { restarting=true; StartCoroutine(RestartNextFrame()); }
        }
        System.Collections.IEnumerator RestartNextFrame()
        {
            bool resumeRange=rangeMode;
            bool resumeStage=stageMode;int resumeIndex=stageIndex;
            bool resumeDuel=isPvPDuel;int resumeNode=campaignNode!=null?campaignNode.id:-1;
            Vector3? restartSpawn=null;
            if(geographicMap && player!=null)
                foreach(var world in SectorWorld.LoadedWorlds)
                    if(world!=null && world.TryFindGeographicSpawn(player.transform.position,out var safe))
                    { restartSpawn=safe; break; }
            CloseMap();
            if(cameraView!=null) cameraView.transform.SetParent(null,true);
            foreach(var enemy in enemies) if(enemy!=null) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            foreach(var civ in civilians) if(civ!=null) Destroy(civ.gameObject);
            enemies.Clear(); countedDeaths.Clear(); civilians.Clear(); defeated=0; fireCooldown=0; reloadTimer=0; ammo=20;
            currentMissionState = MissionState.None;
            missionFailureReason=null;
            if(extractionMarker!=null) Destroy(extractionMarker);
            if(extractionHelicopter!=null) Destroy(extractionHelicopter.gameObject);
            if(missionDynamicBillboard!=null) Destroy(missionDynamicBillboard);
            AdManager.Instance?.HideNativeBillboardAd();
            if(player!=null) Destroy(player.gameObject);
            player=null;
            yield return null;
            Begin(cameraView,mapSpawns,rooftopSpawns,freeRoam,mapLocation,geographicMap,restartSpawn,resumeStage || resumeDuel);
            if(resumeDuel) BeginPvPDuel(resumeNode);
            else if(resumeStage) BeginStage(resumeIndex);
            else if(resumeRange) BeginRange();
            GetComponent<SectorStreamer>()?.SetPlayer(player);
            restarting=false;
        }
        void BuildTraversalRoute()
        {
            var roof=GameObject.CreatePrimitive(PrimitiveType.Cube); roof.name="Procedural rooftop access platform"; roof.transform.position=new Vector3(0,9,-105); roof.transform.localScale=new Vector3(18,1,14);
            var roofBuilding=GameObject.CreatePrimitive(PrimitiveType.Cube); roofBuilding.name="Enterable shell building"; roofBuilding.transform.position=new Vector3(0,4,-105); roofBuilding.transform.localScale=new Vector3(18,8,14);
            var landing=GameObject.CreatePrimitive(PrimitiveType.Cube); landing.name="Street landing"; landing.transform.position=new Vector3(0,.2f,-115); landing.transform.localScale=new Vector3(6,.4f,6);

            // Perimeter safety railings (0.85m high) around procedural rooftop platform
            float railH = 0.85f;
            float railY = 9.5f + (railH / 2f);
            var barrierMat = RuntimeMaterial("Platform_Rail_Mat", new Color(0.24f, 0.26f, 0.28f), 0.5f, 0.2f);
            // North railing
            var nRail = GameObject.CreatePrimitive(PrimitiveType.Cube); nRail.name = "Platform Rail North";
            nRail.transform.position = new Vector3(0, railY, -98f); nRail.transform.localScale = new Vector3(18f, railH, 0.35f);
            nRail.GetComponent<Renderer>().sharedMaterial = barrierMat;
            // East railing
            var eRail = GameObject.CreatePrimitive(PrimitiveType.Cube); eRail.name = "Platform Rail East";
            eRail.transform.position = new Vector3(9f, railY, -105f); eRail.transform.localScale = new Vector3(0.35f, railH, 14f);
            eRail.GetComponent<Renderer>().sharedMaterial = barrierMat;
            // West railing
            var wRail = GameObject.CreatePrimitive(PrimitiveType.Cube); wRail.name = "Platform Rail West";
            wRail.transform.position = new Vector3(-9f, railY, -105f); wRail.transform.localScale = new Vector3(0.35f, railH, 14f);
            wRail.GetComponent<Renderer>().sharedMaterial = barrierMat;
            // South railings (with 2.2m clearance gap at ladder landing)
            var sRailL = GameObject.CreatePrimitive(PrimitiveType.Cube); sRailL.name = "Platform Rail South L";
            sRailL.transform.position = new Vector3(-5.0f, railY, -112f); sRailL.transform.localScale = new Vector3(8.0f, railH, 0.35f);
            sRailL.GetComponent<Renderer>().sharedMaterial = barrierMat;
            var sRailR = GameObject.CreatePrimitive(PrimitiveType.Cube); sRailR.name = "Platform Rail South R";
            sRailR.transform.position = new Vector3(5.0f, railY, -112f); sRailR.transform.localScale = new Vector3(8.0f, railH, 0.35f);
            sRailR.GetComponent<Renderer>().sharedMaterial = barrierMat;

            // Vertical ladder replaces stairs
            float ladderZ = -112.2f;
            float topY = 9.5f;
            var ladderMat = new Material(Shader.Find("Standard"));
            ladderMat.color = new Color(.2f, .22f, .24f);

            for (int side = -1; side <= 1; side += 2)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "Ladder rail";
                rail.transform.position = new Vector3(side * 0.55f, topY / 2f, ladderZ);
                rail.transform.localScale = new Vector3(0.1f, topY, 0.1f);
                rail.GetComponent<Renderer>().sharedMaterial = ladderMat;
            }
            for (float y = 0.4f; y < topY; y += 0.4f)
            {
                var rung = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rung.name = "Ladder rung";
                rung.transform.position = new Vector3(0, y, ladderZ);
                rung.transform.localScale = new Vector3(1.2f, 0.08f, 0.1f);
                rung.GetComponent<Renderer>().sharedMaterial = ladderMat;
                rung.GetComponent<Collider>().enabled = false;
            }
            SectorWorld.Ladders.Add(new SectorWorld.LadderRoute {
                Owner = null,
                Bottom = new Vector3(0, 0.2f, ladderZ - 0.4f),
                Top = new Vector3(0, topY + 0.5f, ladderZ - 0.4f),
                Landing = new Vector3(0, topY + 0.1f, -110f)
            });
        }
        bool IsValidOutdoorSpawn(Vector3 pt, out Vector3 snappedPt, bool isRooftop = false)
        {
            snappedPt = pt;
            
            if(SectorWorld.WaterAt(pt)) return false;
            // 1. Upward open-sky clearance: must have direct sky above (not under building roofs/ceilings)
            if (Physics.Raycast(pt + Vector3.up * 0.4f, Vector3.up, out var roofHit, 35f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // 2. Exact building footprint polygon containment: ground patrols must not spawn inside building interiors
            if (!isRooftop && IsInsideBuildingFootprint(pt))
            {
                return false;
            }

            // 3. Ground surface snapping and slope verification:
            var currentWorld = SectorWorld.LoadedWorlds.Count > 0 ? SectorWorld.LoadedWorlds[0] : null;
            float probeY = currentWorld != null ? Mathf.Max(pt.y, currentWorld.Ground(pt.x, pt.z)) : pt.y;
            if (Physics.Raycast(new Vector3(pt.x, probeY + 30f, pt.z), Vector3.down, out var groundHit, 60f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (groundHit.normal.y < 0.65f) return false;
                snappedPt = groundHit.point + Vector3.up * 0.08f;
            }
            else if (currentWorld != null)
            {
                snappedPt = new Vector3(pt.x, currentWorld.Ground(pt.x, pt.z) + 0.12f, pt.z);
            }
            else
            {
                return false;
            }

            if(SectorWorld.WaterAt(snappedPt)) return false;
            // 4. Capsule clearance for full human standing height
            if (Physics.CheckCapsule(snappedPt + Vector3.up * 0.45f, snappedPt + Vector3.up * 1.55f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return true;
        }

        static bool IsInsideBuildingFootprint(Vector3 pos)
        {
            foreach (var world in SectorWorld.LoadedWorlds)
            {
                if (world == null || world.Features == null) continue;
                Vector3 localPos = world.transform.InverseTransformPoint(pos);
                Vector2 pt2 = new Vector2(localPos.x, localPos.z);
                foreach (var f in world.Features)
                {
                    if (f.Kind != "building" || f.Points == null || f.Points.Count < 3) continue;
                    if (PointInPolygon(pt2, f.Points)) return true;
                }
            }
            return false;
        }

        static bool PointInPolygon(Vector2 p, List<Vector2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
            }
            return inside;
        }

        void SpawnEnemy(int i, Vector3? position=null)
        {
            Vector3 spawnPos;
            var world = SectorWorld.LoadedWorlds.Count > 0 ? SectorWorld.LoadedWorlds[0] : null;
            if (position.HasValue)
            {
                spawnPos = position.Value;
            }
            else
            {
                float fx = (i % 4 - 1.5f) * 22; float fz = 30 + (i / 4) * 35;
                float fy = world != null ? world.Ground(fx, fz) : 1.1f;
                Vector3 fallbackPt = new Vector3(fx, fy + 0.15f, fz);
                if (IsValidOutdoorSpawn(fallbackPt, out var validFallback, false))
                    spawnPos = validFallback;
                else if (player != null)
                {
                    float fanAngle = (i - 1.5f) * 32f;
                    Vector3 dir = Quaternion.Euler(0, fanAngle, 0) * (player.transform.forward.sqrMagnitude > 0.01f ? player.transform.forward : Vector3.forward);
                    Vector3 fwdPt = player.transform.position + dir * (30f + (i % 2) * 14f);
                    float gy = world != null ? world.Ground(fwdPt.x, fwdPt.z) : fwdPt.y;
                    spawnPos = new Vector3(fwdPt.x, gy + 0.12f, fwdPt.z);
                }
                else
                {
                    spawnPos = fallbackPt;
                }
            }

            if(!SectorWorld.DryFootprint(spawnPos,.6f))
            {
                bool relocated=false;
                foreach(var sector in SectorWorld.LoadedWorlds)
                    if(sector!=null && sector.TryFindGeographicSpawn(spawnPos,out var safe) && SectorWorld.DryFootprint(safe,.6f)
                        && (player==null || FlatDistance(safe,player.transform.position)>10f))
                    {spawnPos=safe;relocated=true;break;}
                if(!relocated) { Debug.LogWarning("Enemy spawn skipped: no supported dry land near target."); return; }
            }
            if (world != null)
            {
                float groundY = world.Ground(spawnPos.x, spawnPos.z);
                if (spawnPos.y < groundY + 0.06f) spawnPos.y = groundY + 0.12f;
                if (spawnPos.y > groundY + 2.5f) world.EnsureRooftopStairs(spawnPos);
            }

            var o=new GameObject("Fictional Patrol Human "+(i+1));
            o.transform.position=spawnPos;
                
            Transform visualRoot=o.transform;
            var enemyAssets = ModelLibrary.Load("Enemies");
            var imported = (enemyAssets != null && enemyAssets.Length > 0) ? enemyAssets[i % enemyAssets.Length] : Resources.Load<GameObject>("Models/Enemies/swat");
            
            if(imported!=null)
            {
                var visual=ImportedVisual.CreateEnemy(imported,o.transform);
                ApplyEnemyMaterials(visual.transform);
                ImportedVisual.ConfigureCharacterSurfaces(visual.transform);
                visualRoot=visual.transform;
                // Imported characters can contain a floating origin or animation
                // bounds above the root. Re-align the posed feet to the patrol
                // surface after normalization so rooftop and street targets land.
                var posed=ImportedVisual.PosedBounds(visualRoot);
                if(float.IsFinite(posed.min.y) && Mathf.Abs(posed.min.y)>0.002f)
                    visualRoot.localPosition += Vector3.down*posed.min.y;
            }
            else
            {
                BuildEnemyVisual(o);
            }
            
            var bot=o.AddComponent<EnemyBot>(); bot.Difficulty=difficulty; bot.Alerts=alerts;
            bot.ReportBodies = !stageMode || activeContract.Type == CampaignContractType.Stealth;
            bot.Initialize(player.transform,i); enemies.Add(bot);
            var animation=o.AddComponent<ArmyAnimation>();
            if(!animation.Initialize(bot,visualRoot))
            {
                Destroy(animation);
                var walk=o.AddComponent<EnemyWalkAnimator>(); walk.Initialize(bot,visualRoot);
            }
            BuildEnemyWeapon(o, visualRoot, bot);
            o.AddComponent<EnemyHitboxes>().Initialize(visualRoot);
            bot.InitializePatrol();
        }
        void BuildEnemyVisual(GameObject root)
        {
            EnemyPart(PrimitiveType.Capsule,"Enemy torso",root,new Vector3(0,1.05f,0),new Vector3(.55f,.72f,.38f),enemyJacket,Quaternion.identity);
            EnemyPart(PrimitiveType.Sphere,"Enemy head",root,new Vector3(0,2.05f,0),Vector3.one*.34f,enemySkin,Quaternion.identity);
            EnemyPart(PrimitiveType.Cylinder,"Enemy left arm",root,new Vector3(-.42f,1.02f,0),new Vector3(.13f,.5f,.13f),enemyJacket,Quaternion.Euler(0,0,-18));
            EnemyPart(PrimitiveType.Cylinder,"Enemy right arm",root,new Vector3(.42f,1.02f,0),new Vector3(.13f,.5f,.13f),enemyJacket,Quaternion.Euler(0,0,18));
            EnemyPart(PrimitiveType.Cylinder,"Enemy left leg",root,new Vector3(-.18f,.38f,0),new Vector3(.16f,.48f,.16f),enemyDark,Quaternion.identity);
            EnemyPart(PrimitiveType.Cylinder,"Enemy right leg",root,new Vector3(.18f,.38f,0),new Vector3(.16f,.48f,.16f),enemyDark,Quaternion.identity);
        }
        static void EnemyPart(PrimitiveType type,string name,GameObject parent,Vector3 localPosition,Vector3 scale,Material material,Quaternion rotation)
        {
            var part=GameObject.CreatePrimitive(type); part.name=name; part.transform.SetParent(parent.transform,false); part.transform.localPosition=localPosition; part.transform.localScale=scale; part.transform.localRotation=rotation;
            part.GetComponent<Renderer>().sharedMaterial=material;
            var collider=part.GetComponent<Collider>();
            if(collider!=null) { collider.enabled=false; if(Application.isPlaying) Destroy(collider); else DestroyImmediate(collider); }
        }
        void BuildEnemyWeapon(GameObject root, Transform visualRoot = null, EnemyBot bot = null)
        {
            EnsureEnemyMaterials();
            Transform targetParent = visualRoot != null ? visualRoot : root.transform;

            // Check if model ALREADY has clean pre-attached gun
            if (targetParent != null)
            {
                foreach (var t in targetParent.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("attached_ak47") || n.Contains("enemy tactical rifle") || n.Contains("gun_low") || n.Contains("scar"))
                    {
                        return;
                    }
                }
                foreach (var r in targetParent.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || r.sharedMaterials == null) continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m != null && m.name.ToLowerInvariant().Contains("scar"))
                            return;
                    }
                }
            }

            // Exact suffixes: "handr" must never select LeftHandRing1.
            Transform rightHand = WeaponGripPose.Bone(targetParent,"righthand","handr","rightwrist","wristr","rhand");
            Transform leftHand = WeaponGripPose.Bone(targetParent,"lefthand","handl","leftwrist","wristl","lhand");
            Transform rightFingers = WeaponGripPose.Bone(targetParent,"righthandmiddle1","righthandindex1");

            var ak47=Resources.Load<GameObject>("Models/AK47");
            if(ak47!=null)
            {
                BuildImportedEnemyWeapon(ak47,targetParent,rightHand,leftHand,rightFingers,bot);
                return;
            }

            Transform attachParent = rightHand != null ? rightHand : targetParent;
            GameObject weapon = new GameObject("Enemy tactical rifle");
            weapon.transform.SetParent(attachParent, false);

            bool isImported = false;
            if (isImported)
            {
            }
            else
            {
                // Procedural AK-74 style rifle.
                // The rifle's LOCAL X axis = the barrel direction so the full ~0.55m length
                // is visible from BOTH front and side views (no matter which way enemy faces).
                // All sizes in metres; these are deliberately large enough to read at 50-100m.

                // --- Receiver body (main block) ---
                EnemyPart(PrimitiveType.Cube, "Rifle Receiver",
                    weapon, new Vector3(0f, 0f, 0f),
                    new Vector3(.52f, .11f, .10f), enemyWeapon, Quaternion.identity);

                // --- Barrel (extends from front of receiver) ---
                EnemyPart(PrimitiveType.Cylinder, "Rifle Barrel",
                    weapon, new Vector3(.44f, 0f, 0f),
                    new Vector3(.035f, .18f, .035f), enemyWeapon, Quaternion.Euler(0, 0, 90));

                // --- Handguard (over barrel, front half) ---
                EnemyPart(PrimitiveType.Cube, "Handguard",
                    weapon, new Vector3(.26f, -.005f, 0f),
                    new Vector3(.22f, .085f, .09f), enemyDark, Quaternion.identity);

                // --- Stock (extends rearward) ---
                EnemyPart(PrimitiveType.Cube, "Rifle Stock",
                    weapon, new Vector3(-.34f, -.01f, 0f),
                    new Vector3(.24f, .09f, .075f), enemyArmor, Quaternion.identity);
                EnemyPart(PrimitiveType.Cube, "Rifle Butt",
                    weapon, new Vector3(-.48f, -.045f, 0f),
                    new Vector3(.04f, .11f, .07f), enemyArmor, Quaternion.identity);

                // --- AK-style curved banana magazine (most identifiable part) ---
                EnemyPart(PrimitiveType.Cube, "Magazine",
                    weapon, new Vector3(-.04f, -.145f, 0f),
                    new Vector3(.085f, .21f, .075f), enemyArmor, Quaternion.Euler(0, 0, -8));

                // --- Pistol grip ---
                EnemyPart(PrimitiveType.Cube, "Grip",
                    weapon, new Vector3(-.18f, -.10f, 0f),
                    new Vector3(.06f, .16f, .058f), enemyDark, Quaternion.Euler(0, 0, 22));

                // --- Gas tube (above barrel) ---
                EnemyPart(PrimitiveType.Cylinder, "Gas Tube",
                    weapon, new Vector3(.22f, .07f, 0f),
                    new Vector3(.20f, .016f, .016f), enemyWeapon, Quaternion.Euler(0, 0, 90));

                // --- Muzzle (bright so it reads against sky/walls) ---
                EnemyPart(PrimitiveType.Cylinder, "Muzzle",
                    weapon, new Vector3(.575f, 0f, 0f),
                    new Vector3(.045f, .045f, .045f), enemyDark, Quaternion.Euler(0, 0, 90));

                // Force-apply weapon material to procedural gun parts
                if (enemyWeapon != null)
                    foreach (var rend in weapon.GetComponentsInChildren<Renderer>())
                        rend.sharedMaterial = enemyWeapon;
            }

            // --- Positioning ---
            // Calibrate in the visual root's coordinates before attaching to the
            // scaled hand bone, preserving world size when changing parents.
            weapon.transform.SetParent(targetParent, false);

            // Desired world size: receiver should be ~0.65m in world space
            float targetWorldSize = 0.9f;   // total rifle length in metres
            float receiverLocalSize = 1.12f; // complete stock-to-muzzle span, not receiver alone
            float rootLossy = Mathf.Max(targetParent.lossyScale.x, 0.0001f);
            float worldS = targetWorldSize / (rootLossy * receiverLocalSize);
            weapon.transform.localScale = Vector3.one * worldS;

            Vector3 procAimDir = targetParent.forward;
            Vector3 procGripPos = rightHand != null ? (rightHand.position + procAimDir * 0.05f - targetParent.up * 0.015f) : targetParent.TransformPoint(new Vector3(0.42f, 1.15f, 0.12f));
            weapon.transform.position = procGripPos;
            weapon.transform.rotation = Quaternion.LookRotation(procAimDir, targetParent.up) * Quaternion.Euler(0, -90, 0);
            var muzzleMarker=new GameObject("Enemy weapon muzzle").transform;
            muzzleMarker.SetParent(weapon.transform,false);muzzleMarker.localPosition=new Vector3(.62f,0,0);
            WeaponGripPose.Marker(weapon.transform, "WeaponSupport", new Vector3(.26f,-.04f,0));
            var anchor = weapon.AddComponent<EnemyWeaponAnchor>();
            anchor.Initialize(targetParent, rightHand, leftHand, rightFingers, bot, new Vector3(0, -90, 0));
            if(bot!=null) bot.RegisterWeapon(weapon.transform, muzzleMarker);
        }
        void BuildImportedEnemyWeapon(GameObject asset,Transform visual,Transform rightHand,Transform leftHand,Transform rightFingers,EnemyBot bot)
        {
            var holder=new GameObject("Enemy tactical rifle").transform;
            holder.SetParent(visual,false);
            var model=Instantiate(asset,holder,false).transform;
            var gripMarker=AssetCalibration.Marker(model,"Grip");
            var tipMarker=AssetCalibration.Marker(model,"Muzzle");
            Renderer stock=null,barrel=null,trigger=null;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                string name=renderer.name.ToLowerInvariant();
                if(name.StartsWith("stock")) stock=renderer;
                if(name.StartsWith("barrel")) barrel=renderer;
                if(name.StartsWith("trigger")) trigger=renderer;
                // The supplied FBX has missing texture references. Keep valid
                // textures, otherwise give its wood and metal separate finishes.
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                    if(materials[i]==null || materials[i].mainTexture==null)
                        materials[i]=name.Contains("wood")?enemyWeaponWood:enemyWeapon;
                renderer.sharedMaterials=materials;
            }
            foreach(var collider in model.GetComponentsInChildren<Collider>())
            { collider.enabled=false; if(Application.isPlaying) Destroy(collider); else DestroyImmediate(collider); }

            if(gripMarker!=null && tipMarker!=null)
            {
                var direction=holder.InverseTransformPoint(tipMarker.position)-holder.InverseTransformPoint(gripMarker.position);
                if(direction.sqrMagnitude>.000001f)
                    model.localRotation=Quaternion.FromToRotation(direction.normalized,Vector3.forward)*model.localRotation;
            }
            else
            {
                if(AssetCalibration.TryGet(model.name, out var profile))
                    model.localRotation=Quaternion.Euler(profile.rotation);
                else
                    model.localRotation=Quaternion.Euler(0, 180f, 0);
            }

            var bounds=ImportedVisual.LocalBounds(holder);
            model.localScale*=.88f/Mathf.Max(bounds.size.z,.001f);
            bounds=ImportedVisual.LocalBounds(holder);
            // Pistol grip is calibrated at 33% rifle length from stock (Z) and 26% height (Y)
            var grip=gripMarker!=null?holder.InverseTransformPoint(gripMarker.position)
                :trigger!=null ? ImportedVisual.RendererBounds(holder,trigger).center + new Vector3(0,-.025f,-.025f)
                :new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .45f, bounds.min.z + bounds.size.z * .33f);
            model.localPosition-=grip;
            bounds=ImportedVisual.LocalBounds(holder);
            var muzzle=new GameObject("Enemy weapon muzzle").transform;
            muzzle.SetParent(holder,false);
            muzzle.localPosition=tipMarker!=null?holder.InverseTransformPoint(tipMarker.position)
                :barrel!=null ? new Vector3(ImportedVisual.RendererBounds(holder,barrel).center.x,ImportedVisual.RendererBounds(holder,barrel).center.y,bounds.max.z)
                :new Vector3(bounds.center.x,bounds.center.y,bounds.max.z);

            WeaponGripPose.Markers(holder, .27f, .28f, muzzle.localPosition);

            // Dynamically anchor weapon to animated hands in LateUpdate across all stances and animations
            var anchor = holder.gameObject.AddComponent<EnemyWeaponAnchor>();
            anchor.Initialize(visual, rightHand, leftHand, rightFingers, bot);

            if(bot!=null) bot.RegisterWeapon(holder, muzzle);
        }
        void SetupEnvironmentLighting()
        {
            var sunObj = GameObject.Find("Directional Light") ?? GameObject.Find("Sun");
            Light sun = sunObj != null ? sunObj.GetComponent<Light>() : null;
            if (sun == null)
            {
                var mainLight = new GameObject("Sun Direct Light");
                sun = mainLight.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            if (cameraView != null) cameraView.farClipPlane = 2200f;
            if (Camera.main != null) Camera.main.farClipPlane = 2200f;

            ApplyEnvironment();
        }

        void EnsureEnemyMaterials()
        {
            if(enemyJacket!=null) return;
            SetupEnvironmentLighting();
            enemyJacket     = RuntimeMaterial("Enemy jacket",          new Color(.24f,.30f,.22f), .08f, .28f);
            civilianShirt   = RuntimeMaterial("Civilian shirt",         new Color(.22f,.45f,.35f), .05f, .30f);
            civilianPants   = RuntimeMaterial("Civilian pants",         new Color(.20f,.28f,.55f), .05f, .28f);
            enemySkin       = RuntimeMaterial("Enemy skin",             new Color(.82f,.68f,.58f), .02f, .40f);
            enemyArmor      = RuntimeMaterial("Enemy armor",            new Color(.16f,.18f,.20f), .35f, .55f);
            enemyHighlight  = RuntimeMaterial("Enemy armor highlight",  new Color(.28f,.32f,.32f), .45f, .60f);
            enemyDark       = RuntimeMaterial("Enemy boots and straps", new Color(.08f,.08f,.09f), .15f, .25f);
            enemyWeapon     = RuntimeMaterial("Enemy weapon metal",     new Color(.22f,.24f,.26f), .88f, .72f);
            enemyWeaponWood = RuntimeMaterial("AK47 walnut furniture", new Color(.30f,.12f,.04f), .05f, .45f);
        }
        Material RuntimeMaterial(string name,Color color,float metallic, float glossiness = 0.35f)
        {
            // Try Standard first, fall back to any lit shader available
            var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse") ?? Shader.Find("Unlit/Color");
            if(shader == null) { Debug.LogWarning("[GeoSniper] No shader found for material: "+name); return null; }
            var material = new Material(shader);
            ownedMaterials.Add(material);
            material.name = name;
            material.color = color;
            material.enableInstancing = true;
            if(material.HasProperty("_Metallic"))   material.SetFloat("_Metallic",   metallic);
            if(material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", glossiness);
            return material;
        }
        static Texture2D[] army1Textures;
        static Texture2D[] army2Textures;
        static Texture2D swatBodyTex;
        static Texture2D swatHeadTex;
        static Texture2D swatBodyNormal;
        static Texture2D swatHeadNormal;
        static Texture2D ch35_1001_diffuse;
        static Texture2D ch35_1002_diffuse;
        static Texture2D ch35_1003_diffuse;
        static Texture2D ch35_1001_normal;
        static Texture2D ch35_1002_normal;
        static Texture2D ch35_1003_normal;

        static void EnsureEnemyTextures()
        {
            if (swatBodyTex == null)
            {
                swatBodyTex = Resources.Load<Texture2D>("Models/Enemies/Textures/Soldier_Body_diffuse");
                swatHeadTex = Resources.Load<Texture2D>("Models/Enemies/Textures/Soldier_head_diffuse");
                swatBodyNormal = Resources.Load<Texture2D>("Models/Enemies/Textures/Soldier_Body_normal");
                swatHeadNormal = Resources.Load<Texture2D>("Models/Enemies/Textures/Soldier_head_normal");
            }
            if (ch35_1001_diffuse == null)
            {
                ch35_1001_diffuse = Resources.Load<Texture2D>("Models/Enemies/Textures/Ch35_1001_Diffuse");
                ch35_1002_diffuse = Resources.Load<Texture2D>("Models/Enemies/Textures/Ch35_1002_Diffuse");
                ch35_1003_diffuse = Resources.Load<Texture2D>("Models/Enemies/Textures/Ch35_1003_Diffuse");
                ch35_1001_normal = Resources.Load<Texture2D>("Models/Enemies/Textures/Ch35_1001_Normal");
                ch35_1002_normal = Resources.Load<Texture2D>("Models/Enemies/Textures/Ch35_1002_Normal");
                ch35_1003_normal = Resources.Load<Texture2D>("Models/Enemies/Textures/Ch35_1003_Normal");
            }
            if (army1Textures != null) return;
            var list1 = new List<Texture2D>();
            for (int i = 0; i <= 6; i++)
            {
                var tex = Resources.Load<Texture2D>("Models/Enemies/Textures/army_character_1_" + i);
                if (tex != null) list1.Add(tex);
            }
            army1Textures = list1.ToArray();

            var list2 = new List<Texture2D>();
            for (int i = 0; i <= 1; i++)
            {
                var tex = Resources.Load<Texture2D>("Models/Enemies/Textures/army_character_2_" + i);
                if (tex != null) list2.Add(tex);
            }
            army2Textures = list2.ToArray();
        }

        void ApplyEnemyMaterials(Transform root)
        {
            EnsureEnemyMaterials();
            EnsureEnemyTextures();

            // Detect character model type across root and all descendant transform names
            bool isSwat = false, isCh35 = false, isArmy1 = false, isArmy2 = false;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                string tn = tr.name.ToLowerInvariant();
                if (tn.Contains("swat") || tn.Contains("soldier_body") || tn.Contains("soldier_head")) isSwat = true;
                if (tn.Contains("ch35")) isCh35 = true;
                if (tn.Contains("army_character_1")) isArmy1 = true;
                if (tn.Contains("army_character_2")) isArmy2 = true;
            }

            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // Skip any renderer that is part of the weapon hierarchy
                var t = renderer.transform;
                bool isWeapon = false;
                while (t != null && t != root)
                {
                    if (t.name == "Enemy tactical rifle" || t.name.Contains("AK47") || t.name.Contains("Rifle") || t.name.Contains("ak47")) { isWeapon = true; break; }
                    t = t.parent;
                }
                if (isWeapon) continue;

                string rName = renderer.name.ToLowerInvariant();

                // 1. Mixamo SWAT (Soldier_body gets dark navy uniform/vest; Soldier_head gets balaclava/helmet)
                if (isSwat || rName.Contains("soldier"))
                {
                    bool isHead = rName.Contains("head");
                    var mat = RuntimeMaterial("EnemyTactical_" + renderer.name, 
                        isHead ? new Color(0.72f, 0.67f, 0.62f) : new Color(0.32f, 0.35f, 0.38f), 
                        isHead ? 0.04f : 0.03f, 
                        isHead ? 0.12f : 0.16f);
                    mat.mainTexture = isHead ? swatHeadTex : swatBodyTex;
                    var norm = isHead ? swatHeadNormal : swatBodyNormal;
                    if (norm != null)
                    {
                        mat.SetTexture("_BumpMap", norm);
                        mat.EnableKeyword("_NORMALMAP");
                    }
                    if (isHead && renderer is SkinnedMeshRenderer headSkin && headSkin.sharedMesh != null && headSkin.sharedMesh.subMeshCount >= 2)
                    {
                        var helmet = RuntimeMaterial("EnemyTactical_Helmet", new Color(0.48f, 0.52f, 0.55f), 0.03f, 0.12f);
                        helmet.mainTexture = swatBodyTex;
                        if (swatBodyNormal != null)
                        {
                            helmet.SetTexture("_BumpMap", swatBodyNormal);
                            helmet.EnableKeyword("_NORMALMAP");
                        }
                        renderer.sharedMaterials = new[] { helmet, mat };
                    }
                    else renderer.sharedMaterial = mat;
                    continue;
                }

                // 2. Mixamo Ch35 (3 sub-materials: 0=Head/Helmet, 1=Upper Body, 2=Lower Body)
                if (isCh35 || rName.Contains("ch35"))
                {
                    var mats = new Material[3];
                    mats[0] = RuntimeMaterial("Ch35_Head", Color.white, 0.02f, 0.12f);
                    mats[0].mainTexture = ch35_1001_diffuse;
                    mats[1] = RuntimeMaterial("Ch35_UpperBody", Color.white, 0.02f, 0.12f);
                    mats[1].mainTexture = ch35_1002_diffuse;
                    mats[2] = RuntimeMaterial("Ch35_LowerBody", Color.white, 0.02f, 0.12f);
                    mats[2].mainTexture = ch35_1003_diffuse;
                    renderer.sharedMaterials = mats;
                    continue;
                }

                // 3. Army characters
                if (isArmy1 || rName.Contains("army_character_1"))
                {
                    Texture2D enemyTex = null;
                    if (rName.Contains("object_2")) enemyTex = (army1Textures != null && army1Textures.Length > 0) ? army1Textures[0] : null;
                    else if (rName.Contains("object_3")) enemyTex = (army1Textures != null && army1Textures.Length > 2) ? army1Textures[2] : null;
                    else if (rName.Contains("object_4")) enemyTex = (army1Textures != null && army1Textures.Length > 1) ? army1Textures[1] : null;
                    else if (rName.Contains("object_5")) enemyTex = (army1Textures != null && army1Textures.Length > 3) ? army1Textures[3] : null;
                    else if (rName.Contains("object_6")) enemyTex = (army1Textures != null && army1Textures.Length > 4) ? army1Textures[4] : null;
                    else if (rName.Contains("object_7")) enemyTex = (army1Textures != null && army1Textures.Length > 5) ? army1Textures[5] : null;
                    else if (rName.Contains("object_8")) enemyTex = (army1Textures != null && army1Textures.Length > 6) ? army1Textures[6] : null;
                    else enemyTex = (army1Textures != null && army1Textures.Length > 0) ? army1Textures[0] : null;

                    if (enemyTex != null)
                    {
                        var mat = RuntimeMaterial("EnemyTactical_" + renderer.name, Color.white, 0.02f, 0.12f);
                        mat.mainTexture = enemyTex;
                        renderer.sharedMaterial = mat;
                        continue;
                    }
                }
                else if (isArmy2 || rName.Contains("army_character_2"))
                {
                    Texture2D enemyTex = null;
                    if (rName.Contains("object_2")) enemyTex = (army2Textures != null && army2Textures.Length > 0) ? army2Textures[0] : null;
                    else if (rName.Contains("object_3")) enemyTex = (army2Textures != null && army2Textures.Length > 1) ? army2Textures[1] : null;
                    else enemyTex = (army2Textures != null && army2Textures.Length > 1) ? army2Textures[1] : null;

                    if (enemyTex != null)
                    {
                        var mat = RuntimeMaterial("EnemyTactical_" + renderer.name, Color.white, 0.02f, 0.12f);
                        mat.mainTexture = enemyTex;
                        renderer.sharedMaterial = mat;
                        continue;
                    }
                }

                var imported = renderer.sharedMaterial;
                if (imported != null && imported.mainTexture != null) continue;

                string name = (renderer.name + " " + (imported == null ? "" : imported.name)).ToLowerInvariant();
                Material material = enemyArmor;
                if (name.Contains("head") || name.Contains("face") || name.Contains("skin") || name.Contains("object_4") || name.Contains("object_5")) material = enemySkin;
                else if (name.Contains("jacket") || name.Contains("arm") || name.Contains("body") || name.Contains("torso") || name.Contains("shirt") || name.Contains("object_3") || name.Contains("object_6")) material = enemyJacket;
                else if (name.Contains("rifle") || name.Contains("weapon") || name.Contains("barrel") || name.Contains("optic")) material = enemyWeapon;
                else if (name.Contains("vest") || name.Contains("helmet") || name.Contains("kneepad") || name.Contains("pouch") || name.Contains("object_7")) material = enemyArmor;
                else if (name.Contains("boot") || name.Contains("leg") || name.Contains("pant") || name.Contains("strap") || name.Contains("glove") || name.Contains("object_2") || name.Contains("object_8")) material = enemyDark;

                renderer.sharedMaterial = material;
            }
        }
        void SpawnFreeRoamPatrols()
        {
            if(mapSpawns==null) { for(int i=0;i<4;i++) SpawnEnemy(i); return; }
            int rooftopCount=0;
            if(rooftopSpawns!=null)
                foreach(var point in rooftopSpawns)
                {
                    if(rooftopCount>=2 || FlatDistance(point,player.transform.position)<28 || FlatDistance(point,player.transform.position)>125) continue;
                    if(!IsValidOutdoorSpawn(point, out var validRoof, true)) continue;
                    SpawnEnemy(enemies.Count, validRoof); rooftopCount++;
                }
            var candidates=new List<Vector3>();
            AddNearbyGrassPatrols(candidates);
            candidates.AddRange(mapSpawns);
            candidates.Sort((a,b)=>FlatDistance(a,player.transform.position).CompareTo(FlatDistance(b,player.transform.position)));
            int count=0;
            var chosenAngles=new List<float>();
            foreach(var point in candidates)
            {
                if(count>=6) break;
                if(FlatDistance(point,player.transform.position)<28 || FlatDistance(point,player.transform.position)>125) continue;

                // Prevent single-file collinear line: enforce angular dispersion around player
                Vector3 toPoint = point - player.transform.position;
                float angle = Mathf.Atan2(toPoint.z, toPoint.x) * Mathf.Rad2Deg;
                bool angleClash = false;
                foreach(float prev in chosenAngles)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(angle, prev)) < 35f) { angleClash = true; break; }
                }
                if (angleClash && candidates.Count > 10) continue;

                bool separated=true;
                foreach(var enemy in enemies) if(enemy!=null && FlatDistance(point,enemy.transform.position)<16) { separated=false; break; }
                if(!separated) continue;

                // Lateral sidewalk offset to prevent marching on the exact road centerline
                Vector3 side = Vector3.Cross(Vector3.up, toPoint.normalized);
                float lateralOffset = (count % 2 == 0 ? 1f : -1f) * Random.Range(3.5f, 6.5f);
                Vector3 staggered = point + side * lateralOffset;
                Vector3 spawnPos = point;
                if (IsValidOutdoorSpawn(staggered, out var validStaggered, false)) spawnPos = validStaggered;
                else if (IsValidOutdoorSpawn(point, out var validStreet, false)) spawnPos = validStreet;
                else continue;

                SpawnEnemy(count, spawnPos);
                chosenAngles.Add(angle);
                count++;
            }
            if(count==0)
            {
                Vector3 center=player.transform.position;
                Vector3 fwd = player != null ? player.transform.forward : Vector3.forward;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                for(int i=0;i<4;i++)
                {
                    float fanAngle = (i - 1.5f) * 35f;
                    Vector3 dir = Quaternion.Euler(0, fanAngle, 0) * fwd;
                    Vector3 fallbackPt = center + dir * Random.Range(32f, 52f);
                    if (IsValidOutdoorSpawn(fallbackPt, out var validFallback, false))
                        SpawnEnemy(i, validFallback);
                    else
                    {
                        var world = SectorWorld.LoadedWorlds.Count > 0 ? SectorWorld.LoadedWorlds[0] : null;
                        float gy = world != null ? world.Ground(fallbackPt.x, fallbackPt.z) : center.y;
                        SpawnEnemy(i, new Vector3(fallbackPt.x, gy + 0.15f, fallbackPt.z));
                    }
                }
            }
            
            // Spawn some civilians
            int civCount = 0;
            foreach(var point in candidates)
            {
                if(civCount>=5) break;
                if(FlatDistance(point,player.transform.position)<28 || FlatDistance(point,player.transform.position)>150) continue;
                bool separated=true;
                foreach(var enemy in enemies) if(enemy!=null && FlatDistance(point,enemy.transform.position)<10) { separated=false; break; }
                foreach(var civ in civilians) if(civ!=null && FlatDistance(point,civ.transform.position)<10) { separated=false; break; }
                if(!separated) continue;
                SpawnCivilian(civCount,point+Vector3.up); civCount++;
            }
            SpawnVehicles();
        }
        
        void SpawnCivilian(int i, Vector3? position=null)
        {
            var o=new GameObject("Fictional Civilian "+(i+1));
            o.transform.position=position??new Vector3((i%4-1.5f)*22,1.1f,30+(i/4)*35);
            if(!SectorWorld.DryFootprint(o.transform.position,.6f))
            {
                bool found=false;
                foreach(var sector in SectorWorld.LoadedWorlds)
                    if(sector!=null && sector.TryFindGeographicSpawn(o.transform.position,out var safe)
                        && SectorWorld.DryFootprint(safe,.6f))
                    {o.transform.position=safe;found=true;break;}
                if(!found){Destroy(o);Debug.LogWarning("Civilian spawn skipped: no supported dry land near target.");return;}
            }
            else if(position.HasValue && Physics.Raycast(o.transform.position+Vector3.up*4,Vector3.down,out var surface,12,~(1<<2),QueryTriggerInteraction.Ignore)
                && surface.collider.gameObject.name!="water")
                o.transform.position=surface.point+Vector3.up*.12f;
            
            Transform visualRoot=o.transform;
            var civAssets = ModelLibrary.Load("Civilians");
            if(civAssets != null && civAssets.Length > 0)
            {
                var imported = civAssets[i % civAssets.Length];
                var visual = ImportedVisual.CreateEnemy(imported, o.transform);
                ApplyCivilianMaterials(visual.transform);
                ImportedVisual.ConfigureCharacterSurfaces(visual.transform);
                visualRoot = visual.transform;
            }
            else
            {
                BuildCivilianVisual(o);
            }
            
            var bot = o.AddComponent<CivilianBot>(); bot.Initialize(i); civilians.Add(bot);
            var walk = o.AddComponent<EnemyWalkAnimator>(); walk.Initialize(bot, visualRoot);
            o.AddComponent<EnemyHitboxes>().Initialize(visualRoot);
        }
        
        static Texture2D[] civModernMenTextures;
        static Texture2D[] civTf2cTextures;
        static Texture2D[] civFemaleTextures;
        static Texture2D carAtlasTex;

        static void EnsureCivilianTextures()
        {
            if (civModernMenTextures != null) return;
            var listModern = new List<Texture2D>();
            for (int i = 0; i <= 16; i++)
            {
                var tex = Resources.Load<Texture2D>("Models/Civilians/Textures/civilian_modern_men_" + i);
                if (tex != null) listModern.Add(tex);
            }
            civModernMenTextures = listModern.ToArray();

            var listTf2c = new List<Texture2D>();
            for (int i = 0; i <= 2; i++)
            {
                var tex = Resources.Load<Texture2D>("Models/Civilians/Textures/civilian_tf2c_" + i);
                if (tex != null) listTf2c.Add(tex);
            }
            civTf2cTextures = listTf2c.ToArray();

            var listFemale = new List<Texture2D>();
            for (int i = 0; i <= 2; i++)
            {
                var tex = Resources.Load<Texture2D>("Models/Civilians/Textures/female_civilian_v1_" + i);
                if (tex != null) listFemale.Add(tex);
            }
            civFemaleTextures = listFemale.ToArray();
        }

        void ApplyCivilianMaterials(Transform visual)
        {
            EnsureCivilianTextures();
            EnsureEnemyMaterials();
            string visualName = visual.name.ToLowerInvariant();

            Texture2D fallbackTex = null;
            if (visualName.Contains("modern_men") && civModernMenTextures != null && civModernMenTextures.Length > 0)
                fallbackTex = civModernMenTextures[Random.Range(0, civModernMenTextures.Length)];
            else if (visualName.Contains("tf2c") && civTf2cTextures != null && civTf2cTextures.Length > 0)
                fallbackTex = civTf2cTextures[Random.Range(0, civTf2cTextures.Length)];
            else if (visualName.Contains("female") && civFemaleTextures != null && civFemaleTextures.Length > 0)
                fallbackTex = civFemaleTextures[Random.Range(0, civFemaleTextures.Length)];
            else if (civModernMenTextures != null && civModernMenTextures.Length > 0)
                fallbackTex = civModernMenTextures[0];

            Color[] civilianPaints = new Color[] {
                new Color(0.18f, 0.24f, 0.35f),
                new Color(0.22f, 0.22f, 0.25f),
                new Color(0.38f, 0.25f, 0.18f),
                new Color(0.20f, 0.32f, 0.24f),
                new Color(0.42f, 0.18f, 0.20f),
                new Color(0.15f, 0.18f, 0.28f)
            };

            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for(int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null) { materials[i] = civilianShirt; continue; }

                    var texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
                    if (texture == null && source.HasProperty("_MainTex")) texture = source.GetTexture("_MainTex");
                    if (texture == null) texture = fallbackTex;

                    Color baseCol = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                        : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;

                    if (texture == null && (baseCol.r > 0.82f && baseCol.g > 0.82f && baseCol.b > 0.82f))
                    {
                        string rName = (renderer.name + " " + source.name).ToLowerInvariant();
                        if (rName.Contains("head") || rName.Contains("face") || rName.Contains("skin"))
                            baseCol = enemySkin.color;
                        else
                            baseCol = civilianPaints[i % civilianPaints.Length];
                    }

                    var materialKey=(source,texture);
                    if (!civilianMaterialCache.TryGetValue(materialKey, out var compatible) || compatible == null)
                    {
                        compatible = RuntimeMaterial("Civilian_" + source.name, baseCol, 0.05f);
                        compatible.mainTexture = texture;
                        civilianMaterialCache[materialKey] = compatible;
                    }
                    materials[i] = compatible;
                }
                renderer.sharedMaterials = materials;
            }
        }
        void BuildCivilianVisual(GameObject root)
        {
            EnemyPart(PrimitiveType.Capsule,"Civilian torso",root,new Vector3(0,1.05f,0),new Vector3(.55f,.72f,.38f),civilianShirt,Quaternion.identity);
            EnemyPart(PrimitiveType.Sphere,"Civilian head",root,new Vector3(0,2.05f,0),Vector3.one*.34f,enemySkin,Quaternion.identity);
            EnemyPart(PrimitiveType.Cylinder,"Civilian left arm",root,new Vector3(-.42f,1.02f,0),new Vector3(.13f,.5f,.13f),civilianShirt,Quaternion.Euler(0,0,-18));
            EnemyPart(PrimitiveType.Cylinder,"Civilian right arm",root,new Vector3(.42f,1.02f,0),new Vector3(.13f,.5f,.13f),civilianShirt,Quaternion.Euler(0,0,18));
            EnemyPart(PrimitiveType.Cylinder,"Civilian left leg",root,new Vector3(-.18f,.38f,0),new Vector3(.16f,.48f,.16f),civilianPants,Quaternion.identity);
            EnemyPart(PrimitiveType.Cylinder,"Civilian right leg",root,new Vector3(.18f,.38f,0),new Vector3(.16f,.48f,.16f),civilianPants,Quaternion.identity);
        }

        void ApplyVehicleMaterials(Transform visual, bool isTank, bool isPolice)
        {
            if (carAtlasTex == null)
            {
                carAtlasTex = Resources.Load<Texture2D>("Models/Cars/Textures/cars_0");
            }

            Color[] carPaints = new Color[] {
                new Color(0.78f, 0.08f, 0.08f), // crimson gloss
                new Color(0.12f, 0.22f, 0.42f), // deep midnight blue
                new Color(0.15f, 0.15f, 0.16f), // metallic obsidian
                new Color(0.55f, 0.58f, 0.62f), // liquid silver
                new Color(0.85f, 0.65f, 0.12f)  // metallic gold
            };
            Color selectedPaint = carPaints[Random.Range(0, carPaints.Length)];

            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    Material mat;
                    if (isTank)
                    {
                        mat = RuntimeMaterial("TankArmor", new Color(0.25f, 0.29f, 0.21f), 0.45f);
                    }
                    else if (isPolice)
                    {
                        string rName = (renderer.name + " " + (source != null ? source.name : "")).ToLowerInvariant();
                        bool isWhiteDoor = rName.Contains("door") || rName.Contains("roof") || rName.Contains("white");
                        mat = RuntimeMaterial("PolicePaint", isWhiteDoor ? new Color(0.92f, 0.92f, 0.94f) : new Color(0.06f, 0.08f, 0.11f), 0.85f);
                    }
                    else
                    {
                        // Civilian car
                        string rName = (renderer.name + " " + (source != null ? source.name : "")).ToLowerInvariant();
                        if (rName.Contains("glass") || rName.Contains("window") || rName.Contains("windshield"))
                        {
                            mat = RuntimeMaterial("CarGlass", new Color(0.12f, 0.18f, 0.24f), 0.1f);
                            if (mat != null && mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.95f);
                        }
                        else if (carAtlasTex != null)
                        {
                            mat = RuntimeMaterial("CarAtlasMat", Color.white, 0.65f);
                            mat.mainTexture = carAtlasTex;
                        }
                        else
                        {
                            mat = RuntimeMaterial("CarGlossPaint", selectedPaint, 0.85f);
                        }
                    }
                    if (mat != null) materials[i] = mat;
                }
                renderer.sharedMaterials = materials;
            }
        }
        
        void SpawnVehicles()
        {
            int currentActive = TrafficVehicle.ActiveVehicles.Count;
            if (currentActive >= 36) return;
            int targetTotal = 36;
            int spawnedCount = currentActive;
            var carAssets = ModelLibrary.Load("Cars");
            var policePrefab = Resources.Load<GameObject>("Models/PoliceCar");
            var tankPrefab = Resources.Load<GameObject>("Models/Tank");

            var routeRoads=new List<TrafficRoadRoutes.Road>();
            var routeLookup=new Dictionary<List<Vector3>,TrafficRoadRoutes.Road>();
            foreach(var loaded in SectorWorld.LoadedWorlds)
            {
                if(loaded==null)continue;
                foreach(var localPath in loaded.RoadPaths)
                {
                    if(localPath.Count<2)continue;
                    loaded.RoadPathFeatures.TryGetValue(localPath,out var metadata);
                    var points=new List<Vector3>(localPath.Count);
                    foreach(var point in localPath)points.Add(loaded.transform.TransformPoint(point));
                    var entry=new TrafficRoadRoutes.Road{Points=points,Feature=metadata};
                    routeRoads.Add(entry);routeLookup[localPath]=entry;
                }
            }
            var routes=new TrafficRoadRoutes(routeRoads);
            foreach (var world in SectorWorld.LoadedWorlds)
            {
                if (world == null || world.RoadPaths == null) continue;
                foreach (var path in world.RoadPaths)
                {
                    if (path.Count < 2 || spawnedCount >= targetTotal) continue;
                    world.RoadPathFeatures.TryGetValue(path,out var roadFeature);
                    if(roadFeature!=null && MapFeatureStyle.RoadWidth(roadFeature)<3f)continue;
                    int permittedDirection=roadFeature!=null?MapFeatureStyle.TrafficDirection(roadFeature):0;
                    
                    // Spawn up to 2 vehicles per road on longer routes
                    int toSpawn = (permittedDirection==0 && path.Count >= 5 && spawnedCount + 2 <= targetTotal) ? 2 : 1;
                    for (int vIdx = 0; vIdx < toSpawn; vIdx++)
                    {
                        if (spawnedCount >= targetTotal) break;
                        GameObject modelTemplate = null;
                        bool isTank = false;
                        
                        if (stageMode && (activeContract.Type == CampaignContractType.Overwatch || activeContract.Type == CampaignContractType.Escape) && tankPrefab != null && Random.value < 0.20f)
                        {
                            modelTemplate = tankPrefab;
                            isTank = true;
                        }
                        else if (policePrefab != null && Random.value < 0.35f)
                        {
                            modelTemplate = policePrefab;
                        }
                        else if (carAssets != null && carAssets.Length > 0)
                        {
                            modelTemplate = carAssets[Random.Range(0, carAssets.Length)];
                        }

                        if (modelTemplate == null) continue;

                        var obj = new GameObject(isTank ? ("Tank_" + spawnedCount) : ("TrafficVehicle_" + spawnedCount));
                        obj.transform.SetParent(transform, true);
                        var visual = Instantiate(modelTemplate, obj.transform, false);

                        bool isPolice = (policePrefab != null && modelTemplate == policePrefab) || modelTemplate.name.ToLowerInvariant().Contains("police");

                        // Align model forward with Unity +Z and right-side-up (+Y up) with auto-safeguard
                        var bounds = ImportedVisual.AlignVehicle(obj, visual, isTank, isPolice);

                        float longest = Mathf.Max(bounds.size.x, bounds.size.z);
                        if (longest < 0.001f || !float.IsFinite(longest)) { Destroy(obj); continue; }

                        float targetLength = isTank ? 7.2f : 4.5f;
                        visual.transform.localScale *= (targetLength / longest);
                        bounds = ImportedVisual.LocalBounds(obj.transform);
                        visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                        bounds = ImportedVisual.LocalBounds(obj.transform);

                        ApplyVehicleMaterials(visual.transform, isTank, isPolice);

                        var col = obj.AddComponent<BoxCollider>();
                        col.center = bounds.center;
                        col.size = bounds.size;

                        var explosive = obj.AddComponent<ExplosiveProp>();
                        explosive.maxHealth = isTank ? 150f : 50f;
                        explosive.explosionRadius = isTank ? 14f : 9.5f;
                        explosive.explosionDamage = isTank ? 350f : 220f;

                        if(!isTank)obj.AddComponent<TrafficBrakeLights>().Configure(bounds,modelTemplate.name);
                        var vehicle = obj.AddComponent<TrafficVehicle>();
                        int startWaypoint = (vIdx == 0) ? 0 : (path.Count - 1);
                        int moveDir = permittedDirection!=0?permittedDirection:(vIdx == 0 ? 1 : -1);
                        if(permittedDirection!=0)startWaypoint=moveDir>0?0:path.Count-1;
                        var worldPath=routes.Build(routeLookup[path],startWaypoint,moveDir);
                        vehicle.Initialize(worldPath, 0, 1, 0);
                        if(!obj.activeSelf)continue;
                        spawnedCount++;
                    }
                }
            }
        }
        
        void EnsureMissionBillboard()
        {
            if (player == null) return;

            Transform pT = player.transform;
            Vector3 pPos = pT.position;
            Vector3 fwd = pT.forward;
            fwd.y = 0;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            // Check if any billboard is already in comfortable sniper view (20m to 160m, within ~50 deg cone)
            foreach (var bb in InGameBillboard.ActiveBillboards)
            {
                if (bb == null || (missionDynamicBillboard != null && bb.gameObject == missionDynamicBillboard)) continue;
                Vector3 toBb = bb.transform.position - pPos;
                float dist = toBb.magnitude;
                if (dist >= 18f && dist <= 160f)
                {
                    toBb.y = 0;
                    if (Vector3.Dot(fwd, toBb.normalized) > 0.60f)
                    {
                        AdManager.Instance?.LoadNativeBillboardAd();
                        return;
                    }
                }
            }

            if (missionDynamicBillboard != null) Destroy(missionDynamicBillboard);

            float preferredSign = 1f;
            if (enemies.Count > 0 && enemies[0] != null)
            {
                Vector3 toTarget = enemies[0].transform.position - pPos;
                preferredSign = Vector3.Dot(right, toTarget) >= 0 ? -1f : 1f;
            }

            Vector3 desiredPos = pPos + fwd * 60f + right * (18f * preferredSign);
            float groundY = pPos.y;
            if (Physics.Raycast(desiredPos + Vector3.up * 60f, Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;
            else
            {
                var world = SectorWorld.LoadedWorlds.Count > 0 ? SectorWorld.LoadedWorlds[0] : null;
                if (world != null) groundY = world.Ground(desiredPos.x, desiredPos.z);
            }

            float legH = Mathf.Clamp(pPos.y - groundY - 1.5f, 2.8f, 16f);
            Vector3 eyePos = pPos + Vector3.up * 1.6f;
            Vector3 boardCenter = new Vector3(desiredPos.x, groundY + legH + 2.0f, desiredPos.z);

            // If line of sight to preferred side is blocked by building, try opposite flank
            if (Physics.Linecast(eyePos, boardCenter, out _, ~0, QueryTriggerInteraction.Ignore))
            {
                float altSign = -preferredSign;
                Vector3 altDesired = pPos + fwd * 60f + right * (18f * altSign);
                float altGroundY = pPos.y;
                if (Physics.Raycast(altDesired + Vector3.up * 60f, Vector3.down, out var altHit, 120f, ~0, QueryTriggerInteraction.Ignore))
                    altGroundY = altHit.point.y;
                float altLegH = Mathf.Clamp(pPos.y - altGroundY - 1.5f, 2.8f, 16f);
                Vector3 altCenter = new Vector3(altDesired.x, altGroundY + altLegH + 2.0f, altDesired.z);

                if (!Physics.Linecast(eyePos, altCenter, out _, ~0, QueryTriggerInteraction.Ignore))
                {
                    desiredPos = altDesired;
                    groundY = altGroundY;
                    legH = altLegH;
                }
            }

            Vector3 boardBasePos = new Vector3(desiredPos.x, groundY, desiredPos.z);
            Vector3 toPlayer = (pPos - boardBasePos);
            toPlayer.y = 0;
            if (toPlayer.sqrMagnitude < 0.01f) toPlayer = -fwd;
            Quaternion rot = Quaternion.LookRotation(toPlayer.normalized);

            missionDynamicBillboard = new GameObject("Mission_Dynamic_Billboard");
            missionDynamicBillboard.transform.position = boardBasePos;
            missionDynamicBillboard.transform.rotation = rot;

            var darkMetal = RuntimeMaterial("Mission_Billboard_DarkMetal", new Color(0.22f, 0.24f, 0.27f), 0.5f, 0.3f);
            var rustMetal = RuntimeMaterial("Mission_Billboard_RustMetal", new Color(0.48f, 0.30f, 0.22f), 0.7f, 0.1f);

            float boardW = 8.0f;
            float boardH = 4.0f;
            float boardY = legH + (boardH * 0.5f);

            // Legs
            CreateBillboardPart("Leg_L", new Vector3(-2.8f, legH * 0.5f, 0f), new Vector3(0.32f, legH, 0.32f), darkMetal, missionDynamicBillboard.transform);
            CreateBillboardPart("Leg_R", new Vector3(2.8f, legH * 0.5f, 0f), new Vector3(0.32f, legH, 0.32f), darkMetal, missionDynamicBillboard.transform);

            // Kickstands
            var kickL = CreateBillboardPart("Kick_L", new Vector3(-2.8f, legH * 0.5f, -1.0f), new Vector3(0.2f, legH * 1.2f, 0.2f), rustMetal, missionDynamicBillboard.transform);
            kickL.transform.localRotation = Quaternion.Euler(28f, 0, 0);
            var kickR = CreateBillboardPart("Kick_R", new Vector3(2.8f, legH * 0.5f, -1.0f), new Vector3(0.2f, legH * 1.2f, 0.2f), rustMetal, missionDynamicBillboard.transform);
            kickR.transform.localRotation = Quaternion.Euler(28f, 0, 0);

            // Cross beam & back frame
            CreateBillboardPart("CrossBeam", new Vector3(0, legH, 0), new Vector3(boardW * 0.95f, 0.22f, 0.22f), darkMetal, missionDynamicBillboard.transform);
            CreateBillboardPart("BackFrame", new Vector3(0, boardY, -0.09f), new Vector3(boardW + 0.15f, boardH + 0.15f, 0.08f), darkMetal, missionDynamicBillboard.transform);

            // Catwalk & rail
            CreateBillboardPart("Catwalk", new Vector3(0, legH, 0.5f), new Vector3(boardW + 0.4f, 0.12f, 0.85f), darkMetal, missionDynamicBillboard.transform);
            CreateBillboardPart("Rail", new Vector3(0, legH + 0.65f, 0.85f), new Vector3(boardW + 0.4f, 0.08f, 0.08f), rustMetal, missionDynamicBillboard.transform);

            // Lamp arms & fixtures
            for (int i = -1; i <= 1; i++)
            {
                float xOff = i * 2.6f;
                CreateBillboardPart("Arm_" + i, new Vector3(xOff, boardY + boardH * 0.5f + 0.12f, 0.45f), new Vector3(0.08f, 0.08f, 0.9f), darkMetal, missionDynamicBillboard.transform);
                CreateBillboardPart("Lamp_" + i, new Vector3(xOff, boardY + boardH * 0.5f + 0.05f, 0.9f), new Vector3(0.42f, 0.16f, 0.32f), rustMetal, missionDynamicBillboard.transform);
            }

            // Display Screen with InGameBillboard component
            var screenPart = CreateBillboardPart("Billboard_Screen", new Vector3(0, boardY, 0), new Vector3(boardW, boardH, 0.14f), darkMetal, missionDynamicBillboard.transform);
            var billboard = screenPart.AddComponent<InGameBillboard>();
            billboard.Initialize(screenPart.GetComponent<MeshRenderer>(), 100);

            // Request AdMob Native Ad pre-load immediately
            AdManager.Instance?.LoadNativeBillboardAd();
        }

        static GameObject CreateBillboardPart(string partName, Vector3 localPos, Vector3 localScale, Material mat, Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = partName;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = localScale;
            cube.transform.localRotation = Quaternion.identity;
            var r = cube.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return cube;
        }

        public void BeginRange()
        {
            rangeMode = true;
            stageMode = false;
            isPvPDuel = false;
            freeRoam = true;
            currentMissionState = MissionState.None;
            player.Health.Invulnerable = true;
            foreach (var bot in enemies)
                if (bot != null) { bot.ObservationPatrol = true; bot.ReportBodies = false; }
        }

        public void BeginStage(int index)
        {
            RadioCommsChannel.ResetMissionDispatch();
            meeting=null;
            stageMode = true; stageIndex = Mathf.Clamp(index, 0, StageCount - 1);
            isPvPDuel=false;
            if(duelLaserLine!=null) duelLaserLine.enabled=false;
            ConfigureCampaign(CampaignNodeGraph.ForStage(stageIndex));
            activeContract = campaignNode!=null
                ? new CampaignContract(stageIndex,campaignNode.contractType,campaignNode.title,campaignNode.briefing)
                : CampaignContract.ForLevel(stageIndex);
            firstContact = null; contractSetupError = null; missionFailureReason=null; hasWaypointPin = false;
            if (player != null && player.Health != null)
            {
                player.Health.Initialize(100);
            }
            currentMissionState = MissionState.Briefing; victoryPending = false; victoryPendingTimer = 0f; stageRewarded = false; stageRewardsDoubled = false; hasUsedAdRevive = false; defeated = 0; ammo = weapon!=null?weapon.MaxAmmo:20; reloadTimer = 0; fireCooldown = 0.5f;
            totalShotsFired = 0; totalHitsScored = 0; totalHeadshotsScored = 0; missionDuration = 0f; victorySoundPlayed = false; victoryScreenTimer = 0f;
            if (extractionMarker != null) Destroy(extractionMarker);
            if (extractionHelicopter != null) Destroy(extractionHelicopter.gameObject);
            if (missionDynamicBillboard != null) Destroy(missionDynamicBillboard);
            LoadDailyContract();

            // Clear previous hostiles and civilians
            foreach (var enemy in enemies) if (enemy != null) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            enemies.Clear(); countedDeaths.Clear();
            foreach (var civ in civilians) if (civ != null) { civ.gameObject.SetActive(false); Destroy(civ.gameObject); }
            civilians.Clear();
            contractTarget = null;
            contractVIP = null;
            contractCounterSniper = null;
            contractModeTimer = 0f;
            extractionWaves=null;reinforcementPoints.Clear();
            contractVipPath.Clear();
            alerts.Reset();

            // Dedicated mode-specific tactical setups (each selects verified inner-city street corridor + vantage)
            try
            {
                switch (activeContract.Type)
                {
                    case CampaignContractType.TargetIdentification:
                        SetupModeTargetIdentification();
                        break;
                    case CampaignContractType.Overwatch:
                        SetupModeOverwatch();
                        break;
                    case CampaignContractType.TimedInterception:
                        SetupModeTimedInterception();
                        break;
                    case CampaignContractType.Stealth:
                        SetupModeStealthSyndicate();
                        break;
                    case CampaignContractType.Escape:
                        SetupModeHotExtraction();
                        break;
                    default:
                        SpawnFreeRoamPatrols();
                        break;
                }
            }
            catch (System.Exception setupEx)
            {
                Debug.LogWarning("[GeoSniper] Tactical stage setup failed: " + setupEx.Message);
                contractSetupError="No safe route here. Retry the contract or choose another sector.";
            }

            if (contractSetupError == null)
            {
                contractSetupError = ValidateContractSetup();
            }

            if (enemies.Count == 0 || contractSetupError != null)
            {
                contractSetupError=contractSetupError ?? "No hostile targets were generated. Retry or choose another sector.";
                FailMission(contractSetupError);
                if(player!=null)player.InputBlocked=true;
                return;
            }
            EnsurePlayableStageSpawns();
            if (activeContract.Type == CampaignContractType.Overwatch) EnsureVipAmbushSpacing();

            SpawnVehicles();
            if (player != null)
            {
                if (player.Health != null) player.Health.Invulnerable = false;
                player.InputBlocked = true;

                if (contractVIP != null)
                    player.Face(contractVIP.transform.position);
                else if (contractTarget != null)
                    player.Face(contractTarget.transform.position);
                else if (enemies.Count > 0 && enemies[0] != null)
                    player.Face(enemies[0].transform.position);
            }
            EnsureMissionBillboard();
        }

        string ValidateContractSetup()
        {
            if (enemies.Count == 0) return "No hostile targets were generated. Retry the mission.";
            switch (activeContract.Type)
            {
                case CampaignContractType.TargetIdentification:
                    return contractTarget == null ? "The marked target was not generated. Retry the mission." : null;
                case CampaignContractType.Overwatch:
                    return contractVIP == null || contractVipPath == null || contractVipPath.Count < 2
                        ? "The VIP route was not generated safely. Retry the mission." : null;
                case CampaignContractType.TimedInterception:
                    return contractTarget == null || !contractTarget.isFugitiveRunner || contractModeTimer <= 0f
                        ? "The courier route was not generated safely. Retry the mission." : null;
                case CampaignContractType.Stealth:
                    return alerts == null ? "The stealth alert system is unavailable. Retry the mission." : null;
                case CampaignContractType.Escape:
                    return contractModeTimer <= 0f ? "The extraction timer was not generated. Retry the mission." : null;
                default:
                    return null;
            }
        }

        void EnsurePlayableStageSpawns()
        {
            if (player == null) return;
            const float minimumSpawnDistance = 22f;
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.Actor == null || enemy.isFugitiveRunner) continue;
                if (FlatDistance(enemy.transform.position, player.transform.position) >= minimumSpawnDistance) continue;

                Vector3 replacement = Vector3.zero;
                float bestDistance = 0f;
                var candidates = new List<Vector3>();
                if (rooftopSpawns != null) candidates.AddRange(rooftopSpawns);
                candidates.AddRange(GetInnerGroundPoints());
                foreach (var candidate in candidates)
                {
                    float distance = FlatDistance(candidate, player.transform.position);
                    if (distance < minimumSpawnDistance || distance > 125f) continue;
                    if (enemy.isVipAttacker)
                    {
                        bool occupied = false;
                        foreach (var other in enemies)
                            if (other != null && other != enemy && other.isVipAttacker && FlatDistance(candidate, other.transform.position) < 8f)
                            { occupied = true; break; }
                        if (occupied) continue;
                        // Keep relocated guards near their assigned section of
                        // the VIP route, rather than choosing the same farthest
                        // street point for every guard.
                        float routeDistance = FlatDistance(candidate, enemy.transform.position);
                        if (routeDistance > 45f || (replacement != Vector3.zero && routeDistance >= bestDistance)) continue;
                        bestDistance = routeDistance;
                    }
                    else if (distance < bestDistance) continue;
                    if (!SupportedStandingPoint(candidate)) continue;
                    replacement = candidate;
                    if (!enemy.isVipAttacker) bestDistance = distance;
                }
                if (replacement != Vector3.zero)
                {
                    if (enemy.isVipAttacker) enemy.RelocateVipAmbushPost(replacement);
                    else enemy.transform.position = replacement;
                }
            }
        }

        List<Vector3> GetInnerGroundPoints()
        {
            var list = new List<Vector3>();
            var world = SectorWorld.LoadedWorlds.Count > 0 ? SectorWorld.LoadedWorlds[0] : null;
            if (mapSpawns != null)
            {
                foreach (var pt in mapSpawns)
                {
                    if (Mathf.Abs(pt.x) <= 165f && Mathf.Abs(pt.z) <= 165f && !SectorWorld.WaterAt(pt))
                    {
                        Vector3 p = pt;
                        float castY = world != null ? Mathf.Max(pt.y, world.Ground(pt.x, pt.z)) : pt.y;
                        if (Physics.Raycast(new Vector3(pt.x, castY + 30f, pt.z), Vector3.down, out var gHit, 60f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                            p = gHit.point + Vector3.up * 0.08f;
                        else if (world != null)
                            p = new Vector3(pt.x, world.Ground(pt.x, pt.z) + 0.12f, pt.z);
                        if(!SectorWorld.WaterAt(p)) list.Add(p);
                    }
                }
            }

            if (list.Count < 4)
            {
                for (int x = -60; x <= 60; x += 30)
                {
                    for (int z = -60; z <= 60; z += 30)
                    {
                        if(SectorWorld.WaterAt(new Vector3(x,0,z)))continue;
                        float gy = world != null ? world.Ground(x, z) : 0f;
                        Vector3 sample = new Vector3(x, gy + 30f, z);
                        if (Physics.Raycast(sample, Vector3.down, out var hit, 60f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                            list.Add(hit.point + Vector3.up * 0.08f);
                        else
                            list.Add(new Vector3(x, gy + 0.12f, z));
                    }
                }
            }
            return list;
        }

        void SelectSniperNest(Vector3 focusArea)
        {
            Vector3 chosen = Vector3.zero;
            float bestScore = -9999f;
            var world = SectorWorld.LoadedWorlds.Count > 0 ? SectorWorld.LoadedWorlds[0] : null;

            if(rooftopSpawns!=null) foreach (var roof in rooftopSpawns)
            {
                if (Mathf.Abs(roof.x) > 175f || Mathf.Abs(roof.z) > 175f) continue;
                float groundH = world != null ? world.Ground(roof.x, roof.z) : 0f;
                if (roof.y - groundH < 3.0f) continue;
                if (!SupportedStandingPoint(roof)) continue;

                float dist = FlatDistance(roof, focusArea);
                if (dist < 25f || dist > 180f) continue;

                bool hasLOS = ClearSniperTarget(roof, focusArea);
                if(!hasLOS) continue;
                float heightAboveGround = roof.y - groundH;
                float score = (hasLOS ? 600f : 0f) + (heightAboveGround * 3f) - (dist * 0.5f);
                if (world != null && world.HasStairs(roof)) score += 250f;
                if (score > bestScore)
                {
                    bestScore = score;
                    chosen = roof;
                }
            }

            if (bestScore <= -999f)
            {
                // A supported street position is preferable to a fabricated point in mid-air.
                foreach (var point in GetInnerGroundPoints())
                {
                    if (SupportedStandingPoint(point) && ClearSniperTarget(point,focusArea))
                    {
                        chosen=point; bestScore=0; break;
                    }
                }
            }

            if (bestScore <= -999f)
            {
                // Fallback: Pick highest supported rooftop near reasonable distance
                float bestFallbackDist = float.MaxValue;
                if (rooftopSpawns != null)
                {
                    foreach (var roof in rooftopSpawns)
                    {
                        if (Mathf.Abs(roof.x) > 180f || Mathf.Abs(roof.z) > 180f || roof.y < 4f) continue;
                        if (!SupportedStandingPoint(roof)) continue;
                        float dist = FlatDistance(roof, focusArea);
                        float scoreDiff = Mathf.Abs(dist - 65f) - roof.y * 2f;
                        if (scoreDiff < bestFallbackDist)
                        {
                            bestFallbackDist = scoreDiff;
                            chosen = roof;
                            bestScore = 1;
                        }
                    }
                }
            }

            if (bestScore <= -999f)
            {
                var innerPoints = GetInnerGroundPoints();
                if (innerPoints.Count > 0)
                {
                    chosen = innerPoints[0];
                }
                else
                {
                    chosen = focusArea + new Vector3(0, 0, -45f);
                    if (Physics.Raycast(chosen + Vector3.up * 10f, Vector3.down, out var gHit, 30f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                        chosen = gHit.point + Vector3.up * 0.1f;
                }
            }

            if (player != null)
            {
                if (world != null && chosen.y > world.Ground(chosen.x, chosen.z) + 2.5f) world.EnsureRooftopStairs(chosen);
                player.Place(chosen);
                player.Face(focusArea);
            }
        }

        static bool SupportedStandingPoint(Vector3 point)
        {
            return !SectorWorld.WaterAt(point) && Physics.Raycast(point+Vector3.up*.2f,Vector3.down,out var ground,.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                && ground.normal.y>=.7f
                && !Physics.CheckCapsule(point+Vector3.up*.5f,point+Vector3.up*1.5f,.35f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }

        bool ClearSniperTarget(Vector3 vantage, Vector3 point)
        {
            float distance = FlatDistance(vantage, point);
            if (distance < 25 || distance > 220) return false;
            return !Physics.Linecast(vantage + Vector3.up * 1.8f, point + Vector3.up * 1.5f, out var hit, ~(1 << 2), QueryTriggerInteraction.Ignore)
                   || Vector3.Distance(hit.point, point + Vector3.up * 1.5f) < 4f;
        }

        void SetupModeTargetIdentification()
        {
            var streetPoints = GetInnerGroundPoints();
            if (streetPoints.Count == 0)
            {
                streetPoints.Add(new Vector3(0, 0.1f, 0));
                streetPoints.Add(new Vector3(15, 0.1f, 15));
            }

            Vector3 hvtPos = streetPoints[Random.Range(0, streetPoints.Count)];
            if (rooftopSpawns != null && rooftopSpawns.Count > 0)
            {
                foreach (var sp in streetPoints)
                {
                    bool foundGoodNest = false;
                    foreach (var roof in rooftopSpawns)
                    {
                        if (roof.y >= 5f && FlatDistance(roof, sp) >= 30f && FlatDistance(roof, sp) <= 130f && ClearSniperTarget(roof, sp))
                        {
                            hvtPos = sp;
                            foundGoodNest = true;
                            break;
                        }
                    }
                    if (foundGoodNest) break;
                }
            }
            SelectSniperNest(hvtPos);

            // Central plaza node for HVT
            SpawnEnemy(enemies.Count, hvtPos);
            contractTarget = enemies[enemies.Count - 1];
            contractTarget.name = "HVT Syndicate Commander";
            contractTarget.health = 100;
            contractTarget.SetHealth(100);
            contractTarget.speed = 0f;

            Vector3 toPlayer = (player != null ? player.transform.position : Vector3.zero) - hvtPos;
            toPlayer.y = 0;
            if (toPlayer.sqrMagnitude > 0.01f)
                contractTarget.transform.rotation = Quaternion.LookRotation(toPlayer);

            // Bodyguards flanking HVT closely (protective escort formation)
            Vector3 rightDir = Vector3.Cross(Vector3.up, toPlayer.normalized);
            int guardCount=Mathf.Max(1,CampaignThreatBudget-1-(CampaignCounterSniper?1:0));
            for (int g = 0; g < guardCount; g++)
            {
                Vector3 offset=rightDir*((g%2==0?1:-1)*(2.8f+(g/2)*2.8f))-toPlayer.normalized*(1.5f+(g/2)*2f);
                Vector3 guardPos = hvtPos + offset;
                if (Physics.Raycast(guardPos + Vector3.up * 10f, Vector3.down, out var groundHit, 20f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    guardPos = groundHit.point + Vector3.up * 0.05f;

                SpawnEnemy(enemies.Count, guardPos);
                var guard = enemies[enemies.Count - 1];
                guard.name = "Syndicate Bodyguard " + (g + 1);
                guard.speed = 0f;
                if(offset.sqrMagnitude>.01f) guard.transform.rotation = Quaternion.LookRotation(offset);
            }

            // Introduce counter-snipers only after the first duel.
            if (CampaignCounterSniper && rooftopSpawns != null && rooftopSpawns.Count > 1)
            {
                foreach (var roof in rooftopSpawns)
                {
                    if (Mathf.Abs(roof.x) > 185f || Mathf.Abs(roof.z) > 185f) continue;
                    Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
                    float d = FlatDistance(roof, playerPos);
                    if (d >= 50f && d <= 140f && roof.y >= 7f && ClearSniperTarget(roof, playerPos))
                    {
                        SpawnEnemy(enemies.Count, roof);
                        if (enemies.Count > 0)
                        {
                            var sniper = enemies[enemies.Count - 1];
                            if (sniper != null)
                            {
                                sniper.name = "Hostile Counter-Sniper";
                                sniper.ConfigureAsCounterSniper();
                                contractCounterSniper = sniper;
                            }
                        }
                        break;
                    }
                }
            }

            // Ambient innocent civilians (6-8 pedestrians walking/standing nearby)
            int civTargetCount = campaignNode!=null?Mathf.Min(7,CampaignProgression.Tier(campaignNode)):7;
            int spawnedCivs = 0;
            foreach (var pt in streetPoints)
            {
                if (spawnedCivs >= civTargetCount) break;
                float d = FlatDistance(pt, hvtPos);
                if (d >= 12f && d <= 55f)
                {
                    SpawnCivilian(civilians.Count, pt);
                    spawnedCivs++;
                }
            }

            stageTarget = enemies.Count;
        }

        List<Vector3> BuildObjectiveRoute(List<Vector3> candidates,int maxPoints)
        {
            for(int attempt=0;attempt<12;attempt++)
            {
                var route=CampaignProgression.RunnerRoute(candidates,maxPoints,(a,b)=>
                    !Physics.Linecast(a+Vector3.up,b+Vector3.up,~0,QueryTriggerInteraction.Ignore));
                if(route.Count<2)continue;
                if(TrySafeRouteEnd(route[route.Count-1],out var end))
                {
                    route[route.Count-1]=end;
                    if(TryExpandWalkableRoute(route,out var walkable))return walkable;
                }
                if(TrySafeRouteEnd(route[0],out end))
                {
                    route.Reverse();
                    route[route.Count-1]=end;
                    if(TryExpandWalkableRoute(route,out var reversedWalkable))return reversedWalkable;
                }
            }
            return new List<Vector3>();
        }

        void EnsureVipAmbushSpacing()
        {
            var streetPoints = GetInnerGroundPoints();
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isVipAttacker) continue;
                bool crowded = false;
                for (int j = 0; j < i; j++)
                    if (enemies[j] != null && enemies[j].isVipAttacker
                        && FlatDistance(enemy.transform.position, enemies[j].transform.position) < 8f)
                    { crowded = true; break; }
                if (!crowded) continue;

                bool moved = false;
                foreach (var candidate in streetPoints)
                {
                    if (FlatDistance(candidate, enemy.transform.position) > 45f
                        || FlatDistance(candidate, player.transform.position) < 22f
                        || !SupportedStandingPoint(candidate)) continue;
                    bool free = true;
                    foreach (var other in enemies)
                        if (other != null && other != enemy && other.isVipAttacker
                            && FlatDistance(candidate, other.transform.position) < 8f)
                        { free = false; break; }
                    if (!free) continue;
                    enemy.RelocateVipAmbushPost(candidate);
                    moved = true;
                    break;
                }
                if (moved) continue;
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
                enemies.RemoveAt(i--);
            }
        }

        bool TryExpandWalkableRoute(List<Vector3> route,out List<Vector3> walkable)
        {
            walkable=new List<Vector3>();
            for(int i=0;i<route.Count-1;i++)
            {
                if(!NavMesh.SamplePosition(route[i],out var start,2f,NavMesh.AllAreas)
                    || !NavMesh.SamplePosition(route[i+1],out var finish,2f,NavMesh.AllAreas)
                    || FlatDistance(start.position,route[i])>2f
                    || FlatDistance(finish.position,route[i+1])>2f)
                    return false;
                var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(start.position,finish.position,NavMesh.AllAreas,path)
                    || path.status!=NavMeshPathStatus.PathComplete || path.corners.Length<2)return false;
                float direct=FlatDistance(route[i],route[i+1]);
                float pathLength=0f;
                for(int j=1;j<path.corners.Length;j++)
                {
                    var a=path.corners[j-1];var b=path.corners[j];
                    float leg=FlatDistance(a,b);
                    pathLength+=leg;
                    for(float d=0;d<=leg;d+=1.5f)
                        if(!SectorWorld.DryFootprint(Vector3.Lerp(a,b,leg>0?d/leg:0f),.5f))return false;
                }
                if(pathLength>Mathf.Max(85f,direct*2.5f))return false;
                if(walkable.Count==0)walkable.Add(route[i]);
                for(int j=1;j<path.corners.Length-1;j++)
                    if(FlatDistance(walkable[walkable.Count-1],path.corners[j])>1f)
                        walkable.Add(path.corners[j]);
                walkable.Add(route[i+1]);
            }
            return walkable.Count>=2;
        }

        bool TrySafeRouteEnd(Vector3 requested,out Vector3 end)
        {
            end=requested;
            if(SupportedStandingPoint(requested) && ValidExtractionFootprint(requested,4.5f))return true;
            foreach(var sector in SectorWorld.LoadedWorlds)
                if(sector!=null && sector.TryFindGeographicSpawn(requested,out var dry)
                    && FlatDistance(requested,dry)<=16f && Mathf.Abs(requested.y-dry.y)<=2.5f
                    && ValidExtractionFootprint(dry,4.5f))
                {end=dry;return true;}
            return false;
        }

        void SetupModeOverwatch()
        {
            contractVipPath.Clear();
            var streetPoints = GetInnerGroundPoints();
            if (streetPoints.Count < 2) return;

            // Select an optimal sniper rooftop nest that directly overlooks street points
            Vector3 sniperNest = Vector3.zero;
            bool hasNest = false;
            if (rooftopSpawns != null && rooftopSpawns.Count > 0)
            {
                float bestVantageScore = -1f;
                foreach (var roof in rooftopSpawns)
                {
                    if (Mathf.Abs(roof.x) > 165f || Mathf.Abs(roof.z) > 165f || roof.y < 5.5f || !SupportedStandingPoint(roof)) continue;
                    int visibleStreets = 0;
                    foreach (var sp in streetPoints)
                    {
                        float d = FlatDistance(roof, sp);
                        if (d >= 35f && d <= 90f && ClearSniperTarget(roof, sp)) visibleStreets++;
                    }
                    if (visibleStreets >= 2 && visibleStreets > bestVantageScore)
                    {
                        bestVantageScore = visibleStreets;
                        sniperNest = roof;
                        hasNest = true;
                    }
                }
            }

            List<Vector3> candidatePoints = streetPoints;
            if (hasNest)
            {
                var visibleStreets = streetPoints.FindAll(sp =>
                {
                    float d = FlatDistance(sniperNest, sp);
                    return d >= 30f && d <= 85f && ClearSniperTarget(sniperNest, sp);
                });
                if (visibleStreets.Count >= 2) candidatePoints = visibleStreets;
            }
            contractVipPath.AddRange(BuildObjectiveRoute(candidatePoints,4));
            if(contractVipPath.Count<2 && candidatePoints!=streetPoints)
                contractVipPath.AddRange(BuildObjectiveRoute(streetPoints,4));
            if(contractVipPath.Count<2){contractSetupError="No connected VIP route with a safe extraction site. Retry or choose another sector.";return;}

            Vector3 vipSpawn = contractVipPath[0];
            extractionPoint = contractVipPath[contractVipPath.Count - 1];

            // Vantage overlooks the center of the VIP route
            Vector3 routeCenter = (vipSpawn + extractionPoint) * 0.5f;
            if (hasNest)
            {
                player.Place(sniperNest);
                player.Face(routeCenter);
            }
            else
            {
                SelectSniperNest(routeCenter);
            }

            // Spawn extraction zone & police car
            if(!SpawnExtractionZone(extractionPoint)) { contractSetupError="No dry, supported VIP extraction site was found."; return; }

            // Spawn VIP with tactical health & steady speed
            int civilianCountBeforeVip=civilians.Count;
            SpawnCivilian(99, vipSpawn);
            if(civilians.Count==civilianCountBeforeVip){contractSetupError="No safe VIP starting position was found on dry land.";return;}
            contractVIP = civilians[civilians.Count - 1];
            contractVIP.name = "Allied VIP Operative";
            contractVIP.isVIP = true;
            contractVIP.vipDestination = extractionPoint;
            contractVIP.waypoints = new List<Vector3>(contractVipPath);
            contractVIP.waypointIndex = 0;
            contractVIP.walkSpeed = 2.0f;
            contractVIP.Actor.Initialize(100f * (difficulty != null ? difficulty.vipHealth : 1f));
            contractVIP.Actor.OnDamaged += (dmg) =>
            {
                shotNotice = "⚠️ ALLIED VIP UNDER ATTACK! DEFEND THE VIP!";
                shotNoticeTime = 2.5f;
            };

            // Give each ambusher a separate section of the route. NavMesh
            // corners are unevenly spaced, so indexing them clustered guards
            // at the same turn on short routes.
            int ambushCount = Mathf.Max(1, CampaignThreatBudget - (CampaignCounterSniper ? 1 : 0));
            for (int i = 0; i < ambushCount; i++)
            {
                SampleVipRoute((i + 1f) / (ambushCount + 1f), out var checkpoint, out var direction);
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                bool found = false;
                Vector3 pos = checkpoint;
                for (int attempt = 0; attempt < 12 && !found; attempt++)
                {
                    float width = 6f + (attempt / 4) * 3f;
                    float sign = ((i + attempt % 2) & 1) == 0 ? 1f : -1f;
                    float along = (attempt % 4 < 2 ? 0f : (attempt % 2 == 0 ? 4f : -4f));
                    Vector3 candidate = checkpoint + side * (sign * width) + direction * along;
                    if (!Physics.Raycast(candidate + Vector3.up * 8f, Vector3.down, out var hit, 16f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                    candidate = hit.point + Vector3.up * 0.08f;
                    if (Mathf.Abs(candidate.y - checkpoint.y) > 1.5f || !SupportedStandingPoint(candidate)
                        || !SectorWorld.DryFootprint(candidate, .6f)) continue;
                    bool separated = true;
                    foreach (var enemy in enemies)
                        if (enemy != null && enemy.isVipAttacker && FlatDistance(enemy.transform.position, candidate) < 8f)
                        { separated = false; break; }
                    if (!separated) continue;
                    pos = candidate;
                    found = true;
                }
                if (!found) continue;

                int beforeAmbush=enemies.Count;
                SpawnEnemy(beforeAmbush, pos);
                if(enemies.Count==beforeAmbush)continue;
                var ambusher = enemies[enemies.Count - 1];
                ambusher.name = "Syndicate Ambusher " + (i + 1);
                ambusher.ConfigureAsVipAmbusher(contractVIP, 14f + i * 7f);
            }

            // Counter-Sniper overlooking corridor
            if (CampaignCounterSniper && rooftopSpawns != null && rooftopSpawns.Count > 1)
            {
                foreach (var roof in rooftopSpawns)
                {
                    if (Mathf.Abs(roof.x) > 185f || Mathf.Abs(roof.z) > 185f) continue;
                    float d = FlatDistance(roof, player.transform.position);
                    if (d >= 50f && d <= 140f && roof.y >= 7f && ClearSniperTarget(roof, vipSpawn))
                    {
                        int beforeSniper=enemies.Count;
                        SpawnEnemy(beforeSniper, roof);
                        if(enemies.Count==beforeSniper)continue;
                        var sniper = enemies[enemies.Count - 1];
                        sniper.name = "Hostile Counter-Sniper";
                        sniper.ConfigureAsCounterSniper();
                        contractCounterSniper = sniper;
                        break;
                    }
                }
            }

            // Ambient civilians on nearby streets
            int civCount = 0;
            foreach (var pt in streetPoints)
            {
                if (civCount >= 5) break;
                float dFromRoute = FlatDistance(pt, routeCenter);
                if (dFromRoute >= 20f && dFromRoute <= 60f)
                {
                    SpawnCivilian(civCount, pt);
                    civCount++;
                }
            }

            stageTarget = enemies.Count;
        }

        void SetupModeTimedInterception()
        {
            contractModeTimer = 90f*(difficulty!=null?difficulty.missionTime:1f);
            var streetPoints = GetInnerGroundPoints();
            if (streetPoints.Count < 2) return;

            var runnerPath=BuildObjectiveRoute(streetPoints,Mathf.Clamp(CampaignThreatBudget,3,6));
            if(runnerPath.Count<2){contractSetupError="No connected courier route with a safe exit. Retry or choose another sector.";return;}
            Vector3 startPt=runnerPath[0];
            float routeLength=0;
            for(int i=1;i<runnerPath.Count;i++) routeLength+=Vector3.Distance(runnerPath[i-1],runnerPath[i]);

            extractionPoint = runnerPath[runnerPath.Count - 1];
            Vector3 corridorCenter = (startPt + extractionPoint) * 0.5f;
            SelectSniperNest(corridorCenter);

            if(!SpawnExtractionZone(extractionPoint)) { contractSetupError="No dry, supported courier extraction site was found."; return; }

            // Spawn Courier
            SpawnEnemy(0, startPt);
            if(enemies.Count==0){contractSetupError="No safe courier starting position was found on dry land.";return;}
            contractTarget = enemies[0];
            contractTarget.name = "Fleeing Syndicate Courier";
            contractTarget.isFugitiveRunner = true;
            contractTarget.runnerStartDelay = Mathf.Clamp(12f*(difficulty!=null?difficulty.missionTime:1f),10f,18f);
            contractTarget.speed = Mathf.Min(
                CampaignProgression.RunnerSpeed(campaignNode,difficulty,routeLength,Mathf.Clamp(CampaignThreatBudget,3,6)),
                routeLength/(55f*Mathf.Max(.5f,difficulty!=null?difficulty.enemySpeed:1f)));
            // Never author a route whose travel time is shorter than the runner's
            // actual movement time. Add a small reaction window for aiming.
            contractModeTimer = Mathf.Max(contractModeTimer,
                contractTarget.runnerStartDelay+routeLength/Mathf.Max(.1f,contractTarget.speed*(difficulty!=null?difficulty.enemySpeed:1f))+30f);
            contractTarget.SetHealth(75f);
            contractTarget.escapeDestination = extractionPoint;
            contractTarget.runnerWaypoints = runnerPath;
            contractTarget.runnerWaypointIndex = 0;

            // Spawn Suppressors along street nodes flanking the corridor
            for (int i = 1; i < CampaignThreatBudget; i++)
            {
                int checkpoint=1+(i-1)%(runnerPath.Count-1);
                Vector3 suppressorPos = runnerPath[checkpoint] + Vector3.Cross(Vector3.up, (extractionPoint - startPt).normalized) * (8f+3f*((i-1)/(runnerPath.Count-1))) * (i%2==0?-1:1);
                if (Physics.Raycast(suppressorPos + Vector3.up * 10f, Vector3.down, out var sHit, 20f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    suppressorPos = sHit.point + Vector3.up * 0.05f;

                int beforeSuppressor=enemies.Count;
                SpawnEnemy(beforeSuppressor, suppressorPos);
                if(enemies.Count==beforeSuppressor)continue;
                var suppressor = enemies[enemies.Count - 1];
                suppressor.name = "Syndicate Suppressor " + i;
                suppressor.isFugitiveRunner = false;
                suppressor.currentRole = CombatRole.Suppressor;
                suppressor.currentState = AIState.Patrol;
                suppressor.stateTimer = 0f;
                suppressor.speed = 1.5f;
            }

            // Ambient pedestrians
            int civCount = 0;
            foreach (var pt in streetPoints)
            {
                if (civCount >= 5) break;
                float d = FlatDistance(pt, startPt);
                if (d >= 15f && d <= 60f)
                {
                    SpawnCivilian(civCount, pt);
                    civCount++;
                }
            }

            stageTarget = enemies.Count;
        }

        void SetupModeStealthSyndicate()
        {
            alerts.Reset();
            var streetPoints = GetInnerGroundPoints();
            if (streetPoints.Count == 0) return;

            Vector3 districtCenter = streetPoints[0];
            SelectSniperNest(districtCenter);

            int targetCount = Mathf.Clamp(CampaignThreatBudget, 2, 6);
            var sentryPoints = new List<Vector3>();
            foreach (var pt in streetPoints)
            {
                bool farEnough = true;
                foreach (var sp in sentryPoints)
                {
                    if (Vector3.Distance(pt, sp) < 32f) { farEnough = false; break; }
                }
                if (farEnough)
                {
                    sentryPoints.Add(pt);
                    if (sentryPoints.Count >= targetCount) break;
                }
            }

            for (int i = 0; i < sentryPoints.Count; i++)
            {
                SpawnEnemy(enemies.Count, sentryPoints[i]);
                var sentry = enemies[enemies.Count - 1];
                sentry.name = "Syndicate Sentry " + (i + 1);
                sentry.currentState = AIState.Patrol;
                sentry.patrolRadius = 4f;
                sentry.detectionRange = 36f;
                sentry.speed = 1.6f;
            }

            int civCount = 0;
            foreach (var pt in streetPoints)
            {
                if (civCount >= 5) break;
                Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
                float dFromPlayer = FlatDistance(pt, playerPos);
                if (dFromPlayer >= 30f && dFromPlayer <= 120f)
                {
                    bool nearSentry = false;
                    foreach (var sp in sentryPoints)
                    {
                        if (FlatDistance(pt, sp) < 15f) { nearSentry = true; break; }
                    }
                    if (!nearSentry)
                    {
                        SpawnCivilian(civCount, pt);
                        civCount++;
                    }
                }
            }

            stageTarget = enemies.Count;
        }

        void SetupModeHotExtraction()
        {
            contractModeTimer = CampaignProgression.SurvivalSeconds(campaignNode,difficulty);
            extractionWaves=new ExtractionWavePlan(contractModeTimer,CampaignThreatBudget);
            waveSpawnCooldown=0;waveWarningElapsed=0;waveSpawnBlocked=0;warnedWave=0;
            var streetPoints = GetInnerGroundPoints();
            reinforcementPoints.Clear();reinforcementPoints.AddRange(streetPoints);
            if (streetPoints.Count == 0) return;

            SelectSniperNest(Vector3.zero);
            assaultAnchor=player.transform.position;

            int assaultCount = Mathf.Clamp(CampaignThreatBudget - (CampaignCounterSniper ? 1 : 0), 2, 5);
            int spawned = 0;
            Vector3 targetPlayerPos = player != null ? player.transform.position : Vector3.zero;
            foreach (var pt in streetPoints)
            {
                if (spawned >= assaultCount) break;
                float d = FlatDistance(pt, targetPlayerPos);
                if (d >= 25f && d <= 75f)
                {
                    bool separated = true;
                    foreach (var e in enemies)
                    {
                        if (e != null && FlatDistance(pt, e.transform.position) < 8f) { separated = false; break; }
                    }
                    if (!separated) continue;

                    int before=enemies.Count;
                    SpawnEnemy(before, pt);
                    if(enemies.Count==before)continue;
                    var rusher = enemies[enemies.Count - 1];
                    rusher.name = "Assault Rusher " + (spawned + 1);
                    rusher.currentRole = CombatRole.Assault;
                    rusher.BeginAssault(assaultAnchor);
                    rusher.speed = 3.2f;
                    spawned++;
                }
            }

            // Opposing sniper
            if (CampaignCounterSniper && rooftopSpawns != null && rooftopSpawns.Count > 1)
            {
                foreach (var roof in rooftopSpawns)
                {
                    if (Mathf.Abs(roof.x) > 185f || Mathf.Abs(roof.z) > 185f) continue;
                    float d = FlatDistance(roof, player.transform.position);
                    if (d >= 55f && d <= 130f && roof.y >= 7f && ClearSniperTarget(roof, player.transform.position))
                    {
                        SpawnEnemy(enemies.Count, roof);
                        var sniper = enemies[enemies.Count - 1];
                        sniper.name = "Hostile Counter-Sniper";
                        sniper.ConfigureAsCounterSniper();
                        contractCounterSniper = sniper;
                        break;
                    }
                }
            }

            stageTarget = enemies.Count;
        }

        void ConfigureCampaign(LevelNode node)
        {
            campaignNode=node;
            if(baselineDifficulty==null) baselineDifficulty=DifficultyProfile.Create(DifficultyProfile.Selected);
            if(difficulty!=null) Destroy(difficulty);
            difficulty=CampaignProgression.CreateProfile(baselineDifficulty,node);
        }

        public void PlayHQDispatchSound()
        {
            if (hqRadioSource == null)
            {
                hqRadioSource = gameObject.AddComponent<AudioSource>();
                hqRadioSource.spatialBlend = 0f;
                hqRadioSource.playOnAwake = false;
                hqRadioSource.volume = 1f;
            }
            if (RadioCommsChannel.TryPlayHQDispatch(hqRadioSource, out var dispatchCaption, out var clip))
            {
                shotNotice = "[HQ] " + dispatchCaption;
                shotNoticeTime = 5f;
                if (audioSource != null && clip != null) StartCoroutine(DuckMusicForHQ(clip.length));
            }
        }

        System.Collections.IEnumerator DuckMusicForHQ(float duration)
        {
            float originalVolume=audioSource.volume;
            audioSource.volume=originalVolume*.32f;
            yield return new WaitForSecondsRealtime(duration+.15f);
            if(audioSource!=null)audioSource.volume=originalVolume;
        }

        public void BeginPvPDuel(int nodeId = -1)
        {
            RadioCommsChannel.ResetMissionDispatch();
            var node=CampaignNodeGraph.GetNode(nodeId);
            ConfigureCampaign(node!=null && node.isPvPDuel?node:null);
            victoryPending=false; victoryPendingTimer=0f; stageRewarded=false; stageRewardsDoubled=false; hasUsedAdRevive=false;
            totalShotsFired=0; totalHitsScored=0; totalHeadshotsScored=0; missionDuration=0;
            victorySoundPlayed=false; victoryScreenTimer=0;
            firstContact=null; contractSetupError=null; missionFailureReason=null;
            LoadDailyContract();
            alerts.Reset();
            isPvPDuel = true;
            stageMode = false;
            freeRoam = false;
            currentMissionState = MissionState.InProgress;
            defeated = 0;
            ammo = weapon != null ? weapon.MaxAmmo : 10;
            reloadTimer = 0;
            fireCooldown = 0.5f;
            duelLockTimer = 0f;
            duelLevel = node != null ? Mathf.Clamp(node.difficultyTier / 3, 0, 3) : Mathf.Clamp(PlayerPrefs.GetInt("GeoSniper.DuelWins", 0), 0, 2);
            shotNotice = "SNIPER DUEL: ELIMINATE THE AI RIVAL!";
            shotNoticeTime = 4f;
            GameAnalyticsManager.TrackMissionStart(-1, "PvP_Duel", weapon != null ? weapon.CurrentWeaponIndex : 1);
            PlayHQDispatchSound();

            if (player != null)
            {
                player.Health.Initialize(100);
                player.Health.Invulnerable = false;
                player.InputBlocked = false;
            }

            foreach (var enemy in enemies) if (enemy != null) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            enemies.Clear(); countedDeaths.Clear();
            foreach (var civ in civilians) if (civ != null) { civ.gameObject.SetActive(false); Destroy(civ.gameObject); }
            civilians.Clear();

            Vector3 playerPos = Vector3.zero;
            Vector3 rivalPos = Vector3.zero;
            bool foundPair = false;

            if (rooftopSpawns != null && rooftopSpawns.Count >= 2)
            {
                var elevatedRoofs = new List<Vector3>();
                foreach (var r in rooftopSpawns)
                {
                    if (r.y >= 7f && SupportedStandingPoint(r))
                        elevatedRoofs.Add(r);
                }

                float bestScore = -1f;
                for (int i = 0; i < elevatedRoofs.Count; i++)
                {
                    for (int j = i + 1; j < elevatedRoofs.Count; j++)
                    {
                        float d = FlatDistance(elevatedRoofs[i], elevatedRoofs[j]);
                        if (d >= DuelMinimumSeparation && d <= 135f)
                        {
                            if (!Physics.Linecast(elevatedRoofs[i] + Vector3.up * 1.6f, elevatedRoofs[j] + Vector3.up * 1.6f, ~0, QueryTriggerInteraction.Ignore))
                            {
                                float score = (elevatedRoofs[i].y + elevatedRoofs[j].y) - Mathf.Abs(d - 100f) * 0.2f;
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    playerPos = elevatedRoofs[i];
                                    rivalPos = elevatedRoofs[j];
                                    foundPair = true;
                                }
                            }
                        }
                    }
                }
            }

            if (!foundPair)
            {
                // Some sectors have roofs that are too close, blocked, or not safely
                // walkable. Fall back to two real street positions rather than inventing
                // airborne rooftops; the duel remains playable and physically supported.
                var safePoints=GetInnerGroundPoints();
                for(int i=0;i<safePoints.Count && !foundPair;i++)
                {
                    if(!SupportedStandingPoint(safePoints[i])) continue;
                    for(int j=i+1;j<safePoints.Count;j++)
                    {
                        float d=FlatDistance(safePoints[i],safePoints[j]);
                        if(d<DuelMinimumSeparation || d>135f || !SupportedStandingPoint(safePoints[j])) continue;
                        if(Physics.Linecast(safePoints[i]+Vector3.up*1.6f,safePoints[j]+Vector3.up*1.6f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) continue;
                        playerPos=safePoints[i]; rivalPos=safePoints[j]; foundPair=true; break;
                    }
                }
                if(!foundPair)
                {
                    currentMissionState=MissionState.Failed;
                    contractSetupError="No safe duel positions here. Retry or choose another sector.";
                    if(player!=null) player.InputBlocked=true;
                    if(duelLaserLine!=null) duelLaserLine.enabled=false;
                    return;
                }
            }

            player.Place(playerPos);

            SpawnEnemy(0, rivalPos);
            if (enemies.Count > 0)
            {
                duelRivalSniper = enemies[0];
                duelRivalSniper.name = "Rival Elite Sniper";
                duelRivalSniper.health = 100;
                duelRivalSniper.detectionRange = 500;
                duelRivalSniper.engagementRange = 500;
                duelRivalSniper.speed = 0f;
                var actor = duelRivalSniper.GetComponent<CombatActor>();
                duelRivalSniper.SetHealth(100);
                if (FlatDistance(player.transform.position, duelRivalSniper.transform.position) < DuelMinimumSeparation)
                {
                    Vector3 replacement = FindDuelReplacement(player.transform.position);
                    if (replacement != Vector3.zero) duelRivalSniper.transform.position = replacement;
                }
                duelRivalHome=duelRivalSniper.transform.position;
                duelStrafePhase=UnityEngine.Random.Range(0f,Mathf.PI*2f);
                // Each completed duel raises movement pressure. The first duel
                // remains readable; later duels stop presenting a stationary target.
                duelRivalSniper.health=100f;
                duelRivalSniper.SetHealth(duelRivalSniper.health);

                var dir = player.transform.position - duelRivalSniper.transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f) duelRivalSniper.transform.rotation = Quaternion.LookRotation(dir);
                player.Face(duelRivalSniper.transform.position);

                if (duelLaserLine == null)
                {
                    var laserGo = new GameObject("RivalLaser");
                    duelLaserLine = laserGo.AddComponent<LineRenderer>();
                    duelLaserLine.positionCount = 2;
                    duelLaserLine.startWidth = 0.04f;
                    duelLaserLine.endWidth = 0.015f;
                    var mat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"));
                    mat.color = new Color(1f, 0.15f, 0.1f, 0.9f);
                    duelLaserLine.sharedMaterial = mat;
                }
                duelLaserLine.enabled = true;
            }
        }

        public void DisableAIRivalForMultiplayer()
        {
            if (duelRivalSniper != null)
            {
                duelRivalSniper.gameObject.SetActive(false);
            }
            if (duelLaserLine != null)
            {
                duelLaserLine.enabled = false;
            }
        }
        string StageTitle()
        {
            if(campaignNode!=null) return CampaignProgression.Label(campaignNode)+" / "+campaignNode.title;
            if(stageIndex==0) return "STAGE 1 / "+StageCount+"  |  FIRST CONTACT";
            return "STAGE "+(stageIndex+1)+" / "+StageCount+"  |  "+activeContract.Title.ToUpperInvariant();
        }
        void LoadDailyContract()
        {
            dailyKey=System.DateTime.UtcNow.ToString("yyyyMMdd");
            if(PlayerPrefs.GetString("GeoSniper.DailyKey","")!=dailyKey)
            {
                PlayerPrefs.SetString("GeoSniper.DailyKey",dailyKey);
                PlayerPrefs.SetInt("GeoSniper.DailyProgress",0);
                PlayerPrefs.SetInt("GeoSniper.DailyTarget",5+(System.DateTime.UtcNow.DayOfYear%4));
                PlayerPrefs.SetInt("GeoSniper.DailyClaimed",0); PlayerPrefs.Save();
            }
            dailyProgress=PlayerPrefs.GetInt("GeoSniper.DailyProgress",0);
            dailyTarget=PlayerPrefs.GetInt("GeoSniper.DailyTarget",5);
        }
        void RegisterKill()
        {
            LoadDailyContract();
            if(dailyProgress<dailyTarget)
            {
                dailyProgress++; PlayerPrefs.SetInt("GeoSniper.DailyProgress",dailyProgress); PlayerPrefs.Save();
            }
        }
        void RewardStage()
        {
            if(stageRewarded) return; stageRewarded=true;
            int activeNode = campaignNode!=null?campaignNode.id:-1;
            if (activeNode < 0 && FirstContactMode)
            {
                activeNode = 0;
            }
            if (activeNode >= 0)
            {
                CampaignNodeGraph.CompleteNode(activeNode);
            }
            if(stageMode) PlayerPrefs.SetInt("GeoSniper.StageUnlocked",Mathf.Max(PlayerPrefs.GetInt("GeoSniper.StageUnlocked",0),stageIndex+1));

            // Determine rewards based on active node or game mode
            LevelNode node = (activeNode >= 0) ? CampaignNodeGraph.GetNode(activeNode) : null;
            if (node != null)
            {
                rewardCash = node.rewardCash;
                rewardXP = node.rewardXP;
            }
            else if (isPvPDuel)
            {
                rewardCash = 500;
                rewardXP = 100;
            }
            else
            {
                rewardCash = Mathf.Max(250, (stageIndex + 1) * 75);
                rewardXP = Mathf.Max(100, (stageIndex + 1) * 100);
                if (FirstContactMode && firstContact != null)
                {
                    rewardCash = 200 + firstContact.BonusCredits;
                    rewardXP = 100 + firstContact.BonusXP;
                }
            }

            // Calculate star rating (1-3 stars)
            rewardStars = 1;
            float hpPct = (player != null && player.Health != null) ? (player.Health.Health / Mathf.Max(1,player.Health.maxHealth)) : 1f;
            if (totalHeadshotsScored > 0 || hpPct >= 0.5f) rewardStars++;
            if (totalHeadshotsScored >= 2 || (hpPct >= 0.8f && missionDuration < 90f)) rewardStars++;
            rewardStars = Mathf.Clamp(rewardStars, 1, 3);

            PlayerPrefs.SetInt("GeoSniper.XP",PlayerPrefs.GetInt("GeoSniper.XP",0)+rewardXP);
            PlayerPrefs.SetInt("GeoSniper.Credits",PlayerPrefs.GetInt("GeoSniper.Credits",0)+rewardCash);
            if(dailyProgress>=dailyTarget && PlayerPrefs.GetInt("GeoSniper.DailyClaimed",0)==0)
            {
                PlayerPrefs.SetInt("GeoSniper.DailyClaimed",1);
                PlayerPrefs.SetInt("GeoSniper.Credits",PlayerPrefs.GetInt("GeoSniper.Credits",0)+100);
                int today=System.DateTime.UtcNow.DayOfYear+System.DateTime.UtcNow.Year*366;
                int last=PlayerPrefs.GetInt("GeoSniper.LastStreakDay",-999);
                int streak=PlayerPrefs.GetInt("GeoSniper.Streak",0);
                PlayerPrefs.SetInt("GeoSniper.Streak",last==today-1?streak+1:1); PlayerPrefs.SetInt("GeoSniper.LastStreakDay",today);
            }
            PlayerPrefs.Save();
        }

        void TriggerMissionVictory(string title = "MISSION ACCOMPLISHED", string subtitle = "ALL THREATS ELIMINATED")
        {
            if (currentMissionState == MissionState.Complete || currentMissionState == MissionState.Failed || victoryPending) return;
            victoryHeader = title;
            victorySubheader = subtitle;
            victoryPending = true;
            victoryPendingTimer = (extractionHelicopter != null && extractionHelicopter.State == HelicopterPickup.HeliState.Outbound) ? 3.0f : (MeetingMode ? 2.8f : BallisticsSystem.isBulletCamActive ? 1.6f : 0.9f);
            shotNotice = title;
            shotNoticeTime = Mathf.Max(shotNoticeTime, .9f);
        }

        void FailMission(string reason)
        {
            if (currentMissionState == MissionState.Failed) return;
            currentMissionState = MissionState.Failed;
            missionFailureReason=reason;
            victoryPending=false;
            shotNotice = reason;
            shotNoticeTime = 3.5f;
            AdManager.Instance?.HideNativeBillboardAd();

            float acc = totalShotsFired > 0 ? (float)totalHitsScored / totalShotsFired : 0f;
            string cType = isPvPDuel ? "PvP_Duel" : (stageMode ? activeContract.Type.ToString() : "FreeRoam");
            GameAnalyticsManager.TrackMissionEnd(stageIndex, cType, false, missionDuration, 0, totalHeadshotsScored, acc, reason);
        }

        void CompletePendingVictory()
        {
            if (!victoryPending || currentMissionState==MissionState.Failed) return;
            if(player!=null && player.Health!=null && player.Health.IsDead) {FailMission("OPERATIVE KILLED IN ACTION");return;}
            victoryPending = false;
            currentMissionState = MissionState.Complete;
            showingDebriefing = false;
            victoryScreenTimer = 0f;
            AdManager.Instance?.HideNativeScreenAd();

            if (player != null && player.Scoped)
            {
                player.ToggleScope();
            }
            if (mapOpen) CloseMap();
            if (duelLaserLine != null) duelLaserLine.enabled = false;

            RewardStage();

            float acc = totalShotsFired > 0 ? (float)totalHitsScored / totalShotsFired : 0f;
            string cType = isPvPDuel ? "PvP_Duel" : (stageMode ? activeContract.Type.ToString() : "FreeRoam");
            GameAnalyticsManager.TrackMissionEnd(stageIndex, cType, true, missionDuration, rewardStars, totalHeadshotsScored, acc);

            if (!victorySoundPlayed)
            {
                victorySoundPlayed = true;
                var speaker = cameraView != null ? cameraView.GetComponent<AudioSource>() : audioSource;
                if (speaker != null) speaker.PlayOneShot(ProceduralAudio.CreateVictoryFanfare(), 0.95f);
            }
        }

        public void ExitToLevelMap()
        {
            showingDebriefing = false;
            AdManager.Instance?.HideNativeScreenAd();
            int activeNode = campaignNode!=null?campaignNode.id:-1;
            if (activeNode < 0 && FirstContactMode)
            {
                activeNode = 0;
            }
            var game = GetComponent<GeoSniperGame>();
            if (game != null)
            {
                game.ReturnToLevelMap(activeNode);
            }
            else
            {
                Destroy(this);
            }
        }
        
        void HandleDamageDealt(DamageInfo info)
        {
            if(!confirmedHit.Confirm(Time.unscaledTime,info.TotalDamage,info.isHeadshot)) return;
            if(!mapOpen && player!=null && player.Health!=null && !player.Health.IsDead) AndroidHitHaptics.ConfirmHit();
            totalHitsScored++;
            lastShotWasHeadshot = info.isHeadshot;
            if (info.isHeadshot) totalHeadshotsScored++;
            hitFlash = 0.25f;
            if (info.isHeadshot)
            {
                shotNotice = "🎯 HEADSHOT! +200 XP";
            }
            else if (info.bodyPart != null && (info.bodyPart.Contains("arm") || info.bodyPart.Contains("hand")))
            {
                shotNotice = "💥 ENEMY DISARMED! +120 XP";
            }
            else if (info.bodyPart != null && (info.bodyPart.Contains("leg") || info.bodyPart.Contains("foot")))
            {
                shotNotice = "🎯 MOBILITY KILL! +100 XP";
            }
            else
            {
                shotNotice = "💥 CRITICAL HIT! +100 XP";
            }
            shotNoticeTime = 1.6f;
            weapon?.ConfirmHit();
            SpawnHitSparks(info.hitPoint);
        }

        public void NotifySniperShotDodged()
        {
            shotNotice = "💨 RIVAL SNIPER ROUND DODGED!";
            shotNoticeTime = 2.0f;
        }
        void HandlePlayerDamaged(float amount)
        {
            damageFeedbackTimer = 1.0f;
            damageFeedbackStrength = Mathf.Clamp01(.4f + amount / 80f);
            hitFlash = .5f;
            threatArcTimer = 1.5f;
            if (player != null && player.Health != null)
            {
                lastThreatWorldPos = player.Health.LastDamageSourcePos;
            }
            shotNotice = "UNDER FIRE";
            shotNoticeTime = 1.8f;
        }
        void DrawDamageFeedback(float width, float height)
        {
            if (damageFeedbackTimer <= 0) return;
            float alpha = Mathf.Clamp01(damageFeedbackStrength * (damageFeedbackTimer / 0.75f));
            GUI.color = new Color(.85f, .03f, .02f, alpha * .24f);
            GUI.DrawTexture(new Rect(0, 0, width, 18), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, height - 18, width, 18), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, 18, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(width - 18, 0, 18, height), Texture2D.whiteTexture);

            if (threatArcTimer > 0 && lastThreatWorldPos != Vector3.zero && player != null)
            {
                Vector3 toThreat = lastThreatWorldPos - player.transform.position;
                toThreat.y = 0;
                if (toThreat.sqrMagnitude > 1f)
                {
                    Camera cam = Camera.main;
                    Transform refTransform = cam != null ? cam.transform : player.transform;
                    Vector3 forward = refTransform.forward; forward.y = 0;
                    if (forward.sqrMagnitude > 0.01f)
                    {
                        float angle = Vector3.SignedAngle(forward, toThreat, Vector3.up);
                        DrawThreatArcIndicator(width, height, angle, Mathf.Clamp01(threatArcTimer / 1.5f));
                    }
                }
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(width / 2 - 130, height * .68f, 260, 32), "UNDER FIRE", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, .35f, .25f, alpha) } });
        }

        void DrawThreatArcIndicator(float width, float height, float angle, float intensity)
        {
            var prevMat = GUI.matrix;
            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            GUIUtility.RotateAroundPivot(angle, center);

            float radius = Mathf.Min(width, height) * 0.28f;
            float cx = center.x;
            float cy = center.y - radius;
            float alpha = Mathf.Clamp01(intensity);

            GUI.color = new Color(1f, 0.15f, 0.1f, alpha * 0.88f);
            GUI.DrawTexture(new Rect(cx - 32, cy - 6, 64, 5), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 0.2f, 0.1f, alpha * 0.65f);
            GUI.DrawTexture(new Rect(cx - 48, cy + 3, 20, 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 28, cy + 3, 20, 4), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 0.85f, 0.2f, alpha * 0.95f);
            GUI.DrawTexture(new Rect(cx - 10, cy - 12, 20, 3), Texture2D.whiteTexture);

            GUI.matrix = prevMat;
            GUI.color = Color.white;
        }
        void HandleShotResolved(bool hitEnemy)
        {
            if(FirstContactMode) firstContact?.ResolveShot(hitEnemy);
            if(hitEnemy) return; // Damage events supply HIT/HEADSHOT feedback.
            shotNotice="MISS"; shotNoticeTime=.75f;
        }
        
        void HandleEnemyKilled(EnemyBot bot)
        {
            if (isCurrentKillKnife)
            {
                isCurrentKillKnife = false;
                shotNotice = "🗡️ GHOST KNIFE TAKEDOWN";
                shotNoticeTime = 2.2f;
                weapon?.ConfirmKill();
                return;
            }

            shotNotice = "☠️ ENEMY ELIMINATED";
            shotNoticeTime = 1.8f;
            weapon?.ConfirmKill();
            if (bot != null)
            {
                weapon?.TriggerXRayHitCam(bot.transform, bot.transform.position + Vector3.up * 1.5f);

                // Compute AAA Military Accolades
                float dist = player != null ? Vector3.Distance(player.transform.position, bot.transform.position) : 0f;
                float timeSinceLast = Time.time - lastKillTime;
                lastKillTime = Time.time;
                if (timeSinceLast < 4.2f) multiKillStreak++;
                else multiKillStreak = 1;

                string title = "HOSTILE ELIMINATED";
                string sub = "+100 XP";
                int bonusXP = 100;

                if (multiKillStreak >= 3)
                {
                    title = "RAPID MULTI-KILL";
                    bonusXP = 350;
                    sub = $"x{multiKillStreak} STREAK  •  +{bonusXP} XP";
                }
                else if (multiKillStreak == 2)
                {
                    title = "DOUBLE KILL";
                    bonusXP = 200;
                    sub = $"RAPID ENGAGEMENT  •  +{bonusXP} XP";
                }
                else if (bot.isCounterSniper)
                {
                    if (bot.IsAimingAtPlayer)
                    {
                        title = "🎯 COUNTER-SNIPED! (DUEL WON)";
                        bonusXP = 500;
                        sub = "ELIMINATED RIVAL BEFORE LOCK  •  +500 XP";
                    }
                    else
                    {
                        title = "COUNTER-SNIPER NEUTRALIZED";
                        bonusXP = 300;
                        sub = $"ROOFTOP THREAT ELIMINATED  •  +{bonusXP} XP";
                    }
                }
                else if (IsSoundMasked)
                {
                    title = "⚡ SOUND MASKED SNIPE";
                    bonusXP = 280;
                    sub = $"ACOUSTIC PHANTOM KILL  •  +{bonusXP} XP";
                }
                else if (dist >= 140f)
                {
                    title = $"LONG DISTANCE TAKEDOWN [{dist:F0}m]";
                    bonusXP = 250;
                    sub = $"EXTREME BALLISTIC SHOT  •  +{bonusXP} XP";
                }
                else if (lastShotWasHeadshot)
                {
                    title = "ONE SHOT ONE KILL";
                    bonusXP = 200;
                    sub = $"CLEAN HEADSHOT  •  +{bonusXP} XP";
                }
                else if (bot.currentState != AIState.Combat)
                {
                    title = "GHOST SNIPER TAKEDOWN";
                    bonusXP = 220;
                    sub = $"UNNOTICED LONG-RANGE KILL  •  +{bonusXP} XP";
                }

                TriggerAccolade(title, sub, bonusXP);
            }
        }

        void TriggerAccolade(string title, string subtitle, int xpBonus)
        {
            accoladeTitle = title;
            accoladeSubtitle = subtitle;
            accoladeTimer = 2.8f;
            int curXp = PlayerPrefs.GetInt("GeoSniper.XP", 0);
            PlayerPrefs.SetInt("GeoSniper.XP", curXp + xpBonus);
            if (audioSource != null)
            {
                audioSource.PlayOneShot(ProceduralAudio.CreateAccoladeStinger(), 0.85f);
            }
        }

        public bool CanKnifeKill(EnemyBot bot)
        {
            if (player == null || player.Health == null || player.Health.IsDead || player.InputBlocked || mapOpen
                || BallisticsSystem.isBulletCamActive || bot == null || bot.Actor == null || bot.Actor.IsDead || !bot.gameObject.activeInHierarchy) return false;
            Vector3 delta = bot.transform.position - player.transform.position;
            if (delta.magnitude > 3.2f || Mathf.Abs(delta.y) > 2.2f) return false;
            Vector3 forward = cameraView != null ? cameraView.transform.forward : player.transform.forward;
            if (Vector3.Dot(forward, delta.normalized) < .5f) return false;
            Vector3 from = cameraView != null ? cameraView.transform.position : player.transform.position + Vector3.up * 1.4f;
            Vector3 to = bot.transform.position + Vector3.up;
            foreach (var hit in Physics.RaycastAll(from, (to-from).normalized, Vector3.Distance(from,to), ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(player.transform) && !hit.transform.IsChildOf(bot.transform)) return false;
            return true;
        }

        public void ExecuteKnifeKill(EnemyBot bot)
        {
            if (!CanKnifeKill(bot)) return;
            isCurrentKillKnife = true;

            // 1. Drop scope
            if (player != null && player.Scoped) player.ToggleScope();

            // 2. Play Knife Audio (Slash whoosh + flesh stab impact)
            if (audioSource != null)
            {
                audioSource.PlayOneShot(ProceduralAudio.CreateKnifeSlash(), 1.0f);
                StartCoroutine(PlayDelayedKnifeStab(0.14f));
            }

            // 3. Knife blade slash animation with lethal blood impact
            if (weapon != null) weapon.TriggerKnifeSlash(true);

            // 4. Cinematic Micro-Bullet Time during stealth takedown
            StartCoroutine(KnifeTakedownSlowMo(0.24f));

            // 5. Heavy blood spray at throat / chest
            Vector3 stabPos = bot.transform.position + Vector3.up * 1.4f;
            Vector3 stabDir = player != null ? (bot.transform.position - player.transform.position).normalized : bot.transform.forward;
            BallisticsSystem.SpawnBloodSplatter(stabPos, stabDir);
            BallisticsSystem.SpawnBloodSplatter(stabPos + Vector3.up * 0.15f, stabDir);

            // 6. Silent lethal kill (no gunshot alert!)
            bot.Actor.Damage(9999f, player != null ? player.transform.position : bot.transform.position);

            // 7. Camera punch
            if (player != null) player.ApplyRecoil(-2.4f);

            // 8. Accolade stinger & banner
            TriggerAccolade("☠️ GHOST KNIFE TAKEDOWN", "SILENT MELEE ELIMINATION  •  +300 XP", 300);
        }

        System.Collections.IEnumerator PlayDelayedKnifeStab(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (audioSource != null) audioSource.PlayOneShot(ProceduralAudio.CreateKnifeStab(), 1.0f);
        }

        System.Collections.IEnumerator KnifeTakedownSlowMo(float duration)
        {
            Time.timeScale = 0.32f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1.0f;
        }

        public void ExecuteKnifeSlash()
        {
            if (player == null || player.Health == null || player.Health.IsDead) return;

            // 1. Drop scope if aiming
            if (player.Scoped) player.ToggleScope();

            // 2. Play Knife Audio (slash swoosh)
            if (audioSource != null)
            {
                audioSource.PlayOneShot(ProceduralAudio.CreateKnifeSlash(), 1.0f);
            }

            // 3. Melee spherecast forward (hit detection within 2.8m)
            Camera pCam = cameraView != null ? cameraView : Camera.main;
            bool hitEnemy = false;
            if (pCam != null)
            {
                Ray ray = new Ray(pCam.transform.position, pCam.transform.forward);
                if (Physics.SphereCast(ray, 0.45f, out RaycastHit hit, 2.8f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var bot = hit.collider.GetComponentInParent<EnemyBot>();
                    if (bot != null && bot.Actor != null && !bot.Actor.IsDead)
                    {
                        hitEnemy = true;
                        StartCoroutine(KnifeTakedownSlowMo(0.18f));
                        if (audioSource != null)
                        {
                            audioSource.PlayOneShot(ProceduralAudio.CreateKnifeStab(), 1.0f);
                        }
                        Vector3 stabPos = hit.point;
                        Vector3 stabDir = pCam.transform.forward;
                        BallisticsSystem.SpawnBloodSplatter(stabPos, stabDir);
                        bot.Actor.Damage(150f, player.transform.position);
                        if (bot.Actor.IsDead)
                        {
                            TriggerAccolade("🗡️ COMBAT KNIFE SLASH", "CLOSE QUARTERS ELIMINATION  •  +250 XP", 250);
                        }
                    }
                }
            }

            // 4. Knife blade slash animation
            if (weapon != null) weapon.TriggerKnifeSlash(hitEnemy);

            // 5. Tactical camera punch
            if (player != null) player.ApplyRecoil(-1.4f);
        }

        void HandleCivilianKilled(CivilianBot civ)
        {
            if (civ == null) return;
            if (civ == contractVIP)
            {
                FailMission("VIP KILLED IN ACTION - MISSION FAILED");
                return;
            }
            if (stageMode && activeContract.Title != null)
            {
                if (activeContract.Type == CampaignContractType.TargetIdentification)
                {
                    currentMissionState = MissionState.Failed;
                    shotNotice = "INNOCENT CIVILIAN CASUALTY - CONTRACT FAILED";
                    shotNoticeTime = 4f;
                    return;
                }
                else if (activeContract.Type == CampaignContractType.Stealth)
                {
                    currentMissionState = MissionState.Failed;
                    shotNotice = "CIVILIAN CASUALTY - MISSION COMPROMISED";
                    shotNoticeTime = 4f;
                    return;
                }
            }
            shotNotice = "⚠️ COLLATERAL CASUALTY: CIVILIAN KILLED (-150 XP)";
            shotNoticeTime = 3f;
            int curXp = PlayerPrefs.GetInt("GeoSniper.XP", 0);
            PlayerPrefs.SetInt("GeoSniper.XP", Mathf.Max(0, curXp - 150));
        }

        void HandleAccidentKill(Vector3 pos, string text)
        {
            shotNotice = text;
            shotNoticeTime = 3.5f;
            weapon?.ConfirmKill();
            TriggerAccolade("TACTICAL ENVIRONMENTAL KILL", text + "  •  +250 XP", 250);
        }

        void AddNearbyGrassPatrols(List<Vector3> candidates)
        {
            Vector3 center=player.transform.position;
            for(float radius=34;radius<=92;radius+=14)
                for(int i=0;i<12;i++)
                {
                    float angle=(i*.5235988f)+(radius*.017f);
                    Vector3 sample=center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                    if(TryGetOpenGround(sample,out var point)) candidates.Add(point);
                }
        }
        bool TryGetOpenGround(Vector3 sample,out Vector3 point)
        {
            point=default;
            if(!SectorWorld.DryFootprint(sample,.6f))return false;
            if(!Physics.Raycast(sample+Vector3.up*120,Vector3.down,out var hit,240)) return false;
            if(!hit.collider.name.Contains("Local terrain")) return false;
            point=hit.point+Vector3.up*.05f;
            return SectorWorld.DryFootprint(point,.6f)
                && !Physics.CheckCapsule(point+Vector3.up*.45f,point+Vector3.up*1.55f,.34f);
        }
        Vector3 FindDuelReplacement(Vector3 playerPosition)
        {
            var candidates = new List<Vector3>();
            if (rooftopSpawns != null) candidates.AddRange(rooftopSpawns);
            candidates.AddRange(GetInnerGroundPoints());
            Vector3 best = Vector3.zero; float bestScore = -1f;
            foreach (var candidate in candidates)
            {
                float distance = FlatDistance(candidate, playerPosition);
                if (distance < DuelMinimumSeparation || distance > 135f) continue;
                if (!SupportedStandingPoint(candidate)) continue;
                if (!ClearSniperTarget(candidate, playerPosition)) continue;
                if (distance > bestScore) { bestScore = distance; best = candidate; }
            }
            return best;
        }
        static float FlatDistance(Vector3 a,Vector3 b)
        {
            a.y=b.y=0; return Vector3.Distance(a,b);
        }

        public void CallHelicopterExtraction(string customNotice = null)
        {
            if (player == null || currentMissionState == MissionState.Extraction || currentMissionState == MissionState.Complete || currentMissionState == MissionState.Failed) return;

            currentMissionState = MissionState.Extraction;
            Vector3 playerPos = player.transform.position;
            Vector3 spawnDir = player.transform.forward;
            spawnDir.y = 0f;
            if (spawnDir.sqrMagnitude < 0.01f) spawnDir = Vector3.forward;
            spawnDir.Normalize();

            // Find valid rooftop spot in front of player on the same elevation level
            Vector3 bestLz = playerPos + spawnDir * 7.5f;
            bestLz.y = playerPos.y;
            bool foundLz = false;

            for (float dist = 5f; dist <= 12f; dist += 2f)
            {
                for (float ang = 0f; ang <= 60f; ang += 20f)
                {
                    for (int sign = (ang == 0f ? 1 : -1); sign <= 1; sign += 2)
                    {
                        Vector3 dir = Quaternion.Euler(0, ang * sign, 0) * spawnDir;
                        Vector3 probe = playerPos + dir * dist;
                        if (!SectorWorld.WaterAt(probe) && Physics.Raycast(probe + Vector3.up * 4f, Vector3.down, out var rHit, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                        {
                            if (Mathf.Abs(rHit.point.y - playerPos.y) < 1.8f)
                            {
                                bestLz = rHit.point;
                                foundLz = true;
                                break;
                            }
                        }
                    }
                    if (foundLz) break;
                }
                if (foundLz) break;
            }

            extractionPoint = bestLz;
            if(!SpawnExtractionZone(extractionPoint))
            {
                currentMissionState=MissionState.InProgress;
                shotNotice="NO SAFE DUSTOFF SITE HERE. MOVE TO OPEN GROUND AND RETRY.";
                shotNoticeTime=4f;
                return;
            }
            shotNotice = string.IsNullOrEmpty(customNotice) ? "🚁 DUSTOFF HELICOPTER INBOUND! MOVE TO EXTRACTION ROPE!" : customNotice;
            shotNoticeTime = 5.0f;
            TriggerAccolade("🚁 DUSTOFF INBOUND", "PROCEED TO FAST-ROPE FOR AIR EXTRACTION", 200);
        }

        bool SpawnExtractionZone(Vector3? customPos = null)
        {
            if (extractionMarker != null) Destroy(extractionMarker);
            if (extractionHelicopter != null) Destroy(extractionHelicopter.gameObject);
            extractionMarker=null;
            extractionHelicopter=null;
            if (customPos.HasValue)
            {
                extractionPoint = customPos.Value;
            }
            else
            {
                Vector3 dir = Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward;
                extractionPoint = player.transform.position + dir * 65f;
            }

            bool isHostileEscape = stageMode && activeContract.Type == CampaignContractType.TimedInterception;
            bool isVipExtraction = stageMode && activeContract.Type == CampaignContractType.Overwatch;
            float radius=(isVipExtraction || isHostileEscape)?4.5f:3.5f;
            Vector3 requested=extractionPoint;
            if(SectorWorld.LoadedWorlds.Count>0)
            {
                bool found=SupportedStandingPoint(requested) && ValidExtractionFootprint(requested,radius);
                foreach(var sector in SectorWorld.LoadedWorlds)
                    if(!found && sector!=null && sector.TryFindGeographicSpawn(requested,out var dry)
                        && FlatDistance(requested,dry)<=16f && Mathf.Abs(requested.y-dry.y)<=2.5f
                        && ValidExtractionFootprint(dry,radius))
                    {extractionPoint=dry;found=true;break;}
                if(!found){Debug.LogWarning("Extraction zone skipped: no dry supported landing footprint near target.");return false;}
            }
            else
            {
                if(!Physics.Raycast(requested+Vector3.up*50f,Vector3.down,out var gHit,100f,~(1<<2),QueryTriggerInteraction.Ignore)
                    || gHit.normal.y<.7f || gHit.collider.gameObject.name=="water")return false;
                extractionPoint=gHit.point;
                if(!ValidExtractionFootprint(extractionPoint,radius))return false;
            }

            extractionMarker = new GameObject(isHostileEscape ? "Hostile Escape Point" : "Extraction Zone");
            extractionMarker.transform.position = extractionPoint;

            // Spawn parked vehicle if applicable (Police Car for VIP, Getaway Sports Car for courier)
            if (isVipExtraction || isHostileEscape)
            {
                GameObject carPrefab = null;
                bool isPolice = false;
                if (isVipExtraction)
                {
                    carPrefab = Resources.Load<GameObject>("Models/PoliceCar");
                    isPolice = true;
                }
                else
                {
                    var carAssets = ModelLibrary.Load("Cars");
                    if (carAssets != null && carAssets.Length > 0)
                    {
                        carPrefab = carAssets[0];
                    }
                }

                if (carPrefab != null)
                {
                    var carRoot = new GameObject(isVipExtraction ? "Extraction_PoliceCar" : "Getaway_SportsCar");
                    carRoot.transform.SetParent(extractionMarker.transform, false);
                    carRoot.transform.localPosition = Vector3.zero;

                    var visual = Instantiate(carPrefab, carRoot.transform, false);
                    var bounds = ImportedVisual.AlignVehicle(carRoot, visual, false, isPolice);

                    float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    if (longest > 0.001f && float.IsFinite(longest))
                    {
                        visual.transform.localScale *= (4.5f / longest);
                        bounds = ImportedVisual.LocalBounds(carRoot.transform);
                        visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    }

                    ApplyVehicleMaterials(visual.transform, false, isPolice);

                    bounds = ImportedVisual.LocalBounds(carRoot.transform);
                    var col = carRoot.AddComponent<BoxCollider>();
                    col.center = bounds.center;
                    col.size = bounds.size;
                }
            }
            else
            {
                // Player Air Evacuation: Spawn UH-60M Black Hawk Helicopter with fast-rope extraction
                extractionHelicopter = HelicopterPickup.Spawn(extractionPoint, () => {
                    TriggerMissionVictory("EXTRACTION CONFIRMED", "AIR EVACUATION SUCCESSFUL");
                    shotNotice = "MISSION ACCOMPLISHED!";
                    shotNoticeTime = 3.5f;
                });
            }

            // Procedural disc ground mesh - zero primitives
            var discObj = new GameObject("ExtractionDisc");
            discObj.transform.SetParent(extractionMarker.transform, false);
            discObj.transform.localPosition = Vector3.up * 0.06f;

            var mf = discObj.AddComponent<MeshFilter>();
            var mr = discObj.AddComponent<MeshRenderer>();

            Mesh discMesh = new Mesh();
            int segments = 24;
            Vector3[] verts = new Vector3[segments + 1];
            int[] tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                verts[s + 1] = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                tris[s * 3] = 0;
                tris[s * 3 + 1] = s + 1;
                tris[s * 3 + 2] = (s == segments - 1) ? 1 : (s + 2);
            }
            discMesh.vertices = verts;
            discMesh.triangles = tris;
            discMesh.RecalculateNormals();
            mf.sharedMesh = discMesh;

            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = isHostileEscape ? new Color(1f, 0.35f, 0.1f, 0.45f) : new Color(0, 1f, 0.25f, 0.45f);
                mr.sharedMaterial = mat;
            }
            return true;
        }

        static bool ValidExtractionFootprint(Vector3 point,float radius)
        {
            if(!SectorWorld.DryFootprint(point,radius))return false;
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI/6f;
                Vector3 sample=point+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                if(!Physics.Raycast(sample+Vector3.up*4f,Vector3.down,out var hit,8f,~(1<<2),QueryTriggerInteraction.Ignore)
                    || hit.normal.y<.7f || hit.collider.gameObject.name=="water"
                    || Mathf.Abs(hit.point.y-point.y)>1f)return false;
            }
            return true;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (audioSource == null) return;
            if (hasFocus)
            {
                if (audioSource.clip != null && !audioSource.isPlaying) audioSource.UnPause();
            }
            else
            {
                if (audioSource.isPlaying) audioSource.Pause();
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && (currentMissionState==MissionState.InProgress || currentMissionState==MissionState.Extraction)) OpenMap();
            if (audioSource == null) return;
            if (pauseStatus)
            {
                if (audioSource.isPlaying) audioSource.Pause();
            }
            else
            {
                if (audioSource.clip != null && !audioSource.isPlaying) audioSource.UnPause();
            }
        }
        
        void Update()
        {
            Input.backButtonLeavesApp = false;
            if(audioSource!=null && ambientMusic!=null && !audioSource.isPlaying && !restarting && Application.isFocused)
            { audioSource.clip=ambientMusic; audioSource.loop=true; audioSource.Play(); }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (mapOpen) CloseMap();
                else if (currentMissionState == MissionState.Briefing) ExitToLevelMap();
                else if (currentMissionState == MissionState.InProgress || currentMissionState == MissionState.Extraction) OpenMap();
            }
            if(Input.GetKeyDown(KeyCode.M) && !mapSearchFocused) { if(mapOpen) CloseMap(); else OpenMap(); }
            if (mapOpen)
            {
                // 1. Android hardware / gesture back navigation
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseMap();
                    return;
                }

                float s = Mathf.Max(.4f, Mathf.Min(Screen.height / 720f, Screen.width / 800f));
                float w = Screen.width / s, h = Screen.height / s;
                float panel = w >= 1000 ? 280 : 210;
                float topResumeX = Mathf.Max(panel + 16 + 330, w - 240);

                // 2. High-priority touch detection for "RESUME GAME" button (top toolbar & bottom-right thumb)
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Ended)
                    {
                        Vector2 tp = new Vector2(t.position.x, Screen.height - t.position.y) / s;
                        if ((tp.x >= topResumeX - 12 && tp.x <= w && tp.y <= 68) ||
                            (tp.x >= w - 235 && tp.y >= h - 76) ||
                            (tp.x >= w - 240 && tp.y <= 70))
                        {
                            CloseMap();
                            return;
                        }
                    }
                }

                // 3. Mouse click in resume zones
                if (Input.GetMouseButtonDown(0))
                {
                    Vector2 mp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / s;
                    if ((mp.x >= topResumeX - 12 && mp.x <= w && mp.y <= 68) ||
                        (mp.x >= w - 235 && mp.y >= h - 76) ||
                        (mp.x >= w - 240 && mp.y <= 70))
                    {
                        CloseMap();
                        return;
                    }
                }
            }
            if(uavTimer > 0) uavTimer -= Time.deltaTime;
            if(uavCooldown > 0) uavCooldown -= Time.deltaTime;
            if(mapClosedCooldown > 0f) mapClosedCooldown -= Time.unscaledDeltaTime;

            // Direct touch detection for top-left ABORT button in combat HUD
            if (!mapOpen && mapClosedCooldown <= 0f && (currentMissionState == MissionState.InProgress || currentMissionState == MissionState.Extraction))
            {
                float s = Mathf.Max(.4f, Mathf.Min(Screen.height / 720f, Screen.width / 800f));
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Ended)
                    {
                        Vector2 tp = new Vector2(t.position.x, Screen.height - t.position.y) / s;
                        if (tp.x >= 0 && tp.x <= 145 && tp.y >= 0 && tp.y <= 70)
                        {
                            ExitToLevelMap();
                            return;
                        }
                    }
                }
            }

            if(!mapOpen)
            {
            if(Input.GetKeyDown(KeyCode.V)) ToggleThermalMode();
            if(Input.GetKeyDown(KeyCode.U)) TriggerUAVRecon();
            if(Input.GetKeyDown(KeyCode.N)) CycleEnvironment();
            if(Input.GetKeyDown(KeyCode.B))
            {
                if (player != null) player.ToggleBinoculars();
                shotNotice = (player != null && player.BinocularsMode) ? "🔭 RECON BINOCULARS ACTIVATED" : "RECON MODE DEACTIVATED";
                shotNoticeTime = 2.0f;
            }
            if(Input.GetKeyDown(KeyCode.K) && hasWaypointPin) CallAirStrike();
            }

            hitFlash=Mathf.Max(0,hitFlash-Time.deltaTime);
            damageFeedbackTimer=Mathf.Max(0,damageFeedbackTimer-Time.deltaTime);
            damageFeedbackStrength=Mathf.MoveTowards(damageFeedbackStrength,0,Time.deltaTime*2.8f);
            threatArcTimer=Mathf.Max(0,threatArcTimer-Time.deltaTime);
            shotNoticeTime=Mathf.Max(0,shotNoticeTime-Time.deltaTime);
            accoladeTimer=Mathf.Max(0,accoladeTimer-Time.deltaTime);
            if (victoryPending)
            {
                victoryPendingTimer -= Time.deltaTime;
                if (victoryPendingTimer <= 0f && !BallisticsSystem.isBulletCamActive) CompletePendingVictory();
            }
            if (!mapOpen && (currentMissionState == MissionState.InProgress || currentMissionState == MissionState.Extraction))
            {
                missionDuration += Time.deltaTime;
                UpdateSoundMasking();
            }
            if (currentMissionState == MissionState.Complete)
            {
                if (player != null && player.Scoped) player.ToggleScope();
                victoryScreenTimer += Time.deltaTime;
                if (!victorySoundPlayed)
                {
                    victorySoundPlayed = true;
                    var speaker = cameraView != null ? cameraView.GetComponent<AudioSource>() : audioSource;
                    if (speaker != null) speaker.PlayOneShot(ProceduralAudio.CreateVictoryFanfare(), 0.95f);
                }
            }
            foreach(var enemy in enemies) if(enemy!=null) enemy.Suspended=victoryPending || (isPvPDuel && enemy==duelRivalSniper) || mapOpen || restarting || (stageMode && currentMissionState!=MissionState.InProgress && currentMissionState!=MissionState.Extraction);
            foreach(var civ in civilians) if(civ!=null) civ.Suspended=mapOpen || restarting || (stageMode && currentMissionState!=MissionState.InProgress && currentMissionState!=MissionState.Extraction);
            UpdateMapZoom();
            if(player!=null)
            {
                player.InputBlocked=victoryPending || mapOpen || currentMissionState == MissionState.Briefing || currentMissionState == MissionState.Complete || currentMissionState == MissionState.Failed || (!freeRoam && currentMissionState == MissionState.None && defeated>=enemies.Count && enemies.Count>0);
                player.Controls.enabled=!victoryPending && !mapOpen && currentMissionState != MissionState.Briefing && currentMissionState != MissionState.Complete && currentMissionState != MissionState.Failed;
                player.Controls.DrawControlsInOnGUI=!victoryPending && !mapOpen && currentMissionState != MissionState.Briefing && currentMissionState != MissionState.Complete && currentMissionState != MissionState.Failed;
            }
            
            if(player==null || restarting)return;
            UpdateMeeting();
            if(MeetingMode && !meeting.ShotFired)
                if(contractTarget!=null) contractTarget.Suspended=true;
            
            if (weapon != null && (!mapOpen && player.Controls != null && player.Controls.FLIRPressed))
            {
                weapon.ThermalMode = !weapon.ThermalMode;
                AudioSource speaker = cameraView != null ? cameraView.GetComponent<AudioSource>() : null;
                if (speaker != null) speaker.PlayOneShot(ProceduralAudio.CreateThermalToggle(), 0.7f);
            }
            MobileCombatInput.FLIRActive = weapon != null && weapon.ThermalMode;
            
            if (player.Health.IsDead && currentMissionState != MissionState.Failed) {
                FailMission("OPERATIVE KILLED IN ACTION");
            }
            
            for(int i=0;i<enemies.Count;i++) if(enemies[i]!=null && enemies[i].GetComponent<CombatActor>().IsDead && countedDeaths.Add(enemies[i]))
            { defeated++; if(stageMode) RegisterKill(); StartCoroutine(RemoveFallenEnemy(enemies[i])); }
            
            if(!mapOpen && stageMode && !FirstContactMode && (currentMissionState == MissionState.InProgress || currentMissionState == MissionState.Extraction))
            {
                if (contractModeTimer > 0 && activeContract.Type!=CampaignContractType.Escape) contractModeTimer -= Time.deltaTime;

                if (activeContract.Type == CampaignContractType.TargetIdentification)
                {
                    bool hvtDead = contractTarget != null && contractTarget.GetComponent<CombatActor>().IsDead;
                    bool counterSniperDead = (contractCounterSniper == null || contractCounterSniper.GetComponent<CombatActor>().IsDead);
                    if (hvtDead && counterSniperDead && currentMissionState == MissionState.InProgress)
                    {
                        TriggerMissionVictory("HVT ELIMINATED", "ALL PRIMARY THREATS NEUTRALIZED");
                        shotNotice = "TARGET ELIMINATED - CONTRACT COMPLETED!";
                        shotNoticeTime = 3.5f;
                    }
                }
                else if (activeContract.Type == CampaignContractType.Overwatch)
                {
                    if (contractVIP == null || contractVIP.Actor == null || contractVIP.Actor.IsDead)
                    {
                        FailMission("VIP KILLED IN ACTION - MISSION FAILED");
                    }
                    else if (Vector3.Distance(contractVIP.transform.position, extractionPoint) < 5f)
                    {
                        TriggerMissionVictory("VIP EXTRACTED", "SAFELY REACHED EXTRACTION VEHICLE");
                        shotNotice = "VIP REACHED EXTRACTION VEHICLE - MISSION COMPLETE!";
                        shotNoticeTime = 3.5f;
                    }
                }
                else if (activeContract.Type == CampaignContractType.TimedInterception)
                {
                    if (contractTarget != null && contractTarget.GetComponent<CombatActor>().IsDead && currentMissionState == MissionState.InProgress)
                    {
                        TriggerMissionVictory("COURIER INTERCEPTED", "INTEL RETRIEVED SAFELY");
                        shotNotice = "FUGITIVE INTERCEPTED - MISSION COMPLETE!";
                        shotNoticeTime = 3.5f;
                    }
                    else if (contractModeTimer <= 0f || (contractTarget != null && contractTarget.runnerWaypoints!=null && contractTarget.runnerWaypointIndex>=contractTarget.runnerWaypoints.Count-1 && Vector3.Distance(contractTarget.transform.position, extractionPoint) < 5f))
                    {
                        FailMission("TARGET ESCAPED DISTRICT - MISSION FAILED");
                    }
                }
                else if (activeContract.Type == CampaignContractType.Stealth)
                {
                    int alertLimit = (difficulty != null) ? difficulty.stealthAlertLimit : 4;
                    if (alerts.Level >= alertLimit)
                    {
                        FailMission("ALARM SOUNDED - BASE LOCKDOWN - MISSION COMPROMISED!");
                    }
                    else if (defeated >= enemies.Count && enemies.Count > 0 && currentMissionState == MissionState.InProgress)
                    {
                        TriggerMissionVictory("GHOST CLEAR SECURED", "ALL TARGETS ELIMINATED SILENTLY");
                        shotNotice = "SECTOR SECURED IN SILENCE - MISSION COMPLETE!";
                        shotNoticeTime = 3.5f;
                    }
                }
                else if (activeContract.Type == CampaignContractType.Escape)
                {
                    UpdateExtractionWaves(Time.deltaTime);
                }
                else
                {
                    // Default elimination contracts
                    if (defeated >= enemies.Count && enemies.Count > 0 && currentMissionState == MissionState.InProgress)
                    {
                        TriggerMissionVictory("AREA SECURED", "ALL HOSTILES ELIMINATED");
                        shotNotice = "ALL TARGETS DOWN - CONTRACT COMPLETE!";
                        shotNoticeTime = 3.5f;
                    }
                }

                if (currentMissionState == MissionState.Extraction && player != null)
                {
                    if (Vector3.Distance(player.transform.position, extractionPoint) < 4.0f)
                    {
                        if (extractionHelicopter != null && extractionHelicopter.State == HelicopterPickup.HeliState.Hovering)
                        {
                            extractionHelicopter.TriggerBoarding();
                        }
                    }
                }
            }

            if (!stageMode && !isPvPDuel && currentMissionState == MissionState.InProgress && defeated >= enemies.Count && enemies.Count > 0)
            {
                TriggerMissionVictory("DISTRICT CLEARED", "ALL HOSTILE FORCES NEUTRALIZED");
                shotNotice = "ZONE CLEARED!";
                shotNoticeTime = 3.5f;
            }

            if (isPvPDuel && currentMissionState == MissionState.InProgress)
            {
                if (GeoSniper.Duel.SniperDuelManager.Instance != null && GeoSniper.Duel.SniperDuelManager.Instance.Opponent != null)
                {
                    DisableAIRivalForMultiplayer();
                    return;
                }
                if (duelRivalSniper != null && duelRivalSniper.GetComponent<CombatActor>().IsDead)
                {
                    if(!stageRewarded) PlayerPrefs.SetInt("GeoSniper.DuelWins",PlayerPrefs.GetInt("GeoSniper.DuelWins",0)+1);
                    TriggerMissionVictory("SNIPER DUEL WON", "RIVAL SNIPER ELIMINATED");
                    shotNotice = "VICTORY! +$"+rewardCash+" CASH +"+rewardXP+" XP";
                    shotNoticeTime = 4f;
                }
                else if (player != null && duelRivalSniper != null)
                {
                    if (player.Health.IsDead)
                    {
                        FailMission("ELIMINATED BY RIVAL SNIPER");
                        if (duelLaserLine != null) duelLaserLine.enabled = false;
                    }
                    else
                    {
                        if(duelLevel>0 && !mapOpen)
                        {
                            // Keep the rival readable and beatable. Movement is a
                            // telegraphed side-step, not a constant jitter target.
                            float strafe=Mathf.Sin(Time.time*(.45f+duelLevel*.18f)+duelStrafePhase)*(.8f+duelLevel*.7f);
                            Vector3 desired=duelRivalHome+duelRivalSniper.transform.right*strafe;
                            if(SupportedStandingPoint(desired)) duelRivalSniper.transform.position=desired;
                        }
                        Vector3 rivalMuzzle = duelRivalSniper.transform.position + Vector3.up * 1.4f;
                        Vector3 playerChest = player.transform.position + Vector3.up * 1.2f;
                        bool clear=!Physics.Linecast(rivalMuzzle,playerChest,out var cover,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                            || cover.transform==player.transform || cover.transform.IsChildOf(player.transform);
                        clear=clear && !mapOpen && !restarting;
                        if (duelLaserLine != null)
                        {
                            duelLaserLine.enabled = clear;
                            duelLaserLine.SetPosition(0, rivalMuzzle);
                            duelLaserLine.SetPosition(1, playerChest);
                        }
                        var rivalDir = player.transform.position - duelRivalSniper.transform.position;
                        rivalDir.y = 0;
                        if (rivalDir.sqrMagnitude > 0.01f) duelRivalSniper.transform.rotation = Quaternion.LookRotation(rivalDir);

                        duelLockTimer = clear?duelLockTimer+Time.deltaTime:0;
                        float lockReq = Mathf.Max(3.2f, (difficulty != null ? difficulty.duelLockSeconds : 3.5f));
                        if (duelLockTimer > lockReq)
                        {
                            duelLockTimer = 0f;
                            player.Health.Damage(28f * Mathf.Min(1.2f, (difficulty != null ? difficulty.enemyDamage : 1f)), duelRivalSniper.transform.position);
                            hitFlash = 0.5f;
                            shotNotice = "⚠️ UNDER FIRE - ENEMY SNIPER HIT YOU!";
                            shotNoticeTime = 2f;
                        }
                    }
                }
            }
            
            if(FirstContactMode) UpdateFirstContact();

            if (player.InputBlocked || player.Health.IsDead)
            {
                weapon?.Animate(true,Mathf.Max(reloadTimer,chamberTimer),false);
                if(sway!=null) sway.UpdateSway(false,false);
                fireBufferTimer=0;
                return;
            }
            bool isMoving = player != null && player.SpeedMetersPerSecond > 0.1f;
            if(weapon!=null) weapon.Animate(mapOpen || (player!=null && player.Scoped), Mathf.Max(reloadTimer, chamberTimer), isMoving, player != null && player.Sprinting, player != null && player.IsCrouching, player != null ? player.SpeedMetersPerSecond : 0f);
            
            if (player != null && player.Scoped)
            {
                if (Physics.Raycast(cameraView.transform.position, cameraView.transform.forward, out var distHit, 1000f))
                    weapon?.SetTargetDistance(distHit.distance);
                else
                    weapon?.SetTargetDistance(0f);
                    
                
            }
            
            if (sway != null) sway.UpdateSway(isMoving, !mapOpen && player.Scoped && player.IsHoldingBreath);
            if(reloadTimer>0) { reloadTimer-=Time.deltaTime; if(reloadTimer<=0) ammo=weapon!=null?weapon.MaxAmmo:20; }
            if(chamberTimer>0) { chamberTimer-=Time.deltaTime; }
            if(!mapOpen && player.Controls!=null && (player.Controls.ReloadPressed || Input.GetKeyDown(KeyCode.R)) && ammo<(weapon!=null?weapon.MaxAmmo:20) && reloadTimer<=0 && chamberTimer<=0)
            {
                float reloadSeconds=weapon==null?1.4f:(weapon.CurrentWeaponIndex==0?2.2f:weapon.CurrentWeaponIndex==1?1.45f:1.1f);
                reloadTimer=reloadSeconds; weapon?.ReloadSound(reloadSeconds);
            }
            
            // Sync weapon dock from mobile/touch controls + keyboard hotkeys (1, 2, 3)
            if (player != null && !mapOpen && (player.Health == null || !player.Health.IsDead))
            {
                if ((Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) && IsWeaponOwned(0)) SelectWeapon(0);
                if ((Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) && IsWeaponOwned(1)) SelectWeapon(1);
                if ((Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) && IsWeaponOwned(2)) SelectWeapon(2);

                if (player.Controls != null)
                {
                    int requestedIdx = player.Controls.SelectedWeaponIndex;
                    if (player.Controls.WeaponSwitchTriggered) SelectWeapon(requestedIdx);
                    int currIdx = weapon != null ? weapon.CurrentWeaponIndex : 0;
                    player.Controls.SelectedWeaponIndex = currIdx;
                    if (currIdx >= 0 && currIdx < player.Controls.WeaponSlotAmmo.Length)
                    {
                        player.Controls.WeaponSlotAmmo[currIdx] = (reloadTimer > 0 ? "..." : ammo.ToString()) + " / " + (weapon != null ? weapon.MaxAmmo : 20);
                    }
                }
            }

            // Find nearby stealth knife target
            nearbyKnifeTarget = null;
            if (player != null && player.Health != null && !player.Health.IsDead && enemies != null && !mapOpen)
            {
                Vector3 pPos = player.transform.position;
                float closestDist = 3.2f;
                foreach (var bot in enemies)
                {
                    if (bot == null || bot.Actor == null || bot.Actor.IsDead || !bot.gameObject.activeInHierarchy) continue;
                    float d = Vector3.Distance(pPos, bot.transform.position);
                    if (d <= closestDist && CanKnifeKill(bot))
                    {
                        closestDist = d;
                        nearbyKnifeTarget = bot;
                    }
                }
            }

            if (player != null && player.Controls != null)
            {
                player.Controls.KnifeKillAvailable = (nearbyKnifeTarget != null);
            }

            // Execute knife action (stealth takedown if near target, tactical melee slash otherwise)
            if (!mapOpen && (player != null && player.Health != null && !player.Health.IsDead))
            {
                bool knifeTriggered = Input.GetKeyDown(KeyCode.F) || (player != null && player.Controls != null && player.Controls.KnifeKillPressed);
                if (knifeTriggered)
                {
                    if (nearbyKnifeTarget != null)
                    {
                        ExecuteKnifeKill(nearbyKnifeTarget);
                    }
                    else
                    {
                        ExecuteKnifeSlash();
                    }
                }
            }

            if (player != null && player.Controls != null && (player.Controls.FirePressed || player.Controls.FireHeld))
            {
                fireBufferTimer = 0.22f;
            }
            if (!Application.isMobilePlatform && Input.touchCount == 0 && Input.GetMouseButtonDown(0) && (player == null || player.Controls == null || !player.Controls.IsPointerOverUI))
            {
                fireBufferTimer = 0.22f;
            }

            if (fireBufferTimer > 0)
            {
                if (fireCooldown <= 0 && reloadTimer <= 0 && chamberTimer <= 0 && ammo > 0 && !mapOpen && (player != null && player.Health != null && !player.Health.IsDead) && !player.BinocularsMode)
                {
                    Fire();
                    fireBufferTimer = 0f;
                }
                else
                {
                    fireBufferTimer -= Time.deltaTime;
                }
            }

            if (Input.GetKeyDown(KeyCode.V)) ToggleThermalMode();
            if (Input.GetKeyDown(KeyCode.U)) TriggerUAVRecon();
            if (Input.GetKeyDown(KeyCode.N)) CycleEnvironment();
            fireCooldown-=Time.deltaTime;
        }
        System.Collections.IEnumerator RemoveFallenEnemy(EnemyBot enemy)
        {
            yield return new WaitForSeconds(2);
            if(enemy!=null) enemy.gameObject.SetActive(false);
        }
        public void Fire()
        {
            if(BallisticsSystem.isBulletCamActive || ballistics==null || ballistics.config==null) return;
            if((stageMode && currentMissionState!=MissionState.InProgress && currentMissionState!=MissionState.Extraction) || mapOpen || fireCooldown>0 || reloadTimer>0 || chamberTimer>0 || ammo<=0 || player==null || player.Health==null || player.Health.IsDead || (player != null && player.BinocularsMode)) return;
            totalShotsFired++;
            ammo--;
            int rifleIndex=weapon!=null?weapon.CurrentWeaponIndex:1;
            chamberTimer = rifleIndex==1?1.05f:rifleIndex==0?.55f:.2f;
            if(FirstContactMode) firstContact?.Shot();
            fireCooldown = weapon?.Config != null ? weapon.Config.fireRate : 0.32f;
            
            shotNotice=""; shotNoticeTime=0;
            player.ApplyRecoil(rifleIndex==0?140f:rifleIndex==1?95f:60f);
            weapon?.OnShotFired();
            ballistics?.Fire();
            if (isPvPDuel && GeoSniper.Duel.SniperDuelManager.Instance != null && weapon != null)
            {
                var cam = cameraView != null ? cameraView : Camera.main;
                Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
                GeoSniper.Duel.SniperDuelManager.Instance.NotifyLocalGunshot(weapon.transform.position, fwd, rifleIndex);
            }
        }
        static void SpawnHitSparks(Vector3 position)
        {
            BallisticsSystem.SpawnBloodSplatter(position);
        }

        string GetTargetBuildingLocationName()
        {
            if (enemies == null || enemies.Count == 0) return "CENTRAL SECTOR";
            // Personalize the briefing around the operator's actual nearby
            // landmark, rather than always naming the first hostile's building.
            Vector3 refPos = player != null ? player.transform.position
                : (enemies[0] != null ? enemies[0].transform.position : Vector3.zero);

            string fallbackName = "LIVE GPS SECTOR";
            if (mapLocation != null && !string.IsNullOrWhiteSpace(mapLocation.Label))
                fallbackName = mapLocation.Label.Trim().ToUpperInvariant() + " AREA";

            foreach (var world in SectorWorld.LoadedWorlds)
            {
                if (world == null || world.Buildings == null) continue;
                if (!string.IsNullOrWhiteSpace(world.SourceLabel))
                {
                    string label = world.SourceLabel.Split('|')[0].Trim().ToUpper();
                    bool providerLabel = label.Contains("OVERTURE") || label.Contains("OSM") || label.Contains("CACHED") || label.Contains("OFFLINE");
                    if (!providerLabel && label != "LOCAL SECTOR") fallbackName = label;
                }

                float minDst = 120f;
                string bestName = null;
                foreach (var kvp in world.Buildings)
                {
                    if (kvp.Value == null) continue;
                    float dst = Vector3.Distance(refPos, kvp.Value.transform.position);
                    if (dst < minDst)
                    {
                        var f = kvp.Key;
                        if (f != null)
                        {
                            string candidateName = null;
                            if (!string.IsNullOrWhiteSpace(f.Name)) candidateName = f.Name.ToUpper();
                            else if (!string.IsNullOrWhiteSpace(f.Landmark)) candidateName = f.Landmark.ToUpper();
                            else if (!string.IsNullOrWhiteSpace(f.Kind) && f.Kind != "building") candidateName = f.Kind.ToUpper() + " BUILDING";

                            if (candidateName != null)
                            {
                                bestName = candidateName;
                                minDst = dst;
                            }
                        }
                    }
                }
                // Cached Overture sectors may contain named places/shops that
                // are not represented in the Buildings dictionary. Search the
                // original feature list as well so cache mode still feels local.
                if (world.Features != null)
                {
                    foreach (var feature in world.Features)
                    {
                        if (feature == null || string.IsNullOrWhiteSpace(feature.Name) || feature.Points == null || feature.Points.Count == 0) continue;
                        if (feature.Kind == "road" || feature.Kind == "water") continue;
                        Vector2 center = Vector2.zero;
                        foreach (var point in feature.Points) center += point;
                        center /= feature.Points.Count;
                        Vector3 worldCenter = world.transform.TransformPoint(new Vector3(center.x, 0f, center.y));
                        float dst = Vector3.Distance(refPos, worldCenter);
                        if (dst < minDst)
                        {
                            bestName = feature.Name.Trim().ToUpperInvariant();
                            minDst = dst;
                        }
                    }
                }
                if (bestName != null) return bestName;
            }
            return fallbackName;
        }

        void OnGUI()
        {
            GUI.depth = -200;
            float s=Mathf.Max(.4f, Mathf.Min(Screen.height/720f, Screen.width/800f)); GUI.matrix=Matrix4x4.Scale(new Vector3(s,s,1)); float w=Screen.width/s,h=Screen.height/s;
            if (BallisticsSystem.isBulletCamActive)
            {
                float t = Mathf.Clamp01(BallisticsSystem.bulletCamElapsed / 0.3f);
                float slide = Mathf.SmoothStep(0f, 1f, t);
                float barH = h * 0.12f;
                
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, barH * (slide - 1f), w, barH), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, h - barH * slide, w, barH), Texture2D.whiteTexture);
                
                // Vignette dark tint overlay
                GUI.color = new Color(0f, 0f, 0f, slide * 0.35f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                
                GUI.color = Color.white;

                var slowmoStyle = GUIStyleCache.Get(18, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.3f, 0.2f, 0.95f));
                GUI.Label(new Rect(0, 18, w, 32), "• SLOW MOTION •", slowmoStyle);
                return;
            }
            bool wasMapOpen = mapOpen;
            if (wasMapOpen) { if (!nativeMapOpen) DrawTacticalMap(w, h); return; }
            if(stageMode && !FirstContactMode && currentMissionState==MissionState.Briefing)
            {
                DrawCommandBriefing();
                return;
            }
            if (currentMissionState == MissionState.Complete)
            {
                if (showingDebriefing)
                    DrawMissionDebriefingScreen(w, h);
                else
                    DrawVictoryRewardScreen(w, h);
                return;
            }
            if (currentMissionState == MissionState.Failed)
            {
                DrawMissionFailedScreen(w, h);
                return;
            }
            var style = GUIStyleCache.GetBox(19, TextAnchor.MiddleCenter);
            if(!mapOpen && player!=null && player.BinocularsMode)
            {
                DrawTacticalBinocularsHUD(w,h);
                DrawDamageFeedback(w,h);
            }
            else if(!mapOpen && player!=null && player.Scoped)
            {
                weapon?.DrawScope(w,h, ammo, reloadTimer > 0);
                DrawScopeZoomSlider(w,h);
                DrawDamageFeedback(w,h);
            }
            else if(!mapOpen)
            {
                DrawScreenRain(w,h);
                DrawDamageFeedback(w,h);
            }
            
            string targetLoc = GetTargetBuildingLocationName();
            string objText = "🎯 "+(stageMode && !FirstContactMode ? activeContract.Title.ToUpperInvariant() : "CONTRACT")+" NEAR " + targetLoc + " (" + defeated + " / " + enemies.Count + ")";
            if (stageMode && activeContract.Type == CampaignContractType.TargetIdentification)
            {
                string hvtStatus = (contractTarget != null && contractTarget.GetComponent<CombatActor>().IsDead) ? "DEAD" : "ALIVE";
                string sniperStatus = (contractCounterSniper == null || contractCounterSniper.GetComponent<CombatActor>().IsDead) ? "DOWN" : "THREAT";
                objText = campaignNode!=null && campaignNode.id==0
                    ? $"TRAINING: ELIMINATE THE SCOUT AND GUARD ({defeated}/{enemies.Count})"
                    : $"HVT: COMMANDER [{hvtStatus}]"+(contractCounterSniper!=null?$" | COUNTER-SNIPER [{sniperStatus}]":"");
            }
            else if (stageMode && activeContract.Type == CampaignContractType.Overwatch)
            {
                int vipHp = (contractVIP != null && contractVIP.Actor != null) ? Mathf.RoundToInt(contractVIP.Actor.Health) : 0;
                string vipAlert = (vipHp < 70) ? " ⚠️ [UNDER FIRE]" : "";
                objText = $"🛡️ OVERWATCH: ESCORT VIP ({vipHp} HP){vipAlert} | THREATS: {enemies.Count - defeated} REMAINING";
            }
            else if (stageMode && activeContract.Type == CampaignContractType.TimedInterception)
            {
                float remDist = (contractTarget != null) ? Vector3.Distance(contractTarget.transform.position, extractionPoint) : 0f;
                objText = contractTarget!=null && contractTarget.runnerStartDelay>0f
                    ? $"⚡ INTERCEPTION: COURIER DEPARTS IN {Mathf.CeilToInt(contractTarget.runnerStartDelay)}s | IDENTIFY TARGET"
                    : $"⚡ INTERCEPTION: ELIMINATE FLEEING COURIER ({Mathf.CeilToInt(contractModeTimer)}s / {Mathf.RoundToInt(remDist)}m)";
            }
            else if (stageMode && activeContract.Type == CampaignContractType.Stealth)
            {
                int alertLimit = (difficulty != null) ? difficulty.stealthAlertLimit : 4;
                string alertStatus = alerts.Level == 0 ? "STEALTH INTACT" : (alerts.Level >= alertLimit - 1 ? "CRITICAL LOCKDOWN" : "CAUTION");
                objText = $"🤫 GHOST CONTRACT: SILENT CLEAR ({defeated}/{enemies.Count}) | ALERTS: {alerts.Level}/{alertLimit} [{alertStatus}]";
            }
            else if (stageMode && activeContract.Type == CampaignContractType.Escape)
            {
                if (currentMissionState == MissionState.InProgress)
                    objText = ExtractionObjective();
                else
                    objText = "🚁 EXTRACTION BIRD ARRIVED - REACH THE LZ!";
            }
            if (currentMissionState == MissionState.Extraction) objText = "🚁 REACH DUSTOFF HELICOPTER LZ";
            if (currentMissionState == MissionState.Complete) objText = "🏆 MISSION COMPLETE - CONTRACT EXECUTED";
            string header = stageMode ? StageTitle() + "  |  " + objText : freeRoam ? (rangeMode ? "GPS RANGE  |  " : "FREE ROAM SECTOR  |  ") + objText : objText;

            if (isPvPDuel)
            {
                float rivalDist = (duelRivalSniper != null && player != null) ? Vector3.Distance(player.transform.position, duelRivalSniper.transform.position) : 0f;
                header = currentMissionState == MissionState.Complete
                    ? "🏆 VICTORY - RIVAL SNIPER ELIMINATED! +$500 CASH +100 XP"
                    : $"⚔️ PvP DUEL: 1v1 ROOFTOP SHOWDOWN  |  RIVAL DISTANCE: {rivalDist:0}m";
            }

            if(FirstContactMode) header=FirstContactObjective();
            if(MeetingMode && currentMissionState==MissionState.InProgress) header=MeetingObjective;

            // Top Header Objective Bar: Show ONLY informative text at top (hidden while scoped to keep optic clean)
            if (player == null || !player.Scoped)
            {
                Rect headerRect = new Rect(20, 8, w - 40, 32);
                TacticalGUI.DrawPanel(headerRect, new Color(0.04f, 0.08f, 0.12f, 0.88f), isPvPDuel ? Color.red : TacticalGUI.AccentCyan, 1.2f);
                var headerStyle = GUIStyleCache.Get(12, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
                GUI.Label(headerRect, header, headerStyle);
            }

            if (isPvPDuel && currentMissionState == MissionState.InProgress && duelLockTimer > 0.4f)
            {
                float lockPct = Mathf.Clamp01(duelLockTimer / Mathf.Max(3.2f, (difficulty!=null?difficulty.duelLockSeconds:3.5f)));
                Rect lockRect = new Rect(w / 2 - 180, 62, 360, 24);
                TacticalGUI.DrawPanel(lockRect, new Color(0.25f, 0.05f, 0.05f, 0.92f), Color.red, 1.2f);
                GUI.Label(lockRect, $"⚠️ RIVAL SNIPER LOCK: {lockPct * 100:0}% - SHOOT OR TAKE COVER!", GUIStyleCache.Get(11, FontStyle.Bold, TextAnchor.MiddleCenter, Color.yellow));
            }
            else if (!isPvPDuel && currentMissionState == MissionState.InProgress)
            {
                var activeRival = EnemyBot.ActiveAimingCounterSniper;
                if (activeRival != null && activeRival.Actor != null && !activeRival.Actor.IsDead && activeRival.IsAimingAtPlayer)
                {
                    float rem = activeRival.CounterSniperLockRemaining;
                    Rect lockRect = new Rect(w / 2 - 190, 62, 380, 24);
                    TacticalGUI.DrawPanel(lockRect, new Color(0.32f, 0.04f, 0.04f, 0.94f), Color.red, 1.4f);
                    float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 16f);
                    GUI.color = new Color(1f, 0.85f, 0.2f, pulse);
                    GUI.Label(lockRect, $"⚠️ COUNTER-SNIPER LOCK [{rem:F1}s] — CROUCH / TAKE COVER!", GUIStyleCache.Get(11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.2f)));
                    GUI.color = Color.white;
                }
            }

            // Bottom Player Status HUD (HP & Ammo, positioned neatly above the bottom weapon dock - hidden while scoped)
            if (player == null || !player.Scoped)
            {
                float hp = player == null || player.Health == null ? 0 : player.Health.Health;
                Rect hudRect = new Rect(w / 2f - 175, h - 76, 350, 24);
                TacticalGUI.DrawPanel(hudRect, new Color(0.03f, 0.06f, 0.10f, 0.75f), TacticalGUI.ThemeBorder, 1.0f);

                // HP Bar fill
                TacticalGUI.DrawProgressBar(new Rect(hudRect.x + 6, hudRect.y + 4, 115, 16), hp / 100f, hp > 35 ? new Color(0.2f, 0.85f, 0.35f) : TacticalGUI.AccentRed, new Color(0.1f, 0.12f, 0.16f));
                var barTextStyle = GUIStyleCache.Get(10, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
                GUI.Label(new Rect(hudRect.x + 6, hudRect.y + 4, 115, 16), "❤️ " + Mathf.CeilToInt(hp) + "%", barTextStyle);

                // Stance Badge
                string stanceText = player != null ? (player.IsCrouching ? "CROUCH" : player.Sprinting ? "SPRINT" : "STAND") : "STAND";
                Color stanceCol = player != null && player.IsCrouching ? TacticalGUI.AccentCyan : player != null && player.Sprinting ? TacticalGUI.AccentGold : new Color(0.7f, 0.8f, 0.9f);
                TacticalGUI.DrawBadge(new Rect(hudRect.x + 128, hudRect.y + 4, 76, 16), stanceText, stanceCol, 9);

                // Ammo counter label
                string ammoStr = reloadTimer > 0 ? "⚡ RELOADING" : "AMMO " + ammo + " / " + (weapon!=null?weapon.MaxAmmo:20);
                TacticalGUI.DrawBadge(new Rect(hudRect.x + 210, hudRect.y + 4, 134, 16), ammoStr, reloadTimer > 0 ? TacticalGUI.AccentGold : TacticalGUI.AccentCyan, 9);
            }
            GUI.color=Color.white;
            if(player==null || !player.Scoped)
            {
                GUI.DrawTexture(new Rect(w/2-10,h/2-1,20,2),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(w/2-1,h/2-10,2,20),Texture2D.whiteTexture);
            }
            DrawConfirmedHit(w,h);

            // Draw detection indicators (unscoped only)
            if (cameraView != null && (player == null || !player.Scoped))
            {
                foreach(var enemy in enemies)
                {
                    if (enemy != null && !string.IsNullOrEmpty(enemy.AwarenessLabel) && enemy.CanShowAwareness(cameraView))
                    {
                        Vector3 screenPos = cameraView.WorldToScreenPoint(enemy.transform.position + Vector3.up * 2.2f);
                        if (screenPos.z > 0)
                        {
                            screenPos.x /= s; screenPos.y = (Screen.height - screenPos.y) / s;
                            float barWidth = 80;
                            TacticalGUI.DrawBadge(new Rect(screenPos.x-55,screenPos.y-25,110,18),enemy.AwarenessLabel,enemy.currentState==AIState.Combat?TacticalGUI.AccentRed:TacticalGUI.AccentGold,10);
                            GUI.color = Color.black;
                            GUI.DrawTexture(new Rect(screenPos.x - barWidth/2 - 2, screenPos.y - 6, barWidth + 4, 12), Texture2D.whiteTexture);
                            GUI.color = Color.Lerp(Color.yellow, Color.red, enemy.detectionProgress);
                            GUI.DrawTexture(new Rect(screenPos.x - barWidth/2, screenPos.y - 4, barWidth * enemy.detectionProgress, 8), Texture2D.whiteTexture);
                            GUI.color = Color.white;
                        }
                    }
                }

                // Draw enemy combat barks & suppression badges
                foreach (var enemy in enemies)
                {
                    if (enemy != null && enemy.BarkTimer > 0f && !string.IsNullOrEmpty(enemy.CombatBark) && enemy.CanShowAwareness(cameraView))
                    {
                        Vector3 headPos = enemy.transform.position + Vector3.up * (enemy.IsCrouched ? 1.35f : 2.15f);
                        Vector3 sp = cameraView.WorldToScreenPoint(headPos);
                        if (sp.z > 0)
                        {
                            float bx = Mathf.Clamp(sp.x / s, 95f, w - 95f);
                            float by = Mathf.Clamp((Screen.height - sp.y) / s, 45f, h - 55f);
                            Color barkColor = enemy.currentState == AIState.TakeCover ? new Color(1f, 0.82f, 0.2f) : new Color(1f, 0.35f, 0.25f);
                            TacticalGUI.DrawBadge(new Rect(bx - 85, by - 52, 170, 22), $"💬 {enemy.CombatBark}", barkColor, 10);
                        }
                    }
                }
            }
            
            if(shotNoticeTime>0 && !mapOpen && (player == null || !player.Scoped))
            {
                float t = Mathf.Clamp01((1.4f - shotNoticeTime) / 0.15f);
                float scaleAnim = 1.0f + Mathf.Sin(t * Mathf.PI) * 0.25f;
                bool isHead = shotNotice.Contains("HEADSHOT");
                Color txtColor = isHead ? new Color(1f, 0.25f, 0.15f) : new Color(1f, 0.85f, 0.2f);
                
                var noticeStyle = GUIStyleCache.Get(Mathf.RoundToInt(22 * scaleAnim), FontStyle.Bold, TextAnchor.MiddleCenter, txtColor);
                
                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.Label(new Rect(w/2 - 198, h/2 + 30, 400, 38), shotNotice, noticeStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(w/2 - 200, h/2 + 28, 400, 38), shotNotice, noticeStyle);
            }
            
            
            if(!mapOpen && (player == null || !player.Scoped)) DrawAccoladeBanner(w,h);
            if (nearbyKnifeTarget != null && !mapOpen && player != null && player.Health != null && !player.Health.IsDead && !player.Scoped)
            {
                bool mobile = Application.isMobilePlatform || Input.touchSupported;
                float pW = 240, pH = 48;
                float pX = (w - pW) * 0.5f;
                float pY = h * 0.58f;
                string prompt = mobile ? "🗡️ STEALTH KNIFE KILL" : "[F] 🗡️ STEALTH KNIFE KILL";
                if (TacticalGUI.DrawButton(new Rect(pX, pY, pW, pH), prompt, true, 13))
                {
                    ExecuteKnifeKill(nearbyKnifeTarget);
                }
            }
            if(!mapOpen && (player == null || !player.Scoped)) DrawMinimap(w,h);
            if(!mapOpen && (player == null || !player.Scoped)) Draw3DWaypointMarker(w,h);
            if(!mapOpen && (player == null || (!player.Scoped && !player.BinocularsMode)) && (stageMode && currentMissionState==MissionState.InProgress || HasAnyTaggedEnemies())) DrawTargetMarkers(w,h);
            if(!mapOpen && (player == null || !player.Scoped) && player!=null) DrawTacticalHUDButtons(w,h);
            if(FirstContactMode && (player == null || !player.Scoped)) DrawFirstContactHUD(w,h);
            else if(!stageMode && freeRoam && player!=null && !player.Scoped && TacticalGUI.DrawButton(new Rect(15, 114, 220, 44), "🎯 START CONTRACT", true, 13)) BeginStage(0);
        }

        void DrawAccoladeBanner(float width, float height)
        {
            if (accoladeTimer <= 0 || mapOpen) return;
            float dur = 2.8f;
            float progress = 1f - (accoladeTimer / dur);
            float alpha = accoladeTimer < 0.4f ? accoladeTimer / 0.4f : Mathf.Clamp01(progress / 0.15f);
            float bannerY = Mathf.Lerp(15f, 48f, Mathf.Clamp01(progress / 0.12f));
            float bannerW = Mathf.Clamp(width * 0.42f, 320f, 460f);
            float bannerH = 54f;
            float bannerX = (width - bannerW) * 0.5f;

            // Background
            GUI.color = new Color(0.04f, 0.07f, 0.09f, alpha * 0.94f);
            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerW, bannerH), Texture2D.whiteTexture);

            // Gold tactical border
            Color gold = new Color(1f, 0.82f, 0.22f, alpha * 0.95f);
            GUI.color = gold;
            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bannerX, bannerY + bannerH - 2, bannerW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bannerX, bannerY, 3, bannerH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bannerX + bannerW - 3, bannerY, 3, bannerH), Texture2D.whiteTexture);

            // Icon box
            GUI.color = new Color(0.12f, 0.16f, 0.2f, alpha);
            GUI.DrawTexture(new Rect(bannerX + 8, bannerY + 8, 38, 38), Texture2D.whiteTexture);
            GUI.color = gold;
            GUI.DrawTexture(new Rect(bannerX + 10, bannerY + 10, 34, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bannerX + 10, bannerY + 44, 34, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bannerX + 10, bannerY + 10, 2, 36), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bannerX + 42, bannerY + 10, 2, 36), Texture2D.whiteTexture);

            var iconStyle = GUIStyleCache.Get(16, FontStyle.Normal, TextAnchor.MiddleCenter, gold);
            GUI.Label(new Rect(bannerX + 8, bannerY + 8, 38, 38), "🎖️", iconStyle);

            // Title
            var titleStyle = GUIStyleCache.Get(14, FontStyle.Bold, TextAnchor.MiddleLeft, gold);
            GUI.Label(new Rect(bannerX + 54, bannerY + 6, bannerW - 60, 22), accoladeTitle, titleStyle);

            // Subtitle
            var subStyle = GUIStyleCache.Get(11, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.35f, 0.92f, 0.95f, alpha));
            GUI.Label(new Rect(bannerX + 54, bannerY + 28, bannerW - 60, 20), accoladeSubtitle, subStyle);
            GUI.color = Color.white;
        }

        void DrawScreenRain(float w, float h)
        {
            // WorldRainSystem handles 3D world particle rain realistically.
            // 2D GUI screen lines removed to prevent fake-looking static streaks.
        }

        void DrawScopeZoomSlider(float width, float height)
        {
            float d = Mathf.Min(width, height) * 0.92f;
            float scopeLeft = (width - d) * 0.5f;

            // Position immediately alongside the left flank of the circular scope
            // Safely spaced from the left-edge joystick/buttons
            float trackX = Mathf.Max(scopeLeft - 38f, 50f);
            float trackHeight = Mathf.Clamp(height * 0.46f, 240f, 380f);
            float trackTop = (height - trackHeight) * 0.5f;

            Rect hit = new Rect(trackX - 35f, trackTop - 25f, 90f, trackHeight + 50f);
            bool pointer = hit.Contains(Event.current.mousePosition);
            if (pointer && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag))
            {
                MobileCombatInput.ZoomSliderValue = Mathf.Clamp01(1f - (Event.current.mousePosition.y - trackTop) / trackHeight);
                Event.current.Use();
            }

            float value = Mathf.Clamp01(MobileCombatInput.ZoomSliderValue);
            float knobY = trackTop + (1f - value) * trackHeight;

            float minFov = 24f;
            float maxFov = 4f;
            if (weapon != null && weapon.Config != null && weapon.Config.zoomLevels != null && weapon.Config.zoomLevels.Length > 0)
            {
                minFov = weapon.Config.zoomLevels[0];
                maxFov = weapon.Config.zoomLevels[Mathf.Min(1, weapon.Config.zoomLevels.Length - 1)];
            }
            float currentFov = Mathf.Lerp(minFov, maxFov, value);
            float magMultiplier = Mathf.Clamp(65f / Mathf.Max(1f, currentFov), 1f, 16f);

            Color oldColor = GUI.color;

            // 1. Sleek Smoked Glass Rail Housing
            GUI.color = new Color(0.015f, 0.035f, 0.06f, 0.88f);
            GUI.DrawTexture(new Rect(trackX - 18f, trackTop - 18f, 36f, trackHeight + 36f), Texture2D.whiteTexture);

            // Beveled Hairline Outer Border
            GUI.color = pointer ? TacticalGUI.AccentCyan : new Color(0.16f, 0.28f, 0.38f, 0.65f);
            TacticalGUI.DrawPanel(new Rect(trackX - 18f, trackTop - 18f, 36f, trackHeight + 36f), new Color(0, 0, 0, 0), GUI.color, 1f);

            // 2. Central Metallic Track Groove
            GUI.color = new Color(0.06f, 0.10f, 0.14f, 0.95f);
            GUI.DrawTexture(new Rect(trackX - 3f, trackTop, 6f, trackHeight), Texture2D.whiteTexture);

            // 3. Active Magnification Luminous Fill Bar
            float fillHeight = value * trackHeight;
            GUI.color = pointer ? new Color(0.15f, 0.90f, 1f, 0.95f) : new Color(0.08f, 0.72f, 0.88f, 0.85f);
            GUI.DrawTexture(new Rect(trackX - 2.5f, knobY, 5f, fillHeight), Texture2D.whiteTexture);

            // 4. Precision Optic Graduation Hash Marks (Etched along track)
            int tickSteps = 8;
            for (int i = 0; i <= tickSteps; i++)
            {
                float t = (float)i / tickSteps;
                float ty = trackTop + (1f - t) * trackHeight;
                bool isMajor = (i % 2 == 0);
                float tickW = isMajor ? 9f : 5f;

                // Right-side tick mark towards scope
                GUI.color = isMajor ? new Color(0.65f, 0.82f, 0.95f, 0.75f) : new Color(0.35f, 0.50f, 0.65f, 0.45f);
                GUI.DrawTexture(new Rect(trackX + 6f, ty - 0.5f, tickW, 1.5f), Texture2D.whiteTexture);

                // Left-side sub-mil ticks
                GUI.DrawTexture(new Rect(trackX - 6f - (isMajor ? 6f : 3f), ty - 0.5f, isMajor ? 6f : 3f, 1.5f), Texture2D.whiteTexture);

                // Small tick labels for major steps
                if (isMajor && i > 0 && i < tickSteps)
                {
                    float stepFov = Mathf.Lerp(minFov, maxFov, t);
                    float stepMag = Mathf.Clamp(65f / Mathf.Max(1f, stepFov), 1f, 16f);
                    var tickStyle = GUIStyleCache.Get(8, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.55f, 0.70f, 0.85f, 0.60f));
                    GUI.Label(new Rect(trackX - 44f, ty - 8f, 32f, 16f), stepMag.ToString("0.#") + "X", tickStyle);
                }
            }

            // 5. Advanced Knurled Metallic Slider Thumb
            float thumbW = 34f;
            float thumbH = 26f;
            GUI.color = pointer ? new Color(0.12f, 0.22f, 0.32f, 0.98f) : new Color(0.08f, 0.15f, 0.22f, 0.95f);
            GUI.DrawTexture(new Rect(trackX - thumbW * 0.5f, knobY - thumbH * 0.5f, thumbW, thumbH), Texture2D.whiteTexture);

            // Thumb bezel border
            TacticalGUI.DrawPanel(new Rect(trackX - thumbW * 0.5f, knobY - thumbH * 0.5f, thumbW, thumbH), new Color(0, 0, 0, 0), pointer ? TacticalGUI.AccentCyan : TacticalGUI.AccentGold, 1.5f);

            // Grip ridges inside thumb
            GUI.color = pointer ? new Color(0.9f, 0.98f, 1f, 0.9f) : new Color(0.6f, 0.75f, 0.85f, 0.8f);
            GUI.DrawTexture(new Rect(trackX - 10f, knobY - 5f, 20f, 1.5f), Texture2D.whiteTexture);
            GUI.color = pointer ? TacticalGUI.AccentCyan : TacticalGUI.AccentGold;
            GUI.DrawTexture(new Rect(trackX - 12f, knobY - 0.5f, 24f, 2f), Texture2D.whiteTexture);
            GUI.color = pointer ? new Color(0.9f, 0.98f, 1f, 0.9f) : new Color(0.6f, 0.75f, 0.85f, 0.8f);
            GUI.DrawTexture(new Rect(trackX - 10f, knobY + 4f, 20f, 1.5f), Texture2D.whiteTexture);

            // 6. Digital HUD Magnification Readout Badge
            float badgeW = 76f;
            float badgeH = 28f;
            float badgeY = Mathf.Clamp(knobY - 34f, trackTop - 34f, trackTop + trackHeight + 4f);
            TacticalGUI.DrawPanel(new Rect(trackX - badgeW * 0.5f, badgeY, badgeW, badgeH), new Color(0.02f, 0.04f, 0.07f, 0.94f), pointer ? TacticalGUI.AccentCyan : TacticalGUI.AccentGold, 1f);

            var magLabel = GUIStyleCache.Get(13, FontStyle.Bold, TextAnchor.MiddleCenter, pointer ? TacticalGUI.AccentCyan : Color.white);
            GUI.Label(new Rect(trackX - badgeW * 0.5f, badgeY + 1f, badgeW, badgeH - 2f), magMultiplier.ToString("0.0") + "×", magLabel);

            // 7. Tactical Header & Footer Tags
            var tagStyle = GUIStyleCache.Get(8, FontStyle.Bold, TextAnchor.MiddleCenter, TacticalGUI.AccentCyan);
            GUI.Label(new Rect(trackX - 45f, trackTop - 38f, 90f, 18f), "OPTIC ZOOM", tagStyle);

            var footStyle = GUIStyleCache.Get(8, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.55f, 0.68f, 0.80f, 0.7f));
            GUI.Label(new Rect(trackX - 45f, trackTop + trackHeight + 18f, 90f, 18f), "SLIDE", footStyle);

            GUI.color = oldColor;
        }

        void DrawVictoryRewardScreen(float w, float h)
        {
            // Fullscreen dark backdrop
            GUI.color = new Color(0.02f, 0.04f, 0.07f, 0.94f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Ambient central golden radiance glow
            TacticalGUI.DrawGlow(new Rect(w / 2f - 320f, h / 2f - 240f, 640f, 480f), new Color(1f, 0.72f, 0.1f, 0.12f));

            float modalW = Mathf.Min(560f, w - 24f);
            float modalH = Mathf.Min(525f, h - 20f);
            Rect modalRect = new Rect((w - modalW) / 2f, (h - modalH) / 2f, modalW, modalH);

            // Modal card with glowing gold tactical border
            TacticalGUI.DrawPanel(modalRect, new Color(0.06f, 0.09f, 0.14f, 0.97f), TacticalGUI.AccentGold, 2.2f);

            float mx = modalRect.x;
            float my = modalRect.y;

            // --- 1. STARS RATING BANNER ---
            float starT = victoryScreenTimer;
            string s1 = starT >= 0.15f ? "★" : "☆";
            string s2 = (rewardStars >= 2 && starT >= 0.40f) ? "★" : "☆";
            string s3 = (rewardStars >= 3 && starT >= 0.65f) ? "★" : "☆";

            var starStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = TacticalGUI.AccentGold }
            };
            Rect starsRect = new Rect(mx, my + 14, modalW, 38);
            GUI.Label(starsRect, $"{s1}   {s2}   {s3}", starStyle);

            // --- 2. HEADER TITLE & SUBHEADER ---
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(mx + 16, my + 54, modalW - 32, 32), victoryHeader, titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = TacticalGUI.AccentCyan }
            };
            GUI.Label(new Rect(mx + 16, my + 86, modalW - 32, 20), victorySubheader.ToUpperInvariant(), subStyle);

            // Decorative separator line
            TacticalGUI.DrawLine(new Vector2(mx + 30, my + 112), new Vector2(mx + modalW - 30, my + 112), new Color(0.25f, 0.40f, 0.55f, 0.65f), 1.5f);

            // --- 3. JUICY REWARD CARDS (Side by Side) ---
            float cardY = my + 120;
            float cardGap = 12f;
            float cardW = (modalW - 36 - cardGap) / 2f;
            float cardH = 88f;

            // Cash Reward Card (Green Theme)
            Rect cashRect = new Rect(mx + 18, cardY, cardW, cardH);
            TacticalGUI.DrawPanel(cashRect, new Color(0.04f, 0.12f, 0.07f, 0.94f), new Color(0.20f, 0.85f, 0.35f, 0.90f), 1.8f);

            var rewardLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.45f, 0.90f, 0.55f) }
            };
            GUI.Label(new Rect(cashRect.x, cashRect.y + 8, cashRect.width, 18), "💵 BOUNTY REWARD", rewardLabelStyle);

            var cashValStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.35f, 1.0f, 0.45f) }
            };
            GUI.Label(new Rect(cashRect.x, cashRect.y + 28, cashRect.width, 34), "+$" + rewardCash, cashValStyle);

            var cardSubStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.55f, 0.70f, 0.60f, 0.80f) }
            };
            GUI.Label(new Rect(cashRect.x, cashRect.y + 64, cashRect.width, 16), "CREDITED TO BANK", cardSubStyle);

            // XP Reward Card (Gold Theme)
            Rect xpRect = new Rect(mx + 18 + cardW + cardGap, cardY, cardW, cardH);
            TacticalGUI.DrawPanel(xpRect, new Color(0.14f, 0.11f, 0.04f, 0.94f), TacticalGUI.AccentGold, 1.8f);

            var xpLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 0.82f, 0.35f) }
            };
            GUI.Label(new Rect(xpRect.x, xpRect.y + 8, xpRect.width, 18), "⭐ TACTICAL XP", xpLabelStyle);

            var xpValStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 0.85f, 0.20f) }
            };
            GUI.Label(new Rect(xpRect.x, xpRect.y + 28, xpRect.width, 34), "+" + rewardXP + " XP", xpValStyle);

            var xpSubStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.75f, 0.50f, 0.80f) }
            };
            GUI.Label(new Rect(xpRect.x, xpRect.y + 64, xpRect.width, 16), "OPERATIVE MASTERY", xpSubStyle);

            // --- 4. COMBAT TELEMETRY STATS STRIP ---
            float statY = cardY + cardH + 12;
            Rect statsRect = new Rect(mx + 18, statY, modalW - 36, 68);
            TacticalGUI.DrawPanel(statsRect, new Color(0.04f, 0.06f, 0.09f, 0.90f), TacticalGUI.ThemeBorder, 1.2f);

            float colW = statsRect.width / 4f;
            int acc = totalShotsFired > 0 ? Mathf.Clamp(Mathf.RoundToInt((float)totalHitsScored / totalShotsFired * 100f), 0, 100) : 100;
            int playerHp = (player != null && player.Health != null) ? Mathf.CeilToInt(player.Health.Health) : 100;
            int timeSec = Mathf.CeilToInt(missionDuration);

            string[] statHeaders = { "🎯 ACCURACY", "💀 HEADSHOTS", "❤️ VITALITY", "⏱️ TIME" };
            string[] statValues = { acc + "%", totalHeadshotsScored.ToString(), playerHp + "%", $"{timeSec / 60:D2}:{timeSec % 60:D2}" };
            Color[] statColors = { acc >= 70 ? TacticalGUI.AccentCyan : Color.white, totalHeadshotsScored > 0 ? TacticalGUI.AccentGold : Color.white, playerHp > 40 ? new Color(0.35f, 0.95f, 0.45f) : TacticalGUI.AccentRed, Color.white };

            for (int i = 0; i < 4; i++)
            {
                float colX = statsRect.x + i * colW;
                var colHdr = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.6f, 0.7f, 0.82f) } };
                GUI.Label(new Rect(colX, statsRect.y + 10, colW, 16), statHeaders[i], colHdr);

                var colVal = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = statColors[i] } };
                GUI.Label(new Rect(colX, statsRect.y + 30, colW, 26), statValues[i], colVal);

                if (i < 3)
                {
                    GUI.color = new Color(0.2f, 0.3f, 0.4f, 0.4f);
                    GUI.DrawTexture(new Rect(colX + colW - 1, statsRect.y + 12, 1, 44), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }

            if (totalHeadshotsScored > 0)
            {
                Rect bonusRect = new Rect(mx + 18, statY + 72, modalW - 36, 18);
                TacticalGUI.DrawBadge(bonusRect, $"PRECISION SHOTS: {totalHeadshotsScored} HEADSHOTS", TacticalGUI.AccentGold, 10);
            }

            // --- 5. ACTION BUTTONS ---
            float btnY = my + modalH - 120;

            // Rewarded 2X Payout Button
            Rect adDoubleRect = new Rect(mx + 22, btnY - 56, modalW - 44, 48);
            if (!stageRewardsDoubled)
            {
                if (TacticalGUI.DrawButton(adDoubleRect, $"🎬 2X REWARDS (+${rewardCash:N0} CASH  |  +{rewardXP} XP)", true, 13))
                {
                    AdManager.Instance.ShowRewardedAd((success) =>
                    {
                        if (success)
                        {
                            stageRewardsDoubled = true;
                            PlayerPrefs.SetInt("GeoSniper.Credits", PlayerPrefs.GetInt("GeoSniper.Credits", 0) + rewardCash);
                            PlayerPrefs.SetInt("GeoSniper.XP", PlayerPrefs.GetInt("GeoSniper.XP", 0) + rewardXP);
                            PlayerPrefs.Save();
                            rewardCash *= 2;
                            rewardXP *= 2;
                        }
                    }, "victory_double_reward");
                }
            }
            else
            {
                TacticalGUI.DrawBadge(adDoubleRect, "✅ 2X COMBAT REWARDS APPLIED & DEPOSITED", TacticalGUI.AccentGold, 11);
            }

            Rect claimRect = new Rect(mx + 22, btnY, modalW - 44, 52);
            if (TacticalGUI.DrawGreenPlayButton(claimRect, "CLAIM & CONTINUE ▶"))
            {
                if (AdManager.Instance != null && AdManager.Instance.IsNativeBillboardAdLoaded)
                {
                    showingDebriefing = true;
                    AdManager.Instance.ShowNativeScreenAd(GoogleMobileAds.Api.AdPosition.Top);
                }
                else
                {
                    AdManager.Instance?.HideNativeScreenAd();
                    AdManager.Instance?.NotifyStageCompleted();
                    AdManager.Instance?.ShowInterstitialIfReady(() => { ExitToLevelMap(); }, "stage_transition");
                }
            }

            Rect replayRect = new Rect(mx + 22, btnY + 58, modalW - 44, 42);
            if (TacticalGUI.DrawButton(replayRect, "↺ REPLAY MISSION", false, 12))
            {
                showingDebriefing = false;
                AdManager.Instance?.HideNativeScreenAd();
                if (stageMode) BeginStage(stageIndex);
                else Restart();
            }
        }

        void DrawMissionDebriefingScreen(float w, float h)
        {
            // Fullscreen dark tactical backdrop
            GUI.color = new Color(0.02f, 0.04f, 0.07f, 0.98f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Ambient central golden radiance glow
            TacticalGUI.DrawGlow(new Rect(w / 2f - 300f, h / 2f - 180f, 600f, 360f), new Color(1f, 0.72f, 0.1f, 0.09f));

            // Lower confirmation & proceed card
            float panelW = Mathf.Min(620f, w - 24f);
            float panelH = 100f;
            float panelY = h - panelH - 14f;
            Rect panelRect = new Rect((w - panelW) / 2f, panelY, panelW, panelH);
            TacticalGUI.DrawPanel(panelRect, new Color(0.06f, 0.09f, 0.14f, 0.97f), TacticalGUI.AccentGold, 2.0f);

            var infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.70f, 0.85f, 0.98f) }
            };
            GUI.Label(new Rect(panelRect.x + 10, panelRect.y + 8, panelRect.width - 20, 18), "🎯 CONTRACT SIGN-OFF // SPONSOR INTEL VERIFIED", infoStyle);

            Rect proceedRect = new Rect(panelRect.x + 30, panelRect.y + 32, panelRect.width - 60, 54);
            if (TacticalGUI.DrawGreenPlayButton(proceedRect, "PROCEED TO BASE ▶"))
            {
                showingDebriefing = false;
                AdManager.Instance?.HideNativeScreenAd();
                AdManager.Instance?.NotifyStageCompleted();
                AdManager.Instance?.ShowInterstitialIfReady(() => { ExitToLevelMap(); }, "stage_transition");
            }
        }

        string FailureGuidance()
        {
            if(contractSetupError!=null)return "This sector could not support the objective. Retry to rebuild it, or choose another sector. No weapon upgrade is required.";
            if(isPvPDuel)return "Break the rival's laser lock with solid cover. Peek briefly, locate the sniper, fire, then return to cover.";
            string objective;
            if(FirstContactMode)objective="Eliminate the marked target, then follow the extraction marker and stay inside the zone.";
            else if(!stageMode)objective="Eliminate the remaining hostiles. Use cover and reload before moving into the open.";
            else switch(activeContract.Type)
            {
                case CampaignContractType.Overwatch: objective="Keep the VIP alive until they reach extraction. Prioritize enemies firing at the VIP.";break;
                case CampaignContractType.TimedInterception: objective="Eliminate the marked courier before the timer expires or they reach the exit. Lead the moving target; guards are secondary.";break;
                case CampaignContractType.Stealth: objective="Eliminate every sentry before the alert reaches "+(difficulty!=null?difficulty.stealthAlertLimit:4)+". Break line of sight and engage isolated guards.";break;
                case CampaignContractType.Escape: objective="Survive all three assault waves and the hold timer. Clear hostiles when reinforcements are waiting, then reach the helicopter marker and board when it is hovering.";break;
                default: objective="Eliminate the marked commander and any counter-sniper. Clearing ordinary guards alone does not finish this objective.";break;
            }
            if(player!=null && player.Health!=null && player.Health.IsDead)objective="Stay behind solid cover between shots. "+objective;
            return objective;
        }

        void DrawMissionFailedScreen(float w, float h)
        {
            GUI.color = new Color(0.12f, 0.02f, 0.02f, 0.94f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            TacticalGUI.DrawGlow(new Rect(w / 2f - 270f, h / 2f - 215f, 540f, 430f), new Color(1f, 0.15f, 0.15f, 0.15f));

            float modalW = Mathf.Min(530f, w - 24f);
            float modalH = Mathf.Min(405f, h - 24f);
            Rect modalRect = new Rect((w - modalW) / 2f, (h - modalH) / 2f, modalW, modalH);

            TacticalGUI.DrawPanel(modalRect, new Color(0.10f, 0.05f, 0.06f, 0.97f), TacticalGUI.AccentRed, 2f);

            float mx = modalRect.x;
            float my = modalRect.y;

            var failTitle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = TacticalGUI.AccentRed } };
            GUI.Label(new Rect(mx + 16, my + 14, modalW - 32, 30), "💀 MISSION FAILED", failTitle);

            var failSub = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.85f, 0.85f, 0.9f) } };
            string reason = contractSetupError ?? missionFailureReason ?? (firstContact!=null?firstContact.Failure:null) ?? "Objective incomplete. Retry the mission.";
            GUI.Label(new Rect(mx + 16, my + 44, modalW - 32, 38), reason, failSub);

            // Armory Upgrade Tactical Recommendation Popup Card
            Rect adviceRect = new Rect(mx + 24, my + 86, modalW - 48, 74);
            TacticalGUI.DrawPanel(adviceRect, new Color(0.04f, 0.12f, 0.18f, 0.92f), TacticalGUI.AccentCyan, 1.2f);
            var adviceTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = TacticalGUI.AccentCyan } };
            var adviceBodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.88f, 0.92f, 0.96f) } };
            GUI.Label(new Rect(adviceRect.x + 8, adviceRect.y + 4, adviceRect.width - 16, 18), "NEXT ATTEMPT: WHAT TO DO", adviceTitleStyle);
            GUI.Label(new Rect(adviceRect.x + 8, adviceRect.y + 24, adviceRect.width - 16, 46), FailureGuidance(), adviceBodyStyle);

            // 1. Direct Armory Upgrade Button (Highlighted Primary)
            Rect armoryBtnRect = new Rect(mx + 28, my + 164, modalW - 56, 34);
            if (TacticalGUI.DrawButton(armoryBtnRect, "↺ RETRY CONTRACT", true, 13))
            {
                if(stageMode)BeginStage(stageIndex);else Restart();
                return;
            }

            // 2. Rewarded Revive Option
            Rect reviveRect = new Rect(mx + 28, my + 204, modalW - 56, 34);
            if (contractSetupError != null)
            {
                TacticalGUI.DrawBadge(reviveRect, "RETRY OR CHOOSE ANOTHER SECTOR", TacticalGUI.AccentGold, 11);
            }
            else if (!hasUsedAdRevive && player != null && player.Health != null && player.Health.IsDead)
            {
                if (TacticalGUI.DrawButton(reviveRect, "🎬 EMERGENCY REVIVE (WATCH AD)", false, 12))
                {
                    AdManager.Instance.ShowRewardedAd((success) =>
                    {
                        if (success)
                        {
                            hasUsedAdRevive = true;
                            RevivePlayerFromDeath();
                        }
                    }, "emergency_revive");
                }
            }
            else
            {
                TacticalGUI.DrawBadge(reviveRect, hasUsedAdRevive ? "EMERGENCY REVIVE ALREADY USED" : "OBJECTIVE LOST - RETRY CONTRACT", TacticalGUI.AccentRed, 11);
            }

            // 3. Retry Contract Button
            Rect retryRect = new Rect(mx + 28, my + 244, modalW - 56, 34);
            if (TacticalGUI.DrawButton(retryRect, "ARMORY", false, 13))
            {
                ExitToArmory();
            }

            // 4. Abort Button
            Rect exitRect = new Rect(mx + 28, my + 284, modalW - 56, 34);
            if (TacticalGUI.DrawButton(exitRect, "✕ ABORT TO LEVEL SCREEN", false, 11))
            {
                ExitToLevelMap();
            }
        }

        public void ExitToArmory()
        {
            PlayerPrefs.SetInt("GeoSniper.OpenArmoryOnExit", 1);
            PlayerPrefs.Save();
            ExitToLevelMap();
        }

        void RevivePlayerFromDeath()
        {
            if (player != null && player.Health != null)
            {
                player.Health.Initialize(100);
            }
            currentMissionState = MissionState.InProgress;
            shotNotice = "OPERATIVE REVIVED - GET TO COVER!";
            shotNoticeTime = 2.5f;
        }

        void DrawTacticalHUDButtons(float width, float height)
        {
            // Vertical Tactical Action Dock on Left Edge (Well below the top header text bar, above bottom controls)
            float dockX = 14f;
            float dockY = 48f;
            float btnW = 76f;
            float btnH = 28f;
            float spacing = 6f;

            // 1. Mission Abort Button
            if (mapClosedCooldown <= 0f)
            {
                Rect abortRect = new Rect(dockX, dockY, btnW, btnH);
                if (TacticalGUI.DrawButton(abortRect, "✕ ABORT", false, 11))
                {
                    ExitToLevelMap();
                }
            }

            // 2. FLIR Thermal Vision
            bool thermalOn = weapon != null && weapon.ThermalMode;
            string thermLabel = thermalOn ? "🔥 FLIR" : "❄️ FLIR";
            if (TacticalGUI.DrawButton(new Rect(dockX, dockY + (btnH + spacing) * 1, btnW, btnH), thermLabel, thermalOn, 10))
            {
                ToggleThermalMode();
            }

            // 3. UAV Recon Scan
            string uavLabel = uavTimer > 0 ? "🛰️ SCAN" : uavCooldown > 0 ? $"🛰️ {Mathf.CeilToInt(uavCooldown)}s" : "🛰️ UAV";
            if (TacticalGUI.DrawButton(new Rect(dockX, dockY + (btnH + spacing) * 2, btnW, btnH), uavLabel, uavTimer > 0, 10))
            {
                TriggerUAVRecon();
            }

            // 4. Environment / Weather Cycle
            string envLabel = envState == 0 ? "☀️ DAY" : envState == 1 ? "🌙 NIGHT" : envState == 2 ? "🌧️ RAIN" : "⛈️ STORM";
            if (TacticalGUI.DrawButton(new Rect(dockX, dockY + (btnH + spacing) * 3, btnW, btnH), envLabel, false, 10))
            {
                CycleEnvironment();
            }

            // 5. Recon Rangefinder / Binoculars
            bool binosOn = player != null && player.BinocularsMode;
            string reconLabel = binosOn ? "🔭 ON" : "🔭 RECON";
            if (TacticalGUI.DrawButton(new Rect(dockX, dockY + (btnH + spacing) * 4, btnW, btnH), reconLabel, binosOn, 10))
            {
                if (player != null) player.ToggleBinoculars();
                shotNotice = (player != null && player.BinocularsMode) ? "🔭 RECON BINOCULARS ACTIVATED" : "RECON MODE DEACTIVATED";
                shotNoticeTime = 2.0f;
            }


            // Sound Masking Banner
            if (IsSoundMasked)
            {
                Rect maskRect = new Rect(width / 2 - 225, 78, 450, 24);
                TacticalGUI.DrawPanel(maskRect, new Color(0.04f, 0.20f, 0.08f, 0.92f), new Color(0.2f, 0.95f, 0.35f), 1.2f);
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 8f);
                GUI.color = new Color(0.3f, 1f, 0.45f, pulse);
                GUI.Label(maskRect, $"🔊 ACOUSTIC MASK: {activeSoundMaskName.ToUpperInvariant()} ({Mathf.CeilToInt(activeSoundMaskTimer)}s) — SHOTS SILENCED", new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.35f, 1f, 0.5f) } });
                GUI.color = Color.white;
            }

            // Active UAV Scan Banner
            if (uavTimer > 0)
            {
                GUI.color = new Color(0f, 0.9f, 1f, 0.85f);
                GUI.Label(new Rect(width / 2 - 150, 50, 300, 24), $"🛰️ UAV RECON SWEEP ACTIVE ({Mathf.CeilToInt(uavTimer)}s)", new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.cyan } });
                GUI.color = Color.white;
                DrawUAVMarkers(width,height);
            }


        }

        static Texture2D binoOcularTex = null;
        static Texture2D GetBinoOcularTexture()
        {
            if (binoOcularTex != null) return binoOcularTex;
            int size = 256;
            binoOcularTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "BinoOcularMask" };
            Color32[] px = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float maxR = center;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / maxR;
                    if (dist >= 1.0f)
                    {
                        px[y * size + x] = new Color32(5, 8, 12, 255);
                    }
                    else if (dist > 0.88f)
                    {
                        float a = Mathf.SmoothStep(0f, 1f, (dist - 0.88f) / 0.12f);
                        px[y * size + x] = new Color(0.02f, 0.03f, 0.05f, a);
                    }
                    else
                    {
                        px[y * size + x] = new Color(0f, 0f, 0f, 0f);
                    }
                }
            }
            binoOcularTex.SetPixels32(px);
            binoOcularTex.Apply();
            return binoOcularTex;
        }

        float binoTagLockTimer = 0f;
        EnemyBot binoLockTargetEnemy = null;

        void DrawTacticalBinocularsHUD(float width, float height)
        {
            if (cameraView == null || player == null) return;
            float s = Mathf.Max(.4f, Mathf.Min(Screen.height / 720f, Screen.width / 800f));

            // 1. Dual-Ocular Optical Vignette
            float d = height * 0.96f;
            float eyeSep = d * 0.44f;
            float leftEyeX = width * 0.5f - eyeSep * 0.5f - d * 0.5f;
            float rightEyeX = width * 0.5f + eyeSep * 0.5f - d * 0.5f;
            float eyeY = (height - d) * 0.5f;

            Texture2D mask = GetBinoOcularTexture();
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(leftEyeX, eyeY, d, d), mask);
            GUI.DrawTexture(new Rect(rightEyeX, eyeY, d, d), mask);

            // Solid black outer borders
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 1.0f);
            if (leftEyeX > 0) GUI.DrawTexture(new Rect(0, 0, leftEyeX + 2, height), Texture2D.whiteTexture);
            if (rightEyeX + d < width) GUI.DrawTexture(new Rect(rightEyeX + d - 2, 0, width - (rightEyeX + d - 2), height), Texture2D.whiteTexture);
            if (eyeY > 0)
            {
                GUI.DrawTexture(new Rect(0, 0, width, eyeY + 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, eyeY + d - 2, width, height - (eyeY + d - 2)), Texture2D.whiteTexture);
            }

            float cx = width * 0.5f;
            float cy = height * 0.5f;

            // 2. Top Azimuth Compass Tape
            DrawBinoCompassTape(cx, 38f, 360f, 32f);

            // 3. Stadiametric Center Reticle & Mil-Dots
            GUI.color = new Color(0.2f, 0.95f, 0.4f, 0.75f);
            GUI.DrawTexture(new Rect(cx - 24, cy - 1, 18, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6, cy - 1, 18, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy - 24, 2, 18), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy + 6, 2, 18), Texture2D.whiteTexture);

            for (int i = 1; i <= 5; i++)
            {
                float offset = i * 22f;
                float tickH = (i % 2 == 0) ? 8f : 5f;
                GUI.DrawTexture(new Rect(cx - offset, cy - tickH * 0.5f, 1.5f, tickH), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + offset, cy - tickH * 0.5f, 1.5f, tickH), Texture2D.whiteTexture);
                float tickW = (i % 2 == 0) ? 8f : 5f;
                GUI.DrawTexture(new Rect(cx - tickW * 0.5f, cy - offset, tickW, 1.5f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - tickW * 0.5f, cy + offset, tickW, 1.5f), Texture2D.whiteTexture);
            }

            // 4. Center Laser Rangefinder Raycast & Enemy Lock-on Detection
            Ray centerRay = cameraView.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float rangeMeters = 0f;
            EnemyBot aimedEnemy = null;
            string targetObj = "CLEAR TERRAIN";

            if (Physics.Raycast(centerRay, out var hit, 1500f, ~0, QueryTriggerInteraction.Ignore))
            {
                rangeMeters = hit.distance;
                aimedEnemy = hit.collider.GetComponentInParent<EnemyBot>();
                if (aimedEnemy != null)
                {
                    targetObj = $"HOSTILE [{aimedEnemy.currentRole.ToString().ToUpperInvariant()}]";
                }
                else if (hit.collider.GetComponentInParent<CivilianBot>() != null)
                {
                    targetObj = "CIVILIAN [NO-FIRE]";
                }
                else
                {
                    targetObj = hit.collider.gameObject.name.ToUpperInvariant();
                    if (targetObj.Length > 18) targetObj = targetObj.Substring(0, 18);
                }
            }

            // Lock-on Enemy Tagging Logic (0.5s hold)
            if (aimedEnemy != null && aimedEnemy.Actor != null && !aimedEnemy.Actor.IsDead)
            {
                if (binoLockTargetEnemy == aimedEnemy)
                {
                    binoTagLockTimer += Time.deltaTime;
                }
                else
                {
                    binoLockTargetEnemy = aimedEnemy;
                    binoTagLockTimer = 0f;
                }

                float lockProgress = Mathf.Clamp01(binoTagLockTimer / 0.5f);
                float bracketDist = Mathf.Lerp(45f, 16f, lockProgress);
                Color lockCol = lockProgress >= 1f ? new Color(1f, 0.85f, 0.15f, 0.95f) : new Color(0.2f, 0.9f, 1f, 0.85f);
                GUI.color = lockCol;

                GUI.DrawTexture(new Rect(cx - bracketDist, cy - bracketDist, 8, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - bracketDist, cy - bracketDist, 2, 8), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + bracketDist - 8, cy - bracketDist, 8, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + bracketDist - 2, cy - bracketDist, 2, 8), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - bracketDist, cy + bracketDist - 2, 8, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - bracketDist, cy + bracketDist - 8, 2, 8), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + bracketDist - 8, cy + bracketDist - 2, 8, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + bracketDist - 2, cy + bracketDist - 8, 2, 8), Texture2D.whiteTexture);

                if (lockProgress < 1f)
                {
                    Rect pBar = new Rect(cx - 30, cy + 22, 60 * lockProgress, 3);
                    GUI.DrawTexture(pBar, Texture2D.whiteTexture);
                }

                if (binoTagLockTimer >= 0.5f && !aimedEnemy.IsTagged)
                {
                    aimedEnemy.TagEnemy(45f);
                    AudioSource spk = cameraView.GetComponent<AudioSource>();
                    if (spk != null)
                    {
                        var clip = ProceduralAudio.LoadAsset("HitMarker");
                        if (clip != null) spk.PlayOneShot(clip, 0.95f);
                    }
                    shotNotice = $"◆ TARGET TAGGED: {aimedEnemy.currentRole.ToString().ToUpperInvariant()} [{Mathf.RoundToInt(rangeMeters)}m]";
                    shotNoticeTime = 2.5f;
                }
            }
            else
            {
                binoTagLockTimer = Mathf.MoveTowards(binoTagLockTimer, 0f, Time.deltaTime * 3f);
                if (binoTagLockTimer <= 0f) binoLockTargetEnemy = null;
            }

            // 5. Ballistics & Rangefinder Telemetry Box
            float boxW = 260f;
            float boxH = 95f;
            float boxX = cx - boxW * 0.5f;
            float boxY = cy + 120f;
            TacticalGUI.DrawPanel(new Rect(boxX, boxY, boxW, boxH), new Color(0.04f, 0.08f, 0.06f, 0.88f), new Color(0.2f, 0.9f, 0.4f, 0.75f), 1f);

            var readStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.45f, 1f, 0.65f, 0.95f) }
            };
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.3f, 0.8f, 0.5f, 0.75f) }
            };

            float pitch = -cameraView.transform.eulerAngles.x;
            if (pitch < -180f) pitch += 360f;
            float yaw = cameraView.transform.eulerAngles.y;

            float bulletVel = 850f;
            float tof = rangeMeters > 0 ? rangeMeters / bulletVel : 0f;
            float dropM = 0.5f * 9.81f * tof * tof;
            float holdoverMil = rangeMeters > 5f ? (dropM / rangeMeters) * 1000f : 0f;

            GUI.Label(new Rect(boxX + 12, boxY + 8, 110, 16), "RANGEFINDER", headerStyle);
            GUI.Label(new Rect(boxX + 12, boxY + 22, 120, 20), rangeMeters > 0 ? $"{rangeMeters:F1} M" : "---.- M", readStyle);

            GUI.Label(new Rect(boxX + 130, boxY + 8, 110, 16), "ELEVATION / AZ", headerStyle);
            GUI.Label(new Rect(boxX + 130, boxY + 22, 120, 20), $"{pitch:+0.0;-0.0;0.0}°  {yaw:000}°", readStyle);

            GUI.Label(new Rect(boxX + 12, boxY + 44, 110, 16), "BALLISTIC HOLDOVER", headerStyle);
            GUI.Label(new Rect(boxX + 12, boxY + 58, 130, 20), $"+{holdoverMil:F1} MIL ({dropM:F2}m)", readStyle);

            GUI.Label(new Rect(boxX + 130, boxY + 44, 110, 16), "OPTIC MAGNIFICATION", headerStyle);
            GUI.Label(new Rect(boxX + 130, boxY + 58, 120, 20), $"{player.BinocularZoom:F1}× [SCROLL]", readStyle);

            GUI.Label(new Rect(boxX + 12, boxY + 76, 236, 16), $"TARGET: {targetObj}", headerStyle);

            // 6. Draw Persistent Tactical Badges for Tagged and In-View Hostiles
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.Actor == null || enemy.Actor.IsDead) continue;
                Vector3 p = cameraView.WorldToScreenPoint(enemy.transform.position + Vector3.up * 1.8f);
                if (p.z <= 0) continue;
                float ex = p.x / s;
                float ey = (Screen.height - p.y) / s;
                float dist = Vector3.Distance(player.transform.position, enemy.transform.position);

                string botTag = enemy.isCounterSniper ? "SNIPER" : enemy.currentRole.ToString().ToUpperInvariant();
                if (enemy.IsTagged)
                {
                    TacticalGUI.DrawBadge(new Rect(ex - 48, ey - 24, 96, 22), $"◆ {botTag} {Mathf.RoundToInt(dist)}m", new Color(1f, 0.85f, 0.15f), 9);
                }
                else
                {
                    TacticalGUI.DrawBadge(new Rect(ex - 40, ey - 24, 80, 22), $"▼ {botTag} {Mathf.RoundToInt(dist)}m", enemy.isCounterSniper ? Color.red : new Color(1f, 0.65f, 0.1f), 9);
                }
            }

            if (currentMissionState == MissionState.Extraction)
            {
                Vector3 ep = cameraView.WorldToScreenPoint(extractionPoint + Vector3.up * 2f);
                if (ep.z > 0)
                {
                    float ex = ep.x / s;
                    float ey = (Screen.height - ep.y) / s;
                    float dist = Vector3.Distance(player.transform.position, extractionPoint);
                    TacticalGUI.DrawBadge(new Rect(ex - 48, ey - 24, 96, 22), $"🚁 LZ {Mathf.RoundToInt(dist)}m", Color.green, 9);
                }
            }

            GUI.color = Color.white;
        }

        void DrawBinoCompassTape(float centerX, float y, float width, float height)
        {
            float heading = cameraView.transform.eulerAngles.y;
            Rect bgRect = new Rect(centerX - width * 0.5f, y, width, height);
            TacticalGUI.DrawPanel(bgRect, new Color(0.02f, 0.06f, 0.04f, 0.82f), new Color(0.2f, 0.85f, 0.4f, 0.6f), 1f);

            var tickStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(0.4f, 1f, 0.6f, 0.9f) }
            };

            float pixelsPerDeg = width / 70f;
            float leftDeg = heading - 35f;
            float rightDeg = heading + 35f;

            int startTick = Mathf.FloorToInt(leftDeg / 5f) * 5;
            int endTick = Mathf.CeilToInt(rightDeg / 5f) * 5;

            Color oldC = GUI.color;
            for (int deg = startTick; deg <= endTick; deg += 5)
            {
                float x = centerX + (deg - heading) * pixelsPerDeg;
                if (x < bgRect.x + 8 || x > bgRect.xMax - 8) continue;

                int normDeg = (deg % 360 + 360) % 360;
                bool isMajor = (normDeg % 45 == 0);
                bool isCardinal = (normDeg % 90 == 0);

                float tickH = isCardinal ? 14f : (isMajor ? 10f : 6f);
                GUI.color = isCardinal ? new Color(0.3f, 1f, 0.6f, 0.95f) : new Color(0.3f, 0.85f, 0.45f, 0.65f);
                GUI.DrawTexture(new Rect(x - 0.75f, y + height - tickH - 2, 1.5f, tickH), Texture2D.whiteTexture);

                if (isMajor)
                {
                    string label = normDeg == 0 ? "N" : normDeg == 45 ? "NE" : normDeg == 90 ? "E" : normDeg == 135 ? "SE" : normDeg == 180 ? "S" : normDeg == 225 ? "SW" : normDeg == 270 ? "W" : "NW";
                    GUI.Label(new Rect(x - 16, y + 2, 32, 14), label, tickStyle);
                }
            }

            GUI.color = new Color(1f, 0.85f, 0.15f, 0.95f);
            GUI.Label(new Rect(centerX - 24, y + height - 14, 48, 14), "▲", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.85f, 0.15f) }, fontSize = 10 });
            GUI.Label(new Rect(centerX - 30, y - 18, 60, 18), $"{Mathf.RoundToInt(heading)}°", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.15f) }, fontSize = 11 });

            GUI.color = oldC;
        }

        bool HasAnyTaggedEnemies()
        {
            if (enemies == null) return false;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].IsTagged) return true;
            }
            return false;
        }

        void UpdateSoundMasking()
        {
            if (activeSoundMaskTimer > 0f)
            {
                activeSoundMaskTimer -= Time.deltaTime;
                if (activeSoundMaskTimer <= 0f)
                {
                    activeSoundMaskTimer = 0f;
                    activeSoundMaskName = "";
                }
            }
            else
            {
                soundMaskInterval -= Time.deltaTime;
                if (soundMaskInterval <= 0f)
                {
                    soundMaskInterval = Random.Range(18f, 30f);
                    string[] maskEvents = new string[]
                    {
                        "Thunderclap Rumble",
                        "Freight Train Crossing",
                        "Low-Altitude Jet Flyover",
                        "Heavy Industrial Siren"
                    };
                    activeSoundMaskName = maskEvents[Random.Range(0, maskEvents.Length)];
                    activeSoundMaskTimer = Random.Range(5.5f, 8.5f);
                    AudioSource speaker = cameraView != null ? cameraView.GetComponent<AudioSource>() : audioSource;
                    if (speaker != null)
                    {
                        speaker.PlayOneShot(ProceduralAudio.CreateSoundMaskRumble(), 0.9f);
                    }
                }
            }
        }

        void DrawUAVMarkers(float width,float height)
        {
            if(cameraView==null) return;
            foreach(var enemy in enemies)
            {
                if(enemy==null || enemy.Actor==null || enemy.Actor.IsDead) continue;
                Vector3 p=cameraView.WorldToScreenPoint(enemy.transform.position+Vector3.up*2f);
                if(p.z<=0) continue;
                float x=p.x/Mathf.Max(.01f,Mathf.Max(.4f,Mathf.Min(Screen.height/720f,Screen.width/800f)));
                float y=(Screen.height-p.y)/Mathf.Max(.01f,Mathf.Max(.4f,Mathf.Min(Screen.height/720f,Screen.width/800f)));
                TacticalGUI.DrawBadge(new Rect(x-42,y-14,84,28),"HOSTILE",Color.red,9);
            }
        }

        void ToggleThermalMode()
        {
            if (weapon != null) weapon.ThermalMode = !weapon.ThermalMode;
        }

        void TriggerUAVRecon()
        {
            if (uavCooldown > 0) return;
            uavTimer = 14f;
            uavCooldown = 45f;
            shotNotice = "🛰️ UAV RECON DRONE ACTIVATED";
            shotNoticeTime = 2.5f;
        }

        static void ApplyWeaponConfiguration(int index, BallisticsConfig ballistic, WeaponConfig presentation)
        {
            float damage = index == 0 ? 120f : index == 1 ? 85f : 45f;
            ballistic.baseDamage = damage * (1f + Mathf.Clamp(PlayerPrefs.GetInt("GeoSniper.WpnDmgLvl_" + index, 0),0,2) * .2f);
            ballistic.noiseRadius = index == 2 ? 25f : 130f;
            float zoom = Mathf.Max(3f, (index == 0 ? 10f : index == 1 ? 14f : 18f)
                - Mathf.Clamp(PlayerPrefs.GetInt("GeoSniper.WpnScopeLvl_" + index, 0),0,2) * 3.5f);
            presentation.zoomLevels = new[] { zoom, zoom * .5f };
        }

        void SelectWeapon(int index)
        {
            if (weapon == null || index == weapon.CurrentWeaponIndex || reloadTimer > 0 || chamberTimer > 0 || BallisticsSystem.isBulletCamActive) return;
            if (!IsWeaponOwned(index))
            {
                shotNotice = "RIFLE LOCKED — BUY IT IN ARMORY";
                shotNoticeTime = 1.5f;
                return;
            }
            weaponAmmo[weapon.CurrentWeaponIndex] = ammo;
            ApplyWeaponConfiguration(index, ballistics.config, weapon.Config);
            weapon.EquipWeaponModel(index);
            ammo = weaponAmmo[index] < 0 ? weapon.MaxAmmo : Mathf.Min(weaponAmmo[index], weapon.MaxAmmo);
            fireCooldown = Mathf.Max(fireCooldown, .25f);
            weapon.ReloadSound();
            shotNotice = "EQUIPPED: " + weapon.WeaponName;
            shotNoticeTime = 1.5f;
            if (player != null && player.Controls != null)
            {
                player.Controls.SelectedWeaponIndex = weapon.CurrentWeaponIndex;
            }
        }
        bool IsWeaponOwned(int index)
        {
            if (GeoSniperGame.IsDebugMode) return index >= 0 && index < 3;
            return index >= 0 && index < 3 && PlayerPrefs.GetInt("GeoSniper.WeaponUnlocked_" + index, index == 1 ? 1 : 0) == 1;
        }

        void CycleEnvironment()
        {
            manualEnvironment=true;
            envState = (envState + 1) % 4;
            ApplyEnvironment();
            shotNoticeTime = 2.0f;
        }

        void ApplyEnvironment()
        {
            bool isNight = (envState == 1 || envState == 3);
            bool isRain = (envState == 2 || envState == 3);

            SectorStreetLighting.SetNight(isNight, 0f);
            MobileGraphics.Apply();

            var sky = AtmosphericSky.Create(cameraView ?? Camera.main);
            if (sky != null)
            {
                int code = isRain ? 61 : 0;
                sky.SetWeatherAtmosphere(code, !isNight);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;

            float sunIntensity;
            Color sunColor;
            Quaternion sunRot;

            if (envState == 0) // CRISP DAYLIGHT
            {
                sunIntensity = 1.45f;
                sunColor = new Color(1.0f, 0.95f, 0.86f);
                sunRot = Quaternion.Euler(46f, 138f, 0f);

                RenderSettings.ambientSkyColor = new Color(0.55f, 0.68f, 0.84f);
                RenderSettings.ambientEquatorColor = new Color(0.44f, 0.46f, 0.44f);
                RenderSettings.ambientGroundColor = new Color(0.22f, 0.24f, 0.26f);
                RenderSettings.fogColor = new Color(0.72f, 0.80f, 0.88f);
                RenderSettings.fogStartDistance = 220f;
                RenderSettings.fogEndDistance = 1400f;

                if (weapon != null) weapon.RainEffect = false;
                WorldRainSystem.SetRain(false);
                shotNotice = "ENVIRONMENT: DAYLIGHT";
            }
            else if (envState == 1) // DEEP MIDNIGHT MISSION
            {
                sunIntensity = 0.12f; // Cool moonlight
                sunColor = new Color(0.28f, 0.44f, 0.78f);
                sunRot = Quaternion.Euler(62f, -42f, 0f);

                RenderSettings.ambientSkyColor = new Color(0.025f, 0.04f, 0.10f);
                RenderSettings.ambientEquatorColor = new Color(0.018f, 0.025f, 0.05f);
                RenderSettings.ambientGroundColor = new Color(0.008f, 0.012f, 0.02f);
                RenderSettings.fogColor = new Color(0.022f, 0.038f, 0.085f);
                RenderSettings.fogStartDistance = 35f;
                RenderSettings.fogEndDistance = 580f;

                if (weapon != null) weapon.RainEffect = false;
                WorldRainSystem.SetRain(false);
                shotNotice = "ENVIRONMENT: NIGHT MISSION";
            }
            else if (envState == 2) // DAY STORM & RAIN
            {
                sunIntensity = 0.62f;
                sunColor = new Color(0.70f, 0.76f, 0.84f);
                sunRot = Quaternion.Euler(54f, 125f, 0f);

                RenderSettings.ambientSkyColor = new Color(0.32f, 0.36f, 0.42f);
                RenderSettings.ambientEquatorColor = new Color(0.26f, 0.28f, 0.32f);
                RenderSettings.ambientGroundColor = new Color(0.14f, 0.16f, 0.18f);
                RenderSettings.fogColor = new Color(0.35f, 0.40f, 0.46f);
                RenderSettings.fogStartDistance = 45f;
                RenderSettings.fogEndDistance = 520f;

                if (weapon != null) weapon.RainEffect = true;
                WorldRainSystem.SetRain(true, true);
                shotNotice = "ENVIRONMENT: RAIN & STORM";
            }
            else // RAINY NIGHT
            {
                sunIntensity = 0.06f; // Dim moon behind storm clouds
                sunColor = new Color(0.18f, 0.28f, 0.50f);
                sunRot = Quaternion.Euler(65f, -42f, 0f);

                RenderSettings.ambientSkyColor = new Color(0.015f, 0.025f, 0.06f);
                RenderSettings.ambientEquatorColor = new Color(0.01f, 0.018f, 0.035f);
                RenderSettings.ambientGroundColor = new Color(0.005f, 0.008f, 0.012f);
                RenderSettings.fogColor = new Color(0.016f, 0.024f, 0.05f);
                RenderSettings.fogStartDistance = 25f;
                RenderSettings.fogEndDistance = 380f;

                if (weapon != null) weapon.RainEffect = true;
                WorldRainSystem.SetRain(true, true);
                shotNotice = "ENVIRONMENT: RAINY NIGHT";
            }

            // Synchronize all directional lights in scene so ghost suns don't blow out night
            var allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in allLights)
            {
                if (light == null) continue;
                if (light.name.StartsWith("Lobby", System.StringComparison.OrdinalIgnoreCase))
                {
                    light.enabled = false;
                    continue;
                }
                if (light.type == LightType.Directional)
                {
                    light.enabled = true;
                    light.intensity = sunIntensity;
                    light.color = sunColor;
                    light.transform.rotation = sunRot;
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = isNight ? 0.95f : 0.85f;
                }
            }
        }

        void CallAirStrike()
        {
            if(FirstContactMode) {shotNotice="FIRST CONTACT: RIFLE ONLY";shotNoticeTime=2;return;}
            if (!hasWaypointPin) return;
            Vector3 strikePos = waypointPinPos;
            StartCoroutine(ExecuteAirStrike(strikePos));
            hasWaypointPin = false;
            CloseMap();
        }

        System.Collections.IEnumerator ExecuteAirStrike(Vector3 target)
        {
            shotNotice = "🚀 PRECISION MISSILE INBOUND!";
            shotNoticeTime = 3.5f;
            yield return new WaitForSeconds(1.5f);

            SniperPresentation.SpawnImpactSparks(target);
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = Random.insideUnitSphere * 8f; offset.y = 0;
                SniperPresentation.SpawnImpactSparks(target + offset);
            }

            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeSelf) continue;
                if (Vector3.Distance(enemy.transform.position, target) <= 25f)
                {
                    var actor = enemy.GetComponent<CombatActor>();
                    if (actor != null && !actor.IsDead)
                    {
                        actor.Damage(500f);
                    }
                }
            }

            shotNotice = "💥 AIR STRIKE IMPACT CONFIRMED!";
            shotNoticeTime = 3f;
        }
        readonly List<Rect> drawnMarkerRects = new List<Rect>();

        void DrawTargetMarkers(float width, float height)
        {
            if (cameraView == null || Event.current.type != EventType.Repaint) return;
            drawnMarkerRects.Clear();

            // Tactical VIP Marker
            if (stageMode && activeContract.Type == CampaignContractType.Overwatch && contractVIP != null && contractVIP.gameObject.activeSelf && contractVIP.Actor != null && !contractVIP.Actor.IsDead)
            {
                var vipTarget = contractVIP.transform.position + Vector3.up * 1.65f;
                var screen = cameraView.WorldToViewportPoint(vipTarget + Vector3.up * 0.45f);
                if (screen.z > 0 && screen.x >= .06f && screen.x <= .84f && screen.y >= .12f && screen.y <= .88f)
                {
                    float metres = Vector3.Distance(cameraView.transform.position, vipTarget);
                    int hp = Mathf.RoundToInt(contractVIP.Actor.Health);
                    string labelText = $"VIP  {hp} HP  |  {Mathf.RoundToInt(metres)}m";
                    Color vipCol = hp < 40 ? new Color(1f, 0.25f, 0.25f) : (hp < 75 ? new Color(1f, 0.85f, 0.2f) : new Color(0.15f, 0.95f, 0.45f));
                    DrawTacticalScreenBadge(new Vector2(screen.x * width, (1 - screen.y) * height), labelText, vipCol,width,height);
                }
            }

            // Tactical Extraction Marker
            if (stageMode && (activeContract.Type == CampaignContractType.Overwatch || activeContract.Type == CampaignContractType.TimedInterception || activeContract.Type == CampaignContractType.Escape))
            {
                var screen = cameraView.WorldToViewportPoint(extractionPoint + Vector3.up * 1.5f);
                if (screen.z > 0 && screen.x >= .06f && screen.x <= .84f && screen.y >= .12f && screen.y <= .88f)
                {
                    float metres = Vector3.Distance(cameraView.transform.position, extractionPoint);
                    bool isHostileEsc = (activeContract.Type == CampaignContractType.TimedInterception);
                    string extTag = isHostileEsc ? $"ESCAPE {Mathf.RoundToInt(metres)}m" : $"EXTRACTION {Mathf.RoundToInt(metres)}m";
                    Color col = isHostileEsc ? new Color(1f, 0.55f, 0.15f) : new Color(0.2f, 0.9f, 1f);
                    DrawTacticalScreenBadge(new Vector2(screen.x * width, (1 - screen.y) * height), extTag, col,width,height);
                }
            }

            // Hostiles / Enemies
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeSelf || enemy.Actor==null || enemy.Actor.IsDead || (FirstContactMode && enemy != contractTarget)) continue;
                if(drawnMarkerRects.Count>=8 && enemy!=contractTarget && enemy!=contractCounterSniper && !enemy.IsTagged)continue;
                var target = enemy.transform.position + Vector3.up * 1.65f;
                bool isObstructed = Physics.Linecast(cameraView.transform.position, target, out var obstruction, ~(1 << 2), QueryTriggerInteraction.Ignore)
                    && obstruction.collider.GetComponentInParent<EnemyBot>() != enemy;
                if (isObstructed && !enemy.IsTagged) continue;

                var screen = cameraView.WorldToViewportPoint(target + Vector3.up * 0.45f);
                if (screen.z <= 0 || screen.x < .02f || screen.x > .98f || screen.y < .04f || screen.y > .96f) continue;
                float metres = Vector3.Distance(cameraView.transform.position, target);

                string tag;
                Color tagCol = new Color(1f, 0.28f, 0.24f);

                if (enemy.IsTagged)
                {
                    string roleName = enemy.isCounterSniper ? "SNIPER" : enemy.currentRole.ToString().ToUpperInvariant();
                    tag = $"{roleName}  {Mathf.RoundToInt(metres)}m";
                    tagCol = new Color(1f, 0.85f, 0.15f);
                }
                else if (enemy == contractCounterSniper)
                {
                    tag = $"SNIPER  {Mathf.RoundToInt(metres)}m";
                    tagCol = new Color(1f, 0.18f, 0.18f);
                }
                else if (stageMode && activeContract.Type == CampaignContractType.TimedInterception && enemy == contractTarget)
                {
                    tag = $"COURIER  {Mathf.RoundToInt(metres)}m";
                    tagCol = new Color(1f, 0.55f, 0.15f);
                }
                else if (stageMode && activeContract.Type == CampaignContractType.TargetIdentification && enemy == contractTarget)
                {
                    tag = $"HVT  {Mathf.RoundToInt(metres)}m";
                    tagCol = new Color(1f, 0.25f, 0.2f);
                }
                else if (stageMode && activeContract.Type == CampaignContractType.Overwatch)
                {
                    tag = $"AMBUSHER  {Mathf.RoundToInt(metres)}m";
                }
                else
                {
                    tag = $"HOSTILE  {Mathf.RoundToInt(metres)}m";
                }

                DrawTacticalScreenBadge(new Vector2(screen.x * width, (1 - screen.y) * height), tag, tagCol,width,height);
            }
        }

        void DrawTacticalScreenBadge(Vector2 screenPos, string text, Color color,float width,float height)
        {
            var style=GUIStyleCache.Get(12,FontStyle.Bold,TextAnchor.MiddleCenter,Color.white);
            float badgeW = Mathf.Max(64f,style.CalcSize(new GUIContent(text)).x+20f);
            float badgeH = 24f;
            Rect rect = new Rect(screenPos.x - badgeW * 0.5f, screenPos.y - badgeH * 0.5f, badgeW, badgeH);
            rect.x=Mathf.Clamp(rect.x,12f,Mathf.Max(12f,width-badgeW-12f));

            // Anti-overlap resolution: shift downward if colliding with another badge
            for (int attempt = 0; attempt < 5; attempt++)
            {
                bool collides = false;
                foreach (var r in drawnMarkerRects)
                {
                    if (r.Overlaps(rect)) { collides = true; break; }
                }
                if(collides && attempt==4)return;
                if (collides) rect.y -= 26f;
                else break;
            }
            if(rect.y<55f || rect.yMax>height-70f)return;
            drawnMarkerRects.Add(rect);

            // Sleek tactical semi-transparent badge
            GUI.color = new Color(0.02f, 0.03f, 0.04f, 0.84f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), Texture2D.whiteTexture);
            GUI.color=Color.white;
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
        }

        void SampleVipRoute(float fraction, out Vector3 point, out Vector3 direction)
        {
            float total = 0f;
            for (int i = 1; i < contractVipPath.Count; i++) total += FlatDistance(contractVipPath[i - 1], contractVipPath[i]);
            float remaining = total * Mathf.Clamp01(fraction);
            for (int i = 1; i < contractVipPath.Count; i++)
            {
                Vector3 leg = contractVipPath[i] - contractVipPath[i - 1];
                leg.y = 0f;
                float length = leg.magnitude;
                if (length < .01f) continue;
                if (remaining <= length || i == contractVipPath.Count - 1)
                {
                    point = Vector3.Lerp(contractVipPath[i - 1], contractVipPath[i], Mathf.Clamp01(remaining / length));
                    direction = leg / length;
                    return;
                }
                remaining -= length;
            }
            point = contractVipPath[contractVipPath.Count - 1];
            direction = (point - contractVipPath[0]).normalized;
        }
        void DrawMinimap(float width,float height)
        {
            if(player==null) return;
            if (height < 330) { Rect smMapBtn = new Rect(width - 135, 120, 117, 32); if (TacticalGUI.DrawButton(smMapBtn, "🗺️ MAP [M]", true, 11)) OpenMap(); return; }
            float size = Mathf.Clamp(height * 0.19f, 105f, 125f);
            float left = width - size - 16;
            float top = 48;
            Rect minimapHitRect = new Rect(left - 2, top - 20, size + 4, size + 24);
            if (TacticalGUI.IsClicked(minimapHitRect)) OpenMap();

            TacticalGUI.DrawPanel(new Rect(left, top, size, size), new Color(0.03f, 0.06f, 0.08f, 0.85f), TacticalGUI.AccentCyan, 1.2f);
            string district = GetTargetBuildingLocationName();
            if (district.Length > 16) district = district.Substring(0, 16);
            GUI.Label(new Rect(left, top - 18, size, 16), $"📍 {district.ToUpperInvariant()}", new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = TacticalGUI.AccentCyan } });

            Vector3 center = player.transform.position;
            DrawStreetMinimap(new Rect(left + 2, top + 2, size - 4, size - 4), center);
            GUI.color = Color.white;
            GUI.Label(new Rect(left + size - 18, top + 3, 16, 16), "N", MapText(10, new Color(0.20f, 0.28f, 0.36f)));

            // Clean bottom prompt overlay inside the minimap
            GUI.Label(new Rect(left, top + size - 15, size, 14), "TAP FOR MAP", new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.85f, 0.92f, 1f, 0.65f) } });

            radarEnemies.Clear(); radarMarkers.Clear();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeSelf) continue;
                Vector3 delta = enemy.transform.position - center;
                if (delta.x * delta.x + delta.z * delta.z > 85 * 85 || Mathf.Abs(delta.y) > 25) continue;
                radarEnemies.Add(enemy);
            }
            radarEnemies.Sort((a, b) => (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));
            foreach (var enemy in radarEnemies)
            {
                Vector3 delta = enemy.transform.position - center;
                Vector2 point = new Vector2(left + size / 2 + delta.x / 85 * (size / 2 - 12), top + size / 2 - delta.z / 85 * (size / 2 - 12));
                Vector2 radarCenter = new Vector2(left + size / 2, top + size / 2), bearing = point - radarCenter;
                if (bearing.sqrMagnitude < 15 * 15) point = radarCenter + (bearing.sqrMagnitude > .01f ? bearing.normalized : Vector2.up) * 15;
                if (radarMarkers.Exists(p => (point - p).sqrMagnitude < 11 * 11)) continue;
                radarMarkers.Add(point);

                // Height Marker: ▲ if above player, ▼ if below
                bool isAbove = enemy.transform.position.y - center.y > 3f;
                GUI.color = isAbove ? new Color(1f, .2f, .2f) : new Color(.95f, .46f, .19f);
                GUI.DrawTexture(new Rect(point.x - 3, point.y - 3, 6, 6), Texture2D.whiteTexture);
                if (isAbove)
                {
                    GUI.Label(new Rect(point.x - 6, point.y - 12, 12, 12), "▲", new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.red } });
                }
                if (radarMarkers.Count >= 8) break;
            }

            if (currentMissionState == MissionState.Extraction)
            {
                Vector3 delta = extractionPoint - center;
                Vector2 point = new Vector2(left + size / 2 + delta.x / 85 * (size / 2 - 12), top + size / 2 - delta.z / 85 * (size / 2 - 12));
                Vector2 radarCenter = new Vector2(left + size / 2, top + size / 2), bearing = point - radarCenter;
                if (bearing.sqrMagnitude < 15 * 15) point = radarCenter + (bearing.sqrMagnitude > .01f ? bearing.normalized : Vector2.up) * 15;
                GUI.color = new Color(0f, 1f, 0.2f);
                GUI.DrawTexture(new Rect(point.x - 4, point.y - 4, 8, 8), Texture2D.whiteTexture);
            }

            if (hasWaypointPin)
            {
                Vector3 pinDelta = waypointPinPos - center;
                float pinDist = waypointPin.Distance(center);
                Vector2 pinOffset = new Vector2(pinDelta.x / 85f * (size / 2 - 14), -pinDelta.z / 85f * (size / 2 - 14));
                Vector2 radarCenter = new Vector2(left + size / 2, top + size / 2);
                Vector2 pinPos = radarCenter + pinOffset;

                if (pinOffset.sqrMagnitude > (size / 2 - 14) * (size / 2 - 14))
                    pinPos = radarCenter + pinOffset.normalized * (size / 2 - 14);

                GUI.color = new Color(1f, .82f, .1f, .45f);
                DrawMapLine(radarCenter, pinPos, 1.5f);

                GUI.color = new Color(1f, .82f, .1f, 1f);
                DrawWaypointIcon(pinPos,new Color(1f,.78f,.16f),22);
                GUI.Label(new Rect(pinPos.x - 25, pinPos.y - 15, 50, 15), $"📍{Mathf.RoundToInt(pinDist)}m", new GUIStyle(GUI.skin.label) { fontSize = 9, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, .85f, .1f) } });
            }

            DrawModernPlayerMarker(new Vector2(left + size / 2, top + size / 2), 12);
            GUI.color = Color.white;
        }
        void DrawModernPlayerMarker(Vector2 center, float size)
        {
            Vector3 facing = player.transform.forward;
            Vector2 forward = new Vector2(facing.x, -facing.z).normalized;
            Vector2 right = new Vector2(-forward.y, forward.x);

            Vector2 fovLeft = (forward * 1.6f - right * 0.5f).normalized;
            Vector2 fovRight = (forward * 1.6f + right * 0.5f).normalized;
            GUI.color = new Color(0f, 0.95f, 1f, 0.25f);
            DrawMapLine(center, center + fovLeft * (size * 1.8f), 2f);
            DrawMapLine(center, center + fovRight * (size * 1.8f), 2f);

            float pulse = Mathf.PingPong(Time.time * 3f, 4f);
            GUI.color = new Color(0f, 0.9f, 1f, 0.4f);
            GUI.DrawTexture(new Rect(center.x - size * 0.5f - pulse * 0.5f, center.y - size * 0.5f - pulse * 0.5f, size + pulse, size + pulse), Texture2D.whiteTexture);

            Vector2 tip = center + forward * (size * 0.75f);
            Vector2 backLeft = center - forward * (size * 0.35f) - right * (size * 0.45f);
            Vector2 backRight = center - forward * (size * 0.35f) + right * (size * 0.45f);
            Vector2 backInner = center - forward * (size * 0.15f);

            GUI.color = new Color(0f, 0.1f, 0.15f, 0.9f);
            DrawMapLine(tip, backLeft, 4f);
            DrawMapLine(backLeft, backInner, 4f);
            DrawMapLine(backInner, backRight, 4f);
            DrawMapLine(backRight, tip, 4f);

            GUI.color = Color.white;
            DrawMapLine(tip, backLeft, 2f);
            DrawMapLine(backLeft, backInner, 2f);
            DrawMapLine(backInner, backRight, 2f);
            DrawMapLine(backRight, tip, 2f);

            GUI.color = new Color(0f, 0.9f, 1f, 1f);
            GUI.DrawTexture(new Rect(center.x - 3, center.y - 3, 6, 6), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawWaypointPath(Vector2 from, Vector2 to, Color pathColor)
        {
            Vector2 delta = to - from;
            float dist = delta.magnitude;
            if (dist < 4f) return;

            int dashes = Mathf.Clamp(Mathf.FloorToInt(dist / 14f), 2, 80);
            float animOffset = (Time.time * 25f) % 14f;

            for (int i = 0; i < dashes; i++)
            {
                float d = (i * 14f + animOffset) % dist;
                Vector2 pos = Vector2.Lerp(from, to, d / dist);
                GUI.color = new Color(pathColor.r, pathColor.g, pathColor.b, 0.85f);
                GUI.DrawTexture(new Rect(pos.x - 2.5f, pos.y - 2.5f, 5, 5), Texture2D.whiteTexture);
            }
        }

        void Draw3DWaypointMarker(float width,float height) => DrawPinWorldGuidance(width,height);
        void DrawMapLine(Vector2 from,Vector2 to,float thickness)
        {
            Vector2 delta=to-from; float length=delta.magnitude; if(length<1) return;
            // Avoid rotating the GUI matrix inside a clipping group on Android.
            int steps=Mathf.Min(2048,Mathf.CeilToInt(length));
            for(int i=0;i<=steps;i++)
            {
                Vector2 p=Vector2.Lerp(from,to,(float)i/steps);
                GUI.DrawTexture(new Rect(p.x-thickness/2,p.y-thickness/2,thickness,thickness),Texture2D.whiteTexture);
            }
        }
        void OnDestroy()
        {
            AdManager.Instance?.HideNativeScreenAd();
            WorldRainSystem.SetRain(false);
            foreach(var material in ownedMaterials) if(material!=null) Destroy(material);
            ownedMaterials.Clear();
            if(difficulty!=null) Destroy(difficulty);
            if(baselineDifficulty!=null) Destroy(baselineDifficulty);
            if(weapon!=null && weapon.Config!=null) Destroy(weapon.Config);
            if(ballistics!=null) {if(ballistics.config!=null) Destroy(ballistics.config); Destroy(ballistics);}
            if(sway!=null) Destroy(sway);
            if(ballistics!=null) ballistics.ShotResolved-=HandleShotResolved;
            DamageSystem.OnDamageDealt -= HandleDamageDealt;
            DamageSystem.OnEnemyKilled -= HandleEnemyKilled;
            DamageSystem.OnCivilianKilled -= HandleCivilianKilled;
            ExplosiveProp.OnAccidentKillNotice -= HandleAccidentKill;
            if(contractExtractionMaterial!=null) Destroy(contractExtractionMaterial);
            if(gameplayOverview!=null) Destroy(gameplayOverview);
            if(streetMinimap!=null) Destroy(streetMinimap);
            if(waypointIcon!=null) Destroy(waypointIcon);
            if(duelLaserLine!=null) Destroy(duelLaserLine.gameObject);
            CloseMap();
            if(cameraView!=null) cameraView.transform.SetParent(null,true);
            if(player!=null) Destroy(player.gameObject);
            foreach(var enemy in enemies) if(enemy!=null) Destroy(enemy.gameObject);
            foreach(var civ in civilians) if(civ!=null) Destroy(civ.gameObject);
            if(extractionMarker!=null) Destroy(extractionMarker);
            if(contractVIP!=null) Destroy(contractVIP.gameObject);
            if(contractCounterSniper!=null) Destroy(contractCounterSniper.gameObject);
            var vehicles = FindObjectsOfType<TrafficVehicle>();
            foreach (var v in vehicles) if (v != null) Destroy(v.gameObject);
            if(weapon!=null) Destroy(weapon);
            if(missionDynamicBillboard!=null) Destroy(missionDynamicBillboard);
            AdManager.Instance?.HideNativeBillboardAd();
            if(audioSource!=null) Destroy(audioSource);
            ambientMusic=null;
            foreach(var material in civilianMaterialCache.Values) if(material!=null) Destroy(material);
            foreach(var material in new[]{enemyJacket,enemySkin,enemyArmor,enemyHighlight,enemyDark,enemyWeapon,enemyWeaponWood,civilianShirt,civilianPants}) if(material!=null) Destroy(material);
        }

        public void ShowNotification(string message, float duration = 2.0f)
        {
            shotNotice = message;
            shotNoticeTime = duration;
        }
    }

}
