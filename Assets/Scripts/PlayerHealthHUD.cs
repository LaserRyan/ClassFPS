using UnityEngine;

// Uses the same immediate-mode UI as CombatCrosshair; state updates are event-driven.
[DisallowMultipleComponent]
public sealed class PlayerHealthHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private Vector2 screenMargin = new Vector2(24f, 24f);
    [SerializeField] private Vector2 barSize = new Vector2(240f, 24f);
    [SerializeField] private Color fillColor = new Color(0.2f, 0.85f, 0.3f);
    private PlayerHealth observedHealth;
    private float fraction;
    private string healthText = "";
    private GUIStyle labelStyle;

    private void OnEnable() => Bind(health);
    private void OnDisable() => Unsubscribe();

    public void Bind(PlayerHealth source)
    {
        Unsubscribe();
        health = source;
        if (!isActiveAndEnabled || health == null)
            return;
        observedHealth = health;
        observedHealth.HealthChanged += Refresh;
        Refresh(health.CurrentHealth, health.MaximumHealth);
    }

    private void Unsubscribe()
    {
        if (observedHealth != null)
            observedHealth.HealthChanged -= Refresh;
        observedHealth = null;
    }

    private void Refresh(float current, float maximum)
    {
        fraction = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        healthText = $"{current:0.##} / {maximum:0.##}";
    }

    private void OnGUI()
    {
        if (observedHealth == null)
            return;
        if (labelStyle == null)
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
        Color previous = GUI.color;
        float width = Mathf.Max(32f, barSize.x);
        float height = Mathf.Max(16f, barSize.y);
        var rect = new Rect(screenMargin.x, Screen.height - screenMargin.y - height, width, height);
        GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (width - 4f) * fraction, height - 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(rect, healthText, labelStyle);
        GUI.color = previous;
    }
}
