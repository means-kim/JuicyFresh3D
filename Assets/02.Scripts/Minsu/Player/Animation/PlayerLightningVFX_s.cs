using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class PlayerLightningVFX_s : MonoBehaviour, ISerializationCallbackReceiver
{
    private const int ArcCount = 14;
    private const int ArcPoints = 9;
    private const int TrailPoints = 20;
    private const int TrailSubdivisions = 20;
    private const int ImpactSlots = 4;
    private const int ImpactRays = 16;
    private static readonly HumanBodyBones[] Bones =
    {
        HumanBodyBones.Hips, HumanBodyBones.Chest,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
    };
    private static readonly int[] Starts = { 5, 6, 2, 3, 1, 1, 0, 8, 9, 0, 11, 12, 4, 7 };
    private static readonly int[] Ends = { 6, 7, 3, 4, 5, 2, 8, 9, 10, 11, 12, 13, 1, 1 };

    [SerializeField] private Material _material;
    [SerializeField] private LineRenderer _bodyPrefab;
    [SerializeField] private LineRenderer _bladePrefab;
    [SerializeField] private LineRenderer _trailPrefab;
    [SerializeField] private LineRenderer _impactPrefab;
    [SerializeField] private Transform _sword;
    [SerializeField] private Vector3 _bladeBase = new Vector3(0f, 0.16f, 0f);
    [SerializeField] private Vector3 _bladeTip = new Vector3(0f, 1.04f, 0f);
    [Header("휘두르기 번개 궤적")]
    [Tooltip("검 밑부분 기준 궤적 크기 배율입니다. 1은 기존 크기이며 공격 판정에는 영향을 주지 않습니다.")]
    [SerializeField, Min(0.01f)] private float _trailSize = 1f;
    [Tooltip("중심 번개 궤적의 굵기입니다.")]
    [SerializeField, Min(0.001f)] private float _trailWidth = 0.04f;
    [Tooltip("두 보조 번개 궤적의 굵기입니다.")]
    [SerializeField, Min(0.001f)] private float _trailSecondaryWidth = 0.016f;
    [SerializeField, Range(0.05f, 0.4f)] private float _trailLifetime = 0.14f;
    [Tooltip("기록된 궤적의 꺾임을 완화합니다. 0은 원래 경로, 1은 가장 강한 평활화입니다. 양 끝 위치는 유지합니다.")]
    [SerializeField, Range(0f, 1f)] private float _trailPathSmoothing = 0.75f;
    [Tooltip("곡선 보간 전 위치를 평활화하는 횟수입니다. 높을수록 부드러워지지만 궤적이 안쪽으로 좁아질 수 있습니다.")]
    [SerializeField, Range(1, 6)] private int _trailSmoothingPasses = 3;
    [Header("납도 궤적")]
    [SerializeField, Range(0f, 2f)] private float _sheatheTrailWidthMultiplier = 0.65f;
    [SerializeField, Range(0.05f, 0.4f)] private float _sheatheTrailLifetime = 0.18f;
    [Header("차징 몸 전기")]
    [Tooltip("골반과 가슴 사이를 중심으로 몸 전기 배치를 확대합니다. 1은 기존 크기이며 굵기는 바꾸지 않습니다.")]
    [SerializeField, Min(0.01f)] private float _bodySize = 1f;
    [Tooltip("몸 전기의 지그재그 퍼짐 배율입니다. 1은 기존 퍼짐이며 굵기는 바꾸지 않습니다.")]
    [SerializeField, Min(0f)] private float _bodySpread = 1f;
    [SerializeField, Range(0.01f, 0.1f)] private float _bodyWidth = 0.018f;
    [Header("일반 명중 · 차징 1/2타")]
    [InspectorName("Normal Impact Radius")]
    [SerializeField, Min(0.01f)] private float _impactRadius = 0.55f;
    [SerializeField, Min(0.001f)] private float _normalImpactWidth = 0.035f;
    [Header("차징 마지막 타")]
    [Tooltip("일반 명중 반경과 독립적인 마지막 타의 반경입니다.")]
    [SerializeField, Min(0.01f)] private float _finisherImpactRadius = -1f;
    [SerializeField, Min(0.001f)] private float _finisherImpactWidth = 0.075f;
    // 기존 씬/프리팹의 반경 × 배율을 최초 로드 시 보존한다.
    [SerializeField, HideInInspector] private float _finisherMultiplier = 2.2f;

    private PlayerChargeSkill_s _charge;
    private KatanaChargeSkill_s _katanaCharge;
    private PlayerComboAnimation_s _combo;
    private PlayerMovement_s _movement;
    private Animator _animator;
    private GameObject _root;
    private readonly Dictionary<LineRenderer, LineRenderer> _branches = new Dictionary<LineRenderer, LineRenderer>();
    private readonly Transform[] _bones = new Transform[14];
    private readonly LineRenderer[] _arcs = new LineRenderer[ArcCount];
    private readonly LineRenderer[] _bladeArcs = new LineRenderer[2];
    private readonly LineRenderer[] _trails = new LineRenderer[3];
    private readonly Vector3[] _tips = new Vector3[TrailPoints];
    private readonly Vector3[] _bases = new Vector3[TrailPoints];
    private readonly float[] _ages = new float[TrailPoints];
    private readonly Vector3[] _trailControlPoints = new Vector3[TrailPoints];
    private readonly Vector3[] _trailSmoothingBuffer = new Vector3[TrailPoints];
    private int _count;
    private int _lastSwing = -1;
    private bool _lastSkill;
    private bool _sheatheTrailActive;
    private bool _sheatheTrailStyle;
    private int _sheathePhase;
    private int _sheatheSwing;
    private float _clock;
    private readonly LineRenderer[,] _impacts = new LineRenderer[ImpactSlots, ImpactRays];
    private readonly Vector3[] _impactPositions = new Vector3[ImpactSlots];
    private readonly float[] _impactAges = new float[ImpactSlots];
    private readonly bool[] _impactActive = new bool[ImpactSlots];
    private readonly bool[] _impactFinishers = new bool[ImpactSlots];
    private int _nextImpact;

    public int ActiveBodyArcs { get; private set; }
    public int TrailSampleCount => _count;
    public int ImpactCount { get; private set; }
    public bool LastImpactWasFinisher { get; private set; }

    public void OnBeforeSerialize()
    {
        InitializeImpactSettings();
    }

    public void OnAfterDeserialize()
    {
        InitializeImpactSettings();
    }

    private void OnValidate()
    {
        InitializeImpactSettings();
        _trailSize = Mathf.Max(0.01f, _trailSize);
        _bodySize = Mathf.Max(0.01f, _bodySize);
        _bodySpread = Mathf.Max(0f, _bodySpread);
        _trailWidth = Mathf.Max(0.001f, _trailWidth);
        _trailSecondaryWidth = Mathf.Max(0.001f, _trailSecondaryWidth);
    }

    private void InitializeImpactSettings()
    {
        if (_finisherImpactRadius < 0f)
        {
            _finisherImpactRadius = _impactRadius * _finisherMultiplier;
        }
        _impactRadius = Mathf.Max(0.01f, _impactRadius);
        _finisherImpactRadius = Mathf.Max(0.01f, _finisherImpactRadius);
        _normalImpactWidth = Mathf.Max(0.001f, _normalImpactWidth);
        _finisherImpactWidth = Mathf.Max(0.001f, _finisherImpactWidth);
    }

    private void Awake()
    {
        _charge = GetComponent<PlayerChargeSkill_s>();
        _katanaCharge = GetComponent<KatanaChargeSkill_s>();
        _combo = GetComponent<PlayerComboAnimation_s>();
        _movement = GetComponent<PlayerMovement_s>();
        _animator = GetComponentInChildren<Animator>();
        if (_material == null || _sword == null || _animator == null || !_animator.isHuman) return;
        for (int i = 0; i < Bones.Length; i++)
        {
            _bones[i] = _animator.GetBoneTransform(Bones[i]);
        }
        _root = new GameObject("Lightning VFX (Runtime)");
        _root.transform.SetParent(transform, false);
        for (int i = 0; i < ArcCount; i++)
        {
            _arcs[i] = CreateLine("Body Arc " + i, i % 2 == 0, _bodyPrefab);
        }
        for (int i = 0; i < _bladeArcs.Length; i++)
        {
            _bladeArcs[i] = CreateLine("Blade Arc " + i, true, _bladePrefab);
        }
        for (int i = 0; i < _trails.Length; i++)
        {
            _trails[i] = CreateLine("Lightning Wake " + i, false, _trailPrefab);
            var gradient = new Gradient();
            gradient.SetKeys(new[]
            {
                new GradientColorKey(new Color(0.55f, 0.9f, 1f), 0f),
                new GradientColorKey(new Color(0.1f, 0.35f, 1f), 1f)
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            if (_trailPrefab == null) _trails[i].colorGradient = gradient;
        }
        for (int slot = 0; slot < ImpactSlots; slot++)
        {
            for (int ray = 0; ray < ImpactRays; ray++)
            {
                _impacts[slot, ray] = CreateLine("Impact " + slot + " Spark " + ray, ray % 2 == 0, _impactPrefab);
            }
        }
    }

    private LineRenderer CreateLine(string label, bool withBranch = false, LineRenderer template = null)
    {
        LineRenderer line;
        if (template != null)
        {
            line = Instantiate(template, _root.transform, false);
            line.gameObject.name = label;
        }
        else
        {
            // 기존 에이리언은 프리팹 참조 없이도 이전 생성 방식을 유지한다.
            var lineObject = new GameObject(label);
            lineObject.transform.SetParent(_root.transform, false);
            line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = _material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCornerVertices = 1;
            line.numCapVertices = 0;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.12f, 1f),
                new Keyframe(0.55f, 0.55f), new Keyframe(1f, 0.015f));
        }
        line.gameObject.layer = gameObject.layer;
        line.useWorldSpace = true;
        line.enabled = false;
        if (withBranch)
        {
            _branches.Add(line, CreateLine(label + " Branch", false, template));
        }
        return line;
    }

    private void LateUpdate()
    {
        TickVisuals(Time.deltaTime);
    }

    // 같은 경로를 프레임별 플레이 검증에서도 사용한다.
    public void TickVisuals(float deltaTime)
    {
        if (!isActiveAndEnabled || _root == null || deltaTime <= 0f) return;
        if (_movement != null && _movement.IsActionBlocked)
        {
            Clear();
            return;
        }
        _clock += deltaTime;
        foreach (var branch in _branches.Values)
        {
            branch.enabled = false;
        }
        UpdateImpacts(deltaTime);
        bool charging = _katanaCharge != null ? _katanaCharge.IsCharging : _charge != null && _charge.IsCharging;
        int stage = _katanaCharge != null
            ? (charging ? _katanaCharge.ChargeStage : Mathf.Max(1, _katanaCharge.ReleasedStage))
            : charging ? _charge.ChargeStage : Mathf.Max(1, _charge != null ? _charge.ReleasedStage : 1);
        ActiveBodyArcs = charging ? (stage == 1 ? 4 : stage == 2 ? 8 : ArcCount) : 0;
        Color color = stage == 3 ? new Color(0.4f, 0.7f, 1f) : new Color(0.15f, 0.65f, 1f);
        Vector3 bodyCenter = _bones[0] != null ? _bones[0].position : transform.position;
        if (_bones[0] != null && _bones[1] != null)
        {
            bodyCenter = (_bones[0].position + _bones[1].position) * 0.5f;
        }
        for (int i = 0; i < ArcCount; i++)
        {
            var start = _bones[Starts[i]];
            var end = _bones[Ends[i]];
            bool visible = i < ActiveBodyArcs && start != null && end != null;
            _arcs[i].enabled = visible;
            if (!visible) continue;
            Vector3 offset = transform.forward * (i % 2 == 0 ? 0.12f : -0.12f);
            Vector3 arcStart = bodyCenter + (start.position + offset - bodyCenter) * _bodySize;
            Vector3 arcEnd = bodyCenter + (end.position + offset - bodyCenter) * _bodySize;
            DrawArc(_arcs[i], arcStart, arcEnd,
                i, _bodyWidth * (0.6f + stage * 0.3f), (0.025f + stage * 0.012f) * _bodySpread, color);
        }

        int swing = 0;
        int damage;
        int step;
        float time = 0f;
        Vector2 window = Vector2.zero;
        bool skill = _katanaCharge != null
            ? _katanaCharge.TryGetDamagePhase(out swing, out damage, out time, out window)
            : _charge != null && _charge.TryGetDamagePhase(out swing, out damage, out time, out window);
        bool valid = skill || (_combo != null && _combo.TryGetDamagePhase(out swing, out step, out time, out window));
        bool swinging = valid && time >= Mathf.Max(0f, window.x - 0.06f) && time <= window.y + 0.1f;
        if (_sheatheTrailActive && (!TryGetSheatheSwing(_sheathePhase, out int currentSwing)
            || currentSwing != _sheatheSwing))
        {
            _sheatheTrailActive = false;
        }
        bool emittingTrail = swinging || _sheatheTrailActive;
        if (emittingTrail && (_sheatheTrailStyle != _sheatheTrailActive
            || _lastSwing != swing || _lastSkill != skill))
        {
            _count = 0;
            _sheatheTrailStyle = _sheatheTrailActive;
        }
        float trailLifetime = Mathf.Max(0.01f, _sheatheTrailStyle ? _sheatheTrailLifetime : _trailLifetime);
        Vector3 bladeBase = _sword.TransformPoint(_bladeBase);
        Vector3 bladeTip = _sword.TransformPoint(_bladeTip);
        for (int i = 0; i < _bladeArcs.Length; i++)
        {
            _bladeArcs[i].enabled = charging || swinging;
            if (_bladeArcs[i].enabled)
            {
                DrawArc(_bladeArcs[i], bladeBase, bladeTip, i + 31, 0.012f + stage * 0.005f,
                    0.02f + stage * 0.01f, color);
            }
        }
        for (int i = 0; i < _count; i++)
        {
            _ages[i] += deltaTime;
        }
        while (_count > 0 && _ages[_count - 1] > trailLifetime)
        {
            _count--;
        }
        if (emittingTrail)
        {
            if (_lastSwing != swing || _lastSkill != skill)
            {
                _count = 0;
            }
            _lastSwing = swing;
            _lastSkill = skill;
            _count = Mathf.Min(_count + 1, TrailPoints);
            for (int i = _count - 1; i > 0; i--)
            {
                _tips[i] = _tips[i - 1];
                _bases[i] = _bases[i - 1];
                _ages[i] = _ages[i - 1];
            }
            _tips[0] = bladeTip;
            _bases[0] = bladeBase;
            _ages[0] = 0f;
        }
        for (int rail = 0; rail < _trails.Length; rail++)
        {
            var line = _trails[rail];
            line.enabled = _count > 1;
            line.positionCount = _count > 1 ? (_count - 1) * TrailSubdivisions + 1 : 0;
            float fade = _count == 0 ? 0f : 1f - Mathf.Clamp01(_ages[0] / trailLifetime);
            float widthScale = _sheatheTrailStyle ? _sheatheTrailWidthMultiplier : 0.8f + stage * 0.2f;
            line.widthMultiplier = (rail == 0 ? _trailWidth : _trailSecondaryWidth) * fade * widthScale;
            for (int i = 0; i < _count; i++)
            {
                Vector3 position = Vector3.LerpUnclamped(_bases[i], _tips[i], (1f - rail * 0.24f) * _trailSize);
                if (rail > 0 && i > 0 && i < _count - 1)
                {
                    position += Jitter(i + rail * 17) * (0.006f * _trailSize);
                }
                _trailControlPoints[i] = position;
            }
            SmoothTrailPath();
            for (int i = 0; i < _count - 1; i++)
            {
                Vector3 start = _trailControlPoints[i];
                Vector3 end = _trailControlPoints[i + 1];
                Vector3 startTangent = GetTrailTangent(i);
                Vector3 endTangent = GetTrailTangent(i + 1);
                for (int sample = 0; sample < TrailSubdivisions; sample++)
                {
                    float t = (float)sample / TrailSubdivisions;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    Vector3 position = (2f * t3 - 3f * t2 + 1f) * start
                        + (t3 - 2f * t2 + t) * startTangent
                        + (-2f * t3 + 3f * t2) * end
                        + (t3 - t2) * endTangent;
                    line.SetPosition(i * TrailSubdivisions + sample, position);
                }
            }
            if (_count > 1) line.SetPosition(line.positionCount - 1, _trailControlPoints[_count - 1]);
        }
    }

    // 납도 전용 애니메이션 이벤트. 공격 판정은 열지 않는다.
    public void BeginKatanaSheatheTrail(int phase)
    {
        if (!isActiveAndEnabled || !TryGetSheatheSwing(phase, out int swing)) return;
        _sheathePhase = phase;
        _sheatheSwing = swing;
        _sheatheTrailActive = true;
        _sheatheTrailStyle = true;
        _count = 0;
    }

    public void EndKatanaSheatheTrail(int phase)
    {
        if (_sheathePhase != phase || !TryGetSheatheSwing(phase, out int swing) || swing != _sheatheSwing) return;
        _sheatheTrailActive = false;
    }

    private bool TryGetSheatheSwing(int phase, out int swing)
    {
        swing = 0;
        if (phase == 5)
        {
            return _katanaCharge != null
                && _katanaCharge.TryGetDamagePhase(out swing, out _, out _, out _);
        }
        return _combo != null && _combo.TryGetDamagePhase(out swing, out int step, out _, out _)
            && step == phase;
    }

    private void SmoothTrailPath()
    {
        float strength = Mathf.Clamp01(_trailPathSmoothing);
        if (_count < 3 || strength <= 0f) return;

        int passes = Mathf.Clamp(_trailSmoothingPasses, 1, 6);
        // 원본 위치 기록은 보존한다. 매 프레임 원본에서 다시 계산해 잔상이 계속 수축하지 않게 한다.
        // 양 끝을 고정하고 이전 패스의 이웃만 사용해 순회 방향에 따른 편향을 막는다.
        for (int pass = 0; pass < passes; pass++)
        {
            for (int i = 1; i < _count - 1; i++)
            {
                Vector3 average = (_trailControlPoints[i - 1] + 2f * _trailControlPoints[i]
                    + _trailControlPoints[i + 1]) * 0.25f;
                _trailSmoothingBuffer[i] = Vector3.Lerp(_trailControlPoints[i], average, strength);
            }
            for (int i = 1; i < _count - 1; i++)
            {
                _trailControlPoints[i] = _trailSmoothingBuffer[i];
            }
        }
    }

    private Vector3 GetTrailTangent(int index)
    {
        if (index == 0) return _trailControlPoints[1] - _trailControlPoints[0];
        if (index == _count - 1) return _trailControlPoints[index] - _trailControlPoints[index - 1];

        Vector3 incoming = _trailControlPoints[index] - _trailControlPoints[index - 1];
        Vector3 outgoing = _trailControlPoints[index + 1] - _trailControlPoints[index];
        // 짧거나 겹친 구간에서 접선이 길어져 궤적이 튀는 것을 제한한다.
        float length = Mathf.Min(incoming.magnitude, outgoing.magnitude);
        return (incoming.normalized + outgoing.normalized).normalized * length;
    }

    private Vector3 Jitter(int seed)
    {
        float tick = Mathf.Floor(_clock * 24f);
        return new Vector3(Mathf.Sin(seed * 12.7f + tick * 7.1f),
            Mathf.Sin(seed * 7.3f + tick * 11.9f), Mathf.Cos(seed * 19.1f + tick * 5.3f));
    }

    public void PlayImpact(Vector3 position, bool finisher)
    {
        if (!isActiveAndEnabled || _root == null) return;
        int slot = _nextImpact;
        _nextImpact = (_nextImpact + 1) % ImpactSlots;
        _impactPositions[slot] = position;
        _impactAges[slot] = 0f;
        _impactFinishers[slot] = finisher;
        _impactActive[slot] = true;
        ImpactCount++;
        LastImpactWasFinisher = finisher;
        UpdateImpacts(0f);
    }

    private void UpdateImpacts(float deltaTime)
    {
        for (int slot = 0; slot < ImpactSlots; slot++)
        {
            if (!_impactActive[slot]) continue;
            _impactAges[slot] += deltaTime;
            bool finisher = _impactFinishers[slot];
            float lifetime = finisher ? 0.28f : 0.18f;
            float progress = Mathf.Clamp01(_impactAges[slot] / lifetime);
            int count = finisher ? ImpactRays : 7;
            float impactRadius = finisher ? _finisherImpactRadius : _impactRadius;
            float impactWidth = finisher ? _finisherImpactWidth : _normalImpactWidth;
            for (int ray = 0; ray < ImpactRays; ray++)
            {
                var line = _impacts[slot, ray];
                line.enabled = progress < 1f && ray < count;
                if (!line.enabled) continue;
                // 황금각으로 분산하여 전역 Random 상태를 바꾸지 않는다.
                float y = 1f - 2f * (ray + 0.5f) / count;
                float radius = Mathf.Sqrt(1f - y * y);
                float angle = ray * 2.399963f + slot * 0.73f;
                Vector3 direction = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                float length = impactRadius * (0.65f + 0.35f * Mathf.Abs(Mathf.Sin(ray * 13f)))
                               * Mathf.Lerp(0.45f, 1f, Mathf.Sqrt(progress));
                Vector3 start = _impactPositions[slot] + direction * length * progress * 0.35f;
                Vector3 end = _impactPositions[slot] + direction * length;
                Color color = finisher ? new Color(0.5f, 0.8f, 1f) : new Color(0.2f, 0.7f, 1f);
                color.a = (1f - progress) * (1f - progress);
                DrawArc(line, start, end, slot * 97 + ray, impactWidth,
                    0.04f * impactRadius / 0.55f * (1f - progress), color);
            }
            if (progress >= 1f) _impactActive[slot] = false;
        }
    }

    private void DrawArc(LineRenderer line, Vector3 start, Vector3 end, int seed,
        float width, float noise, Color color)
    {
        line.positionCount = ArcPoints;
        line.widthMultiplier = width * (0.8f + 0.2f * Mathf.Sin(_clock * 45f + seed));
        line.startColor = color;
        line.endColor = color;
        for (int i = 0; i < ArcPoints; i++)
        {
            float t = i / (float)(ArcPoints - 1);
            Vector3 jitter = i == 0 || i == ArcPoints - 1 ? Vector3.zero : Jitter(seed * 13 + i) * noise;
            line.SetPosition(i, Vector3.Lerp(start, end, t) + jitter);
        }
        if (_branches.TryGetValue(line, out var branch))
        {
            branch.enabled = line.enabled;
            int fork = 3 + (seed & 1);
            Vector3 branchStart = line.GetPosition(fork);
            Vector3 direction = end - start;
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude < 0.0001f)
            {
                side = Vector3.Cross(direction, Vector3.right);
            }
            float sign = (seed & 1) == 0 ? 1f : -1f;
            Vector3 branchEnd = branchStart + direction * 0.2f
                                + side.normalized * (direction.magnitude * 0.22f * sign);
            color.a *= 0.65f;
            DrawArc(branch, branchStart, branchEnd, seed + 71, width * 0.4f, noise * 0.45f, color);
        }
    }

    private void Clear()
    {
        _sheatheTrailActive = false;
        _sheatheTrailStyle = false;
        ActiveBodyArcs = 0;
        _count = 0;
        _lastSwing = -1;
        if (_root == null) return;
        foreach (var branch in _branches.Values)
        {
            branch.enabled = false;
        }
        foreach (var line in _arcs)
        {
            line.enabled = false;
        }
        foreach (var line in _bladeArcs)
        {
            line.enabled = false;
        }
        foreach (var line in _trails)
        {
            line.enabled = false;
        }
        for (int slot = 0; slot < ImpactSlots; slot++)
        {
            _impactActive[slot] = false;
            for (int ray = 0; ray < ImpactRays; ray++)
            {
                _impacts[slot, ray].enabled = false;
            }
        }
    }

    private void OnDisable()
    {
        Clear();
    }

    private void OnDestroy()
    {
        if (_root != null)
        {
            Destroy(_root);
        }
    }
}
