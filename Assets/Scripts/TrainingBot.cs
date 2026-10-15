using UnityEngine;

// Reuses the existing actor-health component; child colliders resolve to it normally.
[RequireComponent(typeof(PlayerHealth))]
[DisallowMultipleComponent]
public sealed class TrainingBot : MonoBehaviour
{
    [SerializeField] private Transform muzzle;
    [SerializeField] private StraightProjectile projectilePrefab;
    [SerializeField, Min(0.01f)] private float firingInterval = 1f;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 4f;
    [SerializeField, Min(0f)] private float projectileDamage = 25f;
    [SerializeField, Min(0.01f)] private float projectileLifetime = 5f;
    private PlayerHealth health;
    private float nextShot;
    private bool defeated;

    private void Awake() => health = GetComponent<PlayerHealth>();
    private void OnEnable()
    {
        health.HealthChanged += OnHealthChanged;
        nextShot = Time.time + Mathf.Max(0.01f, firingInterval);
        OnHealthChanged(health.CurrentHealth, health.MaximumHealth);
    }
    private void OnDisable() => health.HealthChanged -= OnHealthChanged;

    private void OnHealthChanged(float current, float maximum)
    {
        if (current > 0f || defeated)
            return;
        defeated = true;
        // Stop now rather than waiting for deferred Destroy at end of frame.
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void Update()
    {
        if (defeated || health.CurrentHealth <= 0f || muzzle == null || projectilePrefab == null ||
            Time.time < nextShot)
            return;
        nextShot = Time.time + Mathf.Max(0.01f, firingInterval);
        StraightProjectile shot = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
        shot.Launch(transform, muzzle.forward, projectileSpeed, projectileDamage, projectileLifetime);
    }
}
