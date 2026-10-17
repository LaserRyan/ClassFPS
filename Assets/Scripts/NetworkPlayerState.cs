using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct NetworkHeroState : INetworkSerializable, IEquatable<NetworkHeroState>
{
    public FixedString64Bytes HeroId;
    public Vector3 Spawn;
    public float Yaw;
    public uint Revision;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref HeroId);
        serializer.SerializeValue(ref Spawn);
        serializer.SerializeValue(ref Yaw);
        serializer.SerializeValue(ref Revision);
    }
    public bool Equals(NetworkHeroState other) => HeroId.Equals(other.HeroId) && Spawn == other.Spawn &&
        Yaw == other.Yaw && Revision == other.Revision;
}

public struct NetworkPlayerPose : INetworkSerializable, IEquatable<NetworkPlayerPose>
{
    public Vector3 Position;
    public float Yaw;
    public float Pitch;
    public uint Revision;
    public uint Sequence;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Yaw);
        serializer.SerializeValue(ref Pitch);
        serializer.SerializeValue(ref Revision);
        serializer.SerializeValue(ref Sequence);
    }
    public bool Equals(NetworkPlayerPose other) => Position == other.Position && Yaw == other.Yaw &&
        Pitch == other.Pitch && Revision == other.Revision && Sequence == other.Sequence;
    public bool IsFinite => Finite(Position.x) && Finite(Position.y) && Finite(Position.z) && Finite(Yaw) && Finite(Pitch);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
