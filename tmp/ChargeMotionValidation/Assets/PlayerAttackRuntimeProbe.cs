#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public class PlayerAttackRuntimeProbe : MonoBehaviour
{
    public static float Horizontal;
    public static float Vertical;
    public static bool AttackPressed;
    public static bool RollPressed;
    public static bool DashPressed;
    public static bool WHeld;
    private PlayerMove _move;
    private PlayerAttack _attack;
    private Animator _animator;
    private AnimationClip[] _clips;
    private int _fps;
    private int _errors;
    private bool _rollAtAttackEnd;
    private bool _boundaryTriggered;
    private readonly StringBuilder _report = new StringBuilder();
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    private void Update()
    {
        if (_rollAtAttackEnd && _attack.IsAttacking && !_animator.IsInTransition(0)
            && _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
        {
            RollPressed = true;
            _rollAtAttackEnd = false;
            _boundaryTriggered = true;
        }
    }

    private IEnumerator Start()
    {
        _move = FindFirstObjectByType<PlayerMove>();
        _attack = _move.GetComponent<PlayerAttack>();
        _animator = _move.GetComponent<Animator>();
        _clips = (AnimationClip[])typeof(PlayerAttack).GetField("_comboClips", Flags).GetValue(_attack);
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (trace.Contains("UnityEditor.Search.SearchDatabase"))
            {
                _report.AppendLine("EDITOR NOTE: pre-existing isolated-project SearchDatabase startup error (outside gameplay)");
                return;
            }
            if (type == LogType.Error || type == LogType.Exception
                || (message.Contains("Parameter") && message.Contains("does not exist")))
            {
                _errors++;
            }
        };
        foreach (int fps in new[] { 60, 30 })
        {
            _fps = fps;
            Time.captureDeltaTime = 1f / fps;
            ClearInput();
            yield return Frames(12);
            ResetAttack();

            // One accepted click per stage, exact animation duration, stationary body and direction.
            for (int combo = 1; combo <= 5; combo++)
            {
                Vector3 start = _move.transform.position;
                Quaternion rotation = _move.transform.rotation;
                float startTime = Time.time;
                Horizontal = 1;
                Vertical = 1;
                AttackPressed = true;
                DashPressed = true;
                WHeld = true;
                yield return null;
                Check(_attack.IsAttacking && _attack.CurrentCombo == combo && !_move.IsDashing,
                    "Click did not start expected combo " + combo);
                Check(_attack.ComboRemainingTime > 0, "Timer not started on click");
                float remaining = _attack.ComboRemainingTime;
                for (int spam = 0; spam < 3; spam++)
                {
                    yield return null;
                    Check(_attack.CurrentCombo == combo, "Spam changed active combo");
                    Check(_attack.ComboRemainingTime < remaining, "Rejected click refreshed timer");
                    remaining = _attack.ComboRemainingTime;
                }
                AttackPressed = false;
                DashPressed = false;
                while (_attack.IsAttacking && Time.time - startTime < 3)
                {
                    Check(Vector3.Distance(Planar(start), Planar(_move.transform.position)) < 0.002f,
                        "Player moved horizontally during attack");
                    Check(Quaternion.Angle(rotation, _move.transform.rotation) < 0.01f, "Attack rotated with WASD");
                    Check(!_move.IsDashing && !_animator.GetBool("IsMoving"), "Dash/run accepted during attack");
                    yield return null;
                    if (!_attack.IsAttacking)
                    {
                        // The finishing frame may accept movement again, as intended.
                        break;
                    }
                }
                ClearInput();
                float elapsed = Time.time - startTime;
                Check(!_attack.IsAttacking, "Attack did not end");
                Check(Mathf.Abs(elapsed - _clips[combo - 1].length) <= 3.1f / fps,
                    "Animation duration mismatch: " + elapsed + " vs " + _clips[combo - 1].length);
                Check(_attack.NextCombo == combo % 5 + 1, "Incorrect next combo");
                _report.AppendLine($"PASS {fps}fps combo{combo}: duration={elapsed:F4}; clip={_clips[combo - 1].length:F4}; next={_attack.NextCombo}; stop/rotation/dash/spam locks=True");
                yield return null;
            }
            yield return Click(1);
            yield return WaitAttack();

            // Immediate cancel and same-frame Space priority, then preserve combo through roll.
            ResetAttack();
            yield return Click(1);
            yield return Frames(3);
            float beforeRoll = _attack.ComboRemainingTime;
            RollPressed = true;
            AttackPressed = true;
            yield return null;
            Check(_move.IsRolling && !_attack.IsAttacking && _attack.NextCombo == 2, "Roll did not cancel/preserve combo");
            yield return null;
            Check(_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("RollForward"),
                "Roll waited for attack instead of switching immediately");
            ClearInput();
            yield return WaitRoll();
            yield return Frames(Mathf.CeilToInt(0.2f * fps));
            Check(_attack.ComboRemainingTime < beforeRoll && _attack.NextCombo == 2, "Timer did not run during roll");
            yield return Click(2);
            yield return WaitAttack();
            _report.AppendLine($"PASS {fps}fps cancel: Space priority; immediate RollForward; countdown continues; next=2 after roll/grace");

            // Complete animation, complete roll, grace, then next combo (requested typical flow).
            ResetAttack();
            yield return Click(1);
            yield return WaitAttack();
            RollPressed = true;
            yield return null;
            ClearInput();
            yield return WaitRoll();
            yield return Frames(Mathf.CeilToInt(0.2f * fps));
            yield return Click(2);
            yield return WaitAttack();
            _report.AppendLine($"PASS {fps}fps full attack -> roll -> 0.2s grace -> combo2");

            // Deadline during an attack resets next combo but must not cut current animation short.
            ResetAttack();
            SetDelay(1, 0.1f);
            yield return Click(1);
            yield return Frames(Mathf.CeilToInt(0.2f * fps));
            Check(_attack.IsAttacking && _attack.NextCombo == 1 && _attack.ComboRemainingTime == 0,
                "Expiry interrupted attack or failed to reset combo");
            yield return WaitAttack();
            yield return Click(1);
            yield return WaitAttack();
            SetDelay(1, 2.2f);

            // Each Inspector delay is independent and changes an already running deadline immediately.
            ResetAttack();
            for (int combo = 1; combo <= 5; combo++)
            {
                SetDelay(combo, 4f + combo);
                yield return Click(combo);
                Check(Mathf.Abs(_attack.ComboRemainingTime - (4f + combo)) < 0.001f, "Wrong per-stage delay");
                yield return Frames(3);
                float previous = _attack.ComboRemainingTime;
                SetDelay(combo, 6f + combo);
                Check(Mathf.Abs(_attack.ComboRemainingTime - previous - 2) < 0.001f, "Live delay edit did not extend active timer");
                yield return WaitAttack();
            }
            SetDelay(5, 0);
            yield return null;
            Check(_attack.ComboRemainingTime == 0 && _attack.NextCombo == 1, "Zero delay did not expire");
            SetDelay(1, 2.2f);
            SetDelay(2, 2.3f);
            SetDelay(3, 2.2f);
            SetDelay(4, 2.5f);
            SetDelay(5, 2.6f);
            ResetAttack();
            _report.AppendLine($"PASS {fps}fps expiry: reset to 1 without aborting attack; all 5 delays independent; live edits and zero supported");
            yield return VerifyBufferedCombos();
        }
        Check(_errors == 0, "Runtime errors or missing Animator parameters");
        _report.AppendLine("PASS no runtime errors/parameter warnings");
        File.WriteAllText(Path.Combine(Application.dataPath, "../../player-combo-blend-result.txt"), _report.ToString());
        Debug.Log(_report.ToString());
        EditorApplication.ExitPlaymode();
    }

    private IEnumerator VerifyBufferedCombos()
    {
        ResetAttack();
        SetSetting("_inputBufferTime", 0.2f);
        SetSetting("_comboBlendDuration", 0.08f);
        Vector3 position = _move.transform.position;
        Quaternion rotation = _move.transform.rotation;
        yield return Click(1);
        for (int source = 1; source <= 5; source++)
        {
            yield return WaitProgress(0.86f);
            Horizontal = 1;
            Vertical = 1;
            DashPressed = true;
            WHeld = true;
            AttackPressed = true;
            yield return null;
            AttackPressed = false;
            DashPressed = false;
            Check(_attack.HasBufferedAttack && _attack.CurrentCombo == source, "Late click was not buffered");
            float inputTime = (float)typeof(PlayerAttack).GetField("_bufferedInputTime", Flags).GetValue(_attack);
            int target = source % 5 + 1;
            int frames = 0;
            float finalNormalized = 0;
            while (_attack.CurrentCombo == source && frames++ < _fps * 2)
            {
                Check(_attack.IsAttacking && !_move.IsDashing && !_animator.GetBool("IsMoving"), "Lock opened between attacks");
                Check(Vector3.Distance(Planar(position), Planar(_move.transform.position)) < 0.002f, "Combo chain moved body");
                Check(Quaternion.Angle(rotation, _move.transform.rotation) < 0.01f, "Combo chain rotated body");
                AnimatorStateInfo before = _animator.GetCurrentAnimatorStateInfo(0);
                Check(before.shortNameHash != Animator.StringToHash("Idle_Battle"), "Idle inserted before next attack");
                finalNormalized = before.normalizedTime;
                yield return null;
            }
            Check(_attack.IsAttacking && _attack.CurrentCombo == target, "Buffered attack not consumed once");
            AnimatorStateInfo oldState = _animator.GetCurrentAnimatorStateInfo(0);
            // Coroutine resumes after Update but before Animator evaluation; observe the blend next frame.
            yield return null;
            Check(_animator.IsInTransition(0)
                && _animator.GetNextAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("AttackCombo" + target.ToString("00")),
                "Missing direct combo crossfade: source=" + source + " target=" + target
                    + " transition=" + _animator.IsInTransition(0) + " old=" + oldState.shortNameHash
                    + " current=" + _animator.GetCurrentAnimatorStateInfo(0).shortNameHash
                    + " next=" + _animator.GetNextAnimatorStateInfo(0).shortNameHash);
            Check(oldState.normalizedTime >= 1f, "Source attack cut before its end");
            float nextTime = _animator.GetNextAnimatorStateInfo(0).normalizedTime;
            float actualBlend = _animator.GetAnimatorTransitionInfo(0).duration;
            if (source == 4 && target == 5)
            {
                Check(nextTime >= 0.1f && nextTime <= 0.1f + 2.1f / _fps,
                    "04->05 did not start at the configured 0.1 second offset");
                Check(Mathf.Abs(actualBlend - 0.04f) < 0.001f, "04->05 special blend duration missing");
            }
            else
            {
                Check(nextTime <= 2.1f / _fps, "Other combo acquired a start offset");
                Check(Mathf.Abs(actualBlend - 0.08f) < 0.001f, "Other combo blend changed");
            }
            Check(!_attack.HasBufferedAttack, "Consumed input remained queued");
            float delay = (float)typeof(PlayerAttack).GetField("_comboDelay" + target.ToString("00"), Flags).GetValue(_attack);
            Check(Mathf.Abs(_attack.ComboRemainingTime - (delay - (Time.time - inputTime))) < 1.1f / _fps,
                "ComboDelay started at execution instead of buffered click");
            _report.AppendLine($"PASS {_fps}fps buffer {source}->{target}: sourceNormalized={oldState.normalizedTime:F4}; nextNormalized={nextTime:F4}; blend={actualBlend:F4}; directCrossfade=True; noIdle/lockGap=True; timerFromClick=True");
            ClearInput();
        }
        yield return WaitAttack();
        yield return Frames(8);
        Check(!_attack.IsAttacking, "One buffered click queued multiple later attacks");

        ResetAttack();
        yield return Click(1);
        yield return WaitProgress(0.1f);
        AttackPressed = true;
        yield return null;
        AttackPressed = false;
        Check(_attack.HasBufferedAttack, "Early click not captured");
        yield return Frames(Mathf.CeilToInt(0.25f * _fps));
        Check(!_attack.HasBufferedAttack, "Stale buffer did not expire");
        yield return WaitAttack();
        Check(_attack.NextCombo == 2 && !_attack.IsAttacking, "Stale click caused automatic attack");

        // Extra clicks cannot extend a pending click's lifetime or create a queue of many stages.
        ResetAttack();
        SetSetting("_inputBufferTime", 1f);
        yield return Click(1);
        yield return WaitProgress(0.2f);
        AttackPressed = true;
        yield return null;
        float firstInputTime = (float)typeof(PlayerAttack).GetField("_bufferedInputTime", Flags).GetValue(_attack);
        yield return Frames(3);
        AttackPressed = false;
        Check(Mathf.Abs(firstInputTime - (float)typeof(PlayerAttack).GetField("_bufferedInputTime", Flags).GetValue(_attack)) < 0.0001f,
            "Spam refreshed pending click");
        yield return WaitAttack();
        Check(_attack.NextCombo == 3, "Spam queued more than one attack");
        SetSetting("_inputBufferTime", 0.2f);

        // Cancel the queued click as well as the current animation; keep the established combo step.
        ResetAttack();
        yield return Click(1);
        yield return WaitProgress(0.8f);
        AttackPressed = true;
        yield return null;
        AttackPressed = false;
        RollPressed = true;
        yield return null;
        ClearInput();
        Check(_move.IsRolling && !_attack.IsAttacking && !_attack.HasBufferedAttack && _attack.NextCombo == 2,
            "Roll failed to discard pending input and preserve sequence");
        yield return WaitRoll();
        yield return Frames(8);
        Check(!_attack.IsAttacking, "Attack fired automatically after roll");
        yield return Click(2);
        yield return WaitAttack();

        // A dodge on the exact animation completion frame must win over the queued combo.
        ResetAttack();
        yield return Click(1);
        yield return WaitProgress(0.86f);
        AttackPressed = true;
        yield return null;
        AttackPressed = false;
        _boundaryTriggered = false;
        _rollAtAttackEnd = true;
        int boundaryFrames = 0;
        while (!_move.IsRolling && boundaryFrames++ < _fps * 2)
        {
            yield return null;
        }
        ClearInput();
        _rollAtAttackEnd = false;
        Check(_boundaryTriggered && _move.IsRolling && !_attack.HasBufferedAttack && _attack.NextCombo == 2,
            "Queued combo beat roll on final frame");
        yield return WaitRoll();

        // Input validity is decided when clicked, even if the old combo timer expires before execution.
        ResetAttack();
        SetDelay(1, 2.2f);
        yield return Click(1);
        yield return WaitProgress(0.75f);
        float elapsedBeforeClick = (float)typeof(PlayerAttack).GetField("_comboElapsed", Flags).GetValue(_attack);
        SetDelay(1, elapsedBeforeClick + 1.5f / _fps);
        AttackPressed = true;
        yield return null;
        AttackPressed = false;
        Check(_attack.HasBufferedAttack, "Valid deadline click not buffered");
        while (_attack.CurrentCombo == 1)
        {
            yield return null;
        }
        Check(_attack.CurrentCombo == 2, "Accepted click lost combo step while waiting for full clip");
        yield return WaitAttack();

        // Timer expiry can link 01 back to 01. The outgoing 01 must not end the new one during blending.
        ResetAttack();
        SetDelay(1, 0.1f);
        yield return Click(1);
        yield return WaitProgress(0.86f);
        AttackPressed = true;
        yield return null;
        AttackPressed = false;
        Check(_attack.HasBufferedAttack, "Expired-combo click not buffered");
        int sameFrames = 0;
        while (_attack.HasBufferedAttack && sameFrames++ < _fps * 2)
        {
            yield return null;
        }
        float sameStart = Time.time;
        Check(_attack.IsAttacking && _attack.CurrentCombo == 1, "01 self-transition failed");
        yield return WaitAttack();
        Check(Time.time - sameStart > 0.5f, "Old normalizedTime ended new 01 early");
        SetDelay(1, 2.2f);

        ResetAttack();
        SetSetting("_inputBufferTime", 0f);
        yield return Click(1);
        yield return WaitProgress(0.86f);
        AttackPressed = true;
        yield return null;
        AttackPressed = false;
        Check(!_attack.HasBufferedAttack, "Zero buffer setting still queued input");
        yield return WaitAttack();
        SetSetting("_inputBufferTime", 0.2f);
        ResetAttack();
        _report.AppendLine($"PASS {_fps}fps edge cases: stale expiry; one queue only; roll discards queue; exact-end roll priority; accepted-click deadline; self-transition; buffer=0");
    }

    private IEnumerator WaitProgress(float normalized)
    {
        int frames = 0;
        while (frames++ < _fps * 3)
        {
            if (_attack.IsAttacking && !_animator.IsInTransition(0)
                && _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("AttackCombo" + _attack.CurrentCombo.ToString("00"))
                && _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= normalized)
            {
                yield break;
            }
            yield return null;
        }
        Check(false, "Attack progress timeout");
    }

    private void SetSetting(string name, float value)
    {
        typeof(PlayerAttack).GetField(name, Flags).SetValue(_attack, value);
    }

    private IEnumerator Click(int combo)
    {
        AttackPressed = true;
        yield return null;
        AttackPressed = false;
        Check(_attack.IsAttacking && _attack.CurrentCombo == combo, "Expected combo " + combo);
    }

    private IEnumerator Frames(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            yield return null;
        }
    }

    private IEnumerator WaitAttack()
    {
        int frames = 0;
        while (_attack.IsAttacking && frames++ < _fps * 4)
        {
            yield return null;
        }
        Check(!_attack.IsAttacking, "Attack timeout");
    }

    private IEnumerator WaitRoll()
    {
        int frames = 0;
        while (_move.IsRolling && frames++ < _fps * 3)
        {
            yield return null;
        }
        Check(!_move.IsRolling, "Roll timeout");
    }

    private void SetDelay(int combo, float delay)
    {
        typeof(PlayerAttack).GetField("_comboDelay" + combo.ToString("00"), Flags).SetValue(_attack, delay);
    }

    private void ResetAttack()
    {
        ClearInput();
        _attack.enabled = false;
        _attack.enabled = true;
    }

    private static Vector3 Planar(Vector3 value) => new Vector3(value.x, 0, value.z);

    private static void ClearInput()
    {
        Horizontal = 0;
        Vertical = 0;
        AttackPressed = false;
        RollPressed = false;
        DashPressed = false;
        WHeld = false;
    }

    private void Check(bool condition, string message)
    {
        if (!condition)
        {
            File.WriteAllText(Path.Combine(Application.dataPath, "../../player-combo-blend-result.txt"), "FAIL " + _fps + "fps: " + message + "\n" + _report);
            Debug.LogError(message);
            EditorApplication.Exit(2);
            throw new Exception(message);
        }
    }
}
#endif
