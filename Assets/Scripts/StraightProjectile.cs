using System;
using UnityEngine;

// Collider-free visual plus explicit swept physics queries. No player/input assumptions.
[DisallowMultipleComponent]
public sealed class StraightProjectile : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.12f;
    [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;
    private Transform owner;
    private Vector3 direction;
    private float speed;
    private float damage;
    private float remainingLife;
    private float radius;
    private bool launched;
    private bool consumed;
    public event Action<float> Launched;
    public event Action<Vector3, bool> Impacted;

    // Existing bot calls retain their serialized radius; spell launches can override per instance.
    public void Launch(Transform firingOwner, Vector3 forward, float projectileSpeed,
        float projectileDamage, float lifetime, float radiusOverride = 0f)
    {
        owner = firingOwner;
        direction = forward.normalized;
        speed = Mathf.Max(0f, projectileSpeed);
        damage = Mathf.Max(0f, projectileDamage);
        remainingLife = Mathf.Max(0f, lifetime);
        radius = Mathf.Max(0.01f, radiusOverride > 0f ? radiusOverride : collisionRadius);
        consumed = false;
        launched = true;
        Launched?.Invoke(radius);
    }

    private void FixedUpdate()
    {
        if (!launched || consumed)
            return;
        if (remainingLife <= 0f)
        {
            Consume(null);
            return;
        }
        float step = Mathf.Min(Time.fixedDeltaTime, remainingLife);
        Vector3 end = transform.position + direction * (speed * step);
        if (ProjectileCollision.TryHit(transform.position, end, radius, owner, hitLayers,
            out Collider surface, out Vector3 center, transform))
        {
            transform.position = center;
            Consume(surface);
            return;
        }
        transform.position = end;
        remainingLife -= step;
        if (remainingLife <= 0f)
            Consume(null);
    }

    // Used when a cast is blocked before its muzzle: consume at the safe first contact.
    public void ImpactImmediately(Collider surface)
    {
        if (launched)
            Consume(surface);
    }

    private void Consume(Collider collider)
    {
        if (consumed)
            return;
        consumed = true;
        bool dealtDamage = collider != null &&
            (collider.GetComponentInParent<IDamageable>()?.TakeDamage(damage) ?? false);
        if (collider != null)
            Impacted?.Invoke(transform.position, dealtDamage);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
