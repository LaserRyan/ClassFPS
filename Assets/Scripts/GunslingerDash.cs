using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GunslingerDash : HeroSecondary
{
    [SerializeField] private GunslingerDefinition definition;
    private FPSController movement;
    private Vector3 direction;
    private readonly DashState state = new DashState();
    public event Action Started;
    public event Action Stopped;
    public bool IsDashing => state.Active;
    public GunslingerDefinition Definition => definition;
    public FPSController Movement => movement;
    public float CooldownRemaining => state.CooldownRemaining(Time.time);
    public override void Bind(FPSController owner) { movement = owner; owner.Dash = this; }
    public override void Activate(WeaponAttackContext context)
    {
        if (definition == null || movement == null || !isActiveAndEnabled ||
            !movement.GetComponent<CharacterController>().enabled) return;
        Vector3 facing = Vector3.ProjectOnPlane(context.AimRay.direction, Vector3.up);
        if (facing.sqrMagnitude < 0.0001f) facing = movement.transform.forward;
        if (!state.TryStart(Time.time, definition.DashDistance, definition.DashDuration,
            definition.DashCooldown, true, movement.CanAcceptGameplayInput)) return;
        direction = facing.normalized;
        Started?.Invoke();
    }
    // Called by FPSController after its cursor/input decision, before its single Move.
    public Vector3 Step(float seconds)
    {
        if (!isActiveAndEnabled || movement == null || !movement.CanAcceptGameplayInput) Cancel();
        bool active = state.Active;
        Vector3 displacement = direction * state.Step(seconds);
        if (active && !state.Active) Stopped?.Invoke();
        return displacement;
    }
    public override void Cancel()
    {
        bool active = state.Active;
        state.Cancel();
        if (active) Stopped?.Invoke();
    }
    public override void ResetForRespawn() { Cancel(); state.Reset(); }
    private void OnDisable() => Cancel();
    private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
}
