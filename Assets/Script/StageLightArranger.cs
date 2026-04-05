using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class StageLightArranger : MonoBehaviour
{
    public enum BuildMode
    {
        [InspectorName("絬 (Linear)")] Linear,
        [InspectorName("┓ (Arc)")] Arc,
        [InspectorName("SΡ絬 (SShape)")] SShape
    }

    [Header("膀娄皌竚")]
    [Tooltip("匡拒縊竝")]
    public BuildMode buildMode = BuildMode.Linear;

    [Tooltip("璶ネΘ縊ㄣ Prefab")]
    public GameObject lightPrefab;

    [Range(1, 30)]
    public int count = 8;

    [Header("絬家Α把计")]
    public float spacing = 2.0f;

    [Header("S家Α把计")]
    [Range(-10f, 10f)] public float sIntensity = 2.0f;
    public bool invertS = false;

    [Header("┓家Α把计")]
    public float radius = 5.0f;
    [Range(0, 360)] public float arcAngle = 90f;

    // --- 呸胯场だ玂ぃ跑 ---
    public void GenerateLights()
    {
        if (lightPrefab == null) return;
        List<Transform> children = new List<Transform>();
        foreach (Transform child in transform) children.Add(child);

        if (children.Count < count)
        {
            int toCreate = count - children.Count;
            for (int i = 0; i < toCreate; i++)
            {
                GameObject obj = Instantiate(lightPrefab, transform);
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(obj, "Create Light");
#endif
                children.Add(obj.transform);
            }
        }
        else if (children.Count > count)
        {
            int toDestroy = children.Count - count;
            for (int i = 0; i < toDestroy; i++)
            {
                GameObject targetObj = children[children.Count - 1 - i].gameObject;
#if UNITY_EDITOR
                UnityEditor.Undo.DestroyObjectImmediate(targetObj);
#else
                DestroyImmediate(targetObj);
#endif
            }
            children.RemoveRange(count, toDestroy);
        }

        List<SLMUnit> unitList = new List<SLMUnit>();
        for (int i = 0; i < count; i++)
        {
            Transform lightT = children[i];
            Vector3 targetPos = CalculatePosition(i);
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(lightT, "Move Light");
#endif
            lightT.localPosition = targetPos;
            lightT.localRotation = Quaternion.Euler(0, 0, 180f);
            lightT.name = string.Format("Light_{0}_{1}", buildMode, i);
            var unit = lightT.GetComponent<SLMUnit>();
            if (unit != null) unitList.Add(unit);
        }

        var controller = GetComponent<UnifiedStageController>();
        if (controller != null)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(controller, "Update Controller Units");
#endif
            controller.slmUnits = unitList.ToArray();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(controller);
#endif
        }
    }

    public Vector3 CalculatePosition(int index)
    {
        float t = (count <= 1) ? 0.5f : (float)index / (count - 1);
        if (buildMode == BuildMode.Arc)
        {
            float currentAngle = (t - 0.5f) * arcAngle;
            Quaternion rot = Quaternion.Euler(0, currentAngle, 0);
            return rot * Vector3.back * radius;
        }
        float totalWidth = (count - 1) * spacing;
        float xPos = -totalWidth / 2f + (index * spacing);
        float zOffset = 0;
        if (buildMode == BuildMode.SShape)
        {
            float multiplier = invertS ? -1f : 1f;
            zOffset = Mathf.Sin(t * Mathf.PI * 2) * sIntensity * multiplier;
        }
        return new Vector3(xPos, 0, zOffset);
    }

    public void ClearLights()
    {
        while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);
    }
}