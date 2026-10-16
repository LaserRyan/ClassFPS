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
    private Transform selectedAvatar;
    public HeroDefinition[] Heroes => heroes;
    public HeroDefinition SelectedHero { get; private set; }
    public HeroKit SelectedKit { get; private set; }
    public WorldHeroPresentation SelectedWorldPresentation { get; private set; }

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
        ApplyHero(hero, false, true);
        return true;
    }

    // Network authority lives outside selection. This only builds/reset local instances.
    public void ApplyNetworkHero(HeroDefinition hero, bool owner, Vector3 spawn, Quaternion facing)
    {
        ClearPresentation();
        ApplyHero(hero, true, owner);
        movement.ResetAtSpawn(spawn, facing);
    }

    private void ClearPresentation()
    {
        combat.RequirePrimaryRelease();
        combat.Secondary?.Cancel();
        combat.Secondary = null;
        movement.Dash = null;
        combat.EquippedWeapon = null;
        // Deactivate immediately; deferred Destroy must not leave two active kits/subscribers.
        if (SelectedKit != null) { SelectedKit.gameObject.SetActive(false); Destroy(SelectedKit.gameObject); }
        if (SelectedWorldPresentation != null)
        { SelectedWorldPresentation.gameObject.SetActive(false); Destroy(SelectedWorldPresentation.gameObject); }
        if (selectedAvatar != null) { selectedAvatar.gameObject.SetActive(false); Destroy(selectedAvatar.gameObject); }
    }

    private void ApplyHero(HeroDefinition hero, bool network, bool owner)
    {
        // Runtime state exists only in this selected instance; no shared asset writes.
        HeroKit kit = Instantiate(hero.KitPrefab, movement.PlayerCamera.transform, false);
        SelectedKit = kit;
        SelectedHero = hero;
        combat.EquippedWeapon = kit.PrimaryWeapon;
        if (!network && hero.SecondaryActionPrefab != null)
        {
            GameObject secondary = Instantiate(hero.SecondaryActionPrefab, kit.transform, false);
            combat.Secondary = secondary.GetComponent<HeroSecondary>();
            combat.Secondary.Bind(movement);
        }
        if (!network && hero.PassivePrefab != null)
            Instantiate(hero.PassivePrefab, kit.transform, false).GetComponent<HeroPassive>()
                .Bind(health, kit.PrimaryWeapon);
        health.SetMaximumHealth(hero.MaximumHealth, true);
        movement.ConfigureMovement(hero.MovementSpeed, hero.JumpHeight, hero.Gravity);
        Transform avatar = hero.AvatarPrefab != null ? Instantiate(hero.AvatarPrefab, transform, false).transform : null;
        selectedAvatar = avatar;
        WorldHeroPresentation world = hero.WorldPresentationPrefab != null ?
            Instantiate(hero.WorldPresentationPrefab, transform, false) : WorldHeroPlaceholder.CreateFallback(hero.HeroId);
        world.transform.SetParent(transform, false);
        SelectedWorldPresentation = world;
        world.Bind(owner ? movement.PlayerCamera : null, health, kit.PrimaryWeapon);
        PlayerViewVisibility visibility = GetComponent<PlayerViewVisibility>();
        if (visibility == null) visibility = gameObject.AddComponent<PlayerViewVisibility>();
        visibility.Configure(owner ? movement.PlayerCamera : null, kit.transform, world.transform, avatar);
        if (network)
        {
            kit.PrimaryWeapon.SetAttacksBlocked(true);
            kit.PrimaryWeapon.RestoreResources();
            kit.PrimaryWeapon.enabled = false;
            foreach (AudioSource source in kit.GetComponentsInChildren<AudioSource>(true))
            { source.Stop(); source.enabled = false; }
        }
        lifecycle.RefreshWeapons();
        if (!network) lifecycle.PlaceAtSpawn();
        health.DamageBlocked = network;
        combat.RequirePrimaryRelease();
        movement.SetHeroSelection(false);
    }

    public static bool IsValid(HeroDefinition hero, out string message)
    {
        message = null;
        if (hero == null || string.IsNullOrWhiteSpace(hero.HeroId) || hero.KitPrefab == null)
            message = "Hero needs a stable ID and a kit prefab.";
        else
        {
            if (!WorldHeroPresentation.IsVisualOnly(hero.WorldPresentationPrefab, out message)) return false;
            if ((hero.SecondaryActionPrefab != null && hero.SecondaryActionPrefab.GetComponent<HeroSecondary>() == null) ||
                (hero.PassivePrefab != null && hero.PassivePrefab.GetComponent<HeroPassive>() == null))
            {
                message = "Hero behavior slots need matching HeroSecondary/HeroPassive components.";
                return false;
            }
            if (hero.HeroId == "prototype.gun" &&
                (hero.SecondaryActionPrefab == null || hero.PassivePrefab == null ||
                 !hero.SecondaryActionPrefab.TryGetComponent(out GunslingerDash dash) ||
                 !hero.PassivePrefab.TryGetComponent(out GunslingerLifesteal passive) ||
                 !dash.enabled || !passive.enabled || dash.Definition == null ||
                 dash.Definition != passive.Definition || !hero.SecondaryActionPrefab.activeSelf ||
                 !hero.PassivePrefab.activeSelf))
            {
                message = "Gunslinger needs enabled dash/lifesteal prefabs with the same ability definition. Re-run hero setup.";
                return false;
            }
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
