using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerEffectController_m)), CanEditMultipleObjects]
public sealed class PlayerEffectControllerEditor_m : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Play 모드에서 세 연출을 함께 재생·정지할 수 있습니다. "
            + "Effect Color와 Stop Fade Duration은 공통 설정입니다. "
            + "개별 연출의 모양과 자동 페이드 시간은 CrossFlashEffect_m, "
            + "ShilhouetteOutlineEffect_m, OutlineParticleLightEffect_m 컴포넌트에서 조절합니다.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("연출 재생"))
            {
                foreach (Object selectedTarget in targets)
                    ((PlayerEffectController_m)selectedTarget).PlayEffect();
            }

            if (GUILayout.Button("연출 정지"))
            {
                foreach (Object selectedTarget in targets)
                    ((PlayerEffectController_m)selectedTarget).StopEffect();
            }
        }
    }
}
