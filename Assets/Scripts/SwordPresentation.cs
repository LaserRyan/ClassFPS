using UnityEngine;

// Only animates a visual pivot; never performs physics queries or applies damage.
[DisallowMultipleComponent]
public sealed class SwordPresentation : MonoBehaviour
{
    [SerializeField] private SwordWeapon sword;
    [SerializeField] private Transform model;
    [SerializeField] private Vector3 restAngles = new Vector3(10f, 0f, -15f);
    [SerializeField] private Vector3 windupAngles = new Vector3(-25f, -35f, -65f);
    [SerializeField] private Vector3 slashAngles = new Vector3(35f, 45f, 65f);

    private void LateUpdate()
    {
        if (model == null)
            return;
        Quaternion rest = Quaternion.Euler(restAngles);
        if (sword == null || !sword.isActiveAndEnabled || !sword.IsSwinging)
        {
            model.localRotation = rest;
            return;
        }
        float t = sword.SwingElapsed;
        Quaternion ready = Quaternion.Euler(windupAngles);
        Quaternion end = Quaternion.Euler(slashAngles);
        if (t < sword.Windup)
            model.localRotation = Quaternion.Slerp(rest, ready, Smooth(t, sword.Windup));
        else if (t < sword.Windup + sword.ActiveDuration)
            model.localRotation = Quaternion.Slerp(ready, end, Smooth(t - sword.Windup, sword.ActiveDuration));
        else
            model.localRotation = Quaternion.Slerp(end, rest,
                Smooth(t - sword.Windup - sword.ActiveDuration, sword.Recovery));
    }

    private static float Smooth(float elapsed, float duration) =>
        Mathf.SmoothStep(0f, 1f, duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration));

    private void OnDisable() => ResetPresentation();

    public void ResetPresentation()
    {
        if (model != null)
            model.localRotation = Quaternion.Euler(restAngles);
    }
}
