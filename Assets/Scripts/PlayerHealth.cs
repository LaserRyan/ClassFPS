using System;
using UnityEngine;

// Actor state only: no input, HUD, death, or local-player assumptions.
[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    private float currentHealth;
    private bool initialized;
    public bool DamageBlocked { get; set; }

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

    public bool TakeDamage(float amount)
    {
        Initialize();
        if (DamageBlocked || !isActiveAndEnabled || !IsPositiveFinite(amount) || currentHealth <= 0f)
            return false;
        return SetCurrent(currentHealth - amount);
    }

    public bool RestoreFullHealth()
    {
        Initialize();
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
