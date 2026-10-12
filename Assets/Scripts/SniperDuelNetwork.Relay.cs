using System;
using System.Net;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace GeoSniper.Duel
{
    public sealed partial class SniperDuelNetwork
    {
        NetworkDriver relayDriver;
        NetworkPipeline relayPipeline;
        NetworkConnection relayPeer;
        NetworkEndpoint relayEndpoint;
        bool usingRelay, relayStarting;
        int relayEpoch;
        float relayReconnectAt;
        static Task servicesTask;
        static readonly IPEndPoint RelayPeerIdentity = new IPEndPoint(IPAddress.Loopback, DefaultPort);
        public bool IsRelayStarting => relayStarting;
        public string ConnectionStatus { get; private set; } = "";

        public static bool TryRelayCode(string input, out string code)
        {
            code=(input??"").Trim().ToUpperInvariant();
            if(code.Length==10)
            {
                ulong number=0;
                foreach(char c in code){if(c<'0'||c>'9')return false;number=number*10+(uint)(c-'0');}
                if(number>=2176782336UL)return false; // 36^6 possible Relay codes.
                var chars=new char[6];
                for(int i=5;i>=0;i--){chars[i]="0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"[(int)(number%36)];number/=36;}
                code=new string(chars);return true;
            }
            if(code.StartsWith("R-",StringComparison.Ordinal))code=code.Substring(2);
            if(code.Length!=6)return false;
            foreach(char c in code)if(!(c>='A'&&c<='Z')&&!(c>='0'&&c<='9'))return false;
            return true;
        }
        public static string NumericRoomCode(string relayCode)
        {
            if(!TryRelayCode(relayCode,out string code))throw new ArgumentException("Invalid Relay code.",nameof(relayCode));
            ulong number=0;
            foreach(char c in code)number=number*36+(uint)"0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".IndexOf(c);
            return number.ToString("D10",System.Globalization.CultureInfo.InvariantCulture);
        }

        // Existing callers start synchronously; allocation and authentication remain asynchronous.
        public bool StartHost(int port=DefaultPort)
        {
            if(usingRelay && IsHost)return true;
            StartRelay(true,null);return true;
        }
        public bool StartClient(string targetHostOrCode,int port=DefaultPort)
        {
            if(!DuelInviteShare.TryExtractJoinTarget(targetHostOrCode,out string target)
                || !TryRelayCode(target,out string code))
            { LastDisconnectReason="Enter the 10-digit room code or paste the invite.";return false; }
            StartRelay(false,code);return true;
        }
        static async Task InitializeRelayServices()
        {
            if(UnityServices.State!=ServicesInitializationState.Initialized)await UnityServices.InitializeAsync();
            if(!AuthenticationService.Instance.IsSignedIn)await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        async void StartRelay(bool host,string code)
        {
            CloseSockets();
            int epoch=relayEpoch;
            usingRelay=true;relayStarting=true;IsHost=host;
            LastDisconnectReason="";ConnectionStatus="Connecting to Unity Relay...";
            clientNonce=host?"":Guid.NewGuid().ToString("N");
            lastPacketTime=Time.unscaledTime;
            try
            {
                if(servicesTask==null || servicesTask.IsFaulted || servicesTask.IsCanceled)servicesTask=InitializeRelayServices();
                await WithRelayTimeout(servicesTask);
                if(epoch!=relayEpoch)return;
                RelayServerData data;
                string room;
                if(host)
                {
                    ConnectionStatus="Creating Relay room...";
                    var allocation=await WithRelayTimeout(RelayService.Instance.CreateAllocationAsync(1));
                    if(epoch!=relayEpoch)return;
                    room=await WithRelayTimeout(RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId));
                    if(epoch!=relayEpoch)return;
                    data=AllocationUtils.ToRelayServerData(allocation,"dtls");
                }
                else
                {
                    ConnectionStatus="Joining Relay room...";
                    var allocation=await WithRelayTimeout(RelayService.Instance.JoinAllocationAsync(code));
                    if(epoch!=relayEpoch)return;
                    data=AllocationUtils.ToRelayServerData(allocation,"dtls");room=code;
                }
                var settings=new NetworkSettings(Allocator.Temp);
                try
                {
                    settings.WithRelayParameters(ref data);
                    settings.WithFragmentationStageParameters(payloadCapacity:MaxDatagramBytes);
                    relayDriver=NetworkDriver.Create(settings);
                }
                finally {settings.Dispose();}
                relayPipeline=relayDriver.CreatePipeline(typeof(FragmentationPipelineStage));
                relayEndpoint=data.Endpoint;
                if(relayDriver.Bind(NetworkEndpoint.AnyIpv4)!=0)throw new InvalidOperationException("Relay transport could not bind.");
                if(host)
                { if(relayDriver.Listen()!=0)throw new InvalidOperationException("Relay transport could not listen."); }
                else
                { RemoteEndPoint=RelayPeerIdentity;relayPeer=relayDriver.Connect(relayEndpoint); }
                // Update pumps the driver while binding; never expose an unusable invite.
                var expires=DateTime.UtcNow.AddSeconds(20);
                while(epoch==relayEpoch && relayDriver.IsCreated && relayDriver.GetRelayConnectionStatus()!=RelayConnectionStatus.Established)
                {
                    if(relayDriver.GetRelayConnectionStatus()==RelayConnectionStatus.AllocationInvalid || DateTime.UtcNow>=expires)
                        throw new InvalidOperationException("Could not reach the Relay server. Check your connection and retry.");
                    await Task.Delay(50);
                }
                if(epoch!=relayEpoch)return;
                RoomCode=NumericRoomCode(room);relayStarting=false;lastPacketTime=Time.unscaledTime;
                ConnectionStatus=host?"RELAY ONLINE · SHARE THIS CODE":"RELAY ONLINE · CONNECTING TO HOST";
            }
            catch(Exception error)
            {
                if(epoch!=relayEpoch)return;
                Debug.LogWarning("[DuelRelay] Room setup failed: "+error);
                Disconnect("Unity Relay could not connect. Check internet access, the room code, and Relay/Anonymous Authentication in Unity Dashboard.");
            }
        }
        static async Task WithRelayTimeout(Task task)
        {
            if(await Task.WhenAny(task,Task.Delay(30000))!=task)throw new TimeoutException("Unity services request timed out.");
            await task;
        }
        static async Task<T> WithRelayTimeout<T>(Task<T> task)
        { await WithRelayTimeout((Task)task);return await task; }

        void PumpRelay()
        {
            if(!usingRelay || !relayDriver.IsCreated)return;
            relayDriver.ScheduleUpdate().Complete();
            if(relayDriver.GetRelayConnectionStatus()==RelayConnectionStatus.AllocationInvalid)
            { Disconnect("Relay room expired or connection was lost. Create a new room and share its code.");return; }
            if(IsHost)
            {
                NetworkConnection next;
                while((next=relayDriver.Accept()).IsCreated)
                {
                    if(relayPeer.IsCreated)relayDriver.Disconnect(next);
                    else {relayPeer=next;RemoteEndPoint=RelayPeerIdentity;}
                }
            }
            if(!relayPeer.IsCreated)
            {
                if(!IsHost && IsReconnecting && Time.unscaledTime>=relayReconnectAt)
                { relayReconnectAt=Time.unscaledTime+1;relayPeer=relayDriver.Connect(relayEndpoint); }
                return;
            }
            NetworkEvent.Type type;
            while((type=relayDriver.PopEventForConnection(relayPeer,out var reader))!=NetworkEvent.Type.Empty)
            {
                if(type==NetworkEvent.Type.Connect && !IsHost)SendHello();
                else if(type==NetworkEvent.Type.Data && reader.Length>0 && reader.Length<=MaxDatagramBytes)
                {
                    using(var bytes=new NativeArray<byte>(reader.Length,Allocator.Temp))
                    {reader.ReadBytes(bytes);incomingQueue.Enqueue(new ReceivedPacket{data=bytes.ToArray(),sender=RelayPeerIdentity});}
                }
                else if(type==NetworkEvent.Type.Disconnect)
                {
                    relayPeer=default;
                    if(IsConnected)BeginReconnect();
                    else if(!IsHost){Disconnect("The Relay host disconnected or could not be reached.");return;}
                    break;
                }
            }
        }
        void SendRelay(byte[] data)
        {
            if(!relayDriver.IsCreated || !relayPeer.IsCreated || data.Length>MaxDatagramBytes)return;
            if(relayDriver.BeginSend(relayPipeline,relayPeer,out var writer,data.Length)!=0)return;
            using(var bytes=new NativeArray<byte>(data,Allocator.Temp))writer.WriteBytes(bytes);
            relayDriver.EndSend(writer);
        }
        void CloseRelay()
        {
            relayEpoch++;relayStarting=false;usingRelay=false;ConnectionStatus="";
            if(relayDriver.IsCreated)relayDriver.Dispose();
            relayPeer=default;
        }
    }
}
