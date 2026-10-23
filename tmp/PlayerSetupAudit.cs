using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerSetupAudit
{
    [InitializeOnLoadMethod]
    private static void QueueAudit()
    {
        EditorApplication.delayCall += Capture;
    }

    private static void Capture()
    {
        string workspace = Directory.GetParent(Application.dataPath).FullName;
        string request = Path.Combine(workspace, "tmp/player-setup-audit.request");
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        var report = new StringBuilder();
        report.AppendLine("Scene=" + EditorSceneManager.GetActiveScene().path);
        GameObject player = GameObject.Find("Player");
        if (player != null && File.ReadAllText(request).Trim() == "fix")
        {
            FixPlayer(player);
        }
        if (player == null)
        {
            report.AppendLine("Player not found");
        }
        else
        {
            report.AppendLine("Player position=" + player.transform.position.ToString("F4")
                + " rotation=" + player.transform.eulerAngles.ToString("F4")
                + " scale=" + player.transform.lossyScale.ToString("F4"));
            report.AppendLine("Components=" + string.Join(", ", player.GetComponents<Component>().Select(c => c == null ? "Missing" : c.GetType().FullName)));
            foreach (CharacterController capsule in player.GetComponentsInChildren<CharacterController>(true))
            {
                report.AppendLine("CharacterController " + capsule.name + " center=" + capsule.center.ToString("F4")
                    + " height=" + capsule.height + " radius=" + capsule.radius + " skin=" + capsule.skinWidth
                    + " step=" + capsule.stepOffset + " worldBottom="
                    + capsule.transform.TransformPoint(capsule.center - Vector3.up * capsule.height * 0.5f).ToString("F4"));
            }
            foreach (Animator animator in player.GetComponentsInChildren<Animator>(true))
            {
                report.AppendLine("Animator " + animator.name + " controller=" + AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)
                    + " rootMotion=" + animator.applyRootMotion + " update=" + animator.updateMode);
                report.AppendLine("Parameters=" + string.Join(", ", animator.parameters.Select(p => p.name + ":" + p.type)));
            }
            report.AppendLine("IsMoving hash=" + Animator.StringToHash("IsMoving"));
            foreach (Transform bone in player.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("foot") || t.name.Contains("Foot")))
            {
                report.AppendLine("Bone " + bone.name + " world=" + bone.position.ToString("F4"));
            }
            foreach (SkinnedMeshRenderer renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                report.AppendLine("Mesh " + renderer.name + " bounds=" + renderer.bounds);
            }
        }
        File.WriteAllText(Path.Combine(workspace, "tmp/player-setup-audit.txt"), report.ToString());
        File.Delete(request);
        Debug.Log("Player setup audit saved to tmp/player-setup-audit.txt");
    }

    private static AnimationClip LoadClip(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath("Assets/ModularAnimalKnightsPolyart/Animation/" + name + ".fbx")
            .OfType<AnimationClip>().First(clip => clip.name == name);
    }

    private static void FixPlayer(GameObject player)
    {
        const string controllerPath = "Assets/07.Animations/KwnagHee/PlayerMoveAnimator.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Roll", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ActionSpeed", AnimatorControllerParameterType.Float);
            var parameters = controller.parameters;
            parameters.First(p => p.name == "ActionSpeed").defaultFloat = 1f;
            controller.parameters = parameters;
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle_Battle", new Vector3(260, 120, 0));
            idle.motion = LoadClip("Idle_Battle");
            machine.defaultState = idle;
            var run = machine.AddState("RunForwardBattle", new Vector3(540, 120, 0));
            run.motion = LoadClip("RunForwardBattle");
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
                var state = machine.AddState(name, new Vector3(name == "RollForward" ? 260 : 540, 330, 0));
                state.motion = LoadClip(name);
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
        }

        var capsule = player.GetComponent<CharacterController>();
        var animator = player.GetComponent<Animator>();
        var movement = player.GetComponent<PlayerMove>();
        Undo.RecordObjects(new Object[] { capsule, animator, movement }, "Fix Player Grounding and Animator");
        capsule.height = 1.5f;
        capsule.radius = 0.3f;
        capsule.center = new Vector3(0, 0.75f, 0);
        capsule.skinWidth = 0.03f;
        capsule.stepOffset = 0.25f;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        var serialized = new SerializedObject(movement);
        serialized.FindProperty("_rollClip").objectReferenceValue = LoadClip("RollForward");
        serialized.FindProperty("_dashClip").objectReferenceValue = LoadClip("DashForward");
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        PrefabUtility.RecordPrefabInstancePropertyModifications(movement);
        EditorSceneManager.MarkSceneDirty(player.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(player.scene);
    }
}
