using System.IO;
using System.Linq;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerEightWaySetup
{
    [InitializeOnLoadMethod]
    private static void QueueSetup()
    {
        EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        string workspace = Directory.GetParent(Application.dataPath).FullName;
        string request = Path.Combine(workspace, "tmp/player-eight-way-setup.request");
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        var player = Object.FindFirstObjectByType<PlayerMove>();
        Camera mainCamera = Camera.main;
        if (player == null || mainCamera == null
            || player.gameObject.scene.path != "Assets/01.Scenes/KwangHeeScene.unity")
        {
            Debug.LogError("PlayerEightWaySetup: 플레이어 씬과 Main Camera를 확인하세요.");
            return;
        }

        Transform target = player.transform.Find("CameraTarget");
        if (target == null)
        {
            var targetObject = new GameObject("CameraTarget");
            Undo.RegisterCreatedObjectUndo(targetObject, "Create player camera target");
            target = targetObject.transform;
            target.SetParent(player.transform, false);
            target.localPosition = Vector3.up;
        }

        var brain = mainCamera.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            brain = Undo.AddComponent<CinemachineBrain>(mainCamera.gameObject);
        }
        Undo.RecordObject(brain, "Configure player camera brain");
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
        brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;

        var camera = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.Follow == target || c.Follow == player.transform);
        if (camera == null)
        {
            var cameraObject = new GameObject("PlayerFollowCamera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create player follow camera");
            camera = Undo.AddComponent<CinemachineCamera>(cameraObject);
        }
        Undo.RecordObject(camera, "Configure player follow camera");
        var targets = camera.Target;
        targets.TrackingTarget = target;
        targets.CustomLookAtTarget = false;
        camera.Target = targets;
        camera.Priority = 10;
        var lens = camera.Lens;
        lens.FieldOfView = 55f;
        lens.NearClipPlane = 0.1f;
        camera.Lens = lens;

        var follow = camera.GetComponent<CinemachineFollow>();
        if (follow == null)
        {
            follow = Undo.AddComponent<CinemachineFollow>(camera.gameObject);
        }
        Undo.RecordObject(follow, "Configure camera tracking space");
        var tracker = follow.TrackerSettings;
        tracker.BindingMode = BindingMode.LockToTargetOnAssign;
        tracker.PositionDamping = Vector3.zero;
        tracker.RotationDamping = Vector3.zero;
        tracker.QuaternionDamping = 0f;
        follow.TrackerSettings = tracker;
        follow.FollowOffset = new Vector3(0f, 1f, -4f);

        var composer = camera.GetComponent<CinemachineRotationComposer>();
        if (composer == null)
        {
            composer = Undo.AddComponent<CinemachineRotationComposer>(camera.gameObject);
        }
        Undo.RecordObject(composer, "Configure camera framing");
        composer.Damping = Vector2.zero;
        composer.TargetOffset = Vector3.zero;
        composer.Lookahead = new LookaheadSettings();
        var composition = composer.Composition;
        composition.ScreenPosition = Vector2.zero;
        composition.DeadZone.Enabled = false;
        composer.Composition = composition;

        Undo.RecordObject(player, "Assign player movement camera");
        var serializedPlayer = new SerializedObject(player);
        serializedPlayer.FindProperty("_cameraTransform").objectReferenceValue = mainCamera.transform;
        serializedPlayer.FindProperty("_rotationSmoothTime").floatValue = 0.1f;
        serializedPlayer.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);

        Vector3 initialOffset = Quaternion.Euler(0, player.transform.eulerAngles.y, 0) * follow.FollowOffset;
        Undo.RecordObject(mainCamera.transform, "Set initial third person framing");
        mainCamera.transform.position = target.position + initialOffset;
        mainCamera.transform.rotation = Quaternion.LookRotation(target.position - mainCamera.transform.position, Vector3.up);
        camera.PreviousStateIsValid = false;
        camera.UpdateCameraState(Vector3.up, -1f);

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        File.WriteAllText(Path.Combine(workspace, "tmp/player-eight-way-setup.txt"),
            "Scene=" + player.gameObject.scene.path + "\nCamera=" + camera.name
            + "\nTarget=" + target.name + "\nBinding=" + follow.TrackerSettings.BindingMode
            + "\nOffset=" + follow.FollowOffset + "\nBrain=" + brain.UpdateMethod
            + "\nMain camera=" + mainCamera.transform.position + "; yaw=" + mainCamera.transform.eulerAngles.y);
        File.Delete(request);
        Debug.Log("카메라 기준 8방향 이동 및 Cinemachine 카메라 연결 완료");
    }
}
