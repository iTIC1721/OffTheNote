#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProgressManager))]
public class ProgressManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("개발 도구", EditorStyles.boldLabel);

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("Reset All Progress", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "진행 데이터 초기화",
                "모든 월드/스테이지 해금 데이터를 삭제합니다.\n이 작업은 되돌릴 수 없습니다.",
                "초기화", "취소"))
            {
                // 플레이 중이면 인스턴스 통해 호출
                if (Application.isPlaying)
                {
                    ((ProgressManager)target).ResetAllProgress();
                }
                else
                {
                    // 에디터 정지 상태: ProgressManager의 worldListData 직접 참조
                    ((ProgressManager)target).ResetAllProgress();
                }
            }
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif