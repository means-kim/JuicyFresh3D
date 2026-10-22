using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ChargeMotionProbe
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        if (File.Exists(Path.Combine(Application.dataPath, "../run-combat-fsm.flag")))
        {
            CombatFsmProbe.Run();
            return;
        }
        var report = new StringBuilder();
        RunCase(report, 0f, false);
        RunCase(report, 90f, false);
        RunCase(report, 0f, true);
        File.WriteAllText(Path.Combine(Application.dataPath, "../../charge-runtime-result.txt"), report.ToString());
        Debug.Log(report.ToString());
    }

    private static void RunCase(StringBuilder report, float yaw, bool wall)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(100f, 1f, 100f);
        GameObject obstacle = null;
        if (wall)
        {
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = new Vector3(0f, 3f, 3f);
            obstacle.transform.localScale = new Vector3(20f, 6f, 0.5f);
        }

        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Probe/SK_BullTank.fbx");
        GameObject boss = UnityEngine.Object.Instantiate(source);
        boss.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
        Animator animator = boss.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Probe/BossMonsterAnimator.controller");
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var controller = boss.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = Vector3.up;
        var attack = boss.AddComponent<BossAttackController>();
        var movement = boss.AddComponent<BossMovement>();
        typeof(BossAttackController).GetMethod("Awake", Flags).Invoke(attack, null);
        typeof(BossMovement).GetMethod("Awake", Flags).Invoke(movement, null);
        Transform root = boss.GetComponentsInChildren<Transform>().First(t => t.name == "DeformationSystem");
        animator.Rebind();
        animator.Play("Idle", 0, 0f);
        animator.Update(0f);
        Vector3 rootLocalStart = boss.transform.InverseTransformPoint(root.position);
        Physics.SyncTransforms();
        attack.TryStartAttack(BossAttackType.Charge);
        float maxBodyDrift = 0f;
        float maxStep = 0f;
        float maxRootHeight = boss.transform.position.y;
        float minFeetHeight = float.PositiveInfinity;
        Transform[] feet = boss.GetComponentsInChildren<Transform>().Where(t => t.name == "Ankle_L" || t.name == "Ankle_R" || t.name == "Toes_L" || t.name == "Toes_R").ToArray();
        int manualCallbacks = 0;
        bool entered = false;
        bool finished = false;
        var callback = typeof(BossMovement).GetMethod("OnAnimatorMove", Flags);
        for (int frame = 0; frame < 600; frame++)
        {
            Vector3 before = boss.transform.position;
            animator.Update(1f / 60f);
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool charge = state.IsTag("ChargeRootMotion") || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("ChargeRootMotion"));
            if (charge)
            {
                entered = true;
                typeof(BossMovement).GetField("_verticalSpeed", Flags).SetValue(movement, -10f);
                typeof(BossMovement).GetMethod("Update", Flags).Invoke(movement, null);
                if ((float)typeof(BossMovement).GetField("_verticalSpeed", Flags).GetValue(movement) != 0f)
                {
                    throw new Exception("Code gravity was not suspended during charge");
                }
                if ((boss.transform.position - before).sqrMagnitude < 0.00000001f && new Vector2(animator.deltaPosition.x, animator.deltaPosition.z).sqrMagnitude > 0.00000001f)
                {
                    callback.Invoke(movement, null);
                    manualCallbacks++;
                }
            }
            maxStep = Mathf.Max(maxStep, (boss.transform.position - before).magnitude);
            maxRootHeight = Mathf.Max(maxRootHeight, boss.transform.position.y);
            minFeetHeight = Mathf.Min(minFeetHeight, feet.Min(t => t.position.y));
            Vector3 drift = boss.transform.InverseTransformPoint(root.position) - rootLocalStart;
            drift.y = 0f;
            maxBodyDrift = Mathf.Max(maxBodyDrift, drift.magnitude);
            if (entered && state.IsName("Idle") && !animator.IsInTransition(0))
            {
                finished = true;
                break;
            }
        }
        Vector3 landing = boss.transform.position;
        for (int frame = 0; frame < 60; frame++)
        {
            animator.Update(1f / 60f);
            callback.Invoke(movement, null);
        }
        float forward = Vector3.Dot(landing, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
        float idleReturn = Vector3.Distance(boss.transform.position, landing);
        report.AppendLine($"yaw={yaw} wall={wall} finished={finished} attackFlag={attack.IsAttacking} landing={landing:F4} forward={forward:F3} idleReturn={idleReturn:F6} bodyDrift={maxBodyDrift:F4} maxStep={maxStep:F4} editorManualCallbacks={manualCallbacks} maxRootHeight={maxRootHeight:F4} minFeetHeight={minFeetHeight:F4}");
        if (!finished || attack.IsAttacking || forward < 1f || idleReturn > 0.01f || maxStep > 0.75f || maxBodyDrift > 0.1f || maxRootHeight < 1f || minFeetHeight < -0.02f)
        {
            throw new Exception("Charge validation failed: " + report);
        }
        if (wall && landing.z > 2.5f)
        {
            throw new Exception("Charge passed through the wall: " + landing);
        }
        UnityEngine.Object.DestroyImmediate(boss);
        UnityEngine.Object.DestroyImmediate(floor);
        if (obstacle != null)
        {
            UnityEngine.Object.DestroyImmediate(obstacle);
        }
    }
}
