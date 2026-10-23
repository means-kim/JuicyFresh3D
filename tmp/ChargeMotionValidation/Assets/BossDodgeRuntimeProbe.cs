#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public class BossDodgeRuntimeProbe : MonoBehaviour
{
    public static float Horizontal;
    public static float Vertical;
    public static bool AttackPressed;
    public static bool RollPressed;
    public static bool DashPressed;
    public static bool WHeld;
    private PlayerMove _player;
    private PlayerAttack _playerAttack;
    private CharacterController _playerController;
    private BossAttackController _boss;
    private BossCombatController _combat;
    private Animator _bossAnimator;
    private AnimationClip _clip;
    private Vector3 _bossPosition;
    private Vector3 _rollStartPosition;
    private int _successes;
    private int _rolls;
    private int _errors;
    private int _fps;
    private readonly StringBuilder _report = new StringBuilder();
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly int MeleeHash = Animator.StringToHash("Base Layer.Boss_Attack01");

    private IEnumerator Start()
    {
        _player = FindFirstObjectByType<PlayerMove>();
        _playerAttack = _player.GetComponent<PlayerAttack>();
        _playerController = _player.GetComponent<CharacterController>();
        _boss = FindFirstObjectByType<BossAttackController>();
        _combat = _boss.GetComponent<BossCombatController>();
        _combat.enabled = false;
        _boss.GetComponent<BossMovement>().enabled = false;
        _bossAnimator = _boss.GetComponent<Animator>();
        _bossAnimator.applyRootMotion = false;
        _bossAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _bossAnimator.speed = 0f;
        _bossPosition = _boss.transform.position;
        _clip = _bossAnimator.runtimeAnimatorController.animationClips.First(c => c.name == "Attack_01");
        _boss.DodgeSucceeded += () => _successes++;
        _player.RollStarted += player =>
        {
            _rolls++;
            _rollStartPosition = player.transform.position;
        };
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (trace.Contains("UnityEditor.Search.SearchDatabase"))
            {
                return;
            }
            if (type == LogType.Error || type == LogType.Exception
                || (message.Contains("Parameter") && message.Contains("does not exist")))
            {
                _errors++;
            }
        };
        _report.AppendLine($"Clip={_clip.name}; length={_clip.length:F4}s; sourceFPS={_clip.frameRate:F2}; totalFrames={_clip.length * _clip.frameRate:F2}");

        foreach (int fps in new[] { 60, 30 })
        {
            _fps = fps;
            Time.captureDeltaTime = 1f / fps;
            ClearInput();
            yield return Frames(12);
            SetRange(2.5f);

            // Check original animation frame boundaries using actual accepted roll inputs.
            foreach (float frame in new[] { 39f, 40f, 50f, 59.9f, 60f, 65f })
            {
                yield return Prepare(frame, 1.6f);
                bool expected = frame >= 40 && frame < 60;
                Check(_boss.IsDodgeWindowOpen == expected, "Incorrect Window at source frame " + frame);
                int successes = _successes;
                int rolls = _rolls;
                yield return Roll();
                Check(_rolls == rolls + 1, "Grounded Space did not start a roll");
                Check(_successes - successes == (expected ? 1 : 0), "Incorrect success at source frame " + frame);
                _report.AppendLine($"PASS {fps}fps sourceFrame={frame:F2}: success={expected}; actualRoll=True");
            }

            foreach (float distance in new[] { 2.5f, 2.51f, 4f })
            {
                yield return Prepare(45f, distance);
                bool expected = distance <= 2.5f;
                int successes = _successes;
                yield return Roll();
                Check(_successes - successes == (expected ? 1 : 0), "Incorrect distance judgement at " + distance);
                Check(Mathf.Abs(PlanarDistance(_rollStartPosition) - distance) < 0.002f,
                    "Roll event emitted after movement instead of at start");
                _report.AppendLine($"PASS {fps}fps startDistance={distance:F2}: success={expected}; preMovementPosition=True");
            }

            // Start inside the melee range, then leave before the Window and roll in empty space.
            yield return Prepare(0, 1.6f);
            PositionPlayer(4f);
            SetFrame(45f);
            int before = _successes;
            yield return Roll();
            Check(_successes == before, "Luring melee then rolling outside range succeeded");

            // A roll begun before frame 40 must not become successful when the Window later opens.
            yield return Prepare(35, 1.6f);
            before = _successes;
            yield return Roll();
            SetFrame(45);
            yield return Frames(3);
            Check(_successes == before, "Existing roll succeeded when Window opened later");
            int beforeRolls = _rolls;
            yield return Roll();
            Check(_rolls == beforeRolls && _successes == before, "Repeated Space during roll was accepted");

            // Two real rolls in a frozen Window still issue only one success event per boss attack.
            yield return Prepare(45, 1.6f);
            before = _successes;
            yield return Roll();
            yield return WaitActions();
            PositionPlayer(1.6f);
            yield return Roll();
            Check(_successes == before + 1 && !_boss.IsDodgeWindowOpen, "Duplicate success in same attack");
            yield return Prepare(45, 1.6f);
            yield return Roll();
            Check(_successes == before + 2, "Next boss attack did not reset success eligibility");

            // The same configured melee range governs both boss attack selection and dodge judgement.
            SetRange(3f);
            yield return Prepare(45, 3.01f);
            before = _successes;
            yield return Roll();
            Check(_successes == before && !_combat.IsWithinMeleeRange(_rollStartPosition), "Old range used after Inspector edit");
            yield return Prepare(45, 3f);
            yield return Roll();
            Check(_successes == before + 1 && _combat.IsWithinMeleeRange(_rollStartPosition),
                "Edited melee range boundary rejected: before=" + before + " after=" + _successes
                + " start=" + _rollStartPosition.ToString("F6") + " boss=" + _boss.transform.position.ToString("F6")
                + " distance=" + PlanarDistance(_rollStartPosition).ToString("F6") + " range=" + _combat.MeleeRange);
            SetRange(2.5f);

            // Raw Space in the air must not be mistaken for an accepted roll.
            yield return Prepare(45, 1.6f);
            _playerController.enabled = false;
            _player.transform.position = _bossPosition + Vector3.right * 1.6f + Vector3.up * 3f;
            _playerController.enabled = true;
            _playerController.Move(Vector3.down * 0.001f);
            yield return null;
            Check(!_playerController.isGrounded, "Airborne test did not leave ground");
            before = _successes;
            beforeRolls = _rolls;
            yield return Roll();
            Check(_successes == before && _rolls == beforeRolls, "Airborne Space caused dodge success");

            // Attack cancellation uses exactly the same accepted-roll event.
            yield return Prepare(45, 1.6f);
            AttackPressed = true;
            yield return null;
            AttackPressed = false;
            Check(_playerAttack.IsAttacking, "Player attack not started for cancellation test");
            before = _successes;
            yield return Roll();
            Check(_successes == before + 1 && !_playerAttack.IsAttacking && _player.IsRolling,
                "Attack-cancel roll did not report success");

            // Charge/throw requests cannot award melee dodge success, even if pose is forced to melee.
            foreach (BossAttackType type in new[] { BossAttackType.Charge, BossAttackType.Throw })
            {
                yield return Prepare(45, 1.6f, type);
                Check(!_boss.IsDodgeWindowOpen, "Non-melee request opened Window");
                before = _successes;
                yield return Roll();
                Check(_successes == before, "Non-melee request caused success");
            }

            yield return Prepare(45, 1.6f);
            _boss.OnAttackFinished();
            Check(!_boss.IsDodgeWindowOpen, "Attack end left Window open");
            before = _successes;
            yield return Roll();
            Check(_successes == before, "Ended attack caused success");

            yield return Prepare(45, 1.6f);
            _boss.enabled = false;
            Check(!_boss.IsDodgeWindowOpen, "Disabled boss left Window open");
            before = _successes;
            yield return Roll();
            Check(_successes == before, "Disabled boss still listened for roll");
            yield return WaitActions();
            _boss.enabled = true;
            Check(!_boss.IsDodgeWindowOpen, "Re-enable reopened an interrupted Window");
            yield return Prepare(45, 1.6f);
            before = _successes;
            yield return Roll();
            Check(_successes == before + 1, "Re-enable caused missing/duplicate subscriptions");

            // Natural playback must open/close at the original frame numbers, independent of Update order.
            yield return Prepare(0, 1.6f);
            _bossAnimator.speed = 1f;
            int frames = 0;
            while (!_boss.IsDodgeWindowOpen && frames++ < fps * 6)
            {
                yield return null;
            }
            float openFrame = _boss.CurrentMeleeFrame;
            Check(_boss.IsDodgeWindowOpen && openFrame >= 39.999f
                && openFrame < 40 + _clip.frameRate / fps * 2.1f, "Natural Window opening not aligned to frame 40");
            while (_boss.IsDodgeWindowOpen && frames++ < fps * 6)
            {
                yield return null;
            }
            float closeFrame = _boss.CurrentMeleeFrame;
            Check(!_boss.IsDodgeWindowOpen && closeFrame >= 59.999f
                && closeFrame < 60 + _clip.frameRate / fps * 2.1f, "Natural Window closing not aligned to frame 60");
            _bossAnimator.speed = 0;
            _report.AppendLine($"PASS {fps}fps lifecycle: lure rejected; early roll rejected; one success/attack; live shared range; air rejected; attack-cancel roll; ranged rejected; end/disable closed; subscription restored");
            _report.AppendLine($"PASS {fps}fps natural playback: openFrame={openFrame:F4}; closeFrame={closeFrame:F4}");
        }
        Check(_errors == 0, "Runtime errors or missing parameters");
        _report.AppendLine("PASS parameterless DodgeSucceeded; no runtime errors/parameter warnings");
        File.WriteAllText(Path.Combine(Application.dataPath, "../../boss-dodge-result.txt"), _report.ToString());
        Debug.Log(_report.ToString());
        EditorApplication.ExitPlaymode();
    }

    private IEnumerator Prepare(float frame, float distance, BossAttackType type = BossAttackType.Melee)
    {
        ClearInput();
        yield return WaitActions();
        _boss.OnAttackFinished();
        _bossAnimator.speed = 0;
        _bossAnimator.ResetTrigger("AttackStart");
        _bossAnimator.Play("Base Layer.Idle", 0, 0);
        _bossAnimator.Update(0);
        PositionPlayer(distance);
        _boss.TryStartAttack(type);
        _bossAnimator.ResetTrigger("AttackStart");
        SetFrame(frame);
        yield return null;
        Check(_boss.IsAttacking && _playerController.isGrounded, "Case setup failed");
    }

    private void SetFrame(float frame)
    {
        _bossAnimator.Play(MeleeHash, 0, frame / (_clip.length * _clip.frameRate));
        _bossAnimator.Update(0);
    }

    private void PositionPlayer(float distance)
    {
        _playerController.enabled = false;
        _player.transform.position = _bossPosition + Vector3.right * distance + Vector3.up * 0.05f;
        _playerController.enabled = true;
        _playerController.Move(Vector3.down * 0.2f);
        Physics.SyncTransforms();
    }

    private float PlanarDistance(Vector3 position)
    {
        position -= _bossPosition;
        position.y = 0;
        return position.magnitude;
    }

    private void SetRange(float range)
    {
        typeof(BossCombatController).GetField("_meleeRange", Flags).SetValue(_combat, range);
    }

    private IEnumerator Roll()
    {
        RollPressed = true;
        yield return null;
        RollPressed = false;
    }

    private IEnumerator WaitActions()
    {
        int frames = 0;
        while ((_player.IsRolling || _player.IsDashing || _playerAttack.IsAttacking) && frames++ < _fps * 4)
        {
            yield return null;
        }
        Check(frames < _fps * 4, "Player action did not finish");
    }

    private IEnumerator Frames(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            yield return null;
        }
    }

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
            File.WriteAllText(Path.Combine(Application.dataPath, "../../boss-dodge-result.txt"), "FAIL " + _fps + "fps: " + message + "\n" + _report);
            Debug.LogError(message);
            EditorApplication.Exit(2);
            throw new Exception(message);
        }
    }
}
#endif
