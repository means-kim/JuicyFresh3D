using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HealthBar_s : MonoBehaviour
{
    [SerializeField] private Health_s _health;
    [SerializeField] private Slider _slider;
    [SerializeField] private Text _valueText;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private bool _faceCamera;
    [SerializeField] private bool _hideWhenDead;
    [Header("체력 감소 연출")]
    [SerializeField, Min(0f)] private float _healthSmoothTime = 0.06f;
    [SerializeField, Min(0f)] private float _damageTrailDelay = 0.15f;
    [SerializeField, Min(0f)] private float _damageTrailSmoothTime = 0.25f;
    [SerializeField] private Color _damageTrailColor = new Color(1f, 0.5f, 0.1f, 1f);
    private Camera _camera;
    private Image _trailImage;
    private float _target;
    private float _displayed;
    private float _trail;
    private float _healthVelocity;
    private float _trailVelocity;
    private float _delayRemaining;
    private bool _initialized;

    private void Awake()
    {
        if (_health == null) _health = GetComponentInParent<Health_s>();
        CreateDamageTrail();
    }

    private void OnEnable()
    {
        if (_health == null) return;
        _initialized = false;
        _health.HealthChanged += Refresh;
        Refresh(_health.CurrentHealth, _health.MaxHealth);
    }

    private void Start()
    {
        // 다른 오브젝트의 Awake에서 체력이 초기화된 뒤 다시 동기화한다.
        _initialized = false;
        if (_health != null) Refresh(_health.CurrentHealth, _health.MaxHealth);
    }

    private void Refresh(int current, int maximum)
    {
        float next = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
        if (!_initialized || next > _target)
        {
            // 초기화/회복은 잔상을 정리하고 실제 체력에 즉시 맞춘다.
            _displayed = next;
            _trail = next;
            _healthVelocity = 0f;
            _trailVelocity = 0f;
            _delayRemaining = 0f;
            _initialized = true;
        }
        else if (next < _target)
        {
            // 연속 피격은 현재 표시 위치에서 이어가며 대기 시간만 갱신한다.
            _delayRemaining = Mathf.Max(0f, _damageTrailDelay);
            _trailVelocity = 0f;
        }
        _target = next;
        ApplyValues();
        if (_valueText != null) _valueText.text = current + " / " + maximum;
        if (_canvasGroup != null) _canvasGroup.alpha = _hideWhenDead && current <= 0 ? 0f : 1f;
    }

    private void LateUpdate()
    {
        if (_initialized)
        {
            float deltaTime = Time.unscaledDeltaTime;
            _displayed = Damp(_displayed, _target, ref _healthVelocity, _healthSmoothTime, deltaTime);
            float trailDelta = Mathf.Max(0f, deltaTime - _delayRemaining);
            _delayRemaining = Mathf.Max(0f, _delayRemaining - deltaTime);
            if (trailDelta > 0f)
            {
                _trail = Damp(_trail, _target, ref _trailVelocity, _damageTrailSmoothTime, trailDelta);
            }
            // 잔상이 앞쪽 체력바보다 먼저 내려가지 않게 한다.
            _trail = Mathf.Max(_trail, _displayed);
            ApplyValues();
        }
        if (!_faceCamera) return;
        if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        if (_camera != null) transform.rotation = _camera.transform.rotation;
    }

    private void OnDisable()
    {
        if (_health != null) _health.HealthChanged -= Refresh;
        if (_trailImage != null) _trailImage.enabled = false;
    }

    private static float Damp(float value, float target, ref float velocity, float smoothTime, float deltaTime)
    {
        if (smoothTime <= 0f || Mathf.Abs(value - target) < 0.0001f)
        {
            velocity = 0f;
            return target;
        }
        return Mathf.SmoothDamp(value, target, ref velocity, smoothTime, Mathf.Infinity, deltaTime);
    }

    private void CreateDamageTrail()
    {
        if (_slider == null || _slider.fillRect == null) return;
        var source = _slider.fillRect.GetComponent<Image>();
        if (source == null) return;
        var child = new GameObject("Damage Trail (Runtime)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.layer = source.gameObject.layer;
        var rect = (RectTransform)child.transform;
        rect.SetParent(source.transform.parent, false);
        rect.SetSiblingIndex(source.transform.GetSiblingIndex());
        rect.localRotation = source.rectTransform.localRotation;
        rect.localScale = source.rectTransform.localScale;
        rect.pivot = source.rectTransform.pivot;
        rect.anchoredPosition = source.rectTransform.anchoredPosition;
        rect.sizeDelta = source.rectTransform.sizeDelta;
        _trailImage = child.GetComponent<Image>();
        _trailImage.sprite = source.sprite;
        _trailImage.type = source.type;
        _trailImage.material = source.material;
        _trailImage.preserveAspect = source.preserveAspect;
        _trailImage.fillCenter = source.fillCenter;
        _trailImage.fillMethod = source.fillMethod;
        _trailImage.fillOrigin = source.fillOrigin;
        _trailImage.fillClockwise = source.fillClockwise;
        _trailImage.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        _trailImage.maskable = source.maskable;
        _trailImage.raycastTarget = false;
    }

    private void ApplyValues()
    {
        if (_slider != null)
        {
            _slider.SetValueWithoutNotify(Mathf.Lerp(_slider.minValue, _slider.maxValue, _displayed));
        }
        if (_trailImage == null || _slider == null) return;
        _trailImage.color = _damageTrailColor;
        _trailImage.enabled = isActiveAndEnabled && _trail > _displayed + 0.0001f && _slider.isActiveAndEnabled;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.one;
        if (_trailImage.type == Image.Type.Filled)
        {
            _trailImage.fillAmount = _trail;
        }
        else
        {
            int axis = _slider.direction == Slider.Direction.LeftToRight || _slider.direction == Slider.Direction.RightToLeft ? 0 : 1;
            bool reverse = _slider.direction == Slider.Direction.RightToLeft || _slider.direction == Slider.Direction.TopToBottom;
            if (reverse) min[axis] = 1f - _trail;
            else max[axis] = _trail;
        }
        _trailImage.rectTransform.anchorMin = min;
        _trailImage.rectTransform.anchorMax = max;
    }

    private void OnDestroy()
    {
        if (_trailImage != null) Destroy(_trailImage.gameObject);
    }
}
