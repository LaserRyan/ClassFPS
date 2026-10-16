using UnityEngine;

// Configuration only. Cooldowns, ammo, and other mutable state belong to instances.
public abstract class WeaponDefinition : ScriptableObject
{
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField, Min(0.01f)] private float range = 100f;
    [SerializeField, Min(0f)] private float cooldown = 0.2f;
    public float Damage => Mathf.Max(0f, damage);
    public float Range => Mathf.Max(0.01f, range);
    public float Cooldown => Mathf.Max(0f, cooldown);
}
