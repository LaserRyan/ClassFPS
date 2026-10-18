using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class NetworkPrototypePlayer
{
    private const float MotorStep = 1f / 30f;
    private readonly Queue<NetworkMoveCommand> serverInputs = new Queue<NetworkMoveCommand>();
    private readonly List<NetworkMoveCommand> predictions = new List<NetworkMoveCommand>();
    private readonly List<NetworkPlayerPose> visualSamples = new List<NetworkPlayerPose>();
    private uint inputSequence, acceptedInput, acknowledgedInput, serverFrame;
    private float inputClock, serverClock;
    private double inputTokensAt, lastInputTime;
    private float inputTokens;
    private byte edgeButtons;
    private bool previouslyAllowed;
    private NetworkMoveCommand lastServerInput;
    private double lastServerInputAt;
    private void ResetNetworkMovement()
    {
        ClearNetworkMovement();
        inputSequence = acceptedInput = acknowledgedInput = serverFrame = 0;
        lastServerInput = default; lastServerInputAt = double.NegativeInfinity;
        inputClock = serverClock = 0; inputTokens = 4; inputTokensAt = ServerNow; lastInputTime = 0;
        if (IsServer) { PublishPose(); RecordHistory(); }
        else ReceiveMotorSnapshot(pose.Value);
    }
    private void ClearNetworkMovement()
    { serverInputs.Clear(); predictions.Clear(); visualSamples.Clear(); hitHistory.Clear(); edgeButtons = 0; previouslyAllowed = false; }
    private void TickNetworkMovement()
    {
        if (HasLocalControl) CollectMotorInput();
        if (IsServer)
        {
            serverClock = Mathf.Min(serverClock + Time.unscaledDeltaTime, MotorStep * 4);
            while (serverClock >= MotorStep)
            {
                serverClock -= MotorStep;
                if (combatState.Value.Dead) { serverInputs.Clear(); continue; }
                NetworkMoveCommand command;
                if (IsTestOpponent) command = OpponentCommand();
                else if (serverInputs.Count > 0)
                {
                    command = serverInputs.Dequeue(); lastServerInput = command; lastServerInputAt = ServerNow;
                }
                else
                {
                    // Brief input lease bridges packet jitter. Never repeat an action edge or shot.
                    command = lastServerInput;
                    command.Generation = appliedRevision; command.Sequence = 0; command.Time = ServerNow;
                    command.Buttons = ServerNow - lastServerInputAt <= .25 ?
                        (byte)(lastServerInput.Buttons & NetworkMoveCommand.Allowed) : (byte)0;
                    if (lastServerInputAt == double.NegativeInfinity)
                    { command.Yaw = transform.eulerAngles.y; command.Pitch = movement.MotorSnapshot(0, 0, 0, 0).Pitch; }
                }
                if (command.Generation != appliedRevision) continue;
                // Stalled transport never queues a burst of attacks/movement when a client resumes.
                if (ServerNow - command.Time > .5) { command.Buttons = 0; lastServerInput = command; }
                bool wasDashing = movement.NetworkDashing;
                bool started = movement.SimulateNetwork(command, MotorStep, ServerNow, DashDefinition);
                if (command.Sequence != 0) acknowledgedInput = command.Sequence;
                if (started)
                {
                    LastServerDashDirection = Quaternion.Euler(0, command.Yaw, 0) * Vector3.forward;
                    MotorDashRpc(appliedRevision, LastServerDashDirection, DashDefinition.DashDistance, DashDefinition.DashDuration);
                }
                if ((wasDashing || started) && !movement.NetworkDashing) EndMotorDashRpc(appliedRevision);
                RecordHistory();
                ExecuteServerCombat(command);
                PublishPose();
            }
        }
    }
    private void CollectMotorInput()
    {
        bool allowed = movement.CanAcceptGameplayInput && !pendingChange && !combatState.Value.Dead;
        if (!allowed && previouslyAllowed) CancelMotorInputRpc(appliedRevision);
        previouslyAllowed = allowed;
        bool dashEnabled = networkDash != null && networkDash.isActiveAndEnabled;
        Mouse mouse = Mouse.current; Keyboard keys = Keyboard.current;
        bool fireHeld = mouse != null && mouse.leftButton.isPressed;
        bool dashHeld = mouse != null && mouse.rightButton.isPressed;
        if (!allowed) { movement.TakeNetworkJump(); fireRequiresRelease = fireHeld; dashRequiresRelease = dashHeld; edgeButtons = 0; movement.CancelNetworkTravel(); }
        if (!fireHeld) fireRequiresRelease = false;
        if (!dashHeld) dashRequiresRelease = false;
        if (allowed)
        {
            if (movement.TakeNetworkJump()) edgeButtons |= NetworkMoveCommand.Jump;
            if (keys != null && keys.rKey.wasPressedThisFrame) edgeButtons |= NetworkMoveCommand.Reload;
            if (dashEnabled && dashHeld && !dashRequiresRelease && mouse.rightButton.wasPressedThisFrame)
            { edgeButtons |= NetworkMoveCommand.Dash; dashRequiresRelease = true; }
        }
        inputClock = Mathf.Min(inputClock + Time.unscaledDeltaTime, MotorStep * 4);
        while (inputClock >= MotorStep)
        {
            inputClock -= MotorStep;
            if (combatState.Value.Dead || predictions.Count >= 128) { edgeButtons = 0; continue; }
            Vector2 axes = movement.ReadNetworkMove();
            var command = new NetworkMoveCommand { Generation = appliedRevision, Sequence = ++inputSequence,
                Time = NetworkManager.ServerTime.Time, X = axes.x, Z = axes.y,
                Yaw = transform.eulerAngles.y, Pitch = Mathf.DeltaAngle(0, movement.PlayerCamera.transform.localEulerAngles.x),
                Buttons = allowed ? (byte)(edgeButtons | NetworkMoveCommand.Allowed |
                    (fireHeld && !fireRequiresRelease ? NetworkMoveCommand.Fire : 0) |
                    (!dashEnabled ? NetworkMoveCommand.CancelDash : 0)) : (byte)0 };
            edgeButtons = 0;
            if (!IsServer)
            {
                bool wasDashing = movement.NetworkDashing;
                bool dash = movement.SimulateNetwork(command, MotorStep, command.Time, DashDefinition);
                if (dash) networkDash?.BeginNetworkDash(transform.forward, DashDefinition.DashDistance, DashDefinition.DashDuration);
                if (wasDashing && !movement.NetworkDashing) networkDash?.Cancel();
                predictions.Add(command);
            }
            MoveInputRpc(command);
        }
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void MoveInputRpc(NetworkMoveCommand command, RpcParams rpc = default)
    {
        if (IsTestOpponent || rpc.Receive.SenderClientId != OwnerClientId || combatState.Value.Dead ||
            command.Generation != appliedRevision || !command.Valid || command.Sequence <= acceptedInput ||
            command.Time > ServerNow + .15 || ServerNow - command.Time > .5 || command.Time < lastInputTime) return;
        inputTokens = Mathf.Min(4, inputTokens + (float)(ServerNow - inputTokensAt) * 30);
        inputTokensAt = ServerNow;
        if (inputTokens < 1 || serverInputs.Count >= 8) return;
        inputTokens--; acceptedInput = command.Sequence; lastInputTime = command.Time;
        serverInputs.Enqueue(command);
    }
    private void ReceiveMotorSnapshot(NetworkPlayerPose state)
    {
        if (state.Revision != appliedRevision || !state.IsFinite) return;
        if (HasLocalControl && !IsServer)
        {
            float yaw = transform.eulerAngles.y;
            float pitchNow = Mathf.DeltaAngle(0, movement.PlayerCamera.transform.localEulerAngles.x);
            predictions.RemoveAll(command => command.Sequence <= state.Sequence || ServerNow - command.Time > .5);
            movement.RestoreMotor(state);
            foreach (NetworkMoveCommand command in predictions)
                movement.SimulateNetwork(command, MotorStep, command.Time, DashDefinition);
            // Reconciliation cannot consume the latest frame's mouse delta or replay effects.
            movement.PreserveNetworkLook(yaw, pitchNow);
        }
        else ApplyRemotePose(state);
        if (!HasLocalControl)
        {
            visualSamples.Add(state);
            if (visualSamples.Count > 32) visualSamples.RemoveAt(0);
        }
    }
    private void SampleRemoteVisual()
    {
        if (visualSamples.Count == 0) return;
        double renderTime = ServerNow - RenderDelay;
        NetworkPlayerPose a = visualSamples[0], b = a;
        foreach (NetworkPlayerPose sample in visualSamples)
        { if (sample.Time <= renderTime) a = sample; b = sample; if (sample.Time >= renderTime) break; }
        float t = b.Time > a.Time ? Mathf.Clamp01((float)((renderTime - a.Time) / (b.Time - a.Time))) : 0;
        visualPosition = Vector3.Lerp(a.Position, b.Position, t) + Quaternion.Euler(0, Mathf.LerpAngle(a.Yaw, b.Yaw, t), 0) * worldRestPosition;
        visualRotation = Quaternion.Euler(0, Mathf.LerpAngle(a.Yaw, b.Yaw, t), 0) * worldRestRotation;
        remotePitch = Mathf.Lerp(a.Pitch, b.Pitch, t);
    }
}
