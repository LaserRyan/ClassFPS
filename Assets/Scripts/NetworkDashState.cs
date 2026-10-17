using System;

// Server permission/cooldown only. Accepted movement uses the owner's existing controller.
public sealed class NetworkDashState
{
    public uint Generation { get; private set; }
    public uint LastRequest { get; private set; }
    public uint ActiveRequest { get; private set; }
    public double ReadyAt { get; private set; }
    private double activeUntil, nextRequest;
    public bool Active(double now) => activeUntil > now;
    public void Reset(uint generation)
    { Generation = generation; LastRequest = ActiveRequest = 0; ReadyAt = activeUntil = nextRequest = 0; }
    public bool TryStart(uint generation, uint sequence, double now, bool owner, bool gunslinger, bool alive,
        bool validPose, float distance, float duration, float cooldown)
    {
        if (!owner || generation != Generation || sequence <= LastRequest) return false;
        LastRequest = sequence;
        if (now < nextRequest) return false;
        nextRequest = now + .05;
        if (!gunslinger || !alive || !validPose || Active(now) || now < ReadyAt ||
            !(distance > 0) || !(duration > 0) || !(cooldown >= 0) ||
            float.IsInfinity(distance) || float.IsInfinity(duration) || float.IsInfinity(cooldown)) return false;
        ActiveRequest = sequence;
        activeUntil = now + duration;
        ReadyAt = now + cooldown;
        return true;
    }
    public bool Cancel(uint generation, uint request)
    {
        if (generation != Generation || request == 0 || request != ActiveRequest || activeUntil == 0) return false;
        activeUntil = 0;
        return true;
    }
}
