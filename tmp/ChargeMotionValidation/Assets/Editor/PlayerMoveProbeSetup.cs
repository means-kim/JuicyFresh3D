using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerMoveProbeSetup
{
    private static AnimationClip Clip(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath("Assets/ModularAnimalKnightsPolyart/Animation/" + name + ".fbx")
            .OfType<AnimationClip>().First(c => c.name == name);
    }

    public static void Prepare()
    {
        foreach (BossCombatController boss in Object.FindObjectsByType<BossCombatController>(FindObjectsSortMode.None))
        {
            boss.gameObject.SetActive(false);
        }
        var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/PlayerMoveProbe.controller");
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Roll", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("ActionSpeed", AnimatorControllerParameterType.Float);
        var parameters = controller.parameters;
        parameters.First(p => p.name == "ActionSpeed").defaultFloat = 1f;
        controller.parameters = parameters;
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        var idle = machine.AddState("Idle_Battle");
        idle.motion = Clip("Idle_Battle");
        machine.defaultState = idle;
        var run = machine.AddState("RunForwardBattle");
        run.motion = Clip("RunForwardBattle");
        var toRun = idle.AddTransition(run);
        toRun.hasExitTime = false;
        toRun.duration = 0.1f;
        toRun.hasFixedDuration = true;
        toRun.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        var toIdle = run.AddTransition(idle);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.1f;
        toIdle.hasFixedDuration = true;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        foreach (string name in new[] { "RollForward", "DashForward" })
        {
            var state = machine.AddState(name);
            state.motion = Clip(name);
            state.speedParameter = "ActionSpeed";
            state.speedParameterActive = true;
            var start = machine.AddAnyStateTransition(state);
            start.hasExitTime = false;
            start.hasFixedDuration = true;
            start.duration = 0.05f;
            start.canTransitionToSelf = false;
            start.AddCondition(AnimatorConditionMode.If, 0, name == "RollForward" ? "Roll" : "Dash");
            var end = state.AddTransition(idle);
            end.hasExitTime = true;
            end.exitTime = 1f;
            end.hasFixedDuration = true;
            end.duration = 0.05f;
        }
        var player = GameObject.Find("Player");
        player.GetComponent<Animator>().runtimeAnimatorController = controller;
        var capsule = player.AddComponent<CharacterController>();
        capsule.height = 1.6f;
        capsule.center = new Vector3(0, 0.8f, 0);
        capsule.radius = 0.3f;
        var move = player.AddComponent<PlayerMove>();
        var serialized = new SerializedObject(move);
        serialized.FindProperty("_rollClip").objectReferenceValue = Clip("RollForward");
        serialized.FindProperty("_dashClip").objectReferenceValue = Clip("DashForward");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }
}
