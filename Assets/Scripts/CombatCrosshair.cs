using UnityEngine;

[RequireComponent(typeof(PlayerCombatInput))]
[DisallowMultipleComponent]
public sealed class CombatCrosshair : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float fireFeedbackDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float damageFeedbackDuration = 0.15f;
    private PlayerCombatInput combat;
    private FPSController movement;
    private Weapon observedWeapon;
    private float fireUntil;
    private float damageUntil;

    private void Awake()
    {
        combat = GetComponent<PlayerCombatInput>();
        movement = GetComponent<FPSController>();
    }

    private void Update()
    {
        if (observedWeapon == combat.EquippedWeapon)
            return;
        Unsubscribe();
        observedWeapon = combat.EquippedWeapon;
        if (observedWeapon != null)
            observedWeapon.AttackFeedback += OnAttack;
    }

    private void OnDisable() => Unsubscribe();

    private void Unsubscribe()
    {
        if (observedWeapon != null)
            observedWeapon.AttackFeedback -= OnAttack;
        observedWeapon = null;
    }

    private void OnAttack(bool dealtDamage)
    {
        fireUntil = Time.unscaledTime + fireFeedbackDuration;
        if (dealtDamage)
            damageUntil = Time.unscaledTime + damageFeedbackDuration;
    }

    private void OnGUI()
    {
        if (movement == null || !movement.CanAcceptGameplayInput)
            return;

        Color previousColor = GUI.color;
        bool damaged = Time.unscaledTime < damageUntil;
        bool fired = Time.unscaledTime < fireUntil;
        GUI.color = damaged ? Color.red : fired ? Color.yellow : Color.white;
        float x = Screen.width * 0.5f;
        float y = Screen.height * 0.5f;
        float gap = fired ? 7f : 4f;
        DrawRect(x - gap - 5f, y - 1f, 5f, 2f);
        DrawRect(x + gap, y - 1f, 5f, 2f);
        DrawRect(x - 1f, y - gap - 5f, 2f, 5f);
        DrawRect(x - 1f, y + gap, 2f, 5f);
        if (damaged)
            DrawRect(x - 2f, y - 2f, 4f, 4f);
        GUI.color = previousColor;
    }

    private static void DrawRect(float x, float y, float width, float height)
    {
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
    }
}
