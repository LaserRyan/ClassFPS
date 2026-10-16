using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FireEnergy))]
public sealed class FireSpellWeapon : Weapon
{
    [SerializeField] private Transform castOrigin;
    private Transform owner;
    private FPSController movement;
    private Ray acceptedAim;
    private float releaseAt;
    private float castStarted;
    private float reservedCost;
    private bool flameOwnsAttack;
    private int primaryActivityFrame = -1;
    public FireEnergy Energy => GetComponent<FireEnergy>();
    public override bool UsesHeldSecondary => true;
    public bool IsFlamethrowing { get; private set; }
    public Vector3 FlameStart { get; private set; }
    public Vector3 FlameEnd { get; private set; }
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
        if (IsCasting || flameOwnsAttack || Spell == null || Spell.ProjectilePrefab == null || context.Owner == null ||
            Energy == null || !Energy.isActiveAndEnabled ||
            !Energy.State.CanSpend(Spell.FireballCost, Time.time) ||
            (controller != null && !controller.CanAcceptGameplayInput) || !TryBeginAttack())
            return;
        reservedCost = Spell.FireballCost;
        if (!Energy.State.TrySpend(reservedCost, Time.time))
        {
            reservedCost = 0f;
            return;
        }
        owner = context.Owner;
        movement = controller;
        acceptedAim = context.AimRay;
        castStarted = Time.time;
        releaseAt = castStarted + Spell.CastDelay;
        IsCasting = true;
        primaryActivityFrame = Time.frameCount;
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
        shot.Launch(owner, direction, config.ProjectileSpeed, Damage, config.ProjectileLifetime,
            config.CollisionRadius, config.BurnPercent, config.BurnDuration);
        reservedCost = 0f; // Committed at release; launched shots never refund their cost.
        IsCasting = false;
        // Cooldown starts at release, including immediately blocked shots.
        primaryActivityFrame = Time.frameCount;
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
        IsFlamethrowing = false;
        flameOwnsAttack = false;
        float refund = reservedCost;
        reservedCost = 0f; // Clear before notifying observers; repeated resets cannot refund twice.
        if (refund > 0f && Energy != null)
            Energy.State.Refund(refund, Time.time);
        owner = null;
        movement = null;
        if (cancelled)
            CastCancelled?.Invoke();
    }

    public override void ObserveAttackButtons(bool primaryHeld, bool secondaryHeld)
    {
        // Keep ownership across input frames, even though presentation is refreshed below.
        if (!secondaryHeld)
            flameOwnsAttack = false;
        IsFlamethrowing = false;
        if (Energy != null)
            Energy.State.ObserveButtons(primaryHeld, secondaryHeld, Time.time);
    }

    public override void SecondaryAttack(WeaponAttackContext context)
    {
        FPSController controller = context.Owner != null ? context.Owner.GetComponent<FPSController>() : null;
        if (IsCasting || primaryActivityFrame == Time.frameCount ||
            !CanUseWeapon || Spell == null || context.Owner == null || Energy == null ||
            !Energy.isActiveAndEnabled || (controller != null && !controller.CanAcceptGameplayInput))
            return;
        float fundedSeconds = Energy.State.SpendFiringTime(Spell.FlamethrowerConsumption, Time.deltaTime, Time.time);
        if (fundedSeconds <= 0f)
            return;
        // Exhaustion can enter overheat here; the final funded fraction still deals damage.
        Vector3 origin = castOrigin != null ? castOrigin.position : context.AimRay.origin;
        bool hit = ProjectileCollision.TryHit(context.AimRay.origin, origin, Spell.FlameRadius,
            context.Owner, Physics.DefaultRaycastLayers, out Collider surface, out Vector3 center);
        FlameStart = hit ? center : origin;
        Vector3 direction = context.AimRay.GetPoint(Spell.FlameReach) - origin;
        if (direction.sqrMagnitude < 0.000001f)
            direction = context.AimRay.direction;
        Vector3 end = origin + direction.normalized * Spell.FlameReach;
        if (!hit)
            hit = ProjectileCollision.TryHit(origin, end, Spell.FlameRadius, context.Owner,
                Physics.DefaultRaycastLayers, out surface, out center);
        FlameEnd = hit ? center : end;
        IsFlamethrowing = !Energy.State.IsOverheated;
        flameOwnsAttack = IsFlamethrowing;
        IDamageable receiver = hit ? surface.GetComponentInParent<IDamageable>() : null;
        if (receiver?.TakeDamage(Spell.FlameDamagePerSecond * fundedSeconds) ?? false)
        {
            BurnStatus.Apply(receiver, Spell.FlameDamagePerSecond, Spell.BurnPercent, Spell.BurnDuration);
            ReportAttack(true);
        }
    }

    public override void RestoreResources()
    {
        base.RestoreResources();
        if (Energy != null)
            Energy.ResetFull();
    }
    private void OnDisable() => ResetAttackState();
}
