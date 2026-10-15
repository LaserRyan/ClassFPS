using UnityEngine;

[DisallowMultipleComponent]
public sealed class TargetHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    public float CurrentHealth { get; private set; }

    private void OnEnable() => CurrentHealth = Mathf.Max(1f, maximumHealth);

    public bool TakeDamage(float amount)
    {
        if (!isActiveAndEnabled || CurrentHealth <= 0f || amount <= 0f)
            return false;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        if (CurrentHealth <= 0f)
            gameObject.SetActive(false);
        return true;
    }
}
