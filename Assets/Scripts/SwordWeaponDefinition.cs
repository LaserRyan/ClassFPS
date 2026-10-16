using UnityEngine;

[CreateAssetMenu(menuName = "ClassFPS/Weapons/Sword Definition")]
public sealed class SwordWeaponDefinition : WeaponDefinition
{
    [SerializeField, Range(1f, 180f)] private float attackArc = 100f;
    [SerializeField, Min(0f)] private float windup = 0.18f;
    [SerializeField, Min(0.01f)] private float activeDuration = 0.16f;
    [SerializeField, Min(0f)] private float recovery = 0.3f;
    public float AttackArc => Mathf.Clamp(attackArc, 1f, 180f);
    public float Windup => Mathf.Max(0f, windup);
    public float ActiveDuration => Mathf.Max(0.01f, activeDuration);
    public float Recovery => Mathf.Max(0f, recovery);
}
