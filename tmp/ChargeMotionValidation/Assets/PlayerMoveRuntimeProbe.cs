#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public class PlayerMoveRuntimeProbe : MonoBehaviour
{
    private IEnumerator Start()
    {
        var report = new StringBuilder();
        var move = FindFirstObjectByType<PlayerMove>();
        var animator = move.GetComponent<Animator>();
        var method = typeof(PlayerMove).GetMethod("TryStartAction", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (int fps in new[] { 60, 30 })
        {
            Time.captureDeltaTime = 1f / fps;
            foreach (PlayerMoveState state in new[] { PlayerMoveState.Rolling, PlayerMoveState.Dashing })
            {
                for (int i = 0; i < 10; i++)
                {
                    yield return null;
                }
                Vector3 before = move.transform.position;
                bool started = (bool)method.Invoke(move, new object[] { state });
                Check(started, "Action did not start");
                Check(!(bool)method.Invoke(move, new object[] { state }), "Duplicate action was accepted");
                int frames = 0;
                while (move.CurrentState != PlayerMoveState.Locomotion && frames < fps * 5)
                {
                    frames++;
                    yield return null;
                }
                Vector3 delta = move.transform.position - before;
                delta.y = 0f;
                Check(frames < fps * 5, "Action failed to return to locomotion");
                Check(Mathf.Abs(delta.magnitude - (state == PlayerMoveState.Rolling ? 3f : 4f)) < 0.02f, "Incorrect action distance");
                Check(!animator.IsInTransition(0), "Movement unlocked before animation transition completed");
                report.AppendLine($"PASS {fps}fps {state}: distance={delta.magnitude:F4}; frames={frames}; complete=True; duplicateBlocked=True");
            }
        }
        File.WriteAllText(Path.Combine(Application.dataPath, "../../player-move-result.txt"), report.ToString());
        Debug.Log(report.ToString());
        EditorApplication.ExitPlaymode();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            File.WriteAllText(Path.Combine(Application.dataPath, "../../player-move-result.txt"), "FAIL: " + message);
            Debug.LogError(message);
            EditorApplication.Exit(2);
            throw new Exception(message);
        }
    }
}
#endif
