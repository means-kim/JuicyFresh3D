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
    private Camera _camera;

    private void Awake()
    {
        if (_health == null) _health = GetComponentInParent<Health_s>();
    }

    private void OnEnable()
    {
        if (_health == null) return;
        _health.HealthChanged += Refresh;
        Refresh(_health.CurrentHealth, _health.MaxHealth);
    }

    private void Start()
    {
        // 다른 오브젝트의 Awake에서 체력이 초기화된 뒤 다시 동기화한다.
        if (_health != null) Refresh(_health.CurrentHealth, _health.MaxHealth);
    }

    private void Refresh(int current, int maximum)
    {
        if (_slider != null) _slider.SetValueWithoutNotify(maximum > 0 ? (float)current / maximum : 0f);
        if (_valueText != null) _valueText.text = current + " / " + maximum;
        if (_canvasGroup != null) _canvasGroup.alpha = _hideWhenDead && current <= 0 ? 0f : 1f;
    }

    private void LateUpdate()
    {
        if (!_faceCamera) return;
        if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        if (_camera != null) transform.rotation = _camera.transform.rotation;
    }

    private void OnDisable()
    {
        if (_health != null) _health.HealthChanged -= Refresh;
    }
}
