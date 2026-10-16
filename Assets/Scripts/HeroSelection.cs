using System;
using UnityEngine;

// One selection per Play session; shares the existing player, health, input and lifecycle.
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(PlayerCombatInput), typeof(PlayerHealth), typeof(PlayerLifecycle))]
[DisallowMultipleComponent]
public sealed class HeroSelection : MonoBehaviour
{
    [SerializeField] private HeroDefinition[] heroes;
    private PlayerCombatInput combat;
    private FPSController movement;
    private PlayerHealth health;
    private PlayerLifecycle lifecycle;
    private string error;
    private Vector2 selectionScroll;
    public HeroDefinition SelectedHero { get; private set; }
    public HeroKit SelectedKit { get; private set; }

    private void Awake()
    {
        combat = GetComponent<PlayerCombatInput>();
        movement = GetComponent<FPSController>();
        health = GetComponent<PlayerHealth>();
        lifecycle = GetComponent<PlayerLifecycle>();
        combat.EquippedWeapon = null;
        health.DamageBlocked = true;
        movement.SetHeroSelection(true);
    }

    public bool SelectHero(HeroDefinition hero)
    {
        if (SelectedHero != null || hero == null || heroes == null || Array.IndexOf(heroes, hero) < 0)
            return false;
        if (!IsValid(hero, out error))
            return false;
        if (movement.PlayerCamera == null)
        {
            error = "Player camera is missing.";
            return false;
        }
        // Runtime state exists only in this selected instance; no shared asset writes.
        HeroKit kit = Instantiate(hero.KitPrefab, movement.PlayerCamera.transform, false);
        SelectedKit = kit;
        SelectedHero = hero;
        combat.EquippedWeapon = kit.PrimaryWeapon;
        health.SetMaximumHealth(hero.MaximumHealth, true);
        movement.ConfigureMovement(hero.MovementSpeed, hero.JumpHeight, hero.Gravity);
        if (hero.AvatarPrefab != null)
            Instantiate(hero.AvatarPrefab, transform, false);
        lifecycle.RefreshWeapons();
        lifecycle.PlaceAtSpawn();
        health.DamageBlocked = false;
        combat.RequirePrimaryRelease();
        movement.SetHeroSelection(false);
        return true;
    }

    public static bool IsValid(HeroDefinition hero, out string message)
    {
        message = null;
        if (hero == null || string.IsNullOrWhiteSpace(hero.HeroId) || hero.KitPrefab == null)
            message = "Hero needs a stable ID and a kit prefab.";
        else
        {
            Weapon weapon = hero.KitPrefab.PrimaryWeapon;
            if (weapon == null || hero.KitPrefab.GetComponentsInChildren<Weapon>(true).Length != 1 ||
                !weapon.transform.IsChildOf(hero.KitPrefab.transform) || !weapon.enabled ||
                !hero.KitPrefab.gameObject.activeSelf || !weapon.gameObject.activeSelf)
                message = "Hero kit must contain exactly one enabled primary weapon.";
            else if ((weapon is HitscanGun && !(weapon.Definition is GunWeaponDefinition)) ||
                (weapon is SwordWeapon && !(weapon.Definition is SwordWeaponDefinition)))
                message = "Prototype weapon needs its matching gun/sword definition.";
            else if (weapon is HitscanGun && (weapon.Ammo == null || !weapon.Ammo.enabled))
                message = "Gun kit needs enabled WeaponAmmo.";
            else if (weapon is FireSpellWeapon fire &&
                (fire.Spell == null || fire.Spell.ProjectilePrefab == null ||
                 fire.Energy == null || !fire.Energy.enabled ||
                 !fire.Spell.ProjectilePrefab.enabled || !fire.Spell.ProjectilePrefab.gameObject.activeSelf))
                message = "Fire kit needs its definition, projectile, and enabled FireEnergy. Re-run hero setup.";
        }
        return message == null;
    }

    private void OnGUI()
    {
        if (SelectedHero != null)
            return;
        float width = Mathf.Min(480f, Screen.width - 20f);
        float height = Mathf.Min(Screen.height - 20f, 150f + (heroes != null ? heroes.Length : 0) * 80f);
        GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f,
            Mathf.Max(10f, (Screen.height - height) * 0.5f), width, height), GUI.skin.box);
        GUILayout.Label("Select a prototype hero");
        GUILayout.Label("One fixed weapon per hero. Restart Play mode to choose again.");
        selectionScroll = GUILayout.BeginScrollView(selectionScroll);
        if (heroes != null)
            foreach (HeroDefinition hero in heroes)
            {
                if (hero == null)
                    continue;
                var label = new GUIContent($"{hero.DisplayName} — {hero.HeroClass}\n" +
                    $"Health {hero.MaximumHealth} | Speed {hero.MovementSpeed} | Jump {hero.JumpHeight}",
                    hero.Icon != null ? hero.Icon.texture : null);
                if (GUILayout.Button(label, GUILayout.Height(80f)))
                    SelectHero(hero);
            }
        GUILayout.EndScrollView();
        if (!string.IsNullOrEmpty(error))
            GUILayout.Label(error);
        GUILayout.EndArea();
    }
}
