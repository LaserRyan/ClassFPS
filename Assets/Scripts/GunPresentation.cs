using UnityEngine;

// Runs after PlayerCombatInput's LateUpdate so a new kick appears in the same frame.
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class GunPresentation : MonoBehaviour
{
    [SerializeField] private HitscanGun gun;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Renderer muzzleFlash;
    [SerializeField] private LineRenderer tracer;

    [Header("Camera-local resting pose")]
    [SerializeField] private Vector3 weaponPosition = new Vector3(0.23f, -0.18f, 0.5f);
    [SerializeField] private Vector3 weaponRotation = Vector3.zero;

    [Header("Visual recoil only")]
    [SerializeField, Min(0f)] private float recoilDistance = 0.035f;
    [SerializeField, Min(0f)] private float recoilAngle = 3f;
    [SerializeField, Min(0.01f)] private float recoverySpeed = 16f;

    [Header("Effects")]
    [SerializeField, Min(0.01f)] private float muzzleFlashDuration = 0.045f;
    [SerializeField, Min(0.01f)] private float tracerDuration = 0.07f;
    private float kick;
    private float tilt;
    private float flashUntil;
    private float tracerUntil;

    private void OnEnable()
    {
        if (gun != null)
            gun.ShotFired += OnShotFired;
        ResetPresentation();
    }

    private void OnDisable()
    {
        if (gun != null)
            gun.ShotFired -= OnShotFired;
        ResetPresentation();
    }

    private void OnShotFired(Vector3 endpoint)
    {
        // Bounded accumulation prevents rapid shots from moving the model into the camera.
        kick = Mathf.Min(kick + recoilDistance, recoilDistance * 2f);
        tilt = Mathf.Min(tilt + recoilAngle, recoilAngle * 2f);
        ApplyPose();

        if (muzzleFlash != null)
        {
            muzzleFlash.enabled = true;
            flashUntil = Time.time + muzzleFlashDuration;
        }
        if (tracer != null && muzzle != null)
        {
            // Reuse one world-space line and its shared material; never deal damage here.
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.SetPosition(0, muzzle.position);
            tracer.SetPosition(1, endpoint);
            tracer.enabled = true;
            tracerUntil = Time.time + tracerDuration;
        }
    }

    private void LateUpdate()
    {
        if (gun == null || !gun.isActiveAndEnabled)
        {
            ResetPresentation();
            return;
        }

        // Exponential recovery gives consistent damping across frame rates.
        float recovery = Mathf.Exp(-recoverySpeed * Time.deltaTime);
        kick *= recovery;
        tilt *= recovery;
        ApplyPose();
        if (muzzleFlash != null && Time.time >= flashUntil)
            muzzleFlash.enabled = false;
        if (tracer != null && Time.time >= tracerUntil)
            tracer.enabled = false;
    }

    private void ApplyPose()
    {
        Quaternion restRotation = Quaternion.Euler(weaponRotation);
        transform.localPosition = weaponPosition + restRotation * Vector3.back * kick;
        transform.localRotation = restRotation * Quaternion.Euler(-tilt, 0f, 0f);
    }

    public void ResetPresentation()
    {
        kick = tilt = 0f;
        flashUntil = tracerUntil = 0f;
        if (muzzleFlash != null)
            muzzleFlash.enabled = false;
        if (tracer != null)
            tracer.enabled = false;
        ApplyPose();
    }

    private void OnValidate()
    {
        recoilDistance = Mathf.Max(0f, recoilDistance);
        recoilAngle = Mathf.Max(0f, recoilAngle);
        recoverySpeed = Mathf.Max(0.01f, recoverySpeed);
        muzzleFlashDuration = Mathf.Max(0.01f, muzzleFlashDuration);
        tracerDuration = Mathf.Max(0.01f, tracerDuration);
    }
}
