using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

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
        EditorGUILayout.LabelField("舞台燈光管理中心 (Master)", EditorStyles.boldLabel);

        for (int i = 0; i < master.lightGroups.Count; i++)
        {
            var group = master.lightGroups[i];

            // --- 核心修正：使用 SessionState 儲存展開狀態 ---
            // 產出一個唯一的 Key 值 (物件 ID + 索引)
            string sessionKey = $"StagePreset_{master.GetInstanceID()}_{i}_Expanded";
            bool isExpanded = SessionState.GetBool(sessionKey, true);

            EditorGUILayout.BeginVertical("helpbox");

            EditorGUILayout.BeginHorizontal();

            // 繪製 Foldout，但不把值存回 group.isExpanded (避開 Undo)
            bool nextExpanded = EditorGUILayout.Foldout(isExpanded, string.IsNullOrEmpty(group.groupName) ? "新燈光群組" : group.groupName, true);

            // 如果狀態改變，存入 SessionState (這不會被 Ctrl+Z 影響)
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

            // 根據 SessionState 的值決定是否展開
            if (nextExpanded)
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.TextField("群組名稱", group.groupName);
                StageLightArranger newArranger = (StageLightArranger)EditorGUILayout.ObjectField("關聯 Arranger", group.arranger, typeof(StageLightArranger), true);

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
                    EditorGUILayout.HelpBox("請分配一個 StageLightArranger 物件。", MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        if (GUILayout.Button("+ 新增燈光群組", GUILayout.Height(30)))
        {
            Undo.RecordObject(master, "Add New Light Group");
            master.lightGroups.Add(new StagePresetMaster.LightGroupData());
        }

        serializedObject.ApplyModifiedProperties();
    }

    // --- 以下 DrawEmbeddedTransform 與 DrawArrangerDetails 邏輯不變，確保數據與 Undo 連動 ---

    private void DrawEmbeddedTransform(Transform targetTransform)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("物件座標 (Transform)", EditorStyles.miniBoldLabel);
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
        EditorGUILayout.LabelField("燈光排列參數", EditorStyles.miniBoldLabel);
        SerializedObject so = new SerializedObject(arranger);
        so.Update();

        EditorGUI.BeginChangeCheck();

        // 使用 GUIContent 來自定義顯示名稱
        EditorGUILayout.PropertyField(so.FindProperty("buildMode"), new GUIContent("佈署模式"));
        EditorGUILayout.PropertyField(so.FindProperty("lightPrefab"), new GUIContent("燈具 Prefab"));
        EditorGUILayout.PropertyField(so.FindProperty("count"), new GUIContent("燈光數量"));

        StageLightArranger.BuildMode mode = (StageLightArranger.BuildMode)so.FindProperty("buildMode").enumValueIndex;

        if (mode == StageLightArranger.BuildMode.Arc)
        {
            EditorGUILayout.PropertyField(so.FindProperty("radius"), new GUIContent("弧形半徑 (Radius)"));
            EditorGUILayout.PropertyField(so.FindProperty("arcAngle"), new GUIContent("張開角度 (Angle)"));
        }
        else
        {
            EditorGUILayout.PropertyField(so.FindProperty("spacing"), new GUIContent("燈光間距"));
            if (mode == StageLightArranger.BuildMode.SShape)
            {
                EditorGUILayout.PropertyField(so.FindProperty("sIntensity"), new GUIContent("S 彎曲強度"));
                EditorGUILayout.PropertyField(so.FindProperty("invertS"), new GUIContent("反轉 S 方向"));
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