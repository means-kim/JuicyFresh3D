using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuicyFresh3D.Parkour;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JuicyFresh3D.Editor
{
    public static class ParkourValidation
    {
        private const string ScenePath = "Assets/01.Scenes/UkYoung/01.Scenes/UkYoungScene.unity";
        private const string AnimationFolder = "Assets/01.Scenes/UkYoung/06.Animations";

        [MenuItem("Tools/JuicyFresh3D/Validate Parkour Scene")]
        public static void ValidateParkourScene()
        {
            List<string> failures = new();
            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException("Parkour scene was not found.", ScenePath);
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("PF_Player");
            GameObject mainCamera = GameObject.Find("Main Camera");
            GameObject course = GameObject.Find("Parkour Course");

            Require(player != null, "PF_Player is missing.", failures);
            Require(mainCamera != null, "Main Camera is missing.", failures);
            Require(course != null, "Parkour Course is missing.", failures);

            if (player != null)
            {
                Require(player.GetComponent<CharacterController>() != null, "CharacterController is missing.", failures);
                Require(player.GetComponent<ParkourInput>() != null, "ParkourInput is missing.", failures);
                Require(player.GetComponent<ParkourMotor>() != null, "ParkourMotor is missing.", failures);
                Require(player.GetComponent<ParkourAnimationDriver>() != null, "ParkourAnimationDriver is missing.", failures);

                Animator animator = player.GetComponentInChildren<Animator>();
                Require(animator != null, "Animator is missing.", failures);
                if (animator != null)
                {
                    Require(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman,
                        "Humanoid avatar is invalid.", failures);
                    Require(animator.runtimeAnimatorController != null, "AnimatorController is missing.", failures);
                    if (animator.runtimeAnimatorController is AnimatorController controller)
                    {
                        int stateCount = controller.layers[0].stateMachine.states.Length;
                        Require(stateCount >= 5, $"Animator state count is too low: {stateCount}", failures);
                    }
                }

                Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers.Skip(1))
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }

                    Require(bounds.size.y > 0.8f && bounds.size.y < 3.2f,
                        $"Character height is outside the expected range: {bounds.size.y:F2}", failures);
                    Debug.Log($"Parkour character bounds: {bounds.size}");
                }
                else
                {
                    failures.Add("Character renderer is missing.");
                }
            }

            if (mainCamera != null)
            {
                Require(mainCamera.GetComponent<Camera>() != null, "Camera component is missing.", failures);
                Require(mainCamera.GetComponent<ParkourCamera>() != null, "ParkourCamera is missing.", failures);
            }

            if (course != null)
            {
                Transform curvedWall = course.transform.Find("Curved Wall Run");
                Require(curvedWall != null, "Curved wall course is missing.", failures);
                if (curvedWall != null)
                {
                    Require(curvedWall.childCount == 12,
                        $"Curved wall segment count is {curvedWall.childCount}, expected 12.", failures);
                }
            }

            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                int missingScriptCount = CountMissingScripts(root);
                Require(missingScriptCount == 0,
                    $"Missing script count under {root.name}: {missingScriptCount}", failures);
            }

            ReportAnimationClips();

            if (failures.Count > 0)
            {
                throw new InvalidOperationException("Parkour validation failed:\n" + string.Join("\n", failures));
            }

            Debug.Log("Parkour validation passed.");
        }

        private static int CountMissingScripts(GameObject gameObject)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            foreach (Transform child in gameObject.transform)
            {
                count += CountMissingScripts(child.gameObject);
            }

            return count;
        }

        private static void ReportAnimationClips()
        {
            foreach (string path in AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimationFolder })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Distinct()
                         .OrderBy(path => path))
            {
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(animation => !animation.name.StartsWith("__preview__"));
                if (clip != null)
                {
                    Debug.Log($"Parkour clip: {Path.GetFileName(path)}, {clip.length:F2}s, loop={clip.isLooping}");
                }
            }
        }

        private static void Require(bool condition, string message, ICollection<string> failures)
        {
            if (!condition)
            {
                failures.Add(message);
            }
        }
    }
}
