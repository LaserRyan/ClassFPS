using UnityEngine;

// Player-only, non-spatial prototype audio. Damage feedback is not a firing event.
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class PlayerWeaponAudio : MonoBehaviour
{
    [SerializeField] private HitscanGun gun;
    [SerializeField] private SwordWeapon sword;
    [SerializeField] private AudioClip gunClip;
    [SerializeField] private AudioClip swordClip;
    [SerializeField, Range(0f, 1f)] private float gunVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float swordVolume = 0.7f;
    public AudioClip GunClip => gunClip;
    public float GunVolume => gunVolume;
    private AudioSource source;
    private PlayerCombatInput combat;
    private FPSController movement;
    private PlayerHealth health;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        combat = GetComponentInParent<PlayerCombatInput>();
        movement = GetComponentInParent<FPSController>();
        health = GetComponentInParent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (gun != null)
            gun.ShotFired += OnGunShot;
        if (sword != null)
            sword.SwingStarted += OnSwordSwing;
        if (health != null)
            health.HealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (gun != null)
            gun.ShotFired -= OnGunShot;
        if (sword != null)
            sword.SwingStarted -= OnSwordSwing;
        if (health != null)
            health.HealthChanged -= OnHealthChanged;
        if (source != null)
            source.Stop();
    }

    private bool CanPlay(Weapon weapon) => source != null && source.isActiveAndEnabled &&
        combat != null && combat.EquippedWeapon == weapon && movement != null &&
        movement.CanAcceptGameplayInput && (health == null || health.CurrentHealth > 0f);

    private void OnGunShot(Vector3 endpoint)
    {
        if (gunClip != null && CanPlay(gun))
            source.PlayOneShot(gunClip, gunVolume);
    }

    private void OnSwordSwing()
    {
        if (swordClip != null && CanPlay(sword))
            source.PlayOneShot(swordClip, swordVolume);
    }

    private void OnHealthChanged(float current, float maximum)
    {
        if (current <= 0f && source != null)
            source.Stop();
    }
}
