using System;

public static class HeatAmmoStateChecks
{
    private static int checks;
    private static void Check(bool condition, string label)
    {
        checks++;
        if (!condition) throw new Exception(label);
    }
    private static void Near(float actual, float expected, string label) =>
        Check(Math.Abs(actual - expected) < 0.001f, label + $" ({actual} versus {expected})");
    private static HeatAmmoState Pool(float maximum = 100f, float delay = 2f,
        float rate = 25f, float duration = 3f) => new HeatAmmoState(maximum, delay, rate, duration, 0f);

    public static void Main()
    {
        HeatAmmoState pool = Pool();
        Near(pool.Current, 100f, "full start");
        Check(pool.TrySpend(25f, 0f), "accept fireball cost");
        Near(pool.Current, 75f, "charged on acceptance");
        Check(!pool.TrySpend(80f, 1f), "insufficient cost rejected");
        pool.Advance(1.99f);
        Near(pool.Current, 75f, "delay prevents regeneration");
        pool.Advance(2.5f);
        Near(pool.Current, 87.5f, "incremental regeneration after delay");
        Check(pool.TrySpend(25f, 2.5f), "second spend");
        pool.Advance(4.49f);
        Near(pool.Current, 62.5f, "consumption restarts delay");
        pool.Advance(4.9f);
        Near(pool.Current, 72.5f, "second incremental regeneration");
        pool.Advance(100f);
        Near(pool.Current, 100f, "regeneration capped");

        pool = Pool();
        Check(pool.TrySpend(100f, 0f), "last-ammo fireball accepted, not rejected");
        Check(pool.IsOverheated && !pool.CanAttack, "zero locks subsequent attacks");
        Near(pool.OverheatRemaining, 3f, "default lockout");
        Check(!pool.TrySpend(1f, 1f), "cannot spend during heat");
        Near(pool.Current, 25f, "overheat regenerates immediately and incrementally");
        Near(pool.OverheatRemaining, 2f, "rejection did not restart timer");
        pool.ObserveButtons(false, false, 1f);
        Check(pool.RequiresRelease, "release latch is not cleared before lockout ends");
        pool.Advance(3f);
        Near(pool.Current, 75f, "useful resource amount restored by lockout end");
        Check(!pool.IsOverheated && !pool.CanAttack, "timer expiry retains release gate");
        pool.ObserveButtons(true, false, 3f);
        Check(!pool.CanAttack, "primary held blocks both attacks");
        pool.ObserveButtons(false, true, 3f);
        Check(!pool.CanAttack, "secondary held blocks both attacks");
        pool.ObserveButtons(false, false, 3f);
        Check(pool.CanAttack && pool.TrySpend(25f, 3f), "both released allows fresh attack");

        pool = Pool(10f);
        float funded = pool.SpendFiringTime(30f, 1f, 0f);
        Near(funded, 1f / 3f, "partial frame funded at exhaustion");
        Near(30f * funded, 10f, "damage scales to funded time, not requested time");
        Check(pool.IsOverheated, "flame exhaustion overheats");
        Near(pool.SpendFiringTime(30f, 1f, 0.5f), 0f, "no flame funding during heat");
        Near(pool.OverheatRemaining, 2.5f, "no duplicate timer");

        pool = Pool();
        Check(pool.TrySpend(25f, 0f), "reserve interrupted cast");
        pool.Refund(25f, 1f);
        Near(pool.Current, 100f, "windup refund");
        pool = Pool();
        Check(pool.TrySpend(100f, 0f), "reserve last-ammo interrupted cast");
        pool.Refund(100f, 0.5f);
        Near(pool.Current, 100f, "refund capped at maximum");
        Check(pool.IsOverheated, "refund preserves existing lockout");
        Near(pool.OverheatRemaining, 2.5f, "refund does not restart lockout");
        pool.ResetFull(1f);
        Check(pool.CanAttack && !pool.IsOverheated && !pool.RequiresRelease, "respawn clears all locks");
        Near(pool.Current, 100f, "respawn refills");
        pool.Advance(20f);
        Check(pool.CanAttack, "old timer cannot return after reset");

        pool = Pool();
        Check(pool.TrySpend(25f, 0f), "shared primary spend");
        Near(pool.SpendFiringTime(30f, 0.5f, 0f), 0.5f, "shared secondary funding");
        Near(pool.Current, 60f, "both attacks consume the same pool");
        Check(!pool.TrySpend(float.NaN, 0f), "NaN cost rejected");
        Near(pool.SpendFiringTime(30f, -1f, 0f), 0f, "invalid firing interval rejected");

        pool = Pool(100f, 10f, 25f, 3f);
        Check(pool.TrySpend(100f, 0f), "long delay emptying");
        pool.Advance(4f);
        Near(pool.Current, 75f, "heat overrides delay only during its interval");
        pool.Advance(10.5f);
        Near(pool.Current, 87.5f, "normal delay resumes after heat");
        Console.WriteLine($"Passed {checks} heat ammo state checks.");
    }
}
