using UnityEngine;

public class BossChargeArea_k : MonoBehaviour
{
    [Header("돌진 공격 경로")]
    [SerializeField][Min(0.01f)] private float _chargeAttackWidth = 2f;
    [SerializeField][Min(0.01f)] private float _chargeAttackLength = 10f;

    [Header("장판 표시")]
    [SerializeField] private Transform _chargeAreaVisual;
    [SerializeField] private float _chargeAreaHeightOffset = 0.03f;
    [SerializeField] private Transform _groundTransform;

    private Vector3 _chargeStartPosition;
    private Vector3 _chargeDirection;
    private bool _hasChargeArea;
    private float _chargeProgressDistance;
    private BossAttackController _attackController;
    private Renderer _chargeAreaRenderer;
    private MaterialPropertyBlock _areaProperties;
    private Color _defaultAreaColor;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        _attackController = GetComponent<BossAttackController>();
        ClearChargeArea();

        if (_chargeAreaVisual == null)
        {
            return;
        }

        _chargeAreaRenderer = _chargeAreaVisual.GetComponentInChildren<Renderer>(true);

        if (_chargeAreaRenderer == null || _chargeAreaRenderer.sharedMaterial == null
            || !_chargeAreaRenderer.sharedMaterial.HasProperty(BaseColorId))
        {
            return;
        }

        _defaultAreaColor = _chargeAreaRenderer.sharedMaterial.GetColor(BaseColorId);
        _areaProperties = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (!_hasChargeArea || _chargeAreaVisual == null || _groundTransform == null)
        {
            return;
        }

        UpdateChargeProgress();
        UpdateChargeAreaVisual();
        UpdateChargeAreaColor(_attackController != null && _attackController.IsChargeDodgeWindowActive);
    }

    [ContextMenu("돌진 경로 초기화")]
    public void ClearChargeArea()
    {
        _hasChargeArea = false;
        _chargeStartPosition = Vector3.zero;
        _chargeDirection = Vector3.zero;
        _chargeProgressDistance = 0f;
        UpdateChargeAreaColor(false);

        if (_chargeAreaVisual != null)
        {
            _chargeAreaVisual.gameObject.SetActive(false);
        }
    }

    [ContextMenu("돌진 경로 준비 테스트")]
    public void PrepareChargeArea()
    {
        ClearChargeArea();

        if (_chargeAreaVisual == null || _groundTransform == null)
        {
            Debug.LogWarning("돌진 장판과 바닥 Transform을 연결하세요.", this);
            return;
        }

        Vector3 direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        _chargeStartPosition = transform.position;
        _chargeDirection = direction.normalized;
        _hasChargeArea = true;

        UpdateChargeAreaVisual();
        UpdateChargeAreaColor(_attackController != null && _attackController.IsChargeDodgeWindowActive);
        _chargeAreaVisual.gameObject.SetActive(true);
    }
    private void UpdateChargeProgress()
    {
        Vector3 displacement = transform.position - _chargeStartPosition;
        displacement.y = 0f;

        float forwardDistance = Vector3.Dot(displacement, _chargeDirection);

        _chargeProgressDistance = Mathf.Clamp(Mathf.Max(_chargeProgressDistance, forwardDistance), 0f, _chargeAttackLength);
    }

    public bool IsWithinChargeArea(Vector3 worldPosition)
    {
        if (!isActiveAndEnabled || !_hasChargeArea)
        {
            return false;
        }

        // 구르기는 Update에서 시작하므로 LateUpdate 전에도 최신 진행 거리를 사용합니다.
        UpdateChargeProgress();

        if (_chargeAttackLength - _chargeProgressDistance <= 0.0001f)
        {
            return false;
        }

        Vector3 offset = worldPosition - _chargeStartPosition;
        offset.y = 0f;

        float forwardDistance = Vector3.Dot(offset, _chargeDirection);

        if (forwardDistance < _chargeProgressDistance || forwardDistance > _chargeAttackLength)
        {
            return false;
        }

        Vector3 right = Vector3.Cross(Vector3.up, _chargeDirection);
        float sideDistance = Mathf.Abs(Vector3.Dot(offset, right));
        return sideDistance <= _chargeAttackWidth * 0.5f;
    }

    private void UpdateChargeAreaVisual()
    {
        float remainingLength = _chargeAttackLength - _chargeProgressDistance;

        if (remainingLength <= 0.0001f)
        {
            _chargeAreaVisual.gameObject.SetActive(false);
            return;
        }

        Vector3 remainingStart = _chargeStartPosition + _chargeDirection * _chargeProgressDistance;

        Vector3 center = remainingStart + _chargeDirection * (remainingLength * 0.5f);
        center.y = _groundTransform.position.y + _chargeAreaHeightOffset;

        Quaternion rotation = Quaternion.LookRotation(_chargeDirection, Vector3.up);

        _chargeAreaVisual.SetPositionAndRotation(center, rotation);

        _chargeAreaVisual.localScale = new Vector3(_chargeAttackWidth, 1f, remainingLength);
    }

    private void UpdateChargeAreaColor(bool windowActive)
    {
        if (_chargeAreaRenderer == null || _areaProperties == null)
        {
            return;
        }

        Color color = windowActive
            ? new Color(1f, 0f, 0f, _defaultAreaColor.a)
            : _defaultAreaColor;

        _chargeAreaRenderer.GetPropertyBlock(_areaProperties);
        _areaProperties.SetColor(BaseColorId, color);
        _chargeAreaRenderer.SetPropertyBlock(_areaProperties);
    }

    private void OnDisable()
    {
        ClearChargeArea();
    }
}
