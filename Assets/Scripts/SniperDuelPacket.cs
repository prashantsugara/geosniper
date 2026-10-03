using System;
using System.IO;
using UnityEngine;

namespace GeoSniper.Duel
{
    public enum DuelPacketType : byte
    {
        Ping = 1,
        Pong = 2,
        Hello = 3,
        Welcome = 4,
        StateSync = 5,
        Fire = 6,
        Hit = 7,
        Death = 8,
        RoundState = 9,
        Rematch = 10,
        Disconnect = 11
    }

    public enum DuelMatchState : byte
    {
        Idle = 0,
        LobbyWait = 1,
        Warmup = 2,
        InRound = 3,
        RoundOver = 4,
        MatchOver = 5
    }

    public static class DuelPacketCodec
    {
        public const byte ProtocolMagic = 0x53; // 'S' for Sniper
        public const byte ProtocolVersion = 1;

        public static byte[] SerializeHello(string callsign, int weaponIndex)
        {
            using (var ms = new MemoryStream(64))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.Hello);
                bw.Write(ProtocolVersion);
                bw.Write(callsign ?? "GUEST");
                bw.Write(weaponIndex);
                return ms.ToArray();
            }
        }

        public static void DeserializeHello(BinaryReader br, out string callsign, out int weaponIndex)
        {
            callsign = br.ReadString();
            weaponIndex = br.ReadInt32();
        }

        public static byte[] SerializeWelcome(string hostCallsign, double lat, double lon, int roundTarget, int seed)
        {
            using (var ms = new MemoryStream(64))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.Welcome);
                bw.Write(ProtocolVersion);
                bw.Write(hostCallsign ?? "HOST");
                bw.Write(lat);
                bw.Write(lon);
                bw.Write(roundTarget);
                bw.Write(seed);
                return ms.ToArray();
            }
        }

        public static void DeserializeWelcome(BinaryReader br, out string hostCallsign, out double lat, out double lon, out int roundTarget, out int seed)
        {
            hostCallsign = br.ReadString();
            lat = br.ReadDouble();
            lon = br.ReadDouble();
            roundTarget = br.ReadInt32();
            seed = br.ReadInt32();
        }

        public static byte[] SerializeStateSync(Vector3 pos, float yaw, float pitch, bool isAiming, bool isCrouching, float hp)
        {
            using (var ms = new MemoryStream(32))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.StateSync);
                bw.Write(pos.x);
                bw.Write(pos.y);
                bw.Write(pos.z);
                bw.Write(yaw);
                bw.Write(pitch);
                byte flags = 0;
                if (isAiming) flags |= 1;
                if (isCrouching) flags |= 2;
                bw.Write(flags);
                bw.Write((byte)Mathf.Clamp(Mathf.RoundToInt(hp), 0, 255));
                return ms.ToArray();
            }
        }

        public static void DeserializeStateSync(BinaryReader br, out Vector3 pos, out float yaw, out float pitch, out bool isAiming, out bool isCrouching, out float hp)
        {
            pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            yaw = br.ReadSingle();
            pitch = br.ReadSingle();
            byte flags = br.ReadByte();
            isAiming = (flags & 1) != 0;
            isCrouching = (flags & 2) != 0;
            hp = br.ReadByte();
        }

        public static byte[] SerializeFire(Vector3 muzzle, Vector3 dir, int weaponIndex)
        {
            using (var ms = new MemoryStream(32))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.Fire);
                bw.Write(muzzle.x);
                bw.Write(muzzle.y);
                bw.Write(muzzle.z);
                bw.Write(dir.x);
                bw.Write(dir.y);
                bw.Write(dir.z);
                bw.Write((byte)weaponIndex);
                return ms.ToArray();
            }
        }

        public static void DeserializeFire(BinaryReader br, out Vector3 muzzle, out Vector3 dir, out int weaponIndex)
        {
            muzzle = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            dir = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            weaponIndex = br.ReadByte();
        }

        public static byte[] SerializeHit(byte hitZone, float damage, Vector3 hitPoint)
        {
            using (var ms = new MemoryStream(24))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.Hit);
                bw.Write(hitZone);
                bw.Write(damage);
                bw.Write(hitPoint.x);
                bw.Write(hitPoint.y);
                bw.Write(hitPoint.z);
                return ms.ToArray();
            }
        }

        public static void DeserializeHit(BinaryReader br, out byte hitZone, out float damage, out Vector3 hitPoint)
        {
            hitZone = br.ReadByte();
            damage = br.ReadSingle();
            hitPoint = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }

        public static byte[] SerializeDeath(string killer, bool isHeadshot, float distanceMeters)
        {
            using (var ms = new MemoryStream(32))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.Death);
                bw.Write(killer ?? "RIVAL");
                bw.Write(isHeadshot);
                bw.Write(distanceMeters);
                return ms.ToArray();
            }
        }

        public static void DeserializeDeath(BinaryReader br, out string killer, out bool isHeadshot, out float distanceMeters)
        {
            killer = br.ReadString();
            isHeadshot = br.ReadBoolean();
            distanceMeters = br.ReadSingle();
        }

        public static byte[] SerializeRoundState(DuelMatchState state, int hostScore, int clientScore, string roundWinner)
        {
            using (var ms = new MemoryStream(32))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.RoundState);
                bw.Write((byte)state);
                bw.Write(hostScore);
                bw.Write(clientScore);
                bw.Write(roundWinner ?? "");
                return ms.ToArray();
            }
        }

        public static void DeserializeRoundState(BinaryReader br, out DuelMatchState state, out int hostScore, out int clientScore, out string roundWinner)
        {
            state = (DuelMatchState)br.ReadByte();
            hostScore = br.ReadInt32();
            clientScore = br.ReadInt32();
            roundWinner = br.ReadString();
        }

        public static byte[] SerializePing(long timestamp)
        {
            using (var ms = new MemoryStream(16))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.Ping);
                bw.Write(timestamp);
                return ms.ToArray();
            }
        }

        public static byte[] SerializePong(long timestamp)
        {
            using (var ms = new MemoryStream(16))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.Pong);
                bw.Write(timestamp);
                return ms.ToArray();
            }
        }

        public static long DeserializePingPong(BinaryReader br)
        {
            return br.ReadInt64();
        }

        public static byte[] SerializeSimple(DuelPacketType type)
        {
            return new byte[] { ProtocolMagic, (byte)type };
        }
    }
}
