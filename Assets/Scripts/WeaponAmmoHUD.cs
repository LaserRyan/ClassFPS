using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponAmmoHUD : MonoBehaviour
{
    [SerializeField] private PlayerCombatInput combat;
    [SerializeField] private Vector2 screenMargin = new Vector2(24f, 24f);
    [SerializeField] private Vector2 panelSize = new Vector2(220f, 60f);
    private WeaponAmmo observedAmmo;
    private string text = "";
    private GUIStyle style;

    private void OnEnable()
    {
        if (combat == null)
            return;
        combat.EquippedWeaponChanged += BindWeapon;
        BindWeapon(combat.EquippedWeapon);
    }

    private void OnDisable()
    {
        if (combat != null)
            combat.EquippedWeaponChanged -= BindWeapon;
        UnsubscribeAmmo();
    }

    private void UnsubscribeAmmo()
    {
        if (observedAmmo != null)
            observedAmmo.Changed -= Refresh;
        observedAmmo = null;
    }

    private void BindWeapon(Weapon weapon)
    {
        UnsubscribeAmmo();
        if (weapon == null)
        {
            text = "";
            return;
        }
        observedAmmo = weapon.Ammo;
        if (observedAmmo != null)
            observedAmmo.Changed += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        text = observedAmmo == null ? "Ammo: \u221E" :
            $"Ammo: {observedAmmo.CurrentAmmo} / {observedAmmo.Capacity}\n" +
            (observedAmmo.IsReloading ? "Reloading…" : "R: Reload");
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(text))
            return;
        if (style == null)
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
        float width = Mathf.Max(120f, panelSize.x);
        float height = Mathf.Max(48f, panelSize.y);
        GUI.Box(new Rect(Screen.width - screenMargin.x - width,
            Screen.height - screenMargin.y - height, width, height), text, style);
    }
}
