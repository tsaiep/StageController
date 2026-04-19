using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 舞台燈組排列工具。
/// 支援 Linear / Arc / SShape / Polygon 四種主模式，
/// 以及 RingStacking（徑向/次軸複製）與 YLayerStacking（Y 軸垂直疊層）兩種複合模式。
/// 生成的燈光階層：StageLightArranger → Group_n → Light (SLMUnit)
/// </summary>
[ExecuteAlways]
public class StageLightArranger : MonoBehaviour
{
    // ============================================================
    //  Enums
    // ============================================================

    public enum BuildMode
    {
        [InspectorName("Line")]    Linear,
        [InspectorName("Arc")]     Arc,
        [InspectorName("S-Shape")] SShape,
        [InspectorName("Polygon")] Polygon
    }

    public enum CompoundMode
    {
        [InspectorName("None（單一分組）")]            None,
        [InspectorName("Ring Stacking（徑向或次軸複製）")] RingStacking,
        [InspectorName("Y-Layer Stacking（Y 軸疊層）")]   YLayerStacking
    }

    /// <summary>Linear 模式在 RingStacking 時，沿哪一個軸複製列</summary>
    public enum SecondaryAxis { Y, Z }

    public enum LightFacing
    {
        [InspectorName("朝下（預設）")]   Down,
        [InspectorName("朝向排列中心")] TowardCenter,
        [InspectorName("背向排列中心")] AwayFromCenter,
        [InspectorName("自訂 Euler")]   Custom
    }

    // ============================================================
    //  Fields
    // ============================================================

    [Header("Base Settings")]
    [Tooltip("排列模式")]
    public BuildMode buildMode = BuildMode.Linear;

    [Tooltip("每顆燈使用的 Prefab")]
    public GameObject lightPrefab;

    [Tooltip("單圈/單列的燈光數量")]
    [Range(1, 60)] public int count = 8;

    // ── Linear ──────────────────────────────────────────────────
    [Header("Line Settings")]
    [Tooltip("燈與燈之間的間距")]
    public float spacing = 2.0f;

    // ── Arc ─────────────────────────────────────────────────────
    [Header("Arc Settings")]
    [Tooltip("弧線半徑（arcAngle=360 即為完整圓）")]
    public float radius = 5.0f;
    [Tooltip("弧線總角度（1~360，360 = 完整圓）")]
    [Range(1f, 360f)] public float arcAngle = 180f;

    // ── S-Shape ──────────────────────────────────────────────────
    [Header("S-Shape Settings")]
    [Tooltip("S 型沿 X 軸的間距")]
    public float sSpacing = 2.0f;
    [Range(-10f, 10f)] public float sIntensity = 2.0f;
    public bool invertS = false;

    // ── Polygon ──────────────────────────────────────────────────
    [Header("Polygon Settings")]
    [Tooltip("正多邊形邊數（≥3）")]
    [Min(3)] public int polygonSides = 6;
    [Tooltip("外接圓半徑")]
    public float polygonRadius = 3.0f;
    [Tooltip("沿邊分佈（true）或僅擺頂點（false）")]
    public bool distributeAlongEdges = true;

    // ── Compound Mode ────────────────────────────────────────────
    [Header("Compound Mode")]
    [Tooltip("複合模式：None=單一分組 / RingStacking=徑向次軸複製 / YLayerStacking=Y 軸疊層")]
    public CompoundMode compoundMode = CompoundMode.None;

    [Tooltip("分組數量（圈數 / 層數），複合模式時有效")]
    [Min(2)] public int compoundGroupCount = 2;

    [Tooltip("RingStacking：每圈的半徑增量（Arc/Polygon）或列間距（Linear/SShape）\n" +
             "YLayerStacking：每層的 Y 軸間距")]
    public float compoundStep = 1.5f;

    [Tooltip("Linear RingStacking 的次軸方向（主軸固定為 Local X）")]
    public SecondaryAxis gridSecondaryAxis = SecondaryAxis.Z;

    // ── Arc Concentric：每圈是否依半徑比例增加燈數 ───────────────
    [Tooltip("Arc/Concentric：外圈是否依半徑比例增加燈數（同 PatternPlacer 的 scaleCountWithRadius）")]
    public bool scaleCountWithRadius = false;

    // ── Light Facing ─────────────────────────────────────────────
    [Header("Light Facing")]
    [Tooltip("燈光朝向設定")]
    public LightFacing lightFacing = LightFacing.Down;
    [Tooltip("Custom 模式下的完整 Euler 角度（X,Y,Z）")]
    public Vector3 customFacingEuler = Vector3.zero;
    [Tooltip("朝向/背向中心模式的 X 軸旋轉（度）\n" +
             "控制燈光的俯仰傾斜角度，Z 軸固定為 0")] 
    public float facingTiltX = 0f;

    // ── Light & Beam Settings ────────────────────────────────────
    [Header("Light & Beam Settings")]
    [Tooltip("Light component 的 Range")]
    public float lightRange = 12f;

    [Tooltip("VolumetricLightBeamHD 的 Side Softness")]
    public float vlbSideSoftness = 0.0001f;

    [Tooltip("VolumetricLightBeamHD 的 Attenuation Equation（0=Linear, 1=Quadratic）")]
    public VLB.AttenuationEquationHD vlbAttenuationEquation = VLB.AttenuationEquationHD.Linear;

    [Tooltip("VolumetricLightBeamHD 的 3D Noise > Enabled")]
    public VLB.NoiseMode vlbNoiseMode = VLB.NoiseMode.Disabled;

    [Tooltip("VolumetricLightBeamHD 的 3D Noise > Intensity")]
    [Range(0f, 1f)]
    public float vlbNoiseIntensity = 0.5f;

    // ============================================================
    //  Generate / Clear
    // ============================================================

    [ContextMenu("Generate Lights")]
    public void GenerateLights()
    {
        if (lightPrefab == null)
        {
            Debug.LogWarning("[StageLightArranger] lightPrefab 未指定。");
            return;
        }

        ClearLights();

        int groupCount = (compoundMode == CompoundMode.None) ? 1 : compoundGroupCount;

        // 收集所有生成的 SLMUnit，最後展平給 controller
        var allUnits = new List<SLMUnit>();

        // 計算排列的「中心點」（local space，用於 TowardCenter facing）
        // 對 Arc/Polygon 是 (0,0,0)；對 Linear/SShape 是排列中點
        Vector3 arrangeCenterLocal = ComputeArrangeCenterLocal();

        for (int g = 0; g < groupCount; g++)
        {
            // 建立 Group GameObject
            var groupGO = new GameObject($"Group_{g}");
            groupGO.transform.SetParent(transform, worldPositionStays: false);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(groupGO, "StageLightArranger Group");
#endif

            // 計算此 group 的局部 positions（在 Arranger local space）
            List<Vector3> positions = ComputeGroupPositions(g);

            int gSize = positions.Count;

            for (int i = 0; i < gSize; i++)
            {
                Vector3 localPos = positions[i];

                // ── 面向旋轉 ──
                Quaternion rot = ComputeFacingRotation(localPos, arrangeCenterLocal, g);

                // 生成燈光
                GameObject lightGO;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    lightGO = (GameObject)PrefabUtility.InstantiatePrefab(lightPrefab, groupGO.transform);
                    lightGO.transform.SetLocalPositionAndRotation(localPos, rot);
                    Undo.RegisterCreatedObjectUndo(lightGO, "StageLightArranger Light");
                }
                else
                {
                    lightGO = Instantiate(lightPrefab, groupGO.transform);
                    lightGO.transform.SetLocalPositionAndRotation(localPos, rot);
                }
#else
                lightGO = Instantiate(lightPrefab, groupGO.transform);
                lightGO.transform.SetLocalPositionAndRotation(localPos, rot);
#endif
                lightGO.name = $"Light_{buildMode}_{g:D1}_{i:D2}";

                // 套用 Light component 設定（遍歷所有子物件中的 Light）
                foreach (var lightComp in lightGO.GetComponentsInChildren<Light>(true))
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        var lightSO = new SerializedObject(lightComp);
                        lightSO.FindProperty("m_Range").floatValue = lightRange;
                        lightSO.ApplyModifiedProperties();
                    }
                    else
#endif
                    {
                        lightComp.range = lightRange;
                    }
                }

                // 套用 VolumetricLightBeamHD component 設定（遍歷所有子物件中的 VLB HD）
                foreach (var vlbHD in lightGO.GetComponentsInChildren<VLB.VolumetricLightBeamHD>(true))
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        var vlbSO = new SerializedObject(vlbHD);
                        vlbSO.FindProperty("m_SideSoftness").floatValue         = vlbSideSoftness;
                        vlbSO.FindProperty("m_AttenuationEquation").enumValueIndex = (int)vlbAttenuationEquation;
                        vlbSO.FindProperty("m_NoiseMode").enumValueIndex        = (int)vlbNoiseMode;
                        vlbSO.FindProperty("m_NoiseIntensity").floatValue       = vlbNoiseIntensity;
                        vlbSO.ApplyModifiedProperties();
                    }
                    else
#endif
                    {
                        vlbHD.sideSoftness       = vlbSideSoftness;
                        vlbHD.attenuationEquation = vlbAttenuationEquation;
                        vlbHD.noiseMode          = vlbNoiseMode;
                        vlbHD.noiseIntensity     = vlbNoiseIntensity;
                    }
                }

                // 寫入 SLMUnit 分組資訊
                var unit = lightGO.GetComponent<SLMUnit>();
                if (unit == null) unit = lightGO.AddComponent<SLMUnit>();

                unit.groupIndex   = g;
                unit.groupCount   = groupCount;
                unit.indexInGroup = i;
                unit.groupSize    = gSize;

                allUnits.Add(unit);
            }
        }

        // 更新 UnifiedStageController
        var controller = GetComponent<UnifiedStageController>();
        if (controller != null)
        {
#if UNITY_EDITOR
            Undo.RecordObject(controller, "Update Controller Units");
#endif
            controller.slmUnits = allUnits.ToArray();
#if UNITY_EDITOR
            EditorUtility.SetDirty(controller);
#endif
        }
    }

    [ContextMenu("Clear Lights")]
    public void ClearLights()
    {
        var toDestroy = new List<Transform>();
        foreach (Transform child in transform)
            toDestroy.Add(child);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            foreach (var t in toDestroy) Undo.DestroyObjectImmediate(t.gameObject);
        else
            foreach (var t in toDestroy) Destroy(t.gameObject);
#else
        foreach (var t in toDestroy) DestroyImmediate(t.gameObject);
#endif
    }

    // ============================================================
    //  Position Computation
    // ============================================================

    /// <summary>計算第 g 個分組的所有燈光位置（Arranger local space）</summary>
    List<Vector3> ComputeGroupPositions(int g)
    {
        // YLayerStacking：基礎形狀相同，只在 Y 軸上偏移
        float yOffset  = (compoundMode == CompoundMode.YLayerStacking) ? g * compoundStep : 0f;

        // RingStacking 用的基礎半徑/間距增量（對 Linear 是次軸偏移）
        float ringScale = (compoundMode == CompoundMode.RingStacking) ? g : 0;

        switch (buildMode)
        {
            case BuildMode.Linear:   return ComputeLinearPositions(g, ringScale, yOffset);
            case BuildMode.Arc:      return ComputeArcPositions(g, ringScale, yOffset);
            case BuildMode.SShape:   return ComputeSShapePositions(g, ringScale, yOffset);
            case BuildMode.Polygon:  return ComputePolygonPositions(g, ringScale, yOffset);
            default:                 return new List<Vector3>();
        }
    }

    List<Vector3> ComputeLinearPositions(int g, float ringScale, float yOffset)
    {
        var list = new List<Vector3>();
        float totalWidth = (count - 1) * spacing;

        // RingStacking：次軸偏移
        float secondOffset = (compoundMode == CompoundMode.RingStacking) ? g * compoundStep : 0f;

        for (int i = 0; i < count; i++)
        {
            float x = -totalWidth * 0.5f + i * spacing;
            float y = yOffset;
            float z = 0f;

            if (compoundMode == CompoundMode.RingStacking)
            {
                if (gridSecondaryAxis == SecondaryAxis.Y) y += secondOffset;
                else                                      z += secondOffset;
            }

            list.Add(new Vector3(x, y, z));
        }
        return list;
    }

    List<Vector3> ComputeArcPositions(int g, float ringScale, float yOffset)
    {
        var list = new List<Vector3>();

        float r = radius;
        if (compoundMode == CompoundMode.RingStacking)
            r = radius + g * compoundStep;

        // scaleCountWithRadius: 外圈燈數依比例增加
        int c = count;
        if (scaleCountWithRadius && compoundMode == CompoundMode.RingStacking && g > 0)
            c = Mathf.Max(3, Mathf.RoundToInt(count * (r / Mathf.Max(0.0001f, radius))));

        float halfArc = arcAngle * 0.5f;

        for (int i = 0; i < c; i++)
        {
            float t = (c <= 1) ? 0f : (float)i / (c - 1);
            float angle = Mathf.Deg2Rad * (-halfArc + t * arcAngle);

            // arcAngle=360 的完整圓：等間距不重疊
            if (Mathf.Approximately(arcAngle, 360f))
                angle = Mathf.Deg2Rad * ((float)i / c * 360f);

            float x = Mathf.Sin(angle) * r;
            float z = Mathf.Cos(angle) * r;   // XZ 平面，圓弧面向 +Z 方向
            list.Add(new Vector3(x, yOffset, z));
        }
        return list;
    }

    List<Vector3> ComputeSShapePositions(int g, float ringScale, float yOffset)
    {
        var list = new List<Vector3>();
        float totalWidth = (count - 1) * sSpacing;

        float secondOffset = (compoundMode == CompoundMode.RingStacking) ? g * compoundStep : 0f;

        for (int i = 0; i < count; i++)
        {
            float tNorm = (count <= 1) ? 0.5f : (float)i / (count - 1);
            float x = -totalWidth * 0.5f + i * sSpacing;
            float multiplier = invertS ? -1f : 1f;
            float zWave = Mathf.Sin(tNorm * Mathf.PI * 2f) * sIntensity * multiplier;

            float y = yOffset;
            float z = zWave;

            if (compoundMode == CompoundMode.RingStacking)
                z += secondOffset; // 在 Z 方向（S 波偏移軸）複製

            list.Add(new Vector3(x, y, z));
        }
        return list;
    }

    List<Vector3> ComputePolygonPositions(int g, float ringScale, float yOffset)
    {
        var list = new List<Vector3>();

        float r = polygonRadius;
        if (compoundMode == CompoundMode.RingStacking)
            r = polygonRadius + g * compoundStep;

        // 建立頂點
        var verts = new List<Vector3>(polygonSides);
        for (int i = 0; i < polygonSides; i++)
        {
            float angle = Mathf.Deg2Rad * ((float)i / polygonSides * 360f);
            verts.Add(new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
        }

        if (!distributeAlongEdges)
        {
            // 僅頂點
            foreach (var v in verts)
                list.Add(v + Vector3.up * yOffset);
        }
        else
        {
            // 沿邊均勻分佈，整圈共 count 顆（不重疊）
            float perimeter = 0f;
            for (int i = 0; i < polygonSides; i++)
                perimeter += Vector3.Distance(verts[i], verts[(i + 1) % polygonSides]);

            float step = perimeter / Mathf.Max(1, count);
            float walked = 0f;
            int placed = 0;
            int edge = 0;
            float edgePos = 0f;

            while (placed < count)
            {
                Vector3 a = verts[edge];
                Vector3 b = verts[(edge + 1) % polygonSides];
                float edgeLen = Vector3.Distance(a, b);

                float nextTarget = edgePos + (step - walked);
                if (nextTarget > edgeLen + 1e-5f)
                {
                    edge = (edge + 1) % polygonSides;
                    walked += edgeLen - edgePos;
                    edgePos = 0f;
                    continue;
                }

                float localT = Mathf.Clamp01(nextTarget / edgeLen);
                Vector3 pos = Vector3.Lerp(a, b, localT);
                list.Add(pos + Vector3.up * yOffset);
                placed++;

                edgePos = nextTarget;
                walked = 0f;
                if (edgePos >= edgeLen - 1e-5f)
                {
                    edge = (edge + 1) % polygonSides;
                    edgePos = 0f;
                }
            }
        }
        return list;
    }

    // ============================================================
    //  Facing Rotation
    // ============================================================

    /// <summary>排列的中心點（Arranger local space），用於 TowardCenter/AwayFromCenter 計算</summary>
    Vector3 ComputeArrangeCenterLocal()
    {
        switch (buildMode)
        {
            case BuildMode.Arc:
            case BuildMode.Polygon:
                return Vector3.zero; // XZ 平面原點

            case BuildMode.Linear:
            case BuildMode.SShape:
                return Vector3.zero; // 排列已以原點置中
            default:
                return Vector3.zero;
        }
    }

    /// <summary>計算單顆燈光的面向旋轉（Inspector 顯示：X=0, Y=Pan, Z=Tilt）</summary>
    Quaternion ComputeFacingRotation(Vector3 lightLocalPos, Vector3 centerLocal, int groupIndex)
    {
        switch (lightFacing)
        {
            case LightFacing.Down:
                return Quaternion.Euler(0f, 0f, 180f);

            case LightFacing.Custom:
                return Quaternion.Euler(customFacingEuler);

            case LightFacing.TowardCenter:
            case LightFacing.AwayFromCenter:
            {
                // 在 XZ 平面計算水平方向（忽略 Y，避免 YLayerStacking 疊層傾斜）
                float dx = lightLocalPos.x - centerLocal.x;
                float dz = lightLocalPos.z - centerLocal.z;

                if (dx * dx + dz * dz < 0.0001f)
                    return Quaternion.Euler(facingTiltX, 0f, 0f); // 在圓心，退化為純 X 傾斜

                // 計算 Y 旋轉角度：Atan2(x, z) 給出從 +Z 朝向 (dx, dz) 所需的 Y 旋轉（度）
                // TowardCenter 方向 = 由燈位指向圓心 → 取反向
                float yAngle = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                if (lightFacing == LightFacing.TowardCenter)
                    yAngle += 180f; // 朝向圓心 = 反轉背向中心方向

                // 結果：X=使用者設定的俯仰傾斜，Y=水平Pan朝向圓心，Z=0（固定）
                return Quaternion.Euler(facingTiltX, yAngle, 0f);
            }

            default:
                return Quaternion.Euler(0f, 0f, 180f);
        }
    }
}

// ================================================================
//  Custom Editor
// ================================================================
#if UNITY_EDITOR

[CustomEditor(typeof(StageLightArranger))]
public class StageLightArrangerEditor : Editor
{
    SerializedProperty buildModeProp, lightPrefabProp, countProp;
    SerializedProperty spacingProp;
    SerializedProperty radiusProp, arcAngleProp, scaleCountWithRadiusProp;
    SerializedProperty sSpacingProp, sIntensityProp, invertSProp;
    SerializedProperty polygonSidesProp, polygonRadiusProp, distributeAlongEdgesProp;
    SerializedProperty compoundModeProp, compoundGroupCountProp, compoundStepProp, gridSecondaryAxisProp;
    SerializedProperty lightFacingProp, customFacingEulerProp, facingTiltXProp;
    SerializedProperty lightRangeProp, vlbSideSoftnessProp, vlbAttenuationEquationProp, vlbNoiseModeProp, vlbNoiseIntensityProp;

    static bool foldBase = true, foldShape = true, foldCompound = true, foldFacing = true, foldBeamSettings = true;

    void OnEnable()
    {
        buildModeProp             = serializedObject.FindProperty("buildMode");
        lightPrefabProp           = serializedObject.FindProperty("lightPrefab");
        countProp                 = serializedObject.FindProperty("count");
        spacingProp               = serializedObject.FindProperty("spacing");
        radiusProp                = serializedObject.FindProperty("radius");
        arcAngleProp              = serializedObject.FindProperty("arcAngle");
        scaleCountWithRadiusProp  = serializedObject.FindProperty("scaleCountWithRadius");
        sSpacingProp              = serializedObject.FindProperty("sSpacing");
        sIntensityProp            = serializedObject.FindProperty("sIntensity");
        invertSProp               = serializedObject.FindProperty("invertS");
        polygonSidesProp          = serializedObject.FindProperty("polygonSides");
        polygonRadiusProp         = serializedObject.FindProperty("polygonRadius");
        distributeAlongEdgesProp  = serializedObject.FindProperty("distributeAlongEdges");
        compoundModeProp          = serializedObject.FindProperty("compoundMode");
        compoundGroupCountProp    = serializedObject.FindProperty("compoundGroupCount");
        compoundStepProp          = serializedObject.FindProperty("compoundStep");
        gridSecondaryAxisProp     = serializedObject.FindProperty("gridSecondaryAxis");
        lightFacingProp           = serializedObject.FindProperty("lightFacing");
        customFacingEulerProp     = serializedObject.FindProperty("customFacingEuler");
        facingTiltXProp           = serializedObject.FindProperty("facingTiltX");
        lightRangeProp            = serializedObject.FindProperty("lightRange");
        vlbSideSoftnessProp       = serializedObject.FindProperty("vlbSideSoftness");
        vlbAttenuationEquationProp = serializedObject.FindProperty("vlbAttenuationEquation");
        vlbNoiseModeProp          = serializedObject.FindProperty("vlbNoiseMode");
        vlbNoiseIntensityProp     = serializedObject.FindProperty("vlbNoiseIntensity");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var arr = (StageLightArranger)target;
        var mode = (StageLightArranger.BuildMode)buildModeProp.enumValueIndex;
        var compound = (StageLightArranger.CompoundMode)compoundModeProp.enumValueIndex;
        var facing = (StageLightArranger.LightFacing)lightFacingProp.enumValueIndex;

        // ── Base ────────────────────────────────────────────────
        foldBase = EditorGUILayout.BeginFoldoutHeaderGroup(foldBase, "Base Settings");
        if (foldBase)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.PropertyField(buildModeProp,   new GUIContent("Build Mode"));
                EditorGUILayout.PropertyField(lightPrefabProp, new GUIContent("Light Prefab"));
                if (!lightPrefabProp.objectReferenceValue)
                    EditorGUILayout.HelpBox("請指定 Light Prefab。", MessageType.Warning);
                EditorGUILayout.PropertyField(countProp, new GUIContent("Count（每圈 / 每列燈數）"));
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── Shape Settings ───────────────────────────────────────
        foldShape = EditorGUILayout.BeginFoldoutHeaderGroup(foldShape, "Shape Settings");
        if (foldShape)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                switch (mode)
                {
                    case StageLightArranger.BuildMode.Linear:
                        EditorGUILayout.PropertyField(spacingProp, new GUIContent("Spacing（燈距）"));
                        break;

                    case StageLightArranger.BuildMode.Arc:
                        EditorGUILayout.PropertyField(radiusProp,   new GUIContent("Radius（半徑）"));
                        EditorGUILayout.PropertyField(arcAngleProp, new GUIContent("Arc Angle（弧度，360=完整圓）"));
                        break;

                    case StageLightArranger.BuildMode.SShape:
                        EditorGUILayout.PropertyField(sSpacingProp,  new GUIContent("Spacing（燈距）"));
                        EditorGUILayout.PropertyField(sIntensityProp, new GUIContent("S Intensity"));
                        EditorGUILayout.PropertyField(invertSProp,    new GUIContent("Invert S"));
                        break;

                    case StageLightArranger.BuildMode.Polygon:
                        EditorGUILayout.PropertyField(polygonSidesProp,         new GUIContent("Polygon Sides（邊數）"));
                        EditorGUILayout.PropertyField(polygonRadiusProp,        new GUIContent("Polygon Radius（外接圓半徑）"));
                        EditorGUILayout.PropertyField(distributeAlongEdgesProp, new GUIContent("Distribute Along Edges"));
                        break;
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── Compound Mode ────────────────────────────────────────
        foldCompound = EditorGUILayout.BeginFoldoutHeaderGroup(foldCompound, "Compound Mode（分組複製）");
        if (foldCompound)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.PropertyField(compoundModeProp, new GUIContent("Compound Mode"));

                if (compound != StageLightArranger.CompoundMode.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(compoundGroupCountProp, new GUIContent("Group Count（分組數）"));

                    string stepLabel = compound == StageLightArranger.CompoundMode.YLayerStacking
                        ? "Layer Spacing（Y 層間距）"
                        : (mode == StageLightArranger.BuildMode.Linear
                            ? "Row Spacing（列間距）"
                            : "Radius Step（每圈半徑增量）");
                    EditorGUILayout.PropertyField(compoundStepProp, new GUIContent(stepLabel));

                    // Arc + RingStacking：可選 scaleCountWithRadius
                    if (mode == StageLightArranger.BuildMode.Arc &&
                        compound == StageLightArranger.CompoundMode.RingStacking)
                    {
                        EditorGUILayout.PropertyField(scaleCountWithRadiusProp,
                            new GUIContent("Scale Count With Radius（外圈燈數比例增加）"));
                    }

                    // Linear + RingStacking：次軸選擇
                    if (mode == StageLightArranger.BuildMode.Linear &&
                        compound == StageLightArranger.CompoundMode.RingStacking)
                    {
                        EditorGUILayout.PropertyField(gridSecondaryAxisProp,
                            new GUIContent("Secondary Axis（複製方向）"));
                    }
                    EditorGUI.indentLevel--;

                    // 分組數提示
                    if (compoundGroupCountProp.intValue >= 2)
                    {
                        string desc = compound == StageLightArranger.CompoundMode.YLayerStacking
                            ? $"將生成 {compoundGroupCountProp.intValue} 層，每層 {countProp.intValue} 顆，共 {compoundGroupCountProp.intValue * countProp.intValue} 顆燈。"
                            : $"將生成 {compoundGroupCountProp.intValue} 圈，共 {compoundGroupCountProp.intValue * countProp.intValue} 顆燈（Arc 依比例增加時數量可能不同）。";
                        EditorGUILayout.HelpBox(desc, MessageType.Info);
                    }
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── Light Facing ─────────────────────────────────────────
        foldFacing = EditorGUILayout.BeginFoldoutHeaderGroup(foldFacing, "Light Facing（燈光面向）");
        if (foldFacing)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.PropertyField(lightFacingProp, new GUIContent("Facing Mode"));

                if (facing == StageLightArranger.LightFacing.Custom)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(customFacingEulerProp, new GUIContent("Custom Euler（度）"));
                    EditorGUI.indentLevel--;
                }

                if (facing == StageLightArranger.LightFacing.TowardCenter ||
                    facing == StageLightArranger.LightFacing.AwayFromCenter)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(facingTiltXProp,
                        new GUIContent("Tilt X（俯仰角度）",
                            "結果旋轉為 Euler(X=此值, Y=水平朝向圓心, Z=0)\n" +
                            "Y 軸自動計算指向圓心，Z 固定為 0"));
                    EditorGUI.indentLevel--;
                    EditorGUILayout.HelpBox(
                        "Inspector 顯示：X=Tilt俯仰（此處設定）  |  Y=水平Pan（自動計算朝向圓心）  |  Z=0（固定）\n" +
                        "YLayer 疊層模式：Y 偏移不影響水平Pan計算，各層旋轉一致。",
                        MessageType.Info);
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── Light & Beam Settings ────────────────────────────────
        foldBeamSettings = EditorGUILayout.BeginFoldoutHeaderGroup(foldBeamSettings, "Light & Beam Settings（燈光統一設定）");
        if (foldBeamSettings)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Light Component", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(lightRangeProp, new GUIContent("Range（範圍）"));
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Volumetric Light Beam HD", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(vlbSideSoftnessProp,        new GUIContent("Side Softness"));
                EditorGUILayout.PropertyField(vlbAttenuationEquationProp,  new GUIContent("Attenuation Equation"));
                EditorGUILayout.LabelField("3D Noise", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(vlbNoiseModeProp,       new GUIContent("Enabled"));
                EditorGUILayout.PropertyField(vlbNoiseIntensityProp,  new GUIContent("Intensity"));
                EditorGUI.indentLevel--;
                EditorGUI.indentLevel--;

                EditorGUILayout.HelpBox("以上設定將在點擊 Generate Lights 時套用到所有已生成燈光的對應 Component。", MessageType.Info);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── Buttons ───────────────────────────────────────────────
        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button("Generate Lights", GUILayout.Height(30)))
                arr.GenerateLights();

            GUI.backgroundColor = new Color(1f, 0.5f, 0.4f);
            if (GUILayout.Button("Clear Lights", GUILayout.Height(30)))
                arr.ClearLights();

            GUI.backgroundColor = Color.white;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
