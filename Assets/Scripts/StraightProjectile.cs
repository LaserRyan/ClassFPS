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
    private bool launched;
    private bool consumed;

    public void Launch(Transform firingOwner, Vector3 forward, float projectileSpeed,
        float projectileDamage, float lifetime)
    {
        owner = firingOwner;
        direction = forward.normalized;
        speed = Mathf.Max(0f, projectileSpeed);
        damage = Mathf.Max(0f, projectileDamage);
        remainingLife = Mathf.Max(0f, lifetime);
        consumed = false;
        launched = true;
    }

    private bool Ignore(Collider collider) => collider.transform.IsChildOf(transform) ||
        (owner != null && collider.transform.IsChildOf(owner));

    private void FixedUpdate()
    {
        if (!launched || consumed)
            return;
        if (remainingLife <= 0f)
        {
            Consume(null);
            return;
        }
        float radius = Mathf.Max(0.01f, collisionRadius);
        Vector3 origin = transform.position;
        // Sphere casts alone do not report colliders overlapping their starting volume.
        Collider[] overlaps = Physics.OverlapSphere(origin, radius, hitLayers, QueryTriggerInteraction.Ignore);
        // If spawned in both cover and a receiver, cover wins. Never damage through it.
        foreach (Collider collider in overlaps)
            if (!Ignore(collider) && collider.GetComponentInParent<IDamageable>() == null)
            {
                Consume(collider);
                return;
            }
        foreach (Collider collider in overlaps)
            if (!Ignore(collider))
            {
                Consume(collider);
                return;
            }
        float step = Mathf.Min(Time.fixedDeltaTime, remainingLife);
        float distance = speed * step;
        RaycastHit nearest = default;
        float nearestDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in Physics.SphereCastAll(origin, radius, direction, distance,
            hitLayers, QueryTriggerInteraction.Ignore))
        {
            if (Ignore(hit.collider) || hit.distance >= nearestDistance)
                continue;
            nearest = hit;
            nearestDistance = hit.distance;
        }
        // A concave mesh may be back-facing from the firing side. Sweep back along
        // the same segment too, without changing the project's global query settings.
        foreach (RaycastHit hit in Physics.SphereCastAll(origin + direction * distance,
            radius, -direction, distance, hitLayers, QueryTriggerInteraction.Ignore))
        {
            float forwardDistance = Mathf.Clamp(distance - hit.distance, 0f, distance);
            if (Ignore(hit.collider) || forwardDistance >= nearestDistance)
                continue;
            nearest = hit;
            nearestDistance = forwardDistance;
        }
        if (nearestDistance < float.PositiveInfinity)
        {
            transform.position = origin + direction * nearestDistance;
            Consume(nearest.collider);
            return;
        }
        transform.position = origin + direction * distance;
        remainingLife -= step;
        if (remainingLife <= 0f)
            Consume(null);
    }

    private void Consume(Collider collider)
    {
        if (consumed)
            return;
        consumed = true; // Guard before callbacks, including destruction and multiple hitboxes.
        if (collider != null)
            collider.GetComponentInParent<IDamageable>()?.TakeDamage(damage);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
