using UnityEngine;

[CreateAssetMenu(menuName = "ClassFPS/Weapons/Gun Definition")]
public sealed class GunWeaponDefinition : WeaponDefinition
{
    [SerializeField, Min(1)] private int magazineCapacity = 12;
    [SerializeField, Min(0.01f)] private float reloadDuration = 1.5f;
    [SerializeField] private bool automaticReload = true;
    public int MagazineCapacity => Mathf.Max(1, magazineCapacity);
    public float ReloadDuration => Mathf.Max(0.01f, reloadDuration);
    public bool AutomaticReload => automaticReload;
}
