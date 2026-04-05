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
        EditorGUILayout.HelpBox("癟疭╰参沮竊笆ち传ぃ疭家舱", MessageType.Info);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("audioSourceProcessor"), new GUIContent("癟稰莱み"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("疭よ睲虫", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("vfxPresets"), new GUIContent("ち传疭"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("笆ち传把计", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("threshold"), new GUIContent("牟祇艶庇"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cooldown"), new GUIContent("ち传程丁筳"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif