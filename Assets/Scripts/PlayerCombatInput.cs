using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(FPSController))]
[DisallowMultipleComponent]
public sealed class PlayerCombatInput : MonoBehaviour
{
    [SerializeField] private Weapon equippedWeapon;
    private FPSController movement;

    public Weapon EquippedWeapon
    {
        get => equippedWeapon;
        set => equippedWeapon = value;
    }

    private void Awake() => movement = GetComponent<FPSController>();

    private void LateUpdate()
    {
        // All Update calls (including look and cursor capture) finish before this.
        // The movement controller excludes the click that recaptures the cursor.
        Mouse mouse = Mouse.current;
        if (!movement.CanAcceptGameplayInput || mouse == null ||
            equippedWeapon == null || !equippedWeapon.isActiveAndEnabled ||
            movement.PlayerCamera == null)
            return;

        Camera aimCamera = movement.PlayerCamera;
        Ray centerRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var context = new WeaponAttackContext(transform,
            new Ray(aimCamera.transform.position, centerRay.direction));

        if (mouse.leftButton.wasPressedThisFrame)
            equippedWeapon.PrimaryAttack(context);
        if (mouse.rightButton.wasPressedThisFrame)
            equippedWeapon.SecondaryAttack(context);
    }
}
