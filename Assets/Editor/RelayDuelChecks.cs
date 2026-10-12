using System;
using System.Reflection;
using GeoSniper.Duel;
using UnityEditor;
using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using System.Net;

public static class RelayDuelChecks
{
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static GameObject hostRoot,guestRoot;
    static SniperDuelNetwork host,guest;
    static double deadline;
    static byte[] map;
    static bool joining;
    public static void Run()
    {
        foreach(var original in new[]{"000000","ZZZZZZ","ABC123","000001","123456"})
        {
            var numeric=SniperDuelNetwork.NumericRoomCode(original);
            if(numeric.Length!=10||!SniperDuelNetwork.TryRelayCode(numeric,out var decoded)||decoded!=original)
                throw new Exception("Numeric room code round trip failed");
            if(!DuelInviteShare.TryExtractJoinTarget(DuelInviteShare.BuildInvite(numeric,"HOST"),out var parsed)||parsed!="R-"+original)
                throw new Exception("Numeric invite round trip failed");
        }
        foreach(var invalid in new[]{"2176782336","9999999999","00000000-1","00000000001"})
            if(SniperDuelNetwork.TryRelayCode(invalid,out _))throw new Exception("Invalid numeric code accepted");
        foreach(var input in new[]{"ABC123","r-abc123","  ABC123  "})
        {
            if(!DuelInviteShare.TryExtractJoinTarget(input,out var code)||code!="R-ABC123")throw new Exception("Relay code normalization failed");
        }
        foreach(var input in new[]{"R-", "R-ABC12", "R-ABC1234", "R-ABC!23"})
            if(SniperDuelNetwork.TryRelayCode(input,out _))throw new Exception("Malformed Relay code accepted");
        var invite=DuelInviteShare.BuildInvite("R-ABC123","HOST");
        if(!DuelInviteShare.TryExtractJoinTarget(invite,out var target)||target!="R-ABC123"||invite.Contains("same Wi-Fi"))throw new Exception("Internet invite round trip failed");
        var root=new GameObject("Inactive Relay validation");root.SetActive(false);
        try
        {
            var network=root.AddComponent<SniperDuelNetwork>();
            if(network.StartClient("192.168.1.2:7777")||network.IsConnecting)throw new Exception("Direct IP accepted as an internet Relay room");
            typeof(SniperDuelNetwork).GetField("relayStarting",Private).SetValue(network,true);
            typeof(SniperDuelNetwork).GetField("usingRelay",Private).SetValue(network,true);
            if(!network.IsConnecting)throw new Exception("Guest allocation does not keep loading alive");
            int epoch=(int)typeof(SniperDuelNetwork).GetField("relayEpoch",Private).GetValue(network);
            network.PrepareToJoin();
            if(network.IsConnecting||network.IsRelayStarting||(int)typeof(SniperDuelNetwork).GetField("relayEpoch",Private).GetValue(network)==epoch)
                throw new Exception("Room cancellation leaves async startup alive");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        Debug.Log("PASS: Relay codes, pasted invites, direct-IP rejection, allocation loading and cancellation");
        RunLocalTransport();
    }
    static void Set(object obj,string name,object value)
    {
        var field=obj.GetType().GetField(name,Private)??obj.GetType().GetField("<"+name+">k__BackingField",Private);
        field.SetValue(obj,value);
    }
    static NetworkDriver Driver(out NetworkPipeline pipeline)
    {
        var settings=new NetworkSettings(Allocator.Temp);
        try{settings.WithFragmentationStageParameters(payloadCapacity:2048);var driver=NetworkDriver.Create(settings);
            pipeline=driver.CreatePipeline(typeof(FragmentationPipelineStage));return driver;}
        finally{settings.Dispose();}
    }
    static void RunLocalTransport()
    {
        var h=new GameObject("Local transport host");h.SetActive(false);
        var g=new GameObject("Local transport guest");g.SetActive(false);
        try
        {
            var hn=h.AddComponent<SniperDuelNetwork>();var gn=g.AddComponent<SniperDuelNetwork>();
            hn.LocalCallsign="HOST";gn.LocalCallsign="GUEST";
            var hd=Driver(out var hp);Set(hn,"relayDriver",hd);Set(hn,"relayPipeline",hp);Set(hn,"usingRelay",true);Set(hn,"IsHost",true);
            if(hd.Bind(NetworkEndpoint.LoopbackIpv4)!=0||hd.Listen()!=0)throw new Exception("Local host bind failed");
            var gd=Driver(out var gp);Set(gn,"relayDriver",gd);Set(gn,"relayPipeline",gp);Set(gn,"usingRelay",true);
            gd.Bind(NetworkEndpoint.AnyIpv4);
            Set(gn,"RemoteEndPoint",new IPEndPoint(IPAddress.Loopback,SniperDuelNetwork.DefaultPort));
            Set(gn,"clientNonce",Guid.NewGuid().ToString("N"));Set(gn,"relayPeer",gd.Connect(hd.GetLocalEndpoint()));
            var bytes=new byte[65536];new System.Random(456).NextBytes(bytes);hn.SetHostMap(bytes,28.6,77.2);
            var update=typeof(SniperDuelNetwork).GetMethod("Update",Private);var end=DateTime.UtcNow.AddSeconds(10);
            while(DateTime.UtcNow<end && gn.SharedMapData==null)
            {update.Invoke(hn,null);update.Invoke(gn,null);System.Threading.Thread.Sleep(1);}
            if(!hn.IsConnected||!gn.IsConnected||gn.SharedMapData==null||hn.RemoteCallsign!="GUEST"||gn.RemoteCallsign!="HOST")
                throw new Exception("Transport bridge handshake or map transfer failed");
            for(int i=0;i<bytes.Length;i++)if(gn.SharedMapData[i]!=bytes[i])throw new Exception("Transport map payload corrupted");
            Debug.Log("PASS: Local Unity Transport bridge, two-peer session handshake and reliable 64 KiB map transfer");
        }
        finally{
            typeof(SniperDuelNetwork).GetMethod("CloseSockets",Private).Invoke(h.GetComponent<SniperDuelNetwork>(),null);
            typeof(SniperDuelNetwork).GetMethod("CloseSockets",Private).Invoke(g.GetComponent<SniperDuelNetwork>(),null);
            UnityEngine.Object.DestroyImmediate(h);UnityEngine.Object.DestroyImmediate(g);
        }
    }
    // Creates a temporary two-player Relay allocation in the linked project, using real DTLS.
    public static void RunLive()
    {
        Run();
        hostRoot=new GameObject("Relay smoke host");hostRoot.SetActive(false);
        guestRoot=new GameObject("Relay smoke guest");guestRoot.SetActive(false);
        host=hostRoot.AddComponent<SniperDuelNetwork>();guest=guestRoot.AddComponent<SniperDuelNetwork>();
        host.LocalCallsign="RELAY CHECK HOST";guest.LocalCallsign="RELAY CHECK GUEST";
        map=new byte[65536];new System.Random(123).NextBytes(map);
        joining=false;deadline=EditorApplication.timeSinceStartup+120;
        EditorApplication.update+=Tick;
        host.StartHost();
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Live Relay smoke test timed out");
            typeof(SniperDuelNetwork).GetMethod("Update",Private).Invoke(host,null);
            typeof(SniperDuelNetwork).GetMethod("Update",Private).Invoke(guest,null);
            if(host.LastDisconnectReason.Length>0||guest.LastDisconnectReason.Length>0)throw new Exception(host.LastDisconnectReason+guest.LastDisconnectReason);
            if(!joining && host.RoomCode.Length>0)
            {joining=true;host.SetHostMap(map,28.6,77.2);guest.StartClient(host.RoomCode);}
            if(host.IsConnected && guest.IsConnected && guest.SharedMapData!=null)
            {
                if(host.RemoteCallsign!=guest.LocalCallsign||guest.RemoteCallsign!=host.LocalCallsign||guest.SharedMapData.Length!=map.Length)
                    throw new Exception("Relay handshake/map mismatch");
                for(int n=0;n<map.Length;n++)if(map[n]!=guest.SharedMapData[n])throw new Exception("Relay map corrupted");
                Debug.Log("PASS: Live Unity Relay DTLS binding, two-peer Hello/Welcome handshake, reliable 64 KiB map transfer");
                Finish(0);
            }
        }
        catch(Exception error){Debug.LogError("Relay validation: "+error);Finish(1);}
    }
    static void Finish(int code)
    {
        EditorApplication.update-=Tick;
        if(host!=null)typeof(SniperDuelNetwork).GetMethod("CloseSockets",Private).Invoke(host,null);
        if(guest!=null)typeof(SniperDuelNetwork).GetMethod("CloseSockets",Private).Invoke(guest,null);
        if(hostRoot!=null)UnityEngine.Object.DestroyImmediate(hostRoot);
        if(guestRoot!=null)UnityEngine.Object.DestroyImmediate(guestRoot);
        EditorApplication.Exit(code);
    }
}
