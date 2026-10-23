using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerAttackSetup
{
    [InitializeOnLoadMethod]
    private static void QueueSetup()
    {
        EditorApplication.delayCall += ApplyRequest;
    }

    private static void ApplyRequest()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string request = Path.Combine(root, "tmp/player-attack-setup.request");
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Prepare(Path.Combine(root, "tmp/player-attack-setup.txt"));
        File.Delete(request);
    }

    public static void Prepare(string reportPath)
    {
        PlayerMove player = Object.FindFirstObjectByType<PlayerMove>();
        if (player == null || player.gameObject.scene.path != "Assets/01.Scenes/KwangHeeScene.unity")
        {
            throw new System.Exception("Open KwangHeeScene before player attack setup.");
        }

        Animator animator = player.GetComponent<Animator>();
        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null || AssetDatabase.GetAssetPath(controller)
            != "Assets/07.Animations/KwnagHee/PlayerMoveAnimator.controller")
        {
            throw new System.Exception("Unexpected player Animator controller.");
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimationClip[] clips = new AnimationClip[5];
        StringBuilder report = new StringBuilder();
        Undo.RecordObject(controller, "Add player combo animation states");
        for (int index = 0; index < 5; index++)
        {
            string name = "AttackCombo" + (index + 1).ToString("00");
            string path = "Assets/ModularAnimalKnightsPolyart/Animation/" + name + ".fbx";
            clips[index] = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .First(c => c.name == name);
            AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
            if (state == null)
            {
                state = machine.AddState(name, new Vector3(250 + index % 3 * 260, 540 + index / 3 * 120, 0));
            }
            state.motion = clips[index];
            state.speed = 1f;
            state.speedParameterActive = false;
            state.writeDefaultValues = true;
            // PlayerAttack checks normalizedTime and returns to Idle after the entire clip.
            report.AppendLine($"{name}: length={clips[index].length:F6}s; fps={clips[index].frameRate:F2}; loop={clips[index].isLooping}; transitions={state.transitions.Length}");
        }

        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        bool added = attack == null;
        if (added)
        {
            attack = Undo.AddComponent<PlayerAttack>(player.gameObject);
        }
        SerializedObject serialized = new SerializedObject(attack);
        SerializedProperty clipList = serialized.FindProperty("_comboClips");
        clipList.arraySize = 5;
        float rollDuration = new SerializedObject(player).FindProperty("_rollDuration").floatValue;
        for (int index = 0; index < 5; index++)
        {
            clipList.GetArrayElementAtIndex(index).objectReferenceValue = clips[index];
            SerializedProperty delay = serialized.FindProperty("_comboDelay" + (index + 1).ToString("00"));
            if (added)
            {
                delay.floatValue = Mathf.Ceil((clips[index].length + rollDuration + 1f) * 10f - 0.0001f) / 10f;
            }
            report.AppendLine($"ComboDelay{index + 1:00}={delay.floatValue:F2}s (clip + roll {rollDuration:F2}s + approximately 1s grace)");
        }
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(attack);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, report.ToString());
        Debug.Log("PlayerAttack 01~05 및 단계별 ComboDelay 연결 완료\n" + report);
    }
}
