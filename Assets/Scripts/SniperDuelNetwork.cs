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

    public sealed class SniperDuelNetwork : MonoBehaviour
    {
        public static SniperDuelNetwork Instance { get; private set; }

        public const int DefaultPort = 7777;
        public const int BeaconPort = 7776;

        public bool IsHost { get; private set; }
        public bool IsConnected { get; private set; }
        public string LocalCallsign { get; set; } = "OPERATOR";
        public string RemoteCallsign { get; private set; } = "RIVAL";
        public string RoomCode { get; private set; } = "";
        public string PublicEndpointString { get; private set; } = "";
        public int PingMs { get; private set; } = 0;

        public IPEndPoint RemoteEndPoint { get; private set; }

        private UdpClient socket;
        private UdpClient beaconSocket;
        private CancellationTokenSource cancelTokenSource;

        private readonly ConcurrentQueue<ReceivedPacket> incomingQueue = new ConcurrentQueue<ReceivedPacket>();
        public readonly List<DiscoveredHost> DiscoveredLANHosts = new List<DiscoveredHost>();

        public event Action<DuelPacketType, BinaryReader> OnPacketReceived;
        public event Action<string> OnConnected;
        public event Action OnDisconnected;

        private float lastBeaconTime;
        private float lastPingTime;
        private float lastPacketTime;
        private const float ConnectionTimeout = 8f;

        private struct ReceivedPacket
        {
            public byte[] data;
            public IPEndPoint sender;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LocalCallsign = PlayerPrefs.GetString("GeoSniper.PlayerName", "SPECTRE-01").ToUpperInvariant();
        }

        private void Update()
        {
            // Process queued network packets on the main Unity thread
            while (incomingQueue.TryDequeue(out var item))
            {
                ProcessPacket(item.data, item.sender);
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

            // Ping keep-alive when connected
            if (IsConnected && RemoteEndPoint != null)
            {
                if (Time.unscaledTime - lastPingTime > 1.0f)
                {
                    lastPingTime = Time.unscaledTime;
                    long nowTicks = DateTime.UtcNow.Ticks;
                    byte[] ping = DuelPacketCodec.SerializePing(nowTicks);
                    SendRaw(ping);
                }

                // Check for timeout
                if (Time.unscaledTime - lastPacketTime > ConnectionTimeout)
                {
                    Disconnect("Connection timed out.");
                }
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

        public bool StartHost(int port = DefaultPort)
        {
            CloseSockets();
            cancelTokenSource = new CancellationTokenSource();
            try
            {
                socket = new UdpClient(port);
                socket.EnableBroadcast = true;
                IsHost = true;
                IsConnected = false;
                RemoteEndPoint = null;
                lastPacketTime = Time.unscaledTime;

                // Generate a local room code based on port and random salt
                RoomCode = GenerateRoomCode(port);

                // Run STUN lookup in background to determine public IP & Port
                Task.Run(() => QueryGoogleSTUN(port));

                // Start async packet receive loop
                BeginReceive();

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DuelNetwork] Failed to start host: " + ex.Message);
                CloseSockets();
                return false;
            }
        }

        public bool StartClient(string targetHostOrCode, int port = DefaultPort)
        {
            CloseSockets();
            cancelTokenSource = new CancellationTokenSource();
            try
            {
                socket = new UdpClient(0); // auto bind ephemeral port
                socket.EnableBroadcast = true;
                IsHost = false;
                IsConnected = false;
                lastPacketTime = Time.unscaledTime;

                IPEndPoint targetEP = ResolveTarget(targetHostOrCode, port);
                if (targetEP == null)
                {
                    Debug.LogWarning("[DuelNetwork] Cannot resolve target: " + targetHostOrCode);
                    CloseSockets();
                    return false;
                }

                RemoteEndPoint = targetEP;
                BeginReceive();

                // Send initial Hello handshake packet
                int weaponIndex = PlayerPrefs.GetInt("GeoSniper.SelectedWeapon", 0);
                byte[] hello = DuelPacketCodec.SerializeHello(LocalCallsign, weaponIndex);
                SendRaw(hello);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DuelNetwork] Failed to start client: " + ex.Message);
                CloseSockets();
                return false;
            }
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
            if (RemoteEndPoint == null || socket == null) return;
            SendRaw(data);
        }

        private void SendRaw(byte[] data)
        {
            if (socket == null || RemoteEndPoint == null) return;
            try
            {
                socket.BeginSend(data, data.Length, RemoteEndPoint, null, null);
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
                socket.BeginReceive(OnReceiveCallback, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DuelNetwork] BeginReceive error: " + ex.Message);
            }
        }

        private void OnReceiveCallback(IAsyncResult ar)
        {
            if (socket == null || cancelTokenSource == null || cancelTokenSource.IsCancellationRequested) return;
            try
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = socket.EndReceive(ar, ref sender);
                if (data != null && data.Length > 0)
                {
                    incomingQueue.Enqueue(new ReceivedPacket { data = data, sender = sender });
                }
                BeginReceive();
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Debug.LogWarning("[DuelNetwork] Receive callback error: " + ex.Message);
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
                if (data != null && data.Length > 0)
                {
                    string text = Encoding.UTF8.GetString(data);
                    if (text.StartsWith("GEOSNIPER_HOST:"))
                    {
                        var parts = text.Split(':');
                        if (parts.Length >= 5)
                        {
                            string callsign = parts[1];
                            int port = int.Parse(parts[2]);
                            double lat = double.Parse(parts[3]);
                            double lon = double.Parse(parts[4]);

                            IPEndPoint hostEP = new IPEndPoint(sender.Address, port);
                            UpdateDiscoveredHost(callsign, hostEP, lat, lon);
                        }
                    }
                }
                BeginBeaconReceive();
            }
            catch { }
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
                double lat = 35.6762;
                double lon = 139.6503;
                string msg = $"GEOSNIPER_HOST:{LocalCallsign}:{DefaultPort}:{lat:F4}:{lon:F4}";
                byte[] bytes = Encoding.UTF8.GetBytes(msg);
                IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, BeaconPort);
                socket.BeginSend(bytes, bytes.Length, broadcastEP, null, null);
            }
            catch { }
        }

        private void ProcessPacket(byte[] data, IPEndPoint sender)
        {
            if (data == null || data.Length < 2) return;
            if (data[0] != DuelPacketCodec.ProtocolMagic) return;

            lastPacketTime = Time.unscaledTime;
            DuelPacketType type = (DuelPacketType)data[1];

            using (var ms = new MemoryStream(data, 2, data.Length - 2))
            using (var br = new BinaryReader(ms))
            {
                switch (type)
                {
                    case DuelPacketType.Hello:
                        if (IsHost && !IsConnected)
                        {
                            RemoteEndPoint = sender;
                            DuelPacketCodec.DeserializeHello(br, out string guestCallsign, out int guestWeapon);
                            RemoteCallsign = guestCallsign;
                            IsConnected = true;

                            // Reply with Welcome packet containing match parameters
                            int seed = UnityEngine.Random.Range(1000, 999999);
                            byte[] welcome = DuelPacketCodec.SerializeWelcome(LocalCallsign, 35.6762, 139.6503, 3, seed);
                            SendRaw(welcome);

                            OnConnected?.Invoke(RemoteCallsign);
                        }
                        break;

                    case DuelPacketType.Welcome:
                        if (!IsHost && !IsConnected)
                        {
                            RemoteEndPoint = sender;
                            DuelPacketCodec.DeserializeWelcome(br, out string hostCallsign, out double lat, out double lon, out int roundTarget, out int seed);
                            RemoteCallsign = hostCallsign;
                            IsConnected = true;

                            OnConnected?.Invoke(RemoteCallsign);
                        }
                        break;

                    case DuelPacketType.Ping:
                        long clientTicks = DuelPacketCodec.DeserializePingPong(br);
                        byte[] pong = DuelPacketCodec.SerializePong(clientTicks);
                        SendRaw(pong);
                        break;

                    case DuelPacketType.Pong:
                        long origTicks = DuelPacketCodec.DeserializePingPong(br);
                        long elapsed = DateTime.UtcNow.Ticks - origTicks;
                        PingMs = Mathf.Clamp((int)(elapsed / TimeSpan.TicksPerMillisecond), 1, 999);
                        break;

                    case DuelPacketType.Disconnect:
                        Disconnect("Opponent disconnected.");
                        break;

                    default:
                        OnPacketReceived?.Invoke(type, br);
                        break;
                }
            }
        }

        public void Disconnect(string reason = "")
        {
            if (IsConnected && RemoteEndPoint != null)
            {
                try
                {
                    byte[] dc = DuelPacketCodec.SerializeSimple(DuelPacketType.Disconnect);
                    SendRaw(dc);
                }
                catch { }
            }

            IsConnected = false;
            RemoteEndPoint = null;
            CloseSockets();
            OnDisconnected?.Invoke();
        }

        private void CloseSockets()
        {
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

        // ─── STUN & Room Code Discovery ──────────────────────────────────
        private async void QueryGoogleSTUN(int localPort)
        {
            try
            {
                using (var stunClient = new UdpClient())
                {
                    stunClient.Client.ReceiveTimeout = 3000;
                    var stunServer = new IPEndPoint(Dns.GetHostAddresses("stun.l.google.com")[0], 19302);

                    // RFC 5389 Binding Request
                    byte[] req = new byte[20];
                    req[0] = 0x00; req[1] = 0x01; // Binding Request
                    req[2] = 0x00; req[3] = 0x00; // Message Length = 0
                    req[4] = 0x21; req[5] = 0x12; req[6] = 0xA4; req[7] = 0x42; // Magic Cookie

                    // Transaction ID (12 bytes)
                    var rnd = new System.Random();
                    for (int i = 8; i < 20; i++) req[i] = (byte)rnd.Next(256);

                    await stunClient.SendAsync(req, req.Length, stunServer);
                    var resp = await stunClient.ReceiveAsync();
                    if (resp.Buffer.Length >= 20 && resp.Buffer[0] == 0x01 && resp.Buffer[1] == 0x01)
                    {
                        // Parse XOR-MAPPED-ADDRESS (0x0020)
                        int offset = 20;
                        while (offset + 4 <= resp.Buffer.Length)
                        {
                            ushort attrType = (ushort)((resp.Buffer[offset] << 8) | resp.Buffer[offset + 1]);
                            ushort attrLen = (ushort)((resp.Buffer[offset + 2] << 8) | resp.Buffer[offset + 3]);
                            offset += 4;

                            if (attrType == 0x0020 && offset + attrLen <= resp.Buffer.Length)
                            {
                                int port = ((resp.Buffer[offset + 2] ^ 0x21) << 8) | (resp.Buffer[offset + 3] ^ 0x12);
                                byte[] ipBytes = new byte[4];
                                ipBytes[0] = (byte)(resp.Buffer[offset + 4] ^ 0x21);
                                ipBytes[1] = (byte)(resp.Buffer[offset + 5] ^ 0x12);
                                ipBytes[2] = (byte)(resp.Buffer[offset + 6] ^ 0xA4);
                                ipBytes[3] = (byte)(resp.Buffer[offset + 7] ^ 0x42);
                                var pubIp = new IPAddress(ipBytes);

                                PublicEndpointString = $"{pubIp}:{port}";
                                RoomCode = EncodeEndpointToRoomCode(pubIp, port);
                                break;
                            }
                            offset += attrLen;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[STUN] Discovery: " + ex.Message);
            }
        }

        private string GenerateRoomCode(int port)
        {
            string localIp = GetLocalIPAddress();
            var parts = localIp.Split('.');
            if (parts.Length == 4)
            {
                int b3 = int.Parse(parts[2]);
                int b4 = int.Parse(parts[3]);
                return $"D-{b3:X2}{b4:X2}";
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
            input = input.Trim().ToUpperInvariant();

            // Check if room code format "D-XXXX" (Local Subnet code)
            if (input.StartsWith("D-") && input.Length >= 6)
            {
                try
                {
                    string hex = input.Substring(2);
                    int b3 = Convert.ToInt32(hex.Substring(0, 2), 16);
                    int b4 = Convert.ToInt32(hex.Substring(2, 2), 16);
                    string localSubnet = GetLocalSubnetPrefix();
                    return new IPEndPoint(IPAddress.Parse($"{localSubnet}.{b3}.{b4}"), defaultPort);
                }
                catch { }
            }

            // Direct IP with or without port: "192.168.1.5:7777" or "192.168.1.5"
            string[] hostParts = input.Split(':');
            string host = hostParts[0];
            int p = hostParts.Length > 1 && int.TryParse(hostParts[1], out int parsedPort) ? parsedPort : defaultPort;

            if (IPAddress.TryParse(host, out var ip))
            {
                return new IPEndPoint(ip, p);
            }

            try
            {
                var addresses = Dns.GetHostAddresses(host);
                if (addresses.Length > 0) return new IPEndPoint(addresses[0], p);
            }
            catch { }

            return null;
        }

        public static string GetLocalIPAddress()
        {
            try
            {
                using (var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    sock.Connect("8.8.8.8", 65530);
                    var endPoint = sock.LocalEndPoint as IPEndPoint;
                    return endPoint?.Address.ToString() ?? "127.0.0.1";
                }
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private static string GetLocalSubnetPrefix()
        {
            string ip = GetLocalIPAddress();
            int idx = ip.LastIndexOf('.');
            if (idx > 0)
            {
                int firstIdx = ip.IndexOf('.');
                return ip.Substring(0, firstIdx); // returns 192 or similar
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
