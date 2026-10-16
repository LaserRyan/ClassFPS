using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class FireSpellPresentation : MonoBehaviour
{
    [SerializeField] private FireSpellWeapon weapon;
    [SerializeField] private Transform chargeVisual;
    [SerializeField] private LineRenderer flameLine;
    [SerializeField] private AudioClip castClip;
    [SerializeField] private AudioClip releaseClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
    private AudioSource source;
    private void Awake() => source = GetComponent<AudioSource>();
    private void OnEnable()
    {
        if (weapon != null)
        {
            weapon.CastStarted += OnCast;
            weapon.CastCancelled += ResetPresentation;
            weapon.ProjectileReleased += OnRelease;
        }
        ResetPresentation();
    }
    private void OnDisable()
    {
        if (weapon != null)
        {
            weapon.CastStarted -= OnCast;
            weapon.CastCancelled -= ResetPresentation;
            weapon.ProjectileReleased -= OnRelease;
        }
        ResetPresentation();
    }
    private void OnCast()
    {
        if (chargeVisual != null)
        {
            chargeVisual.localScale = Vector3.one * 0.05f;
            chargeVisual.gameObject.SetActive(true);
        }
        if (source != null && castClip != null)
            source.PlayOneShot(castClip, volume);
    }
    private void OnRelease()
    {
        ResetPresentation();
        if (source != null && releaseClip != null)
            source.PlayOneShot(releaseClip, volume);
    }
    private void LateUpdate()
    {
        if (flameLine != null)
        {
            flameLine.enabled = weapon != null && weapon.IsFlamethrowing;
            if (flameLine.enabled)
            {
                flameLine.startWidth = weapon.Spell.FlameRadius * 2f;
                flameLine.endWidth = weapon.Spell.FlameRadius;
                flameLine.SetPosition(0, weapon.FlameStart);
                flameLine.SetPosition(1, weapon.FlameEnd);
            }
        }
        if (weapon == null || !weapon.IsCasting || chargeVisual == null)
            return;
        chargeVisual.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.25f, weapon.CastProgress);
    }
    public void ResetPresentation()
    {
        if (flameLine != null)
            flameLine.enabled = false;
        if (chargeVisual != null)
            chargeVisual.gameObject.SetActive(false);
        if (source != null)
            source.Stop();
    }
}
