using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class NetworkPrototypePlayer
{
    [SerializeField, Min(0f)] private float networkRespawnDelay = 3f;
    private readonly NetworkVariable<NetworkCombatSnapshot> combatState = new NetworkVariable<NetworkCombatSnapshot>();
    private readonly NetworkRevolverState revolver = new NetworkRevolverState();
    private bool fireRequiresRelease = true;
    private AudioSource worldShotAudio;
    private Collider[] actorColliders;
    private bool[] colliderDefaults;
    internal Vector3 LastServerShotOrigin { get; private set; }
    private double ServerNow => NetworkManager.ServerTime.Time;
    private GunWeaponDefinition ServerGun => selection.SelectedHero != null && CurrentHeroId == "prototype.gun" ?
        selection.SelectedHero.KitPrefab.PrimaryWeapon.Definition as GunWeaponDefinition : null;

    private void ResetServerCombat()
    {
        GunWeaponDefinition gun = ServerGun;
        revolver.Reset(hero.Value.Revision, gun != null ? gun.MagazineCapacity : 0);
        combatState.Value = new NetworkCombatSnapshot { Generation = hero.Value.Revision,
            Health = selection.SelectedHero.MaximumHealth, Maximum = selection.SelectedHero.MaximumHealth,
            Ammo = revolver.Ammo };
    }
    private void TickServerCombat()
    {
        if (appliedRevision == 0) return;
        if (revolver.Dead)
        {
            if (ServerNow >= revolver.RespawnAt)
            {
                NetworkHeroState next = hero.Value;
                next.Revision++;
                next.Spawn = IsTestOpponent ? initialHero.Spawn : NetworkPrototypeSession.Active.SpawnFor(OwnerClientId);
                next.Yaw = NetworkPrototypeSession.Active.SpawnYaw;
                hero.Value = next;
            }
            return;
        }
        GunWeaponDefinition gun = ServerGun;
        if (gun != null) { revolver.Tick(ServerNow, gun.MagazineCapacity); PublishCombat(); }
    }
    private void PublishCombat()
    {
        NetworkCombatSnapshot state = combatState.Value;
        state.Ammo = revolver.Ammo; state.Reloading = revolver.ReloadAt > 0;
        state.Dead = revolver.Dead; state.RespawnAt = revolver.RespawnAt;
        state.DashReadyAt = movement.NetworkDashReady;
        combatState.Value = state;
    }
    private void OnCombatChanged(NetworkCombatSnapshot previous, NetworkCombatSnapshot current) => ApplyCombatSnapshot();
    private void ApplyCombatSnapshot()
    {
        NetworkCombatSnapshot state = combatState.Value;
        if (state.Generation != appliedRevision || appliedRevision == 0) return;
        health.ApplyNetworkSnapshot(state.Health, state.Maximum);
        movement.SetGameplayBlocked(state.Dead);
        // Corpses never absorb shots or collide with living actors.
        GetComponent<CharacterController>().enabled = !state.Dead;
        for (int i = 0; i < actorColliders.Length; i++)
            if (actorColliders[i] != null) actorColliders[i].enabled = !state.Dead && colliderDefaults[i];
        if (selection.SelectedKit != null)
        {
            selection.SelectedKit.PrimaryWeapon.Ammo?.ApplyNetworkSnapshot(state.Ammo, state.Reloading);
            foreach (GunPresentation visual in selection.SelectedKit.GetComponentsInChildren<GunPresentation>(true))
                visual.gameObject.SetActive(!state.Dead);
        }
        if (state.Dead) { CancelLocalDashRequest(); fireRequiresRelease = true; if (worldShotAudio != null) worldShotAudio.Stop(); }
    }
    private void ExecuteServerCombat(NetworkMoveCommand command)
    {
        if (!IsServer || !command.Has(NetworkMoveCommand.Allowed) ||
            !revolver.Request(command.Generation, command.Sequence, ServerNow, true)) return;
        GunWeaponDefinition gun = ServerGun;
        if (gun == null || combatState.Value.Dead) return;
        bool reload = command.Has(NetworkMoveCommand.Reload);
        if (!reload && !command.Has(NetworkMoveCommand.Fire)) return;
        uint generation = command.Generation;
        if (reload)
        { revolver.Reload(ServerNow, gun.MagazineCapacity, gun.ReloadDuration); PublishCombat(); return; }
        if (!revolver.Fire(ServerNow, gun.Cooldown, gun.MagazineCapacity, gun.ReloadDuration, gun.AutomaticReload))
        { PublishCombat(); return; }
        PublishCombat();
        Physics.SyncTransforms();
        Vector3 origin = transform.TransformPoint(movement.PlayerCamera.transform.localPosition);
        LastServerShotOrigin = origin;
        Ray ray = new Ray(origin, Quaternion.Euler(command.Pitch, command.Yaw, 0) * Vector3.forward);
        NetworkPrototypePlayer target = ResolveCompensatedHit(ray, gun.Range, command.Time, out Vector3 endpoint);
        float actual = target != null && target != this ? target.ServerDamage(gun.Damage) : 0f;
        if (actual > 0 && selection.SelectedHero.PassivePrefab != null)
        {
            GunslingerLifesteal passive = selection.SelectedHero.PassivePrefab.GetComponent<GunslingerLifesteal>();
            if (passive != null && passive.Definition != null)
            {
                NetworkCombatSnapshot state = combatState.Value;
                state.Health += GunslingerRules.Healing(actual, passive.Definition.LifestealPercent,
                    state.Health, state.Maximum, !state.Dead, false);
                combatState.Value = state;
            }
        }
        AcceptedShotRpc(generation, endpoint, actual > 0);
    }
    private float ServerDamage(float damage)
    {
        if (!IsServer || revolver.Dead || !(damage > 0) || float.IsInfinity(damage)) return 0;
        NetworkCombatSnapshot state = combatState.Value;
        float actual = NetworkRevolverState.ActualDamage(state.Health, damage);
        state.Health -= actual;
        if (state.Health <= 0 && revolver.Die(ServerNow, networkRespawnDelay))
        { StopServerDash();
          state.Dead = true; state.Reloading = false; state.RespawnAt = revolver.RespawnAt; }
        combatState.Value = state;
        return actual;
    }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void AcceptedShotRpc(uint generation, Vector3 endpoint, bool hit)
    {
        if (generation != appliedRevision || selection.SelectedKit == null) return;
        HitscanGun gun = selection.SelectedKit.PrimaryWeapon as HitscanGun;
        if (gun == null) return;
        gun.NetworkPresentation = true;
        // Existing event subscribers supply recoil, tracer, world kick and confirmed crosshair.
        gun.PresentNetworkShot(endpoint, HasLocalControl && hit);
        selection.SelectedWorldPresentation?.PresentNetworkShot(endpoint);
        PlayerWeaponAudio audio = selection.SelectedKit.GetComponentInChildren<PlayerWeaponAudio>(true);
        if (audio == null || audio.GunClip == null) return;
        if (worldShotAudio == null)
        {
            worldShotAudio = gameObject.AddComponent<AudioSource>();
            worldShotAudio.playOnAwake = false;
            worldShotAudio.rolloffMode = AudioRolloffMode.Linear;
            worldShotAudio.minDistance = 1; worldShotAudio.maxDistance = 40;
        }
        worldShotAudio.spatialBlend = HasLocalControl ? 0f : 1f;
        worldShotAudio.PlayOneShot(audio.GunClip, audio.GunVolume);
    }
}
