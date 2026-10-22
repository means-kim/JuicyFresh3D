using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CombatFsmProbe
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly StringBuilder Report = new StringBuilder();

    public static void Run()
    {
        VerifyFarTarget();
        VerifyCycle(1.5f, 50, 50, BossState.MeleeAttack);
        VerifyCycle(5f, 100, 0, BossState.ChargeAttack);
        VerifyCycle(5f, 0, 100, BossState.ThrowAttack);
        VerifyExternalAndMissedEvent();
        File.WriteAllText(Path.Combine(Application.dataPath, "../../combat-fsm-result.txt"), Report.ToString());
        Debug.Log(Report.ToString());
    }

    private static void VerifyFarTarget()
    {
        var fixture = new Fixture(20f, 50, 50);
        Tick(fixture.Combat, "Update");
        Check(fixture.Combat.CurrentState == BossState.Chase && !fixture.Attack.IsAttacking,
            "Out of range must remain Chase");
        Set(fixture.Combat, "_target", null);
        Tick(fixture.Combat, "Update");
        Check(fixture.Combat.CurrentState == BossState.Chase, "Missing target must not start an attack");
        Report.AppendLine("PASS: out-of-range and missing target keep Chase without starting an attack");
        fixture.Dispose();
    }

    private static void VerifyCycle(float distance, int chargeWeight, int throwWeight, BossState expected)
    {
        var fixture = new Fixture(distance, chargeWeight, throwWeight);
        Tick(fixture.Combat, "Update");
        Check(fixture.Combat.CurrentState == expected && fixture.Attack.IsAttacking, "Incorrect attack choice: " + expected);
        fixture.Target.transform.position = Vector3.forward * 100f;
        bool recoverySeen = false;
        bool waitedForAnimation = false;
        bool completed = false;
        float maxRootHeight = 0f;
        float maxStep = 0f;
        for (int frame = 0; frame < 720; frame++)
        {
            Vector3 before = fixture.Boss.transform.position;
            fixture.Animator.Update(1f / 60f);
            if ((fixture.Boss.transform.position - before).sqrMagnitude < 0.00000001f)
            {
                Tick(fixture.Movement, "OnAnimatorMove");
            }
            maxRootHeight = Mathf.Max(maxRootHeight, fixture.Boss.transform.position.y);
            maxStep = Mathf.Max(maxStep, Vector3.Distance(before, fixture.Boss.transform.position));
            if (fixture.Combat.CurrentState == BossState.Recovery)
            {
                recoverySeen = true;
                bool ready = (bool)Tick(fixture.Combat, "IsLocomotionReady");
                Tick(fixture.Combat, "TickRecovery", 1f / 60f);
                if (!ready)
                {
                    Check(fixture.Combat.CurrentState == BossState.Recovery, "Recovery ended before animation settled");
                    waitedForAnimation = true;
                }
            }
            else
            {
                Tick(fixture.Combat, "Update");
            }

            if (fixture.Combat.CurrentState == BossState.Chase)
            {
                Check(recoverySeen, "Attack bypassed Recovery");
                completed = true;
                break;
            }
            Check(fixture.Combat.CurrentState == expected || fixture.Combat.CurrentState == BossState.Recovery,
                "Distance change selected another attack mid-cycle");
        }
        Check(completed && !fixture.Attack.IsAttacking, "Attack did not finish: " + expected);
        if (expected == BossState.ChargeAttack)
        {
            Check(fixture.Boss.transform.position.z > 5f && maxRootHeight > 1f && maxStep < 0.75f,
                "Charge displacement or jump regressed");
        }

        fixture.Target.transform.position = fixture.Boss.transform.position + Vector3.forward * distance;
        Tick(fixture.Combat, "Update");
        Check(fixture.Combat.CurrentState == expected && fixture.Attack.IsAttacking,
            "A second attack could not start after Recovery: " + expected);
        Report.AppendLine($"PASS: {expected} -> Recovery -> Chase -> {expected}; target changes ignored during attack; animation gate={waitedForAnimation}; peakY={maxRootHeight:F3}; maxStep={maxStep:F3}");
        fixture.Dispose();
    }

    private static void VerifyExternalAndMissedEvent()
    {
        var fixture = new Fixture(20f, 50, 50);
        fixture.Attack.TryStartAttack(BossAttackType.Throw);
        Tick(fixture.Combat, "Update");
        Check(fixture.Combat.CurrentState == BossState.ThrowAttack, "External attack was not reflected in FSM");
        Tick(fixture.Combat, "OnDisable");
        fixture.Attack.OnAttackFinished();
        Tick(fixture.Combat, "OnEnable");
        Tick(fixture.Combat, "Update");
        Check(fixture.Combat.CurrentState == BossState.Recovery, "Missed completion event did not recover");
        Tick(fixture.Combat, "TickRecovery", 0.1f);
        float remaining = (float)Get(fixture.Combat, "_cooldownRemaining");
        fixture.Attack.OnAttackFinished();
        Check(Mathf.Approximately(remaining, (float)Get(fixture.Combat, "_cooldownRemaining")),
            "Duplicate completion reset the cooldown");
        Report.AppendLine("PASS: external attack adoption, missed completion event recovery, duplicate completion protection");
        fixture.Dispose();
    }

    private static object Tick(object instance, string method, params object[] args)
    {
        return instance.GetType().GetMethod(method, Flags).Invoke(instance, args);
    }

    private static void Set(object instance, string field, object value)
    {
        instance.GetType().GetField(field, Flags).SetValue(instance, value);
    }

    private static object Get(object instance, string field)
    {
        return instance.GetType().GetField(field, Flags).GetValue(instance);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message + "\n" + Report);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject Boss;
        public readonly GameObject Target;
        public readonly Animator Animator;
        public readonly BossAttackController Attack;
        public readonly BossMovement Movement;
        public readonly BossCombatController Combat;
        private readonly GameObject _floor;

        public Fixture(float distance, int chargeWeight, int throwWeight)
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(200f, 1f, 200f);
            Boss = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Probe/SK_BullTank.fbx"));
            Boss.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator = Boss.GetComponent<Animator>();
            Animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Probe/BossMonsterAnimator.controller");
            var controller = Boss.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.up;
            Attack = Boss.AddComponent<BossAttackController>();
            Movement = Boss.AddComponent<BossMovement>();
            Combat = Boss.AddComponent<BossCombatController>();
            Tick(Attack, "Awake");
            Tick(Movement, "Awake");
            Tick(Combat, "Awake");
            Target = new GameObject("PlayerTarget");
            Target.transform.position = Vector3.forward * distance;
            Set(Combat, "_target", Target.transform);
            Set(Combat, "_attackCooldown", 0.2f);
            Set(Attack, "chargeWeight", chargeWeight);
            Set(Attack, "throwWeight", throwWeight);
            Tick(Combat, "OnEnable");
            Animator.Rebind();
            Animator.Play("Idle", 0, 0f);
            Animator.Update(0f);
            Physics.SyncTransforms();
        }

        public void Dispose()
        {
            Tick(Combat, "OnDisable");
            UnityEngine.Object.DestroyImmediate(Boss);
            UnityEngine.Object.DestroyImmediate(Target);
            UnityEngine.Object.DestroyImmediate(_floor);
        }
    }
}
