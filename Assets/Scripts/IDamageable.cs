public interface IDamageable
{
    // Return true only when damage was accepted (used by hit feedback).
    bool TakeDamage(float amount);
}
