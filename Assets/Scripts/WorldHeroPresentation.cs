using System;
using UnityEngine;

// Root stays upright under Player; only the weapon pivot follows aim pitch.
[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
public sealed class WorldHeroPresentation : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Transform attackPivot;
    [SerializeField, Min(0f)] private float attackKick = 8f;
    [SerializeField, Min(0.01f)] private float recoverySpeed = 18f;
    private Camera aimCamera;
    private PlayerHealth health;
    private Weapon primary;
    private float pitch;
    private float kick;
    private Quaternion aimRest;
    private Quaternion attackRest;
    private Material fallbackMaterial;
    private LineRenderer networkTracer;
    private Material networkTracerMaterial;
    private float networkTracerUntil;
    public void PresentNetworkShot(Vector3 endpoint)
    {
        if (networkTracer == null) return;
        networkTracer.SetPosition(0, attackPivot != null ? attackPivot.position : transform.position + Vector3.up * 1.4f);
        networkTracer.SetPosition(1, endpoint);
        networkTracer.enabled = true;
        networkTracerUntil = Time.time + .07f;
    }
    public event Action AcceptedAttack;

    public static bool IsVisualOnly(WorldHeroPresentation presentation, out string error)
    {
        error = null;
        if (presentation == null) return true; // Missing optional art uses the fallback.
        if (!presentation.enabled || !presentation.gameObject.activeSelf)
            error = "World presentation must be enabled and active.";
        if (presentation.visualRoot == presentation.transform)
            error = "Visual Root must be a child so health subscriptions survive death.";
        foreach (Component component in presentation.GetComponentsInChildren<Component>(true))
            if (component is Collider || component is Rigidbody || component is Weapon ||
                component is IDamageable || component is StraightProjectile || component is AudioSource ||
                component is Camera || component is AudioListener)
                error = "World presentation must contain only visuals: remove " + component.GetType().Name;
        return error == null;
    }

    public void ConfigurePivots(Transform visuals, Transform aim, Transform attack)
    {
        visualRoot = visuals;
        aimPivot = aim;
        attackPivot = attack;
    }
    public void Bind(Camera aimSource, PlayerHealth actorHealth, Weapon weapon)
    {
        Unsubscribe();
        // Created before PlayerViewVisibility caches this actor's world renderers.
        if (networkTracer == null && GetComponentInParent<NetworkPrototypePlayer>() != null)
        {
            var line = new GameObject("Network world shot tracer");
            line.transform.SetParent(transform, false);
            networkTracer = line.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                networkTracerMaterial = new Material(shader);
                networkTracerMaterial.SetColor("_BaseColor", new Color(1f, .8f, .2f));
                networkTracer.sharedMaterial = networkTracerMaterial;
            }
            networkTracer.positionCount = 2;
            networkTracer.useWorldSpace = true;
            networkTracer.startWidth = networkTracer.endWidth = .018f;
            networkTracer.enabled = false;
        }
        aimCamera = aimSource;
        health = actorHealth;
        primary = weapon;
        aimRest = aimPivot != null ? aimPivot.localRotation : Quaternion.identity;
        attackRest = attackPivot != null ? attackPivot.localRotation : Quaternion.identity;
        if (isActiveAndEnabled) Subscribe();
        RefreshHealth(health != null ? health.CurrentHealth : 1f, 1f);
    }
    // Later remote presentation can supply replicated pitch without a camera.
    public void SetAimPitch(float degrees) => pitch = Mathf.Clamp(degrees, -89f, 89f);
    private void OnEnable()
    {
        Subscribe();
        if (health != null) RefreshHealth(health.CurrentHealth, health.MaximumHealth);
    }
    private void OnDisable() { Unsubscribe(); ResetPose(); }
    private void Subscribe()
    {
        if (health != null) health.HealthChanged += RefreshHealth;
        if (primary is HitscanGun gun) gun.ShotFired += OnShot;
        if (primary is SwordWeapon sword) sword.SwingStarted += OnAccepted;
        if (primary is FireSpellWeapon fire) fire.CastStarted += OnAccepted;
    }
    private void Unsubscribe()
    {
        if (health != null) health.HealthChanged -= RefreshHealth;
        if (primary is HitscanGun gun) gun.ShotFired -= OnShot;
        if (primary is SwordWeapon sword) sword.SwingStarted -= OnAccepted;
        if (primary is FireSpellWeapon fire) fire.CastStarted -= OnAccepted;
    }
    private void OnShot(Vector3 endpoint) => OnAccepted();
    private void OnAccepted()
    {
        if (health != null && health.CurrentHealth <= 0f) return;
        kick = attackKick;
        AcceptedAttack?.Invoke();
    }
    private void RefreshHealth(float current, float maximum)
    {
        if (visualRoot != null && visualRoot.gameObject.activeSelf != (current > 0f))
        {
            visualRoot.gameObject.SetActive(current > 0f);
            ResetPose();
        }
    }
    private void ResetPose()
    {
        kick = 0f;
        if (networkTracer != null) networkTracer.enabled = false;
        if (aimPivot != null) aimPivot.localRotation = aimRest;
        if (attackPivot != null) attackPivot.localRotation = attackRest;
    }
    private void LateUpdate()
    {
        if (networkTracer != null && Time.time >= networkTracerUntil) networkTracer.enabled = false;
        if (aimCamera != null) SetAimPitch(Mathf.DeltaAngle(0f, aimCamera.transform.localEulerAngles.x));
        if (aimPivot != null) aimPivot.localRotation = aimRest * Quaternion.Euler(pitch, 0f, 0f);
        if (attackPivot != null) attackPivot.localRotation = attackRest * Quaternion.Euler(-kick, 0f, 0f);
        kick *= Mathf.Exp(-recoverySpeed * Time.deltaTime);
    }
    public void OwnFallbackMaterial(Material material) => fallbackMaterial = material;
    private void OnDestroy() { if (fallbackMaterial != null) Destroy(fallbackMaterial); if (networkTracerMaterial != null) Destroy(networkTracerMaterial); }
}
