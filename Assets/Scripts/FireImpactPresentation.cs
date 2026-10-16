using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class FireImpactPresentation : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField, Min(0.01f)] private float duration = 0.22f;
    [SerializeField, Min(0.01f)] private float size = 0.6f;
    [SerializeField] private AudioClip impactClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
    private float started;
    private float destroyAt;
    private void Start()
    {
        started = Time.time;
        destroyAt = started + Mathf.Max(duration, impactClip != null ? impactClip.length : 0f);
        if (impactClip != null)
            GetComponent<AudioSource>().PlayOneShot(impactClip, volume);
    }
    private void Update()
    {
        float t = Mathf.Clamp01((Time.time - started) / Mathf.Max(0.01f, duration));
        if (visual != null)
            visual.localScale = Vector3.one * (size * Mathf.Sin(t * Mathf.PI));
        if (Time.time >= destroyAt)
            Destroy(gameObject);
    }
}
