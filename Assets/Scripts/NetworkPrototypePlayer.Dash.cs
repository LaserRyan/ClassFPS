using Unity.Netcode;
using UnityEngine;

public sealed partial class NetworkPrototypePlayer
{
    private GunslingerDash networkDash;
    private bool dashRequiresRelease = true;
    internal Vector3 LastServerDashDirection { get; private set; }
    private GunslingerDefinition DashDefinition => networkDash != null && networkDash.isActiveAndEnabled && CurrentHeroId == "prototype.gun" && selection.SelectedHero != null ?
        selection.SelectedHero.SecondaryActionPrefab?.GetComponent<GunslingerDash>()?.Definition : null;
    private void BindNetworkDash()
    {
        dashRequiresRelease = true;
        networkDash = movement.Dash;
        if (networkDash != null) networkDash.NetworkCooldownRemaining = () =>
            (float)System.Math.Max(0, movement.NetworkDashReady - ServerNow);
    }
    private void UnbindNetworkDash()
    {
        if (networkDash != null) { networkDash.NetworkCooldownRemaining = null; networkDash.Cancel(); }
        networkDash = null;
    }
    private void CancelLocalDashRequest() { movement.CancelNetworkTravel(); networkDash?.Cancel(); }
    private void OnDisable()
    {
        if (!HasLocalControl || !IsSpawned) return;
        CancelLocalDashRequest();
        if (NetworkManager != null && NetworkManager.IsListening && !NetworkManager.ShutdownInProgress)
            CancelMotorInputRpc(appliedRevision);
    }
    private void OnApplicationFocus(bool focused)
    { if (!focused && HasLocalControl && IsSpawned) { dashRequiresRelease = true; CancelLocalDashRequest(); CancelMotorInputRpc(appliedRevision); } }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void CancelMotorInputRpc(uint generation, RpcParams rpc = default)
    {
        if (generation != appliedRevision || IsTestOpponent || rpc.Receive.SenderClientId != OwnerClientId) return;
        serverInputs.Clear(); lastServerInput.Buttons = 0; lastServerInput.X = lastServerInput.Z = 0;
        movement.CancelNetworkTravel(); EndMotorDashRpc(generation);
    }
    private void StopServerDash() { movement.CancelNetworkTravel(); networkDash?.Cancel(); }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void EndMotorDashRpc(uint generation)
    { if (generation == appliedRevision) networkDash?.Cancel(); }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void MotorDashRpc(uint generation, Vector3 direction, float distance, float duration)
    {
        if (generation != appliedRevision || networkDash == null || (HasLocalControl && !IsServer)) return;
        networkDash.BeginNetworkDash(direction, distance, duration);
    }
}
