public interface IDamageable
{
    // Return true only when damage was accepted (used by hit feedback).
    bool TakeDamage(float amount);
}

// Optional precise result for on-hit effects. Legacy receivers remain supported.
public interface IHealthDamageReceiver : IDamageable
{
    float ApplyHealthDamage(float amount);
}
