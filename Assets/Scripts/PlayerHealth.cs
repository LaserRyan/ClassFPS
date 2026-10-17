using System;
using UnityEngine;

// Actor state only: no input, HUD, death, or local-player assumptions.
[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour, IHealthDamageReceiver
{
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    private float currentHealth;
    private bool initialized;
    public bool DamageBlocked { get; set; }
    // Presentation-only network milestone: no offline health operation may affect this actor.
    public bool NetworkProtected { get; set; }

    public float MaximumHealth => ValidMaximum(maximumHealth);
    public float CurrentHealth { get { Initialize(); return currentHealth; } }
    public event Action<float, float> HealthChanged;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized)
            return;
        maximumHealth = ValidMaximum(maximumHealth);
        currentHealth = maximumHealth;
        initialized = true;
    }

    public bool TakeDamage(float amount) => ApplyHealthDamage(amount) > 0f;

    public float ApplyHealthDamage(float amount)
    {
        Initialize();
        if (NetworkProtected || DamageBlocked || !isActiveAndEnabled || !IsPositiveFinite(amount) || currentHealth <= 0f)
            return 0f;
        float next = Mathf.Max(0f, currentHealth - amount);
        float removed = currentHealth - next;
        SetCurrent(next);
        return removed;
    }

    public bool Heal(float amount)
    {
        Initialize();
        return !NetworkProtected && isActiveAndEnabled && !DamageBlocked && currentHealth > 0f &&
            IsPositiveFinite(amount) && SetCurrent(currentHealth + amount);
    }

    public bool RestoreFullHealth()
    {
        Initialize();
        if (NetworkProtected) return false;
        GetComponent<BurnStatus>()?.Clear();
        return isActiveAndEnabled && SetCurrent(MaximumHealth);
    }

    // Future actor configuration can provide hero-specific values without UI coupling.
    public void SetMaximumHealth(float value, bool restoreToFull = false)
    {
        Initialize();
        float previousMaximum = MaximumHealth;
        maximumHealth = ValidMaximum(value);
        float next = restoreToFull ? maximumHealth : Mathf.Clamp(currentHealth, 0f, maximumHealth);
        bool changed = next != currentHealth || previousMaximum != maximumHealth;
        currentHealth = next;
        if (changed)
            HealthChanged?.Invoke(currentHealth, maximumHealth);
    }

    // Network adapter supplies server snapshots; ordinary damage/heal stay protected.
    internal void ApplyNetworkSnapshot(float current, float maximum)
    {
        if (!NetworkProtected) return;
        Initialize();
        maximumHealth = ValidMaximum(maximum);
        currentHealth = Mathf.Clamp(current, 0f, maximumHealth);
        HealthChanged?.Invoke(currentHealth, maximumHealth);
    }

    private bool SetCurrent(float value)
    {
        float next = Mathf.Clamp(value, 0f, MaximumHealth);
        if (next == currentHealth)
            return false;
        currentHealth = next;
        HealthChanged?.Invoke(currentHealth, MaximumHealth);
        return true;
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
    private static float ValidMaximum(float value) => IsPositiveFinite(value) ? Mathf.Max(1f, value) : 1f;

    private void OnValidate()
    {
        maximumHealth = ValidMaximum(maximumHealth);
        if (!initialized)
            return;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
        HealthChanged?.Invoke(currentHealth, maximumHealth);
    }
}
