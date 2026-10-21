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
            "Play 모드에서 재생·정지할 수 있습니다. Shared Settings의 Effect Color가 공통 연출 색상입니다.",
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
