using UnityEngine;

[CreateAssetMenu(menuName = "ClassFPS/Weapons/Fire Spell Definition")]
public sealed class FireSpellDefinition : WeaponDefinition
{
    [SerializeField, Min(0f)] private float castDelay = 0.35f;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 12f;
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    [SerializeField, Min(0.01f)] private float projectileLifetime = 4f;
    [SerializeField] private StraightProjectile projectilePrefab;
    [Header("Burn (one refreshed effect per target)")]
    [Tooltip("Percent of fireball damage / flame DPS dealt each second as additional burn damage. Zero disables burn.")]
    [SerializeField, Min(0f)] private float burnPercent = 20f;
    [SerializeField, Min(0f)] private float burnDuration = 3f;
    public float BurnPercent => Mathf.Max(0f, burnPercent);
    public float BurnDuration => Mathf.Max(0f, burnDuration);
    [Header("Shared heat-based ammo (both actions)")]
    [SerializeField, Min(1f)] private float maximumEnergy = 100f;
    [SerializeField, Min(0.01f)] private float fireballCost = 25f;
    [SerializeField, Min(0.01f)] private float flamethrowerConsumption = 30f;
    [SerializeField, Min(0f)] private float regenerationDelay = 2f;
    [SerializeField, Min(0f)] private float regenerationRate = 25f;
    [SerializeField, Min(0.01f)] private float overheatDuration = 3f;
    [Header("Prototype secondary flame")]
    [SerializeField, Min(0f)] private float flameDamagePerSecond = 30f;
    [SerializeField, Min(0.01f)] private float flameReach = 5f;
    [SerializeField, Min(0.01f)] private float flameRadius = 0.35f;
    public float MaximumEnergy => Mathf.Max(1f, maximumEnergy);
    public float FireballCost => Mathf.Max(0.01f, fireballCost);
    public float FlamethrowerConsumption => Mathf.Max(0.01f, flamethrowerConsumption);
    public float RegenerationDelay => Mathf.Max(0f, regenerationDelay);
    public float RegenerationRate => Mathf.Max(0f, regenerationRate);
    public float OverheatDuration => Mathf.Max(0.01f, overheatDuration);
    public float FlameDamagePerSecond => Mathf.Max(0f, flameDamagePerSecond);
    public float FlameReach => Mathf.Max(0.01f, flameReach);
    public float FlameRadius => Mathf.Max(0.01f, flameRadius);
    public float CastDelay => Mathf.Max(0f, castDelay);
    public float ProjectileSpeed => Mathf.Max(0.01f, projectileSpeed);
    public float CollisionRadius => Mathf.Max(0.01f, collisionRadius);
    public float ProjectileLifetime => Mathf.Max(0.01f, projectileLifetime);
    public StraightProjectile ProjectilePrefab => projectilePrefab;
}
