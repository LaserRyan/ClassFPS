using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDeathHUD : MonoBehaviour
{
    [SerializeField] private PlayerLifecycle lifecycle;
    private GUIStyle style;

    private void OnGUI()
    {
        if (lifecycle == null || !lifecycle.IsDead)
            return;
        if (style == null)
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 24 };
        GUI.Box(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 65f, 440f, 130f),
            $"You died\nRespawning in {Mathf.CeilToInt(lifecycle.SecondsUntilRespawn)}…", style);
    }
}
