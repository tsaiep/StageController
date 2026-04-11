#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioVFXSwitcher))]
public class AudioVFXSwitcherEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Audio-driven VFX switching: rotate effect presets automatically based on the beat.",
            MessageType.Info);

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("audioSourceProcessor"),
            new GUIContent("Audio Controller"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("VFX Presets", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("vfxPresets"),
            new GUIContent("VFX Library"),
            true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Switch Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("threshold"),
            new GUIContent("Trigger Sensitivity"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("cooldown"),
            new GUIContent("Minimum Switch Interval"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
