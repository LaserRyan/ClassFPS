using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class NetworkPrototypePlayer
{
    private const float DashApprovalTimeout = 1f;
    private readonly NetworkDashState serverDash = new NetworkDashState();
    private GunslingerDash networkDash;
    private uint dashRequestSequence, presentedDashRequest;
    private bool dashRequiresRelease = true, dashPending;
    private float dashPendingUntil;
    internal Vector3 LastServerDashDirection { get; private set; }
    private GunslingerDefinition DashDefinition => CurrentHeroId == "prototype.gun" && selection.SelectedHero != null ?
        selection.SelectedHero.SecondaryActionPrefab?.GetComponent<GunslingerDash>()?.Definition : null;

    private void BindNetworkDash()
    {
        dashRequestSequence = presentedDashRequest = 0;
        dashPending = false;
        dashRequiresRelease = true;
        networkDash = movement.Dash;
        if (networkDash == null) return;
        networkDash.NetworkCooldownRemaining = () => combatState.Value.Generation == appliedRevision ?
            (float)System.Math.Max(0, combatState.Value.DashReadyAt - ServerNow) : 0f;
        if (IsOwner) networkDash.Stopped += OnLocalDashStopped;
    }
    private void UnbindNetworkDash()
    {
        if (networkDash != null)
        {
            networkDash.Stopped -= OnLocalDashStopped;
            networkDash.NetworkCooldownRemaining = null;
            networkDash.NetworkRequestPending = false;
            networkDash.Cancel();
        }
        networkDash = null;
        dashPending = false;
        presentedDashRequest = 0;
    }
    private void ReadDashInput()
    {
        if (networkDash == null) return;
        Mouse mouse = Mouse.current;
        bool held = mouse != null && mouse.rightButton.isPressed;
        if (!movement.CanAcceptGameplayInput || combatState.Value.Dead || pendingChange ||
            !networkDash.isActiveAndEnabled || CurrentHeroId != "prototype.gun")
        {
            CancelLocalDashRequest();
            dashRequiresRelease = held;
            return;
        }
        if (dashPending && Time.unscaledTime >= dashPendingUntil) CancelLocalDashRequest();
        if (!held) dashRequiresRelease = false;
        else if (!dashRequiresRelease && mouse.rightButton.wasPressedThisFrame)
        {
            dashRequiresRelease = true;
            if (dashPending || networkDash.IsDashing || networkDash.CooldownRemaining > 0) return;
            dashPending = networkDash.NetworkRequestPending = true;
            dashPendingUntil = Time.unscaledTime + DashApprovalTimeout;
            NetworkPlayerPose sample = CapturePose();
            pose.Value = sample;
            RequestDashRpc(appliedRevision, ++dashRequestSequence, sample);
        }
    }
    private void CancelLocalDashRequest()
    {
        if (IsOwner && IsSpawned && dashPending)
            CancelDashRpc(appliedRevision, dashRequestSequence);
        dashPending = false;
        if (networkDash != null)
        {
            networkDash.NetworkRequestPending = false;
            networkDash.Cancel();
        }
    }
    private void OnLocalDashStopped()
    {
        if (!IsOwner || !IsSpawned || presentedDashRequest == 0) return;
        uint request = presentedDashRequest;
        presentedDashRequest = 0;
        CancelDashRpc(appliedRevision, request);
    }
    private void OnDisable()
    {
        if (IsOwner && IsSpawned) CancelLocalDashRequest();
    }
    private void OnApplicationFocus(bool focused)
    {
        if (!focused && IsOwner && IsSpawned)
        { dashRequiresRelease = true; CancelLocalDashRequest(); }
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestDashRpc(uint generation, uint sequence, NetworkPlayerPose sample, RpcParams rpc = default)
    {
        GunslingerDefinition definition = DashDefinition;
        bool valid = sample.IsFinite && sample.Sequence != 0 && sample.Revision == generation;
        bool accepted = serverDash.TryStart(generation, sequence, ServerNow,
            rpc.Receive.SenderClientId == OwnerClientId, definition != null,
            !combatState.Value.Dead && !serverChanging, valid,
            definition != null ? definition.DashDistance : 0,
            definition != null ? definition.DashDuration : 0,
            definition != null ? definition.DashCooldown : 0);
        if (!accepted) { DashRejectedRpc(generation, sequence); return; }
        ApplyRemotePose(sample);
        PublishCombat();
        // Direction is captured from body yaw. Camera pitch never affects dash travel.
        LastServerDashDirection = Quaternion.Euler(0, sample.Yaw, 0) * Vector3.forward;
        AcceptedDashRpc(generation, sequence, LastServerDashDirection, definition.DashDistance, definition.DashDuration);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void DashRejectedRpc(uint generation, uint sequence)
    {
        if (generation != appliedRevision || sequence != dashRequestSequence) return;
        dashPending = false;
        if (networkDash != null) networkDash.NetworkRequestPending = false;
    }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void AcceptedDashRpc(uint generation, uint sequence, Vector3 direction, float distance, float duration)
    {
        if (generation != appliedRevision || networkDash == null) return;
        if (IsOwner)
        {
            if (!dashPending || sequence != dashRequestSequence || Time.unscaledTime >= dashPendingUntil ||
                !movement.CanMaintainGameplayInput ||
                combatState.Value.Dead || pendingChange || !networkDash.isActiveAndEnabled)
            {
                if (sequence == dashRequestSequence) dashPending = networkDash.NetworkRequestPending = false;
                CancelDashRpc(generation, sequence);
                return;
            }
            dashPending = networkDash.NetworkRequestPending = false;
        }
        presentedDashRequest = sequence;
        networkDash.BeginNetworkDash(direction, distance, duration);
    }
    private void StopServerDash(uint generation, uint request)
    {
        if (serverDash.Cancel(generation, request)) StoppedDashRpc(generation, request);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void CancelDashRpc(uint generation, uint request, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId == OwnerClientId) StopServerDash(generation, request);
    }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void StoppedDashRpc(uint generation, uint request)
    {
        if (generation != appliedRevision || request != presentedDashRequest) return;
        presentedDashRequest = 0;
        networkDash?.Cancel();
    }
}
