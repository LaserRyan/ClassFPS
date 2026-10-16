using UnityEngine;

[CreateAssetMenu(menuName = "ClassFPS/Weapons/Fire Spell Definition")]
public sealed class FireSpellDefinition : WeaponDefinition
{
    [SerializeField, Min(0f)] private float castDelay = 0.35f;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 12f;
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    [SerializeField, Min(0.01f)] private float projectileLifetime = 4f;
    [SerializeField] private StraightProjectile projectilePrefab;
    public float CastDelay => Mathf.Max(0f, castDelay);
    public float ProjectileSpeed => Mathf.Max(0.01f, projectileSpeed);
    public float CollisionRadius => Mathf.Max(0.01f, collisionRadius);
    public float ProjectileLifetime => Mathf.Max(0.01f, projectileLifetime);
    public StraightProjectile ProjectilePrefab => projectilePrefab;
}
