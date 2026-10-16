using System;

// These shims run the real health/rules sources without native Unity. They do not test Unity lifecycle/physics.
namespace UnityEngine
{
    public class SerializeField : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class DisallowMultipleComponent : Attribute { }
    public class GameObject { public void SetActive(bool value) { } }
    public class MonoBehaviour
    {
        public bool isActiveAndEnabled = true;
        public GameObject gameObject = new GameObject();
        public T GetComponent<T>() where T : class => null;
    }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Clamp(float v, float a, float b) => Math.Min(b, Math.Max(a, v));
    }
}
public class BurnStatus { public void Clear() { } }

public static class GunslingerChecks
{
    private static int checks;
    private static void Equal(float expected, float actual, string label)
    {
        checks++;
        if (Math.Abs(expected - actual) > 0.0001f || float.IsNaN(actual))
            throw new Exception(label + ": " + actual + " expected " + expected);
    }
    private static void Is(bool value, string label) => Equal(1f, value ? 1f : 0f, label);
    public static void Main()
    {
        var target = new TargetHealth();
        typeof(TargetHealth).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(target, null);
        Equal(90f, target.ApplyHealthDamage(90f), "target accepted damage");
        float kill = target.ApplyHealthDamage(25f);
        Equal(10f, kill, "target overkill result");
        Equal(0f, target.ApplyHealthDamage(25f), "dead target");
        var owner = new PlayerHealth();
        Equal(0f, owner.ApplyHealthDamage(float.Epsilon), "unrepresentable health change reports zero");
        owner.TakeDamage(50f);
        int changes = 0;
        owner.HealthChanged += (current, maximum) => changes++;
        float heal = GunslingerRules.Healing(kill, 15f, owner.CurrentHealth, owner.MaximumHealth, true, false);
        Equal(1.5f, heal, "killing shot heals actual damage");
        owner.Heal(heal);
        Equal(51.5f, owner.CurrentHealth, "partial heal");
        Equal(1f, changes, "normal health notification");
        Equal(0f, GunslingerRules.Healing(0f, 15f, 50f, 100f, true, false), "zero or blocked result");
        Equal(0f, GunslingerRules.Healing(25f, 15f, 50f, 100f, true, true), "self damage");
        Equal(0f, GunslingerRules.Healing(25f, 15f, 50f, 100f, false, false), "disabled attacker");
        Equal(2f, GunslingerRules.Healing(25f, 15f, 98f, 100f, true, false), "full health cap");
        owner.Heal(1000f);
        Equal(100f, owner.CurrentHealth, "health clamp");
        Is(!owner.Heal(1f), "full health no change");
        owner.DamageBlocked = true;
        Equal(0f, owner.ApplyHealthDamage(25f), "blocked damage result");
        owner.DamageBlocked = false;
        Equal(100f, owner.ApplyHealthDamage(200f), "actor overkill result");
        Is(!owner.Heal(100f), "healing cannot revive");
        Equal(0f, GunslingerRules.Healing(25f, 15f, 0f, 100f, true, false), "dead attacker policy");
        Equal(0f, owner.ApplyHealthDamage(float.NaN), "invalid damage");
        var dash = new DashState();
        Is(!dash.TryStart(0f, 5f, .2f, 5f, false, true), "held input rejected");
        Is(!dash.TryStart(0f, 5f, .2f, 5f, true, false), "blocked input rejected");
        Is(dash.TryStart(0f, 5f, .2f, 5f, true, true), "fresh dash");
        Equal(5f, dash.CooldownRemaining(0f), "cooldown starts at activation");
        Equal(2.5f, dash.Step(.1f), "partial movement");
        Is(!dash.TryStart(.1f, 5f, .2f, 5f, true, true), "active cannot overlap");
        Equal(2.5f, dash.Step(1f), "long frame travel capped");
        Is(!dash.Active, "duration expires");
        Is(!dash.TryStart(4f, 5f, .2f, 5f, true, true), "cooldown rejects");
        Equal(0f, dash.Step(.1f), "no queued dash");
        Is(dash.TryStart(5f, 5f, .2f, 5f, true, true), "ready at deadline");
        dash.Cancel();
        Equal(0f, dash.Step(.1f), "cancellation stops travel");
        Equal(4f, dash.CooldownRemaining(6f), "cancel preserves cooldown");
        dash.Reset();
        Equal(0f, dash.CooldownRemaining(6f), "respawn resets cooldown");
        Is(dash.TryStart(6f, 5f, .2f, 5f, true, true), "ready after respawn");
        Console.WriteLine(checks + " Gunslinger checks passed.");
    }
}
