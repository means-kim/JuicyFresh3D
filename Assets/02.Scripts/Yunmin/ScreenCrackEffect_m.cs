using UnityEngine;

// 빈사 화면 깨짐 연출입니다. 재생 순간 금이 퍼지고 그 형태를 계속 유지합니다.
// 렌더링은 PC_Renderer의 Full Screen Pass Renderer Feature(ScreenCrack 셰이더)가 하고,
// 이 컴포넌트는 전역 셰이더 프로퍼티만 갱신합니다. 재생/정지는 PostProcessEffectController_m을 통해 호출합니다.
[DisallowMultipleComponent]
public sealed class ScreenCrackEffect_m : MonoBehaviour
{
    private static readonly int AmountId = Shader.PropertyToID("_ScreenCrackAmount");
    private static readonly int CenterId = Shader.PropertyToID("_ScreenCrackCenter");
    private static readonly int ShapeId = Shader.PropertyToID("_ScreenCrackShape");
    private static readonly int DistortionId = Shader.PropertyToID("_ScreenCrackDistortion");
    private static readonly int LineColorId = Shader.PropertyToID("_ScreenCrackLineColor");

    [Header("Impact")]
    [Tooltip("충격 지점이 랜덤으로 정해지는 화면 범위(UV)입니다. 최소/최대가 같으면 고정 위치입니다.")]
    [SerializeField] private Vector2 _impactMin = new Vector2(0.35f, 0.35f);
    [SerializeField] private Vector2 _impactMax = new Vector2(0.65f, 0.65f);
    [Tooltip("금이 퍼지는 최대 반경(화면 높이 기준). 1.2 이상이면 화면 전체가 깨집니다.")]
    [SerializeField, Min(0f)] private float _spreadRadius = 1.4f;
    [Tooltip("금이 퍼지는 데 걸리는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float _crackDuration = 0.15f;
    [SerializeField, Min(0f)] private float _impactRadius = 0.04f;

    [Header("Shards")]
    [Tooltip("충격 지점에서 뻗어나가는 방사 조각 수입니다.")]
    [SerializeField, Range(3, 24)] private int _sectorCount = 9;
    [Tooltip("낮을수록 조각이 방사 방향으로 길쭉해집니다.")]
    [SerializeField, Range(0.2f, 2f)] private float _ringDensity = 0.7f;

    [Header("Crack Line")]
    [SerializeField] private Color _lineColor = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("금 선 두께(화면 높이 기준)입니다.")]
    [SerializeField, Range(0.0005f, 0.01f)] private float _lineWidth = 0.0025f;

    [Header("Distortion")]
    [Tooltip("조각마다 화면이 어긋나는 정도(화면 높이 기준)입니다.")]
    [SerializeField, Range(0f, 0.05f)] private float _shardOffset = 0.008f;
    [Tooltip("금 주변이 굴절되는 폭(화면 높이 기준)입니다.")]
    [SerializeField, Range(0f, 0.1f)] private float _bevelWidth = 0.02f;
    [SerializeField, Range(0f, 0.05f)] private float _bevelStrength = 0.012f;
    [SerializeField, Range(0f, 0.02f)] private float _chromaticAberration = 0.004f;

    private bool _isPlaying;
    private float _amount;
    private Vector2 _impactCenter = new Vector2(0.5f, 0.5f);
    private float _seed;

    public bool IsPlaying => _isPlaying;
    public bool IsAnimating => _isPlaying && _amount < 1f;

    internal void Play()
    {
        // 이미 깨진 화면은 다시 깨지지 않고 그 형태를 유지합니다.
        if (_isPlaying)
        {
            return;
        }

        _isPlaying = true;
        _amount = 0f;
        _impactCenter = new Vector2(
            Random.Range(Mathf.Min(_impactMin.x, _impactMax.x), Mathf.Max(_impactMin.x, _impactMax.x)),
            Random.Range(Mathf.Min(_impactMin.y, _impactMax.y), Mathf.Max(_impactMin.y, _impactMax.y)));
        _seed = Random.Range(0f, 100f);

        if (_crackDuration <= 0f)
        {
            _amount = 1f;
        }

        Apply();
    }

    internal void Stop()
    {
        Clear();
    }

    internal void Tick(float deltaTime)
    {
        if (!_isPlaying)
        {
            return;
        }

        _amount = _crackDuration > 0f ? Mathf.Min(1f, _amount + deltaTime / _crackDuration) : 1f;
        Apply();
    }

    private void Apply()
    {
        // 처음엔 빠르게 퍼지고 끝에서 느려지도록 ease-out을 적용합니다.
        float eased = 1f - Mathf.Pow(1f - _amount, 3f);

        Shader.SetGlobalFloat(AmountId, _isPlaying && isActiveAndEnabled ? Mathf.Max(eased, 0.0001f) : 0f);
        Shader.SetGlobalVector(CenterId, new Vector4(_impactCenter.x, _impactCenter.y, _seed, _spreadRadius));
        Shader.SetGlobalVector(ShapeId, new Vector4(_sectorCount, _ringDensity, _lineWidth, _bevelWidth));
        Shader.SetGlobalVector(DistortionId, new Vector4(_shardOffset, _bevelStrength, _chromaticAberration, _impactRadius));
        Shader.SetGlobalColor(LineColorId, _lineColor);
    }

    internal void Clear()
    {
        _isPlaying = false;
        _amount = 0f;
        Shader.SetGlobalFloat(AmountId, 0f);
    }

    private void OnValidate()
    {
        // 플레이 중 인스펙터 조정이 바로 보이도록 합니다.
        if (Application.isPlaying && _isPlaying)
        {
            Apply();
        }
    }

    private void OnDisable()
    {
        Clear();
    }
}
