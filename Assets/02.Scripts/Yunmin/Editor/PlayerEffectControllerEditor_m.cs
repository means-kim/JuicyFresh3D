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
            "Play 모드에서 재생·정지할 수 있습니다. Effect Color가 공통 연출 색상입니다. "
            + "외곽선은 Outline Fade In Duration 동안 켜진 뒤, Outline Fade Out Duration 동안 자동으로 사라집니다. "
            + "도중에 정지하면 현재 밝기에서 Stop Fade Duration 동안 사라집니다. "
            + "Outline Width는 픽셀 두께, Outline Brightness는 발광 밝기입니다.",
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
