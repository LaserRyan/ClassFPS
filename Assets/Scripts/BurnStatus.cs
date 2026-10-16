using UnityEngine;

// One instance on the damage receiver, shared by all of its child hitboxes.
[DisallowMultipleComponent]
public sealed class BurnStatus : MonoBehaviour
{
    private IDamageable receiver;
    private float damagePerSecond;
    private float remaining;

    public static void Apply(IDamageable target, float baseDamage, float percent, float duration)
    {
        if (!(target is MonoBehaviour actor) || !actor.isActiveAndEnabled ||
            baseDamage <= 0f || percent <= 0f || duration <= 0f)
            return;
        BurnStatus burn = actor.GetComponent<BurnStatus>();
        if (burn == null)
            burn = actor.gameObject.AddComponent<BurnStatus>();
        burn.receiver = target;
        // Refresh without stacking; retain the stronger burn while it is still active.
        float rate = baseDamage * percent / 100f;
        burn.damagePerSecond = burn.remaining > 0f ? Mathf.Max(burn.damagePerSecond, rate) : rate;
        burn.remaining = duration;
    }

    private void Update()
    {
        if (remaining <= 0f)
            return;
        float step = Mathf.Min(Time.deltaTime, remaining);
        remaining -= step;
        if (step > 0f && (!(receiver is MonoBehaviour actor) || !actor.isActiveAndEnabled ||
            !receiver.TakeDamage(damagePerSecond * step)))
            Clear();
    }

    public void Clear()
    {
        remaining = 0f;
        damagePerSecond = 0f;
        receiver = null;
    }

    private void OnDisable() => Clear();
}
