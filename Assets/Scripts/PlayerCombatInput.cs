using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(FPSController))]
[DisallowMultipleComponent]
public sealed class PlayerCombatInput : MonoBehaviour
{
    [SerializeField] private Weapon equippedWeapon;
    private FPSController movement;
    private bool primaryRequiresRelease;
    public event System.Action<Weapon> EquippedWeaponChanged;

    public Weapon EquippedWeapon
    {
        get => equippedWeapon;
        set
        {
            if (equippedWeapon == value)
                return;
            equippedWeapon = value;
            EquippedWeaponChanged?.Invoke(value);
        }
    }

    private void Awake() => movement = GetComponent<FPSController>();
    private void OnDisable() => primaryRequiresRelease = true;

    private void LateUpdate()
    {
        // All Update calls (including look and cursor capture) finish before this.
        // The movement controller excludes the click that recaptures the cursor.
        Mouse mouse = Mouse.current;
        if (!movement.CanAcceptGameplayInput || mouse == null ||
            equippedWeapon == null || !equippedWeapon.isActiveAndEnabled ||
            movement.PlayerCamera == null)
        {
            // Holding the capture click (or holding through death/focus loss)
            // must not become an attack when gameplay resumes.
            primaryRequiresRelease = mouse != null && mouse.leftButton.isPressed;
            return;
        }

        Camera aimCamera = movement.PlayerCamera;
        Ray centerRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var context = new WeaponAttackContext(transform,
            new Ray(aimCamera.transform.position, centerRay.direction));

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            equippedWeapon.Reload();

        if (!mouse.leftButton.isPressed)
            primaryRequiresRelease = false;
        else if (!primaryRequiresRelease)
            equippedWeapon.PrimaryAttack(context);
        if (mouse.rightButton.wasPressedThisFrame)
            equippedWeapon.SecondaryAttack(context);
    }
}
