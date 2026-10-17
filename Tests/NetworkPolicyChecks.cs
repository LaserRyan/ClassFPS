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
        var gun = new NetworkRevolverState();
        gun.Reset(3, 2);
        Check(!gun.Request(2, 1, 0, true), "old life request rejected");
        Check(!gun.Request(3, 1, 0, false), "non-owner combat rejected");
        Check(gun.Request(3, 1, 0, true), "fresh owner request");
        Check(!gun.Request(3, 1, 1, true), "duplicate sequence rejected");
        Check(gun.Fire(0, .4f, 2, 1.5f, true) && gun.Ammo == 1, "accepted fire consumes once");
        Check(!gun.Fire(.2, .4f, 2, 1.5f, true) && gun.Ammo == 1, "cooldown rejects without spending");
        Check(gun.Fire(.5, .4f, 2, 1.5f, true) && gun.Ammo == 0 && gun.ReloadAt == 2, "empty starts automatic reload");
        Check(!gun.Fire(1, .4f, 2, 1.5f, true), "reload blocks fire");
        gun.Tick(1.99, 2);
        Check(gun.Ammo == 0, "reload cannot finish early");
        gun.Tick(2, 2);
        Check(gun.Ammo == 2 && gun.ReloadAt == 0, "reload deadline refills");
        Check(gun.Request(3, 2, 3, true) && !gun.Request(3, 3, 3.001, true), "request flood gate");
        Check(gun.Die(4, 3) && !gun.Die(5, 3) && gun.RespawnAt == 7, "death enters once without extending timer");
        Check(!gun.Request(3, 4, 6, true) && !gun.Fire(6, 0, 2, 1, true), "dead attacker rejected");
        gun.Reset(4, 6);
        Check(!gun.Dead && gun.Ammo == 6 && gun.ReloadAt == 0, "respawn/hero change resets resources and timer");
        Check(!gun.Request(3, 999, 8, true) && gun.Request(4, 1, 8, true), "old life cannot poison new sequence");
        Check(!gun.Reload(8, 6, 1), "full magazine rejects reload");
        Check(gun.Fire(8, .4f, 6, 1, false) && gun.Reload(8.5, 6, 1), "manual reload accepted after spending");
        Check(!gun.Reload(8.6, 6, 1) && gun.ReloadAt == 9.5, "repeated reload cannot restart timer");
        float actual = NetworkRevolverState.ActualDamage(10, 25);
        Check(actual == 10 && GunslingerRules.Healing(actual, 15, 50, 100, true, false) == 1.5f, "network killing hit heals actual remaining health");
        Check(NetworkRevolverState.ActualDamage(100, float.Epsilon) == 0, "unrepresentable damage yields zero");
        Check(NetworkRevolverState.ActualDamage(0, 25) == 0 && NetworkRevolverState.ActualDamage(10, float.NaN) == 0, "dead/invalid damage yields zero");
        Check(GunslingerRules.Healing(25, 15, 99, 100, true, false) == 1, "network lifesteal clamps at full");
        Check(GunslingerRules.Healing(25, 15, 0, 100, false, false) == 0, "network lifesteal never revives");
        health.NetworkProtected = true;
        health.ApplyNetworkSnapshot(20, 120);
        Check(health.CurrentHealth == 20 && health.MaximumHealth == 120, "replica updates through normal health state");
        Console.WriteLine(checks + " network policy checks passed (development features: " + dev + ").");
    }
}
