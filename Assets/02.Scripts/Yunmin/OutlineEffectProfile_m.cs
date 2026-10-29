using UnityEngine;

[CreateAssetMenu(fileName = "OutlineEffectProfile", menuName = "Effects/Outline Effect Profile")]
public sealed class OutlineEffectProfile_m : ScriptableObject
{
    [SerializeField, ColorUsage(true, true)]
    private Color _color = Color.white;

    [Tooltip("Thickness outside the silhouette in render-target pixels.")]
    [SerializeField, Range(0f, 10f), InspectorName("Outline Width (Pixels)")]
    private float _width = 2f;

    [SerializeField, Min(0f), InspectorName("Outline Brightness")]
    private float _brightness = 3f;

    [Tooltip("Time to reach full outline brightness. Zero lights it immediately.")]
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.05f;

    [Tooltip("Time to automatically fade the outline to zero after reaching full brightness.")]
    [SerializeField, Min(0f)] private float _fadeOutDuration = 1.5f;

    public Color Color => _color;
    public float Width => Mathf.Clamp(_width, 0f, 10f);
    public float Brightness => Mathf.Max(0f, _brightness);
    public float FadeInDuration => Mathf.Max(0f, _fadeInDuration);
    public float FadeOutDuration => Mathf.Max(0f, _fadeOutDuration);
    public float TotalDuration => FadeInDuration + FadeOutDuration;
}
