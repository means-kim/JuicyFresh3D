#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public class PlayerGroundingRuntimeProbe : MonoBehaviour
{
    public static float TurnInput;
    public static float MoveInput;
    private int _parameterWarnings;

    private IEnumerator Start()
    {
        Time.captureDeltaTime = 1f / 60f;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (message.Contains("Parameter") && message.Contains("does not exist"))
            {
                _parameterWarnings++;
            }
        };
        var report = new StringBuilder();
        var player = FindFirstObjectByType<PlayerMove>();
        var capsule = player.GetComponent<CharacterController>();
        var feet = player.GetComponentsInChildren<Transform>().Where(t => t.name == "foot_l" || t.name == "foot_r").ToArray();
        foreach (string phase in new[] { "idle", "forward", "backward", "turn", "forward-turn" })
        {
            MoveInput = phase == "forward" || phase == "forward-turn" ? 1f : phase == "backward" ? -1f : 0f;
            TurnInput = phase == "turn" || phase == "forward-turn" ? 1f : 0f;
            float minRootY = float.PositiveInfinity;
            float maxRootY = float.NegativeInfinity;
            float minLowerFootY = float.PositiveInfinity;
            float maxLowerFootY = float.NegativeInfinity;
            Vector3 start = player.transform.position;
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                minRootY = Mathf.Min(minRootY, player.transform.position.y);
                maxRootY = Mathf.Max(maxRootY, player.transform.position.y);
                float lowerFoot = feet.Min(f => f.position.y);
                minLowerFootY = Mathf.Min(minLowerFootY, lowerFoot);
                maxLowerFootY = Mathf.Max(maxLowerFootY, lowerFoot);
            }
            Vector3 travel = player.transform.position - start;
            travel.y = 0;
            report.AppendLine($"{phase}: rootY=[{minRootY:F4},{maxRootY:F4}], lowerFootY=[{minLowerFootY:F4},{maxLowerFootY:F4}], travel={travel.magnitude:F4}, grounded={capsule.isGrounded}");
            if (maxRootY > 0.15f || minRootY < -0.1f || !player.enabled || (phase == "turn" && travel.magnitude > 0.01f))
            {
                Finish("FAIL: " + report, 2);
                yield break;
            }
        }
        MoveInput = 0;
        TurnInput = 0;
        report.AppendLine("Missing-parameter warnings=" + _parameterWarnings);
        Finish((_parameterWarnings == 0 ? "PASS\n" : "FAIL\n") + report, _parameterWarnings == 0 ? 0 : 2);
    }

    private static void Finish(string report, int exitCode)
    {
        File.WriteAllText(Path.Combine(Application.dataPath, "../../player-grounding-result.txt"), report);
        Debug.Log(report);
        if (exitCode != 0)
        {
            EditorApplication.Exit(exitCode);
        }
        else
        {
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
