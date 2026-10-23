using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ActualBossPlayProbe
{
    static ActualBossPlayProbe()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("ActualBossProbeRunning", false))
            {
                SessionState.SetBool("ActualBossProbeRunning", false);
                EditorApplication.Exit(0);
            }
        };
    }

    public static void Run()
    {
        string mode = File.ReadAllText(Path.Combine(Application.dataPath, "../actual-boss-probe-mode.txt")).Trim();
        if (mode == "after")
        {
            const string path = "Assets/05.Models/KwangHee/BossMonster.prefab";
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            foreach (Rigidbody body in prefab.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (collider.attachedRigidbody != null && collider.attachedRigidbody.transform != prefab.transform)
                {
                    collider.enabled = false;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            PrefabUtility.UnloadPrefabContents(prefab);
            AssetDatabase.SaveAssets();
        }
        EditorSceneManager.OpenScene("Assets/01.Scenes/KwangHeeScene.unity");
        if (mode == "player-attack")
        {
            PlayerAttackSetup.Prepare(Path.Combine(Application.dataPath, "../../player-attack-clip-report.txt"));
        }
        if (mode == "player-move")
        {
            PlayerMoveProbeSetup.Prepare();
        }
        if (mode == "player-grounding" || mode == "player-eight-way" || mode == "player-attack" || mode == "player-combo45-analysis")
        {
            foreach (BossCombatController boss in Object.FindObjectsByType<BossCombatController>(FindObjectsSortMode.None))
            {
                boss.gameObject.SetActive(false);
            }
        }
        SessionState.SetBool("ActualBossProbeRunning", true);
        EditorApplication.EnterPlaymode();
    }
}
