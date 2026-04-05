using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class StagePresetSpawnerWindow : EditorWindow
{
    private StagePresetLibrary library;
    private Transform vTuberTarget;
    private float heightOffset = 0f;
    private float rotationOffset = 0f;
    private string currentPresetName = "";

    // 自動抓取 Library 的路徑快取
    private void OnEnable()
    {
        FindLibrary();
    }

    [MenuItem("Window/舞台控制/舞台生成器")]
    public static void ShowWindow()
    {
        GetWindow<StagePresetSpawnerWindow>("舞台生成器");
    }

    private void OnGUI()
    {
        if (library == null)
        {
            EditorGUILayout.HelpBox("找不到 StagePresetLibrary 檔案！請確認專案中存在該 Asset。", MessageType.Error);
            if (GUILayout.Button("重新掃描 Library")) FindLibrary();
            return;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("1. 配置目標", EditorStyles.boldLabel);
        vTuberTarget = (Transform)EditorGUILayout.ObjectField("VTuber 角色", vTuberTarget, typeof(Transform), true);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("2. 微調參數", EditorStyles.boldLabel);
        heightOffset = EditorGUILayout.FloatField("高度偏移 (Y)", heightOffset);
        rotationOffset = EditorGUILayout.Slider("旋轉偏移 (Y-Axis)", rotationOffset, -180f, 180f);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("3. 選擇模板", EditorStyles.boldLabel);

        // 繪製模板選擇按鈕組
        DrawPresetGrid();

        GUILayout.FlexibleSpace();

        GUI.enabled = vTuberTarget != null && !string.IsNullOrEmpty(currentPresetName);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("一鍵生成/更新舞台燈效", GUILayout.Height(50)))
        {
            SpawnPreset();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
    }

    private void DrawPresetGrid()
    {
        if (library.presets.Count == 0) return;

        // 每列顯示兩個按鈕
        int count = library.presets.Count;
        for (int i = 0; i < count; i++)
        {
            string pName = library.presets[i].presetName;

            // 如果選中，按鈕顏色變深
            if (currentPresetName == pName) GUI.backgroundColor = Color.cyan;
            else GUI.backgroundColor = Color.white;

            if (GUILayout.Button(pName, GUILayout.Height(30)))
            {
                currentPresetName = pName;
            }
        }
        GUI.backgroundColor = Color.white;
    }

    private void SpawnPreset()
    {
        GameObject prefab = library.GetPrefab(currentPresetName);
        if (prefab == null) return;

        // 1. 尋找舊的並刪除
        GameObject oldInScene = GameObject.Find("[舞台模板] " + currentPresetName);
        if (oldInScene != null) Undo.DestroyObjectImmediate(oldInScene);

        // 2. 生成 Prefab 實例
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        // 3. 設定位置與旋轉
        instance.transform.position = vTuberTarget.position + Vector3.up * heightOffset;
        instance.transform.rotation = Quaternion.Euler(0, vTuberTarget.eulerAngles.y + rotationOffset, 0);
        instance.name = "[舞台模板] " + currentPresetName;

        Undo.RegisterCreatedObjectUndo(instance, "Spawn Stage Preset");

        // --- 修正：移除已不存在的 lockPreset 引用 ---
        var master = instance.GetComponent<StagePresetMaster>();
        if (master != null)
        {
            // master.lockPreset = false;  <-- 這行刪掉
            // master.ForceSyncAll();      <-- 這行也建議刪掉，讓它維持 Prefab 原貌
            // master.lockPreset = true;   <-- 這行刪掉
        }

        Selection.activeGameObject = instance;
    }

    private void FindLibrary()
    {
        string[] guids = AssetDatabase.FindAssets("t:StagePresetLibrary");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            library = AssetDatabase.LoadAssetAtPath<StagePresetLibrary>(path);
        }
    }
}