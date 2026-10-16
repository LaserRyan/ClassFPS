using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerHealth), typeof(FPSController))]
[DisallowMultipleComponent]
public sealed class PlayerLifecycle : MonoBehaviour
{
    [SerializeField] private Transform spawnpoint;
    [SerializeField, Min(0f)] private float respawnDelay = 3f;
    private PlayerHealth health;
    private FPSController movement;
    private Weapon[] weapons;
    private readonly List<GameObject> hiddenPresentations = new List<GameObject>();
    private Vector3 initialPosition;
    private Quaternion initialFacing;
    private float respawnAt;
    public bool IsDead { get; private set; }
    public float SecondsUntilRespawn => IsDead ? Mathf.Max(0f, respawnAt - Time.time) : 0f;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<FPSController>();
        RefreshWeapons();
        initialPosition = transform.position;
        initialFacing = transform.rotation;
    }

    private void OnEnable()
    {
        health.HealthChanged += OnHealthChanged;
        if (health.CurrentHealth <= 0f && !IsDead)
            Die();
    }

    private void OnDisable() => health.HealthChanged -= OnHealthChanged;

    private void OnHealthChanged(float current, float maximum)
    {
        if (current <= 0f && !IsDead)
            Die();
    }

    private void Die()
    {
        IsDead = true;
        respawnAt = Time.time + Mathf.Max(0f, respawnDelay);
        health.DamageBlocked = true;
        movement.SetGameplayBlocked(true);
        ResetWeapons(true);
        foreach (GunPresentation view in GetComponentsInChildren<GunPresentation>(true))
            HidePresentation(view.gameObject);
        foreach (SwordPresentation view in GetComponentsInChildren<SwordPresentation>(true))
            HidePresentation(view.gameObject);
        foreach (FireSpellPresentation view in GetComponentsInChildren<FireSpellPresentation>(true))
            HidePresentation(view.gameObject);
    }

    private void Update()
    {
        if (!IsDead || Time.time < respawnAt)
            return;
        PlaceAtSpawn();
        // State remains blocked throughout repositioning and health notification.
        health.RestoreFullHealth();
        ResetWeapons(false);
        foreach (GameObject view in hiddenPresentations)
            if (view != null)
                view.SetActive(true);
        hiddenPresentations.Clear();
        IsDead = false;
        health.DamageBlocked = false;
        movement.SetGameplayBlocked(false);
    }

    public void RefreshWeapons() => weapons = GetComponentsInChildren<Weapon>(true);

    public void PlaceAtSpawn() => movement.ResetAtSpawn(
        spawnpoint != null ? spawnpoint.position : initialPosition,
        spawnpoint != null ? spawnpoint.rotation : initialFacing);

    private void ResetWeapons(bool blocked)
    {
        foreach (Weapon weapon in weapons)
            if (weapon != null)
            {
                weapon.SetAttacksBlocked(blocked);
                if (!blocked)
                    weapon.Ammo?.Refill();
            }
        foreach (GunPresentation view in GetComponentsInChildren<GunPresentation>(true))
            view.ResetPresentation();
        foreach (SwordPresentation view in GetComponentsInChildren<SwordPresentation>(true))
            view.ResetPresentation();
        foreach (FireSpellPresentation view in GetComponentsInChildren<FireSpellPresentation>(true))
            view.ResetPresentation();
    }

    private void HidePresentation(GameObject view)
    {
        // Restore only visuals that were active before death; keep unequipped ones hidden.
        if (!view.activeSelf)
            return;
        hiddenPresentations.Add(view);
        view.SetActive(false);
    }
}
