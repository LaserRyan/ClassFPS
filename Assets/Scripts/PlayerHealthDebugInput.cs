using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerHealth), typeof(FPSController))]
[DisallowMultipleComponent]
public sealed class PlayerHealthDebugInput : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float testDamage = 25f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private PlayerHealth health;
    private FPSController movement;
    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<FPSController>();
    }

    private void LateUpdate()
    {
        // Evaluate after movement/cursor Update, including recapture suppression.
        Keyboard keyboard = Keyboard.current;
        if (!movement.CanAcceptGameplayInput || keyboard == null)
            return;
        if (keyboard.f8Key.wasPressedThisFrame)
            health.TakeDamage(testDamage);
        if (movement.CanAcceptGameplayInput && keyboard.f9Key.wasPressedThisFrame)
            health.RestoreFullHealth();
    }
#endif
}
