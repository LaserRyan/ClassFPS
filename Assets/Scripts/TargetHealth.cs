using UnityEngine;

[DisallowMultipleComponent]
public sealed class TargetHealth : MonoBehaviour, IHealthDamageReceiver
{
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    public float CurrentHealth { get; private set; }

    private void OnEnable() => CurrentHealth = Mathf.Max(1f, maximumHealth);

    public bool TakeDamage(float amount) => ApplyHealthDamage(amount) > 0f;

    public float ApplyHealthDamage(float amount)
    {
        if (!isActiveAndEnabled || CurrentHealth <= 0f || !(amount > 0f) || float.IsInfinity(amount))
            return 0f;

        float next = Mathf.Max(0f, CurrentHealth - amount);
        float removed = CurrentHealth - next;
        CurrentHealth = next;
        if (CurrentHealth <= 0f)
            gameObject.SetActive(false);
        return removed;
    }
}
