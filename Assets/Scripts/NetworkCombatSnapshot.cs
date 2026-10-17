using System;
using Unity.Netcode;

public struct NetworkCombatSnapshot : INetworkSerializable, IEquatable<NetworkCombatSnapshot>
{
    public uint Generation;
    public float Health, Maximum;
    public int Ammo;
    public bool Dead, Reloading;
    public double RespawnAt;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref Generation); s.SerializeValue(ref Health); s.SerializeValue(ref Maximum);
        s.SerializeValue(ref Ammo); s.SerializeValue(ref Dead); s.SerializeValue(ref Reloading); s.SerializeValue(ref RespawnAt);
    }
    public bool Equals(NetworkCombatSnapshot v) => Generation == v.Generation && Health == v.Health && Maximum == v.Maximum &&
        Ammo == v.Ammo && Dead == v.Dead && Reloading == v.Reloading && RespawnAt == v.RespawnAt;
}
