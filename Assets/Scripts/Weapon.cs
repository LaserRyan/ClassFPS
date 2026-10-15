using System;
using UnityEngine;

// Shared attack information works for rays, projectiles, and melee weapons.
public readonly struct WeaponAttackContext
{
    public readonly Transform Owner;
    public readonly Ray AimRay;

    public WeaponAttackContext(Transform owner, Ray aimRay)
    {
        Owner = owner;
        AimRay = aimRay;
    }
}

public abstract class Weapon : MonoBehaviour
{
    [SerializeField, Min(0f)] protected float damage = 25f;
    [SerializeField, Min(0.01f)] protected float range = 100f;
    [SerializeField, Min(0f)] protected float attackCooldown = 0.2f;

    // True means an attack actually dealt damage, rather than just hitting scenery.
    public event Action<bool> AttackFeedback;
    private float nextAttackTime;
    private bool attacksBlocked;
    public virtual WeaponAmmo Ammo => GetComponent<WeaponAmmo>();

    // Future secondary abilities choose their own resource costs independently.
    public virtual void Reload()
    {
        if (!attacksBlocked && isActiveAndEnabled)
            Ammo?.BeginReload();
    }

    public void SetAttacksBlocked(bool blocked)
    {
        attacksBlocked = blocked;
        ResetAttackState();
    }

    public virtual void ResetAttackState()
    {
        nextAttackTime = 0f;
        Ammo?.CancelReload();
    }

    public abstract void PrimaryAttack(WeaponAttackContext context);
    public virtual void SecondaryAttack(WeaponAttackContext context) { }

    protected bool TryBeginAttack()
    {
        if (attacksBlocked || !isActiveAndEnabled || Time.time < nextAttackTime)
            return false;

        WeaponAmmo ammo = Ammo;
        if (ammo != null && !ammo.TryConsume())
            return false;

        nextAttackTime = Time.time + attackCooldown;
        return true;
    }

    protected void ReportAttack(bool dealtDamage) => AttackFeedback?.Invoke(dealtDamage);
}
