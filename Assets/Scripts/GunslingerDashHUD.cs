using UnityEngine;

[RequireComponent(typeof(GunslingerDash))]
[DisallowMultipleComponent]
public sealed class GunslingerDashHUD : MonoBehaviour
{
    private GunslingerDash dash;
    private void Awake() => dash = GetComponent<GunslingerDash>();
    private void OnGUI()
    {
        FPSController movement = dash.Movement;
        if (movement == null || !movement.isActiveAndEnabled ||
            movement.GetComponent<PlayerHealth>().CurrentHealth <= 0f) return;
        float remaining = dash.CooldownRemaining;
        GUI.Label(new Rect(Screen.width - 230f, Screen.height - 125f, 220f, 30f),
            dash.NetworkRequestPending ? "Dash [MB2] Waiting for server" : remaining > 0f ? $"Dash [MB2] {remaining:0.0}s" : "Dash [MB2] Ready");
    }
}
