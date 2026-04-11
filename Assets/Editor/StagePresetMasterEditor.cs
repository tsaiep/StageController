using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StagePresetMaster))]
public class StagePresetMasterEditor : Editor
{
    private Editor transformEditor;

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        if (transformEditor != null) DestroyImmediate(transformEditor);
    }

    private void OnUndoRedo()
    {
        StagePresetMaster master = (StagePresetMaster)target;
        if (master != null)
        {
            foreach (var group in master.lightGroups)
            {
                if (group.arranger != null) group.arranger.GenerateLights();
            }
            Repaint();
        }
    }

    public override void OnInspectorGUI()
    {
        StagePresetMaster master = (StagePresetMaster)target;
        serializedObject.Update();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Stage Preset Master", EditorStyles.boldLabel);

        for (int i = 0; i < master.lightGroups.Count; i++)
        {
            var group = master.lightGroups[i];

            string sessionKey = $"StagePreset_{master.GetInstanceID()}_{i}_Expanded";
            bool isExpanded = SessionState.GetBool(sessionKey, true);

            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.BeginHorizontal();

            bool nextExpanded = EditorGUILayout.Foldout(
                isExpanded,
                string.IsNullOrEmpty(group.groupName) ? "New Light Group" : group.groupName,
                true);

            if (nextExpanded != isExpanded)
            {
                SessionState.SetBool(sessionKey, nextExpanded);
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                Undo.RecordObject(master, "Remove Light Group");
                master.lightGroups.RemoveAt(i);
                serializedObject.ApplyModifiedProperties();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (nextExpanded)
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.TextField("Group Name", group.groupName);
                StageLightArranger newArranger = (StageLightArranger)EditorGUILayout.ObjectField(
                    "Linked Arranger",
                    group.arranger,
                    typeof(StageLightArranger),
                    true);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(master, "Update Group Info");
                    group.groupName = newName;
                    group.arranger = newArranger;
                }

                if (group.arranger != null)
                {
                    DrawEmbeddedTransform(group.arranger.transform);
                    EditorGUILayout.Space(5);
                    DrawArrangerDetails(group.arranger);
                }
                else
                {
                    EditorGUILayout.HelpBox("Assign a StageLightArranger object.", MessageType.Info);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        if (GUILayout.Button("+ Add Light Group", GUILayout.Height(30)))
        {
            Undo.RecordObject(master, "Add New Light Group");
            master.lightGroups.Add(new StagePresetMaster.LightGroupData());
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEmbeddedTransform(Transform targetTransform)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Transform", EditorStyles.miniBoldLabel);
        CreateCachedEditor(targetTransform, null, ref transformEditor);
        if (transformEditor != null)
        {
            EditorGUI.BeginChangeCheck();
            transformEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                StageLightArranger arranger = targetTransform.GetComponent<StageLightArranger>();
                if (arranger != null) arranger.GenerateLights();
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawArrangerDetails(StageLightArranger arranger)
    {
        EditorGUILayout.LabelField("Light Layout Settings", EditorStyles.miniBoldLabel);
        SerializedObject so = new SerializedObject(arranger);
        so.Update();

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.PropertyField(so.FindProperty("buildMode"), new GUIContent("Build Mode"));
        EditorGUILayout.PropertyField(so.FindProperty("lightPrefab"), new GUIContent("Light Prefab"));
        EditorGUILayout.PropertyField(so.FindProperty("count"), new GUIContent("Light Count"));

        StageLightArranger.BuildMode mode = (StageLightArranger.BuildMode)so.FindProperty("buildMode").enumValueIndex;

        if (mode == StageLightArranger.BuildMode.Arc)
        {
            EditorGUILayout.PropertyField(so.FindProperty("radius"), new GUIContent("Arc Radius"));
            EditorGUILayout.PropertyField(so.FindProperty("arcAngle"), new GUIContent("Arc Angle"));
        }
        else
        {
            EditorGUILayout.PropertyField(so.FindProperty("spacing"), new GUIContent("Spacing"));
            if (mode == StageLightArranger.BuildMode.SShape)
            {
                EditorGUILayout.PropertyField(so.FindProperty("sIntensity"), new GUIContent("S Curve Intensity"));
                EditorGUILayout.PropertyField(so.FindProperty("invertS"), new GUIContent("Invert S Direction"));
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            arranger.GenerateLights();
            EditorUtility.SetDirty(arranger);
        }
    }
}
