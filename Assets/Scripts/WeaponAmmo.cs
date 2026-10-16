using System;
using UnityEngine;

// Optional finite magazine resource. Reserve ammunition is unlimited for this prototype.
[DisallowMultipleComponent]
public sealed class WeaponAmmo : MonoBehaviour
{
    [SerializeField, Min(1)] private int magazineCapacity = 12;
    [SerializeField, Min(0.01f)] private float reloadDuration = 1.5f;
    [SerializeField] private bool automaticReload = true;
    private int currentAmmo;
    private bool initialized;
    private float reloadAt;
    public GunWeaponDefinition Definition => GetComponent<Weapon>()?.Definition as GunWeaponDefinition;
    public int Capacity => Definition != null ? Definition.MagazineCapacity : Mathf.Max(1, magazineCapacity);
    public float ReloadDuration => Definition != null ? Definition.ReloadDuration : Mathf.Max(0.01f, reloadDuration);
    public bool AutomaticReload => Definition != null ? Definition.AutomaticReload : automaticReload;
    public int CurrentAmmo { get { Initialize(); return currentAmmo; } }
    public bool IsReloading { get; private set; }
    public event Action Changed;

    private void Awake() => Initialize();
    private void Initialize()
    {
        if (initialized)
            return;
        initialized = true;
        currentAmmo = Capacity;
    }

    public bool TryConsume()
    {
        Initialize();
        if (!isActiveAndEnabled || IsReloading)
            return false;
        if (currentAmmo <= 0)
        {
            if (AutomaticReload)
                BeginReload();
            return false;
        }
        currentAmmo--;
        Changed?.Invoke();
        if (currentAmmo == 0 && AutomaticReload)
            BeginReload();
        return true;
    }

    public bool BeginReload()
    {
        Initialize();
        if (!isActiveAndEnabled || IsReloading || currentAmmo >= Capacity)
            return false;
        IsReloading = true;
        reloadAt = Time.time + ReloadDuration;
        Changed?.Invoke();
        return true;
    }

    private void Update()
    {
        if (IsReloading && Time.time >= reloadAt)
            Refill();
    }

    public void CancelReload()
    {
        if (!IsReloading)
            return;
        IsReloading = false;
        Changed?.Invoke();
    }

    public void Refill()
    {
        Initialize();
        currentAmmo = Capacity;
        IsReloading = false;
        Changed?.Invoke();
    }

    private void OnDisable() => CancelReload();
    private void OnValidate()
    {
        magazineCapacity = Mathf.Max(1, magazineCapacity);
        reloadDuration = Mathf.Max(0.01f, reloadDuration);
        if (!initialized)
            return;
        currentAmmo = Mathf.Clamp(currentAmmo, 0, Capacity);
        Changed?.Invoke();
    }
}
