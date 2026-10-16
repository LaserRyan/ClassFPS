using UnityEngine;

[CreateAssetMenu(menuName = "ClassFPS/Gunslinger Abilities")]
public sealed class GunslingerDefinition : ScriptableObject
{
    [SerializeField, Min(0f)] private float dashDistance = 5f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.2f;
    [SerializeField, Min(0f)] private float dashCooldown = 5f;
    [SerializeField, Range(0f, 100f)] private float lifestealPercent = 15f;
    public float DashDistance => Mathf.Max(0f, dashDistance);
    public float DashDuration => Mathf.Max(0.01f, dashDuration);
    public float DashCooldown => Mathf.Max(0f, dashCooldown);
    public float LifestealPercent => Mathf.Clamp(lifestealPercent, 0f, 100f);
}
