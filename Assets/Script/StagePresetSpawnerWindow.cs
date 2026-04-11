using UnityEditor;
using UnityEngine;

public class StagePresetSpawnerWindow : EditorWindow
{
    private const string StagePresetPrefix = "[Stage Preset] ";
    private const string LegacyStagePresetPrefix = "[\u821e\u53f0\u6a21\u677f] ";

    private StagePresetLibrary library;
    private Transform vTuberTarget;
    private float heightOffset = 0f;
    private float rotationOffset = 0f;
    private string currentPresetName = "";

    private void OnEnable()
    {
        FindLibrary();
    }

    [MenuItem("Window/Stage Control/Stage Preset Spawner")]
    public static void ShowWindow()
    {
        GetWindow<StagePresetSpawnerWindow>("Stage Preset Spawner");
    }

    private void OnGUI()
    {
        if (library == null)
        {
            EditorGUILayout.HelpBox(
                "StagePresetLibrary was not found. Make sure the asset exists in the project.",
                MessageType.Error);

            if (GUILayout.Button("Rescan Library")) FindLibrary();
            return;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("1. Target Setup", EditorStyles.boldLabel);
        vTuberTarget = (Transform)EditorGUILayout.ObjectField("VTuber Character", vTuberTarget, typeof(Transform), true);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("2. Fine Tune", EditorStyles.boldLabel);
        heightOffset = EditorGUILayout.FloatField("Height Offset (Y)", heightOffset);
        rotationOffset = EditorGUILayout.Slider("Rotation Offset (Y-Axis)", rotationOffset, -180f, 180f);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("3. Choose Preset", EditorStyles.boldLabel);
        DrawPresetGrid();

        GUILayout.FlexibleSpace();

        GUI.enabled = vTuberTarget != null && !string.IsNullOrEmpty(currentPresetName);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Spawn or Update Stage Preset", GUILayout.Height(50)))
        {
            SpawnPreset();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
    }

    private void DrawPresetGrid()
    {
        if (library.presets.Count == 0) return;

        for (int i = 0; i < library.presets.Count; i++)
        {
            string presetName = library.presets[i].presetName;

            GUI.backgroundColor = currentPresetName == presetName ? Color.cyan : Color.white;
            if (GUILayout.Button(presetName, GUILayout.Height(30)))
            {
                currentPresetName = presetName;
            }
        }

        GUI.backgroundColor = Color.white;
    }

    private void SpawnPreset()
    {
        GameObject prefab = library.GetPrefab(currentPresetName);
        if (prefab == null) return;

        string instanceName = StagePresetPrefix + currentPresetName;
        GameObject oldInScene = GameObject.Find(instanceName);
        if (oldInScene == null)
        {
            oldInScene = GameObject.Find(LegacyStagePresetPrefix + currentPresetName);
        }

        if (oldInScene != null) Undo.DestroyObjectImmediate(oldInScene);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = vTuberTarget.position + Vector3.up * heightOffset;
        instance.transform.rotation = Quaternion.Euler(0, vTuberTarget.eulerAngles.y + rotationOffset, 0);
        instance.name = instanceName;

        Undo.RegisterCreatedObjectUndo(instance, "Spawn Stage Preset");
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
