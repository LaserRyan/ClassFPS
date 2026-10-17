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
    private uint requestSequence;
    private uint lastFirePoseSequence;
    private float nextFireIntent;
    private AudioSource worldShotAudio;
    private Collider[] actorColliders;
    private bool[] colliderDefaults;
    internal Vector3 LastServerShotOrigin { get; private set; }
    private double ServerNow => NetworkManager.ServerTime.Time;
    private GunWeaponDefinition ServerGun => selection.SelectedHero != null && CurrentHeroId == "prototype.gun" ?
        selection.SelectedHero.KitPrefab.PrimaryWeapon.Definition as GunWeaponDefinition : null;

    private void ResetServerCombat()
    {
        serverDash.Reset(hero.Value.Revision);
        lastFirePoseSequence = 0;
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
                next.Spawn = NetworkPrototypeSession.Active.SpawnFor(OwnerClientId);
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
        state.DashReadyAt = serverDash.ReadyAt;
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
    private void ReadCombatInput()
    {
        Mouse mouse = Mouse.current;
        bool held = mouse != null && mouse.leftButton.isPressed;
        if (!movement.CanAcceptGameplayInput || combatState.Value.Dead || pendingChange ||
            !(selection.SelectedKit.PrimaryWeapon is HitscanGun) || CurrentHeroId != "prototype.gun")
        { fireRequiresRelease = held; return; }
        if (!held) fireRequiresRelease = false;
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            RevolverIntentRpc(appliedRevision, ++requestSequence, CapturePose(), Vector3.forward, true);
        if (held && !fireRequiresRelease && Time.unscaledTime >= nextFireIntent)
        {
            // No prediction, buffering or local ammo consumption. Server accepts each shot.
            nextFireIntent = Time.unscaledTime + .05f;
            NetworkPlayerPose shotPose = CapturePose();
            pose.Value = shotPose;
            nextPoseAt = Time.unscaledTime + 1f / 30f;
            RevolverIntentRpc(appliedRevision, ++requestSequence, shotPose, movement.PlayerCamera.transform.forward, false);
        }
    }
    private static bool ValidAim(Vector3 aim) => !float.IsNaN(aim.x) && !float.IsNaN(aim.y) && !float.IsNaN(aim.z) &&
        !float.IsInfinity(aim.x) && !float.IsInfinity(aim.y) && !float.IsInfinity(aim.z) &&
        aim.sqrMagnitude > .9f && aim.sqrMagnitude < 1.1f;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RevolverIntentRpc(uint generation, uint sequence, NetworkPlayerPose shotPose, Vector3 aim, bool reload, RpcParams rpc = default)
    {
        if (!revolver.Request(generation, sequence, ServerNow, rpc.Receive.SenderClientId == OwnerClientId)) return;
        GunWeaponDefinition gun = ServerGun;
        if (gun == null || combatState.Value.Dead || !ValidAim(aim) || !shotPose.IsFinite ||
            shotPose.Revision != generation || shotPose.Sequence == 0 ||
            (!reload && shotPose.Sequence <= lastFirePoseSequence)) return;
        if (reload)
        { revolver.Reload(ServerNow, gun.MagazineCapacity, gun.ReloadDuration); PublishCombat(); return; }
        if (!revolver.Fire(ServerNow, gun.Cooldown, gun.MagazineCapacity, gun.ReloadDuration, gun.AutomaticReload))
        { PublishCombat(); return; }
        lastFirePoseSequence = shotPose.Sequence;
        PublishCombat();
        // Same-frame body pose follows the existing owner-authoritative movement policy.
        // It may arrive after a newer ordinary pose: never roll collision state backwards.
        ApplyRemotePose(shotPose);
        Physics.SyncTransforms();
        // Server supplies the eye offset; the owner supplies body movement, never an eye origin.
        Vector3 origin = shotPose.Position + Quaternion.Euler(0f, shotPose.Yaw, 0f) *
            movement.PlayerCamera.transform.localPosition;
        LastServerShotOrigin = origin;
        Ray ray = new Ray(origin, aim.normalized);
        HitscanGun source = selection.SelectedHero.KitPrefab.PrimaryWeapon as HitscanGun;
        RaycastHit[] hits = Physics.RaycastAll(ray, gun.Range, source.HitLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        Vector3 endpoint = ray.GetPoint(gun.Range);
        NetworkPrototypePlayer target = null;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            NetworkPrototypePlayer actor = hit.collider.GetComponentInParent<NetworkPrototypePlayer>();
            if (actor != null && actor.combatState.Value.Dead) continue;
            if (hit.distance >= nearest) continue;
            nearest = hit.distance; endpoint = hit.point; target = actor;
        }
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
        { StopServerDash(hero.Value.Revision, serverDash.ActiveRequest);
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
        gun.PresentNetworkShot(endpoint, IsOwner && hit);
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
        worldShotAudio.spatialBlend = IsOwner ? 0f : 1f;
        worldShotAudio.PlayOneShot(audio.GunClip, audio.GunVolume);
    }
}
