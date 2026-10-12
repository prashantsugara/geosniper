using GeoSniper.Duel;
using System.IO;
using System.Net;
using System.Reflection;
using System.Diagnostics;
using UnityEngine;
static class Program {
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string message) {if(!ok) throw new Exception(message);Console.WriteLine("PASS: "+message);}
 static BinaryReader Read(byte[] data)=>new(new MemoryStream(data,2,data.Length-2));
 static void RawProcess(SniperDuelNetwork n,byte[] data,IPEndPoint sender)=>typeof(SniperDuelNetwork).GetMethod("ProcessPacket",Private).Invoke(n,new object[]{data,sender});
 static void Process(SniperDuelNetwork n,byte[] data,IPEndPoint sender)=>RawProcess(n,(byte[])typeof(SniperDuelNetwork).GetMethod("WrapSession",Private).Invoke(n,new object[]{data}),sender);
 static void Set(SniperDuelNetwork n,string field,object value)=>typeof(SniperDuelNetwork).GetField(field,Private).SetValue(n,value);
 static object Get(SniperDuelNetwork n,string field)=>typeof(SniperDuelNetwork).GetField(field,Private).GetValue(n);
 static byte[] Reconnect(string token,string nonce,uint next,byte phase=0,DuelPacketType type=DuelPacketType.Reconnect,uint cutoff=0) {using var ms=new MemoryStream();using var bw=new BinaryWriter(ms);bw.Write(DuelPacketCodec.ProtocolMagic);bw.Write((byte)type);bw.Write(DuelPacketCodec.ProtocolVersion);bw.Write(token);bw.Write(nonce);bw.Write(phase);bw.Write(next);bw.Write(cutoff);return ms.ToArray();}
 static byte[] Session(string token,byte[] payload) {using var ms=new MemoryStream();using var bw=new BinaryWriter(ms);bw.Write(DuelPacketCodec.ProtocolMagic);bw.Write((byte)DuelPacketType.Session);bw.Write(DuelPacketCodec.ProtocolVersion);bw.Write(token);bw.Write(payload);return ms.ToArray();}
 static void Tick(SniperDuelNetwork n)=>typeof(SniperDuelNetwork).GetMethod("Update",Private).Invoke(n,null);
 static byte[] Reliable(uint id,byte[] payload) {using var ms=new MemoryStream();using var bw=new BinaryWriter(ms);bw.Write(DuelPacketCodec.ProtocolMagic);bw.Write((byte)DuelPacketType.Reliable);bw.Write(id);bw.Write(payload);return ms.ToArray();}
 static void Main() {
  using(var br=Read(DuelPacketCodec.SerializeHello("GUEST",2))) {DuelPacketCodec.DeserializeHello(br,out var name,out var weapon);Check(name=="GUEST" && weapon==2,"Hello round-trip");}
  using(var br=Read(DuelPacketCodec.SerializeWelcome("HOST",35.6762,139.6503,3,12345))) {DuelPacketCodec.DeserializeWelcome(br,out var name,out var lat,out var lon,out var score,out var seed);Check(name=="HOST" && lat==35.6762 && lon==139.6503 && score==3 && seed==12345,"Welcome round-trip");}
  var bad=DuelPacketCodec.SerializeHello("OLD",0);bad[2]=1;
  bool rejected=false;try {using var br=Read(bad);DuelPacketCodec.DeserializeHello(br,out _,out _);}catch(InvalidDataException){rejected=true;}Check(rejected,"Unsupported protocol rejected");
  bad=DuelPacketCodec.SerializeHello("PRE-CONSENT",0);bad[2]=6;rejected=false;
  try {using var br=Read(bad);DuelPacketCodec.DeserializeHello(br,out _,out _);}catch(InvalidDataException){rejected=true;}
  Check(rejected && DuelPacketCodec.ProtocolVersion==7,"Pre-consent APK protocol cannot silently auto-agree to new rematches");
  foreach(DuelRematchAction action in Enum.GetValues<DuelRematchAction>()) {
   using var br=Read(DuelPacketCodec.SerializeRematch(action,19,73,true,action==DuelRematchAction.Commit?20:0));
   DuelPacketCodec.DeserializeRematch(br,out var actual,out var scope,out var proposal,out var hostProposal,out var nextRound);
   Check(actual==action && scope==19 && proposal==73 && hostProposal && nextRound==(action==DuelRematchAction.Commit?20:0),"Rematch "+action+" preserves match scope, proposal identity and host ownership");
  }
  var validRematch=DuelPacketCodec.SerializeRematch(DuelRematchAction.Request,19,73,false);
  foreach(var malformed in new[]{validRematch[..^1],new byte[]{DuelPacketCodec.ProtocolMagic,(byte)DuelPacketType.Rematch},validRematch.Concat(new byte[]{0}).ToArray()}) {
   rejected=false;try {using var br=Read(malformed);DuelPacketCodec.DeserializeRematch(br,out _,out _,out _,out _,out _);}catch(Exception error)when(error is IOException || error is InvalidDataException){rejected=true;}
   Check(rejected,"Malformed or legacy empty rematch payload is rejected");
  }
  foreach(int offset in new[]{2,3,7,11}) {
   var malformed=(byte[])validRematch.Clone();
   if(offset==2 || offset==11)malformed[offset]=255;else Array.Clear(malformed,offset,4);
   rejected=false;try {using var br=Read(malformed);DuelPacketCodec.DeserializeRematch(br,out _,out _,out _,out _,out _);}catch(Exception error)when(error is IOException || error is InvalidDataException){rejected=true;}
   Check(rejected,"Invalid rematch action, scope, proposal or owner is rejected at byte "+offset);
  }
  rejected=false;try{DuelPacketCodec.SerializeRematch(DuelRematchAction.Commit,19,73,true,19);}catch(InvalidDataException){rejected=true;}
  Check(rejected,"Rematch commit requires a forward arena round");
  using(var br=Read(DuelPacketCodec.SerializeHit(2,100,new Vector3(1,2,3),7))) {DuelPacketCodec.DeserializeHit(br,out var zone,out var damage,out var point,out var round);Check(zone==2 && damage==100 && point.z==3 && round==7,"Hit preserves round identity");}
  using(var br=Read(DuelPacketCodec.SerializeRoundState(DuelMatchState.RoundOver,2,1,"HOST",7))) {DuelPacketCodec.DeserializeRoundState(br,out var state,out var host,out var guest,out _,out var round);Check(state==DuelMatchState.RoundOver && host==2 && guest==1 && round==7,"Authoritative round result round-trip");}
  using(var br=Read(DuelPacketCodec.SerializeStateSync(new Vector3(1,2,3),45,10,true,true,0,9,17))) {DuelPacketCodec.DeserializeStateSync(br,out var position,out _,out _,out var aiming,out var crouching,out var hp,out var round,out var sequence);Check(position.z==3 && aiming && crouching && hp==0 && round==9 && sequence==17,"Movement preserves death and round identity");}
  Check(SniperDuelNetwork.ResolveTarget("127.0.0.1:19077",7777).Port==19077,"Direct endpoint resolves");
  Check(SniperDuelNetwork.ResolveTarget("127.0.0.1:70000",7777)==null,"Invalid port rejected");
  Check(SniperDuelNetwork.ResolveTarget("X-!",7777)==null,"Malformed encoded room rejected");
  var joinMode=new SniperDuelNetwork();
  try {
   Check(joinMode.StartHost(19080),"Room initially hosts before selecting Join");
   joinMode.DiscoveredLANHosts.Add(new DiscoveredHost {callsign="SELF",endpoint=new IPEndPoint(IPAddress.Loopback,19080)});
   joinMode.PrepareToJoin();
   Check(!joinMode.IsHost && !joinMode.IsConnecting && joinMode.DiscoveredLANHosts.Count==0,"Selecting Join closes own host and clears its discovery entry");
   string ownId=(string)typeof(SniperDuelNetwork).GetField("discoveryId",Private).GetValue(joinMode);
   var beacon=typeof(SniperDuelNetwork).GetMethod("ProcessBeacon",Private);
   var sender=new IPEndPoint(IPAddress.Parse("192.168.1.4"),7777);
   beacon.Invoke(joinMode,new object[]{System.Text.Encoding.UTF8.GetBytes("GEOSNIPER_HOST:SELF:7777:35.6762:139.6503:"+ownId),sender});Tick(joinMode);
   Check(joinMode.DiscoveredLANHosts.Count==0,"Device ignores its own LAN beacon");
   beacon.Invoke(joinMode,new object[]{System.Text.Encoding.UTF8.GetBytes("GEOSNIPER_HOST:OTHER:7777:35.6762:139.6503:other-device"),sender});Tick(joinMode);
   Check(joinMode.DiscoveredLANHosts.Count==1 && joinMode.DiscoveredLANHosts[0].callsign=="OTHER","Another device's host remains discoverable");
  } finally {joinMode.Disconnect();}
  var encode=typeof(SniperDuelNetwork).GetMethod("EncodeEndpointToRoomCode",Private);
  var hostNet=new SniperDuelNetwork {LocalCallsign="HOST"};var guestNet=new SniperDuelNetwork {LocalCallsign="GUEST"};
  string code=(string)encode.Invoke(hostNet,new object[]{IPAddress.Parse("203.0.113.5"),7777});
  Check(SniperDuelNetwork.ResolveTarget(code,7777).Equals(new IPEndPoint(IPAddress.Parse("203.0.113.5"),7777)),"Encoded endpoint round-trip");
  try {
   Check(hostNet.StartHost(19077),"Loopback host starts");
   var sharedMap=new byte[200123];for(int i=0;i<sharedMap.Length;i++)sharedMap[i]=(byte)(i%251);
   Check(hostNet.ConfigureRoom(true) && hostNet.CompetitiveSpotting,"Host can configure competitive spotting before choosing map");
   hostNet.SetHostMap(sharedMap,23.0225,72.5714);
   Check(!hostNet.ConfigureRoom(false),"Host room rules lock when shared arena map is selected");
   Check(guestNet.StartClient("127.0.0.1:19077"),"Loopback guest starts");
   var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<3000 && (!hostNet.IsConnected || !guestNet.IsConnected)){Tick(hostNet);Tick(guestNet);Thread.Sleep(10);}
   Check(hostNet.IsConnected && guestNet.IsConnected && hostNet.RemoteCallsign=="GUEST" && guestNet.RemoteCallsign=="HOST","Actual UDP handshake");
   Check(guestNet.HostLatitude==23.0225 && guestNet.HostLongitude==72.5714,"Handshake uses the host's chosen coordinates");
   timer.Restart();while(timer.ElapsedMilliseconds<5000 && guestNet.SharedMapData==null){Tick(hostNet);Tick(guestNet);Thread.Sleep(5);}
   Check(guestNet.SharedMapData!=null && guestNet.SharedMapData.SequenceEqual(sharedMap),"Host map transfers intact over multiple reliable UDP windows");
   hostNet.SendPacket(DuelPacketCodec.SerializeRoomRules(true));
   timer.Restart();while(timer.ElapsedMilliseconds<300 && !guestNet.CompetitiveSpotting){Tick(hostNet);Tick(guestNet);Thread.Sleep(10);}
   Check(guestNet.CompetitiveSpotting,"Host competitive rule arrives reliably before arena start");
   Process(guestNet,DuelPacketCodec.SerializeArena(new Vector3(1,8,3),new Vector3(4,12,6),2),guestNet.RemoteEndPoint);
   Check(guestNet.ArenaRound==2 && guestNet.ArenaHostSpawn.y==8 && guestNet.ArenaGuestSpawn.z==6,"Guest receives host arena assignments");
   Process(hostNet,DuelPacketCodec.SerializeRoundSignal(DuelPacketType.Ready,2),hostNet.RemoteEndPoint);
   Process(guestNet,DuelPacketCodec.SerializeRoundSignal(DuelPacketType.Engage,2),guestNet.RemoteEndPoint);
   Check(hostNet.ReadyRound==2 && guestNet.EngageRound==2,"Readiness and engage round signals synchronize peers");
   Process(guestNet,DuelPacketCodec.SerializeRoomRules(false),guestNet.RemoteEndPoint);
   Check(guestNet.CompetitiveSpotting,"Guest rule cannot change after gameplay starts");
   int delivered=0;hostNet.OnPacketReceived+=(type,br)=>{if(type==DuelPacketType.Hit) {DuelPacketCodec.DeserializeHit(br,out _,out _,out _,out _);delivered++;}};
   var hit=DuelPacketCodec.SerializeHit(1,25,new Vector3(1,2,3),1);
   guestNet.SendPacket(hit);
   Thread.Sleep(300); Tick(guestNet); // Retry before host processes/acknowledges the original.
   timer.Restart();while(timer.ElapsedMilliseconds<500){Tick(hostNet);Tick(guestNet);Thread.Sleep(10);}
   Check(delivered==1,"Actual UDP reliable retries deliver a hit only once");
   var pending=(System.Collections.IDictionary)typeof(SniperDuelNetwork).GetField("pendingReliable",Private).GetValue(guestNet);
   Check(pending.Count==0,"Acknowledgement clears retry queue");
   Process(hostNet,Reliable(3,hit),hostNet.RemoteEndPoint);Check(delivered==1,"Out-of-order hit waits for missing sequence");
   Process(hostNet,Reliable(2,hit),hostNet.RemoteEndPoint);Check(delivered==3,"Buffered hits delivered in order");
   Process(hostNet,Reliable(2,hit),hostNet.RemoteEndPoint);Check(delivered==3,"Duplicate hit discarded");
   Process(hostNet,DuelPacketCodec.SerializeSimple(DuelPacketType.Disconnect),new IPEndPoint(IPAddress.Loopback,19078));Check(hostNet.IsConnected,"Unrelated sender cannot disconnect match");
   Process(hostNet,new byte[]{DuelPacketCodec.ProtocolMagic,(byte)DuelPacketType.Hit},hostNet.RemoteEndPoint);
   // The event deliberately parses truncated data; transport catches the malformed payload.
   Check(hostNet.IsConnected && delivered==3,"Truncated payload safely rejected");
   int fires=0;guestNet.OnPacketReceived+=(type,br)=>{if(type==DuelPacketType.Fire){DuelPacketCodec.DeserializeFire(br,out var muzzle,out var direction,out var weapon,out var round,out var sequence,out var mask);if(round==2 && sequence==12 && weapon==1 && mask && muzzle.z==3 && direction.z==1)fires++;}};
   hostNet.SendPacket(DuelPacketCodec.SerializeFire(new Vector3(1,2,3),new Vector3(0,0,1),1,2,12,true));
   Thread.Sleep(300);Tick(hostNet);
   timer.Restart();while(timer.ElapsedMilliseconds<500){Tick(hostNet);Tick(guestNet);Thread.Sleep(10);}
   Check(fires==1,"Reliable fire retries preserve round, sequence and acoustic masking exactly once");
   int impacts=0;guestNet.OnPacketReceived+=(type,br)=>{if(type==DuelPacketType.Impact){DuelPacketCodec.DeserializeHit(br,out var material,out _,out var point,out var round);if(material==1 && point.z==3 && round==1)impacts++;}};
   var impact=DuelPacketCodec.SerializeHit(1,0,new Vector3(1,2,3),1);impact[1]=(byte)DuelPacketType.Impact;
   hostNet.SendPacket(impact);
   timer.Restart();while(timer.ElapsedMilliseconds<500){Tick(hostNet);Tick(guestNet);Thread.Sleep(10);}
   Check(impacts==1,"Surface impact reaches the other peer over reliable UDP");
   int rematches=0;guestNet.OnPacketReceived+=(type,br)=>{if(type==DuelPacketType.Rematch){DuelPacketCodec.DeserializeRematch(br,out var action,out var scope,out var proposal,out var hostProposal,out _);if(action==DuelRematchAction.Request && scope==2 && proposal==1 && hostProposal)rematches++;}};
   hostNet.SendPacket(DuelPacketCodec.SerializeRematch(DuelRematchAction.Request,2,1,true));
   Thread.Sleep(300);Tick(hostNet);
   timer.Restart();while(timer.ElapsedMilliseconds<500){Tick(hostNet);Tick(guestNet);Thread.Sleep(10);}
   Check(rematches==1,"Rematch consent uses authenticated reliable UDP and retries deliver one proposal");
   Process(guestNet,DuelPacketCodec.SerializeDisconnect("No supported arena positions."),guestNet.RemoteEndPoint);
   Check(!guestNet.IsConnected && guestNet.LastDisconnectReason.Contains("No supported arena positions."),"Host startup failure reason reaches guest");
  } finally {hostNet.Disconnect();guestNet.Disconnect();}
  Check(hostNet.SharedMapData==null && guestNet.SharedMapData==null,"Leaving a room clears its shared location/map");
  var lateHost=new SniperDuelNetwork {LocalCallsign="LATE-HOST"};
  var earlyGuest=new SniperDuelNetwork {LocalCallsign="EARLY-GUEST"};
  try {
   Check(earlyGuest.StartClient("127.0.0.1:19079"),"Guest can start before host is listening");
   Thread.Sleep(300); // Allow UDP port-unreachable/ConnectionReset before hosting.
   Check(lateHost.StartHost(19079),"Late host starts after initial unreachable reply");
   var recovery=Stopwatch.StartNew();
   while(recovery.ElapsedMilliseconds<4000 && (!lateHost.IsConnected || !earlyGuest.IsConnected)) {Tick(lateHost);Tick(earlyGuest);Thread.Sleep(10);}
   Check(lateHost.IsConnected && earlyGuest.IsConnected,"Receive loop recovers from initial unreachable host and completes handshake");
   Check(earlyGuest.SharedMapData==null,"Guest waits when the connected host has not chosen a map yet");
   int reconnectEvents=0;earlyGuest.OnConnected+=_=>reconnectEvents++;
   var lateMap=new byte[30001];for(int i=0;i<lateMap.Length;i++)lateMap[i]=(byte)(i%239);
   lateHost.SetHostMap(lateMap,-33.8688,151.2093);
   recovery.Restart();while(recovery.ElapsedMilliseconds<3000 && earlyGuest.SharedMapData==null) {Tick(lateHost);Tick(earlyGuest);Thread.Sleep(5);}
   Check(earlyGuest.SharedMapData!=null && earlyGuest.SharedMapData.SequenceEqual(lateMap)
      && earlyGuest.HostLatitude==-33.8688 && earlyGuest.HostLongitude==151.2093,"Host can choose the shared location after guest has connected");
   Check(reconnectEvents==0,"Location update does not restart the connected match");
  } finally {lateHost.Disconnect();earlyGuest.Disconnect();}
  var resumeHost=new SniperDuelNetwork {LocalCallsign="RESUME-HOST"};
  var resumeGuest=new SniperDuelNetwork {LocalCallsign="RESUME-GUEST"};
  try {
   Check(resumeHost.StartHost(19081) && resumeHost.ConfigureRoom(true) && resumeGuest.StartClient("127.0.0.1:19081"),"Reconnect room starts");
   var map=new byte[2001];map[123]=45;resumeHost.SetHostMap(map,12.3,45.6);
   var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<3000 && resumeGuest.SharedMapData==null) {Tick(resumeHost);Tick(resumeGuest);Thread.Sleep(5);}
   Check(resumeHost.IsConnected && resumeGuest.IsConnected && resumeGuest.SharedMapData.SequenceEqual(map),"Reconnect room establishes shared map");
   resumeHost.SendPacket(DuelPacketCodec.SerializeRoomRules(true));
   resumeHost.SendPacket(DuelPacketCodec.SerializeArena(new Vector3(1,2,3),new Vector3(4,5,6),4));
   resumeGuest.SendPacket(DuelPacketCodec.SerializeRoundSignal(DuelPacketType.Ready,4));
   resumeHost.SendPacket(DuelPacketCodec.SerializeRoundSignal(DuelPacketType.Engage,4));
   timer.Restart();while(timer.ElapsedMilliseconds<1000 && (resumeGuest.EngageRound!=4 || resumeHost.ReadyRound!=4)) {Tick(resumeHost);Tick(resumeGuest);Thread.Sleep(5);}
   int hostPauses=0,hostResumes=0,guestPauses=0,guestResumes=0,extraConnections=0;
   resumeHost.OnReconnectStateChanged+=paused=>{if(paused)hostPauses++;else hostResumes++;};
   resumeGuest.OnReconnectStateChanged+=paused=>{if(paused)guestPauses++;else guestResumes++;};
   resumeHost.OnConnected+=_=>extraConnections++;resumeGuest.OnConnected+=_=>extraConnections++;
   string token=(string)Get(resumeHost,"sessionToken");
   var unrelated=new IPEndPoint(IPAddress.Loopback,19999);
   RawProcess(resumeHost,Reconnect(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),1),unrelated);
   Check(!resumeHost.IsReconnecting && !resumeHost.RemoteEndPoint.Equals(unrelated),"Wrong reconnect token cannot pause or steal the peer endpoint");
   RawProcess(resumeHost,DuelPacketCodec.SerializeDisconnect("spoof"),resumeHost.RemoteEndPoint);
   Check(resumeHost.IsConnected,"Established session rejects unwrapped disconnect packets");
   RawProcess(resumeHost,Session(Guid.NewGuid().ToString("N"),DuelPacketCodec.SerializeDisconnect("old room")),resumeHost.RemoteEndPoint);
   Check(resumeHost.IsConnected,"Session envelope rejects delayed packets carrying another room's token");
   RawProcess(resumeHost,Session(token,DuelPacketCodec.SerializeDisconnect(new string('x',2050))),resumeHost.RemoteEndPoint);
   Check(resumeHost.IsConnected,"Oversized authenticated datagram is rejected before parsing its disconnect payload");
   var guestPending=(System.Collections.IDictionary)Get(resumeGuest,"pendingReliable");
   var pendingType=typeof(SniperDuelNetwork).GetNestedType("PendingReliable",BindingFlags.NonPublic);
   var lostAck=Activator.CreateInstance(pendingType);
   pendingType.GetField("data").SetValue(lostAck,Reliable(1,DuelPacketCodec.SerializeRoundSignal(DuelPacketType.Ready,4)));
   pendingType.GetField("lastSent").SetValue(lostAck,Time.unscaledTime);
   guestPending[(uint)1]=lostAck; // Host has delivered Ready #1; emulate its acknowledgement being lost.
   int oldFires=0,oldReloads=0,rematchRequests=0,preservedResults=0,preservedRounds=0,resumeClocks=0;
   resumeHost.OnPacketReceived+=(type,reader)=>{if(type==DuelPacketType.Fire)oldFires++;if(type==DuelPacketType.Reload)oldReloads++;if(type==DuelPacketType.Rematch)rematchRequests++;};
   resumeGuest.OnPacketReceived+=(type,reader)=>{if(type==DuelPacketType.ShotResult)preservedResults++;if(type==DuelPacketType.RoundState)preservedRounds++;if(type==DuelPacketType.ResumeClock)resumeClocks++;};
   resumeGuest.SendPacket(DuelPacketCodec.SerializeFire(new Vector3(1,2,3),new Vector3(0,0,1),1,4,1,false,3));
   resumeGuest.SendPacket(DuelPacketCodec.SerializeReload(4,1,3));
   resumeGuest.SendPacket(DuelPacketCodec.SerializeRematch(DuelRematchAction.Request,4,1,false));
   resumeHost.SendPacket(DuelPacketCodec.SerializeShotResult(4,1,true,1,1,25,75,new Vector3(4,5,6),120));
   resumeHost.SendPacket(DuelPacketCodec.SerializeRoundState(DuelMatchState.RoundOver,1,0,"HOST",4));
   typeof(SniperDuelNetwork).GetMethod("BeginReconnect",Private).Invoke(resumeHost,null);
   typeof(SniperDuelNetwork).GetMethod("BeginReconnect",Private).Invoke(resumeGuest,null);
   Check(resumeHost.IsConnected && resumeHost.IsReconnecting && hostPauses==1 && resumeHost.SharedMapData==map,"Transient timeout pauses while keeping connection and map");
   timer.Restart();while(timer.ElapsedMilliseconds<3000 && (resumeHost.IsReconnecting || hostResumes==0 || guestResumes==0)) {Tick(resumeHost);Tick(resumeGuest);Thread.Sleep(5);}
   Check(!resumeHost.IsReconnecting && !resumeGuest.IsReconnecting && hostResumes==1 && guestPauses==1 && guestResumes==1,"Actual UDP reconnect pauses and recovers both peers once");
   Check(extraConnections==0 && resumeGuest.ArenaRound==4 && resumeGuest.EngageRound==4 && resumeHost.ReadyRound==4 && resumeGuest.CompetitiveSpotting
      && resumeGuest.SharedMapData.SequenceEqual(map) && resumeGuest.HostLatitude==12.3,"Reconnect preserves arena, readiness, rules and shared location without a new match event");
   Check(guestPending.Count==0,"Reconnect receive watermark retires delivered packets whose acknowledgements were lost");
   Check(oldFires==0 && oldReloads==0,"Reconnect discards reliable shooting and reload requests queued before pause regardless of their timestamp");
   Check(rematchRequests==0,"Reconnect discards reliable rematch consent queued before the interruption");
   Check(preservedResults==1 && preservedRounds==1,"Reconnect preserves confirmed shot results and authoritative round results exactly once");
   var clock=DuelPacketCodec.SerializeRoundClock(4,177);clock[1]=(byte)DuelPacketType.ResumeClock;
   resumeHost.SendPacket(clock);
   int resumedHits=0;resumeHost.OnPacketReceived+=(type,reader)=>{if(type==DuelPacketType.Hit){DuelPacketCodec.DeserializeHit(reader,out _,out _,out _,out _);resumedHits++;}};
   resumeGuest.SendPacket(DuelPacketCodec.SerializeHit(1,25,new Vector3(1,2,3),4));
   resumeGuest.SendPacket(DuelPacketCodec.SerializeFire(new Vector3(1,2,3),new Vector3(0,0,1),1,4,2,false,3.1f));
   resumeGuest.SendPacket(DuelPacketCodec.SerializeReload(4,1,3.1f));
   resumeGuest.SendPacket(DuelPacketCodec.SerializeRematch(DuelRematchAction.Request,4,2,false));
   timer.Restart();while(timer.ElapsedMilliseconds<500){Tick(resumeHost);Tick(resumeGuest);Thread.Sleep(5);}
   Check(resumedHits==1,"Reliable gameplay delivery continues exactly once after reconnect");
   Check(oldFires==1 && oldReloads==1 && resumeClocks==1,"Fresh shooting, reload requests and resume clock deliver reliably after recovery");
   Check(rematchRequests==1,"Fresh rematch proposal can arrive reliably after recovery without restoring cancelled consent");
   string completed=(string)Get(resumeHost,"peerReconnectNonce");
   uint next=(uint)Get(resumeGuest,"nextReceiveId");
   RawProcess(resumeHost,Reconnect(token,completed,next),resumeHost.RemoteEndPoint);
   Check(!resumeHost.IsReconnecting && hostPauses==1,"Delayed completed reconnect request does not pause the recovered match again");
   RawProcess(resumeHost,Reconnect(token,completed,next),unrelated);
   Check(!resumeHost.IsReconnecting && !resumeHost.RemoteEndPoint.Equals(unrelated),"Delayed reconnect from an old source port cannot move the recovered peer endpoint");
   string oldFinalAckNonce=(string)Get(resumeHost,"reconnectNonce");
   RawProcess(resumeHost,Reconnect(token,oldFinalAckNonce,next,2,DuelPacketType.ReconnectAck),unrelated);
   Check(!resumeHost.IsReconnecting && !resumeHost.RemoteEndPoint.Equals(unrelated),"Delayed final reconnect acknowledgement cannot restore an old peer source port after recovery");
   var acceptedEndpoint=resumeHost.RemoteEndPoint;
   string migratingNonce=Guid.NewGuid().ToString("N");
   RawProcess(resumeHost,Reconnect(token,migratingNonce,next),unrelated);
   Check(resumeHost.IsReconnecting && resumeHost.RemoteEndPoint.Equals(unrelated),"Session token permits an authenticated peer source-port change");
   RawProcess(resumeHost,Reconnect(token,completed,next,1,DuelPacketType.ReconnectAck),acceptedEndpoint);
   Check(resumeHost.IsReconnecting,"A delayed old reconnect confirmation cannot recover a newer outage");
   Set(resumeHost,"reconnectStarted",Time.unscaledTime-31f);Tick(resumeHost);
   Check(!resumeHost.IsConnected && !resumeHost.IsReconnecting && resumeHost.SharedMapData==null && resumeHost.LastDisconnectReason.Contains("30 seconds"),"Reconnect deadline exits and clears the room instead of waiting forever");
  } finally {resumeHost.Disconnect();resumeGuest.Disconnect();}
  var timeoutNet=new SniperDuelNetwork();
  typeof(SniperDuelNetwork).GetProperty("IsConnected").SetValue(timeoutNet,true);
  typeof(SniperDuelNetwork).GetProperty("IsHost").SetValue(timeoutNet,true);
  typeof(SniperDuelNetwork).GetProperty("RemoteEndPoint").SetValue(timeoutNet,new IPEndPoint(IPAddress.Loopback,19083));
  Set(timeoutNet,"sessionToken",Guid.NewGuid().ToString("N"));Set(timeoutNet,"gameplayStarted",true);
  Set(timeoutNet,"lastPacketTime",Time.unscaledTime-9f);Tick(timeoutNet);
  Check(timeoutNet.IsConnected && timeoutNet.IsReconnecting,"Gameplay's eight-second silence threshold starts reconnect instead of ending the match");
  timeoutNet.Disconnect();
 }
}
