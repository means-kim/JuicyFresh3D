using System.Linq;
using JuicyFresh3D.Parkour;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace JuicyFresh3D.Editor
{
    public static class ParkourSceneBuilder
    {
        private const string ModelPath = "Assets/01.Scenes/UkYoung/05.Models/Character_model.fbx";
        private const string IdlePath = "Assets/01.Scenes/UkYoung/06.Animations/Idle.fbx";
        private const string Idle2Path = "Assets/01.Scenes/UkYoung/06.Animations/Anim_Idle2.fbx";
        private const string IdleToSprintPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_IdleToSprint.fbx";
        private const string StandardRunPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_StandardRun.fbx";
        private const string SprintPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_Sprint.fbx";
        private const string RunToStopPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_RunToStop.fbx";
        private const string RunningJumpPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_RunningJump.fbx";
        private const string ForwardRollPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_SprintingForwardRoll.fbx";
        private const string JumpingDownPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_JumpingDown.fbx";
        private const string FallingIdlePath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_FallingIdle.fbx";
        private const string FallingToLandPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_FallingToLand.fbx";
        private const string FallingToRollPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_FallingToRoll.fbx";
        private const string WallRunPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_WallRun.fbx";
        private static readonly string[] OptionalAnimationPaths =
        {
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_FastRun.fbx",
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_CrouchedToSprinting.fbx",
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_SprintWallClimb.fbx",
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_ChangedDirection.fbx",
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_RunningRightTurn.fbx",
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_Falling.fbx"
        };
        private const string ControllerFolder = "Assets/01.Scenes/UkYoung/06.Animations/Parkour";
        private const string ControllerPath = ControllerFolder + "/AC_Ctrl_Parkour.controller";
        private const string MaterialFolder = "Assets/01.Scenes/UkYoung/09.Materials/Parkour";
        private const string ScenePath = "Assets/01.Scenes/UkYoung/01.Scenes/UkYoungScene.unity";

        [MenuItem("Tools/JuicyFresh3D/Build Parkour Scene")]
        public static void BuildParkourScene()
        {
            EnsureFolder("Assets/01.Scenes/UkYoung/02.Scripts", "Editor");
            EnsureFolder("Assets/01.Scenes/UkYoung/06.Animations", "Parkour");
            EnsureFolder("Assets/01.Scenes/UkYoung/09.Materials", "Parkour");

            AnimatorController controller = UpdateParkourAnimations();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLighting();
            CreateCourse();
            CreatePlayer(controller);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Parkour scene created: {ScenePath}");
        }

        [MenuItem("Tools/JuicyFresh3D/Update Parkour Animations")]
        public static AnimatorController UpdateParkourAnimations()
        {
            EnsureFolder("Assets/01.Scenes/UkYoung/06.Animations", "Parkour");

            Avatar avatar = ConfigureCharacterModel();
            AnimationClip idleClip = ConfigureAnimation(IdlePath, avatar, true);
            AnimationClip idleToSprintClip = ConfigureAnimation(IdleToSprintPath, avatar, false);
            AnimationClip standardRunClip = ConfigureAnimation(StandardRunPath, avatar, true);
            AnimationClip sprintClip = ConfigureAnimation(SprintPath, avatar, true);
            AnimationClip runningJumpClip = ConfigureAnimation(RunningJumpPath, avatar, false);
            AnimationClip forwardRollClip = ConfigureAnimation(ForwardRollPath, avatar, false);
            AnimationClip jumpingDownClip = ConfigureAnimation(JumpingDownPath, avatar, false);
            AnimationClip fallingIdleClip = ConfigureAnimation(FallingIdlePath, avatar, true);
            AnimationClip fallingToLandClip = ConfigureAnimation(FallingToLandPath, avatar, false);
            AnimationClip fallingToRollClip = ConfigureAnimation(FallingToRollPath, avatar, false);
            AnimationClip wallRunClip = ConfigureAnimation(WallRunPath, avatar, true);

            foreach (string path in OptionalAnimationPaths)
            {
                if (AssetImporter.GetAtPath(path) != null)
                {
                    ConfigureAnimation(path, avatar, path.Contains("FastRun"));
                }
            }

            AnimatorController controller = CreateAnimatorController(
                idleClip,
                idleToSprintClip,
                standardRunClip,
                sprintClip,
                runningJumpClip,
                forwardRollClip,
                jumpingDownClip,
                fallingIdleClip,
                fallingToLandClip,
                fallingToRollClip,
                wallRunClip);
            AssetDatabase.SaveAssets();
            Debug.Log("Parkour animations updated.");
            return controller;
        }

        [MenuItem("Tools/JuicyFresh3D/Repair Run To Stop Import")]
        public static void RepairRunToStopImport()
        {
            Avatar avatar = ConfigureCharacterModel();
            ConfigureAnimation(RunToStopPath, avatar, false, true);
            AssetDatabase.SaveAssets();
            Debug.Log("Run To Stop was reimported with a playable clip and baked root transforms.");
        }

        [MenuItem("Tools/JuicyFresh3D/Use Idle2")]
        public static void UseIdle2()
        {
            Avatar avatar = ConfigureCharacterModel();
            AnimationClip idle2Clip = ConfigureAnimation(Idle2Path, avatar, true);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                throw new System.IO.FileNotFoundException("Parkour Animator Controller was not found.", ControllerPath);
            }

            AnimatorState idleState = controller.layers[0].stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(state => state.name == "Idle");
            if (idleState == null)
            {
                throw new System.InvalidOperationException("Idle state was not found in the Parkour Animator Controller.");
            }

            idleState.motion = idle2Clip;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Idle state motion was replaced with Anim_Idle2.");
        }

        private static Avatar ConfigureCharacterModel()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                throw new System.IO.FileNotFoundException("Character model was not found.", ModelPath);
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                throw new System.InvalidOperationException("Character model could not create a valid Humanoid avatar.");
            }

            return avatar;
        }

        private static AnimationClip ConfigureAnimation(
            string path,
            Avatar avatar,
            bool loop,
            bool bakeRootHeight = false)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                throw new System.IO.FileNotFoundException("Animation was not found.", path);
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.SaveAndReimport();

            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips.Length == 0)
            {
                throw new System.InvalidOperationException($"Animation take could not be loaded: {path}");
            }

            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clip.lockRootRotation = bakeRootHeight;
                clip.lockRootHeightY = bakeRootHeight;
                clip.lockRootPositionXZ = false;
                clip.heightFromFeet = bakeRootHeight;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            AnimationClip animationClip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
            if (animationClip == null)
            {
                throw new System.InvalidOperationException($"Animation clip could not be loaded: {path}");
            }

            return animationClip;
        }

        private static AnimatorController CreateAnimatorController(
            AnimationClip idleClip,
            AnimationClip idleToSprintClip,
            AnimationClip standardRunClip,
            AnimationClip sprintClip,
            AnimationClip runningJumpClip,
            AnimationClip forwardRollClip,
            AnimationClip jumpingDownClip,
            AnimationClip fallingIdleClip,
            AnimationClip fallingToLandClip,
            AnimationClip fallingToRollClip,
            AnimationClip wallRunClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "VerticalSpeed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "WallRunning", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Sprinting", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Roll", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "HardLand", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState childState in stateMachine.states.ToArray())
            {
                stateMachine.RemoveState(childState.state);
            }

            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }

            AnimatorState idleState = AddState(stateMachine, "Idle", idleClip, new Vector3(180f, 40f));
            AnimatorState runState = AddState(stateMachine, "Standard Run", standardRunClip, new Vector3(420f, 40f));
            AnimatorState sprintStartState = AddState(stateMachine, "Idle To Sprint", idleToSprintClip, new Vector3(420f, 140f));
            AnimatorState sprintState = AddState(stateMachine, "Sprint", sprintClip, new Vector3(680f, 140f));
            AnimatorState jumpState = AddState(stateMachine, "Running Jump", runningJumpClip, new Vector3(680f, -80f));
            AnimatorState rollState = AddState(stateMachine, "Forward Roll", forwardRollClip, new Vector3(680f, 260f));
            AnimatorState fallingState = AddState(stateMachine, "Jumping Down", jumpingDownClip, new Vector3(940f, -80f));
            AnimatorState fallingIdleState = AddState(stateMachine, "Falling Idle", fallingIdleClip, new Vector3(1180f, -80f));
            AnimatorState landState = AddState(stateMachine, "Landing", fallingToLandClip, new Vector3(1180f, 40f));
            AnimatorState hardLandState = AddState(stateMachine, "Landing Roll", fallingToRollClip, new Vector3(1180f, 260f));
            AnimatorState wallRunState = AddState(stateMachine, "Wall Run", wallRunClip, new Vector3(940f, 380f));
            rollState.speed = 1.1f;
            hardLandState.speed = 1.15f;
            stateMachine.defaultState = idleState;

            AnimatorStateTransition jumpFromIdle = idleState.AddTransition(jumpState);
            jumpFromIdle.hasExitTime = false;
            jumpFromIdle.duration = 0.04f;
            jumpFromIdle.canTransitionToSelf = false;
            jumpFromIdle.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

            AnimatorStateTransition startRunning = idleState.AddTransition(runState);
            startRunning.hasExitTime = false;
            startRunning.duration = 0.12f;
            startRunning.AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");
            startRunning.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sprinting");

            AnimatorStateTransition startSprintingFromIdle = idleState.AddTransition(sprintStartState);
            startSprintingFromIdle.hasExitTime = false;
            startSprintingFromIdle.duration = 0.06f;
            startSprintingFromIdle.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");
            startSprintingFromIdle.AddCondition(AnimatorConditionMode.If, 0f, "Sprinting");

            AnimatorStateTransition stopRunning = runState.AddTransition(idleState);
            stopRunning.hasExitTime = false;
            stopRunning.duration = 0.16f;
            stopRunning.AddCondition(AnimatorConditionMode.Less, 0.2f, "Speed");

            AnimatorStateTransition startSprinting = runState.AddTransition(sprintStartState);
            startSprinting.hasExitTime = false;
            startSprinting.duration = 0.08f;
            startSprinting.AddCondition(AnimatorConditionMode.If, 0f, "Sprinting");

            AnimatorStateTransition reachFullSpeed = sprintStartState.AddTransition(sprintState);
            reachFullSpeed.hasExitTime = true;
            reachFullSpeed.exitTime = 0.88f;
            reachFullSpeed.duration = 0.08f;

            AnimatorStateTransition stopSprinting = sprintState.AddTransition(runState);
            stopSprinting.hasExitTime = false;
            stopSprinting.duration = 0.14f;
            stopSprinting.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sprinting");

            AnimatorStateTransition cancelStart = sprintStartState.AddTransition(runState);
            cancelStart.hasExitTime = false;
            cancelStart.duration = 0.1f;
            cancelStart.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sprinting");

            AnimatorStateTransition jump = stateMachine.AddAnyStateTransition(jumpState);
            jump.hasExitTime = false;
            jump.duration = 0.06f;
            jump.canTransitionToSelf = false;
            jump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

            AnimatorStateTransition landToRun = jumpState.AddTransition(runState);
            landToRun.hasExitTime = false;
            landToRun.duration = 0.12f;
            landToRun.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            landToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sprinting");

            AnimatorStateTransition landToSprint = jumpState.AddTransition(sprintState);
            landToSprint.hasExitTime = false;
            landToSprint.duration = 0.1f;
            landToSprint.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            landToSprint.AddCondition(AnimatorConditionMode.If, 0f, "Sprinting");

            AnimatorStateTransition roll = stateMachine.AddAnyStateTransition(rollState);
            roll.hasExitTime = false;
            roll.duration = 0.04f;
            roll.canTransitionToSelf = false;
            roll.AddCondition(AnimatorConditionMode.If, 0f, "Roll");

            AnimatorStateTransition finishRoll = rollState.AddTransition(sprintState);
            finishRoll.hasExitTime = true;
            finishRoll.exitTime = 0.88f;
            finishRoll.duration = 0.08f;

            AnimatorStateTransition startFalling = stateMachine.AddAnyStateTransition(fallingState);
            startFalling.hasExitTime = false;
            startFalling.duration = 0.1f;
            startFalling.canTransitionToSelf = false;
            startFalling.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            startFalling.AddCondition(AnimatorConditionMode.IfNot, 0f, "WallRunning");
            startFalling.AddCondition(AnimatorConditionMode.Less, -0.5f, "VerticalSpeed");

            AnimatorStateTransition continueFalling = fallingState.AddTransition(fallingIdleState);
            continueFalling.hasExitTime = true;
            continueFalling.exitTime = 0.8f;
            continueFalling.duration = 0.1f;

            AnimatorStateTransition land = fallingIdleState.AddTransition(landState);
            land.hasExitTime = false;
            land.duration = 0.05f;
            land.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            AnimatorStateTransition earlyLand = fallingState.AddTransition(landState);
            earlyLand.hasExitTime = false;
            earlyLand.duration = 0.05f;
            earlyLand.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            AnimatorStateTransition hardLand = stateMachine.AddAnyStateTransition(hardLandState);
            hardLand.hasExitTime = false;
            hardLand.duration = 0.03f;
            hardLand.canTransitionToSelf = false;
            hardLand.AddCondition(AnimatorConditionMode.If, 0f, "HardLand");

            AddLandingExits(landState, idleState, runState, sprintState);

            AnimatorStateTransition finishHardLand = hardLandState.AddTransition(sprintState);
            finishHardLand.hasExitTime = true;
            finishHardLand.exitTime = 0.88f;
            finishHardLand.duration = 0.08f;

            AnimatorStateTransition startWallRun = stateMachine.AddAnyStateTransition(wallRunState);
            startWallRun.hasExitTime = false;
            startWallRun.duration = 0.08f;
            startWallRun.canTransitionToSelf = false;
            startWallRun.AddCondition(AnimatorConditionMode.If, 0f, "WallRunning");

            AnimatorStateTransition leaveWallAirborne = wallRunState.AddTransition(fallingState);
            leaveWallAirborne.hasExitTime = false;
            leaveWallAirborne.duration = 0.08f;
            leaveWallAirborne.AddCondition(AnimatorConditionMode.IfNot, 0f, "WallRunning");
            leaveWallAirborne.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");

            AnimatorStateTransition leaveWallGrounded = wallRunState.AddTransition(runState);
            leaveWallGrounded.hasExitTime = false;
            leaveWallGrounded.duration = 0.1f;
            leaveWallGrounded.AddCondition(AnimatorConditionMode.IfNot, 0f, "WallRunning");
            leaveWallGrounded.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState AddState(
            AnimatorStateMachine stateMachine,
            string name,
            Motion motion,
            Vector3 position)
        {
            AnimatorState state = stateMachine.AddState(name, position);
            state.motion = motion;
            state.writeDefaultValues = true;
            return state;
        }

        private static void AddLandingExits(
            AnimatorState landingState,
            AnimatorState idleState,
            AnimatorState runState,
            AnimatorState sprintState)
        {
            AnimatorStateTransition toIdle = landingState.AddTransition(idleState);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.78f;
            toIdle.duration = 0.1f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.2f, "Speed");

            AnimatorStateTransition toRun = landingState.AddTransition(runState);
            toRun.hasExitTime = true;
            toRun.exitTime = 0.62f;
            toRun.duration = 0.1f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");
            toRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sprinting");

            AnimatorStateTransition toSprint = landingState.AddTransition(sprintState);
            toSprint.hasExitTime = true;
            toSprint.exitTime = 0.55f;
            toSprint.duration = 0.08f;
            toSprint.AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");
            toSprint.AddCondition(AnimatorConditionMode.If, 0f, "Sprinting");
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            if (controller.parameters.All(parameter => parameter.name != name))
            {
                controller.AddParameter(name, type);
            }
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.color = new Color(1f, 0.94f, 0.84f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.56f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.3f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.1f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.54f, 0.7f, 0.84f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 130f;
        }

        private static void CreateCourse()
        {
            Material groundMaterial = CreateMaterial(
                "M_ParkourGround",
                new Color(0.12f, 0.15f, 0.22f));
            Material routeMaterial = CreateMaterial(
                "M_ParkourRoute",
                new Color(0.12f, 0.65f, 0.95f));
            Material accentMaterial = CreateMaterial(
                "M_ParkourAccent",
                new Color(1f, 0.24f, 0.48f));
            Material distantMaterial = CreateMaterial(
                "M_ParkourDistant",
                new Color(0.3f, 0.38f, 0.5f));

            GameObject course = new("Parkour Course");
            CreateBlock(course.transform, "Start Platform", new Vector3(0f, -0.5f, 4f), new Vector3(14f, 1f, 30f), groundMaterial);
            CreateBlock(course.transform, "Takeoff Ramp", new Vector3(0f, 0.35f, 17.5f), new Vector3(7f, 0.6f, 7f), routeMaterial, new Vector3(-10f, 0f, 0f));
            CreateCurvedWall(course.transform, routeMaterial);
            CreateBlock(course.transform, "Landing Platform", new Vector3(-5f, -0.5f, 37f), new Vector3(16f, 1f, 18f), groundMaterial);
            CreateBlock(course.transform, "Landing Accent", new Vector3(-5f, 0.04f, 31f), new Vector3(7f, 0.08f, 4f), accentMaterial);
            CreateBlock(course.transform, "Flow Ramp", new Vector3(-5f, 1.1f, 46f), new Vector3(8f, 0.8f, 10f), routeMaterial, new Vector3(-13f, 0f, 0f));
            CreateBlock(course.transform, "Upper Platform", new Vector3(-5f, 2.15f, 56f), new Vector3(14f, 1f, 16f), groundMaterial);
            CreateBlock(course.transform, "Exit Wall Left", new Vector3(-11.5f, 3.5f, 59f), new Vector3(0.7f, 7f, 14f), routeMaterial);
            CreateBlock(course.transform, "Exit Wall Right", new Vector3(1.5f, 3.5f, 59f), new Vector3(0.7f, 7f, 14f), routeMaterial);

            CreateBlock(course.transform, "Distant Block A", new Vector3(20f, 8f, 44f), new Vector3(13f, 16f, 18f), distantMaterial);
            CreateBlock(course.transform, "Distant Block B", new Vector3(-28f, 6f, 29f), new Vector3(18f, 12f, 24f), distantMaterial);
            CreateBlock(course.transform, "Distant Block C", new Vector3(12f, 12f, 78f), new Vector3(22f, 24f, 18f), distantMaterial);

            CreateBlock(course.transform, "Void Floor", new Vector3(0f, -14f, 35f), new Vector3(100f, 1f, 120f), accentMaterial);
        }

        private static void CreateCurvedWall(Transform parent, Material material)
        {
            GameObject wallRoot = new("Curved Wall Run");
            wallRoot.transform.SetParent(parent);
            Vector3 center = new(-6f, 0f, 20f);
            const int segmentCount = 12;
            const float radius = 9f;
            const float totalAngle = 72f;
            float segmentAngle = totalAngle / segmentCount;
            float segmentLength = 2f * radius * Mathf.Sin(segmentAngle * Mathf.Deg2Rad * 0.5f) + 0.12f;

            for (int index = 0; index < segmentCount; index++)
            {
                float angle = segmentAngle * (index + 0.5f);
                float radians = angle * Mathf.Deg2Rad;
                Vector3 position = center + new Vector3(Mathf.Cos(radians) * radius, 2.8f, Mathf.Sin(radians) * radius);
                CreateBlock(
                    wallRoot.transform,
                    $"Wall Segment {index + 1:00}",
                    position,
                    new Vector3(0.65f, 5.6f, segmentLength),
                    material,
                    new Vector3(0f, -angle, 0f));
            }
        }

        private static GameObject CreateBlock(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Vector3 rotation = default)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.rotation = Quaternion.Euler(rotation);
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreatePlayer(AnimatorController controller)
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (inputActions == null || modelAsset == null)
            {
                throw new System.InvalidOperationException("Player source assets are missing.");
            }

            GameObject player = new("PF_Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 0.1f, -7f);

            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.38f;
            characterController.center = new Vector3(0f, 1f, 0f);
            characterController.stepOffset = 0.35f;
            characterController.slopeLimit = 50f;
            characterController.skinWidth = 0.04f;

            ParkourInput input = player.AddComponent<ParkourInput>();
            ParkourMotor motor = player.AddComponent<ParkourMotor>();

            GameObject visual = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (visual == null)
            {
                throw new System.InvalidOperationException("Character model could not be instantiated.");
            }

            visual.name = "Character Visual";
            visual.transform.SetParent(player.transform, false);
            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = visual.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            ParkourAnimationDriver animationDriver = player.AddComponent<ParkourAnimationDriver>();

            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();
            ParkourCamera parkourCamera = cameraObject.AddComponent<ParkourCamera>();
            cameraObject.transform.position = player.transform.position + new Vector3(0.42f, 2.1f, -3.15f);

            input.Initialize(inputActions);
            motor.Initialize(input, cameraObject.transform);
            animationDriver.Initialize(animator, motor);
            parkourCamera.Initialize(player.transform, input, motor);
        }

        private static void AddSceneToBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.All(scene => scene.path != ScenePath))
            {
                EditorBuildSettings.scenes = scenes
                    .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
                    .ToArray();
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
