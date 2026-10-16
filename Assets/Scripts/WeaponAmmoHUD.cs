using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponAmmoHUD : MonoBehaviour
{
    [SerializeField] private PlayerCombatInput combat;
    [SerializeField] private Vector2 screenMargin = new Vector2(24f, 24f);
    [SerializeField] private Vector2 panelSize = new Vector2(220f, 60f);
    private WeaponAmmo observedAmmo;
    private HeatAmmoState observedEnergy;
    private bool overheated;
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
        if (observedEnergy != null)
            observedEnergy.Changed -= Refresh;
        observedEnergy = null;
        overheated = false;
    }

    private void BindWeapon(Weapon weapon)
    {
        UnsubscribeAmmo();
        if (weapon == null)
        {
            text = "";
            return;
        }
        if (weapon is FireSpellWeapon fire && fire.Energy != null)
        {
            observedEnergy = fire.Energy.State;
            observedEnergy.Changed += Refresh;
        }
        else
            observedAmmo = weapon.Ammo;
        if (observedAmmo != null)
            observedAmmo.Changed += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        if (observedEnergy != null)
        {
            overheated = observedEnergy.IsOverheated;
            string status = overheated ? $"OVERHEATED {observedEnergy.OverheatRemaining:0.0}s" :
                observedEnergy.RequiresRelease ? "Release MB1 + MB2" : "Shared energy";
            text = $"Ammo: {observedEnergy.Current:0.0} / {observedEnergy.Maximum:0.0}\n{status}";
            return;
        }
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
        Color previous = GUI.color;
        if (overheated)
            GUI.color = new Color(1f, 0.3f, 0.15f);
        GUI.Box(new Rect(Screen.width - screenMargin.x - width,
            Screen.height - screenMargin.y - height, width, height), text, style);
        GUI.color = previous;
    }
}
