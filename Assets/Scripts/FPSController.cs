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
    [SerializeField] private bool ownsLocalInput = true;

    private const float GroundedVelocity = -2f;
    private const float PitchLimit = 89f;
    private CharacterController controller;
    private float verticalVelocity;
    private float pitch;
    private bool cursorCaptureRequested;
    private int gameplayInputFrame = -1;
    private bool gameplayBlocked;
    private bool selectingHero;
    private int suppressGameplayThroughFrame = -1;

    public Camera PlayerCamera => playerCamera;
    public GunslingerDash Dash { get; set; }
    // Top-left GUI coordinates. Default empty rect preserves ordinary offline capture.
    public Rect CursorCaptureExclusion { get; set; }
    public bool CanCaptureCursorAt(Vector2 screenPosition) =>
        !CursorCaptureExclusion.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
    // Stable capture/lifecycle gate for replies that arrive before this frame's Update.
    public bool CanMaintainGameplayInput => ownsLocalInput && isActiveAndEnabled && !gameplayBlocked && !selectingHero &&
        cursorCaptureRequested && Application.isFocused && Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0f;
    public bool CanAcceptGameplayInput => CanMaintainGameplayInput && Time.frameCount > suppressGameplayThroughFrame &&
        gameplayInputFrame == Time.frameCount;

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
        Dash?.Cancel();
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
        if (!ownsLocalInput) return;
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool capturedThisFrame = false;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            SetCursorCaptured(false);
        }
        else if (!selectingHero && (!cursorCaptureRequested || Cursor.lockState != CursorLockMode.Locked) &&
                 Application.isFocused && mouse != null &&
                 mouse.leftButton.wasPressedThisFrame && CanCaptureCursorAt(mouse.position.ReadValue()))
        {
            SetCursorCaptured(true);
            // Ignore the capture frame's delta to prevent a sudden camera turn.
            capturedThisFrame = true;
        }

        ApplyCursorState();
        bool acceptInput = cursorCaptureRequested && Application.isFocused &&
            Cursor.lockState == CursorLockMode.Locked && !capturedThisFrame && !gameplayBlocked &&
            Time.frameCount > suppressGameplayThroughFrame && !selectingHero;
        gameplayInputFrame = acceptInput ? Time.frameCount : -1;

        // Keep cursor/Escape processing alive while dead, without movement or gravity.
        if (gameplayBlocked || selectingHero)
        {
            Dash?.Cancel();
            return;
        }

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
        {
            Dash?.Cancel();
            return;
        }

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
        bool dashing = Dash != null && Dash.IsDashing;
        Vector3 dashStep = Dash != null ? Dash.Step(Time.deltaTime) : Vector3.zero;
        Vector3 displacement = velocity * Time.deltaTime;
        if (dashing)
        {
            displacement.x = dashStep.x;
            displacement.z = dashStep.z;
        }
        MoveCharacter(displacement, dashing);
    }

    private void MoveCharacter(Vector3 displacement, bool dashing)
    {
        CollisionFlags collisions = controller.Move(displacement);
        if (dashing && (collisions & CollisionFlags.Sides) != 0)
            Dash.Cancel();
        if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            verticalVelocity = GroundedVelocity;
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    public void SetGameplayBlocked(bool blocked)
    {
        if (gameplayBlocked == blocked) return;
        gameplayBlocked = blocked;
        if (blocked) Dash?.Cancel();
        gameplayInputFrame = -1;
    }

    public void SetHeroSelection(bool selecting)
    {
        selectingHero = selecting;
        SetCursorCaptured(false);
        gameplayInputFrame = -1;
        suppressGameplayThroughFrame = Time.frameCount;
    }

    public void ConfigureMovement(float speed, float jump, float downwardGravity)
    {
        movementSpeed = Mathf.Max(0f, speed);
        jumpHeight = Mathf.Max(0f, jump);
        gravity = Mathf.Min(-0.01f, downwardGravity);
    }

    public void ResetAtSpawn(Vector3 position, Quaternion facing)
    {
        Dash?.ResetForRespawn();
        if (controller == null)
            controller = GetComponent<CharacterController>();
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, facing.eulerAngles.y, 0f));
        verticalVelocity = 0f;
        pitch = 0f;
        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.identity;
        controller.enabled = wasEnabled;
        gameplayInputFrame = -1;
        suppressGameplayThroughFrame = Time.frameCount;
        // Do not alter cursorCaptureRequested: respawning is not a capture action.
    }

    private void SetCursorCaptured(bool captured)
    {
        cursorCaptureRequested = captured;
        if (!captured) Dash?.Cancel();
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
        if (!ownsLocalInput) return;
        bool captured = cursorCaptureRequested && isActiveAndEnabled && Application.isFocused && !selectingHero;
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    public void SetLocalInputOwnership(bool owned) => ownsLocalInput = owned;

    private void OnValidate()
    {
        movementSpeed = Mathf.Max(0f, movementSpeed);
        mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
        jumpHeight = Mathf.Max(0f, jumpHeight);
        gravity = Mathf.Min(gravity, -0.01f);
    }
}
