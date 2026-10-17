using System;

// Server instance state, independent of Unity clocks/input/physics and shared assets.
public sealed class NetworkRevolverState
{
    public uint Generation { get; private set; }
    public uint LastRequest { get; private set; }
    public int Ammo { get; private set; }
    public bool Dead { get; private set; }
    public double ReloadAt { get; private set; }
    public double RespawnAt { get; private set; }
    private double nextShot;
    private double nextRequest;
    public void Reset(uint generation, int capacity)
    { Generation = generation; Ammo = capacity; LastRequest = 0; Dead = false; ReloadAt = RespawnAt = nextShot = nextRequest = 0; }
    public bool Request(uint generation, uint sequence, double now, bool owner)
    {
        if (!owner || generation != Generation || sequence <= LastRequest) return false;
        LastRequest = sequence;
        if (now < nextRequest || Dead) return false;
        nextRequest = now + 0.025; // Shared fire/reload flood budget, no request queue.
        return true;
    }
    public void Tick(double now, int capacity)
    { if (!Dead && ReloadAt > 0 && now >= ReloadAt) { Ammo = capacity; ReloadAt = 0; } }
    public bool Reload(double now, int capacity, float duration)
    {
        if (Dead || ReloadAt > 0 || Ammo >= capacity) return false;
        ReloadAt = now + Math.Max(0.01, duration); return true;
    }
    public bool Fire(double now, float cooldown, int capacity, float reloadDuration, bool automatic)
    {
        Tick(now, capacity);
        if (Dead || ReloadAt > 0 || now < nextShot) return false;
        if (Ammo <= 0) { if (automatic) Reload(now, capacity, reloadDuration); return false; }
        Ammo--; nextShot = now + Math.Max(0.01, cooldown);
        if (Ammo == 0 && automatic) Reload(now, capacity, reloadDuration);
        return true;
    }
    public static float ActualDamage(float health, float requested)
    {
        if (!(health > 0) || !(requested > 0) || float.IsInfinity(requested)) return 0;
        return health - Math.Max(0, health - requested);
    }
    public bool Die(double now, float delay)
    { if (Dead) return false; Dead = true; ReloadAt = 0; RespawnAt = now + Math.Max(0, delay); return true; }
}


// Orders movement snapshots within a life, independently of packet arrival order.
public sealed class NetworkPoseOrder
{
    public uint Generation { get; private set; }
    public uint LastSequence { get; private set; }
    public void Reset(uint generation) { Generation = generation; LastSequence = 0; }
    public bool TryAccept(uint generation, uint sequence, bool finite, bool alive)
    {
        if (!alive || !finite || generation != Generation || sequence == 0 || sequence <= LastSequence) return false;
        LastSequence = sequence;
        return true;
    }
}
