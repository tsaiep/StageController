#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioCameraSwitcher))]
public class AudioCameraSwitcherEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Level 2 運鏡掛件：根據音訊節奏自動從方案庫挑選鏡頭", MessageType.Info);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("audioProcessor"), new GUIContent("音訊感應核心"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("鏡頭方案清單 (Level 1 樣板)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraPresets"), new GUIContent("鏡頭庫 (全身/半身/特寫)"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("切換邏輯設定", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("threshold"), new GUIContent("切換靈敏度"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cooldown"), new GUIContent("最短切換間隔 (秒)"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif