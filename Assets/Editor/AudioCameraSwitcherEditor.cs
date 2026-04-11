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
        EditorGUILayout.HelpBox(
            "Level 2 camera switching: pick camera presets from the library based on audio rhythm.",
            MessageType.Info);

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("audioProcessor"),
            new GUIContent("Audio Controller"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camera Presets (Level 1 Templates)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("cameraPresets"),
            new GUIContent("Camera Library"),
            true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Switch Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("threshold"),
            new GUIContent("Trigger Sensitivity"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("cooldown"),
            new GUIContent("Minimum Switch Interval (sec)"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
