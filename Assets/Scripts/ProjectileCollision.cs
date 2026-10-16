using UnityEngine;

// Shared volume query: owner filtering, starting overlaps, and nearest swept impact.
public static class ProjectileCollision
{
    public static bool TryHit(Vector3 start, Vector3 end, float radius, Transform owner,
        LayerMask layers, out Collider surface, out Vector3 center, Transform projectile = null)
    {
        surface = null;
        center = start;
        Collider[] overlaps = Physics.OverlapSphere(start, radius, layers, QueryTriggerInteraction.Ignore);
        foreach (Collider collider in overlaps)
            if (!Ignore(collider, owner, projectile) && collider.GetComponentInParent<IDamageable>() == null)
            {
                surface = collider;
                return true;
            }
        foreach (Collider collider in overlaps)
            if (!Ignore(collider, owner, projectile))
            {
                surface = collider;
                return true;
            }
        Vector3 offset = end - start;
        float distance = offset.magnitude;
        if (distance <= 0.000001f)
            return false;
        Vector3 direction = offset / distance;
        float nearest = float.PositiveInfinity;
        foreach (RaycastHit hit in Physics.SphereCastAll(start, radius, direction, distance,
            layers, QueryTriggerInteraction.Ignore))
        {
            if (Ignore(hit.collider, owner, projectile) || hit.distance >= nearest)
                continue;
            surface = hit.collider;
            nearest = hit.distance;
        }
        // Reverse sweep catches back-facing concave mesh surfaces without global settings changes.
        foreach (RaycastHit hit in Physics.SphereCastAll(end, radius, -direction, distance,
            layers, QueryTriggerInteraction.Ignore))
        {
            float forwardDistance = Mathf.Clamp(distance - hit.distance, 0f, distance);
            if (Ignore(hit.collider, owner, projectile) || forwardDistance >= nearest)
                continue;
            surface = hit.collider;
            nearest = forwardDistance;
        }
        if (surface == null)
            return false;
        center = start + direction * nearest;
        return true;
    }

    private static bool Ignore(Collider collider, Transform owner, Transform projectile) =>
        (projectile != null && collider.transform.IsChildOf(projectile)) ||
        (owner != null && collider.transform.IsChildOf(owner));
}
