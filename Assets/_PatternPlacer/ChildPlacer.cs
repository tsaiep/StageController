using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChildPlacer : MonoBehaviour
{
    [Header("Object to Place")]
    public GameObject prefab;

    [Header("Match Transform")]
    public bool matchChildRotation = true;
    public bool matchChildScale = false; // 一般不建議自動換縮放，視需求開啟

    // 生成的物件會放在此容器下，方便清除與避免再次被當作「第一層子物件」快照目標
    const string kContainerName = "[ChildPlacer Generated]";

    Transform GetOrCreateContainer()
    {
        var t = transform.Find(kContainerName);
        if (t != null) return t;

        var go = new GameObject(kContainerName);
        go.transform.SetParent(transform, false);
#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(go, "Create ChildPlacer Container");
#endif
        return go.transform;
    }

    /// <summary>
    /// 依「第一層子物件」的位置生成一份 prefab
    /// </summary>
    [ContextMenu("Generate")]
    public void GenerateAtChildren()
    {
        if (prefab == null)
        {
            Debug.LogWarning("[ChildPlacer] 請先指定 Prefab。", this);
            return;
        }

        // 1) 做快照：只抓第一層，且排除容器本身
        var snapshot = new List<Transform>(transform.childCount);
        foreach (Transform child in transform)
        {
            if (child.name == kContainerName) continue; // 排除容器
            snapshot.Add(child);
        }

        if (snapshot.Count == 0)
        {
            Debug.Log("[ChildPlacer] 此物件沒有第一層子物件可用來生成。", this);
            return;
        }

        // 2) 開始生成
        var container = GetOrCreateContainer();

        foreach (var child in snapshot)
        {
            GameObject instance;

#if UNITY_EDITOR
            // 在編輯器：若是 Prefab Asset，用 PrefabUtility 來保留 Prefab 連結
            if (!Application.isPlaying && PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                Undo.RegisterCreatedObjectUndo(instance, "ChildPlacer Generate");
            }
            else
            {
                instance = Instantiate(prefab, container);
                if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(instance, "ChildPlacer Generate");
            }
#else
            instance = Instantiate(prefab, container);
#endif

            // 設定位置/旋轉/縮放
            instance.transform.position = child.position;
            instance.transform.rotation = matchChildRotation ? child.rotation : instance.transform.rotation;

            if (matchChildScale)
            {
                // 這裡用「世界縮放」近似，若你的層級縮放較複雜，請改成你想要的邏輯
                instance.transform.localScale = child.lossyScale;
            }

            instance.name = $"{prefab.name}_at_{child.name}";
        }

        Debug.Log($"[ChildPlacer] 已生成 {snapshot.Count} 個實例於第一層子物件位置。", this);
    }

    /// <summary>
    /// 一鍵清除生成的所有物件
    /// </summary>
    [ContextMenu("Clear Generated")]
    public void ClearGenerated()
    {
        var container = transform.Find(kContainerName);
        if (container == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.DestroyObjectImmediate(container.gameObject);
        else
            Destroy(container.gameObject);
#else
        Destroy(container.gameObject);
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ChildPlacer))]
    public class ChildPlacerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var script = (ChildPlacer)target;

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(script.prefab == null))
                {
                    if (GUILayout.Button("Generate", GUILayout.Height(28)))
                        script.GenerateAtChildren();
                }

                if (GUILayout.Button("Clear Generated", GUILayout.Height(28)))
                    script.ClearGenerated();
            }

            EditorGUILayout.HelpBox(
                "Safety note: This tool first takes a snapshot of first-level children before generating to prevent infinite spawning.\n" +
                "Generated objects are placed under the child container \"[ChildPlacer Generated]\" for easy one-click cleanup.",
                MessageType.Info
            );

        }
    }
#endif
}
