using UnityEngine;

[DisallowMultipleComponent]
public sealed class GunslingerLifesteal : HeroPassive
{
    [SerializeField] private GunslingerDefinition definition;
    private PlayerHealth owner;
    private HitscanGun gun;
    public GunslingerDefinition Definition => definition;
    public override void Bind(PlayerHealth health, Weapon primary)
    {
        Unsubscribe();
        owner = health;
        gun = primary as HitscanGun;
        if (isActiveAndEnabled && gun != null) gun.HealthDamageDealt += OnDamage;
    }
    private void OnEnable() { if (gun != null) gun.HealthDamageDealt += OnDamage; }
    private void OnDisable() => Unsubscribe();
    private void Unsubscribe() { if (gun != null) gun.HealthDamageDealt -= OnDamage; }
    private void OnDamage(Transform attacker, IDamageable target, float actual)
    {
        if (owner == null || definition == null || attacker != owner.transform) return;
        Component receiver = target as Component;
        bool self = ReferenceEquals(target, owner) || (receiver != null && receiver.transform.IsChildOf(owner.transform));
        float healing = GunslingerRules.Healing(actual, definition.LifestealPercent,
            owner.CurrentHealth, owner.MaximumHealth, owner.isActiveAndEnabled && !owner.DamageBlocked, self);
        owner.Heal(healing);
    }
}
