using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GeoSniper.Duel
{
    public struct DiscoveredHost
    {
        public string callsign;
        public IPEndPoint endpoint;
        public double latitude;
        public double longitude;
        public float lastSeenTime;
    }

    public sealed partial class SniperDuelNetwork : MonoBehaviour
    {
        private static SniperDuelNetwork instance;
        public static SniperDuelNetwork ActiveInstance => instance;
        public static SniperDuelNetwork Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<SniperDuelNetwork>();
                    if (instance == null)
                    {
                        var go = new GameObject("SniperDuelNetwork");
                        instance = go.AddComponent<SniperDuelNetwork>();
                    }
                }
                return instance;
            }
        }

        public const int DefaultPort = 7777;
        public const int BeaconPort = 7776;
        private const int MaxDatagramBytes = 2048;

        public string LastDisconnectReason { get; private set; } = "";

        public int HostPort { get; private set; } = DefaultPort;

        public bool IsHost { get; private set; }
        public bool IsConnecting => !IsHost && !IsConnected && !IsReconnecting && (relayStarting || (RemoteEndPoint != null && (socket != null || usingRelay)));
        public bool IsConnected { get; private set; }
        public bool IsReconnecting { get; private set; }
        public float ReconnectSecondsRemaining => IsReconnecting ? Math.Max(0, ReconnectWindow - (Time.unscaledTime - reconnectStarted)) : 0;
        public string LocalCallsign { get; set; } = "OPERATOR";
        public string RemoteCallsign { get; private set; } = "RIVAL";
        public string RoomCode { get; private set; } = "";
        public string PublicEndpointString { get; private set; } = "";
        public int PingMs { get; private set; } = 0;
        public bool CompetitiveSpotting { get; private set; }
        public bool RoomRulesLocked => !IsHost || SharedMapData != null || ArenaRound > 0;
        public bool ConfigureRoom(bool competitive)
        {
            if (RoomRulesLocked) return false;
            CompetitiveSpotting = competitive; return true;
        }


        public int ReadyRound { get; private set; }
        public int EngageRound { get; private set; }

        public int ArenaRound { get; private set; }
        public Vector3 ArenaHostSpawn { get; private set; }
        public Vector3 ArenaGuestSpawn { get; private set; }
        public const int MaxMapBytes = 4 * 1024 * 1024;
        public byte[] SharedMapData { get; private set; }
        public double HostLatitude { get; private set; }
        public double HostLongitude { get; private set; }
        private byte[] receivingMap;
        private int mapSendOffset, mapReceiveOffset;

        public void SetHostMap(byte[] data, double latitude, double longitude)
        {
            if (!IsHost || SharedMapData != null || data == null || data.Length == 0 || data.Length > MaxMapBytes)
                throw new InvalidDataException("Cannot configure this duel room's map.");
            SharedMapData = data; HostLatitude = latitude; HostLongitude = longitude; mapSendOffset = 0;
            if (IsConnected) SendWelcome(0);
        }

        private void SendMapBatch()
        {
            if (!IsHost || !IsConnected || IsReconnecting || SharedMapData == null || pendingReliable.Count != 0) return;
            for (int i = 0; i < 16 && mapSendOffset < SharedMapData.Length; i++)
            {
                int count = Math.Min(900, SharedMapData.Length - mapSendOffset);
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(DuelPacketCodec.ProtocolMagic); writer.Write((byte)DuelPacketType.MapChunk);
                    writer.Write(SharedMapData.Length); writer.Write(mapSendOffset);
                    writer.Write(SharedMapData, mapSendOffset, count);
                    SendPacket(stream.ToArray());
                }
                mapSendOffset += count;
            }
        }

        public IPEndPoint RemoteEndPoint { get; private set; }

        private readonly string discoveryId = Guid.NewGuid().ToString("N");
        private UdpClient socket;
        private UdpClient beaconSocket;
        private CancellationTokenSource cancelTokenSource;

        private readonly ConcurrentQueue<ReceivedPacket> incomingQueue = new ConcurrentQueue<ReceivedPacket>();
        public readonly List<DiscoveredHost> DiscoveredLANHosts = new List<DiscoveredHost>();

        public event Action<DuelPacketType, BinaryReader> OnPacketReceived;
        public event Action<string> OnConnected;
        public event Action OnDisconnected;
        public event Action<bool> OnReconnectStateChanged;

        private sealed class PendingReliable
        {
            public byte[] data;
            public float lastSent;
        }
        private readonly Dictionary<uint, PendingReliable> pendingReliable = new Dictionary<uint, PendingReliable>();
        private readonly List<PendingReliable> retryBatch = new List<PendingReliable>(32);
        private readonly SortedDictionary<uint, byte[]> orderedIncoming = new SortedDictionary<uint, byte[]>();
        private uint nextSendId = 1, nextReceiveId = 1;

        private float lastBeaconTime;
        private float lastPingTime;
        private float lastPacketTime;
        private const float ConnectionTimeout = 8f;
        private const float ReconnectWindow = 30f;
        private bool gameplayStarted;
        private string clientNonce = "", sessionToken = "", reconnectNonce = "", peerReconnectNonce = "";
        private readonly HashSet<string> completedPeerReconnectNonces = new HashSet<string>();
        private float reconnectStarted, lastReconnectSent;
        private byte reconnectPhase;
        private uint localPauseSendThrough, peerPauseSendThrough;

        private struct ReceivedPacket
        {
            public byte[] data;
            public IPEndPoint sender;
        }

        private struct DiscoveredBeaconData
        {
            public string callsign;
            public IPEndPoint endpoint;
            public double latitude;
            public double longitude;
        }
        private readonly ConcurrentQueue<DiscoveredBeaconData> beaconQueue = new ConcurrentQueue<DiscoveredBeaconData>();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            LocalCallsign = PlayerPrefs.GetString("GeoSniper.PlayerName", "SPECTRE-01").ToUpperInvariant();
        }

        private void Update()
        {
            PumpRelay();
            // Process queued network packets on the main Unity thread
            while (incomingQueue.TryDequeue(out var item))
            {
                ProcessPacket(item.data, item.sender);
            }

            retryBatch.Clear();
            retryBatch.AddRange(pendingReliable.Values);
            foreach (var pending in retryBatch)
            {
                if (IsReconnecting) break;
                if (Time.unscaledTime - pending.lastSent < .25f) continue;
                pending.lastSent = Time.unscaledTime;
                SendRaw(pending.data);
            }
            SendMapBatch();

            // Process queued LAN beacon discoveries safely on the main thread
            while (beaconQueue.TryDequeue(out var b))
            {
                UpdateDiscoveredHost(b.callsign, b.endpoint, b.latitude, b.longitude);
            }

            // Beacon broadcast for Host
            if (IsHost && socket != null)
            {
                if (Time.unscaledTime - lastBeaconTime > 1.2f)
                {
                    lastBeaconTime = Time.unscaledTime;
                    SendLANBeacon();
                }
            }

            if (IsConnecting && !relayStarting && Time.unscaledTime - lastPacketTime > 20f)
            {
                Disconnect("No reply from the host within 20 seconds. Check the join code and that its room is still open.");
            }

            // Handshake retry for client until connected
            if (IsConnecting)
            {
                if (Time.unscaledTime - lastPingTime > 1.0f)
                {
                    lastPingTime = Time.unscaledTime;
                    SendHello();
                }
            }

            // Ping keep-alive when connected
            if (IsConnected && !IsReconnecting && RemoteEndPoint != null)
            {
                if (Time.unscaledTime - lastPingTime > 1.0f)
                {
                    lastPingTime = Time.unscaledTime;
                    long nowTicks = DateTime.UtcNow.Ticks;
                    byte[] ping = DuelPacketCodec.SerializePing(nowTicks);
                    SendRaw(ping);
                }

                // Check for timeout
                if (Time.unscaledTime - lastPacketTime > (gameplayStarted ? ConnectionTimeout : 60f))
                {
                    BeginReconnect();
                }
            }

            if (IsReconnecting)
            {
                if (Time.unscaledTime - reconnectStarted >= ReconnectWindow)
                    Disconnect("Could not reconnect within 30 seconds. Exit duel and rejoin the host.");
                else if (Time.unscaledTime - lastReconnectSent >= .5f)
                    SendReconnectAttempt();
            }

            // Clean up stale LAN discovery entries
            for (int i = DiscoveredLANHosts.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime - DiscoveredLANHosts[i].lastSeenTime > 4.0f)
                {
                    DiscoveredLANHosts.RemoveAt(i);
                }
            }
        }

        public bool StartLANHost(int port = DefaultPort)
        {
            CloseSockets();
            LastDisconnectReason = "";
            cancelTokenSource = new CancellationTokenSource();
            try
            {
                socket = new UdpClient(port);
                HostPort = port;
                socket.EnableBroadcast = true;
                IsHost = true;
                IsConnected = false;
                clientNonce = "";
                RemoteEndPoint = null;
                lastPacketTime = Time.unscaledTime;

                // Generate a local room code based on port and random salt
                RoomCode = GenerateRoomCode(port);

                // LAN transport only. Internet rooms use Unity Relay when configured.
                PublicEndpointString = "";

                // Start async packet receive loop
                BeginReceive();
                Debug.LogWarning("[DuelNetwork] HOST listening at " + GetLocalIPAddress() + ":" + port);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DuelNetwork] Failed to start host: " + ex.Message);
                CloseSockets();
                LastDisconnectReason = "Could not open duel room on port " + port + ": " + ex.Message;
                return false;
            }
        }

        public bool StartLANClient(string targetHostOrCode, int port = DefaultPort)
        {
            CloseSockets();
            LastDisconnectReason = "";
            cancelTokenSource = new CancellationTokenSource();
            try
            {
                socket = new UdpClient(0); // auto bind ephemeral port
                socket.EnableBroadcast = true;
                IsHost = false;
                IsConnected = false;
                clientNonce = Guid.NewGuid().ToString("N");
                lastPacketTime = Time.unscaledTime;

                IPEndPoint targetEP = ResolveTarget(targetHostOrCode, port);
                if (targetEP == null)
                {
                    Debug.LogWarning("[DuelNetwork] Cannot resolve target: " + targetHostOrCode);
                    CloseSockets();
                    LastDisconnectReason = "Invalid host code or IP address. Check the invite and try again.";
                    return false;
                }

                RemoteEndPoint = targetEP;
                Debug.LogWarning("[DuelNetwork] JOIN target: " + targetEP);
                BeginReceive();

                // Send initial Hello handshake packet
                SendHello();

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DuelNetwork] Failed to start client: " + ex.Message);
                CloseSockets();
                LastDisconnectReason = "Could not connect to duel room: " + ex.Message;
                return false;
            }
        }

        public void PrepareToJoin()
        {
            // Join mode must not advertise a local room or retain its own beacon.
            CloseSockets();
            DiscoveredLANHosts.Clear();
            while (beaconQueue.TryDequeue(out _)) { }

        }

        public void StartLANDiscovery()
        {
            if (beaconSocket != null) return;
            try
            {
                beaconSocket = new UdpClient();
                beaconSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                beaconSocket.Client.Bind(new IPEndPoint(IPAddress.Any, BeaconPort));
                beaconSocket.EnableBroadcast = true;
                BeginBeaconReceive();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DuelNetwork] LAN discovery bind: " + ex.Message);
            }
        }

        public void StopLANDiscovery()
        {
            try { beaconSocket?.Close(); } catch { }
            beaconSocket = null;
        }

        public void SendPacket(byte[] data)
        {
            if (!IsConnected || data == null || data.Length < 2 || RemoteEndPoint == null || (socket == null && !usingRelay)) return;
            var type = (DuelPacketType)data[1];
            if (type == DuelPacketType.Engage) gameplayStarted = true;
            if (IsReliableType(type))
            {
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    uint id = nextSendId++;
                    bw.Write(DuelPacketCodec.ProtocolMagic);
                    bw.Write((byte)DuelPacketType.Reliable);
                    bw.Write(id);
                    bw.Write(data);
                    data = ms.ToArray();
                    pendingReliable[id] = new PendingReliable {data = data, lastSent = Time.unscaledTime};
                }
            }
            if (!IsReconnecting) SendRaw(data);
        }

        private static bool IsReliableType(DuelPacketType type) => type == DuelPacketType.Fire || type == DuelPacketType.RoomRules
            || type == DuelPacketType.RoundClock || type == DuelPacketType.ReturnToArena || type == DuelPacketType.Impact
            || type == DuelPacketType.Hit || type == DuelPacketType.RoundState || type == DuelPacketType.Rematch
            || type == DuelPacketType.Arena || type == DuelPacketType.Ready || type == DuelPacketType.Engage
            || type == DuelPacketType.MapChunk || type == DuelPacketType.ShotResult || type == DuelPacketType.Reload
            || type == DuelPacketType.ResumeClock;

        private static byte[] AppendHandshake(byte[] packet, params string[] values)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(packet);
                foreach (var value in values) writer.Write(value);
                return stream.ToArray();
            }
        }

        private void SendHello() => SendRaw(AppendHandshake(DuelPacketCodec.SerializeHello(LocalCallsign,
            WeaponCatalog.SelectedOwnedWeapon), clientNonce));

        private void SendWelcome(int seed) => SendRaw(AppendHandshake(DuelPacketCodec.SerializeWelcome(LocalCallsign,
            HostLatitude, HostLongitude, 3, seed), clientNonce, sessionToken));

        // Session envelopes also protect sequence numbers from delayed datagrams of an earlier room.
        // Tokens authenticate possession of this UDP session; they do not encrypt its traffic.
        private byte[] WrapSession(byte[] packet)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(DuelPacketCodec.ProtocolMagic); writer.Write((byte)DuelPacketType.Session);
                writer.Write(DuelPacketCodec.ProtocolVersion); writer.Write(sessionToken); writer.Write(packet);
                return stream.ToArray();
            }
        }

        private void SendRaw(byte[] data)
        {
            if ((socket == null && !usingRelay) || RemoteEndPoint == null) return;
            try
            {
                var type = (DuelPacketType)data[1];
                if (sessionToken.Length != 0 && type != DuelPacketType.Hello && type != DuelPacketType.Welcome
                    && type != DuelPacketType.Reconnect && type != DuelPacketType.ReconnectAck)
                    data = WrapSession(data);
                if(usingRelay) SendRelay(data);
                else socket.BeginSend(data, data.Length, RemoteEndPoint, null, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DuelNetwork] Send error: " + ex.Message);
            }
        }

        private void BeginReceive()
        {
            if (socket == null) return;
            try
            {
                socket.BeginReceive(OnReceiveCallback, socket);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DuelNetwork] BeginReceive error: " + ex.Message);
            }
        }

        private void OnReceiveCallback(IAsyncResult ar)
        {
            // Capture the socket that owns this operation. A callback from an old
            // connection must never consume or re-arm a newly created socket.
            var receiver = ar.AsyncState as UdpClient;
            if (receiver == null) return;
            try
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = receiver.EndReceive(ar, ref sender);
                if (receiver == socket && data != null && data.Length > 0 && data.Length <= MaxDatagramBytes)
                    incomingQueue.Enqueue(new ReceivedPacket { data = data, sender = sender });
            }
            catch (ObjectDisposedException) { }
            catch (SocketException ex)
            {
                // On Windows, an ICMP port-unreachable response can surface as
                // ConnectionReset for UDP. Keep listening while Hello retries.
                if (ex.SocketErrorCode != SocketError.ConnectionReset && ex.SocketErrorCode != SocketError.ConnectionRefused)
                    Debug.LogWarning("[DuelNetwork] Receive callback error: " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DuelNetwork] Receive callback error: " + ex.Message);
            }
            finally
            {
                if (receiver == socket && cancelTokenSource != null && !cancelTokenSource.IsCancellationRequested)
                    BeginReceive();
            }
        }

        private void BeginBeaconReceive()
        {
            if (beaconSocket == null) return;
            try
            {
                beaconSocket.BeginReceive(OnBeaconReceiveCallback, null);
            }
            catch { }
        }

        private void OnBeaconReceiveCallback(IAsyncResult ar)
        {
            if (beaconSocket == null) return;
            try
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = beaconSocket.EndReceive(ar, ref sender);
                ProcessBeacon(data, sender);
            }
            catch { }
            finally
            {
                BeginBeaconReceive();
            }
        }

        private void ProcessBeacon(byte[] data, IPEndPoint sender)
        {
            if (data == null || sender == null) return;
            string text = Encoding.UTF8.GetString(data);
            if (!text.StartsWith("GEOSNIPER_HOST:")) return;
            var parts = text.Split(':');
            if (parts.Length < 5 || (parts.Length >= 6 && parts[5] == discoveryId)) return;
            if (!int.TryParse(parts[2], out int port) || port < 1 || port > 65535) return;
            if (!double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lat)
                || !double.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lon)) return;
            beaconQueue.Enqueue(new DiscoveredBeaconData {callsign=parts[1], endpoint=new IPEndPoint(sender.Address,port), latitude=lat, longitude=lon});
        }

        private void UpdateDiscoveredHost(string callsign, IPEndPoint ep, double lat, double lon)
        {
            for (int i = 0; i < DiscoveredLANHosts.Count; i++)
            {
                if (DiscoveredLANHosts[i].endpoint.Equals(ep))
                {
                    var h = DiscoveredLANHosts[i];
                    h.lastSeenTime = Time.unscaledTime;
                    h.callsign = callsign;
                    DiscoveredLANHosts[i] = h;
                    return;
                }
            }
            DiscoveredLANHosts.Add(new DiscoveredHost
            {
                callsign = callsign,
                endpoint = ep,
                latitude = lat,
                longitude = lon,
                lastSeenTime = Time.unscaledTime
            });
        }

        private void SendLANBeacon()
        {
            try
            {
                double lat = HostLatitude;
                double lon = HostLongitude;
                string msg = $"GEOSNIPER_HOST:{LocalCallsign}:{HostPort}:{lat.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}:{lon.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}:{discoveryId}";
                byte[] bytes = Encoding.UTF8.GetBytes(msg);
                IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, BeaconPort);
                socket.BeginSend(bytes, bytes.Length, broadcastEP, null, null);
            }
            catch { }
        }

        private void BeginReconnect()
        {
            if (!IsConnected || sessionToken.Length == 0 || IsReconnecting) return;
            IsReconnecting = true;
            reconnectStarted = Time.unscaledTime;
            reconnectNonce = Guid.NewGuid().ToString("N");
            reconnectPhase = 0;
            localPauseSendThrough = nextSendId - 1;
            lastReconnectSent = Time.unscaledTime - 1;
            OnReconnectStateChanged?.Invoke(true);
        }

        private void SendReconnectAttempt()
        {
            lastReconnectSent = Time.unscaledTime;
            SendReconnectControl(reconnectPhase == 0 ? DuelPacketType.Reconnect : DuelPacketType.ReconnectAck,
                reconnectNonce, reconnectPhase);
        }

        private void SendReconnectControl(DuelPacketType type, string nonce, byte phase)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(DuelPacketCodec.ProtocolMagic); writer.Write((byte)type);
                writer.Write(DuelPacketCodec.ProtocolVersion); writer.Write(sessionToken);
                writer.Write(nonce); writer.Write(phase); writer.Write(nextReceiveId); writer.Write(localPauseSendThrough);
                SendRaw(stream.ToArray());
            }
        }

        private void RecoverConnection()
        {
            if (!IsReconnecting) return;
            // Simultaneous reconnects can confirm either direction first; retire both
            // attempts so a delayed request cannot start another pause after recovery.
            if (ValidNonce(peerReconnectNonce)) completedPeerReconnectNonces.Add(peerReconnectNonce);
            IsReconnecting = false;
            lastPacketTime = Time.unscaledTime;
            lastPingTime = Time.unscaledTime - 1;
            OnReconnectStateChanged?.Invoke(false);
            DrainOrderedIncoming(RemoteEndPoint);
        }

        private void ProcessReconnect(BinaryReader reader, DuelPacketType type, IPEndPoint sender)
        {
            if (!IsConnected || sessionToken.Length == 0 || reader.ReadByte() != DuelPacketCodec.ProtocolVersion
                || reader.ReadString() != sessionToken) return;
            string nonce = reader.ReadString();
            byte phase = reader.ReadByte();
            uint receivedNext = reader.ReadUInt32();
            uint peerCutoff = reader.ReadUInt32();
            if (!ValidNonce(nonce) || receivedNext == 0 || receivedNext > nextSendId || phase > 2
                || reader.BaseStream.Position != reader.BaseStream.Length) return;
            if (type == DuelPacketType.Reconnect && phase != 0) return;
            if (type == DuelPacketType.ReconnectAck && (phase == 0 ? nonce != reconnectNonce
                : phase == 1 ? nonce != peerReconnectNonce && !completedPeerReconnectNonces.Contains(nonce)
                : nonce != reconnectNonce)) return;
            if (completedPeerReconnectNonces.Contains(nonce) && !RemoteEndPoint.Equals(sender)) return;
            if (type == DuelPacketType.ReconnectAck && !IsReconnecting && !RemoteEndPoint.Equals(sender)) return;

            // A reconnect token permits a NAT mapping/guest source port change without replacing the match.
            RemoteEndPoint = sender;
            peerPauseSendThrough = Math.Max(peerPauseSendThrough, peerCutoff);
            var acknowledged = new List<uint>();
            foreach (var id in pendingReliable.Keys) if (id < receivedNext) acknowledged.Add(id);
            foreach (var id in acknowledged) pendingReliable.Remove(id);
            lastPacketTime = Time.unscaledTime;

            if (type == DuelPacketType.Reconnect)
            {
                if (completedPeerReconnectNonces.Contains(nonce))
                {
                    SendReconnectControl(DuelPacketType.ReconnectAck, nonce, 2);
                    return;
                }
                if (!IsReconnecting) BeginReconnect();
                peerReconnectNonce = nonce;
                SendReconnectControl(DuelPacketType.ReconnectAck, nonce, 0);
            }
            else if (phase == 0)
            {
                if (!IsReconnecting) return;
                reconnectPhase = 1;
                SendReconnectControl(DuelPacketType.ReconnectAck, nonce, 1);
                lastReconnectSent = Time.unscaledTime;
            }
            else if (phase == 1)
            {
                bool alreadyCompleted = !completedPeerReconnectNonces.Add(nonce);
                SendReconnectControl(DuelPacketType.ReconnectAck, nonce, 2);
                // The peer has received the pause acknowledgement and confirmed recovery.
                if (!alreadyCompleted) RecoverConnection();
            }
            else RecoverConnection();
        }

        private static bool ValidNonce(string nonce) => nonce != null && nonce.Length == 32 && Guid.TryParseExact(nonce, "N", out _);

        private void ProcessPacket(byte[] data, IPEndPoint sender)
        {
            if (sender == null || data == null || data.Length < 2 || data.Length > MaxDatagramBytes) return;
            if (data[0] != DuelPacketCodec.ProtocolMagic) return;
            DuelPacketType type = (DuelPacketType)data[1];

            try
            {
                if (type == DuelPacketType.Reconnect || type == DuelPacketType.ReconnectAck)
                {
                    using (var stream = new MemoryStream(data, 2, data.Length - 2))
                    using (var reader = new BinaryReader(stream)) ProcessReconnect(reader, type, sender);
                    return;
                }
                if (RemoteEndPoint != null && !RemoteEndPoint.Equals(sender)) return;
                if (type == DuelPacketType.Session)
                {
                    if (!IsConnected || sessionToken.Length == 0) return;
                    using (var stream = new MemoryStream(data, 2, data.Length - 2))
                    using (var reader = new BinaryReader(stream))
                    {
                        if (reader.ReadByte() != DuelPacketCodec.ProtocolVersion || reader.ReadString() != sessionToken) return;
                        byte[] payload = reader.ReadBytes((int)(stream.Length - stream.Position));
                        if (payload.Length < 2 || payload[0] != DuelPacketCodec.ProtocolMagic) return;
                        var innerType = (DuelPacketType)payload[1];
                        if (innerType == DuelPacketType.Session || innerType == DuelPacketType.Hello || innerType == DuelPacketType.Welcome
                            || innerType == DuelPacketType.Reconnect || innerType == DuelPacketType.ReconnectAck) return;
                        ProcessPayload(payload, sender);
                    }
                    return;
                }
                if (type != DuelPacketType.Hello && type != DuelPacketType.Welcome) return;
                ProcessPayload(data, sender);
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is FormatException)
            {
                Debug.LogWarning("[DuelNetwork] Ignored malformed session packet: " + ex.Message);
            }
        }

        private void DrainOrderedIncoming(IPEndPoint sender)
        {
            if (IsReconnecting) return;
            while (orderedIncoming.TryGetValue(nextReceiveId, out var ordered))
            {
                uint id = nextReceiveId++;
                orderedIncoming.Remove(id);
                var type = (DuelPacketType)ordered[1];
                // Consume old requests to preserve transport ordering, but never
                // replay shooting/reloading or cancelled rematch consent after recovery.
                if (id <= peerPauseSendThrough && (type == DuelPacketType.Fire || type == DuelPacketType.Reload
                    || type == DuelPacketType.Rematch)) continue;
                ProcessPayload(ordered, sender);
            }
        }

        private void ProcessPayload(byte[] data, IPEndPoint sender)
        {
            DuelPacketType type = (DuelPacketType)data[1];

            try
            {
                using (var ms = new MemoryStream(data, 2, data.Length - 2))
                using (var br = new BinaryReader(ms))
                {
                    switch (type)
                    {
                        case DuelPacketType.Ack:
                            if (!IsConnected) return;
                            pendingReliable.Remove(br.ReadUInt32());
                            break;
                        case DuelPacketType.Reliable:
                            if (!IsConnected) return;
                            uint id = br.ReadUInt32();
                            byte[] payload = br.ReadBytes((int)(ms.Length - ms.Position));
                            if (payload.Length < 2 || payload[0] != DuelPacketCodec.ProtocolMagic) return;
                            var innerType = (DuelPacketType)payload[1];
                            if (!IsReliableType(innerType)) return;
                            if (id >= nextReceiveId && id - nextReceiveId > 128) return;
                            using (var ackStream = new MemoryStream())
                            using (var ack = new BinaryWriter(ackStream))
                            {
                                ack.Write(DuelPacketCodec.ProtocolMagic);
                                ack.Write((byte)DuelPacketType.Ack);
                                ack.Write(id);
                                SendRaw(ackStream.ToArray());
                            }
                            if (id < nextReceiveId) break;
                            orderedIncoming[id] = payload;
                            DrainOrderedIncoming(sender);
                            break;
                        case DuelPacketType.RoomRules:
                            if (!IsConnected || IsHost) return;
                            byte rules = br.ReadByte();
                            if (rules > 1 || gameplayStarted) return;
                            CompetitiveSpotting = rules == 1;
                            break;
                        case DuelPacketType.Ready:
                            if (!IsConnected || !IsHost) return;
                            int readyRound = br.ReadInt32();
                            if (readyRound > ReadyRound) ReadyRound = readyRound;
                            break;
                        case DuelPacketType.Engage:
                            if (!IsConnected || IsHost) return;
                            int engageRound = br.ReadInt32();
                            if (engageRound > EngageRound) EngageRound = engageRound;
                            gameplayStarted = true;
                            break;
                        case DuelPacketType.Arena:
                            if (!IsConnected || IsHost) return;
                            int arenaRound = br.ReadInt32();
                            var hostSpawn = new Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle());
                            var guestSpawn = new Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle());
                            if (arenaRound <= ArenaRound || !Finite(hostSpawn) || !Finite(guestSpawn)) return;
                            ArenaHostSpawn = hostSpawn; ArenaGuestSpawn = guestSpawn; ArenaRound = arenaRound;
                            break;
                        case DuelPacketType.Hello:
                            if (IsHost)
                            {
                                bool firstConnection = !IsConnected;
                                DuelPacketCodec.DeserializeHello(br, out string guestCallsign, out int guestWeapon);
                                string nonce = br.ReadString();
                                if (!ValidNonce(nonce) || guestCallsign.Length > 48 || ms.Position != ms.Length
                                    || (!firstConnection && nonce != clientNonce)) return;
                                if (firstConnection)
                                {
                                    clientNonce = nonce;
                                    sessionToken = Guid.NewGuid().ToString("N");
                                }
                                RemoteEndPoint = sender;
                                RemoteCallsign = guestCallsign;
                                IsConnected = true;

                                // Reply with Welcome packet containing match parameters
                                int seed = UnityEngine.Random.Range(1000, 999999);
                                SendWelcome(seed);

                                if (firstConnection) OnConnected?.Invoke(RemoteCallsign);
                            }
                            break;

                        case DuelPacketType.Welcome:
                            if (!IsHost)
                            {
                                bool firstWelcome = !IsConnected;
                                DuelPacketCodec.DeserializeWelcome(br, out string hostCallsign, out double lat, out double lon, out int roundTarget, out int seed);
                                string nonce = br.ReadString(), token = br.ReadString();
                                if (nonce != clientNonce || !ValidNonce(token) || hostCallsign.Length > 48 || ms.Position != ms.Length
                                    || (!firstWelcome && token != sessionToken)) return;
                                sessionToken = token;
                                RemoteEndPoint = sender;
                                RemoteCallsign = hostCallsign;
                                HostLatitude = lat; HostLongitude = lon;
                                IsConnected = true;

                                if (firstWelcome) OnConnected?.Invoke(RemoteCallsign);
                            }
                            break;

                        case DuelPacketType.MapChunk:
                            if (!IsConnected || IsHost || SharedMapData != null) return;
                            int totalBytes = br.ReadInt32(), offset = br.ReadInt32();
                            int chunkBytes = (int)(ms.Length - ms.Position);
                            if (totalBytes <= 0 || totalBytes > MaxMapBytes || offset != mapReceiveOffset
                                || chunkBytes <= 0 || chunkBytes > 900 || offset > totalBytes - chunkBytes)
                                throw new InvalidDataException("Invalid shared map chunk.");
                            if (receivingMap == null) receivingMap = new byte[totalBytes];
                            if (receivingMap.Length != totalBytes) throw new InvalidDataException("Shared map size changed.");
                            var chunk = br.ReadBytes(chunkBytes);
                            Buffer.BlockCopy(chunk, 0, receivingMap, offset, chunkBytes);
                            mapReceiveOffset += chunkBytes;
                            if (mapReceiveOffset == totalBytes) { SharedMapData = receivingMap; receivingMap = null; }
                            break;

                        case DuelPacketType.Ping:
                            if (!IsConnected) return;
                            long clientTicks = DuelPacketCodec.DeserializePingPong(br);
                            byte[] pong = DuelPacketCodec.SerializePong(clientTicks);
                            SendRaw(pong);
                            break;

                        case DuelPacketType.Pong:
                            if (!IsConnected) return;
                            long origTicks = DuelPacketCodec.DeserializePingPong(br);
                            long elapsed = DateTime.UtcNow.Ticks - origTicks;
                            PingMs = Mathf.Clamp((int)(elapsed / TimeSpan.TicksPerMillisecond), 1, 999);
                            break;

                        case DuelPacketType.Disconnect:
                            Disconnect(br.BaseStream.Position < br.BaseStream.Length ? "Rival ended duel: " + br.ReadString() : "Opponent disconnected.");
                            break;

                        default:
                            if (!IsConnected || IsReconnecting) return;
                            OnPacketReceived?.Invoke(type, br);
                            break;
                    }
                    lastPacketTime = Time.unscaledTime;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is FormatException)
            {
                Debug.LogWarning("[DuelNetwork] Ignored malformed packet: " + ex.Message);
            }
        }

        public void Disconnect(string reason = "")
        {
            LastDisconnectReason = string.IsNullOrWhiteSpace(reason) ? "Opponent left the duel." : reason;
            Debug.LogWarning("[DuelNetwork] Disconnect: " + LastDisconnectReason);
            if (IsConnected && RemoteEndPoint != null)
            {
                try
                {
                    byte[] dc = DuelPacketCodec.SerializeDisconnect(LastDisconnectReason);
                    SendRaw(dc);
                }
                catch { }
            }

            IsConnected = false;
            IsHost = false;
            RemoteEndPoint = null;
            CloseSockets();
            OnDisconnected?.Invoke();
        }

        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        private void CloseSockets()
        {
            CloseRelay();
            IsReconnecting = false;
            clientNonce = sessionToken = reconnectNonce = peerReconnectNonce = "";
            completedPeerReconnectNonces.Clear();
            reconnectPhase = 0;
            localPauseSendThrough = peerPauseSendThrough = 0;
            gameplayStarted = false;
            CompetitiveSpotting = false;
            SharedMapData = receivingMap = null;
            mapSendOffset = mapReceiveOffset = 0;
            HostLatitude = HostLongitude = 0;
            ArenaRound = ReadyRound = EngageRound = 0;
            pendingReliable.Clear();
            orderedIncoming.Clear();
            nextSendId = nextReceiveId = 1;
            while (incomingQueue.TryDequeue(out _)) { }
            IsConnected = false;
            IsHost = false;
            RemoteEndPoint = null;
            RemoteCallsign = "RIVAL";
            RoomCode = PublicEndpointString = "";
            PingMs = 0;
            cancelTokenSource?.Cancel();
            try { socket?.Close(); } catch { }
            socket = null;
            StopLANDiscovery();
        }

        private void OnDestroy()
        {
            CloseSockets();
        }

        private void OnApplicationQuit()
        {
            CloseSockets();
        }

        // ─── LAN Room Code Discovery ──────────────────────────────────
        private string GenerateRoomCode(int port)
        {
            string localIp = GetLocalIPAddress();
            var parts = localIp.Split('.');
            if (parts.Length == 4)
            {
                int b3 = int.Parse(parts[2]);
                int b4 = int.Parse(parts[3]);
                return port == DefaultPort ? $"D-{b3:X2}{b4:X2}" : EncodeEndpointToRoomCode(IPAddress.Parse(localIp), port);
            }
            return "SNIPER-1";
        }

        private string EncodeEndpointToRoomCode(IPAddress ip, int port)
        {
            byte[] ipBytes = ip.GetAddressBytes();
            uint ipVal = (uint)((ipBytes[0] << 24) | (ipBytes[1] << 16) | (ipBytes[2] << 8) | ipBytes[3]);
            ulong combined = ((ulong)ipVal << 16) | (ushort)port;
            return "X-" + ConvertToBase36(combined);
        }

        public static IPEndPoint ResolveTarget(string input, int defaultPort)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            input = input.Trim();

            // Direct localhost check
            if (string.Equals(input, "localhost", StringComparison.OrdinalIgnoreCase) || input == "127.0.0.1")
            {
                return new IPEndPoint(IPAddress.Loopback, defaultPort);
            }

            // Check if room code format "D-XXXX" (Local Subnet code)
            string upper = input.ToUpperInvariant();
            if (upper.StartsWith("D-") && upper.Length == 6)
            {
                try
                {
                    string hex = upper.Substring(2);
                    int b3 = Convert.ToInt32(hex.Substring(0, 2), 16);
                    int b4 = Convert.ToInt32(hex.Substring(2, 2), 16);
                    string localSubnet = GetLocalSubnetPrefix();
                    return new IPEndPoint(IPAddress.Parse($"{localSubnet}.{b3}.{b4}"), defaultPort);
                }
                catch { }
            }

            if (upper.StartsWith("X-"))
            {
                try
                {
                    ulong value = 0;
                    foreach (char c in upper.Substring(2))
                    {
                        int digit = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".IndexOf(c);
                        if (digit < 0) return null;
                        value = checked(value * 36 + (uint)digit);
                    }
                    if (value > 0xFFFFFFFFFFFF || (value & 0xFFFF) == 0) return null;
                    uint address = (uint)(value >> 16);
                    return new IPEndPoint(new IPAddress(new byte[] {(byte)(address >> 24), (byte)(address >> 16), (byte)(address >> 8), (byte)address}), (int)(value & 0xFFFF));
                }
                catch (OverflowException) { return null; }
            }

            // Direct IP with or without port: "192.168.1.5:7777" or "192.168.1.5"
            string[] hostParts = input.Split(':');
            string host = hostParts[0];
            int p = hostParts.Length > 1 && int.TryParse(hostParts[1], out int parsedPort) ? parsedPort : defaultPort;

            if (p < 1 || p > 65535) return null;
            if (IPAddress.TryParse(host, out var ip))
            {
                return new IPEndPoint(ip, p);
            }

            try
            {
                var addresses = Dns.GetHostAddresses(host);
                foreach (var addr in addresses)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                        return new IPEndPoint(addr, p);
                }
                if (addresses.Length > 0) return new IPEndPoint(addresses[0], p);
            }
            catch { }

            return null;
        }

        public static string GetLocalIPAddress()
        {
            try
            {
                foreach(var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if(adapter.OperationalStatus!=System.Net.NetworkInformation.OperationalStatus.Up)continue;
                    foreach(var address in adapter.GetIPProperties().UnicastAddresses)
                        if(address.Address.AddressFamily==AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))return address.Address.ToString();
                }
                return "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private static string GetLocalSubnetPrefix()
        {
            string ip = GetLocalIPAddress();
            string[] parts = ip.Split('.');
            if (parts.Length >= 2)
            {
                return $"{parts[0]}.{parts[1]}";
            }
            return "192.168";
        }

        private static string ConvertToBase36(ulong val)
        {
            const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            if (val == 0) return "0";
            var sb = new StringBuilder();
            while (val > 0)
            {
                sb.Insert(0, chars[(int)(val % 36)]);
                val /= 36;
            }
            return sb.ToString();
        }
    }
}
