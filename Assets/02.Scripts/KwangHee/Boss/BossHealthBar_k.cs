using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보스 체력바 UI + 플레이 테스트용 체력 조절 버튼
// 체력 값 자체는 BossStatus_k가 가지고, 이 스크립트는 표시와 버튼만 담당한다.
public class BossHealthBar_k : MonoBehaviour
{
    [Header("보스")]
    [SerializeField] private BossStatus_k _bossStatus;

    [Header("바 이미지 (뒤 → 앞 순서로 겹쳐 놓기)")]
    [SerializeField] private Image _backImage; // 배경 (청회색)
    [SerializeField] private Image _gainImage; // 회복 구간 (초록)
    [SerializeField] private Image _lossImage; // 감소 구간 (주황)
    [SerializeField] private Image _currentImage; // 현재 체력 (흰색)

    [Header("바 색상")]
    [SerializeField] private Color _backColor = new(0.27f, 0.4f, 0.5f);
    [SerializeField] private Color _gainColor = new(0.6f, 0.73f, 0f);
    [SerializeField] private Color _lossColor = new(1f, 0.45f, 0f);
    [SerializeField] private Color _currentColor = Color.white;

    [Header("텍스트 (TMP)")]
    [SerializeField] private TMP_Text _percentText; // 좌측 상단: [044%]
    [SerializeField] private TMP_Text _hpText; // 우측 상단: 현재 / 최대

    [Header("테스트 버튼")]
    [SerializeField] private Button _minusButton;
    [SerializeField] private Button _plusButton;
    [SerializeField] private float _minusAmount = 10f;
    [SerializeField] private float _plusAmount = 10f;

    [Header("따라오는 구간 연출")]
    [SerializeField][Min(0f)] private float _trailDelay = 0.4f; // 값이 바뀐 뒤 따라오기 시작하기까지 대기 시간
    [SerializeField][Min(0.01f)] private float _trailSpeed = 0.6f; // 1초에 줄어드는 비율 (1 = 전체)

    [Header("흔들림 연출 (비워두면 사용 안 함)")]
    [SerializeField] private RectTransform _shakeTarget; // 기울어질 대상 (보통 체력바 전체)
    [SerializeField] private float _shakeAngle = 3f; // 최대 기울기 (도)
    [SerializeField][Min(0.01f)] private float _shakeDuration = 0.3f;
    [SerializeField] private float _shakeFrequency = 40f;

    // 0 ~ 1 비율
    private float _target; // 실제 체력 비율
    private float _current; // 흰색 바
    private float _loss; // 주황 바
    private float _gain; // 초록 바

    private float _delayTimer;
    private float _shakeTimer;

    private static Sprite _whiteSprite;

    private void Awake()
    {
        PrepareImage(_backImage, _backColor);
        PrepareImage(_gainImage, _gainColor);
        PrepareImage(_lossImage, _lossColor);
        PrepareImage(_currentImage, _currentColor);
    }

    private void OnEnable()
    {
        if (_bossStatus != null) _bossStatus.Damaged += OnHpChanged;
        if (_minusButton != null) _minusButton.onClick.AddListener(OnMinusClicked);
        if (_plusButton != null) _plusButton.onClick.AddListener(OnPlusClicked);
    }

    private void OnDisable()
    {
        if (_bossStatus != null) _bossStatus.Damaged -= OnHpChanged;
        if (_minusButton != null) _minusButton.onClick.RemoveListener(OnMinusClicked);
        if (_plusButton != null) _plusButton.onClick.RemoveListener(OnPlusClicked);
    }

    private void Start()
    {
        if (_bossStatus == null) return;

        // 시작할 때는 연출 없이 현재 체력으로 맞춘다
        _target = GetRatio(_bossStatus.CurrentHp, _bossStatus.MaxHp);
        _current = _target;
        _loss = _target;
        _gain = _target;

        UpdateTexts(_bossStatus.CurrentHp, _bossStatus.MaxHp);
        ApplyFill();
    }

    private void Update()
    {
        UpdateTrail();
        UpdateShake();
        ApplyFill();
    }

    private void OnHpChanged(float currentHp, float maxHp)
    {
        _target = GetRatio(currentHp, maxHp);
        _gain = _target; // 초록은 항상 바로 반영 (흰색보다 앞서 있을 때만 보임)

        if (_target < _current)
        {
            // 감소: 흰색은 바로 줄고, 주황이 이전 위치에 남아 천천히 따라온다
            _current = _target;
            _loss = Mathf.Max(_loss, _current);
        }
        else
        {
            // 회복: 초록이 먼저 차고, 흰색이 천천히 따라온다 (주황은 흰색 뒤에 숨긴다)
            _loss = _current;
        }

        _delayTimer = _trailDelay;
        _shakeTimer = _shakeDuration;

        UpdateTexts(currentHp, maxHp);
    }

    private void UpdateTrail()
    {
        if (_delayTimer > 0f)
        {
            _delayTimer -= Time.deltaTime;
            return;
        }

        float step = _trailSpeed * Time.deltaTime;

        if (_current < _target)
        {
            _current = Mathf.MoveTowards(_current, _target, step);
            _loss = _current;
        }
        else if (_loss > _target)
        {
            _loss = Mathf.MoveTowards(_loss, _target, step);
        }
    }

    private void UpdateShake()
    {
        if (_shakeTarget == null) return;

        if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.deltaTime;
            float power = Mathf.Max(0f, _shakeTimer) / _shakeDuration; // 1 → 0 으로 줄어든다
            float angle = Mathf.Sin(Time.time * _shakeFrequency) * _shakeAngle * power;
            _shakeTarget.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        else
        {
            _shakeTarget.localRotation = Quaternion.identity;
        }
    }

    private void ApplyFill()
    {
        if (_backImage != null) _backImage.fillAmount = 1f;
        if (_gainImage != null) _gainImage.fillAmount = _gain;
        if (_lossImage != null) _lossImage.fillAmount = _loss;
        if (_currentImage != null) _currentImage.fillAmount = _current;
    }

    private void UpdateTexts(float currentHp, float maxHp)
    {
        if (_percentText != null)
        {
            int percent = Mathf.RoundToInt(GetRatio(currentHp, maxHp) * 100f);
            _percentText.text = $"[{percent:000}%]";
        }

        if (_hpText != null) _hpText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";
    }

    private void OnMinusClicked()
    {
        if (_bossStatus != null) _bossStatus.TakeDamage(_minusAmount);
    }

    private void OnPlusClicked()
    {
        if (_bossStatus != null) _bossStatus.Heal(_plusAmount);
    }

    private static float GetRatio(float currentHp, float maxHp)
    {
        return maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
    }

    // Filled 타입은 스프라이트가 있어야 동작해서, 비어 있으면 흰색 기본 스프라이트를 넣어준다
    private static void PrepareImage(Image image, Color color)
    {
        if (image == null) return;

        if (image.sprite == null)
        {
            if (_whiteSprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            image.sprite = _whiteSprite;
        }

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.color = color;
    }
}