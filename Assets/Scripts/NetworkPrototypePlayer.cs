using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Server movement/combat; owning clients predict the shared collision motor.
[RequireComponent(typeof(NetworkObject), typeof(HeroSelection))]
[DefaultExecutionOrder(-100)]
public sealed partial class NetworkPrototypePlayer : NetworkBehaviour
{
    private readonly NetworkVariable<NetworkHeroState> hero = new NetworkVariable<NetworkHeroState>();
    private readonly NetworkVariable<NetworkPlayerPose> pose = new NetworkVariable<NetworkPlayerPose>(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> testChangesAllowed = new NetworkVariable<bool>();
    private FPSController movement;
    private HeroSelection selection;
    private PlayerHealth health;
    private uint appliedRevision;
    private readonly NetworkPoseOrder poseOrder = new NetworkPoseOrder();
    private Vector3 visualPosition, worldRestPosition;
    private Quaternion visualRotation, worldRestRotation;
    private float remotePitch;
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
            isTestOpponent.Value = initialTestOpponent;
            renderDelay.Value = NetworkPrototypeSession.Active.InterpolationDelay;
            hero.Value = initialHero;
            testChangesAllowed.Value = initialAllowChanges;
        }
        movement.NetworkDriven = true;
        movement.SetLocalInputOwnership(HasLocalControl);
        hero.OnValueChanged += OnHeroChanged;
        pose.OnValueChanged += OnPoseChanged;
        combatState.OnValueChanged += OnCombatChanged;
        ApplyHero(hero.Value);
        SetLocalComponents(HasLocalControl);
        if (HasLocalControl)
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
        UnbindNetworkDash();
        selection.ApplyNetworkHero(definition, HasLocalControl, state.Spawn, Quaternion.Euler(0f, state.Yaw, 0f));
        appliedRevision = state.Revision;
        poseOrder.Reset(appliedRevision);
        remotePitch = 0;
        if (selection.SelectedWorldPresentation != null)
        {
            Transform world = selection.SelectedWorldPresentation.transform;
            worldRestPosition = world.localPosition;
            worldRestRotation = world.localRotation;
            visualPosition = world.position;
            visualRotation = world.rotation;
        }
        if (worldShotAudio != null) worldShotAudio.Stop();
        fireRequiresRelease = true;
        BindNetworkDash();
        if (IsServer) ResetServerCombat();
        ApplyCombatSnapshot();
        ResetNetworkMovement();
    }
    private void Update()
    {
        if (!IsSpawned) return;
        if (IsServer) TickServerCombat();
        if (!HasLocalControl) return;
        UpdateOpponentMenu();
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
        TickNetworkMovement();
        if (!HasLocalControl) SmoothRemotePresentation(Time.unscaledDeltaTime);
    }
    private void PublishPose() { if (IsServer) pose.Value = movement.MotorSnapshot(appliedRevision, acknowledgedInput, ++serverFrame, ServerNow); }
    private void OnPoseChanged(NetworkPlayerPose previous, NetworkPlayerPose current) => ReceiveMotorSnapshot(current);
    private void ApplyRemotePose(NetworkPlayerPose target)
    {
        if (HasLocalControl || IsServer || !poseOrder.TryAccept(target.Revision, target.Frame, target.IsFinite,
            appliedRevision != 0 && !combatState.Value.Dead)) return;
        // Collision/combat transforms never chase a visually smoothed position.
        transform.SetPositionAndRotation(target.Position, Quaternion.Euler(0f, target.Yaw, 0f));
        remotePitch = target.Pitch;
        if (selection.SelectedWorldPresentation != null)
            selection.SelectedWorldPresentation.transform.SetPositionAndRotation(visualPosition, visualRotation);
    }
    private void SmoothRemotePresentation(float seconds)
    {
        WorldHeroPresentation world = selection.SelectedWorldPresentation;
        if (world == null) return;
        SampleRemoteVisual();
        // Timestamp interpolation above is also the server rewind view delay.
        world.transform.SetPositionAndRotation(visualPosition, visualRotation);
        world.SetAimPitch(remotePitch);
    }
    private void SetTestMenu(bool open)
    {
        if (open) opponentMenu = false;
        testMenu = open;
        movement.SetHeroSelection(open || opponentMenu);
        GetComponent<PlayerCombatInput>().RequirePrimaryRelease();
    }
    private void RequestHero(string id)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!HasLocalControl || pendingChange || !testChangesAllowed.Value) return;
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
        if (!IsSpawned || !HasLocalControl || pendingChange) return;
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
        DrawOpponentMenu();
        if (IsSpawned && HasLocalControl && combatState.Value.Dead)
            GUI.Box(new Rect(Screen.width * .5f - 180, Screen.height * .5f - 50, 360, 100),
                $"You died\nRespawning in {Mathf.CeilToInt((float)System.Math.Max(0, combatState.Value.RespawnAt - NetworkManager.ServerTime.Time))}s");
        if (!IsSpawned || !HasLocalControl || !testMenu || !NetworkTestRules.DevelopmentFeatures) return;
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
        ClearNetworkMovement();
        UnbindNetworkDash();
        hero.OnValueChanged -= OnHeroChanged;
        pose.OnValueChanged -= OnPoseChanged;
        combatState.OnValueChanged -= OnCombatChanged;
        if (worldShotAudio != null) worldShotAudio.Stop();
        if (HasLocalControl)
        {
            movement.enabled = false;
            movement.SetLocalInputOwnership(false);
            if (NetworkPrototypeSession.Active != null && NetworkPrototypeSession.Active.LocalPlayer == this)
                NetworkPrototypeSession.Active.LocalPlayer = null;
        }
        pendingChange = testMenu = false;
    }
}
