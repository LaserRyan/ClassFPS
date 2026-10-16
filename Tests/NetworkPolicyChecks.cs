using System;

public static class NetworkPolicyChecks
{
    private static int checks;
    private static void Check(bool value, string label)
    { checks++; if (!value) throw new Exception(label); }
    public static void Main()
    {
        bool dev = NetworkTestRules.DevelopmentFeatures;
        Check(NetworkTestRules.CanChangeHero(true, true, true, false, 1, 1) == dev, "compiled development gate");
        Check(!NetworkTestRules.CanChangeHero(false, true, true, false, 1, 1), "server setting disabled");
        Check(!NetworkTestRules.CanChangeHero(true, false, true, false, 1, 1), "wrong owner");
        Check(!NetworkTestRules.CanChangeHero(true, true, false, false, 1, 1), "unknown hero");
        Check(!NetworkTestRules.CanChangeHero(true, true, true, true, 1, 1), "concurrent request");
        Check(!NetworkTestRules.CanChangeHero(true, true, true, false, 1, 2), "stale request");
        Check(!NetworkTestRules.CanChangeHero(true, true, true, false, 3, 2), "future revision rejected");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Check(dev, "Editor/development builds support explicitly enabled test changes");
#else
        Check(!dev, "release cannot enable development features");
#endif
        var health = new PlayerHealth();
        health.TakeDamage(40f);
        health.NetworkProtected = true;
        int changes = 0;
        health.HealthChanged += (current, max) => changes++;
        Check(!health.TakeDamage(1000f), "offline damage rejected online");
        Check(health.ApplyHealthDamage(25f) == 0f, "actual damage remains zero online");
        Check(!health.Heal(100f), "offline lifesteal/healing rejected online");
        Check(!health.RestoreFullHealth(), "debug restore rejected online");
        Check(health.CurrentHealth == 60f && changes == 0, "no silent health edits/notifications");
        health.SetMaximumHealth(150f, true);
        Check(health.CurrentHealth == 150f, "approved hero configuration restores health");
        health.NetworkProtected = false;
        Check(health.TakeDamage(25f), "offline damage preserved");
        Check(health.Heal(10f), "offline healing preserved");
        Check(health.RestoreFullHealth(), "offline restore preserved");
        Console.WriteLine(checks + " network policy checks passed (development features: " + dev + ").");
    }
}
