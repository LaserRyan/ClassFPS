using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public sealed class FPSController : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField, Min(0f)] private float movementSpeed = 5f;
    [Tooltip("Degrees of rotation per pixel of mouse movement.")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [Tooltip("Downward acceleration in metres per second squared; must be negative.")]
    [SerializeField] private float gravity = -20f;

    private const float GroundedVelocity = -2f;
    private const float PitchLimit = 89f;
    private CharacterController controller;
    private float verticalVelocity;
    private float pitch;
    private bool cursorCaptureRequested;
    private int gameplayInputFrame = -1;

    public Camera PlayerCamera => playerCamera;
    public bool CanAcceptGameplayInput => isActiveAndEnabled &&
        gameplayInputFrame == Time.frameCount && cursorCaptureRequested &&
        Application.isFocused && Cursor.lockState == CursorLockMode.Locked &&
        Time.timeScale > 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null || playerCamera.transform.parent != transform)
        {
            Debug.LogError("FPSController needs a Camera that is a direct child of Player.", this);
            enabled = false;
            return;
        }

        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, playerCamera.transform.localEulerAngles.x),
            -PitchLimit, PitchLimit);
    }

    private void OnEnable()
    {
        SetCursorCaptured(true);
    }

    private void OnDisable()
    {
        SetCursorCaptured(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Require a click to resume after switching back to the application.
        if (!hasFocus)
            SetCursorCaptured(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool capturedThisFrame = false;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            SetCursorCaptured(false);
        }
        else if ((!cursorCaptureRequested || Cursor.lockState != CursorLockMode.Locked) &&
                 Application.isFocused && mouse != null &&
                 mouse.leftButton.wasPressedThisFrame)
        {
            SetCursorCaptured(true);
            // Ignore the capture frame's delta to prevent a sudden camera turn.
            capturedThisFrame = true;
        }

        ApplyCursorState();
        bool acceptInput = cursorCaptureRequested && Application.isFocused &&
            Cursor.lockState == CursorLockMode.Locked && !capturedThisFrame;
        gameplayInputFrame = acceptInput ? Time.frameCount : -1;

        if (acceptInput && mouse != null)
        {
            // Input System mouse delta is displacement, not a rate. No deltaTime here.
            Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(Vector3.up, look.x, Space.World);
            pitch = Mathf.Clamp(pitch - look.y, -PitchLimit, PitchLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        Vector2 moveInput = Vector2.zero;
        bool jumpPressed = false;
        if (acceptInput && keyboard != null)
        {
            moveInput.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            moveInput.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            moveInput = Vector2.ClampMagnitude(moveInput, 1f);
            jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
        }

        if (!controller.enabled || Time.deltaTime <= 0f)
            return;

        bool grounded = controller.isGrounded;
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = GroundedVelocity;

        // Clamp locally too, so invalid runtime Inspector values cannot reverse gravity.
        float downwardGravity = Mathf.Min(gravity, -0.01f);
        if (grounded && jumpPressed)
            verticalVelocity = Mathf.Sqrt(2f * -downwardGravity * jumpHeight);

        verticalVelocity += downwardGravity * Time.deltaTime;
        Vector3 velocity = (transform.right * moveInput.x + transform.forward * moveInput.y)
            * movementSpeed;
        velocity.y = verticalVelocity;

        // Continue gravity while the cursor is free; only movement/look input is paused.
        CollisionFlags collisions = controller.Move(velocity * Time.deltaTime);
        if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            verticalVelocity = GroundedVelocity;
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    private void SetCursorCaptured(bool captured)
    {
        cursorCaptureRequested = captured;
        ApplyCursorState();
    }

    private void LateUpdate()
    {
        // Unity or another component can reset cursor visibility after initial capture.
        // Reapply our requested state, including the released state after Escape.
        ApplyCursorState();
    }

    private void ApplyCursorState()
    {
        bool captured = cursorCaptureRequested && isActiveAndEnabled && Application.isFocused;
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    private void OnValidate()
    {
        movementSpeed = Mathf.Max(0f, movementSpeed);
        mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
        jumpHeight = Mathf.Max(0f, jumpHeight);
        gravity = Mathf.Min(gravity, -0.01f);
    }
}
