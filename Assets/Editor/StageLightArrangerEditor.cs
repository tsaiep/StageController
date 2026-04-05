using UnityEditor;
using UnityEngine;

// 指定這個 Editor 是為了 StageLightArranger 服務的
[CustomEditor(typeof(StageLightArranger))]
[CanEditMultipleObjects] // 支援多選編輯
public class StageLightArrangerEditor : Editor
{
    // 用於存取序列化屬性的變數
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
        // 在 Editor 啟用時找到所有對應的屬性
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
        // 更新序列化物件狀態
        serializedObject.Update();

        StageLightArranger arranger = (StageLightArranger)target;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("舞台燈光排列器", EditorStyles.boldLabel);

        // --- 開始監控變動 ---
        EditorGUI.BeginChangeCheck();

        // --- 繪製基礎配置 ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("基礎配置", EditorStyles.miniBoldLabel);

        // 使用 GUIContent 自定義顯示名稱與 Tooltip (滑鼠懸停提示)
        EditorGUILayout.PropertyField(buildModeProp, new GUIContent("佈署模式", "選擇燈光的排列形狀"));
        EditorGUILayout.PropertyField(lightPrefabProp, new GUIContent("燈具 Prefab", "要生成的燈光原始物件"));

        // 繪製滑桿並附帶自定義標籤
        EditorGUILayout.PropertyField(countProp, new GUIContent("燈光數量"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // --- 繪製模式專屬參數 ---
        StageLightArranger.BuildMode mode = (StageLightArranger.BuildMode)buildModeProp.enumValueIndex;

        EditorGUILayout.BeginVertical("helpbox");

        if (mode == StageLightArranger.BuildMode.Arc)
        {
            EditorGUILayout.LabelField("弧形 (Arc) 模式參數", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(radiusProp, new GUIContent("弧形半徑 (Radius)"));
            EditorGUILayout.PropertyField(arcAngleProp, new GUIContent("張開角度 (Angle)"));
        }
        else
        {
            EditorGUILayout.LabelField("直線/S型 模式參數", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(spacingProp, new GUIContent("燈光間距"));

            if (mode == StageLightArranger.BuildMode.SShape)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(sIntensityProp, new GUIContent("S 彎曲強度"));
                EditorGUILayout.PropertyField(invertSProp, new GUIContent("反轉 S 方向"));
            }
        }
        EditorGUILayout.EndVertical();

        // --- 檢查變動並應用 ---
        if (EditorGUI.EndChangeCheck())
        {
            // 應用修改並自動記錄 Undo
            serializedObject.ApplyModifiedProperties();

            // 強制執行排列邏輯
            if (!Application.isPlaying)
            {
                arranger.GenerateLights();
            }

            // 標記物件已改變
            EditorUtility.SetDirty(arranger);
        }

        EditorGUILayout.Space(10);

        // --- 繪製功能按鈕 ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("生成/同步燈組", GUILayout.Height(30)))
        {
            arranger.GenerateLights();
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); // 淡淡的紅色
        if (GUILayout.Button("一鍵清除", GUILayout.Height(30)))
        {
            Undo.RegisterFullObjectHierarchyUndo(arranger.gameObject, "Clear Lights");
            arranger.ClearLights();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // 應用修改
        serializedObject.ApplyModifiedProperties();
    }
}