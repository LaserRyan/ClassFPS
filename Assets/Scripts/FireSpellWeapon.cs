using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireSpellWeapon : Weapon
{
    [SerializeField] private Transform castOrigin;
    private Transform owner;
    private FPSController movement;
    private Ray acceptedAim;
    private float releaseAt;
    private float castStarted;
    public override WeaponAmmo Ammo => null;
    public FireSpellDefinition Spell => Definition as FireSpellDefinition;
    public bool IsCasting { get; private set; }
    public float CastProgress => !IsCasting || Spell == null ? 0f :
        Spell.CastDelay <= 0f ? 1f : Mathf.Clamp01((Time.time - castStarted) / Spell.CastDelay);
    public event Action CastStarted;
    public event Action CastCancelled;
    public event Action ProjectileReleased;

    public override void PrimaryAttack(WeaponAttackContext context)
    {
        FPSController controller = context.Owner != null ? context.Owner.GetComponent<FPSController>() : null;
        if (IsCasting || Spell == null || Spell.ProjectilePrefab == null || context.Owner == null ||
            (controller != null && !controller.CanAcceptGameplayInput) || !TryBeginAttack())
            return;
        owner = context.Owner;
        movement = controller;
        acceptedAim = context.AimRay;
        castStarted = Time.time;
        releaseAt = castStarted + Spell.CastDelay;
        IsCasting = true;
        CastStarted?.Invoke();
        if (Spell.CastDelay <= 0f && IsCasting)
            Release();
    }

    private void LateUpdate()
    {
        if (!IsCasting)
            return;
        if (owner == null || !owner.gameObject.activeInHierarchy || Spell == null ||
            (movement != null && !movement.CanAcceptGameplayInput))
        {
            ResetAttackState();
            return;
        }
        if (Time.time >= releaseAt)
            Release();
    }

    private void Release()
    {
        FireSpellDefinition config = Spell;
        if (config == null || config.ProjectilePrefab == null)
        {
            ResetAttackState();
            return;
        }
        Ray aim = movement != null && movement.PlayerCamera != null ?
            new Ray(movement.PlayerCamera.transform.position, movement.PlayerCamera.transform.forward) : acceptedAim;
        Vector3 aimPoint = aim.GetPoint(Range);
        float nearest = Range;
        foreach (RaycastHit hit in Physics.RaycastAll(aim, Range, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            if (owner != null && hit.collider.transform.IsChildOf(owner))
                continue;
            if (hit.distance >= nearest)
                continue;
            nearest = hit.distance;
            aimPoint = hit.point;
        }
        Vector3 origin = castOrigin != null ? castOrigin.position : aim.origin;
        bool blocked = ProjectileCollision.TryHit(aim.origin, origin, config.CollisionRadius,
            owner, Physics.DefaultRaycastLayers, out Collider obstacle, out Vector3 safeOrigin);
        if (blocked)
            origin = safeOrigin;
        Vector3 direction = aimPoint - origin;
        if (direction.sqrMagnitude < 0.000001f)
            direction = aim.direction;
        StraightProjectile shot = Instantiate(config.ProjectilePrefab, origin, Quaternion.LookRotation(direction));
        shot.Impacted += OnProjectileImpact;
        shot.Launch(owner, direction, config.ProjectileSpeed, Damage, config.ProjectileLifetime, config.CollisionRadius);
        IsCasting = false;
        // Cooldown starts at release, including immediately blocked shots.
        RestartCooldown();
        ReportAttack(false);
        ProjectileReleased?.Invoke();
        if (blocked)
            shot.ImpactImmediately(obstacle);
    }

    private void OnProjectileImpact(Vector3 position, bool dealtDamage)
    {
        // Launched shots outlive their caster; Unity's destroyed-object check is intentional.
        if (this != null && isActiveAndEnabled && dealtDamage)
            ReportAttack(true);
    }

    public override void ResetAttackState()
    {
        base.ResetAttackState();
        bool cancelled = IsCasting;
        IsCasting = false;
        owner = null;
        movement = null;
        if (cancelled)
            CastCancelled?.Invoke();
    }
    private void OnDisable() => ResetAttackState();
}
