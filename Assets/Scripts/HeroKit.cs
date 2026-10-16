using UnityEngine;

// One camera-local kit, containing one fixed Weapon plus replaceable presentation.
[DisallowMultipleComponent]
public sealed class HeroKit : MonoBehaviour
{
    [SerializeField] private Weapon primaryWeapon;
    public Weapon PrimaryWeapon => primaryWeapon;
}
