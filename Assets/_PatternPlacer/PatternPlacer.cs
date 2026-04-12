using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 擺放複製物的工具：直線、圓、正多邊形，支援導數(切向)對齊、編號、與重複拼接。
/// 掛在場景中一個空物件上，指定 Prefab 後按下 Generate。
/// </summary>
[ExecuteAlways]
public class PatternPlacer : MonoBehaviour
{
    public enum PatternMode { Line, Circle, Polygon }
    public enum AlignAxis { X, Y, Z, NegX, NegY, NegZ } // 讓指定軸對齊切向

    [System.Serializable]
    public class InstanceId : MonoBehaviour
    {
        public string groupId;
        public int index;        // 此批次的流水號
        public int ringOrRow;    // 針對同心圓的「第幾圈」或網格的「第幾列」
        public int col;          // 針對網格的「第幾行」
    }

    [Header("Basic")]
    [Tooltip("要複製的目標(通常是 Prefab)")]
    public GameObject targetPrefab;
    [Tooltip("此批次的群組ID(之後腳本可用它來取得這批實例)")]
    public string groupId = "BatchA";

    [Header("Placement")]
    public PatternMode mode = PatternMode.Line;
    [Min(1)] public int count = 8;               // 單列或單圈/單多邊形的數量
    [Tooltip("間距：Line=沿切線距離；Circle/Polygon=半徑或邊外接半徑")]
    public float spacing = 1.0f;

    [Header("Orientation (Derivative/Tangent)")]
    [Tooltip("是否將其中一個本地軸對齊排列曲線的導數(切向)")]
    public bool alignToTangent = true;
    [Tooltip("選擇哪一個本地軸對齊切向")]
    public AlignAxis axisToAlign = AlignAxis.Z;
    [Tooltip("若需要，可再施加額外旋轉(度)")]
    public Vector3 extraEuler;

    [Header("Line → Grid (Tiling)")]
    [Tooltip("將直線排列拼接成網格/棋盤")]
    public bool tileLineToGrid = false;
    [Min(1)] public int gridRows = 3;
    [Min(1)] public int gridCols = 8;
    [Tooltip("行距(第二軸方向)")]
    public float gridRowSpacing = 1.0f;
    [Tooltip("棋盤格效果：偶數列水平位移半格")]
    public bool checkerOffset = true;

    [Header("Circle → Concentric Rings")]
    [Tooltip("將圓圈排列擴展為同心圓")]
    public bool concentricRings = false;
    [Min(1)] public int ringCount = 3;
    [Tooltip("每一圈之間的半徑增量")]
    public float ringRadiusStep = 1.0f;
    [Tooltip("每一圈的物件數是否隨半徑線性增加(避免內圈過擠/外圈過疏)")]
    public bool scaleCountWithRadius = true;

    [Header("Polygon (Regular) → Multi-Rings")]
    [Min(3)] public int polygonSides = 6;
    [Tooltip("多邊形：在每條邊上平均分佈(含頂點)；count 代表整個外圈總數，而非單邊數")]
    public bool distributeAlongEdges = true;
    [Tooltip("多層正多邊形(同心)")]
    public bool polygonMultiRings = false;
    [Min(1)] public int polygonRingCount = 2;
    public float polygonRingRadiusStep = 1.0f;

    [Header("Polygon Edge Gaps & Centering")]
    [Tooltip("啟用後：每條邊頭尾會留白 edgeTrim01 的比例，並在可用長度內『置中』分佈(每顆落在自身區段的中央)。")]
    public bool polygonEdgeGap = false;

    [Range(0f, 0.49f)]
    [Tooltip("每條邊頭尾留白比例(0~0.49)。例如 0.1 代表兩端各留 10%，只在中間 80% 擺放。")]
    public float edgeTrim01 = 0.1f;

    [Min(0)]
    [Tooltip("固定每條邊的實例數；0=自動，使用全域 Count 依『可用邊長』加權分配。")]
    public int perEdgeFixedCount = 0;

    // 供外部腳本快速存取此批生成物
    public readonly List<GameObject> spawned = new List<GameObject>();

    // —— 入口 —— //
    [ContextMenu("Generate")]
    public void Generate()
    {
        if (!targetPrefab)
        {
            Debug.LogWarning("[PatternPlacer] targetPrefab 未指定。");
            return;
        }

        EnsureContainer();
        Clear();

        switch (mode)
        {
            case PatternMode.Line:
                GenerateLineOrGrid();
                break;
            case PatternMode.Circle:
                GenerateCircleOrConcentric();
                break;
            case PatternMode.Polygon:
                GeneratePolygonOrMulti();
                break;
        }
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        spawned.Clear();
        if (!container) return;

        // 只清除本工具生成的孩子
        var toDelete = new List<Transform>();
        foreach (Transform child in container)
        {
            toDelete.Add(child);
        }
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            foreach (var t in toDelete)
                Undo.DestroyObjectImmediate(t.gameObject);
        }
        else
        {
            foreach (var t in toDelete)
                Destroy(t.gameObject);
        }
#else
        foreach (var t in toDelete)
            DestroyImmediate(t.gameObject);
#endif
    }

    // ===== 生成實作 =====

    void GenerateLineOrGrid()
    {
        int idx = 0;

        if (!tileLineToGrid)
        {
            // 單一直線：沿著本物件的 +X 方向視為切線；你也可以改成世界軸
            Vector3 basePos = transform.position;
            Vector3 tangent = transform.right; // 導數(切向)
            Vector3 step = tangent.normalized * spacing;

            for (int i = 0; i < count; i++)
            {
                Vector3 p = basePos + step * i;
                Quaternion rot = ComputeRotationFromTangent(tangent);
                SpawnOne(p, rot, idx++, rowOrRing: 0, col: i);
            }
        }
        else
        {
            // 網格/棋盤：X 方向為列內水平，Z 方向為列與列間距
            Vector3 right = transform.right;
            Vector3 forward = transform.forward;

            Vector3 stepX = right.normalized * spacing;
            Vector3 stepZ = forward.normalized * gridRowSpacing;

            for (int r = 0; r < gridRows; r++)
            {
                float offset = (checkerOffset && (r % 2 == 1)) ? 0.5f : 0f; // 半格偏移
                for (int c = 0; c < gridCols; c++)
                {
                    Vector3 p = transform.position + stepZ * r + stepX * (c + offset);
                    // 導數方向：以 X 方向為切向(每列方向)
                    Quaternion rot = ComputeRotationFromTangent(stepX);
                    SpawnOne(p, rot, idx++, rowOrRing: r, col: c);
                }
            }
        }
    }

    void GenerateCircleOrConcentric()
    {
        int idx = 0;

        if (!concentricRings)
        {
            float radius = Mathf.Max(0.0001f, spacing);
            PlaceCircleRing(radius, count, ringIndex: 0, ref idx);
        }
        else
        {
            for (int r = 0; r < ringCount; r++)
            {
                float radius = Mathf.Max(0.0001f, spacing + r * ringRadiusStep);
                int c = count;

                if (scaleCountWithRadius && r > 0)
                {
                    // 依半徑線性增加密度(簡單策略)
                    c = Mathf.Max(3, Mathf.RoundToInt(count * (radius / Mathf.Max(0.0001f, spacing))));
                }
                PlaceCircleRing(radius, c, r, ref idx);
            }
        }
    }

    void PlaceCircleRing(float radius, int itemCount, int ringIndex, ref int globalIndex)
    {
        Vector3 center = transform.position;
        // 在 XZ 平面擺放；如需其他平面可自行修改
        for (int i = 0; i < itemCount; i++)
        {
            float t = (i / (float)itemCount) * Mathf.PI * 2f;
            Vector3 pos = center + new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * radius;

            // 切向(導數)：圓的切線為沿 t 方向的微分，這裡用圓周切向向量
            Vector3 tangent = new Vector3(-Mathf.Sin(t), 0f, Mathf.Cos(t)); // 與半徑正交
            Quaternion rot = ComputeRotationFromTangent(tangent);

            SpawnOne(pos, rot, globalIndex++, rowOrRing: ringIndex, col: i);
        }
    }

    void GeneratePolygonOrMulti()
    {
        int idx = 0;

        int rings = polygonMultiRings ? Mathf.Max(1, polygonRingCount) : 1;
        for (int r = 0; r < rings; r++)
        {
            float R = spacing + r * polygonRingRadiusStep; // 外接圓半徑
            if (R <= 0f) R = 0.0001f;

            if (distributeAlongEdges)
            {
                // 建立正多邊形頂點
                var verts = BuildRegularPolygonVertices(polygonSides, R);

                // 若啟用「邊端留白 & 置中」或「每邊固定數量」，使用新的置中演算法；
                // 否則沿周長等步長行走的舊法。
                if (polygonEdgeGap)
                {
                    int total = (perEdgeFixedCount > 0) ? perEdgeFixedCount * verts.Count : count;
                    PlaceAlongPolygonEdgesWithTrimAndCenter(
                        verts,
                        total,
                        edgeTrim01,
                        perEdgeFixedCount,
                        r,
                        ref idx
                    );
                }
                else
                {
                    // —— 舊：沿整個周長平均鋪點（含邊界）
                    float perimeter = 0f;
                    for (int i = 0; i < polygonSides; i++)
                        //%是序號頭尾相接的方式
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

                        if (edgePos + step - walked > edgeLen + 1e-5f)
                        {
                            edge = (edge + 1) % polygonSides;
                            walked += edgeLen - edgePos;
                            edgePos = 0f;
                            continue;
                        }

                        float t = (edgePos + step - walked) / edgeLen;
                        t = Mathf.Clamp01(t);
                        Vector3 pos = Vector3.Lerp(a, b, t);

                        Vector3 tangent = (b - a).normalized; // 導數=邊向量
                        Quaternion rot = ComputeRotationFromTangent(tangent);

                        SpawnOne(pos, rot, idx++, rowOrRing: r, col: placed);
                        placed++;

                        edgePos += (step - walked);
                        walked = 0f;

                        if (edgePos >= edgeLen - 1e-5f)
                        {
                            edge = (edge + 1) % polygonSides;
                            edgePos = 0f;
                        }
                    }
                }
            }
            else
            {
                // 只擺在頂點：count 會被覆蓋為 sides
                var verts = BuildRegularPolygonVertices(polygonSides, R);
                for (int i = 0; i < verts.Count; i++)
                {
                    Vector3 a = verts[i];
                    Vector3 b = verts[(i + 1) % verts.Count];
                    Vector3 tangent = (b - a).normalized;
                    SpawnOne(verts[i], ComputeRotationFromTangent(tangent), idx++, rowOrRing: r, col: i);
                }
            }
        }
    }

    /// <summary>
    /// 在每條邊『兩端留白 edgeTrim01』後的可用區間內，將 n 顆點「置中分佈」：
    /// 也就是把可用區間平均切成 n 份，每顆落在自己區段的中央（不貼邊界）。
    /// 可選擇每邊固定數量，或以全域 totalCount 依可用長度加權分配（最大餘數補齊）。
    /// </summary>
    void PlaceAlongPolygonEdgesWithTrimAndCenter(
        List<Vector3> verts,
        int totalCount,
        float trim01,
        int fixedPerEdge,
        int ringIndex,
        ref int globalIndex)
    {
        int sides = verts.Count;
        if (sides < 3) return;

        // 每條邊原始長度 & 可用長度（扣掉兩端留白）
        var edgeLens = new float[sides];
        var usableLens = new float[sides];
        float usableSum = 0f;

        float tStart = Mathf.Clamp01(trim01);
        float tEnd = 1f - tStart;
        float span = Mathf.Max(0f, tEnd - tStart);

        for (int e = 0; e < sides; e++)
        {
            Vector3 a = verts[e];
            Vector3 b = verts[(e + 1) % sides];
            float L = Vector3.Distance(a, b);
            edgeLens[e] = L;

            float usable = Mathf.Max(0f, span * L);
            usableLens[e] = usable;
            usableSum += usable;
        }

        if (fixedPerEdge <= 0 && (totalCount <= 0 || usableSum <= 1e-6f))
            return;

        // 計算每條邊要放的數量
        var counts = new int[sides];

        if (fixedPerEdge > 0)
        {
            for (int e = 0; e < sides; e++)
                counts[e] = (usableLens[e] > 1e-6f) ? fixedPerEdge : 0;
        }
        else
        {
            // 依可用長度加權分配 totalCount，先取地板，再用最大餘數法補滿
            float[] quotas = new float[sides];
            int sumFloor = 0;
            for (int e = 0; e < sides; e++)
            {
                float q = (usableLens[e] / usableSum) * totalCount;
                quotas[e] = q;
                int f = Mathf.FloorToInt(q);
                counts[e] = f;
                sumFloor += f;
            }

            int remain = Mathf.Max(0, totalCount - sumFloor);
            if (remain > 0)
            {
                var order = new List<int>(sides);
                for (int e = 0; e < sides; e++) order.Add(e);
                order.Sort((i, j) =>
                {
                    float fi = quotas[i] - Mathf.Floor(quotas[i]);
                    float fj = quotas[j] - Mathf.Floor(quotas[j]);
                    return fj.CompareTo(fi);
                });

                for (int k = 0; k < order.Count && remain > 0; k++)
                {
                    if (usableLens[order[k]] <= 1e-6f) continue;
                    counts[order[k]] += 1;
                    remain--;
                }
            }
        }

        // 在每條邊「置中對稱」放置：把 [tStart, tEnd] 等分為 n 區段，取各區段中央
        int colSerialOnRing = 0;

        for (int e = 0; e < sides; e++)
        {
            int n = counts[e];
            if (n <= 0 || usableLens[e] <= 1e-6f) continue;

            Vector3 a = verts[e];
            Vector3 b = verts[(e + 1) % sides];
            Vector3 tangent = (b - a).normalized;
            Quaternion rot = ComputeRotationFromTangent(tangent);

            for (int k = 0; k < n; k++)
            {
                // 中點取樣：避免貼到 tStart/tEnd 邊界
                float t = tStart + ((k + 0.5f) / n) * span;
                Vector3 pos = Vector3.Lerp(a, b, t);
                SpawnOne(pos, rot, globalIndex++, ringIndex, colSerialOnRing++);
            }
        }
    }

    List<Vector3> BuildRegularPolygonVertices(int sides, float radius)
    {
        var list = new List<Vector3>(sides);
        Vector3 center = transform.position;
        // 放在 XZ 平面
        for (int i = 0; i < sides; i++)
        {
            float t = (i / (float)sides) * Mathf.PI * 2f;
            Vector3 v = center + new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * radius;
            list.Add(v);
        }
        return list;
    }

    // ===== 工具函式 =====

    Quaternion ComputeRotationFromTangent(Vector3 tangent)
    {
        // 若未要求對齊，回傳只帶 ExtraEuler 的旋轉
        if (!alignToTangent)
            return Quaternion.Euler(extraEuler);

        // 1) 目標方向（世界空間）
        Vector3 fwd = tangent.normalized;

        // 2) 穩定參考 Up：圓在 XZ 平面 → 用 transform.up（或 Vector3.up）
        Vector3 upRef = transform.up;
        if (Mathf.Abs(Vector3.Dot(fwd, upRef)) > 0.999f)
            upRef = transform.right;

        // 3) 先做「Z 對齊切向 & Up 穩定」的旋轉（世界空間）
        Quaternion lookZ = Quaternion.LookRotation(fwd, upRef);

        // 4) 把「使用者選的對齊軸」預先旋到 Z 軸
        Vector3 srcLocalAxis =
            axisToAlign == AlignAxis.X ?  Vector3.right :
            axisToAlign == AlignAxis.Y ?  Vector3.up :
            axisToAlign == AlignAxis.Z ?  Vector3.forward :
            axisToAlign == AlignAxis.NegX ? -Vector3.right :
            axisToAlign == AlignAxis.NegY ? -Vector3.up :
            -Vector3.forward;

        Quaternion pre = Quaternion.FromToRotation(srcLocalAxis, Vector3.forward);

        // 5) 組合：把該本地軸對齊切向，再加上額外歐拉
        Quaternion q = lookZ * pre;
        return q * Quaternion.Euler(extraEuler);
    }

    void SpawnOne(Vector3 position, Quaternion rotation, int index, int rowOrRing, int col)
    {
        if (!container) EnsureContainer();

#if UNITY_EDITOR
        GameObject go;
        if (!Application.isPlaying)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab, container);
            go.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(go, "PatternPlacer Spawn");
        }
        else
        {
            go = Instantiate(targetPrefab, position, rotation, container);
        }
#else
        GameObject go = Instantiate(targetPrefab, position, rotation, container);
#endif

        go.name = $"{namePrefix}_{index:D3}";

        // 寫入編號資訊，方便其他腳本檢索
        var id = go.GetComponent<InstanceId>();
        if (!id) id = go.AddComponent<InstanceId>();
        id.groupId = groupId;
        id.index = index;
        id.ringOrRow = rowOrRing;
        id.col = col;

        spawned.Add(go);
    }

    [Header("Parenting & Naming")]
    [Tooltip("把所有生成物收納到一個子節點下")]
    public Transform container; // 若未指定，會自動建立
    [Tooltip("命名前綴，會自動加上 _index")]
    public string namePrefix = "Instance";

    void EnsureContainer()
    {
        if (container) return;
        var holderName = $"{namePrefix}_Container";
        var existing = transform.Find(holderName);
        if (existing)
        {
            container = existing;
            return;
        }

        var go = new GameObject(holderName);
        go.transform.SetParent(transform, false);
        container = go.transform;
    }

    // —— 範例 API：其他腳本如何取得這批物件 —— //
    public static List<InstanceId> FindByGroup(string groupId)
    {
        var list = new List<InstanceId>();
        var all = GameObject.FindObjectsOfType<InstanceId>(true);
        foreach (var x in all)
        {
            if (x.groupId == groupId) list.Add(x);
        }
        // 可依 index 排序
        list.Sort((a, b) => a.index.CompareTo(b.index));
        return list;
    }
}


#if UNITY_EDITOR

[CustomEditor(typeof(PatternPlacer))]
[CanEditMultipleObjects]
public class PatternPlacerEditor : Editor
{
    // Basic
    SerializedProperty targetPrefabProp, groupIdProp;
    // Placement (common)
    SerializedProperty modeProp, countProp, spacingProp;
    // Orientation
    SerializedProperty alignToTangentProp, axisToAlignProp, extraEulerProp;
    // Line → Grid
    SerializedProperty tileLineToGridProp, gridRowsProp, gridColsProp, gridRowSpacingProp, checkerOffsetProp;
    // Circle → Concentric
    SerializedProperty concentricRingsProp, ringCountProp, ringRadiusStepProp, scaleCountWithRadiusProp;
    // Polygon
    SerializedProperty polygonSidesProp, distributeAlongEdgesProp, polygonMultiRingsProp, polygonRingCountProp, polygonRingRadiusStepProp;
    // Polygon extra (edge gaps & centering)
    SerializedProperty polygonEdgeGapProp, edgeTrim01Prop, perEdgeFixedCountProp;
    // Parenting & Naming
    SerializedProperty containerProp, namePrefixProp;

    // Top-level header groups
    static bool foldBasic = true;
    static bool foldPlacement = true;
    static bool foldOrientation = true;
    static bool foldParenting = true;

    // Inner foldouts
    static bool foldLineExtra = true;
    static bool foldCircleExtra = true;
    static bool foldPolygonExtra = true;

    void OnEnable()
    {
        targetPrefabProp = serializedObject.FindProperty("targetPrefab");
        groupIdProp      = serializedObject.FindProperty("groupId");

        modeProp    = serializedObject.FindProperty("mode");
        countProp   = serializedObject.FindProperty("count");
        spacingProp = serializedObject.FindProperty("spacing");

        alignToTangentProp = serializedObject.FindProperty("alignToTangent");
        axisToAlignProp    = serializedObject.FindProperty("axisToAlign");
        extraEulerProp     = serializedObject.FindProperty("extraEuler");

        tileLineToGridProp  = serializedObject.FindProperty("tileLineToGrid");
        gridRowsProp        = serializedObject.FindProperty("gridRows");
        gridColsProp        = serializedObject.FindProperty("gridCols");
        gridRowSpacingProp  = serializedObject.FindProperty("gridRowSpacing");
        checkerOffsetProp   = serializedObject.FindProperty("checkerOffset");

        concentricRingsProp      = serializedObject.FindProperty("concentricRings");
        ringCountProp            = serializedObject.FindProperty("ringCount");
        ringRadiusStepProp       = serializedObject.FindProperty("ringRadiusStep");
        scaleCountWithRadiusProp = serializedObject.FindProperty("scaleCountWithRadius");

        polygonSidesProp          = serializedObject.FindProperty("polygonSides");
        distributeAlongEdgesProp  = serializedObject.FindProperty("distributeAlongEdges");
        polygonMultiRingsProp     = serializedObject.FindProperty("polygonMultiRings");
        polygonRingCountProp      = serializedObject.FindProperty("polygonRingCount");
        polygonRingRadiusStepProp = serializedObject.FindProperty("polygonRingRadiusStep");

        polygonEdgeGapProp     = serializedObject.FindProperty("polygonEdgeGap");
        edgeTrim01Prop         = serializedObject.FindProperty("edgeTrim01");
        perEdgeFixedCountProp  = serializedObject.FindProperty("perEdgeFixedCount");

        containerProp  = serializedObject.FindProperty("container");
        namePrefixProp = serializedObject.FindProperty("namePrefix");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var placer = (PatternPlacer)target;

        // ===== Basic =====
        foldBasic = EditorGUILayout.BeginFoldoutHeaderGroup(foldBasic, "Basic");
        if (foldBasic)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.PropertyField(targetPrefabProp, new GUIContent("Target Prefab"));
                EditorGUILayout.PropertyField(groupIdProp,      new GUIContent("Group ID"));
                if (!targetPrefabProp.objectReferenceValue)
                    EditorGUILayout.HelpBox("請指定要複製的 Prefab。", MessageType.Warning);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ===== Placement =====
        foldPlacement = EditorGUILayout.BeginFoldoutHeaderGroup(foldPlacement, "Placement");
        if (foldPlacement)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.PropertyField(modeProp,  new GUIContent("Mode"));
                EditorGUILayout.PropertyField(countProp, new GUIContent("Count"));

                string spacingHint = (PatternPlacer.PatternMode)modeProp.enumValueIndex switch
                {
                    PatternPlacer.PatternMode.Line    => "間距：沿切向距離",
                    PatternPlacer.PatternMode.Circle  => "半徑：單圈半徑 (spacing)",
                    PatternPlacer.PatternMode.Polygon => "半徑：外接圓半徑 (spacing)",
                    _ => "Spacing"
                };
                EditorGUILayout.PropertyField(spacingProp, new GUIContent("Spacing / Radius", spacingHint));
            }

            var mode = (PatternPlacer.PatternMode)modeProp.enumValueIndex;

            // ---- Mode-specific blocks ----
            if (mode == PatternPlacer.PatternMode.Line)
            {
                foldLineExtra = EditorGUILayout.Foldout(foldLineExtra, "Line → Grid (Tiling)", true);
                if (foldLineExtra)
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUILayout.PropertyField(tileLineToGridProp, new GUIContent("Tile Line To Grid"));
                        if (tileLineToGridProp.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(gridRowsProp,       new GUIContent("Grid Rows"));
                            EditorGUILayout.PropertyField(gridColsProp,       new GUIContent("Grid Cols"));
                            EditorGUILayout.PropertyField(gridRowSpacingProp, new GUIContent("Row Spacing"));
                            EditorGUILayout.PropertyField(checkerOffsetProp,  new GUIContent("Checker Offset (Half-Shift)"));
                            EditorGUI.indentLevel--;
                        }
                    }
                }
            }
            else if (mode == PatternPlacer.PatternMode.Circle)
            {
                foldCircleExtra = EditorGUILayout.Foldout(foldCircleExtra, "Circle → Concentric Rings", true);
                if (foldCircleExtra)
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUILayout.PropertyField(concentricRingsProp, new GUIContent("Concentric Rings"));
                        if (concentricRingsProp.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(ringCountProp,            new GUIContent("Ring Count"));
                            EditorGUILayout.PropertyField(ringRadiusStepProp,       new GUIContent("Ring Radius Step"));
                            EditorGUILayout.PropertyField(scaleCountWithRadiusProp, new GUIContent("Scale Count With Radius"));
                            EditorGUI.indentLevel--;
                        }
                    }
                }

                if (spacingProp.floatValue <= 0f)
                    EditorGUILayout.HelpBox("Circle 模式下 spacing 代表半徑，需 > 0。", MessageType.Info);
            }
            else // Polygon
            {
                foldPolygonExtra = EditorGUILayout.Foldout(foldPolygonExtra, "Polygon (Regular) → Multi-Rings", true);
                if (foldPolygonExtra)
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUILayout.PropertyField(polygonSidesProp,         new GUIContent("Polygon Sides"));
                        EditorGUILayout.PropertyField(distributeAlongEdgesProp, new GUIContent("Distribute Along Edges"));
                        EditorGUILayout.PropertyField(polygonMultiRingsProp,    new GUIContent("Polygon Multi-Rings"));
                        if (polygonMultiRingsProp.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(polygonRingCountProp,      new GUIContent("Ring Count"));
                            EditorGUILayout.PropertyField(polygonRingRadiusStepProp, new GUIContent("Ring Radius Step"));
                            EditorGUI.indentLevel--;
                        }

                        if (polygonSidesProp.intValue < 3)
                            EditorGUILayout.HelpBox("Polygon 邊數需 >= 3。", MessageType.Info);

                        // —— Edge Gaps & Centering（僅在沿邊分佈時顯示）——
                        if (distributeAlongEdgesProp.boolValue)
                        {
                            EditorGUILayout.Space(2);
                            using (new EditorGUILayout.VerticalScope("box"))
                            {
                                EditorGUILayout.LabelField("Edge Gaps & Centering", EditorStyles.boldLabel);
                                EditorGUILayout.PropertyField(polygonEdgeGapProp, new GUIContent("Enable Edge Gap & Centering"));
                                if (polygonEdgeGapProp.boolValue)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.Slider(edgeTrim01Prop, 0f, 0.49f, new GUIContent("Edge Trim (0~0.49)"));
                                    EditorGUILayout.PropertyField(perEdgeFixedCountProp, new GUIContent("Per-Edge Fixed Count (0=auto)"));
                                    if (perEdgeFixedCountProp.intValue > 0)
                                    {
                                        EditorGUILayout.HelpBox("已固定每條邊的數量：總數 = 邊數 × Per-Edge Fixed Count，會忽略 Count。", MessageType.Info);
                                    }
                                    else
                                    {
                                        EditorGUILayout.HelpBox("未固定每邊數量：以可用邊長加權分配全域 Count（最大餘數法補齊），並於每條邊內『置中』分佈。", MessageType.None);
                                    }
                                    EditorGUI.indentLevel--;
                                }
                            }
                        }
                    }
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ===== Orientation =====
        foldOrientation = EditorGUILayout.BeginFoldoutHeaderGroup(foldOrientation, "Orientation (Derivative/Tangent)");
        if (foldOrientation)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.PropertyField(alignToTangentProp, new GUIContent("Align To Tangent"));
                EditorGUILayout.PropertyField(axisToAlignProp,    new GUIContent("Axis To Align"));
                EditorGUILayout.PropertyField(extraEulerProp,     new GUIContent("Extra Euler (deg)"));
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ===== Parenting & Naming =====
        foldParenting = EditorGUILayout.BeginFoldoutHeaderGroup(foldParenting, "Parenting & Naming");
        if (foldParenting)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.PropertyField(containerProp,  new GUIContent("Container"));
                EditorGUILayout.PropertyField(namePrefixProp, new GUIContent("Name Prefix"));
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ===== Buttons =====
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate")) ((PatternPlacer)target).Generate();
            if (GUILayout.Button("Clear"))    ((PatternPlacer)target).Clear();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
