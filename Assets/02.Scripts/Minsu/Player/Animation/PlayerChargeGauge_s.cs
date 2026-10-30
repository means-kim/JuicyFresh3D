using UnityEngine;
using UnityEngine.UI;

// 머리 위 차징 단계 게이지. 화면 고정 크기로 그리고 위치만 월드 좌표에서 투영한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(KatanaChargeSkill_s))]
public class PlayerChargeGauge_s : MonoBehaviour
{
    private const int StageCount = 3;
    private const float MaxFlashTime = 0.3f;
    [Header("위치")]
    [SerializeField, Min(0f)] private float _height = 2.1f;
    [SerializeField] private Vector2 _screenOffset = new Vector2(0f, 12f);
    [Header("칸")]
    [SerializeField] private Vector2 _cellSize = new Vector2(36f, 8f);
    [SerializeField, Min(0f)] private float _cellSpacing = 4f;
    [SerializeField] private Color _emptyColor = new Color(0.05f, 0.08f, 0.12f, 0.6f);
    [SerializeField] private Color _chargeColor = new Color(0.25f, 0.6f, 1f, 1f);
    [SerializeField] private Color _maxColor = new Color(0.85f, 0.95f, 1f, 1f);
    [Header("연출")]
    [SerializeField, Min(0.01f)] private float _fadeInTime = 0.08f;
    [SerializeField, Min(0f)] private float _holdTime = 0.25f;
    [SerializeField, Min(0.01f)] private float _fadeOutTime = 0.12f;
    [SerializeField, Min(0.01f)] private float _pulseTime = 0.12f;
    [SerializeField, Min(1f)] private float _pulseScale = 1.25f;
    private readonly RectTransform[] _cells = new RectTransform[StageCount];
    private readonly RectTransform[] _fillRects = new RectTransform[StageCount];
    private readonly Image[] _fills = new Image[StageCount];
    private readonly float[] _pulses = new float[StageCount];
    private KatanaChargeSkill_s _charge;
    private PlayerMovement_s _movement;
    private GameObject _canvasObject;
    private RectTransform _canvasRect;
    private RectTransform _root;
    private CanvasGroup _group;
    private Camera _camera;
    private float _alpha;
    private float _holdRemaining;
    private float _maxFlash;
    private bool _visible;
    private bool _maxed;

    private void Awake()
    {
        _charge = GetComponent<KatanaChargeSkill_s>();
        _movement = GetComponent<PlayerMovement_s>();
        CreateGauge();
    }

    private void OnEnable()
    {
        _charge.ChargeStarted += OnChargeStarted;
        _charge.ChargeProgressChanged += OnChargeProgressChanged;
        _charge.ChargeStageChanged += OnChargeStageChanged;
        _charge.ChargeStopped += OnChargeStopped;
    }

    private void CreateGauge()
    {
        // 씬 수정 없이 동작하도록 런타임 전용 Canvas를 만든다.
        _canvasObject = new GameObject("Charge Gauge Canvas (Runtime)", typeof(RectTransform), typeof(Canvas));
        var canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = _canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _canvasRect = (RectTransform)_canvasObject.transform;

        var root = new GameObject("Charge Gauge", typeof(RectTransform), typeof(CanvasGroup));
        _root = (RectTransform)root.transform;
        _root.SetParent(_canvasRect, false);
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.sizeDelta = Vector2.zero;
        _group = root.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        for (int i = 0; i < StageCount; i++)
        {
            var cell = CreateImage("Stage " + (i + 1), _root, _emptyColor);
            cell.sizeDelta = _cellSize;
            cell.anchoredPosition = new Vector2((i - 1) * (_cellSize.x + _cellSpacing), 0f);
            _cells[i] = cell;
            // 스프라이트 없는 Image는 Filled가 적용되지 않으므로 앵커 폭으로 채움을 표현한다.
            var fill = CreateImage("Fill", cell, _chargeColor);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            _fillRects[i] = fill;
            _fills[i] = fill.GetComponent<Image>();
        }
        root.SetActive(false);
    }

    private static RectTransform CreateImage(string objectName, Transform parent, Color color)
    {
        var child = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        var image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private void OnChargeStarted()
    {
        _visible = true;
        _maxed = false;
        _holdRemaining = 0f;
        _maxFlash = 0f;
        for (int i = 0; i < StageCount; i++) _pulses[i] = 0f;
        // 1단계는 누르는 순간 확정된다.
        _pulses[0] = _pulseTime;
        SetProgress(0f);
        _root.gameObject.SetActive(true);
    }

    private void OnChargeProgressChanged(float progress)
    {
        SetProgress(progress);
    }

    private void OnChargeStageChanged(int stage)
    {
        if (stage < 1 || stage > StageCount) return;
        _pulses[stage - 1] = _pulseTime;
        if (stage < StageCount) return;
        _maxed = true;
        _maxFlash = MaxFlashTime;
    }

    private void OnChargeStopped()
    {
        _visible = false;
        // 피격·사망으로 취소되면 결과를 보여 주지 않고 바로 숨긴다.
        if (_movement != null && _movement.IsActionBlocked)
        {
            Hide();
            return;
        }
        _holdRemaining = _holdTime;
    }

    private void SetProgress(float progress)
    {
        float ratio = Mathf.Clamp(_charge.PartialChargeRatio, 0.01f, 0.99f);
        SetFill(0, 1f);
        SetFill(1, Mathf.Clamp01(progress / ratio));
        SetFill(2, Mathf.Clamp01((progress - ratio) / (1f - ratio)));
    }

    private void SetFill(int index, float amount)
    {
        _fillRects[index].anchorMax = new Vector2(amount, 1f);
    }

    private void LateUpdate()
    {
        if (!_root.gameObject.activeSelf) return;
        float deltaTime = Time.unscaledDeltaTime;
        UpdateAlpha(deltaTime);
        if (_alpha <= 0f && !_visible && _holdRemaining <= 0f)
        {
            Hide();
            return;
        }
        UpdateCells(deltaTime);
        UpdatePosition();
    }

    private void UpdateAlpha(float deltaTime)
    {
        if (!_visible && _holdRemaining > 0f) _holdRemaining = Mathf.Max(0f, _holdRemaining - deltaTime);
        bool shown = _visible || _holdRemaining > 0f;
        float speed = 1f / (shown ? _fadeInTime : _fadeOutTime);
        _alpha = Mathf.MoveTowards(_alpha, shown ? 1f : 0f, speed * deltaTime);
    }

    private void UpdateCells(float deltaTime)
    {
        _maxFlash = Mathf.Max(0f, _maxFlash - deltaTime);
        Color color = _maxed ? _maxColor : _chargeColor;
        // 최대 단계 도달 순간 칸 전체를 흰색 쪽으로 밝혔다가 되돌린다.
        color = Color.Lerp(color, Color.white, _maxFlash / MaxFlashTime);
        for (int i = 0; i < StageCount; i++)
        {
            _fills[i].color = color;
            _pulses[i] = Mathf.Max(0f, _pulses[i] - deltaTime);
            float pulse = _pulses[i] > 0f ? Mathf.Sin(Mathf.PI * (1f - _pulses[i] / _pulseTime)) : 0f;
            _cells[i].localScale = Vector3.one * (1f + (_pulseScale - 1f) * pulse);
        }
    }

    private void UpdatePosition()
    {
        if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        if (_camera == null)
        {
            _group.alpha = 0f;
            return;
        }
        Vector3 screen = _camera.WorldToScreenPoint(transform.position + Vector3.up * _height);
        // 카메라 뒤나 화면 밖이면 숨긴다.
        if (screen.z <= 0f || !_camera.pixelRect.Contains(new Vector2(screen.x, screen.y)))
        {
            _group.alpha = 0f;
            return;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local);
        _root.anchoredPosition = local + _screenOffset;
        _group.alpha = _alpha;
    }

    private void Hide()
    {
        _visible = false;
        _holdRemaining = 0f;
        _alpha = 0f;
        _group.alpha = 0f;
        _root.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        _charge.ChargeStarted -= OnChargeStarted;
        _charge.ChargeProgressChanged -= OnChargeProgressChanged;
        _charge.ChargeStageChanged -= OnChargeStageChanged;
        _charge.ChargeStopped -= OnChargeStopped;
        if (_root != null) Hide();
    }

    private void OnDestroy()
    {
        if (_canvasObject != null) Destroy(_canvasObject);
    }
}
