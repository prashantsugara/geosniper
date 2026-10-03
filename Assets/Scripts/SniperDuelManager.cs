using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeoSniper.Duel
{
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

        public DuelMatchState MatchState { get; private set; } = DuelMatchState.Idle;
        public int LocalScore { get; private set; } = 0;
        public int RivalScore { get; private set; } = 0;
        public int TargetScore { get; private set; } = 3;
        public string MatchNotice { get; private set; } = "";
        public float NoticeTimer { get; private set; } = 0;

        public DuelOpponentAvatar Opponent { get; private set; }
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

            network.OnConnected += HandleConnected;
            network.OnDisconnected += HandleDisconnected;
            network.OnPacketReceived += HandlePacketReceived;

            DamageSystem.OnDamageDealt += HandleLocalDamageDealt;
        }

        private void OnDestroy()
        {
            if (network != null)
            {
                network.OnConnected -= HandleConnected;
                network.OnDisconnected -= HandleDisconnected;
                network.OnPacketReceived -= HandlePacketReceived;
            }
            DamageSystem.OnDamageDealt -= HandleLocalDamageDealt;
        }

        private void EnsureNetwork()
        {
            if (network == null)
            {
                network = SniperDuelNetwork.Instance;
                if (network != null)
                {
                    network.OnConnected -= HandleConnected;
                    network.OnDisconnected -= HandleDisconnected;
                    network.OnPacketReceived -= HandlePacketReceived;
                    network.OnConnected += HandleConnected;
                    network.OnDisconnected += HandleDisconnected;
                    network.OnPacketReceived += HandlePacketReceived;
                }
            }
        }

        public void StartDuelMatch(UrbanCombatMission activeMission, bool asHost, string roomCodeOrIp = "")
        {
            EnsureNetwork();
            mission = activeMission;
            isHost = asHost;
            localPlayer = UrbanPlayer.Instance;
            playerCamera = Camera.main;

            LocalScore = 0;
            RivalScore = 0;

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
                if (network != null && !network.IsConnected) network.StartClient(roomCodeOrIp);
            }
        }

        private void HandleConnected(string rivalCallsign)
        {
            SetNotice("CHALLENGER CONNECTED: " + rivalCallsign + "!\nPREPARING ROOFTOP STANDOFF...", 3f);
            StartCoroutine(InitializeDuelArenaRoutine());
        }

        private void HandleDisconnected()
        {
            MatchState = DuelMatchState.Idle;
            SetNotice("DUEL TERMINATED: RIVAL DISCONNECTED", 5f);
            if (Opponent != null)
            {
                Destroy(Opponent.gameObject);
                Opponent = null;
            }
        }

        private IEnumerator InitializeDuelArenaRoutine()
        {
            float timeout = 12f;
            while ((localPlayer == null || mission == null || Camera.main == null) && timeout > 0)
            {
                localPlayer = UrbanPlayer.Instance;
                mission = FindAnyObjectByType<UrbanCombatMission>();
                playerCamera = Camera.main;
                timeout -= Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);

            // Locate two elevated rooftop perches with clear line-of-sight
            DetermineRooftopPerches();

            // Disable single-player AI rival bot now that real opponent is active
            if (mission != null)
            {
                mission.DisableAIRivalForMultiplayer();
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

            // Warmup countdown
            MatchState = DuelMatchState.Warmup;
            for (int i = 3; i >= 1; i--)
            {
                SetNotice("DUEL STARTS IN " + i + "...", 1.0f);
                yield return new WaitForSeconds(1.0f);
            }

            SetNotice("ENGAGE! ELIMINATE THE RIVAL MARKSMAN!", 2.5f);
            MatchState = DuelMatchState.InRound;
        }

        private void DetermineRooftopPerches()
        {
            if (localPlayer != null && mission != null && mission.DuelRivalPosition != Vector3.zero)
            {
                LocalSpawnPoint = localPlayer.transform.position;
                RivalSpawnPoint = mission.DuelRivalPosition;
                return;
            }

            // Default rooftops if none found dynamically
            LocalSpawnPoint = new Vector3(0, 18f, -120f);
            RivalSpawnPoint = new Vector3(25f, 22f, 85f);

            if (mission != null && mission.RooftopSpawns != null && mission.RooftopSpawns.Count >= 2)
            {
                var perches = new List<Vector3>();
                foreach (var r in mission.RooftopSpawns)
                {
                    if (r.y >= 6.0f) perches.Add(r);
                }

                float bestDist = 0;
                for (int i = 0; i < perches.Count; i++)
                {
                    for (int j = i + 1; j < perches.Count; j++)
                    {
                        float d = Vector3.Distance(perches[i], perches[j]);
                        if (d >= 80f && d <= 260f)
                        {
                            if (!Physics.Linecast(perches[i] + Vector3.up * 1.6f, perches[j] + Vector3.up * 1.6f, ~0, QueryTriggerInteraction.Ignore))
                            {
                                if (d > bestDist)
                                {
                                    bestDist = d;
                                    LocalSpawnPoint = perches[i];
                                    RivalSpawnPoint = perches[j];
                                }
                            }
                        }
                    }
                }
            }
        }

        private void Update()
        {
            if (NoticeTimer > 0) NoticeTimer -= Time.deltaTime;

            if (MatchState == DuelMatchState.InRound)
            {
                syncTimer += Time.deltaTime;
                if (syncTimer >= SyncRate)
                {
                    syncTimer = 0;
                    SendLocalStateSync();
                }
            }
            else if (MatchState == DuelMatchState.RoundOver)
            {
                roundTransitionTimer -= Time.deltaTime;
                if (roundTransitionTimer <= 0)
                {
                    StartNextRound();
                }
            }
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

            byte[] sync = DuelPacketCodec.SerializeStateSync(pos, yaw, pitch, isAiming, isCrouching, hp);
            network.SendPacket(sync);
        }

        public void NotifyLocalGunshot(Vector3 muzzle, Vector3 dir, int weaponIndex)
        {
            if (MatchState != DuelMatchState.InRound || !network.IsConnected) return;

            byte[] fire = DuelPacketCodec.SerializeFire(muzzle, dir, weaponIndex);
            network.SendPacket(fire);
        }

        private void HandleLocalDamageDealt(DamageInfo info)
        {
            if (MatchState != DuelMatchState.InRound || Opponent == null || !network.IsConnected) return;

            // Check if damage was dealt to Opponent avatar
            float distToOpp = Vector3.Distance(info.hitPoint, Opponent.transform.position);
            if (distToOpp < 2.5f)
            {
                byte zone = (byte)(info.isHeadshot ? 2 : info.bodyPart == "limb" ? 0 : 1);
                float dmg = info.isHeadshot ? 100f : info.baseDamage * info.multiplier;

                Opponent.TakeLocalDamage(zone, dmg, info.hitPoint);

                byte[] hitPkt = DuelPacketCodec.SerializeHit(zone, dmg, info.hitPoint);
                network.SendPacket(hitPkt);

                if (Opponent.IsDead)
                {
                    HandleOpponentEliminated(info.isHeadshot);
                }
            }
        }

        private void HandleOpponentEliminated(bool isHeadshot)
        {
            LocalScore++;
            float dist = Vector3.Distance(localPlayer.transform.position, Opponent.transform.position);

            SetNotice(isHeadshot ? $"HEADSHOT ELIMINATION! (+350 Cr)\nRANGE: {dist:F0}m" : $"RIVAL ELIMINATED! (+200 Cr)\nRANGE: {dist:F0}m", 3.0f);
            OnKillfeedEntry?.Invoke(network.LocalCallsign, isHeadshot, dist);

            byte[] deathPkt = DuelPacketCodec.SerializeDeath(network.LocalCallsign, isHeadshot, dist);
            network.SendPacket(deathPkt);

            CheckMatchCompletion();
        }

        private void HandlePacketReceived(DuelPacketType type, BinaryReader br)
        {
            switch (type)
            {
                case DuelPacketType.StateSync:
                    DuelPacketCodec.DeserializeStateSync(br, out Vector3 pos, out float yaw, out float pitch, out bool isAiming, out bool isCrouch, out float hp);
                    if (Opponent != null)
                    {
                        Opponent.ApplyNetworkState(pos, yaw, pitch, isAiming, isCrouch, hp);
                    }
                    break;

                case DuelPacketType.Fire:
                    DuelPacketCodec.DeserializeFire(br, out Vector3 muzzle, out Vector3 dir, out int wpnIdx);
                    if (Opponent != null)
                    {
                        Opponent.PlayRemoteFire(muzzle, dir, wpnIdx);
                    }
                    break;

                case DuelPacketType.Hit:
                    DuelPacketCodec.DeserializeHit(br, out byte hitZone, out float damage, out Vector3 hitPoint);
                    ApplyHitToLocalPlayer(hitZone, damage, hitPoint);
                    break;

                case DuelPacketType.Death:
                    DuelPacketCodec.DeserializeDeath(br, out string killer, out bool headshot, out float range);
                    RivalScore++;
                    SetNotice($"YOU WERE ELIMINATED BY {killer}!\nRANGE: {range:F0}m", 3.0f);
                    OnKillfeedEntry?.Invoke(killer, headshot, range);
                    CheckMatchCompletion();
                    break;

                case DuelPacketType.Rematch:
                    StartCoroutine(InitializeDuelArenaRoutine());
                    break;
            }
        }

        private void ApplyHitToLocalPlayer(byte hitZone, float damage, Vector3 hitPoint)
        {
            if (localPlayer != null && localPlayer.Health != null)
            {
                localPlayer.Health.Damage(damage, hitPoint);
                TimeScaleController.SetHitCam(0.1f);

                if (localPlayer.Health.IsDead)
                {
                    RivalScore++;
                    float dist = Opponent != null ? Vector3.Distance(localPlayer.transform.position, Opponent.transform.position) : 120f;
                    byte[] deathPkt = DuelPacketCodec.SerializeDeath(network.RemoteCallsign, hitZone == 2, dist);
                    network.SendPacket(deathPkt);
                    CheckMatchCompletion();
                }
            }
        }

        private void CheckMatchCompletion()
        {
            if (LocalScore >= TargetScore || RivalScore >= TargetScore)
            {
                MatchState = DuelMatchState.MatchOver;
                bool victory = LocalScore > RivalScore;
                SetNotice(victory ? "VICTORY! CONTRACT SECURED!\n+$60,000 Cr  +200 XP" : "DEFEAT // RIVAL PREVAILED", 10.0f);

                if (victory)
                {
                    int xp = PlayerPrefs.GetInt("GeoSniper.XP", 0) + 200;
                    PlayerPrefs.SetInt("GeoSniper.XP", xp);
                    int bounty = PlayerPrefs.GetInt("GeoSniper.BountyStash", 0) + 60000;
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
            StartCoroutine(InitializeDuelArenaRoutine());
        }

        public void RequestRematch()
        {
            LocalScore = 0;
            RivalScore = 0;
            byte[] rematch = DuelPacketCodec.SerializeSimple(DuelPacketType.Rematch);
            network.SendPacket(rematch);
            StartCoroutine(InitializeDuelArenaRoutine());
        }

        public void LeaveDuel()
        {
            network?.Disconnect();
            MatchState = DuelMatchState.Idle;
            if (Opponent != null)
            {
                Destroy(Opponent.gameObject);
                Opponent = null;
            }
            if (GeoSniperGame.Instance != null)
            {
                GeoSniperGame.Instance.ReturnToLobby(-1);
            }
        }

        private void SetNotice(string text, float duration)
        {
            MatchNotice = text;
            NoticeTimer = duration;
        }
    }
}
