using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WeaponAmmo))]
public sealed class HitscanGun : Weapon
{
    [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;

    // Presentation-only notification. The shared weapon base stays independent of guns.
    public event System.Action<Vector3> ShotFired;
    public event System.Action<Transform, IDamageable, float> HealthDamageDealt;
    private void OnDisable() => Ammo?.CancelReload();

    public override void PrimaryAttack(WeaponAttackContext context)
    {
        if (!TryBeginAttack())
            return;

        // Start at the camera itself, rather than its near plane, so nearby walls block shots.
        Ray ray = context.AimRay;
        RaycastHit[] hits = Physics.RaycastAll(ray, Range, hitLayers, QueryTriggerInteraction.Ignore);
        RaycastHit nearest = default;
        float nearestDistance = float.PositiveInfinity;
        bool foundSurface = false;
        foreach (RaycastHit hit in hits)
        {
            if (context.Owner != null && hit.collider.transform.IsChildOf(context.Owner))
                continue;
            if (hit.distance >= nearestDistance)
                continue;

            nearest = hit;
            nearestDistance = hit.distance;
            foundSurface = true;
        }

        bool dealtDamage = false;
        if (foundSurface)
        {
            // Search upward so a child collider can belong to a health component on its root.
            IDamageable target = nearest.collider.GetComponentInParent<IDamageable>();
            if (target is IHealthDamageReceiver receiver)
            {
                float actual = receiver.ApplyHealthDamage(Damage);
                dealtDamage = actual > 0f;
                if (dealtDamage)
                    HealthDamageDealt?.Invoke(context.Owner, target, actual);
            }
            else if (target != null)
                dealtDamage = target.TakeDamage(Damage);
        }
        ReportAttack(dealtDamage);
        ShotFired?.Invoke(foundSurface ? nearest.point : ray.GetPoint(Range));
    }
}
