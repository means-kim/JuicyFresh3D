#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class ActualBossRuntimeProbe : MonoBehaviour
{
    private BossCombatController _combat;
    private BossAttackController _attack;
    private Animator _animator;
    private Transform _hips;
    private Transform[] _feet;
    private Transform _target;
    private Rigidbody[] _bodies;
    private StringBuilder _trace = new StringBuilder("frame,time,state,animator,rootY,hipsY,minFootY,rootX,rootZ,physicsBodyMinY,physicsDrift\n");
    private float _minFootY = float.PositiveInfinity;
    private float _maxRootY;
    private int _frames;
    private int _completedAttacks;
    private BossState _previous;
    private bool _throwTimingProbe;
    private float _restoreChaseDistance;
    private int _restoreFrames;
    private Vector3 _lastPosition;
    private string _lastAnimationName;
    private float _throwSequenceMovement;
    private float _walkDistance;
    private int _completionEventCount;
    private string _probeMode;
    private int _meleeCompletions;
    private int _chargeCompletions;
    private int _throwCompletions;
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Begin()
    {
        if (SessionState.GetBool("ActualBossProbeRunning", false))
        {
            if (File.ReadAllText(Path.Combine(Application.dataPath, "../actual-boss-probe-mode.txt")).Trim() == "boss-dodge")
            {
                new GameObject("BossDodgeRuntimeProbe").AddComponent<BossDodgeRuntimeProbe>();
                return;
            }
            if (File.ReadAllText(Path.Combine(Application.dataPath, "../actual-boss-probe-mode.txt")).Trim() == "player-combo45-analysis")
            {
                new GameObject("PlayerCombo45PoseProbe").AddComponent<PlayerCombo45PoseProbe>();
                return;
            }
            if (File.ReadAllText(Path.Combine(Application.dataPath, "../actual-boss-probe-mode.txt")).Trim() == "player-attack")
            {
                new GameObject("PlayerAttackRuntimeProbe").AddComponent<PlayerAttackRuntimeProbe>();
                return;
            }
            if (File.ReadAllText(Path.Combine(Application.dataPath, "../actual-boss-probe-mode.txt")).Trim() == "player-eight-way")
            {
                new GameObject("PlayerEightWayRuntimeProbe").AddComponent<PlayerEightWayRuntimeProbe>();
                return;
            }
            if (File.ReadAllText(Path.Combine(Application.dataPath, "../actual-boss-probe-mode.txt")).Trim() == "player-grounding")
            {
                new GameObject("PlayerGroundingRuntimeProbe").AddComponent<PlayerGroundingRuntimeProbe>();
                return;
            }
            if (File.ReadAllText(Path.Combine(Application.dataPath, "../actual-boss-probe-mode.txt")).Trim() == "player-move")
            {
                new GameObject("PlayerMoveRuntimeProbe").AddComponent<PlayerMoveRuntimeProbe>();
                return;
            }
            new GameObject("ActualBossPlayProbe").AddComponent<ActualBossRuntimeProbe>();
        }
    }

    private void Start()
    {
        _combat = FindFirstObjectByType<BossCombatController>();
        _attack = _combat.GetComponent<BossAttackController>();
        _animator = _combat.GetComponent<Animator>();
        _hips = _combat.GetComponentsInChildren<Transform>(true).First(t => t.name == "DeformationSystem");
        _feet = _combat.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Ankle_") || t.name.StartsWith("Toes_")).ToArray();
        _bodies = _combat.GetComponentsInChildren<Rigidbody>(true);
        _target = (Transform)typeof(BossCombatController).GetField("_target", Flags).GetValue(_combat);
        Time.captureDeltaTime = 1f / 60f;
        _previous = _combat.CurrentState;
        _probeMode = File.ReadAllText(Path.Combine(Application.dataPath, "../actual-boss-probe-mode.txt")).Trim();
        _throwTimingProbe = _probeMode.StartsWith("throw-timing");
        if (_probeMode.EndsWith("30fps"))
        {
            Time.captureDeltaTime = 1f / 30f;
        }
        _lastPosition = _combat.transform.position;
        if (_throwTimingProbe)
        {
            _target.position = _combat.transform.position + _combat.transform.forward * 8f;
            _target.position = new Vector3(_target.position.x, 0f, _target.position.z);
            typeof(BossAttackController).GetField("chargeWeight", Flags).SetValue(_attack, 0);
            typeof(BossAttackController).GetField("throwWeight", Flags).SetValue(_attack, 100);
            _attack.AttackFinished += () =>
            {
                _completionEventCount++;
                Debug.Log("THROW END EVENT time=" + Time.time.ToString("F4")
                + " animator=" + GetAnimationName() + " isAttacking=" + _attack.IsAttacking
                + " root=" + _combat.transform.position.ToString("F4"));
            };
        }
        Debug.Log("Actual scene probe: rigidbodies=" + _bodies.Length + ", dynamic=" + _bodies.Count(b => !b.isKinematic));
        Debug.Log("Physics bones: " + string.Join(", ", _bodies.Select(b => b.name)));
    }

    private void LateUpdate()
    {
        if (_combat == null)
        {
            return;
        }
        _frames++;
        string animationName = GetAnimationName();
        if (_throwTimingProbe)
        {
            Vector3 frameDisplacement = _combat.transform.position - _lastPosition;
            frameDisplacement.y = 0f;
            AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo nextState = _animator.GetNextAnimatorStateInfo(0);
            bool inThrowMotion = currentState.IsName("Boss_ThrowStart") || currentState.IsName("Boss_Throw")
                                 || currentState.IsName("Boss_WeaponRestore")
                                 || (_animator.IsInTransition(0)
                                     && (nextState.IsName("Boss_ThrowStart") || nextState.IsName("Boss_Throw")
                                         || nextState.IsName("Boss_WeaponRestore")));
            if (inThrowMotion)
            {
                _throwSequenceMovement += frameDisplacement.magnitude;
            }
            else
            {
                _walkDistance += frameDisplacement.magnitude;
            }
            if (animationName != _lastAnimationName)
            {
                Debug.Log("THROW STATE time=" + Time.time.ToString("F4") + " animator=" + animationName
                    + " isAttacking=" + _attack.IsAttacking + " fsm=" + _combat.CurrentState
                    + " root=" + _combat.transform.position.ToString("F4"));
            }
            if (_animator.GetCurrentAnimatorStateInfo(0).IsName("Boss_WeaponRestore"))
            {
                _restoreFrames++;
                Vector3 displacement = _combat.transform.position - _lastPosition;
                displacement.y = 0f;
                _restoreChaseDistance += displacement.magnitude;
            }
            _lastPosition = _combat.transform.position;
            _lastAnimationName = animationName;
        }
        if (_frames == 120 || _frames == 240 || _frames == 360 || _frames == 480 || _frames == 720)
        {
            Debug.Log("BONE SAMPLE frame=" + _frames + " state=" + _animator.GetCurrentAnimatorStateInfo(0).shortNameHash + " bossRotation=" + _combat.transform.eulerAngles + "\n" + string.Join("\n", _bodies.Select(b => b.name + " transform=" + b.transform.position.ToString("F3") + " rigidbody=" + b.position.ToString("F3") + " localRotation=" + b.transform.localEulerAngles.ToString("F2"))));
        }
        float feetY = _feet.Min(f => f.position.y);
        _minFootY = Mathf.Min(_minFootY, feetY);
        _maxRootY = Mathf.Max(_maxRootY, _combat.transform.position.y);
        _trace.AppendLine($"{_frames},{Time.time:F4},{_combat.CurrentState},{_animator.GetCurrentAnimatorStateInfo(0).shortNameHash},{_combat.transform.position.y:F4},{_hips.position.y:F4},{feetY:F4},{_combat.transform.position.x:F4},{_combat.transform.position.z:F4},{_bodies.Min(b => b.position.y):F4},{_bodies.Max(b => Vector3.Distance(b.position, b.transform.position)):F4}");

        if (_combat.CurrentState == BossState.Recovery && _previous != BossState.Recovery)
        {
            _completedAttacks++;
            if (_previous == BossState.MeleeAttack)
            {
                _meleeCompletions++;
            }
            else if (_previous == BossState.ChargeAttack)
            {
                _chargeCompletions++;
            }
            else if (_previous == BossState.ThrowAttack)
            {
                _throwCompletions++;
            }
            if (!_throwTimingProbe)
            {
            // Keep later cycles in range; the first attack uses the unchanged scene setup.
            Vector3 forward = (new Vector3(0f, 0f, -7f) - _combat.transform.position);
            forward.y = 0f;
            forward = forward.normalized;
            _target.position = _combat.transform.position + forward * 7f;
            _target.position = new Vector3(_target.position.x, 0f, _target.position.z);
            typeof(BossAttackController).GetField("chargeWeight", Flags).SetValue(_attack, _completedAttacks % 3 == 1 ? 0 : 100);
            typeof(BossAttackController).GetField("throwWeight", Flags).SetValue(_attack, _completedAttacks % 3 == 1 ? 100 : 0);
            if (_completedAttacks % 3 == 2)
            {
                _target.position = _combat.transform.position + forward * 1.5f;
            }
            }
        }
        _previous = _combat.CurrentState;
        if (_frames < (_throwTimingProbe ? Mathf.RoundToInt(10f / Time.captureDeltaTime) : 2100))
        {
            return;
        }
        string mode = File.ReadAllText(Path.Combine(Application.dataPath, "../actual-boss-probe-mode.txt")).Trim();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        File.WriteAllText(Path.Combine(output, "actual-boss-" + mode + ".csv"), _trace.ToString());
        string report = $"mode={mode}; frames={_frames}; fixedSteps={Time.fixedTime:F2}; rigidbodies={_bodies.Length}; dynamic={_bodies.Count(b => !b.isKinematic)}; minFeetY={_minFootY:F4}; maxRootY={_maxRootY:F4}; completedAttacks={_completedAttacks}; finalRoot={_combat.transform.position:F4}; finalSkeleton={_hips.position:F4}";
        if (_probeMode == "attack-finish-regression")
        {
            report += $"; melee={_meleeCompletions}; charge={_chargeCompletions}; throw={_throwCompletions}";
            if (_meleeCompletions == 0 || _chargeCompletions == 0 || _throwCompletions == 0 || _maxRootY < 2f || _minFootY < 0f)
            {
                File.WriteAllText(Path.Combine(output, "actual-boss-" + mode + ".txt"), "FAIL: " + report);
                Debug.LogError(report);
                EditorApplication.Exit(2);
                return;
            }
        }
        if (_throwTimingProbe)
        {
            report += $"; restoreFrames={_restoreFrames}; movementDuringRestore={_restoreChaseDistance:F4}; throwSequenceMovement={_throwSequenceMovement:F4}; walkDistance={_walkDistance:F4}; completionEvents={_completionEventCount}";
            if (_probeMode.Contains("fixed") && (_throwSequenceMovement > 0.001f || _restoreChaseDistance > 0.001f || _walkDistance < 0.5f || _completedAttacks < 2 || _completionEventCount != _completedAttacks))
            {
                File.WriteAllText(Path.Combine(output, "actual-boss-" + mode + ".txt"), "FAIL: " + report);
                Debug.LogError("Throw movement regression: " + report);
                EditorApplication.Exit(2);
                return;
            }
        }
        File.WriteAllText(Path.Combine(output, "actual-boss-" + mode + ".txt"), report);
        Debug.Log(report);
        EditorApplication.ExitPlaymode();
        enabled = false;
    }

    private string GetAnimationName()
    {
        string[] names = { "Idle", "Boss_Walk", "Boss_ThrowStart", "Boss_Throw", "Boss_WeaponRestore", "Boss_Attack01" };
        AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
        string result = names.FirstOrDefault(n => current.IsName(n)) ?? current.shortNameHash.ToString();
        if (_animator.IsInTransition(0))
        {
            AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
            result += " -> " + (names.FirstOrDefault(n => next.IsName(n)) ?? next.shortNameHash.ToString());
        }
        return result;
    }
}
#endif
