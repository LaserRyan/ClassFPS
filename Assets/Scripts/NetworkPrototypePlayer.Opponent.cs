using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class NetworkPrototypePlayer
{
    private readonly NetworkVariable<bool> isTestOpponent = new NetworkVariable<bool>();
    private bool initialTestOpponent, opponentMenu;
    private bool HasLocalControl => IsOwner && !isTestOpponent.Value;
    public bool IsTestOpponent => isTestOpponent.Value;
    private bool opponentStrafes, opponentFires, opponentSingleShot;
    private ulong opponentController;
    private uint opponentSequence;
    private double nextOpponentRequest;
    internal void InitializeOpponent(HeroDefinition definition, Vector3 position, float yaw, ulong controllerId)
    {
        InitializeServer(definition, position, yaw, false);
        initialTestOpponent = true; opponentController = controllerId;
    }
    private NetworkMoveCommand OpponentCommand()
    {
        var command = new NetworkMoveCommand { Generation = appliedRevision, Sequence = ++opponentSequence,
            Time = ServerNow, Yaw = transform.eulerAngles.y, Buttons = NetworkMoveCommand.Allowed };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!NetworkPrototypeSession.Active.AllowTestOpponent) return command;
        if (NetworkManager.ConnectedClients.TryGetValue(opponentController, out NetworkClient client) && client.PlayerObject != null)
        {
            NetworkPrototypePlayer target = client.PlayerObject.GetComponent<NetworkPrototypePlayer>();
            Vector3 direction = target.transform.position + Vector3.up * 1.6f - (transform.position + Vector3.up * 1.6f);
            Quaternion aim = direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction) : transform.rotation;
            command.Yaw = aim.eulerAngles.y; command.Pitch = Mathf.Clamp(Mathf.DeltaAngle(0, aim.eulerAngles.x), -89, 89);
            if (!target.combatState.Value.Dead && (opponentFires || opponentSingleShot)) command.Buttons |= NetworkMoveCommand.Fire;
            if (opponentStrafes)
            {
                float side = Mathf.Sin((float)ServerNow * 1.3f) >= 0 ? 1 : -1;
                float offset = Vector3.Dot(transform.position - initialHero.Spawn, transform.right);
                if (Mathf.Abs(offset) > 2.5f) side = -Mathf.Sign(offset);
                command.X = side;
            }
        }
        opponentSingleShot = false;
#endif
        return command;
    }
    internal void SetOpponentAction(byte action)
    {
        if (!IsServer || !IsTestOpponent || !NetworkPrototypeSession.Active.AllowTestOpponent) return;
        if (action == 1) opponentStrafes = !opponentStrafes;
        if (action == 2) opponentSingleShot = true;
        if (action == 3) opponentFires = !opponentFires;
        if (action == 4)
        {
            opponentFires = opponentStrafes = opponentSingleShot = false;
            NetworkHeroState next = hero.Value; next.Revision++; next.Spawn = initialHero.Spawn; hero.Value = next;
        }
    }
    private void UpdateOpponentMenu()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame)
        {
            opponentMenu = !opponentMenu;
            testMenu = false;
            movement.SetHeroSelection(opponentMenu);
            fireRequiresRelease = dashRequiresRelease = true;
        }
#endif
    }
    private void DrawOpponentMenu()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!IsSpawned || !HasLocalControl || !opponentMenu) return;
        GUILayout.BeginArea(new Rect(405, 140, 340, 350), GUI.skin.box);
        GUILayout.Label("Development network opponent");
        GUILayout.Label("Host must enable Allow Test Opponent before Host.");
        if (GUILayout.Button("Spawn Gunslinger (one per session)")) OpponentRequestRpc(0);
        if (GUILayout.Button("Toggle strafe")) OpponentRequestRpc(1);
        if (GUILayout.Button("Fire once (cooldown still applies)")) OpponentRequestRpc(2);
        if (GUILayout.Button("Toggle repeated shooting")) OpponentRequestRpc(3);
        if (GUILayout.Button("Reset opponent / stop actions")) OpponentRequestRpc(4);
        if (GUILayout.Button("Remove opponent")) OpponentRequestRpc(5);
        if (GUILayout.Button("Close")) { opponentMenu = false; movement.SetHeroSelection(false); }
        GUILayout.EndArea();
#endif
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void OpponentRequestRpc(byte action, RpcParams rpc = default)
    {
        if (IsTestOpponent || rpc.Receive.SenderClientId != OwnerClientId || ServerNow < nextOpponentRequest || action > 5) return;
        nextOpponentRequest = ServerNow + .2;
        bool accepted = NetworkPrototypeSession.Active.HandleOpponent(this, action);
        OpponentResultRpc(accepted);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void OpponentResultRpc(bool accepted) => NetworkPrototypeSession.Active.ReportStatus(accepted ?
        "Opponent command accepted." : "Opponent command rejected: host setting, build or controller permission.");
}
public sealed partial class NetworkPrototypeSession
{
    [SerializeField] private bool allowTestOpponent;
    [SerializeField, Range(.05f, .15f)] private float interpolationDelay = .1f;
    [SerializeField, Range(0f, .3f)] private float maximumRewind = .2f;
    public bool AllowTestOpponent => allowTestOpponent && NetworkTestRules.DevelopmentFeatures;
    public float InterpolationDelay => Mathf.Clamp(interpolationDelay, .05f, .15f);
    public float MaximumRewind => Mathf.Clamp(maximumRewind, 0, .3f);
    private NetworkPrototypePlayer testOpponent;
    private ulong testOpponentController;
    internal bool HandleOpponent(NetworkPrototypePlayer requester, byte action)
    {
        if (!manager.IsServer || requester == null || action > 5) return false;
        bool connected = requester.IsSpawned && !requester.IsTestOpponent &&
            manager.ConnectedClients.TryGetValue(requester.OwnerClientId, out NetworkClient client) && client.PlayerObject == requester.NetworkObject;
        bool permitted = testOpponent == null || requester.OwnerClientId == testOpponentController || requester.OwnerClientId == NetworkManager.ServerClientId;
        if (!NetworkTestRules.CanUseOpponent(allowTestOpponent, connected, permitted)) return false;
        if (action == 5)
        { if (testOpponent != null) testOpponent.NetworkObject.Despawn(); testOpponent = null; return true; }
        if (action == 0 && testOpponent == null)
        {
            HeroDefinition definition = ResolveHero("prototype.gun");
            if (definition == null || !TryOpponentSpawn(requester, out Vector3 spawn)) return false;
            testOpponentController = requester.OwnerClientId;
            testOpponent = Instantiate(playerPrefab, spawn, Quaternion.identity);
            testOpponent.InitializeOpponent(definition, spawn, 0, testOpponentController);
            testOpponent.NetworkObject.Spawn(true);
        }
        if (testOpponent == null) return false;
        testOpponent.SetOpponentAction(action);
        return true;
    }
    private bool TryOpponentSpawn(NetworkPrototypePlayer requester, out Vector3 position)
    {
        for (int i = 0; i < 9; i++)
        {
            Vector3 candidate = requester.transform.position + requester.transform.forward * (5 + i / 3 * 2) +
                requester.transform.right * ((i % 3 - 1) * 2);
            if (!Physics.Raycast(candidate + Vector3.up * 3, Vector3.down, out RaycastHit floor, 8, ~0, QueryTriggerInteraction.Ignore)) continue;
            candidate = floor.point + Vector3.up * .05f;
            if (Physics.CheckCapsule(candidate + Vector3.up * .4f, candidate + Vector3.up * 1.6f, .39f, ~0, QueryTriggerInteraction.Ignore)) continue;
            position = candidate; return true;
        }
        position = default; return false;
    }
    private void RemoveTestOpponentFor(ulong clientId)
    {
        if (testOpponent == null || testOpponentController != clientId) return;
        if (testOpponent.IsSpawned) testOpponent.NetworkObject.Despawn();
        testOpponent = null;
    }
}
