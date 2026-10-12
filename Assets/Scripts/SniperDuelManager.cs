using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeoSniper.Duel
{
    [DefaultExecutionOrder(50)]
    public sealed class SniperDuelManager : MonoBehaviour
    {
        private static SniperDuelManager instance;
        public static SniperDuelManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<SniperDuelManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("SniperDuelManager");
                        instance = go.AddComponent<SniperDuelManager>();
                    }
                }
                return instance;
            }
        }

        public static DuelOpponentAvatar ActiveOpponent => instance != null ? instance.Opponent : null;

        public DuelMatchState MatchState { get; private set; } = DuelMatchState.Idle;
        public int LocalScore { get; private set; } = 0;
        public int RivalScore { get; private set; } = 0;
        public int TargetScore { get; private set; } = 3;
        public string MatchNotice { get; private set; } = "";
        public float NoticeTimer { get; private set; } = 0;

        public DuelOpponentAvatar Opponent { get; private set; }
        public Camera GameplayCamera => playerCamera;
        public bool Reconnecting => network != null && network.IsReconnecting;
        public bool ConnectionPaused => Reconnecting || awaitingResumeClock;
        public bool CanPlay => MatchState == DuelMatchState.InRound && !Reconnecting && !awaitingResumeClock;
        public bool CanRematch => HasActiveDuel && MatchState == DuelMatchState.MatchOver && matchCompletionNotified
            && network != null && network.IsConnected && !network.IsReconnecting;
        public bool RematchIncomingPending => CanRematch && incomingRematchId > 0 && !incomingRematchAccepted && rematchOfferId == 0;
        public bool RematchOutgoingPending => CanRematch && (outgoingRematchId > 0 || incomingRematchAccepted || rematchOfferId > 0);
        public bool CanCancelRematch => CanRematch && HasRematchNegotiation && !rematchReady;
        public float RematchSecondsRemaining => HasRematchNegotiation ? Mathf.Max(0, rematchDeadline - Time.unscaledTime) : 0;
        public string RematchStatus { get; private set; } = "";
        public bool UsesDuelLoadout => mission != null && mission.MultiplayerDuel && MatchState != DuelMatchState.Idle;
        public bool HasActiveDuel => mission != null && mission.isActiveAndEnabled && mission.MultiplayerDuel && MatchState != DuelMatchState.Idle;
        public bool LocalIsScoped => localPlayer != null && localPlayer.Scoped;
        public const float RoundDuration = 180f;
        public int RoundNumber => roundId;
        public float RoundSecondsRemaining { get; private set; }
        public Vector3 ArenaCenter => (LocalSpawnPoint + RivalSpawnPoint) * .5f;
        public float ArenaRadius => Mathf.Clamp(Vector2.Distance(new Vector2(LocalSpawnPoint.x, LocalSpawnPoint.z), new Vector2(RivalSpawnPoint.x, RivalSpawnPoint.z)) * .5f + 35f, 75f, 175f);
        public float LocalBoundarySecondsRemaining { get; private set; } = -1;
        public bool CompetitiveSpotting => network != null && network.CompetitiveSpotting;
        public bool RivalTrackingVisible => !CompetitiveSpotting || Time.unscaledTime < rivalRevealUntil;
        private float rivalRevealUntil, clockSendTimer, remoteOutsideTimer, shotClock, remoteShotClockOffset;
        private bool awaitingResumeClock;
        private GameObject arenaCoverRoot;
        private Material arenaCoverMaterial;
        private int localStateSequence, lastRemoteStateSequence = -1, localShotSequence, lastRemoteShotSequence = -1;
        private readonly DuelPoseHistory localHistory = new DuelPoseHistory(), rivalHistory = new DuelPoseHistory();
        private readonly DuelShotValidator localShots = new DuelShotValidator(), rivalShots = new DuelShotValidator();
        private int lastHostResult, lastGuestResult;
        private Vector3 remotePosition;
        private float remoteYaw, remotePitch;
        private bool remoteCrouched;

        public bool CanShowRivalLocator => CanPlay && localPlayer != null && !localPlayer.InputBlocked && localPlayer.Health != null && !localPlayer.Health.IsDead;
        public Vector3 LocalSpawnPoint { get; private set; }
        public Vector3 RivalSpawnPoint { get; private set; }

        private SniperDuelNetwork network;
        private UrbanPlayer localPlayer;
        private UrbanCombatMission mission;
        private Camera playerCamera;

        private float syncTimer;
        private const float SyncRate = 0.05f; // 20Hz state sync
        private float roundTransitionTimer;
        private bool isHost;
        private int roundId;
        private Coroutine arenaRoutine;
        private bool matchCompletionNotified, leavingDuel, duelExitFinished;
        private const float RematchTimeout = 20f;
        private int completedMatchScope, nextRematchRequestId, lastRemoteRematchId;
        private int outgoingRematchId, incomingRematchId, rematchOfferId;
        private bool incomingRematchAccepted, rematchOfferByHost, rematchReady;
        private float rematchDeadline;
        private int rematchArenaSequence, pendingRematchArenaRound;
        private int committedRematchId;
        private bool committedRematchByHost;
        private bool rematchStartInFlight, rematchPreviousNotified;
        private int rematchPreviousLocalScore, rematchPreviousRivalScore;
        private bool HasRematchNegotiation => outgoingRematchId > 0 || incomingRematchId > 0 || rematchOfferId > 0;

        public event Action<string, bool, float> OnKillfeedEntry; // killer, isHeadshot, distance

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            network = SniperDuelNetwork.Instance;
            if (network == null)
            {
                var netObj = new GameObject("SniperDuelNetwork");
                network = netObj.AddComponent<SniperDuelNetwork>();
            }

            network.OnConnected -= HandleConnected;
            network.OnConnected += HandleConnected;
            network.OnDisconnected -= HandleDisconnected;
            network.OnDisconnected += HandleDisconnected;
            network.OnReconnectStateChanged -= HandleReconnectState;
            network.OnPacketReceived -= HandlePacketReceived;
            network.OnReconnectStateChanged += HandleReconnectState;
            network.OnPacketReceived += HandlePacketReceived;

            // PvP damage is resolved from host-validated fire requests.
        }

        private void OnDestroy()
        {
            DuelKillReplay.Clear();
            ClearArenaCover();
            if (network != null)
            {
                network.OnConnected -= HandleConnected;
                network.OnDisconnected -= HandleDisconnected;
                network.OnReconnectStateChanged -= HandleReconnectState;
                network.OnPacketReceived -= HandlePacketReceived;
            }

        }

        private void EnsureNetwork()
        {
            network = SniperDuelNetwork.Instance;
            network.OnConnected -= HandleConnected;
            network.OnDisconnected -= HandleDisconnected;
            network.OnReconnectStateChanged -= HandleReconnectState;
            network.OnPacketReceived -= HandlePacketReceived;
            network.OnConnected += HandleConnected;
            network.OnDisconnected += HandleDisconnected;
            network.OnReconnectStateChanged += HandleReconnectState;
            network.OnPacketReceived += HandlePacketReceived;
        }

        public void StartDuelMatch(UrbanCombatMission activeMission, bool asHost, string roomCodeOrIp = "")
        {
            ClearRematchNegotiation("");
            completedMatchScope = nextRematchRequestId = lastRemoteRematchId = rematchArenaSequence = pendingRematchArenaRound = 0;
            rematchStartInFlight = false;
            matchCompletionNotified = false;
            leavingDuel = duelExitFinished = false;
            EnsureNetwork();
            mission = activeMission;
            isHost = asHost;
            localPlayer = UrbanPlayer.Instance;
            playerCamera = Camera.main;

            LocalScore = 0;
            RivalScore = 0;
            roundId = 0;

            if (asHost)
            {
                if (network != null && !network.IsHost) network.StartHost();
                MatchState = DuelMatchState.LobbyWait;
                string code = (network != null && !string.IsNullOrEmpty(network.RoomCode)) ? network.RoomCode : "INITIALIZING...";
                SetNotice($"ROOM HOSTED: {code}\nWAITING FOR CHALLENGER TO CONNECT...", 60f);
            }
            else
            {
                MatchState = DuelMatchState.LobbyWait;
                SetNotice($"CONNECTING TO HOST: {roomCodeOrIp}...", 20f);
                if (network != null && !network.IsConnected && !network.IsConnecting) network.StartClient(roomCodeOrIp);
            }
            if (network.IsConnected) HandleConnected(network.RemoteCallsign);
        }

        private void HandleConnected(string rivalCallsign)
        {
            if (mission == null || MatchState != DuelMatchState.LobbyWait) return;
            SetNotice("CHALLENGER CONNECTED: " + rivalCallsign + "!\nPREPARING ROOFTOP STANDOFF...", 3f);
            BeginArena();
        }

        private void HandleReconnectState(bool reconnecting)
        {
            DuelKillReplay.Clear();
            if (reconnecting) CancelRematchForConnection("REMATCH CANCELLED // CONNECTION INTERRUPTED");
            if (localPlayer != null) localPlayer.InputBlocked = reconnecting || awaitingResumeClock || MatchState != DuelMatchState.InRound;
            if (MatchState == DuelMatchState.MatchOver)
            {
                SetNotice(reconnecting ? "MATCH COMPLETE // RECONNECTING..." : "MATCH COMPLETE // CONNECTION RESTORED", 10f);
                return;
            }
            if (reconnecting)
            {
                awaitingResumeClock = !isHost && MatchState == DuelMatchState.InRound;
                localHistory.Clear(); rivalHistory.Clear();
                SetNotice("CONNECTION INTERRUPTED // RECONNECTING...\nMATCH PAUSED; YOUR ROUND AND SCORES ARE SAVED.", 35f);
            }
            else
            {
                localHistory.Clear(); rivalHistory.Clear(); RecordPoses();
                rivalHistory.Record(Time.unscaledTime, remotePosition, remoteYaw, remotePitch, remoteCrouched, Opponent != null && !Opponent.IsDead);
                DuelKillReplay.EnsureForCamera(playerCamera);
                SetNotice("CONNECTION RESTORED // RESUMING DUEL", 2f);
                SendLocalStateSync();
                if (isHost)
                {
                    rivalShots.ShiftClock(-remoteShotClockOffset); remoteShotClockOffset = 0;
                    rivalShots.GetReloadState(shotClock, out int reloadWeapon, out float reloadRemaining);
                    network.SendPacket(DuelPacketCodec.SerializeResumeClock(roundId, RoundSecondsRemaining, shotClock,
                        rivalShots.GetRemainingAmmo(0, shotClock), rivalShots.GetRemainingAmmo(1, shotClock), rivalShots.GetRemainingAmmo(2, shotClock),
                        reloadWeapon, reloadRemaining, rivalShots.GetShotCooldownRemaining(shotClock),
                        new[]{rivalShots.GetRemainingAmmo(3,shotClock),rivalShots.GetRemainingAmmo(4,shotClock),rivalShots.GetRemainingAmmo(5,shotClock),rivalShots.GetRemainingAmmo(6,shotClock),rivalShots.GetRemainingAmmo(7,shotClock)}));
                }
            }
        }

        private void HandleDisconnected()
        {
            DuelKillReplay.Clear();
            CancelRematchForConnection("REMATCH CANCELLED // RIVAL LEFT THE ROOM");
            if (arenaRoutine != null) StopCoroutine(arenaRoutine);
            arenaRoutine = null;
            ClearArenaCover();
            bool activeDuel = mission != null && mission.isActiveAndEnabled && mission.MultiplayerDuel;
            if (activeDuel && matchCompletionNotified && MatchState == DuelMatchState.MatchOver)
            {
                SetNotice("MATCH COMPLETE // RIVAL LEFT THE ROOM", 3600f);
                DestroyOpponent();
                return;
            }
            MatchState = activeDuel ? DuelMatchState.LobbyWait : DuelMatchState.Idle;
            if (!activeDuel)
            {
                MatchNotice = ""; NoticeTimer = 0;
                DestroyOpponent();
                return;
            }
            string reason = network != null ? network.LastDisconnectReason : "Arena initialization failed.";
            SetNotice("DUEL STOPPED: " + reason + "\nEXIT DUEL AND REJOIN THE HOST.", 3600f);
            DestroyOpponent();
        }

        private void BeginArena()
        {
            DuelKillReplay.Clear();
            awaitingResumeClock = false;
            if (arenaRoutine != null) StopCoroutine(arenaRoutine);
            MatchState = DuelMatchState.Warmup;
            arenaRoutine = StartCoroutine(InitializeDuelArenaRoutine());
        }

        private IEnumerator InitializeDuelArenaRoutine()
        {
            localPlayer = mission != null ? mission.Player : UrbanPlayer.Instance;
            playerCamera = Camera.main;
            float timeout = 30f;
            while ((localPlayer == null || mission == null || playerCamera == null) && timeout > 0)
            {
                localPlayer = UrbanPlayer.Instance;
                mission = FindAnyObjectByType<UrbanCombatMission>();
                playerCamera = Camera.main;
                if (!Reconnecting) timeout -= Time.deltaTime;
                yield return null;
            }

            if (localPlayer == null || mission == null || playerCamera == null || !network.IsConnected)
            {
                network.Disconnect("Local player or camera was not ready for arena initialization.");
                yield break;
            }
            roundId = pendingRematchArenaRound > roundId ? pendingRematchArenaRound : roundId + 1;
            pendingRematchArenaRound = 0;
            rematchArenaSequence = Mathf.Max(rematchArenaSequence, roundId);
            ResetShotAuthority();
            DuelKillReplay.EnsureForCamera(playerCamera);
            yield return WaitDuelSeconds(.5f);

            // Locate two elevated rooftop perches with clear line-of-sight
            if (isHost)
            {
                if (!DetermineRooftopPerches())
                {
                    network.Disconnect("No supported positions with a clear shot were found in this arena.");
                    yield break;
                }
                network.SendPacket(DuelPacketCodec.SerializeRoomRules(network.CompetitiveSpotting));
                network.SendPacket(DuelPacketCodec.SerializeArena(LocalSpawnPoint, RivalSpawnPoint, roundId));
            }
            else
            {
                float arenaTimeout = 45f;
                while (network.IsConnected && network.ArenaRound < roundId && arenaTimeout > 0)
                {
                    if (!Reconnecting) arenaTimeout -= Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!network.IsConnected || network.ArenaRound != roundId)
                {
                    if (network.IsConnected) network.Disconnect("Host did not send arena positions within 45 seconds.");
                    yield break;
                }
                LocalSpawnPoint = network.ArenaHostSpawn;
                RivalSpawnPoint = network.ArenaGuestSpawn;
            }

            // Disable single-player AI rival bot now that real opponent is active
            if (mission != null)
            {
                mission.DisableAIRivalForMultiplayer();
                mission.ResetMultiplayerDuelRound();
            }

            // Spawn or reposition local player
            if (localPlayer != null)
            {
                Vector3 mySpawn = isHost ? LocalSpawnPoint : RivalSpawnPoint;
                Vector3 theirSpawn = isHost ? RivalSpawnPoint : LocalSpawnPoint;

                localPlayer.Place(mySpawn);
                localPlayer.Face(theirSpawn);
                if (localPlayer.Health != null)
                {
                    localPlayer.Health.Initialize(100);
                    localPlayer.Health.canRegenerate = false;
                }
            }

            // Spawn Opponent Avatar
            Vector3 oppSpawn = isHost ? RivalSpawnPoint : LocalSpawnPoint;
            Vector3 oppFacing = isHost ? LocalSpawnPoint : RivalSpawnPoint;
            Quaternion oppRot = Quaternion.LookRotation((oppFacing - oppSpawn).normalized);

            if (Opponent == null)
            {
                var oppGo = new GameObject("DuelOpponentAvatar");
                Opponent = oppGo.AddComponent<DuelOpponentAvatar>();
                Opponent.Initialize(network.RemoteCallsign, oppSpawn, oppRot);
            }
            else
            {
                Opponent.Respawn(oppSpawn, oppRot);
            }

            remotePosition = oppSpawn; remoteYaw = oppRot.eulerAngles.y; remotePitch = 0; remoteCrouched = false;
            RecordPoses();
            rivalHistory.Record(Time.unscaledTime, remotePosition, remoteYaw, remotePitch, remoteCrouched, true);
            EnsureArenaRoofAccess();
            BuildArenaCover();

            // Warmup countdown
            MatchState = DuelMatchState.Warmup;
            for (int i = 3; i >= 1; i--)
            {
                SetNotice("DUEL STARTS IN " + i + "...", 1.0f);
                yield return WaitDuelSeconds(1f);
            }

            if (!isHost) network.SendPacket(DuelPacketCodec.SerializeRoundSignal(DuelPacketType.Ready, roundId));
            SetNotice("WAITING FOR RIVAL TO READY...", 15f);
            float readyTimeout = 180f;
            while (network.IsConnected && (isHost ? network.ReadyRound : network.EngageRound) < roundId && readyTimeout > 0)
            {
                if (!Reconnecting) readyTimeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            if (!network.IsConnected || readyTimeout <= 0)
            {
                if (network.IsConnected) network.Disconnect("Rival did not finish loading the shared arena within 180 seconds.");
                yield break;
            }
            while (network.IsConnected && (Reconnecting || awaitingResumeClock)) yield return null;
            if (!network.IsConnected) yield break;
            if (isHost) network.SendPacket(DuelPacketCodec.SerializeRoundSignal(DuelPacketType.Engage, roundId));
            ActivateRound();
        }

        private void ActivateRound()
        {
            if (MatchState != DuelMatchState.Warmup) return;
            rematchStartInFlight = false;
            committedRematchId = 0;
            completedMatchScope = 0;
            SetNotice("ENGAGE! ELIMINATE THE RIVAL MARKSMAN!", 2.5f);
            RoundSecondsRemaining = RoundDuration; shotClock = remoteShotClockOffset = 0;
            clockSendTimer = 0;
            LocalBoundarySecondsRemaining = -1; remoteOutsideTimer = 0;
            localStateSequence = localShotSequence = 0;
            lastRemoteStateSequence = lastRemoteShotSequence = -1;
            rivalRevealUntil = Time.unscaledTime + 3f;
            rivalHistory.Record(Time.unscaledTime, remotePosition, remoteYaw, remotePitch, remoteCrouched, true);
            MatchState = DuelMatchState.InRound;
        }

        private IEnumerator WaitDuelSeconds(float seconds)
        {
            while (seconds > 0)
            {
                if (!Reconnecting) seconds -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private bool DetermineRooftopPerches()
        {
            if (roundId > 1) return true;
            bool found = false;

            if (mission != null && mission.RooftopSpawns != null && mission.RooftopSpawns.Count >= 2)
            {
                var perches = new List<Vector3>();
                foreach (var r in mission.RooftopSpawns)
                {
                    if (r.y >= 6.0f && UrbanCombatMission.IsSupportedDuelPoint(r)) perches.Add(r);
                }

                float bestCost = float.PositiveInfinity;
                for (int i = 0; i < perches.Count; i++)
                {
                    for (int j = i + 1; j < perches.Count; j++)
                    {
                        float d = Vector3.Distance(perches[i], perches[j]);
                        if (d >= 80f && d <= 260f)
                        {
                            if (ClearDuelSightline(perches[i], perches[j]))
                            {
                                // Favor readable duel distances; retain distant roofs only when the map requires them.
                                float cost = Mathf.Abs(d - 120f);
                                if (cost < bestCost)
                                {
                                    found = true;
                                    bestCost = cost;
                                    LocalSpawnPoint = perches[i];
                                    RivalSpawnPoint = perches[j];
                                }
                            }
                        }
                    }
                }
            }
            if (!found && mission != null)
            {
                var ground = mission.MultiplayerGroundSpawns;
                for (int i = 0; i < ground.Count && !found; i++)
                {
                    if (!UrbanCombatMission.IsSupportedDuelPoint(ground[i])) continue;
                    for (int j = i + 1; j < ground.Count; j++)
                    {
                        Vector3 separation = ground[i] - ground[j]; separation.y = 0;
                        if (separation.magnitude < 50f || separation.magnitude > 135f) continue;
                        if (!UrbanCombatMission.IsSupportedDuelPoint(ground[j]) || !ClearDuelSightline(ground[i], ground[j])) continue;
                        LocalSpawnPoint = ground[i]; RivalSpawnPoint = ground[j]; found = true; break;
                    }
                }
            }
            return found;
        }

        private static bool ClearDuelSightline(Vector3 a, Vector3 b)
        {
            Vector3 start = a + Vector3.up * 1.6f;
            Vector3 delta = b + Vector3.up * 1.6f - start;
            foreach (var hit in Physics.RaycastAll(start, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                // Spawned characters must not make a valid arena pair fail selection.
                if (hit.collider.GetComponentInParent<CombatActor>() != null) continue;
                return false;
            }
            return true;
        }

        private void Update()
        {
            if (NoticeTimer > 0) NoticeTimer -= Time.deltaTime;
            TickRematch(Time.unscaledTime);
            if (Reconnecting || awaitingResumeClock) return;
            RecordPoses();

            if (MatchState == DuelMatchState.InRound)
            {
                shotClock += Time.unscaledDeltaTime;
                RoundSecondsRemaining = Mathf.Max(0, RoundSecondsRemaining - Time.unscaledDeltaTime);
                UpdateArenaBoundary();
                if (isHost)
                {
                    clockSendTimer -= Time.unscaledDeltaTime;
                    if (clockSendTimer <= 0)
                    {
                        clockSendTimer = 1f;
                        network.SendPacket(DuelPacketCodec.SerializeRoundClock(roundId, RoundSecondsRemaining));
                    }
                    if (RoundSecondsRemaining <= 0) { ResolveTimedRound(); return; }
                }
                syncTimer += Time.unscaledDeltaTime;
                if (syncTimer >= SyncRate)
                {
                    syncTimer = 0;
                    SendLocalStateSync();
                }
            }
            else if (MatchState == DuelMatchState.RoundOver)
            {
                roundTransitionTimer -= Time.unscaledDeltaTime;
                if (roundTransitionTimer <= 0)
                {
                    StartNextRound();
                }
            }
        }

        public static SniperDuelManager ActiveInstance => instance;

        public void NotifySurfaceImpact(Vector3 point, byte kind)
        {
            if (MatchState != DuelMatchState.InRound || !network.IsConnected) return;
            var packet = DuelPacketCodec.SerializeHit(kind, 0, point, roundId);
            packet[1] = (byte)DuelPacketType.Impact;
            network.SendPacket(packet);
        }

        private void SendLocalStateSync()
        {
            if (localPlayer == null || playerCamera == null || !network.IsConnected) return;

            Vector3 pos = localPlayer.transform.position;
            float yaw = playerCamera.transform.eulerAngles.y;
            float pitch = playerCamera.transform.eulerAngles.x;
            bool isAiming = localPlayer.Scoped;
            bool isCrouching = localPlayer.IsCrouching;
            float hp = localPlayer.Health != null ? localPlayer.Health.Health : 100f;

            byte[] sync = DuelPacketCodec.SerializeStateSync(pos, yaw, pitch, isAiming, isCrouching, hp, roundId, ++localStateSequence, mission!=null?mission.CurrentWeaponId:1);
            network.SendPacket(sync);
        }

        private void ResetShotAuthority()
        {
            shotClock = remoteShotClockOffset = 0;
            RoundSecondsRemaining = RoundDuration;
            localShots.ResetRound(roundId); rivalShots.ResetRound(roundId);
            localHistory.Clear(); rivalHistory.Clear();
            lastHostResult = lastGuestResult = 0;
        }

        private void RecordPoses()
        {
            if (localPlayer == null || Opponent == null) return;
            localHistory.Record(Time.unscaledTime, localPlayer.transform.position,
                playerCamera != null ? playerCamera.transform.eulerAngles.y : 0,
                playerCamera != null ? playerCamera.transform.eulerAngles.x : 0,
                localPlayer.IsCrouching, !localPlayer.Health.IsDead);
        }

        public void NotifyLocalGunshot(Vector3 origin, Vector3 dir, int weaponIndex)
        {
            if (!CanPlay || network == null || !network.IsConnected) return;
            int shot = ++localShotSequence;
            float elapsed = shotClock;
            // State precedes the reliable fire request, reducing eye-origin errors while moving.
            SendLocalStateSync();
            network.SendPacket(DuelPacketCodec.SerializeFire(origin, dir, weaponIndex, roundId, shot, UrbanCombatMission.IsSoundMasked, elapsed));
            if (isHost) ResolveHostShot(true, origin, dir, weaponIndex, shot, elapsed);
        }

        public void NotifyLocalReload(int weaponIndex)
        {
            if (!CanPlay || network == null || !network.IsConnected) return;
            float elapsed = shotClock;
            if (isHost) localShots.TryBeginReload(roundId, weaponIndex, elapsed);
            network.SendPacket(DuelPacketCodec.SerializeReload(roundId, weaponIndex, elapsed));
        }

        private void ResolveHostShot(bool hostShooter, Vector3 origin, Vector3 direction, int weapon, int shotId, float elapsed)
        {
            if (!isHost || !CanPlay || localPlayer == null || Opponent == null) return;
            RecordPoses();
            var shooterHistory = hostShooter ? localHistory : rivalHistory;
            var targetHistory = hostShooter ? rivalHistory : localHistory;
            // Guest saw an already-delayed host pose; bound the total rewind to 250ms.
            float rewind = hostShooter ? 0 : Mathf.Clamp(network.PingMs * .001f, 0, .25f);
            if (!shooterHistory.TrySample(Time.unscaledTime, 0, out var shooter)) return;
            var validator = hostShooter ? localShots : rivalShots;
            if (!validator.TryAccept(roundId, shotId, weapon, origin, direction, shooter, elapsed, out var shot, out _)) return;
            if (!hostShooter) remoteShotClockOffset = elapsed - shotClock;
            if (!ClearShotOrigin(shooter.EyePosition, origin)) return;
            if (!targetHistory.TrySample(Time.unscaledTime, rewind, out var target) || !DuelShotValidator.TryIntersect(shot, target, out var hit)) return;
            float scale = ShotObstructionScale(shot.Origin, shot.Direction, hit.Distance);
            if (scale <= 0) return;
            float damage = DuelShotValidator.DamageFor(hit.Zone, weapon, scale);
            float before = hostShooter ? Opponent.CurrentHealth : localPlayer.Health.Health;
            if (before <= 0) return;
            if (hostShooter)
            {
                Opponent.TakeLocalDamage(hit.Zone, damage, hit.Point);
                DamageSystem.ConfirmDuelHit(hit.Zone, damage, hit.Point);
            }
            else ApplyConfirmedLocalHit(hit.Zone, damage, hit.Point, weapon, hit.Distance);
            float health = hostShooter ? Opponent.CurrentHealth : localPlayer.Health.Health;
            network.SendPacket(DuelPacketCodec.SerializeShotResult(roundId, shotId, hostShooter, weapon, hit.Zone, damage, health, hit.Point, hit.Distance));
            if (health <= 0)
            {
                if (hostShooter) HandleOpponentEliminated(hit.Zone == 2);
                else
                {
                    RivalScore++;
                    OnKillfeedEntry?.Invoke(network.RemoteCallsign, hit.Zone == 2, hit.Distance);
                    CheckMatchCompletion(); SendRoundResult();
                }
            }
        }

        private static bool ClearShotOrigin(Vector3 eye, Vector3 origin)
        {
            Vector3 delta = origin - eye;
            if (delta.sqrMagnitude < .0001f) return true;
            foreach (var hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<CombatActor>() == null) return false;
            return true;
        }

        private static float ShotObstructionScale(Vector3 origin, Vector3 direction, float distance)
        {
            var hits = Physics.RaycastAll(origin, direction, Mathf.Max(0, distance - .005f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            float scale = 1; int layers = 0;
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<CombatActor>() != null) continue;
                var surface = new SniperHitQuery.ShotHit {Collider=hit.collider, Point=hit.point, Distance=hit.distance};
                if (++layers > 4 || !SniperHitQuery.TryPenetrate(surface, direction, out _)) return 0;
                scale *= .65f;
            }
            return scale;
        }

        private void HandleOpponentEliminated(bool isHeadshot)
        {
            LocalScore++;
            float dist = Vector3.Distance(localPlayer.transform.position, Opponent.transform.position);

            SetNotice(isHeadshot ? $"HEADSHOT ELIMINATION!\nRANGE: {dist:F0}m" : $"RIVAL ELIMINATED!\nRANGE: {dist:F0}m", 3.0f);
            OnKillfeedEntry?.Invoke(network.LocalCallsign, isHeadshot, dist);


            CheckMatchCompletion();
            SendRoundResult();
        }

        private void SendRoundResult(string reason = "")
        {
            network.SendPacket(DuelPacketCodec.SerializeRoundState(MatchState, LocalScore, RivalScore, reason, roundId));
        }

        private void HandlePacketReceived(DuelPacketType type, BinaryReader br)
        {
            // Engage and the first hit can arrive in the same network update.
            if (!isHost && MatchState == DuelMatchState.Warmup && Opponent != null && network.EngageRound == roundId)
                ActivateRound();
            switch (type)
            {
                case DuelPacketType.StateSync:
                    DuelPacketCodec.DeserializeStateSync(br, out Vector3 pos, out float yaw, out float pitch, out bool isAiming, out bool isCrouch, out float hp, out int syncRound, out int syncSequence);
                    int syncedWeapon=br.ReadByte();
                    if (WeaponCatalog.Valid(syncedWeapon) && syncRound == roundId && syncSequence > lastRemoteStateSequence && MatchState == DuelMatchState.InRound && Opponent != null && Finite(pos) && float.IsFinite(yaw) && float.IsFinite(pitch))
                    {
                        Opponent.EquipWeapon(syncedWeapon);
                        lastRemoteStateSequence = syncSequence;
                        remotePosition = pos; remoteYaw = yaw; remotePitch = pitch; remoteCrouched = isCrouch;
                        // Guest-reported health is not authoritative.
                        Opponent.ApplyNetworkState(pos, yaw, pitch, isAiming, isCrouch, isHost ? Opponent.CurrentHealth : hp);
                        rivalHistory.Record(Time.unscaledTime, pos, yaw, pitch, isCrouch, !Opponent.IsDead);
                    }
                    break;

                case DuelPacketType.Fire:
                    DuelPacketCodec.DeserializeFire(br, out Vector3 muzzle, out Vector3 dir, out int wpnIdx, out int fireRound, out int fireSequence, out bool soundMasked, out float firedTime);
                    if (fireRound == roundId && fireSequence > lastRemoteShotSequence && CanPlay && Opponent != null && !Opponent.IsDead && Finite(muzzle) && Finite(dir) && dir.sqrMagnitude >= .98f && dir.sqrMagnitude <= 1.02f && WeaponCatalog.Valid(wpnIdx))
                    {
                        lastRemoteShotSequence = fireSequence;
                        if (!soundMasked && wpnIdx != 2) rivalRevealUntil = Time.unscaledTime + 3f;
                        Opponent.PlayRemoteFire(muzzle, dir.normalized, wpnIdx, soundMasked);
                        float hostElapsed = shotClock;
                        if (isHost && float.IsFinite(firedTime) && firedTime >= Mathf.Max(0, hostElapsed - 1f) && firedTime <= hostElapsed + .5f)
                            ResolveHostShot(false, muzzle, dir.normalized, wpnIdx, fireSequence, firedTime);
                    }
                    break;

                case DuelPacketType.Impact:
                    DuelPacketCodec.DeserializeHit(br, out byte materialKind, out _, out Vector3 impactPoint, out int impactRound);
                    if (impactRound == roundId && materialKind <= 2 && float.IsFinite(impactPoint.x) && float.IsFinite(impactPoint.y) && float.IsFinite(impactPoint.z))
                        SniperPresentation.PlayRemoteSurfaceImpact(impactPoint, materialKind);
                    break;

                case DuelPacketType.RoundClock:
                    int clockRound = br.ReadInt32(); float seconds = br.ReadSingle();
                    if (!isHost && clockRound == roundId && float.IsFinite(seconds) && seconds >= 0 && seconds <= RoundDuration)
                        RoundSecondsRemaining = seconds;
                    break;

                case DuelPacketType.ResumeClock:
                    int resumeRound = br.ReadInt32(); float resumedSeconds = br.ReadSingle(), resumedShotClock = br.ReadSingle();
                    int ammo0=br.ReadByte(), ammo1=br.ReadByte(), ammo2=br.ReadByte(), pendingReload=br.ReadByte();
                    float reloadSeconds=br.ReadSingle(), shotCooldown=br.ReadSingle();
                    int[] expandedAmmo={ammo0,ammo1,ammo2,br.ReadByte(),br.ReadByte(),br.ReadByte(),br.ReadByte(),br.ReadByte()};
                    bool ammoValid=true;for(int i=0;i<expandedAmmo.Length;i++)if(expandedAmmo[i]>WeaponCatalog.Get(i).Magazine)ammoValid=false;
                    if (!isHost && resumeRound == roundId && float.IsFinite(resumedSeconds)
                        && resumedSeconds >= 0 && resumedSeconds <= RoundDuration && float.IsFinite(resumedShotClock)
                        && resumedShotClock >= 0 && resumedShotClock <= RoundDuration + 1
                        && ammoValid && (pendingReload<WeaponCatalog.Count || pendingReload==255)
                        && float.IsFinite(reloadSeconds) && reloadSeconds>=0 && reloadSeconds<=2.2f
                        && float.IsFinite(shotCooldown) && shotCooldown>=0 && shotCooldown<=1.05f)
                    {
                        RoundSecondsRemaining = resumedSeconds; shotClock = resumedShotClock; awaitingResumeClock = false;
                        mission?.RestoreMultiplayerDuelAmmo(expandedAmmo, pendingReload==255 ? -1 : pendingReload, reloadSeconds, shotCooldown);
                        if (localPlayer != null) localPlayer.InputBlocked = MatchState != DuelMatchState.InRound;
                    }
                    break;

                case DuelPacketType.ReturnToArena:
                    if (!isHost && br.ReadInt32() == roundId && MatchState == DuelMatchState.InRound) ReturnLocalPlayerToArena();
                    break;

                case DuelPacketType.Hit:
                    // Protocol6 ignores peer-reported hit and damage claims.
                    break;

                case DuelPacketType.Reload:
                    int reloadRound = br.ReadInt32(), reloadWeapon = br.ReadByte(); float reloadTime = br.ReadSingle();
                    float currentElapsed = shotClock;
                    if (isHost && CanPlay && reloadRound == roundId && float.IsFinite(reloadTime)
                        && reloadTime >= Mathf.Max(0, currentElapsed - 1f) && reloadTime <= currentElapsed + .5f)
                        if (rivalShots.TryBeginReload(roundId, reloadWeapon, reloadTime)) remoteShotClockOffset = reloadTime - shotClock;
                    break;

                case DuelPacketType.ShotResult:
                    int shotRound = br.ReadInt32(), resultShot = br.ReadInt32(); bool hostFired = br.ReadBoolean();
                    int resultWeapon = br.ReadByte(); byte zone = br.ReadByte(); float dealt = br.ReadSingle(), targetHp = br.ReadSingle();
                    Vector3 point = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()); float range = br.ReadSingle();
                    if (isHost || Reconnecting || localPlayer == null || localPlayer.Health == null || MatchState != DuelMatchState.InRound || shotRound != roundId || resultShot <= (hostFired ? lastHostResult : lastGuestResult)
                        || resultWeapon >= WeaponCatalog.Count || zone > 2 || !float.IsFinite(dealt) || dealt <= 0 || dealt > 120
                        || !float.IsFinite(targetHp) || targetHp < 0 || targetHp > 100 || !Finite(point) || !float.IsFinite(range) || range < 0 || range > 1200) break;
                    if (hostFired)
                    {
                        lastHostResult = resultShot;
                        ApplyConfirmedLocalHit(zone, Mathf.Max(0, localPlayer.Health.Health - targetHp), point, resultWeapon, range);
                    }
                    else
                    {
                        lastGuestResult = resultShot;
                        if (Opponent != null && targetHp < Opponent.CurrentHealth)
                            Opponent.TakeLocalDamage(zone, Opponent.CurrentHealth - targetHp, point);
                        DamageSystem.ConfirmDuelHit(zone, dealt, point);
                    }
                    break;

                case DuelPacketType.RoundState:
                    DuelPacketCodec.DeserializeRoundState(br, out DuelMatchState state, out int hostScore, out int clientScore, out string winner, out int resultRound);
                    if (isHost || resultRound != roundId || MatchState != DuelMatchState.InRound) break;
                    if (hostScore < 0 || clientScore < 0 || hostScore > TargetScore || clientScore > TargetScore) break;
                    if (state != DuelMatchState.RoundOver && state != DuelMatchState.MatchOver) break;
                    if (winner != "TIMEOUT" && hostScore > RivalScore && localPlayer != null && !localPlayer.Health.IsDead)
                    {
                        localPlayer.Health.Damage(1000, Opponent != null ? Opponent.transform.position : Vector3.zero);
                        localPlayer.ShowDuelHitFeedback(100);
                    }
                    if (winner != "TIMEOUT" && clientScore > LocalScore && Opponent != null && !Opponent.IsDead)
                        Opponent.TakeLocalDamage(1, 1000, Opponent.transform.position);
                    LocalScore = clientScore;
                    RivalScore = hostScore;
                    CheckMatchCompletion();
                    if (winner == "TIMEOUT" && MatchState == DuelMatchState.RoundOver) SetNotice("TIME // HIGHER HEALTH WINS. NEXT ROUND...", 3f);
                    break;

                case DuelPacketType.Rematch:
                    DuelPacketCodec.DeserializeRematch(br, out var rematchAction, out int rematchScope,
                        out int rematchRequest, out bool rematchByHost, out int rematchRound);
                    HandleRematchSignal(rematchAction, rematchScope, rematchRequest, rematchByHost, rematchRound);
                    break;
            }
        }

        private void EnsureArenaRoofAccess()
        {
            // Run on both peers after receiving the host's assignments. The map's
            // mobile detail budget may omit ladders on the roofs chosen for this duel.
            foreach (var perch in new[] { LocalSpawnPoint, RivalSpawnPoint })
                foreach (var world in SectorWorld.LoadedWorlds)
                    if (world != null && world.EnsureDuelRooftopLadder(perch)) break;
        }

        private void BuildArenaCover()
        {
            if (arenaCoverRoot != null) return;
            arenaCoverRoot = new GameObject("Shared duel cover");
            arenaCoverRoot.transform.SetParent(transform, false);
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");
            if (shader == null) return;
            arenaCoverMaterial = new Material(shader);
            arenaCoverMaterial.color = new Color(.19f, .23f, .25f);
            for (int side = 0; side < 2; side++)
            {
                Vector3 spawn = side == 0 ? LocalSpawnPoint : RivalSpawnPoint;
                Vector3 forward = (side == 0 ? RivalSpawnPoint : LocalSpawnPoint) - spawn;
                forward.y = 0; forward.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                for (int wing = -1; wing <= 1; wing += 2)
                {
                    Vector3 foot = spawn + forward * 2.5f + right * (wing * 1.4f);
                    Quaternion rotation = Quaternion.LookRotation(forward);
                    bool supported = true;
                    for (int x = -1; x <= 1; x += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = foot + right * (x * .9f) + forward * (z * .3f);
                            if (SectorWorld.WaterAt(corner) || !Physics.Raycast(corner + Vector3.up * .2f, Vector3.down, out var floor, .7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || floor.normal.y < .8f)
                                supported = false;
                        }
                    if (!supported) continue;
                    var overlaps = Physics.OverlapBox(foot + Vector3.up * .48f, new Vector3(.9f, .42f, .3f), rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    bool blocked = false;
                    foreach (var overlap in overlaps)
                        if (overlap.GetComponentInParent<CombatActor>() == null) { blocked = true; break; }
                    if (blocked) continue;
                    var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cover.name = "Duel concrete cover";
                    cover.transform.SetParent(arenaCoverRoot.transform, false);
                    cover.transform.SetPositionAndRotation(foot + Vector3.up * .45f, rotation);
                    cover.transform.localScale = new Vector3(1.8f, .9f, .6f);
                    cover.GetComponent<Renderer>().sharedMaterial = arenaCoverMaterial;
                }
            }
        }

        private void ClearArenaCover()
        {
            if (arenaCoverRoot != null) { if (Application.isPlaying) Destroy(arenaCoverRoot); else DestroyImmediate(arenaCoverRoot); }
            if (arenaCoverMaterial != null) { if (Application.isPlaying) Destroy(arenaCoverMaterial); else DestroyImmediate(arenaCoverMaterial); }
            arenaCoverRoot = null; arenaCoverMaterial = null;
        }

        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private bool OutsideArena(Vector3 point)
        {
            Vector3 delta = point - ArenaCenter; delta.y = 0;
            return delta.sqrMagnitude > ArenaRadius * ArenaRadius;
        }

        private void UpdateArenaBoundary()
        {
            if (localPlayer != null && !localPlayer.Health.IsDead && OutsideArena(localPlayer.transform.position))
            {
                LocalBoundarySecondsRemaining = LocalBoundarySecondsRemaining < 0 ? 5f : LocalBoundarySecondsRemaining - Time.unscaledDeltaTime;
                if (LocalBoundarySecondsRemaining <= 0) ReturnLocalPlayerToArena();
            }
            else LocalBoundarySecondsRemaining = -1;
            if (isHost && Opponent != null && !Opponent.IsDead)
            {
                remoteOutsideTimer = OutsideArena(Opponent.transform.position) ? remoteOutsideTimer + Time.unscaledDeltaTime : 0;
                if (remoteOutsideTimer >= 5f)
                {
                    network.SendPacket(DuelPacketCodec.SerializeRoundSignal(DuelPacketType.ReturnToArena, roundId));
                    remoteOutsideTimer = 0;
                }
            }
        }

        private void ReturnLocalPlayerToArena()
        {
            if (localPlayer == null || localPlayer.Health.IsDead) return;
            localPlayer.Place(isHost ? LocalSpawnPoint : RivalSpawnPoint);
            localPlayer.Face(Opponent != null ? Opponent.transform.position : (isHost ? RivalSpawnPoint : LocalSpawnPoint));
            LocalBoundarySecondsRemaining = -1;
            SetNotice("RETURNED TO DUEL AREA", 1.5f);
            SendLocalStateSync();
        }

        private void ResolveTimedRound()
        {
            float mine = localPlayer != null ? localPlayer.Health.Health : 0;
            float theirs = Opponent != null ? Opponent.CurrentHealth : 0;
            if (mine > theirs) LocalScore++;
            else if (theirs > mine) RivalScore++;
            CheckMatchCompletion();
            if (MatchState == DuelMatchState.RoundOver) SetNotice(mine == theirs ? "TIME // DRAW. NEXT ROUND..." : "TIME // HIGHER HEALTH WINS. NEXT ROUND...", 3f);
            SendRoundResult("TIMEOUT");
        }

        private void ApplyConfirmedLocalHit(byte zone, float damage, Vector3 point, int weapon, float distance)
        {
            if (Reconnecting || MatchState != DuelMatchState.InRound || localPlayer == null || localPlayer.Health.IsDead || damage <= 0) return;
            localPlayer.Health.Damage(damage, Opponent != null ? Opponent.transform.position : point);
            localPlayer.ShowDuelHitFeedback(damage);
            if (localPlayer.Health.IsDead)
                DuelKillReplay.BeginReplay(network.RemoteCallsign, weapon, zone, damage, distance);
            SendLocalStateSync();
        }

        private void CheckMatchCompletion()
        {
            awaitingResumeClock = false;
            if (LocalScore >= TargetScore || RivalScore >= TargetScore)
            {
                bool firstCompletion = MatchState != DuelMatchState.MatchOver;
                MatchState = DuelMatchState.MatchOver;
                if (!firstCompletion) return;
                completedMatchScope = roundId;
                ClearRematchNegotiation("");
                NotifyCompletedMultiplayerMatch();
                bool victory = LocalScore > RivalScore;
                SetNotice(victory ? "VICTORY! CONTRACT SECURED!\n+50 Cr  +200 XP" : "DEFEAT // RIVAL PREVAILED", 10.0f);

                if (victory)
                {
                    PlayerProgression.AddXP(200);
                    int bounty = PlayerPrefs.GetInt("GeoSniper.BountyStash", 0) + 50;
                    PlayerPrefs.SetInt("GeoSniper.BountyStash", bounty);
                    int wins = PlayerPrefs.GetInt("GeoSniper.DuelWins", 0) + 1;
                    PlayerPrefs.SetInt("GeoSniper.DuelWins", wins);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                MatchState = DuelMatchState.RoundOver;
                roundTransitionTimer = 3.5f;
            }
        }

        private void StartNextRound()
        {
            BeginArena();
        }

        private void NotifyCompletedMultiplayerMatch()
        {
            if (matchCompletionNotified || !HasActiveDuel || MatchState != DuelMatchState.MatchOver
                || network == null || !network.IsConnected || (LocalScore < TargetScore && RivalScore < TargetScore)) return;
            matchCompletionNotified = true;
            completedMatchScope = roundId;
            AdManager.Instance.NotifyDuelMatchCompleted();
        }

        public void RequestRematch()
        {
            TickRematch(Time.unscaledTime);
            if (!CanRematch || completedMatchScope <= 0) return;
            if (RematchIncomingPending) { AcceptRematch(); return; }
            if (HasRematchNegotiation || nextRematchRequestId == int.MaxValue) return;
            outgoingRematchId = ++nextRematchRequestId;
            rematchDeadline = Time.unscaledTime + RematchTimeout;
            RematchStatus = "REMATCH REQUESTED // WAITING FOR RIVAL";
            SendRematch(DuelRematchAction.Request, outgoingRematchId, isHost);
        }

        public void AcceptRematch()
        {
            TickRematch(Time.unscaledTime);
            if (!RematchIncomingPending) return;
            incomingRematchAccepted = true;
            RematchStatus = "REMATCH ACCEPTED // CONFIRMING WITH HOST";
            SendRematch(DuelRematchAction.Accept, incomingRematchId, !isHost);
            if (isHost) OfferAgreedRematch(incomingRematchId, false);
        }

        public void DeclineRematch()
        {
            if (!RematchIncomingPending) return;
            SendRematch(DuelRematchAction.Cancel, incomingRematchId, !isHost);
            ClearRematchNegotiation("REMATCH DECLINED");
        }

        public void CancelRematch()
        {
            if (!CanCancelRematch) return;
            SendRematchCancellations();
            ClearRematchNegotiation("REMATCH CANCELLED");
        }

        private void SendRematch(DuelRematchAction action, int request, bool byHost, int nextRound = 0)
        {
            network?.SendPacket(DuelPacketCodec.SerializeRematch(action, completedMatchScope, request, byHost, nextRound));
        }

        private void SendRematchCancellations()
        {
            if (outgoingRematchId > 0) SendRematch(DuelRematchAction.Cancel, outgoingRematchId, isHost);
            if (incomingRematchId > 0) SendRematch(DuelRematchAction.Cancel, incomingRematchId, !isHost);
        }

        private void HandleRematchSignal(DuelRematchAction action, int scope, int request, bool byHost, int nextRound)
        {
            if (scope != completedMatchScope || scope <= 0 || network == null || !network.IsConnected || network.IsReconnecting) return;
            if (action == DuelRematchAction.Cancel && rematchStartInFlight && MatchState == DuelMatchState.Warmup
                && request == committedRematchId && byHost == committedRematchByHost)
            {
                CancelRematchForConnection("REMATCH CANCELLED");
                return;
            }
            TickRematch(Time.unscaledTime);
            if (!CanRematch) return;
            switch (action)
            {
                case DuelRematchAction.Request:
                    // Only the proposal creator may request; an old request never extends a deadline.
                    if (byHost == isHost || request <= lastRemoteRematchId) return;
                    lastRemoteRematchId = request;
                    if (incomingRematchId > 0 || rematchOfferId > 0) return;
                    incomingRematchId = request;
                    if (!HasRematchDeadline) rematchDeadline = Time.unscaledTime + RematchTimeout;
                    RematchStatus = "RIVAL REQUESTS A REMATCH";
                    if (outgoingRematchId > 0)
                    {
                        incomingRematchAccepted = true;
                        RematchStatus = "BOTH PLAYERS REQUESTED // CONFIRMING REMATCH";
                        SendRematch(DuelRematchAction.Accept, request, byHost);
                        if (isHost) OfferAgreedRematch(request, byHost);
                    }
                    break;
                case DuelRematchAction.Accept:
                    if (byHost != isHost || request != outgoingRematchId) return;
                    RematchStatus = "RIVAL ACCEPTED // CONFIRMING REMATCH";
                    if (isHost) OfferAgreedRematch(request, byHost);
                    break;
                case DuelRematchAction.Cancel:
                    if (byHost != isHost) lastRemoteRematchId = Mathf.Max(lastRemoteRematchId, request);
                    if ((byHost == isHost && request == outgoingRematchId)
                        || (byHost != isHost && request == incomingRematchId)
                        || (request == rematchOfferId && byHost == rematchOfferByHost))
                        ClearRematchNegotiation("REMATCH CANCELLED BY RIVAL");
                    break;
                case DuelRematchAction.Offer:
                    // Only the host offers a proposal to which this guest has already consented.
                    if (isHost || rematchOfferId > 0 || !HasConsent(request, byHost)) return;
                    rematchOfferId = request; rematchOfferByHost = byHost; rematchReady = true;
                    RematchStatus = "BOTH PLAYERS READY // STARTING REMATCH";
                    SendRematch(DuelRematchAction.Ready, request, byHost);
                    break;
                case DuelRematchAction.Ready:
                    if (!isHost || request != rematchOfferId || byHost != rematchOfferByHost || !HasConsent(request, byHost)) return;
                    int nextArena = Mathf.Max(Mathf.Max(roundId, network.ArenaRound), rematchArenaSequence) + 1;
                    if (nextArena <= scope) return;
                    SendRematch(DuelRematchAction.Commit, request, byHost, nextArena);
                    BeginAgreedRematch(request, byHost, nextArena);
                    break;
                case DuelRematchAction.Commit:
                    if (isHost || !rematchReady || request != rematchOfferId || byHost != rematchOfferByHost
                        || !HasConsent(request, byHost) || nextRound <= Mathf.Max(roundId, rematchArenaSequence)) return;
                    BeginAgreedRematch(request, byHost, nextRound);
                    break;
            }
        }

        private bool HasRematchDeadline => rematchDeadline > 0;
        private bool HasConsent(int request, bool byHost) => byHost == isHost
            ? outgoingRematchId == request : incomingRematchId == request && incomingRematchAccepted;

        private void OfferAgreedRematch(int request, bool byHost)
        {
            if (!isHost || rematchOfferId > 0 || !HasConsent(request, byHost)) return;
            rematchOfferId = request; rematchOfferByHost = byHost;
            RematchStatus = "RIVAL ACCEPTED // CONFIRMING REMATCH";
            SendRematch(DuelRematchAction.Offer, request, byHost);
        }

        private void BeginAgreedRematch(int request, bool byHost, int nextRound)
        {
            rematchPreviousLocalScore = LocalScore; rematchPreviousRivalScore = RivalScore;
            rematchPreviousNotified = matchCompletionNotified;
            committedRematchId = request; committedRematchByHost = byHost;
            rematchStartInFlight = true;
            rematchArenaSequence = pendingRematchArenaRound = nextRound;
            ClearRematchNegotiation("");
            matchCompletionNotified = false;
            LocalScore = RivalScore = 0;
            BeginArena();
        }

        private void TickRematch(float now)
        {
            if (!HasRematchNegotiation || now < rematchDeadline) return;
            SendRematchCancellations();
            ClearRematchNegotiation("REMATCH REQUEST EXPIRED");
        }

        private void ClearRematchNegotiation(string status)
        {
            outgoingRematchId = incomingRematchId = rematchOfferId = 0;
            incomingRematchAccepted = rematchOfferByHost = rematchReady = false;
            rematchDeadline = 0;
            RematchStatus = status;
        }

        private void CancelRematchForConnection(string status)
        {
            if (HasRematchNegotiation) ClearRematchNegotiation(status);
            if (!rematchStartInFlight || MatchState != DuelMatchState.Warmup) return;
            if (arenaRoutine != null) StopCoroutine(arenaRoutine);
            arenaRoutine = null;
            ClearArenaCover();
            LocalScore = rematchPreviousLocalScore; RivalScore = rematchPreviousRivalScore;
            matchCompletionNotified = rematchPreviousNotified;
            rematchStartInFlight = false; committedRematchId = pendingRematchArenaRound = 0;
            MatchState = DuelMatchState.MatchOver;
            RematchStatus = status;
            if (localPlayer != null) localPlayer.InputBlocked = true;
        }

        public void ResetSession(string reason = "Left the duel.")
        {
            // Detach gameplay before Disconnect invokes its synchronous callbacks.
            mission = null;
            ClearRematchNegotiation("");
            completedMatchScope = nextRematchRequestId = lastRemoteRematchId = rematchArenaSequence = pendingRematchArenaRound = committedRematchId = 0;
            rematchStartInFlight = false;
            matchCompletionNotified = false;
            duelExitFinished = true;
            if (localPlayer != null) localPlayer.InputBlocked = false;
            localPlayer = null; playerCamera = null;
            if (arenaRoutine != null) StopCoroutine(arenaRoutine);
            arenaRoutine = null;
            MatchState = DuelMatchState.Idle;
            MatchNotice = ""; NoticeTimer = 0;
            LocalScore = RivalScore = roundId = 0;
            RoundSecondsRemaining = 0; LocalBoundarySecondsRemaining = -1;
            awaitingResumeClock = false; rivalRevealUntil = 0;
            localHistory.Clear(); rivalHistory.Clear();
            localShots.ResetRound(0); rivalShots.ResetRound(0);
            DuelKillReplay.Clear();
            ClearArenaCover();
            DestroyOpponent();
            var activeNetwork = network != null ? network : SniperDuelNetwork.ActiveInstance;
            activeNetwork?.Disconnect(reason);
        }

        public void LeaveDuel()
        {
            if (leavingDuel || duelExitFinished) return;
            var ads = CompletedDuelExitEligible() ? AdManager.ActiveInstance : null;
            leavingDuel = true;
            try
            {
                // Remove live gameplay before an SDK overlay can take focus for several seconds.
                if (mission != null) mission.enabled = false;
                ResetSession();
                var game = GeoSniperGame.Instance;
                game?.ReturnToLobby(-1);
                if (game != null && game.currentTab != GeoSniperGame.LobbyTab.Armory)
                    game.SwitchTab(GeoSniperGame.LobbyTab.Home);
                ads?.ShowDuelExitInterstitialIfReady();
            }
            finally { leavingDuel = false; }
        }

        private bool CompletedDuelExitEligible() => matchCompletionNotified && HasActiveDuel && MatchState == DuelMatchState.MatchOver
            && network != null && !network.IsReconnecting;

        private void SetNotice(string text, float duration)
        {
            MatchNotice = text;
            NoticeTimer = duration;
        }

        void DestroyOpponent()
        {
            if (Opponent == null) return;
            if (Application.isPlaying) Destroy(Opponent.gameObject);
            else DestroyImmediate(Opponent.gameObject);
            Opponent = null;
        }
    }
}
