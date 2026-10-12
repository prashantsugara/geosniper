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
        Disconnect = 11,
        Reliable = 12,
        Ack = 13,
        Arena = 14,
        Ready = 15,
        Engage = 16,
        MapChunk = 17,
        Impact = 18,
        RoomRules = 19,
        RoundClock = 20,
        ReturnToArena = 21,
        Session = 22,
        Reconnect = 23,
        ReconnectAck = 24,
        ShotResult = 25,
        Reload = 26,
        ResumeClock = 27
    }

    public enum DuelRematchAction : byte
    {
        Request = 1,
        Accept = 2,
        Cancel = 3,
        Offer = 4,
        Ready = 5,
        Commit = 6
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
        public const byte ProtocolVersion = 9;

        public static byte[] SerializeRematch(DuelRematchAction action, int completedRound, int requestId,
            bool requestedByHost, int nextRound = 0)
        {
            ValidateRematch(action, completedRound, requestId, nextRound);
            using (var ms = new MemoryStream(16))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic); bw.Write((byte)DuelPacketType.Rematch);
                bw.Write((byte)action); bw.Write(completedRound); bw.Write(requestId);
                bw.Write(requestedByHost); bw.Write(nextRound);
                return ms.ToArray();
            }
        }

        public static void DeserializeRematch(BinaryReader br, out DuelRematchAction action, out int completedRound,
            out int requestId, out bool requestedByHost, out int nextRound)
        {
            action = (DuelRematchAction)br.ReadByte(); completedRound = br.ReadInt32(); requestId = br.ReadInt32();
            byte owner = br.ReadByte();
            if (owner > 1) throw new InvalidDataException("Invalid rematch proposal owner");
            requestedByHost = owner == 1; nextRound = br.ReadInt32();
            ValidateRematch(action, completedRound, requestId, nextRound);
            if (br.BaseStream.Position != br.BaseStream.Length) throw new InvalidDataException("Unexpected rematch payload");
        }

        private static void ValidateRematch(DuelRematchAction action, int completedRound, int requestId, int nextRound)
        {
            if (action < DuelRematchAction.Request || action > DuelRematchAction.Commit || completedRound <= 0 || requestId <= 0
                || (action == DuelRematchAction.Commit ? nextRound <= completedRound : nextRound != 0))
                throw new InvalidDataException("Invalid rematch action or match identity");
        }

        private static void ReadVersion(BinaryReader br)
        {
            if (br.ReadByte() != ProtocolVersion) throw new InvalidDataException("Unsupported duel protocol version");
        }

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
            ReadVersion(br);
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
            ReadVersion(br);
            hostCallsign = br.ReadString();
            lat = br.ReadDouble();
            lon = br.ReadDouble();
            roundTarget = br.ReadInt32();
            seed = br.ReadInt32();
        }

        public static byte[] SerializeStateSync(Vector3 pos, float yaw, float pitch, bool isAiming, bool isCrouching, float hp, int roundId = 0, int sequence = 0, int weaponIndex = 1)
        {
            using (var ms = new MemoryStream(32))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.StateSync);
                bw.Write(roundId); bw.Write(sequence);
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
                bw.Write((byte)weaponIndex);
                return ms.ToArray();
            }
        }

        public static void DeserializeStateSync(BinaryReader br, out Vector3 pos, out float yaw, out float pitch, out bool isAiming, out bool isCrouching, out float hp, out int roundId, out int sequence)
        {
            roundId = br.ReadInt32(); sequence = br.ReadInt32();
            pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            yaw = br.ReadSingle();
            pitch = br.ReadSingle();
            byte flags = br.ReadByte();
            isAiming = (flags & 1) != 0;
            isCrouching = (flags & 2) != 0;
            hp = br.ReadByte();
        }

        public static byte[] SerializeFire(Vector3 muzzle, Vector3 dir, int weaponIndex, int roundId = 0, int sequence = 0, bool soundMasked = false, float roundTime = -1f)
        {
            using (var ms = new MemoryStream(40))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.Fire);
                bw.Write(roundId); bw.Write(sequence); bw.Write(soundMasked);
                bw.Write(muzzle.x);
                bw.Write(muzzle.y);
                bw.Write(muzzle.z);
                bw.Write(dir.x);
                bw.Write(dir.y);
                bw.Write(dir.z);
                bw.Write((byte)weaponIndex);
                bw.Write(roundTime);
                return ms.ToArray();
            }
        }

        public static void DeserializeFire(BinaryReader br, out Vector3 muzzle, out Vector3 dir, out int weaponIndex, out int roundId, out int sequence, out bool soundMasked)
            => DeserializeFire(br, out muzzle, out dir, out weaponIndex, out roundId, out sequence, out soundMasked, out _);

        public static void DeserializeFire(BinaryReader br, out Vector3 muzzle, out Vector3 dir, out int weaponIndex, out int roundId, out int sequence, out bool soundMasked, out float roundTime)
        {
            roundId = br.ReadInt32(); sequence = br.ReadInt32(); soundMasked = br.ReadBoolean();
            muzzle = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            dir = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            weaponIndex = br.ReadByte();
            roundTime = br.ReadSingle();
        }

        public static byte[] SerializeShotResult(int round, int shot, bool hostShooter, int weapon, byte zone, float damage, float targetHealth, Vector3 point, float distance)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(ProtocolMagic); writer.Write((byte)DuelPacketType.ShotResult);
                writer.Write(round); writer.Write(shot); writer.Write(hostShooter); writer.Write((byte)weapon);
                writer.Write(zone); writer.Write(damage); writer.Write(targetHealth);
                writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); writer.Write(distance);
                return stream.ToArray();
            }
        }

        public static byte[] SerializeResumeClock(int round, float seconds, float shotTime, int ammo0=5, int ammo1=20, int ammo2=30, int reloadWeapon=-1, float reloadRemaining=0, float cooldownRemaining=0, int[] extendedAmmo=null)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(ProtocolMagic); writer.Write((byte)DuelPacketType.ResumeClock);
                writer.Write(round); writer.Write(seconds); writer.Write(shotTime);
                writer.Write((byte)ammo0); writer.Write((byte)ammo1); writer.Write((byte)ammo2); writer.Write((byte)(reloadWeapon < 0 ? 255 : reloadWeapon));
                writer.Write(reloadRemaining); writer.Write(cooldownRemaining);
                int[] defaults={5,10,30,30,7};
                for(int i=0;i<5;i++)writer.Write((byte)(extendedAmmo!=null?extendedAmmo[i]:defaults[i]));
                return stream.ToArray();
            }
        }

        public static byte[] SerializeReload(int round, int weapon, float roundTime)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(ProtocolMagic); writer.Write((byte)DuelPacketType.Reload);
                writer.Write(round); writer.Write((byte)weapon); writer.Write(roundTime); return stream.ToArray();
            }
        }

        public static byte[] SerializeHit(byte hitZone, float damage, Vector3 hitPoint, int roundId)
        {
            using (var ms = new MemoryStream(24))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.Hit);
                bw.Write(roundId);
                bw.Write(hitZone);
                bw.Write(damage);
                bw.Write(hitPoint.x);
                bw.Write(hitPoint.y);
                bw.Write(hitPoint.z);
                return ms.ToArray();
            }
        }

        public static void DeserializeHit(BinaryReader br, out byte hitZone, out float damage, out Vector3 hitPoint, out int roundId)
        {
            roundId = br.ReadInt32();
            hitZone = br.ReadByte();
            damage = br.ReadSingle();
            hitPoint = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }

        public static byte[] SerializeDeath(string killer, bool isHeadshot, float distanceMeters, int roundId)
        {
            using (var ms = new MemoryStream(32))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.Death);
                bw.Write(roundId);
                bw.Write(killer ?? "RIVAL");
                bw.Write(isHeadshot);
                bw.Write(distanceMeters);
                return ms.ToArray();
            }
        }

        public static void DeserializeDeath(BinaryReader br, out string killer, out bool isHeadshot, out float distanceMeters, out int roundId)
        {
            roundId = br.ReadInt32();
            killer = br.ReadString();
            isHeadshot = br.ReadBoolean();
            distanceMeters = br.ReadSingle();
        }

        public static byte[] SerializeRoundState(DuelMatchState state, int hostScore, int clientScore, string roundWinner, int roundId)
        {
            using (var ms = new MemoryStream(32))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic);
                bw.Write((byte)DuelPacketType.RoundState);
                bw.Write(roundId);
                bw.Write((byte)state);
                bw.Write(hostScore);
                bw.Write(clientScore);
                bw.Write(roundWinner ?? "");
                return ms.ToArray();
            }
        }

        public static void DeserializeRoundState(BinaryReader br, out DuelMatchState state, out int hostScore, out int clientScore, out string roundWinner, out int roundId)
        {
            roundId = br.ReadInt32();
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

        public static byte[] SerializeDisconnect(string reason)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic); bw.Write((byte)DuelPacketType.Disconnect);
                bw.Write(reason ?? "Opponent left the duel.");
                return ms.ToArray();
            }
        }

        public static byte[] SerializeRoundSignal(DuelPacketType type, int roundId)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic); bw.Write((byte)type); bw.Write(roundId);
                return ms.ToArray();
            }
        }

        public static byte[] SerializeArena(Vector3 host, Vector3 guest, int roundId)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ProtocolMagic); bw.Write((byte)DuelPacketType.Arena); bw.Write(roundId);
                bw.Write(host.x); bw.Write(host.y); bw.Write(host.z);
                bw.Write(guest.x); bw.Write(guest.y); bw.Write(guest.z);
                return ms.ToArray();
            }
        }

        public static byte[] SerializeRoomRules(bool competitive)
        {
            return new byte[] {ProtocolMagic, (byte)DuelPacketType.RoomRules, (byte)(competitive ? 1 : 0)};
        }

        public static byte[] SerializeRoundClock(int roundId, float seconds)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(ProtocolMagic); writer.Write((byte)DuelPacketType.RoundClock);
                writer.Write(roundId); writer.Write(seconds); return stream.ToArray();
            }
        }

        public static byte[] SerializeSimple(DuelPacketType type)
        {
            return new byte[] { ProtocolMagic, (byte)type };
        }
    }
}
