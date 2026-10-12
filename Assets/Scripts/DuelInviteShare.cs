using System;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GeoSniper.Duel
{
    public static class DuelInviteShare
    {
        public const string PlayStoreUrl = "https://play.google.com/store/apps/details?id=com.geosniper.game";
        private static readonly Regex InviteCode = new Regex(@"(?im)^\s*(?:join\s*code|room\s*code|host\s*code)\s*:\s*(\S+)", RegexOptions.CultureInvariant);

        public static string BuildInvite(string roomCode, string callsign)
        {
            if (!TryNormalizeTarget(roomCode, out string target) || !(target.StartsWith("R-", StringComparison.Ordinal) || target.StartsWith("D-", StringComparison.Ordinal) || target.StartsWith("X-", StringComparison.Ordinal)))
                throw new ArgumentException("A live host room code is required.", nameof(roomCode));
            string name = string.IsNullOrWhiteSpace(callsign) ? "A friend" : callsign.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (name.Length > 40) name = name.Substring(0, 40);
            string connection = target.StartsWith("R-",StringComparison.Ordinal) ? "Connect from any Wi-Fi or mobile network using Unity Relay.\n" : IsLocalTarget(target) ? "Use the same Wi-Fi or hotspot as the host.\n" : "The host's network must be reachable.\n";
            return "Join " + name + " for a Geo Sniper 1v1 duel!\n\n"
                + "Get Geo Sniper:\n" + PlayStoreUrl + "\n\n"
                + "Join code: " + (target.StartsWith("R-",StringComparison.Ordinal)?SniperDuelNetwork.NumericRoomCode(target):target) + "\n"
                + "Open Geo Sniper > 1v1 Duel > JOIN DUEL ROOM, then paste this invite or enter the code.\n"
                + connection + "The host must keep the room open until you join.";
        }

        public static bool TryExtractJoinTarget(string input, out string target)
        {
            target = "";
            if (string.IsNullOrWhiteSpace(input) || input.Length > 4096) return false;
            if (TryNormalizeTarget(input, out target)) return true;
            Match match = InviteCode.Match(input);
            return match.Success && TryNormalizeTarget(match.Groups[1].Value, out target);
        }

        private static bool TryNormalizeTarget(string input, out string target)
        {
            target = "";
            if (string.IsNullOrWhiteSpace(input)) return false;
            string candidate = input.Trim();
            if (candidate.Length > 253 || Regex.IsMatch(candidate, @"\s") || candidate.Contains("/") || candidate.Contains("?") || candidate.Contains("#")) return false;
            string upper = candidate.ToUpperInvariant();
            if(SniperDuelNetwork.TryRelayCode(upper,out string relayCode)){target="R-"+relayCode;return true;}
            if (upper.StartsWith("D-"))
            {
                if (!Regex.IsMatch(upper, @"^D-[0-9A-F]{4}$")) return false;
                target = upper; return true;
            }
            if (upper.StartsWith("X-"))
            {
                if (!TryDecodeEndpoint(upper, out _, out _)) return false;
                target = upper; return true;
            }
            string[] parts = candidate.Split(':');
            if (parts.Length > 2 || (parts.Length == 2 && (!int.TryParse(parts[1], out int port) || port < 1 || port > 65535))) return false;
            string host = parts[0];
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) { target = candidate.ToLowerInvariant(); return true; }
            if (IPAddress.TryParse(host, out IPAddress address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            { target = address + (parts.Length == 2 ? ":" + int.Parse(parts[1]) : ""); return true; }
            if (Regex.IsMatch(host, @"^\d+(?:\.\d+){3}$")) return false;
            // Retain the direct hostname path without doing blocking DNS work in OnGUI.
            if (!host.Contains(".") || Uri.CheckHostName(host) != UriHostNameType.Dns) return false;
            target = candidate; return true;
        }

        private static bool TryDecodeEndpoint(string code, out IPAddress address, out int port)
        {
            address = null; port = 0;
            if (code.Length < 3 || code.Length > 12) return false;
            try
            {
                ulong value = 0;
                foreach (char c in code.Substring(2))
                {
                    int digit = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".IndexOf(c);
                    if (digit < 0) return false;
                    value = checked(value * 36 + (uint)digit);
                }
                if (value > 0xFFFFFFFFFFFF || (value & 0xFFFF) == 0) return false;
                uint ip = (uint)(value >> 16);
                address = new IPAddress(new byte[] { (byte)(ip >> 24), (byte)(ip >> 16), (byte)(ip >> 8), (byte)ip });
                port = (int)(value & 0xFFFF);
                return true;
            }
            catch (OverflowException) { return false; }
        }

        private static bool IsLocalTarget(string target)
        {
            if (target.StartsWith("D-", StringComparison.Ordinal) || target.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)) return true;
            IPAddress address;
            if (target.StartsWith("X-", StringComparison.Ordinal))
            { if (!TryDecodeEndpoint(target, out address, out _)) return false; }
            else if (!IPAddress.TryParse(target.Split(':')[0], out address)) return false;
            byte[] bytes = address.GetAddressBytes();
            return bytes.Length == 4 && (bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 192 && bytes[1] == 168
                || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31 || bytes[0] == 169 && bytes[1] == 254);
        }

        public static string Share(string roomCode, string callsign)
        {
            string invite;
            try { invite = BuildInvite(roomCode, callsign); }
            catch (ArgumentException) { return "The room is still starting. Wait for its code."; }
            GUIUtility.systemCopyBuffer = invite;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    {
                        try
                        {
                            using (var owner = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                            using (var current = owner.GetStatic<AndroidJavaObject>("currentActivity"))
                            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                            using (var send = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
                            {
                                using (send.Call<AndroidJavaObject>("setType", "text/plain")) { }
                                using (send.Call<AndroidJavaObject>("putExtra", "android.intent.extra.SUBJECT", "Geo Sniper 1v1 invite")) { }
                                using (send.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", invite)) { }
                                using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", send, "Share duel invite"))
                                    current.Call("startActivity", chooser);
                            }
                        }
                        catch (AndroidJavaException) { Debug.LogWarning("Could not open Android sharing. The duel invite is copied to the clipboard."); }
                    }));
                return "Invite copied. Choose an app to share it.";
            }
            catch (AndroidJavaException) { return "Invite copied. Paste it into a message."; }
#else
            return "Invite copied. Paste it into a message.";
#endif
        }
    }
}
