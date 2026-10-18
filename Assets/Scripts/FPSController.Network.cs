using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class FPSController
{
    internal bool NetworkDriven;
    private bool networkJump, networkGrounded;
    internal void PreserveNetworkLook(float yaw, float lookPitch)
    {
        transform.rotation = Quaternion.Euler(0, yaw, 0); pitch = lookPitch;
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
    }
    private float networkDashLeft, networkDashSpeed;
    private Vector3 networkDashDirection;
    private double networkDashReady;
    internal bool NetworkDashing => networkDashLeft > 0;
    internal double NetworkDashReady => networkDashReady;
    internal bool TakeNetworkJump() { bool result = networkJump; networkJump = false; return result; }
    internal Vector2 ReadNetworkMove()
    {
        Keyboard keys = Keyboard.current;
        if (!CanAcceptGameplayInput || keys == null) return Vector2.zero;
        return Vector2.ClampMagnitude(new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
            (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0)), 1);
    }
    internal void CancelNetworkTravel() { networkDashLeft = 0; }
    internal NetworkPlayerPose MotorSnapshot(uint generation, uint sequence, uint frame, double now) => new NetworkPlayerPose
    {
        Revision = generation, Sequence = sequence, Frame = frame, Time = now,
        Position = transform.position, Yaw = transform.eulerAngles.y, Pitch = pitch,
        Grounded = networkGrounded, Vertical = verticalVelocity, DashLeft = networkDashLeft, DashSpeed = networkDashSpeed,
        DashDirection = networkDashDirection, DashReady = networkDashReady
    };
    internal void RestoreMotor(NetworkPlayerPose state)
    {
        bool active = controller.enabled; controller.enabled = false;
        transform.SetPositionAndRotation(state.Position, Quaternion.Euler(0, state.Yaw, 0));
        controller.enabled = active;
        networkGrounded = state.Grounded; pitch = state.Pitch; verticalVelocity = state.Vertical;
        networkDashLeft = state.DashLeft; networkDashSpeed = state.DashSpeed;
        networkDashDirection = state.DashDirection; networkDashReady = state.DashReady;
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
    }
    // Both prediction and authority run this motor. Only its caller can accept combat/effects.
    internal bool SimulateNetwork(NetworkMoveCommand command, float seconds, double now, GunslingerDefinition dash)
    {
        if (!controller.enabled || gameplayBlocked) return false;
        transform.rotation = Quaternion.Euler(0, command.Yaw, 0);
        pitch = command.Pitch; playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        bool allowed = command.Has(NetworkMoveCommand.Allowed);
        if (!allowed || dash == null || command.Has(NetworkMoveCommand.CancelDash)) networkDashLeft = 0;
        bool started = allowed && command.Has(NetworkMoveCommand.Dash) && dash != null &&
            networkDashLeft <= 0 && now >= networkDashReady;
        if (started)
        {
            networkDashDirection = transform.forward;
            networkDashLeft = dash.DashDuration;
            networkDashSpeed = dash.DashDistance / Mathf.Max(.01f, dash.DashDuration);
            networkDashReady = now + dash.DashCooldown;
        }
        if (networkGrounded && verticalVelocity < 0) verticalVelocity = GroundedVelocity;
        float acceleration = Mathf.Min(-.01f, gravity);
        if (allowed && command.Has(NetworkMoveCommand.Jump) && networkGrounded)
            verticalVelocity = Mathf.Sqrt(2 * -acceleration * jumpHeight);
        verticalVelocity += acceleration * seconds;
        Vector3 displacement = allowed ? (transform.right * command.X + transform.forward * command.Z) * movementSpeed * seconds : Vector3.zero;
        if (networkDashLeft > 0)
        {
            Vector3 travel = networkDashDirection * networkDashSpeed * Mathf.Min(seconds, networkDashLeft);
            displacement.x = travel.x; displacement.z = travel.z;
            networkDashLeft = Mathf.Max(0, networkDashLeft - seconds);
        }
        displacement.y = verticalVelocity * seconds;
        CollisionFlags collision = controller.Move(displacement);
        networkGrounded = (collision & CollisionFlags.Below) != 0;
        if ((collision & CollisionFlags.Sides) != 0) networkDashLeft = 0;
        if ((collision & CollisionFlags.Below) != 0 && verticalVelocity < 0) verticalVelocity = GroundedVelocity;
        if ((collision & CollisionFlags.Above) != 0 && verticalVelocity > 0) verticalVelocity = 0;
        return started;
    }
}
