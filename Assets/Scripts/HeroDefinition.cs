using UnityEngine;

[CreateAssetMenu(menuName = "ClassFPS/Hero Definition")]
public sealed class HeroDefinition : ScriptableObject
{
    [Tooltip("Stable identifier: do not change after using it for saved selections.")]
    [SerializeField] private string heroId;
    [SerializeField] private string displayName;
    [SerializeField] private string heroClass;
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    [SerializeField, Min(0f)] private float movementSpeed = 5f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private HeroKit kitPrefab;
    [SerializeField] private GameObject avatarPrefab;
    [SerializeField] private Sprite icon;
    [Header("World presentation (visuals only)")]
    [SerializeField] private WorldHeroPresentation worldPresentationPrefab;
    [Header("Optional selected-hero behaviors")]
    [SerializeField] private GameObject secondaryActionPrefab;
    [SerializeField] private GameObject passivePrefab;
    public string HeroId => heroId;
    public string DisplayName => displayName;
    public string HeroClass => heroClass;
    public float MaximumHealth => Mathf.Max(1f, maximumHealth);
    public float MovementSpeed => Mathf.Max(0f, movementSpeed);
    public float JumpHeight => Mathf.Max(0f, jumpHeight);
    public float Gravity => Mathf.Min(-0.01f, gravity);
    public HeroKit KitPrefab => kitPrefab;
    public GameObject AvatarPrefab => avatarPrefab;
    public Sprite Icon => icon;
    public WorldHeroPresentation WorldPresentationPrefab => worldPresentationPrefab;
    public GameObject SecondaryActionPrefab => secondaryActionPrefab;
    public GameObject PassivePrefab => passivePrefab;
}
