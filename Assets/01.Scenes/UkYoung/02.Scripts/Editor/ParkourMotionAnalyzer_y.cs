using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JuicyFresh3D.Editor
{
    public static class ParkourMotionAnalyzer_y
    {
        private const string SlideFbxPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_RunningSlide.fbx";
        private const string OutputJsonPath = "Assets/01.Scenes/UkYoung/Slide_Motion_DNA.json";

        [MenuItem("Tools/Parkour/Analyze Slide Motion DNA")]
        [InitializeOnLoadMethod]
        public static void AnalyzeSlideMotion()
        {
            var animator = UnityEngine.Object.FindAnyObjectByType<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogWarning("[ParkourMotionAnalyzer_y] Scene에서 유효한 Humanoid Animator를 찾을 수 없습니다.");
                return;
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(SlideFbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToList();

            if (clips.Count == 0)
            {
                Debug.LogError($"[ParkourMotionAnalyzer_y] FBX 클립을 찾을 수 없습니다: {SlideFbxPath}");
                return;
            }

            var clip = clips[0];
            var frameCount = Mathf.Max(2, Mathf.CeilToInt(clip.length * clip.frameRate) + 1);

            AnimationMode.StartAnimationMode();
            try
            {
                // 1. Scan for Key Frame (Minimum Hips Y)
                int keyFrameIndex = 0;
                float keyTime = 0f;
                float minHipsY = float.MaxValue;
                Vector3 rootBasePos = animator.transform.position;

                for (int i = 0; i < frameCount; i++)
                {
                    float t = (i / (float)(frameCount - 1)) * clip.length;
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, clip, t);
                    AnimationMode.EndSampling();

                    Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    float hipsY = hips != null ? hips.position.y : 0f;
                    if (hipsY < minHipsY)
                    {
                        minHipsY = hipsY;
                        keyFrameIndex = i;
                        keyTime = t;
                    }
                }

                // 2. Extract Phase 1: Entry (Frame 0)
                var entryPose = SamplePoseAtTime(animator, clip, 0f, rootBasePos);

                // 3. Extract Phase 2: Key / Extreme Pose (min Hips Y)
                var keyPose = SamplePoseAtTime(animator, clip, keyTime, rootBasePos);

                // 4. Extract Phase 3: Exit (End Frame)
                var exitPose = SamplePoseAtTime(animator, clip, clip.length, rootBasePos);

                // 5. Kinematic Metrics
                float t1 = keyTime;
                float t2 = clip.length - keyTime;

                // Leg extension vector & angle at Key Pose
                Vector3 hipsToLeftFoot = keyPose.Bones["LeftFoot"] - keyPose.Bones["Hips"];
                Vector3 hipsToRightFoot = keyPose.Bones["RightFoot"] - keyPose.Bones["Hips"];
                Vector3 leadLegVec = (hipsToLeftFoot.z > hipsToRightFoot.z) ? hipsToLeftFoot : hipsToRightFoot;
                string leadLegName = (hipsToLeftFoot.z > hipsToRightFoot.z) ? "LeftLeg" : "RightLeg";

                Vector3 leadLegHorizontal = Vector3.ProjectOnPlane(leadLegVec, Vector3.up).normalized;
                float facingDeviationAngle = Vector3.SignedAngle(Vector3.forward, leadLegHorizontal, Vector3.up);

                // Minimum ground clearance across all bones at key pose
                float minBoneY = keyPose.Bones.Values.Min(v => v.y);

                // Root motion total displacement
                Vector3 rootStart = entryPose.RootWorldPos;
                Vector3 rootEnd = exitPose.RootWorldPos;
                Vector3 rootDelta = rootEnd - rootStart;

                // Build DNA JSON
                var lines = new List<string>
                {
                    "{",
                    $"  \"clipName\": \"{clip.name}\",",
                    $"  \"duration\": {clip.length:F3},",
                    $"  \"frameCount\": {frameCount},",
                    "  \"phases\": {",
                    $"    \"t1_accelerateToKeyDuration\": {t1:F3},",
                    $"    \"t2_recoverToExitDuration\": {t2:F3},",
                    $"    \"keyFrameIndex\": {keyFrameIndex},",
                    $"    \"keyTime\": {keyTime:F3}",
                    "  },",
                    "  \"kinematicMetrics\": {",
                    $"    \"minHipsHeight\": {minHipsY:F3},",
                    $"    \"minBoneGroundMargin\": {minBoneY:F3},",
                    $"    \"leadLegExtended\": \"{leadLegName}\",",
                    $"    \"facingDeviationAngleDeg\": {facingDeviationAngle:F1},",
                    $"    \"rootDeltaX\": {rootDelta.x:F3},",
                    $"    \"rootDeltaY\": {rootDelta.y:F3},",
                    $"    \"rootDeltaZ\": {rootDelta.z:F3}",
                    "  },",
                    "  \"entryPose\": {",
                    FormatBoneJson(entryPose.Bones),
                    "  },",
                    "  \"keyPose\": {",
                    FormatBoneJson(keyPose.Bones),
                    "  },",
                    "  \"exitPose\": {",
                    FormatBoneJson(exitPose.Bones),
                    "  }",
                    "}"
                };

                File.WriteAllLines(OutputJsonPath, lines);
                Debug.Log($"[ParkourMotionAnalyzer_y] 슬라이딩 모션 DNA 분석 완료: {OutputJsonPath}");
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
        }

        private struct PoseData
        {
            public Vector3 RootWorldPos;
            public Dictionary<string, Vector3> Bones;
        }

        private static PoseData SamplePoseAtTime(Animator animator, AnimationClip clip, float time, Vector3 baseRoot)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, time);
            AnimationMode.EndSampling();

            var bones = new Dictionary<string, Vector3>();
            var targets = new (string Name, HumanBodyBones Bone)[]
            {
                ("Hips", HumanBodyBones.Hips),
                ("Spine", HumanBodyBones.Spine),
                ("Chest", HumanBodyBones.Chest),
                ("Head", HumanBodyBones.Head),
                ("LeftShoulder", HumanBodyBones.LeftShoulder),
                ("LeftHand", HumanBodyBones.LeftHand),
                ("RightShoulder", HumanBodyBones.RightShoulder),
                ("RightHand", HumanBodyBones.RightHand),
                ("LeftLowerLeg", HumanBodyBones.LeftLowerLeg),
                ("LeftFoot", HumanBodyBones.LeftFoot),
                ("RightLowerLeg", HumanBodyBones.RightLowerLeg),
                ("RightFoot", HumanBodyBones.RightFoot),
            };

            foreach (var (name, bone) in targets)
            {
                Transform t = animator.GetBoneTransform(bone);
                bones[name] = t != null ? t.position - baseRoot : Vector3.zero;
            }

            return new PoseData
            {
                RootWorldPos = animator.transform.position,
                Bones = bones
            };
        }

        private static string FormatBoneJson(Dictionary<string, Vector3> bones)
        {
            var entries = bones.Select(kv => $"    \"{kv.Key}\": [{kv.Value.x:F3}, {kv.Value.y:F3}, {kv.Value.z:F3}]");
            return string.Join(",\n", entries);
        }
    }
}
