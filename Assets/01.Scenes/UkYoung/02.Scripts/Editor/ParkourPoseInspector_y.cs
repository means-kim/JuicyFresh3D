using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JuicyFresh3D.Editor
{
    public static class ParkourPoseInspector_y
    {
        private const string SlideFbxPath = "Assets/01.Scenes/UkYoung/06.Animations/Anim_RunningSlide.fbx";
        private const string OutputFilePath = "Assets/01.Scenes/UkYoung/FirstPose_RunningSlide.json";

        [MenuItem("Tools/Parkour/Inspect First Pose")]
        [InitializeOnLoadMethod]
        public static void InspectFirstPose()
        {
            var animator = UnityEngine.Object.FindAnyObjectByType<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogError("[ParkourPoseInspector_y] Scene에서 Humanoid Animator를 찾을 수 없습니다.");
                return;
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(SlideFbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToList();

            if (clips.Count == 0)
            {
                Debug.LogError($"[ParkourPoseInspector_y] 클립을 찾을 수 없습니다: {SlideFbxPath}");
                return;
            }

            var clip = clips[0];

            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, 0.0f);
                AnimationMode.EndSampling();

                Vector3 rootPos = animator.transform.position;
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Vector3 hipsPos = hips != null ? hips.position : rootPos;

                var bonesToExtract = new (string Name, HumanBodyBones Bone)[]
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
                    ("RightFoot", HumanBodyBones.RightFoot),
                };

                var lines = new List<string>
                {
                    "{",
                    $"  \"clipName\": \"{clip.name}\",",
                    $"  \"frame\": 0,",
                    $"  \"time\": 0.0,",
                    $"  \"rootPosition\": [{rootPos.x:F3}, {rootPos.y:F3}, {rootPos.z:F3}],",
                    "  \"bones\": ["
                };

                for (int i = 0; i < bonesToExtract.Length; i++)
                {
                    var (name, bone) = bonesToExtract[i];
                    Transform t = animator.GetBoneTransform(bone);
                    Vector3 worldPos = t != null ? t.position : Vector3.zero;
                    Vector3 relPos = t != null ? t.position - rootPos : Vector3.zero;

                    string worldYStr = worldPos.y.ToString("F3");
                    string relXStr = relPos.x.ToString("F3");
                    string relYStr = relPos.y.ToString("F3");
                    string relZStr = relPos.z.ToString("F3");
                    string comma = (i < bonesToExtract.Length - 1) ? "," : "";
                    lines.Add($"    {{\"name\": \"{name}\", \"worldY\": {worldYStr}, \"relX\": {relXStr}, \"relY\": {relYStr}, \"relZ\": {relZStr}}}{comma}");
                }

                lines.Add("  ]");
                lines.Add("}");

                File.WriteAllLines(OutputFilePath, lines);
                Debug.Log($"[ParkourPoseInspector_y] 첫 포즈 추출 완료: {OutputFilePath}");
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
        }
    }
}
