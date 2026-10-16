using System;

// Deterministic policy, independent of input devices and Unity physics.
public static class GunslingerRules
{
    public static float Healing(float actualDamage, float percent, float health, float maximum,
        bool aliveAndEnabled, bool selfDamage)
    {
        if (!aliveAndEnabled || selfDamage || !(health > 0f) || !(actualDamage > 0f) ||
            float.IsInfinity(actualDamage) || !(percent > 0f)) return 0f;
        return Math.Max(0f, Math.Min(maximum - health, actualDamage * Math.Min(percent, 100f) / 100f));
    }
}

public sealed class DashState
{
    private float remaining;
    private float speed;
    private float readyAt;
    public bool Active => remaining > 0f;
    public float CooldownRemaining(float now) => Math.Max(0f, readyAt - now);
    public bool TryStart(float now, float distance, float duration, float cooldown, bool freshPress, bool allowed)
    {
        if (!allowed || !freshPress || Active || now < readyAt || distance <= 0f || duration <= 0f) return false;
        remaining = duration;
        speed = distance / duration;
        readyAt = now + Math.Max(0f, cooldown);
        return true;
    }
    public float Step(float seconds)
    {
        float used = Math.Min(remaining, Math.Max(0f, seconds));
        remaining -= used;
        return speed * used;
    }
    public void Cancel() => remaining = 0f;
    public void Reset() { Cancel(); readyAt = 0f; }
}
