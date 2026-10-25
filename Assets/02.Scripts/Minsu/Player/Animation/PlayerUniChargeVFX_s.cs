using UnityEngine;
using UnityEngine.VFX;

[DisallowMultipleComponent]
public class PlayerUniChargeVFX_s : MonoBehaviour
{
    [SerializeField] private VisualEffect _buildupPrefab;
    [SerializeField] private VisualEffect _groundPrefab;
    [SerializeField] private Transform _swordAnchor;
    [SerializeField] private Vector3 _swordOffset = new Vector3(0f, 0f, 0.1f);
    [SerializeField, Min(0.01f)] private float _buildupScale = 0.65f;
    [SerializeField] private bool _useGroundOnslaught = true;
    [SerializeField, Min(0.01f)] private float _groundScale = 0.65f;
    [SerializeField] private Vector3 _groundEulerOffset;
    [SerializeField, Min(0f)] private float _groundLift = 0.03f;
    [SerializeField] private LayerMask _groundLayers = ~0;
    [SerializeField, Min(0f)] private float _fadeOutTime = 0.4f;
    [SerializeField, ColorUsage(false, true)] private Color _coreColor = new Color(1.5f, 3f, 4f);
    [SerializeField, ColorUsage(false, true)] private Color _edgeColor = new Color(0.05f, 0.4f, 2f);
    private VisualEffect _buildup;
    private VisualEffect _ground;
    private bool _charging;
    private bool _groundRunning;
    private float _hideAt = -1f;

    public void Begin()
    {
        if (!isActiveAndEnabled) return;
        Stop(true);
        _charging = true;
        if (_buildup == null && _buildupPrefab != null && _swordAnchor != null)
        {
            _buildup = Instantiate(_buildupPrefab, _swordAnchor, false);
        }
        if (_ground == null && _groundPrefab != null)
        {
            _ground = Instantiate(_groundPrefab, transform, false);
        }
        if (_buildup != null)
        {
            _buildup.transform.localPosition = _swordOffset;
            _buildup.transform.localRotation = Quaternion.identity;
            _buildup.transform.localScale = Vector3.one * _buildupScale;
            StartBuildup(_buildup);
        }
        if (_ground != null) _ground.transform.SetParent(transform, true);
        UpdateGround();
    }

    private void StartBuildup(VisualEffect effect)
    {
        // 전용 프리팹의 초기 이벤트는 Manual로 두어 발사/폭발 자동 재생을 막는다.
        effect.gameObject.SetActive(true);
        effect.Reinit();
        if (effect.HasVector4("CenterColor")) effect.SetVector4("CenterColor", _coreColor);
        if (effect.HasVector4("SecondaryColor")) effect.SetVector4("SecondaryColor", _coreColor);
        if (effect.HasGradient("ColorOverLife"))
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[]
            {
                new GradientColorKey(_coreColor, 0f),
                new GradientColorKey(_edgeColor, 1f)
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            effect.SetGradient("ColorOverLife", gradient);
        }
        effect.SendEvent("buildup");
    }

    private void LateUpdate()
    {
        if (_charging) UpdateGround();
        if (_hideAt >= 0f && Time.unscaledTime >= _hideAt) Stop(true);
    }

    private void UpdateGround()
    {
        if (_ground == null) return;
        bool found = false;
        RaycastHit nearest = default;
        float distance = float.PositiveInfinity;
        if (_useGroundOnslaught)
        {
            foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up, Vector3.down,
                         3f, _groundLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Health_s>() != null
                    || hit.normal.y < 0.3f || hit.distance >= distance)
                {
                    continue;
                }
                found = true;
                nearest = hit;
                distance = hit.distance;
            }
        }
        if (!found)
        {
            if (_groundRunning) StopEffect(_ground, true);
            _groundRunning = false;
            return;
        }
        _ground.transform.SetPositionAndRotation(nearest.point + nearest.normal * _groundLift,
            Quaternion.FromToRotation(Vector3.up, nearest.normal) * Quaternion.Euler(_groundEulerOffset));
        _ground.transform.localScale = Vector3.one * _groundScale;
        if (!_groundRunning) StartBuildup(_ground);
        _groundRunning = true;
    }

    public void Stop(bool immediate)
    {
        _charging = false;
        _groundRunning = false;
        StopEffect(_buildup, immediate);
        StopEffect(_ground, immediate);
        // 해제 후 잔광은 돌진하는 캐릭터를 따라가지 않고 마지막 지면 위치에 남긴다.
        if (_ground != null && !immediate) _ground.transform.SetParent(null, true);
        _hideAt = immediate ? -1f : Time.unscaledTime + _fadeOutTime;
    }

    private static void StopEffect(VisualEffect effect, bool immediate)
    {
        if (effect == null) return;
        if (effect.gameObject.activeSelf)
        {
            effect.SendEvent("stop");
            effect.Stop();
        }
        if (immediate) effect.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        Stop(true);
    }

    private void OnDestroy()
    {
        if (_buildup != null) Destroy(_buildup.gameObject);
        if (_ground != null) Destroy(_ground.gameObject);
    }
}
