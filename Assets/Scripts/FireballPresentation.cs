using UnityEngine;

[RequireComponent(typeof(StraightProjectile))]
[DisallowMultipleComponent]
public sealed class FireballPresentation : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private FireImpactPresentation impactPrefab;
    private StraightProjectile projectile;
    private void Awake() => projectile = GetComponent<StraightProjectile>();
    private void OnEnable()
    {
        projectile.Launched += OnLaunch;
        projectile.Impacted += OnImpact;
    }
    private void OnDisable()
    {
        projectile.Launched -= OnLaunch;
        projectile.Impacted -= OnImpact;
        if (trail != null)
            trail.Clear();
    }
    private void OnLaunch(float radius)
    {
        if (visual != null)
            visual.localScale = Vector3.one * (radius * 2f);
        if (trail != null)
        {
            trail.startWidth = radius * 1.6f;
            trail.endWidth = 0f;
            trail.Clear();
        }
    }
    private void OnImpact(Vector3 center, bool damaged)
    {
        if (impactPrefab != null)
            Instantiate(impactPrefab, center, Quaternion.identity);
    }
}
