using System;

// Deterministic per-instance state; no Unity clocks, input, UI, or shared asset mutation.
public sealed class HeatAmmoState
{
    private const float EmptyThreshold = 0.00001f;
    private readonly float regenerationDelay;
    private readonly float regenerationRate;
    private readonly float overheatDuration;
    private float lastAdvanced;
    private float lastConsumed;
    private float overheatUntil;
    public float Maximum { get; }
    public float Current { get; private set; }
    public bool IsOverheated { get; private set; }
    public bool RequiresRelease { get; private set; }
    public bool CanAttack => !IsOverheated && !RequiresRelease;
    public float OverheatRemaining => IsOverheated ? Math.Max(0f, overheatUntil - lastAdvanced) : 0f;
    public event Action Changed;

    public HeatAmmoState(float maximum, float delay, float rate, float lockout, float now)
    {
        Maximum = Positive(maximum, 100f);
        regenerationDelay = Nonnegative(delay, 2f);
        regenerationRate = Nonnegative(rate, 25f);
        overheatDuration = Positive(lockout, 3f);
        Current = Maximum;
        lastAdvanced = lastConsumed = now;
    }

    public void Advance(float now)
    {
        if (float.IsNaN(now) || now <= lastAdvanced)
            return;
        float start = lastAdvanced;
        float previous = Current;
        bool wasHot = IsOverheated;
        float regenerationSeconds;
        if (wasHot)
        {
            regenerationSeconds = Math.Max(0f, Math.Min(now, overheatUntil) - start);
            if (now >= overheatUntil)
            {
                IsOverheated = false;
                regenerationSeconds += Math.Max(0f, now - Math.Max(Math.Max(start, overheatUntil),
                    lastConsumed + regenerationDelay));
            }
        }
        else
            regenerationSeconds = Math.Max(0f, now - Math.Max(start, lastConsumed + regenerationDelay));
        Current = Math.Min(Maximum, Current + regenerationRate * regenerationSeconds);
        lastAdvanced = now;
        // During lockout notify countdown observers even when the pool is already full.
        if (previous != Current || wasHot)
            Changed?.Invoke();
    }

    public bool CanSpend(float amount, float now)
    {
        Advance(now);
        return CanAttack && ValidAmount(amount) && Current >= amount;
    }

    public bool TrySpend(float amount, float now)
    {
        if (!CanSpend(amount, now))
            return false;
        Spend(amount, now);
        return true;
    }

    public float SpendFiringTime(float rate, float requestedSeconds, float now)
    {
        Advance(now);
        if (!CanAttack || !ValidAmount(rate) || !ValidAmount(requestedSeconds))
            return 0f;
        float fundedSeconds = Math.Min(requestedSeconds, Current / rate);
        if (fundedSeconds > 0f)
            Spend(fundedSeconds * rate, now);
        return fundedSeconds;
    }

    private void Spend(float amount, float now)
    {
        Current = Math.Max(0f, Current - amount);
        lastConsumed = now;
        if (Current <= EmptyThreshold)
        {
            Current = 0f;
            // A single timestamp: no repeated timers or extension by rejected attempts/refunds.
            if (!IsOverheated)
            {
                IsOverheated = true;
                RequiresRelease = true;
                overheatUntil = now + overheatDuration;
            }
        }
        Changed?.Invoke();
    }

    public void Refund(float amount, float now)
    {
        Advance(now);
        if (!ValidAmount(amount))
            return;
        Current = Math.Min(Maximum, Current + amount);
        // Refunds do not count as consumption and never restart or erase a lockout.
        Changed?.Invoke();
    }

    public void ObserveButtons(bool primaryHeld, bool secondaryHeld, float now)
    {
        Advance(now);
        if (!IsOverheated && RequiresRelease && !primaryHeld && !secondaryHeld)
        {
            RequiresRelease = false;
            Changed?.Invoke();
        }
    }

    public void ResetFull(float now)
    {
        Current = Maximum;
        IsOverheated = RequiresRelease = false;
        overheatUntil = 0f;
        lastAdvanced = lastConsumed = now;
        Changed?.Invoke();
    }

    private static bool ValidAmount(float value) => value > 0f && !float.IsInfinity(value);
    private static float Positive(float value, float fallback) => ValidAmount(value) ? value : fallback;
    private static float Nonnegative(float value, float fallback) =>
        value >= 0f && !float.IsInfinity(value) ? value : fallback;
}
