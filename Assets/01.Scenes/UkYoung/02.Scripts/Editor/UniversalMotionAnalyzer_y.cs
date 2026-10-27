using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Antigravity.AnimationAnalysis
{
    public static class UniversalMotionAnalyzer_y
    {
        private static readonly string[] LedgeMotionPaths =
        {
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_JumpingToHanging.fbx",
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_HangingIdle.fbx",
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_BracedHangHopLeft.fbx",
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_BracedHangHopRight.fbx",
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_BracedHangToCrouch.fbx",
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_CrouchedToSprinting.fbx",
            "Assets/01.Scenes/UkYoung/06.Animations/Anim_Sprint.fbx"
        };

        public sealed class MotionDnaResult
        {
            public string ClipName;
            public float Duration;
            public int FrameCount;

            public float T1_TimeToExtreme;
            public float T2_TimeToExit;
            public int ExtremeFrameIndex;
            public string ExtremeType; // "LowestHips" (Slide/Crouch) or "HighestHips" (Jump/Apex)

            public float AbsoluteMinGroundMargin;
            public int WorstFrameIndex;
            public string WorstBoneName;
            public bool HasGroundPenetration;

            public float FacingDeviationAngleDeg;
            public string LeadLegExtended;
            public float HipsYawMin;
            public float HipsYawMax;
            public float HipsLateralMin;
            public float HipsLateralMax;

            public Vector3 RootTotalDelta;
            public float AverageSpeed;
            public float PeakSpeed;

            public Dictionary<string, Vector3> EntryBones = new Dictionary<string, Vector3>();
            public Dictionary<string, Vector3> ExtremeBones = new Dictionary<string, Vector3>();
            public Dictionary<string, Vector3> ExitBones = new Dictionary<string, Vector3>();

            public string ExportJsonPath;
        }

        public static MotionDnaResult AnalyzeByPath(
            string clipAssetPath,
            GameObject targetRig = null,
            bool exportResult = true)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipAssetPath);
            if (clip == null)
            {
                var clips = AssetDatabase.LoadAllAssetsAtPath(clipAssetPath).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToList();
                if (clips.Count > 0)
                {
                    clip = clips[0];
                }
            }

            if (clip == null)
            {
                Debug.LogError($"[UniversalMotionAnalyzer_y] 클립을 로드할 수 없습니다: {clipAssetPath}");
                return null;
            }

            return Analyze(clip, targetRig, exportResult);
        }

        public static MotionDnaResult Analyze(
            AnimationClip clip,
            GameObject targetRig = null,
            bool exportResult = true)
        {
            if (clip == null)
            {
                Debug.LogError("[UniversalMotionAnalyzer_y] 분석할 AnimationClip이 null입니다.");
                return null;
            }

            Animator animator = ResolveHumanoidAnimator(targetRig);
            if (animator == null)
            {
                Debug.LogError("[UniversalMotionAnalyzer_y] 유효한 Humanoid 아바타를 찾을 수 없습니다.");
                return null;
            }

            float duration = clip.length;
            int frameCount = Mathf.Max(2, Mathf.CeilToInt(duration * (clip.frameRate > 0 ? clip.frameRate : 30f)) + 1);

            var targets = new (string Name, HumanBodyBones Bone)[]
            {
                ("Hips", HumanBodyBones.Hips),
                ("Spine", HumanBodyBones.Spine),
                ("Chest", HumanBodyBones.Chest),
                ("Head", HumanBodyBones.Head),
                ("LeftShoulder", HumanBodyBones.LeftShoulder),
                ("LeftUpperArm", HumanBodyBones.LeftUpperArm),
                ("LeftLowerArm", HumanBodyBones.LeftLowerArm),
                ("LeftHand", HumanBodyBones.LeftHand),
                ("RightShoulder", HumanBodyBones.RightShoulder),
                ("RightUpperArm", HumanBodyBones.RightUpperArm),
                ("RightLowerArm", HumanBodyBones.RightLowerArm),
                ("RightHand", HumanBodyBones.RightHand),
                ("LeftUpperLeg", HumanBodyBones.LeftUpperLeg),
                ("LeftLowerLeg", HumanBodyBones.LeftLowerLeg),
                ("LeftFoot", HumanBodyBones.LeftFoot),
                ("RightUpperLeg", HumanBodyBones.RightUpperLeg),
                ("RightLowerLeg", HumanBodyBones.RightLowerLeg),
                ("RightFoot", HumanBodyBones.RightFoot)
            };

            float minHipsY = float.MaxValue;
            float maxHipsY = float.MinValue;
            int lowestFrameIndex = 0;
            float lowestTime = 0f;
            int highestFrameIndex = 0;
            float highestTime = 0f;

            float absoluteMinMargin = float.MaxValue;
            int worstFrameIndex = 0;
            string worstBoneName = "None";

            var hipsPositions = new List<Vector3>(frameCount);
            var rootPositions = new List<Vector3>(frameCount);
            var hipsYawAngles = new List<float>(frameCount);
            var hipsLateralPositions = new List<float>(frameCount);

            AnimationMode.StartAnimationMode();
            try
            {
                for (int i = 0; i < frameCount; i++)
                {
                    float t = (i / (float)(frameCount - 1)) * duration;
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, clip, t);
                    AnimationMode.EndSampling();

                    Vector3 rootPos = animator.transform.position;
                    rootPositions.Add(rootPos);

                    Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    Vector3 hipsPos = hips != null ? hips.position : rootPos;
                    hipsPositions.Add(hipsPos);
                    if (hips != null)
                    {
                        Vector3 rootForward = Vector3.ProjectOnPlane(animator.transform.forward, Vector3.up).normalized;
                        Vector3 hipsForward = Vector3.ProjectOnPlane(hips.forward, Vector3.up).normalized;
                        float hipsYaw = Vector3.SignedAngle(rootForward, hipsForward, Vector3.up);
                        if (hipsYawAngles.Count > 0)
                        {
                            hipsYaw = hipsYawAngles[^1] + Mathf.DeltaAngle(hipsYawAngles[^1], hipsYaw);
                        }

                        hipsYawAngles.Add(hipsYaw);
                        hipsLateralPositions.Add(Vector3.Dot(
                            hipsPos - rootPos,
                            animator.transform.right));
                    }

                    if (hipsPos.y < minHipsY)
                    {
                        minHipsY = hipsPos.y;
                        lowestFrameIndex = i;
                        lowestTime = t;
                    }

                    if (hipsPos.y > maxHipsY)
                    {
                        maxHipsY = hipsPos.y;
                        highestFrameIndex = i;
                        highestTime = t;
                    }

                    // Full-frame worst ground margin scan
                    foreach (var (name, bone) in targets)
                    {
                        Transform tBone = animator.GetBoneTransform(bone);
                        if (tBone != null)
                        {
                            float boneWorldY = tBone.position.y;
                            if (boneWorldY < absoluteMinMargin)
                            {
                                absoluteMinMargin = boneWorldY;
                                worstFrameIndex = i;
                                worstBoneName = name;
                            }
                        }
                    }
                }

                // Determine extreme type (crouch/slide if significant drop, else jump/apex)
                float entryHipsY = hipsPositions[0].y;
                bool isCrouchOrSlide = (entryHipsY - minHipsY) > (maxHipsY - entryHipsY);
                int extremeIndex = isCrouchOrSlide ? lowestFrameIndex : highestFrameIndex;
                float extremeTime = isCrouchOrSlide ? lowestTime : highestTime;
                string extremeType = isCrouchOrSlide ? "LowestHips" : "HighestHips";

                // Capture 3 Key Poses
                var entryBones = CapturePoseAtTime(animator, clip, 0f, targets);
                var extremeBones = CapturePoseAtTime(animator, clip, extremeTime, targets);
                var exitBones = CapturePoseAtTime(animator, clip, duration, targets);

                // Calculate leg extension and facing deviation at extreme pose
                Vector3 hipsExt = extremeBones["Hips"];
                Vector3 lFootExt = extremeBones["LeftFoot"];
                Vector3 rFootExt = extremeBones["RightFoot"];
                Vector3 toLeft = lFootExt - hipsExt;
                Vector3 toRight = rFootExt - hipsExt;

                string leadLegName = toLeft.z > toRight.z ? "LeftLeg" : "RightLeg";
                Vector3 leadVec = toLeft.z > toRight.z ? toLeft : toRight;
                Vector3 leadHorizontal = Vector3.ProjectOnPlane(leadVec, Vector3.up).normalized;
                float deviationAngle = Vector3.SignedAngle(Vector3.forward, leadHorizontal, Vector3.up);

                // Root total displacement and speeds
                Vector3 rootDelta = rootPositions[frameCount - 1] - rootPositions[0];
                float totalDistance = 0f;
                float peakSpeed = 0f;
                float dt = duration / (frameCount - 1);

                for (int i = 0; i < frameCount - 1; i++)
                {
                    float dist = Vector3.Distance(rootPositions[i], rootPositions[i + 1]);
                    totalDistance += dist;
                    float speed = dt > 0f ? dist / dt : 0f;
                    if (speed > peakSpeed) peakSpeed = speed;
                }

                float averageSpeed = duration > 0f ? totalDistance / duration : 0f;

                var result = new MotionDnaResult
                {
                    ClipName = clip.name,
                    Duration = duration,
                    FrameCount = frameCount,
                    T1_TimeToExtreme = extremeTime,
                    T2_TimeToExit = duration - extremeTime,
                    ExtremeFrameIndex = extremeIndex,
                    ExtremeType = extremeType,
                    AbsoluteMinGroundMargin = absoluteMinMargin,
                    WorstFrameIndex = worstFrameIndex,
                    WorstBoneName = worstBoneName,
                    HasGroundPenetration = absoluteMinMargin < -0.005f,
                    FacingDeviationAngleDeg = deviationAngle,
                    LeadLegExtended = leadLegName,
                    HipsYawMin = hipsYawAngles.Count > 0 ? hipsYawAngles.Min() : 0f,
                    HipsYawMax = hipsYawAngles.Count > 0 ? hipsYawAngles.Max() : 0f,
                    HipsLateralMin = hipsLateralPositions.Count > 0 ? hipsLateralPositions.Min() : 0f,
                    HipsLateralMax = hipsLateralPositions.Count > 0 ? hipsLateralPositions.Max() : 0f,
                    RootTotalDelta = rootDelta,
                    AverageSpeed = averageSpeed,
                    PeakSpeed = peakSpeed,
                    EntryBones = entryBones,
                    ExtremeBones = extremeBones,
                    ExitBones = exitBones
                };

                // Export JSON alongside clip or to project root
                string clipPath = AssetDatabase.GetAssetPath(clip);
                string outputDir = !string.IsNullOrEmpty(clipPath) ? Path.GetDirectoryName(clipPath) : "Assets";
                string safeName = clip.name.Replace(" ", "_");
                string jsonPath = Path.Combine(outputDir ?? "Assets", $"Motion_DNA_{safeName}.json").Replace('\\', '/');

                if (exportResult)
                {
                    ExportResultToJson(result, jsonPath);
                    result.ExportJsonPath = jsonPath;
                }

                Debug.Log($"[UniversalMotionAnalyzer_y] 모션 DNA 분석 완료: {clip.name} (결함 침하: {result.HasGroundPenetration}, 최저 마진: {result.AbsoluteMinGroundMargin:F3}m @ {result.WorstBoneName})");
                return result;
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
        }

        private static Dictionary<string, Vector3> CapturePoseAtTime(
            Animator animator,
            AnimationClip clip,
            float time,
            (string Name, HumanBodyBones Bone)[] targets)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, time);
            AnimationMode.EndSampling();

            Vector3 rootPos = animator.transform.position;
            var bones = new Dictionary<string, Vector3>();

            foreach (var (name, bone) in targets)
            {
                Transform t = animator.GetBoneTransform(bone);
                bones[name] = t != null ? t.position - rootPos : Vector3.zero;
            }

            return bones;
        }

        private static void ExportResultToJson(MotionDnaResult res, string path)
        {
            var lines = new List<string>
            {
                "{",
                $"  \"clipName\": \"{res.ClipName}\",",
                $"  \"duration\": {res.Duration:F3},",
                $"  \"frameCount\": {res.FrameCount},",
                "  \"phases\": {",
                $"    \"extremeType\": \"{res.ExtremeType}\",",
                $"    \"extremeFrameIndex\": {res.ExtremeFrameIndex},",
                $"    \"t1_timeToExtreme\": {res.T1_TimeToExtreme:F3},",
                $"    \"t2_timeToExit\": {res.T2_TimeToExit:F3}",
                "  },",
                "  \"kinematicMetrics\": {",
                $"    \"absoluteMinGroundMargin\": {res.AbsoluteMinGroundMargin:F3},",
                $"    \"hasGroundPenetration\": {res.HasGroundPenetration.ToString().ToLower()},",
                $"    \"worstFrameIndex\": {res.WorstFrameIndex},",
                $"    \"worstBoneName\": \"{res.WorstBoneName}\",",
                $"    \"facingDeviationAngleDeg\": {res.FacingDeviationAngleDeg:F1},",
                $"    \"leadLegExtended\": \"{res.LeadLegExtended}\",",
                    $"    \"hipsYawMin\": {res.HipsYawMin:F2},",
                    $"    \"hipsYawMax\": {res.HipsYawMax:F2},",
                    $"    \"hipsYawSpan\": {(res.HipsYawMax - res.HipsYawMin):F2},",
                    $"    \"hipsLateralMin\": {res.HipsLateralMin:F3},",
                    $"    \"hipsLateralMax\": {res.HipsLateralMax:F3},",
                $"    \"rootDeltaForward\": {res.RootTotalDelta.z:F3},",
                $"    \"rootDeltaLateral\": {res.RootTotalDelta.x:F3},",
                $"    \"rootDeltaVertical\": {res.RootTotalDelta.y:F3},",
                $"    \"averageSpeed\": {res.AverageSpeed:F2},",
                $"    \"peakSpeed\": {res.PeakSpeed:F2}",
                "  },",
                "  \"entryPose\": {",
                FormatBones(res.EntryBones),
                "  },",
                "  \"extremePose\": {",
                FormatBones(res.ExtremeBones),
                "  },",
                "  \"exitPose\": {",
                FormatBones(res.ExitBones),
                "  }",
                "}"
            };

            File.WriteAllLines(path, lines);
        }

        private static string FormatBones(Dictionary<string, Vector3> bones)
        {
            var entries = bones.Select(kv => $"    \"{kv.Key}\": [{kv.Value.x:F3}, {kv.Value.y:F3}, {kv.Value.z:F3}]");
            return string.Join(",\n", entries);
        }

        private static Animator ResolveHumanoidAnimator(GameObject targetRig)
        {
            if (targetRig != null)
            {
                var anim = targetRig.GetComponentInChildren<Animator>();
                if (anim != null && anim.avatar != null && anim.avatar.isHuman) return anim;
            }

            var sceneAnimators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            foreach (var anim in sceneAnimators)
            {
                if (anim != null && anim.avatar != null && anim.avatar.isHuman) return anim;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var anim = prefab.GetComponentInChildren<Animator>();
                    if (anim != null && anim.avatar != null && anim.avatar.isHuman) return anim;
                }
            }

            return null;
        }

        [MenuItem("Assets/Antigravity/Analyze Motion DNA", false, 20)]
        public static void AnalyzeSelectedClip()
        {
            var clip = Selection.activeObject as AnimationClip;
            if (clip == null)
            {
                string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToList();
                if (clips.Count > 0) clip = clips[0];
            }

            if (clip != null)
            {
                Analyze(clip);
            }
            else
            {
                Debug.LogWarning("[UniversalMotionAnalyzer_y] 프로젝트 뷰에서 AnimationClip 또는 FBX를 선택해 주십시오.");
            }
        }

        [MenuItem("Tools/Parkour/Analyze Ledge Motion Chain")]
        public static void AnalyzeLedgeMotionChain()
        {
            foreach (string path in LedgeMotionPaths)
            {
                MotionDnaResult result = AnalyzeByPath(path, exportResult: false);
                if (result == null)
                {
                    throw new InvalidOperationException($"Ledge motion analysis failed: {path}");
                }

                Debug.Log(
                    $"[LedgeMotionChain] {Path.GetFileNameWithoutExtension(path)} | " +
                    $"duration={result.Duration:F3}, root={result.RootTotalDelta}, " +
                    $"facing={result.FacingDeviationAngleDeg:F1}, " +
                    $"hipsYaw={result.HipsYawMin:F1}..{result.HipsYawMax:F1}, " +
                    $"hipsLateral={result.HipsLateralMin:F3}..{result.HipsLateralMax:F3}");
            }
        }
    }
}
