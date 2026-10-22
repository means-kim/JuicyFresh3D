using UnityEngine;

[DisallowMultipleComponent]
public class LockOnHighlight_s : MonoBehaviour
{
    [SerializeField] private Renderer[] _renderers;
    [SerializeField] private RenderingLayerMask _outlineLayer = 1u << 8;

    private void Awake()
    {
        if (_renderers == null || _renderers.Length == 0)
        {
            _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        SetHighlighted(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (_renderers == null) return;

        uint outlineMask = _outlineLayer;
        bool show = highlighted && isActiveAndEnabled;

        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;

            if (show)
            {
                renderer.renderingLayerMask |= outlineMask;
            }
            else
            {
                renderer.renderingLayerMask &= ~outlineMask;
            }
        }
    }

    private void OnDisable()
    {
        SetHighlighted(false);
    }
}
