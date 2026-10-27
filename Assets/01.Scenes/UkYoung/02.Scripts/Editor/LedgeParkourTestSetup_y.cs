using System;
using JuicyFresh3D.Parkour;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JuicyFresh3D.Editor
{
    public static class LedgeParkourTestSetup_y
    {
        private const string ScenePath = "Assets/01.Scenes/UkYoung/01.Scenes/UkYoungScene.unity";
        private const string TestRootName = "Sprint Wall Climb Test Setup_y";
        private const string SetupVersionMarkerName = "Ledge Test Setup Version 5_y";
        private const string WallName = "Wall Climb Test";
        private static readonly Vector3 WallPosition = new(-38f, 0f, 76f);
        private static readonly Vector3 WallScale = new(4f, 1.85f, 5f);
        private static readonly Vector3 RunStartPosition = new(-27f, 0.04f, 76f);

        [InitializeOnLoadMethod]
        private static void ScheduleSetup()
        {
            EditorApplication.delayCall += SetupActiveSceneIfNeeded;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += SetupActiveSceneIfNeeded;
            }
        }

        [MenuItem("Tools/JuicyFresh3D/Setup Sprint Wall Climb Test")]
        public static void SetupSprintWallClimbTest()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("PF_Player");
            GameObject wall = GameObject.Find(WallName);
            if (player == null || wall == null)
            {
                throw new InvalidOperationException("PF_Player or Wall Climb Test is missing from UkYoungScene.");
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            ParkourMotor motor = player.GetComponent<ParkourMotor>();
            Collider wallCollider = wall.GetComponentInChildren<Collider>();
            if (controller == null || motor == null || wallCollider == null)
            {
                throw new InvalidOperationException("The wall climb test requires ParkourMotor, CharacterController, and a wall collider.");
            }

            wall.transform.SetPositionAndRotation(WallPosition, Quaternion.identity);
            wall.transform.localScale = WallScale;
            GameObject tallHangingWall = CreateOrUpdateMarkers();
            Physics.SyncTransforms();

            Vector3 originalPosition = player.transform.position;
            Quaternion originalRotation = player.transform.rotation;
            Bounds wallBounds = wallCollider.bounds;
            Vector3 testPosition = new(wallBounds.max.x + 1.7f, 0.04f, wallBounds.center.z);
            player.transform.SetPositionAndRotation(testPosition, Quaternion.LookRotation(Vector3.left));
            Physics.SyncTransforms();

            SerializedObject serializedMotor = new(motor);
            float detectionDistance = serializedMotor.FindProperty("_wallClimbDetectionDistance").floatValue;
            float minimumHeight = serializedMotor.FindProperty("_minimumWallClimbHeight").floatValue;
            float maximumHeight = serializedMotor.FindProperty("_maximumWallClimbHeight").floatValue;
            ParkourLedgeDetector_y detector = new(
                player.transform,
                controller,
                detectionDistance,
                minimumHeight,
                maximumHeight);

            bool sprintClimbDetected = detector.TryFindSprintWallClimbTarget(
                Vector3.left,
                out ParkourLedgeTarget_y sprintClimbTarget);
            if (!sprintClimbDetected)
            {
                player.transform.SetPositionAndRotation(originalPosition, originalRotation);
                Physics.SyncTransforms();
                throw new InvalidOperationException(
                    $"Sprint wall climb physics validation failed. " +
                    $"wallBounds={wallBounds}, testPosition={testPosition}, " +
                    $"heightRange={minimumHeight:F2}-{maximumHeight:F2}, detectionDistance={detectionDistance:F2}");
            }

            Vector3 hangingTestPosition = sprintClimbTarget.HangRootPosition
                                          - Vector3.left * controller.radius
                                          + Vector3.up * (controller.height * 0.25f);
            player.transform.SetPositionAndRotation(hangingTestPosition, Quaternion.LookRotation(Vector3.left));
            Physics.SyncTransforms();
            bool hangingDetected = detector.TryFindHangingTarget(
                Vector3.left,
                out ParkourLedgeTarget_y hangingTarget);
            float edgeError = Mathf.Abs(hangingTarget.TopPoint.x - wallBounds.max.x);
            if (!hangingDetected || edgeError > 0.005f)
            {
                player.transform.SetPositionAndRotation(originalPosition, originalRotation);
                Physics.SyncTransforms();
                throw new InvalidOperationException(
                    $"Hanging physics validation failed. detected={hangingDetected}, " +
                    $"edgeError={edgeError:F4}, wallEdge={wallBounds.max.x:F3}, " +
                    $"targetEdge={hangingTarget.TopPoint.x:F3}, testPosition={hangingTestPosition}");
            }

            float contactHeightError = Mathf.Abs(
                hangingTarget.HangEntryRootPosition.y + 1.865f - hangingTarget.TopPoint.y);
            float leftHandHeightError = Mathf.Abs(
                hangingTarget.LeftHandContactPoint.y - hangingTarget.TopPoint.y);
            float rightHandHeightError = Mathf.Abs(
                hangingTarget.RightHandContactPoint.y - hangingTarget.TopPoint.y);
            float handSpan = Vector3.Distance(
                hangingTarget.LeftHandContactPoint,
                hangingTarget.RightHandContactPoint);
            float bodyClearance = Vector3.Dot(
                hangingTarget.HangRootPosition - hangingTarget.TopPoint,
                hangingTarget.WallNormal);
            bool leftHopDetected = detector.TryFindHangHopTarget(
                hangingTarget,
                -1.5f,
                out ParkourLedgeTarget_y leftHopTarget);
            bool rightHopDetected = detector.TryFindHangHopTarget(
                hangingTarget,
                1.5f,
                out ParkourLedgeTarget_y rightHopTarget);
            float leftHopDistance = Vector3.Distance(
                hangingTarget.HangRootPosition,
                leftHopTarget.HangRootPosition);
            float rightHopDistance = Vector3.Distance(
                hangingTarget.HangRootPosition,
                rightHopTarget.HangRootPosition);
            float requiredClearance = controller.radius + controller.skinWidth;
            if (contactHeightError > 0.005f
                || leftHandHeightError > 0.005f
                || rightHandHeightError > 0.005f
                || Mathf.Abs(handSpan - 0.446f) > 0.01f
                || bodyClearance + 0.005f < requiredClearance
                || !leftHopDetected
                || !rightHopDetected
                || leftHopDistance < 1.49f
                || rightHopDistance < 1.49f)
            {
                player.transform.SetPositionAndRotation(originalPosition, originalRotation);
                Physics.SyncTransforms();
                throw new InvalidOperationException(
                    $"Ledge contact validation failed. contactHeightError={contactHeightError:F4}, " +
                    $"leftHandHeightError={leftHandHeightError:F4}, " +
                    $"rightHandHeightError={rightHandHeightError:F4}, handSpan={handSpan:F3}, " +
                    $"bodyClearance={bodyClearance:F3}, requiredClearance={requiredClearance:F3}, " +
                    $"leftHopDetected={leftHopDetected}, leftHopDistance={leftHopDistance:F3}, " +
                    $"rightHopDetected={rightHopDetected}, rightHopDistance={rightHopDistance:F3}");
            }

            Collider tallWallCollider = tallHangingWall.GetComponent<Collider>();
            Bounds tallWallBounds = tallWallCollider.bounds;
            float hangingRootHeight = tallWallBounds.max.y - 1.664f;
            Vector3 tallWallTestPosition = new(
                tallWallBounds.max.x + controller.radius * 2f + controller.skinWidth,
                hangingRootHeight + controller.height * 0.25f,
                tallWallBounds.center.z);
            player.transform.SetPositionAndRotation(
                tallWallTestPosition,
                Quaternion.LookRotation(Vector3.left));
            Physics.SyncTransforms();
            bool tallWallHangingDetected = detector.TryFindHangingTarget(
                Vector3.left,
                out ParkourLedgeTarget_y tallWallTarget);
            if (!tallWallHangingDetected || tallWallTarget.HasLandingPosition)
            {
                player.transform.SetPositionAndRotation(originalPosition, originalRotation);
                Physics.SyncTransforms();
                throw new InvalidOperationException(
                    $"Tall thin wall hanging validation failed. detected={tallWallHangingDetected}, " +
                    $"hasLanding={tallWallTarget.HasLandingPosition}, bounds={tallWallBounds}, " +
                    $"testPosition={tallWallTestPosition}");
            }

            player.transform.SetPositionAndRotation(RunStartPosition, Quaternion.LookRotation(Vector3.left));
            Physics.SyncTransforms();
            EditorUtility.SetDirty(player.transform);
            EditorUtility.SetDirty(wall.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Scene could not be saved: {ScenePath}");
            }

            Debug.Log(
                $"Sprint wall climb test passed and scene saved. " +
                $"wallHeight={sprintClimbTarget.WallHeight:F3}, " +
                $"frontDistance={sprintClimbTarget.FrontDistance:F3}, " +
                $"forwardDistance={sprintClimbTarget.TotalForwardDistance:F3}, " +
                $"landing={sprintClimbTarget.LandingRootPosition}, " +
                $"hangingDetected={hangingDetected}, handEdgeError={edgeError:F4}, " +
                $"contactHeightError={contactHeightError:F4}, handSpan={handSpan:F3}, " +
                $"bodyClearance={bodyClearance:F3}, " +
                $"leftHopDistance={leftHopDistance:F3}, rightHopDistance={rightHopDistance:F3}, " +
                $"tallThinWallHangingDetected={tallWallHangingDetected}, " +
                $"tallThinWallHasLanding={tallWallTarget.HasLandingPosition}");
        }

        private static void SetupActiveSceneIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath
                || GameObject.Find("Urban Parkour Playground") != null
                || GameObject.Find(SetupVersionMarkerName) != null)
            {
                return;
            }

            try
            {
                SetupSprintWallClimbTest();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Sprint wall climb test setup failed: {exception}");
            }
        }

        private static GameObject CreateOrUpdateMarkers()
        {
            GameObject root = GameObject.Find(TestRootName);
            if (root == null)
            {
                root = new GameObject(TestRootName);
            }

            Material runwayMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/01.Scenes/UkYoung/09.Materials/Parkour/M_UrbanPrimaryRoute.mat");
            Material triggerMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/01.Scenes/UkYoung/09.Materials/Parkour/M_UrbanTechnicalRoute.mat");

            CreateOrUpdateMarker(
                root.transform,
                "Wall Climb Runway",
                new Vector3(-30.5f, 0.055f, 76f),
                new Vector3(7f, 0.025f, 0.45f),
                runwayMaterial);
            CreateOrUpdateMarker(
                root.transform,
                "Jump Input Marker",
                new Vector3(-34.3f, 0.06f, 76f),
                new Vector3(0.08f, 0.035f, 5f),
                triggerMaterial);

            GameObject tallHangingWall = CreateOrUpdateSolidBlock(
                root.transform,
                "Tall Thin Hanging Wall",
                new Vector3(-38f, 1.125f, 68f),
                new Vector3(0.16f, 2.25f, 5f),
                runwayMaterial);
            CreateOrUpdateMarker(
                root.transform,
                "Tall Wall Runway",
                new Vector3(-30.5f, 0.055f, 68f),
                new Vector3(7f, 0.025f, 0.45f),
                runwayMaterial);

            Transform versionMarker = root.transform.Find(SetupVersionMarkerName);
            if (versionMarker == null)
            {
                GameObject marker = new(SetupVersionMarkerName);
                marker.transform.SetParent(root.transform);
            }

            return tallHangingWall;
        }

        private static GameObject CreateOrUpdateSolidBlock(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            Transform existing = parent.Find(name);
            GameObject block = existing != null
                ? existing.gameObject
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.SetPositionAndRotation(position, Quaternion.identity);
            block.transform.localScale = scale;
            Renderer renderer = block.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            if (block.GetComponent<Collider>() == null)
            {
                block.AddComponent<BoxCollider>();
            }

            return block;
        }

        private static void CreateOrUpdateMarker(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            Transform existing = parent.Find(name);
            GameObject marker = existing != null
                ? existing.gameObject
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = name;
            marker.transform.SetParent(parent);
            marker.transform.SetPositionAndRotation(position, Quaternion.identity);
            marker.transform.localScale = scale;
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }
    }
}
