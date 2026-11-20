using UnityEngine;

[CreateAssetMenu(fileName = "GlobalOutline", menuName = "Rendering/Global Outline")]
public sealed class GlobalOutlineSettings : ScriptableObject
{
    public const string ResourcePath = "Rendering/GlobalOutline";
    private static GlobalOutlineSettings instance;

    public readonly struct Values
    {
        public Values(float size, float consistency, float saturation, float value, float scale, float sharpness, float blur)
        {
            Size = size; Consistency = consistency; Saturation = saturation; Value = value;
            Scale = scale; Sharpness = sharpness; Blur = blur;
        }

        public float Size { get; }
        public float Consistency { get; }
        public float Saturation { get; }
        public float Value { get; }
        public float Scale { get; }
        public float Sharpness { get; }
        public float Blur { get; }
    }

    [Header("General")]
    public bool enabled = true;
    public OutlineCategory enabledCategories = OutlineCategory.All;
    [Tooltip("Lifts the recommended limits on the values below.")]
    public bool allowUnrecommendedValues;

    [Header("Shell")]
    [Tooltip("Outline thickness in world units.")]
    [Min(0f)] public float size = 0.07f;
    [Tooltip("0 = thickness scales with object scale, 1 = constant world thickness.")]
    [Min(0f)] public float consistency = 1f;
    [Min(0f)] public float saturation = 5f;
    [Min(0f)] public float value = 0.1f;

    [Header("Composite")]
    [Tooltip("Depth distance over which the outline fades in (smaller = sharper).")]
    [Min(0.001f)] public float scale = 0.002f;
    [Min(0f)] public float sharpness = 1f;
    [Tooltip("Blur radius in pixels (0 = off).")]
    [Min(0f)] public float blur;

    public static GlobalOutlineSettings Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<GlobalOutlineSettings>(ResourcePath);
                if (instance == null)
                {
                    instance = CreateInstance<GlobalOutlineSettings>();
                    instance.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return instance;
        }
    }

    public bool IsCategoryEnabled(OutlineCategory category) => enabled && (enabledCategories & category) != 0;

    // Returns a clamped copy so the asset itself is never rewritten at runtime.
    public Values Resolve()
    {
        bool free = allowUnrecommendedValues;
        return new Values(
            Mathf.Max(0f, size),
            free ? Mathf.Max(0f, consistency) : Mathf.Clamp01(consistency),
            free ? Mathf.Max(0f, saturation) : Mathf.Clamp(saturation, 0f, 5f),
            free ? Mathf.Max(0f, value) : Mathf.Clamp(value, 0f, 2f),
            free ? Mathf.Max(0.001f, scale) : Mathf.Clamp(scale, 0.001f, 0.01f),
            free ? Mathf.Max(1f, sharpness) : Mathf.Clamp(sharpness, 1f, 4f),
            Mathf.Clamp(blur, 0f, 256f));
    }
}
