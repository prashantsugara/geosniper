using GeoSniper.Duel;
using UnityEngine;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
}

string invite = DuelInviteShare.BuildInvite("d-01af", "SPECTRE-01");
Check(invite.Contains(DuelInviteShare.PlayStoreUrl) && invite.Contains("Join code: D-01AF"), "Invite includes the confirmed Play Store URL and actual normalized code");
Check(invite.Contains("same Wi-Fi") && invite.Contains("host must keep the room open"), "Local room invite explains host availability and network access");
Check(DuelInviteShare.TryExtractJoinTarget(invite, out string code) && code == "D-01AF", "Recipient can paste a complete shared invitation");
Check(DuelInviteShare.TryExtractJoinTarget(invite.Replace("\n", "\r\n"), out code) && code == "D-01AF", "Invitation copied through Windows messages still parses");
Check(DuelInviteShare.TryExtractJoinTarget("  d-CAFE  ", out code) && code == "D-CAFE", "Direct room code accepts whitespace and lowercase");
Check(DuelInviteShare.TryExtractJoinTarget("192.168.1.4:7777", out code) && code == "192.168.1.4:7777", "Direct host endpoint remains available");
Check(DuelInviteShare.TryExtractJoinTarget("localhost:7777", out code) && code == "localhost:7777", "Editor localhost testing remains available");
Check(DuelInviteShare.TryExtractJoinTarget("duel.example.org:7777", out code) && code == "duel.example.org:7777", "Existing direct DNS host path remains available");
Check(DuelInviteShare.TryExtractJoinTarget("Join code: X-22FUJNUIKF\nAnother line", out code) && code == "X-22FUJNUIKF", "Encoded endpoint invitation is parsed and bounded");
foreach (string invalid in new[] { "", " ", "D-FFFFF", "D-ZZZZ", "X-!", "X-0", "X-ZZZZZZZZZZZZZZZZZZZ", "192.168.1.4:70000", "192.168.1.4:noport", "192.999.1.4", DuelInviteShare.PlayStoreUrl, "Meet me tomorrow" })
    Check(!DuelInviteShare.TryExtractJoinTarget(invalid, out _), "Malformed target rejected: " + (invalid.Length == 0 ? "empty" : invalid));
bool rejected = false;
try { DuelInviteShare.BuildInvite("INITIALIZING...", "Host"); }
catch (ArgumentException) { rejected = true; }
Check(rejected, "An initializing room cannot create an unusable invitation");
GUIUtility.systemCopyBuffer = "previous clipboard";
string notice = DuelInviteShare.Share("", "Host");
Check(GUIUtility.systemCopyBuffer == "previous clipboard" && notice.Contains("starting"), "Sharing with no live code preserves the clipboard and gives a clear notice");
notice = DuelInviteShare.Share("D-01AF", "SPECTRE-01");
Check(GUIUtility.systemCopyBuffer == invite && notice.Contains("copied"), "Editor share button copies the complete invitation and reports the action");
