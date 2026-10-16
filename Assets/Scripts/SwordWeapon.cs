using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SwordWeapon : Weapon
{
    [Tooltip("Full angle in degrees of the melee volume around the camera's forward direction.")]
    [SerializeField, Range(1f, 180f)] private float attackArc = 100f;
    [Tooltip("Seconds before damage can begin.")]
    [SerializeField, Min(0f)] private float windup = 0.18f;
    [Tooltip("Seconds during which the melee volume can damage each receiver once.")]
    [SerializeField, Min(0.01f)] private float activeDuration = 0.16f;
    [Tooltip("Seconds after the active phase before another swing is allowed.")]
    [SerializeField, Min(0f)] private float recovery = 0.3f;
    private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();
    private Transform owner;
    private FPSController movement;
    private WeaponAttackContext attack;
    private float startedAt;
    private float previousElapsed;
    public event System.Action SwingStarted;
    public override WeaponAmmo Ammo => null; // Melee never consumes or reloads ammo.
    public bool IsSwinging { get; private set; }
    private SwordWeaponDefinition SwordDefinition => Definition as SwordWeaponDefinition;
    public float AttackArc => SwordDefinition != null ? SwordDefinition.AttackArc : Mathf.Clamp(attackArc, 1f, 180f);
    public float Windup => SwordDefinition != null ? SwordDefinition.Windup : Mathf.Max(0f, windup);
    public float ActiveDuration => SwordDefinition != null ? SwordDefinition.ActiveDuration : Mathf.Max(0.01f, activeDuration);
    public float Recovery => SwordDefinition != null ? SwordDefinition.Recovery : Mathf.Max(0f, recovery);
    public float SwingElapsed => IsSwinging ? Mathf.Max(0f, Time.time - startedAt) : 0f;

    private void Reset()
    {
        damage = 35f;
        range = 2.2f; // Shared weapon range is melee reach, measured from the camera.
        attackCooldown = 0f; // The full swing already gates subsequent attacks.
    }

    public override void PrimaryAttack(WeaponAttackContext context)
    {
        FPSController controller = context.Owner != null ? context.Owner.GetComponent<FPSController>() : null;
        if (IsSwinging || (controller != null && !controller.CanAcceptGameplayInput) || !TryBeginAttack())
            return;
        attack = context;
        owner = context.Owner;
        movement = controller;
        damaged.Clear();
        startedAt = Time.time;
        previousElapsed = -1f;
        IsSwinging = true;
        ReportAttack(false);
        SwingStarted?.Invoke();
    }

    private void LateUpdate()
    {
        if (!IsSwinging)
            return;
        if (owner == null || !owner.gameObject.activeInHierarchy ||
            (movement != null && !movement.CanAcceptGameplayInput))
        {
            CancelSwing();
            return;
        }
        float elapsed = SwingElapsed;
        // Include a frame that crosses the entire active interval at a low frame rate.
        if (elapsed >= Windup && previousElapsed < Windup + ActiveDuration)
        {
            Ray aim = attack.AimRay;
            if (movement != null && movement.PlayerCamera != null)
                aim = new Ray(movement.PlayerCamera.transform.position, movement.PlayerCamera.transform.forward);
            DetectDamage(aim);
        }
        previousElapsed = elapsed;
        if (elapsed >= Windup + ActiveDuration + Recovery)
            CancelSwing();
    }

    private bool IsOwner(Collider collider) => owner != null && collider.transform.IsChildOf(owner);

    private void DetectDamage(Ray aim)
    {
        float reach = Mathf.Max(0.01f, Range);
        // A spherical sector is a volume, so off-center and child colliders can be struck.
        Collider[] candidates = Physics.OverlapSphere(aim.origin, reach,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider candidate in candidates)
        {
            if (IsOwner(candidate))
                continue;
            IDamageable target = candidate.GetComponentInParent<IDamageable>();
            if (target == null || damaged.Contains(target))
                continue;
            if (!TryGetContactPoint(candidate, aim.origin, out Vector3 point))
                continue;
            Vector3 offset = point - aim.origin;
            if (offset.sqrMagnitude > reach * reach)
                continue;
            if (Vector3.Angle(aim.direction, offset) > AttackArc * 0.5f)
                continue;
            if (IsBlocked(aim.origin, offset, target, candidates))
                continue;
            // Record before callbacks, including a target with several child colliders.
            damaged.Add(target);
            if (target.TakeDamage(Damage))
                ReportAttack(true);
            if (!IsSwinging || !isActiveAndEnabled)
                return;
        }
    }

    private bool IsBlocked(Vector3 origin, Vector3 offset, IDamageable target, Collider[] nearby)
    {
        // Raycasts miss containing colliders: explicitly reject an origin inside scenery.
        foreach (Collider collider in nearby)
            if (!IsOwner(collider) && SupportsClosestPoint(collider) &&
                collider.GetComponentInParent<IDamageable>() != target &&
                (collider.ClosestPoint(origin) - origin).sqrMagnitude < 0.000001f)
                return true;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, offset.normalized,
            offset.magnitude + 0.001f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (!IsOwner(hit.collider) && hit.collider.GetComponentInParent<IDamageable>() != target)
                return true;
        // Check the reverse segment too: non-convex meshes may have back-facing
        // triangles from the camera side, including when the camera is inside one.
        foreach (RaycastHit hit in Physics.RaycastAll(origin + offset, -offset.normalized,
            offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (!IsOwner(hit.collider) && hit.collider.GetComponentInParent<IDamageable>() != target)
                return true;
        return false;
    }

    private static bool SupportsClosestPoint(Collider collider) =>
        collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider ||
        (collider is MeshCollider mesh && mesh.convex);

    private static bool TryGetContactPoint(Collider collider, Vector3 origin, out Vector3 point)
    {
        if (SupportsClosestPoint(collider))
        {
            point = collider.ClosestPoint(origin);
            return true;
        }
        // Concave meshes and terrain do not support ClosestPoint. Probe their
        // actual surface rather than treating their bounding box as a hitbox.
        Vector3 boundsPoint = collider.ClosestPointOnBounds(origin);
        if (TrySurfacePoint(collider, origin, boundsPoint, out point))
            return true;
        return TrySurfacePoint(collider, origin, collider.bounds.center, out point);
    }

    private static bool TrySurfacePoint(Collider collider, Vector3 origin, Vector3 destination,
        out Vector3 point)
    {
        Vector3 offset = destination - origin;
        if (offset.sqrMagnitude > 0.000001f && collider.Raycast(
            new Ray(origin, offset.normalized), out RaycastHit hit,
            offset.magnitude + collider.bounds.size.magnitude + 0.01f))
        {
            point = hit.point;
            return true;
        }
        point = default;
        return false;
    }

    private void OnDisable() => CancelSwing();
    public override void ResetAttackState()
    {
        base.ResetAttackState();
        CancelSwing();
    }
    private void CancelSwing()
    {
        IsSwinging = false;
        damaged.Clear();
        owner = null;
        movement = null;
    }
}
