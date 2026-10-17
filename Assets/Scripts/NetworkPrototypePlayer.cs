using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Owner movement, server-owned hero identity, combat and lifecycle.
[RequireComponent(typeof(NetworkObject), typeof(HeroSelection))]
[DefaultExecutionOrder(-100)]
public sealed partial class NetworkPrototypePlayer : NetworkBehaviour
{
    private readonly NetworkVariable<NetworkHeroState> hero = new NetworkVariable<NetworkHeroState>();
    private readonly NetworkVariable<NetworkPlayerPose> pose = new NetworkVariable<NetworkPlayerPose>(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<bool> testChangesAllowed = new NetworkVariable<bool>();
    private FPSController movement;
    private HeroSelection selection;
    private PlayerHealth health;
    private uint appliedRevision;
    private float nextPoseAt;
    private bool testMenu;
    private bool pendingChange;
    private bool serverChanging;
    private float pendingDeadline;
    private string changeMessage;
    private NetworkHeroState initialHero;
    private bool initialAllowChanges;
    private Vector2 menuScroll;
    public string CurrentHeroId => hero.Value.HeroId.ToString();

    private void Awake()
    {
        actorColliders = GetComponentsInChildren<Collider>(true);
        colliderDefaults = new bool[actorColliders.Length];
        for (int i = 0; i < actorColliders.Length; i++) colliderDefaults[i] = actorColliders[i].enabled;
        movement = GetComponent<FPSController>();
        selection = GetComponent<HeroSelection>();
        health = GetComponent<PlayerHealth>();
        health.NetworkProtected = health.DamageBlocked = true;
        // Prefab defaults are inert too, so no remote component captures input before spawn.
        selection.enabled = false;
        GetComponent<PlayerLifecycle>().enabled = false;
        GetComponent<PlayerCombatInput>().enabled = false;
    }
    public void InitializeServer(HeroDefinition definition, Vector3 spawn, float yaw, bool allowChanges)
    {
        initialHero = new NetworkHeroState { HeroId = new FixedString64Bytes(definition.HeroId),
            Spawn = spawn, Yaw = yaw, Revision = 1 };
        initialAllowChanges = NetworkTestRules.DevelopmentFeatures && allowChanges;
    }
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            hero.Value = initialHero;
            testChangesAllowed.Value = initialAllowChanges;
        }
        movement.SetLocalInputOwnership(IsOwner);
        hero.OnValueChanged += OnHeroChanged;
        combatState.OnValueChanged += OnCombatChanged;
        ApplyHero(hero.Value);
        SetLocalComponents(IsOwner);
        if (IsOwner)
        {
            NetworkPrototypeSession.Active.LocalPlayer = this;
            NetworkPrototypeSession.Active.ShowLobbyCamera(false);
        }
    }
    private void SetLocalComponents(bool owner)
    {
        foreach (Camera camera in GetComponentsInChildren<Camera>(true)) camera.enabled = owner && camera == movement.PlayerCamera;
        foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true))
            listener.enabled = owner && listener.transform == movement.PlayerCamera.transform;
        foreach (PlayerHealthHUD hud in GetComponentsInChildren<PlayerHealthHUD>(true)) hud.enabled = owner;
        foreach (WeaponAmmoHUD hud in GetComponentsInChildren<WeaponAmmoHUD>(true)) hud.enabled = owner;
        foreach (CombatCrosshair hud in GetComponentsInChildren<CombatCrosshair>(true)) hud.enabled = owner;
        foreach (PlayerDeathHUD hud in GetComponentsInChildren<PlayerDeathHUD>(true)) hud.enabled = false;
        foreach (PlayerHealthDebugInput debug in GetComponentsInChildren<PlayerHealthDebugInput>(true)) debug.enabled = false;
        movement.enabled = owner;
    }
    private void OnHeroChanged(NetworkHeroState oldState, NetworkHeroState newState) => ApplyHero(newState);
    private void ApplyHero(NetworkHeroState state)
    {
        if (state.Revision == 0 || state.Revision == appliedRevision) return;
        HeroDefinition definition = NetworkPrototypeSession.Active.ResolveHero(state.HeroId.ToString());
        if (definition == null) { Debug.LogError("Received unknown hero identity.", this); return; }
        health.NetworkProtected = health.DamageBlocked = true;
        GetComponent<BurnStatus>()?.Clear();
        selection.ApplyNetworkHero(definition, IsOwner, state.Spawn, Quaternion.Euler(0f, state.Yaw, 0f));
        appliedRevision = state.Revision;
        if (worldShotAudio != null) worldShotAudio.Stop();
        fireRequiresRelease = true;
        requestSequence = 0;
        nextFireIntent = 0;
        if (IsServer) ResetServerCombat();
        ApplyCombatSnapshot();
        if (IsOwner) PublishPose();
    }
    private void Update()
    {
        if (!IsSpawned) return;
        if (IsServer) TickServerCombat();
        if (!IsOwner) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (testChangesAllowed.Value && Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
        {
            if (!pendingChange) SetTestMenu(!testMenu);
        }
#endif
        if (pendingChange && Time.unscaledTime >= pendingDeadline)
        {
            pendingChange = false;
            changeMessage = "Request timed out. Reopen the menu to try again.";
            NetworkPrototypeSession.Active.ReportStatus(changeMessage);
            SetTestMenu(false);
        }
    }
    private void LateUpdate()
    {
        if (!IsSpawned || appliedRevision == 0) return;
        if (IsOwner)
        {
            ReadCombatInput();
            if (Time.unscaledTime >= nextPoseAt && !pendingChange && !combatState.Value.Dead)
            { PublishPose(); nextPoseAt = Time.unscaledTime + 1f / 30f; }
        }
        else
        {
            if (combatState.Value.Dead) return;
            NetworkPlayerPose target = pose.Value;
            // A respawn revision prevents old movement from undoing the server's spawn.
            if (target.Revision != appliedRevision || !target.IsFinite) return;
            float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, target.Position, blend),
                Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, target.Yaw, 0f), blend));
            selection.SelectedWorldPresentation?.SetAimPitch(target.Pitch);
        }
    }
    private void PublishPose()
    {
        pose.Value = new NetworkPlayerPose { Position = transform.position, Yaw = transform.eulerAngles.y,
            Pitch = Mathf.DeltaAngle(0f, movement.PlayerCamera.transform.localEulerAngles.x), Revision = appliedRevision };
    }
    private void SetTestMenu(bool open)
    {
        testMenu = open;
        movement.SetHeroSelection(open);
        GetComponent<PlayerCombatInput>().RequirePrimaryRelease();
    }
    private void RequestHero(string id)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!IsOwner || pendingChange || !testChangesAllowed.Value) return;
        pendingChange = true;
        pendingDeadline = Time.unscaledTime + 8f;
        ChangeHeroRpc(new FixedString64Bytes(id), appliedRevision);
#endif
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Explicit validation probe: bypasses the local menu advertisement, never server policy.
    // Use an Editor client against a release host to exercise actual wire rejection.
    [ContextMenu("Development/Probe server test hero gate")]
    public void ProbeServerTestGate()
    {
        if (!IsSpawned || !IsOwner || pendingChange) return;
        foreach (HeroDefinition candidate in NetworkPrototypeSession.Active.Heroes)
        {
            if (candidate.HeroId == CurrentHeroId) continue;
            SetTestMenu(true);
            pendingChange = true;
            pendingDeadline = Time.unscaledTime + 8f;
            NetworkPrototypeSession.Active.ReportStatus("Sent development server-gate validation probe.");
            ChangeHeroRpc(new FixedString64Bytes(candidate.HeroId), appliedRevision);
            break;
        }
    }
#endif
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ChangeHeroRpc(FixedString64Bytes id, uint expectedRevision, RpcParams rpc = default)
    {
        NetworkPrototypeSession session = NetworkPrototypeSession.Active;
        HeroDefinition definition = session.ResolveHero(id.ToString());
        bool accepted = NetworkTestRules.CanChangeHero(session.AllowTestHeroChanges,
            rpc.Receive.SenderClientId == OwnerClientId, definition != null, serverChanging,
            expectedRevision, hero.Value.Revision);
        if (accepted)
        {
            serverChanging = true;
            try
            {
                hero.Value = new NetworkHeroState { HeroId = id, Revision = hero.Value.Revision + 1,
                    Spawn = session.SpawnFor(OwnerClientId), Yaw = session.SpawnYaw };
            }
            finally { serverChanging = false; }
        }
        ChangeResultRpc(accepted);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void ChangeResultRpc(bool accepted)
    {
        pendingChange = false;
        changeMessage = accepted ? "Hero changed; click Game to recapture." : "Server rejected the test hero change.";
        NetworkPrototypeSession.Active.ReportStatus(changeMessage);
        SetTestMenu(false);
    }
    private void OnGUI()
    {
        if (IsSpawned && IsOwner && combatState.Value.Dead)
            GUI.Box(new Rect(Screen.width * .5f - 180, Screen.height * .5f - 50, 360, 100),
                $"You died\nRespawning in {Mathf.CeilToInt((float)System.Math.Max(0, combatState.Value.RespawnAt - NetworkManager.ServerTime.Time))}s");
        if (!IsSpawned || !IsOwner || !testMenu || !NetworkTestRules.DevelopmentFeatures) return;
        GUILayout.BeginArea(new Rect(20f, 140f, 380f, Mathf.Min(420f, Screen.height - 150f)), GUI.skin.box);
        GUILayout.Label("Development test hero change (respawns player)");
        menuScroll = GUILayout.BeginScrollView(menuScroll);
        GUI.enabled = !pendingChange;
        foreach (HeroDefinition definition in NetworkPrototypeSession.Active.Heroes)
            if (GUILayout.Button(definition.DisplayName)) RequestHero(definition.HeroId);
        if (GUILayout.Button("Close")) SetTestMenu(false);
        GUI.enabled = true;
        GUILayout.EndScrollView();
        GUILayout.Label(pendingChange ? "Waiting for server..." : changeMessage);
        GUILayout.EndArea();
    }
    public override void OnNetworkDespawn()
    {
        hero.OnValueChanged -= OnHeroChanged;
        combatState.OnValueChanged -= OnCombatChanged;
        if (worldShotAudio != null) worldShotAudio.Stop();
        if (IsOwner)
        {
            movement.enabled = false;
            movement.SetLocalInputOwnership(false);
            if (NetworkPrototypeSession.Active != null && NetworkPrototypeSession.Active.LocalPlayer == this)
                NetworkPrototypeSession.Active.LocalPlayer = null;
        }
        pendingChange = testMenu = false;
    }
}
