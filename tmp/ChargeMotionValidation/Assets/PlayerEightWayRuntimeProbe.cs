#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public class PlayerEightWayRuntimeProbe : MonoBehaviour
{
    public static float Horizontal;
    public static float Vertical;
    public static bool RollPressed;
    public static bool DashPressed;
    public static bool WHeld;
    private PlayerMove _player;
    private CharacterController _controller;
    private Animator _animator;
    private Camera _camera;
    private CinemachineFollow _follow;
    private Vector3 _origin;
    private int _warnings;
    private readonly StringBuilder _report = new StringBuilder();

    private IEnumerator Start()
    {
        _player = FindFirstObjectByType<PlayerMove>();
        _controller = _player.GetComponent<CharacterController>();
        _animator = _player.GetComponent<Animator>();
        _camera = Camera.main;
        _follow = FindFirstObjectByType<CinemachineFollow>();
        _origin = _player.transform.position;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (message.Contains("Parameter") && message.Contains("does not exist"))
            {
                _warnings++;
            }
        };
        Vector2[] inputs = { Vector2.up, Vector2.right, Vector2.down, Vector2.left,
            new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) };

        foreach (int fps in new[] { 60, 30 })
        {
            Time.captureDeltaTime = 1f / fps;
            foreach (int cameraMode in new[] { 0, 90 })
            {
                if (cameraMode == 90)
                {
                    var tracker = _follow.TrackerSettings;
                    tracker.BindingMode = BindingMode.WorldSpace;
                    _follow.TrackerSettings = tracker;
                    _follow.FollowOffset = Quaternion.Euler(0, 90, 0) * new Vector3(0, 1, -4);
                }
                foreach (Vector2 input in inputs)
                {
                    ResetPosition();
                    for (int i = 0; i < 12; i++)
                    {
                        yield return null;
                    }
                    Vector3 forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    Vector3 expected = (right * input.x + forward * input.y).normalized;
                    Vector3 start = _player.transform.position;
                    float cameraYaw = _camera.transform.eulerAngles.y;
                    Horizontal = input.x;
                    Vertical = input.y;
                    for (int frame = 0; frame < fps; frame++)
                    {
                        yield return null;
                        Check(Mathf.Abs(_player.transform.position.y) < 0.15f, "Player left ground");
                        Check(Mathf.Abs(Mathf.DeltaAngle(cameraYaw, _camera.transform.eulerAngles.y)) < 0.1f,
                            "Camera rotated with the player's facing");
                    }
                    Vector3 displacement = _player.transform.position - start;
                    displacement.y = 0;
                    Check(Mathf.Abs(displacement.magnitude - 3f) < 0.04f, "Incorrect straight/diagonal speed");
                    Check(Vector3.Dot(displacement.normalized, expected) > 0.999f, "Incorrect camera-relative direction");
                    Check(Vector3.Angle(_player.transform.forward, expected) < 0.5f, "Facing did not settle toward movement");
                    Check(_animator.GetBool("IsMoving"), "Run animation not selected");
                    _report.AppendLine($"PASS {fps}fps camera={cameraYaw:F1} input={input}: distance={displacement.magnitude:F4}; facingError={Vector3.Angle(_player.transform.forward, expected):F4}; ground=True; cameraStable=True");
                }
            }
            // Check action keys through the same Update path used by actual input.
            ResetPosition();
            for (int i = 0; i < 12; i++)
            {
                yield return null;
            }
            DashPressed = true;
            yield return null;
            DashPressed = false;
            Check(!_player.IsDashing, "Dash accepted without W");
            Vector3 cameraForward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            Vector3 rollDirection = (cameraForward + Vector3.Cross(Vector3.up, cameraForward)).normalized;
            Horizontal = 1;
            Vertical = 1;
            WHeld = true;
            RollPressed = true;
            Vector3 rollStart = _player.transform.position;
            yield return null;
            RollPressed = false;
            Check(_player.IsRolling, "Roll key did not start rolling");
            ClearInput();
            int actionFrames = 0;
            while (_player.IsRolling && actionFrames++ < fps * 4)
            {
                // Changing movement input while rolling must not steer the action.
                Horizontal = -1;
                Vertical = 0;
                yield return null;
            }
            ClearInput();
            Vector3 rollTravel = _player.transform.position - rollStart;
            rollTravel.y = 0;
            Check(actionFrames < fps * 4 && Mathf.Abs(rollTravel.magnitude - 3f) < 0.04f, "Roll distance/exit regressed");
            Check(Vector3.Dot(rollTravel.normalized, rollDirection) > 0.999f, "Roll did not use input direction");

            ResetPosition();
            for (int i = 0; i < 12; i++)
            {
                yield return null;
            }
            _player.transform.rotation = Quaternion.Euler(0, 135, 0);
            Vector3 dashDirection = _player.transform.forward;
            Vector3 dashStart = _player.transform.position;
            Vertical = 1;
            WHeld = true;
            DashPressed = true;
            yield return null;
            Check(_player.IsDashing, "W+right click did not start dash");
            ClearInput();
            actionFrames = 0;
            while (_player.IsDashing && actionFrames++ < fps * 4)
            {
                yield return null;
            }
            Vector3 dashTravel = _player.transform.position - dashStart;
            dashTravel.y = 0;
            Check(actionFrames < fps * 4 && Mathf.Abs(dashTravel.magnitude - 4f) < 0.04f, "Dash distance/exit regressed");
            Check(Vector3.Dot(dashTravel.normalized, dashDirection) > 0.999f, "Dash did not preserve starting facing");
            _report.AppendLine($"PASS {fps}fps actions: roll={rollTravel.magnitude:F4}; dash={dashTravel.magnitude:F4}; WRequired=True; releaseWContinuesDash=True; inputRollDirection=True; actionLock=True");
        }
        Check(_warnings == 0, "Missing animator parameters");
        _report.AppendLine("PASS: no missing-parameter warnings");
        File.WriteAllText(Path.Combine(Application.dataPath, "../../player-eight-way-result.txt"), _report.ToString());
        Debug.Log(_report.ToString());
        EditorApplication.ExitPlaymode();
    }

    private void ResetPosition()
    {
        ClearInput();
        _controller.enabled = false;
        _player.transform.position = _origin;
        _controller.enabled = true;
        Physics.SyncTransforms();
    }

    private static void ClearInput()
    {
        Horizontal = 0;
        Vertical = 0;
        WHeld = false;
        RollPressed = false;
        DashPressed = false;
    }

    private void Check(bool condition, string message)
    {
        if (!condition)
        {
            File.WriteAllText(Path.Combine(Application.dataPath, "../../player-eight-way-result.txt"), "FAIL: " + message + "\n" + _report);
            Debug.LogError(message);
            EditorApplication.Exit(2);
            throw new System.Exception(message);
        }
    }
}
#endif
