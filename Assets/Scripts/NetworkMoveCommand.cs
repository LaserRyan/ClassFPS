using Unity.Netcode;
using UnityEngine;

// A fixed-duration input, never a client position, velocity, damage or elapsed time.
public struct NetworkMoveCommand : INetworkSerializable
{
    public uint Generation, Sequence;
    public double Time;
    public float X, Z, Yaw, Pitch;
    public byte Buttons;
    public const byte Jump = 1, Fire = 2, Reload = 4, Dash = 8, Allowed = 16, CancelDash = 32;
    public bool Has(byte button) => (Buttons & button) != 0;
    public bool Valid => Sequence > 0 && Finite(Time) && Finite(X) && Finite(Z) && Finite(Yaw) &&
        Finite(Pitch) && X * X + Z * Z <= 1.001f && Mathf.Abs(Pitch) <= 89 && Mathf.Abs(Yaw) <= 36000 && (Buttons & ~63) == 0;
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref Generation); s.SerializeValue(ref Sequence); s.SerializeValue(ref Time);
        s.SerializeValue(ref X); s.SerializeValue(ref Z); s.SerializeValue(ref Yaw); s.SerializeValue(ref Pitch);
        s.SerializeValue(ref Buttons);
    }
}
