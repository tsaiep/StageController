using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageLightArranger))]
[CanEditMultipleObjects]
public class StageLightArrangerEditor : Editor
{
    private SerializedProperty buildModeProp;
    private SerializedProperty lightPrefabProp;
    private SerializedProperty countProp;
    private SerializedProperty spacingProp;
    private SerializedProperty sIntensityProp;
    private SerializedProperty invertSProp;
    private SerializedProperty radiusProp;
    private SerializedProperty arcAngleProp;

    private void OnEnable()
    {
        buildModeProp = serializedObject.FindProperty("buildMode");
        lightPrefabProp = serializedObject.FindProperty("lightPrefab");
        countProp = serializedObject.FindProperty("count");
        spacingProp = serializedObject.FindProperty("spacing");
        sIntensityProp = serializedObject.FindProperty("sIntensity");
        invertSProp = serializedObject.FindProperty("invertS");
        radiusProp = serializedObject.FindProperty("radius");
        arcAngleProp = serializedObject.FindProperty("arcAngle");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        StageLightArranger arranger = (StageLightArranger)target;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Stage Light Arranger", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Base Settings", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(buildModeProp, new GUIContent("Build Mode", "Choose the layout used for the lights."));
        EditorGUILayout.PropertyField(lightPrefabProp, new GUIContent("Light Prefab", "Prefab instantiated for each light."));
        EditorGUILayout.PropertyField(countProp, new GUIContent("Light Count"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        StageLightArranger.BuildMode mode = (StageLightArranger.BuildMode)buildModeProp.enumValueIndex;

        EditorGUILayout.BeginVertical("helpbox");

        if (mode == StageLightArranger.BuildMode.Arc)
        {
            EditorGUILayout.LabelField("Arc Settings", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(radiusProp, new GUIContent("Arc Radius"));
            EditorGUILayout.PropertyField(arcAngleProp, new GUIContent("Arc Angle"));
        }
        else
        {
            EditorGUILayout.LabelField("Line / S-Shape Settings", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(spacingProp, new GUIContent("Spacing"));

            if (mode == StageLightArranger.BuildMode.SShape)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(sIntensityProp, new GUIContent("S Curve Intensity"));
                EditorGUILayout.PropertyField(invertSProp, new GUIContent("Invert S Direction"));
            }
        }

        EditorGUILayout.EndVertical();

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
            {
                arranger.GenerateLights();
            }

            EditorUtility.SetDirty(arranger);
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate / Sync Lights", GUILayout.Height(30)))
        {
            arranger.GenerateLights();
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("Clear All", GUILayout.Height(30)))
        {
            Undo.RegisterFullObjectHierarchyUndo(arranger.gameObject, "Clear Lights");
            arranger.ClearLights();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
