using UnityEngine;

// Optional presentation only. No movement, cooldown or damage decisions.
[RequireComponent(typeof(GunslingerDash), typeof(AudioSource))]
public sealed class DashPresentation : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dashClip;
    [SerializeField] private ParticleSystem dashEffect;
    private GunslingerDash dash;
    private void Awake()
    {
        dash = GetComponent<GunslingerDash>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }
    private void OnEnable() { dash.Started += Play; dash.Stopped += Stop; }
    private void OnDisable() { dash.Started -= Play; dash.Stopped -= Stop; Stop(); }
    private void Play()
    {
        if (audioSource != null && dashClip != null) audioSource.PlayOneShot(dashClip);
        if (dashEffect != null) dashEffect.Play();
    }
    private void Stop()
    {
        if (audioSource != null) audioSource.Stop();
        if (dashEffect != null) dashEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
