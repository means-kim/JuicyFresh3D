using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JuicyFresh3D.Editor
{
    public sealed class UniversalAnimationInspector_y : EditorWindow
    {
        private AnimationClip _targetClip;
        private GameObject _targetCharacterOverride;

        private bool _showTrajectory = true;
        private bool _show3AnchorsOnly = true;
        private bool _showGhostTrail = false;
        private int _ghostTrailStep = 5;
        private Color _trajectoryColor = Color.green;

        private readonly List<Vector3> _rootTrajectory = new List<Vector3>();
        private readonly List<PoseSnapshot> _trailSnapshots = new List<PoseSnapshot>();
        private PoseSnapshot _entryPose;
        private PoseSnapshot _lowestPose;
        private PoseSnapshot _apexPose;
        private PoseSnapshot _exitPose;

        private float _clipDuration;
        private int _frameCount;
        private float _tLowestTime;
        private float _tApexTime;
        private float _minGroundMargin;
        private float _leadLegAngle;
        private string _leadLegName = "None";
        private Vector3 _rootTotalDelta;
        private bool _hasAnalyzed;
        private Vector2 _scrollPos;

        private struct PoseSnapshot
        {
            public float Time;
            public int FrameIndex;
            public Vector3 RootWorldPos;
            public Dictionary<HumanBodyBones, Vector3> BoneWorldPositions;
        }

        [MenuItem("Tools/Parkour/Universal Animation Inspector")]
        public static void OpenWindow()
        {
            var window = GetWindow<UniversalAnimationInspector_y>("Motion Inspector");
            window.minSize = new Vector2(400, 600);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("범용 애니메이션 비주얼 분석기", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("1. 분석 대상 에셋 설정", EditorStyles.boldLabel);
            _targetClip = (AnimationClip)EditorGUILayout.ObjectField("애니메이션 클립", _targetClip, typeof(AnimationClip), false);
            _targetCharacterOverride = (GameObject)EditorGUILayout.ObjectField("캐릭터 모델(선택)", _targetCharacterOverride, typeof(GameObject), true);
            EditorGUILayout.HelpBox("캐릭터 모델을 비워두면 씬의 플레이어 모델을 자동 탐색하고, 없으면 기본 모델을 탐색합니다.", MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("2. 씬 뷰 시각화 옵션", EditorStyles.boldLabel);
            _showTrajectory = EditorGUILayout.Toggle("루트 이동 궤적 선 표시", _showTrajectory);
            _show3AnchorsOnly = EditorGUILayout.Toggle("3대 핵심 앵커 고스트만 표시", _show3AnchorsOnly);
            _showGhostTrail = EditorGUILayout.Toggle("연속 잔상 고스트 표시 (사령관 지시)", _showGhostTrail);
            if (_showGhostTrail)
            {
                _ghostTrailStep = EditorGUILayout.IntSlider("잔상 프레임 간격", _ghostTrailStep, 2, 15);
            }
            _trajectoryColor = EditorGUILayout.ColorField("궤적 선 색상", _trajectoryColor);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("애니메이션 분석 및 씬 뷰 렌더링 실행", GUILayout.Height(36)))
            {
                RunAnalysis();
            }
            GUI.backgroundColor = Color.white;

            if (_hasAnalyzed)
            {
                DrawAnalysisReportUI();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawAnalysisReportUI()
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("3. 3위상 모션 DNA 실측 요약 리포트", EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"클립: {_targetClip.name} (길이: {_clipDuration:F3}초, {_frameCount}프레임)");
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("[시간축 및 위상]", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"- 골반 최저점(웅크림/슬라이드): {_tLowestTime:F3}초 (진입 가속 구간)");
            EditorGUILayout.LabelField($"- 골반 최고점(도약/점프 정점): {_tApexTime:F3}초");
            EditorGUILayout.LabelField($"- 종료 복귀 시간: {_clipDuration - _tLowestTime:F3}초 (회복 구간)");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("[물리 안전 및 기구학]", EditorStyles.boldLabel);
            GUI.color = _minGroundMargin < 0f ? Color.red : Color.green;
            EditorGUILayout.LabelField($"- 지면 최소 마진: {_minGroundMargin:F3}m {(_minGroundMargin < 0f ? "[경고: 지면 파묻힘 감지]" : "[정상]")}");
            GUI.color = Color.white;

            EditorGUILayout.LabelField($"- 전방 주도 발: {_leadLegName}");
            EditorGUILayout.LabelField($"- 진행축 편차각: {_leadLegAngle:F1}도 (0도에 가까울수록 반듯한 전진)");
            EditorGUILayout.LabelField($"- 루트 총 이동량: 전진 {_rootTotalDelta.z:F2}m, 횡편향 {_rootTotalDelta.x:F2}m, 수직변위 {_rootTotalDelta.y:F2}m");

            EditorGUILayout.Space(8);
            if (GUILayout.Button("모션 DNA JSON 리포트 파일로 내보내기", GUILayout.Height(28)))
            {
                ExportJsonReport();
            }
            EditorGUILayout.EndVertical();
        }

        private void RunAnalysis()
        {
            if (_targetClip == null)
            {
                EditorUtility.DisplayDialog("오류", "분석할 애니메이션 클립을 먼저 지정해 주십시오.", "확인");
                return;
            }

            Animator animator = FindTargetAnimator(_targetCharacterOverride);
            if (animator == null)
            {
                EditorUtility.DisplayDialog("오류", "씬 또는 프로젝트에서 유효한 Humanoid 아바타를 찾을 수 없습니다.", "확인");
                return;
            }

            _clipDuration = _targetClip.length;
            _frameCount = Mathf.Max(2, Mathf.CeilToInt(_clipDuration * _targetClip.frameRate) + 1);

            _rootTrajectory.Clear();
            _trailSnapshots.Clear();

            float minHipsY = float.MaxValue;
            float maxHipsY = float.MinValue;
            _minGroundMargin = float.MaxValue;

            AnimationMode.StartAnimationMode();
            try
            {
                for (int i = 0; i < _frameCount; i++)
                {
                    float t = (i / (float)(_frameCount - 1)) * _clipDuration;
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, _targetClip, t);
                    AnimationMode.EndSampling();

                    Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    Vector3 rootPos = animator.transform.position;
                    Vector3 hipsPos = hips != null ? hips.position : rootPos;

                    _rootTrajectory.Add(hipsPos);

                    if (hipsPos.y < minHipsY)
                    {
                        minHipsY = hipsPos.y;
                        _tLowestTime = t;
                    }

                    if (hipsPos.y > maxHipsY)
                    {
                        maxHipsY = hipsPos.y;
                        _tApexTime = t;
                    }

                    if (i % _ghostTrailStep == 0 || i == _frameCount - 1)
                    {
                        _trailSnapshots.Add(CaptureSnapshot(animator, t, i));
                    }
                }

                // 3 Anchor Poses Extraction
                _entryPose = SampleAndCapture(animator, _targetClip, 0f, 0);
                _lowestPose = SampleAndCapture(animator, _targetClip, _tLowestTime, Mathf.RoundToInt((_tLowestTime / _clipDuration) * (_frameCount - 1)));
                _apexPose = SampleAndCapture(animator, _targetClip, _tApexTime, Mathf.RoundToInt((_tApexTime / _clipDuration) * (_frameCount - 1)));
                _exitPose = SampleAndCapture(animator, _targetClip, _clipDuration, _frameCount - 1);

                // Min ground margin check at key extreme
                _minGroundMargin = _lowestPose.BoneWorldPositions.Values.Min(p => p.y);

                // Lead leg extension calculation
                Vector3 hipsWorld = _lowestPose.BoneWorldPositions[HumanBodyBones.Hips];
                Vector3 lFootWorld = _lowestPose.BoneWorldPositions[HumanBodyBones.LeftFoot];
                Vector3 rFootWorld = _lowestPose.BoneWorldPositions[HumanBodyBones.RightFoot];

                Vector3 toLeft = lFootWorld - hipsWorld;
                Vector3 toRight = rFootWorld - hipsWorld;

                if (toLeft.z > toRight.z)
                {
                    _leadLegName = "왼발 (LeftFoot)";
                    Vector3 forwardDir = Vector3.ProjectOnPlane(toLeft, Vector3.up).normalized;
                    _leadLegAngle = Vector3.SignedAngle(Vector3.forward, forwardDir, Vector3.up);
                }
                else
                {
                    _leadLegName = "오른발 (RightFoot)";
                    Vector3 forwardDir = Vector3.ProjectOnPlane(toRight, Vector3.up).normalized;
                    _leadLegAngle = Vector3.SignedAngle(Vector3.forward, forwardDir, Vector3.up);
                }

                _rootTotalDelta = _exitPose.RootWorldPos - _entryPose.RootWorldPos;
                _hasAnalyzed = true;

                ExportJsonReport();
                SceneView.RepaintAll();
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
        }

        private static PoseSnapshot SampleAndCapture(Animator animator, AnimationClip clip, float time, int frameIndex)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, time);
            AnimationMode.EndSampling();

            return CaptureSnapshot(animator, time, frameIndex);
        }

        private static PoseSnapshot CaptureSnapshot(Animator animator, float time, int frameIndex)
        {
            var bones = new Dictionary<HumanBodyBones, Vector3>();
            var targets = new[]
            {
                HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
                HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
            };

            foreach (var bone in targets)
            {
                Transform t = animator.GetBoneTransform(bone);
                bones[bone] = t != null ? t.position : Vector3.zero;
            }

            return new PoseSnapshot
            {
                Time = time,
                FrameIndex = frameIndex,
                RootWorldPos = animator.transform.position,
                BoneWorldPositions = bones
            };
        }

        private void ExportJsonReport()
        {
            if (!_hasAnalyzed || _targetClip == null) return;

            string safeClipName = _targetClip.name.Replace(" ", "_");
            string outputPath = $"Assets/01.Scenes/UkYoung/Motion_DNA_{safeClipName}.json";

            var lines = new List<string>
            {
                "{",
                $"  \"clipName\": \"{_targetClip.name}\",",
                $"  \"duration\": {_clipDuration:F3},",
                $"  \"frameCount\": {_frameCount},",
                "  \"phases\": {",
                $"    \"tLowestExtremeDuration\": {_tLowestTime:F3},",
                $"    \"tApexExtremeDuration\": {_tApexTime:F3},",
                $"    \"tRecoverToExitDuration\": {_clipDuration - _tLowestTime:F3}",
                "  },",
                "  \"kinematics\": {",
                $"    \"minGroundMargin\": {_minGroundMargin:F3},",
                $"    \"leadLegExtended\": \"{_leadLegName}\",",
                $"    \"facingDeviationAngle\": {_leadLegAngle:F1},",
                $"    \"rootDeltaForward\": {_rootTotalDelta.z:F3},",
                $"    \"rootDeltaLateral\": {_rootTotalDelta.x:F3},",
                $"    \"rootDeltaVertical\": {_rootTotalDelta.y:F3}",
                "  }",
                "}"
            };

            File.WriteAllLines(outputPath, lines);
            Debug.Log($"[UniversalAnimationInspector_y] 범용 모션 DNA 리포트 생성 완료: {outputPath}");
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_hasAnalyzed || _targetClip == null) return;

            // 1. Draw Root Trajectory
            if (_showTrajectory && _rootTrajectory.Count > 1)
            {
                Handles.color = _trajectoryColor;
                for (int i = 0; i < _rootTrajectory.Count - 1; i++)
                {
                    Handles.DrawLine(_rootTrajectory[i], _rootTrajectory[i + 1], 2.5f);
                }
            }

            // 2. Draw Continuous Ghost Trail (If enabled)
            if (_showGhostTrail && _trailSnapshots.Count > 0)
            {
                Handles.color = new Color(0.4f, 0.7f, 1.0f, 0.25f);
                foreach (var snapshot in _trailSnapshots)
                {
                    DrawWireSkeleton(snapshot, Handles.color, 0.015f);
                }
            }

            // 3. Draw 3 Anchor Poses (Highlighted)
            if (_show3AnchorsOnly || !_showGhostTrail)
            {
                // Entry (Cyan)
                DrawWireSkeleton(_entryPose, new Color(0.2f, 0.9f, 1f, 0.85f), 0.035f);
                Handles.Label(_entryPose.BoneWorldPositions[HumanBodyBones.Head] + Vector3.up * 0.15f, $"[진입] t=0s");

                // Lowest / Slide Peak (Red/Magenta)
                Color keyColor = _minGroundMargin < 0f ? Color.red : new Color(1f, 0.3f, 0.3f, 0.9f);
                DrawWireSkeleton(_lowestPose, keyColor, 0.045f);
                Handles.Label(_lowestPose.BoneWorldPositions[HumanBodyBones.Head] + Vector3.up * 0.18f, $"[핵심 극점] t={_tLowestTime:F2}s, 마진={_minGroundMargin:F2}m");

                // Exit (Yellow)
                DrawWireSkeleton(_exitPose, new Color(1f, 0.85f, 0.2f, 0.85f), 0.035f);
                Handles.Label(_exitPose.BoneWorldPositions[HumanBodyBones.Head] + Vector3.up * 0.15f, $"[탈출] t={_clipDuration:F2}s");
            }
        }

        private static void DrawWireSkeleton(PoseSnapshot snapshot, Color color, float jointRadius)
        {
            Handles.color = color;
            var bones = snapshot.BoneWorldPositions;

            // Joints
            foreach (var pos in bones.Values)
            {
                Handles.SphereHandleCap(0, pos, Quaternion.identity, jointRadius, EventType.Repaint);
            }

            // Connections
            DrawBoneLink(bones, HumanBodyBones.Head, HumanBodyBones.Chest);
            DrawBoneLink(bones, HumanBodyBones.Chest, HumanBodyBones.Spine);
            DrawBoneLink(bones, HumanBodyBones.Spine, HumanBodyBones.Hips);

            DrawBoneLink(bones, HumanBodyBones.Chest, HumanBodyBones.LeftShoulder);
            DrawBoneLink(bones, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm);
            DrawBoneLink(bones, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
            DrawBoneLink(bones, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);

            DrawBoneLink(bones, HumanBodyBones.Chest, HumanBodyBones.RightShoulder);
            DrawBoneLink(bones, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm);
            DrawBoneLink(bones, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
            DrawBoneLink(bones, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);

            DrawBoneLink(bones, HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg);
            DrawBoneLink(bones, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg);
            DrawBoneLink(bones, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);

            DrawBoneLink(bones, HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg);
            DrawBoneLink(bones, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg);
            DrawBoneLink(bones, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
        }

        private static void DrawBoneLink(Dictionary<HumanBodyBones, Vector3> bones, HumanBodyBones b1, HumanBodyBones b2)
        {
            if (bones.TryGetValue(b1, out Vector3 p1) && bones.TryGetValue(b2, out Vector3 p2))
            {
                Handles.DrawLine(p1, p2, 1.8f);
            }
        }

        private static Animator FindTargetAnimator(GameObject userOverride)
        {
            if (userOverride != null)
            {
                var anim = userOverride.GetComponentInChildren<Animator>();
                if (anim != null && anim.avatar != null && anim.avatar.isHuman) return anim;
            }

            var sceneAnimators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            foreach (var anim in sceneAnimators)
            {
                if (anim != null && anim.avatar != null && anim.avatar.isHuman)
                {
                    return anim;
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var anim = prefab.GetComponentInChildren<Animator>();
                    if (anim != null && anim.avatar != null && anim.avatar.isHuman)
                    {
                        return anim;
                    }
                }
            }

            return null;
        }
    }
}
