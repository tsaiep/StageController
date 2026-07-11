#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

public class MeshUvGridCombinerWindow : EditorWindow
{
    private const int GridSize = 8;
    private const int GridCellCount = GridSize * GridSize;
    private const float CellSize = 1f / GridSize;
    private const string DefaultAssetPath = "Assets/GeneratedMeshes/CombinedMesh.asset";

    [SerializeField] private List<GameObject> sourceObjects = new List<GameObject>();
    [SerializeField] private string outputAssetPath = DefaultAssetPath;
    [SerializeField] private Material LightstripMaterial;
    [SerializeField] private float uvPadding = 0.005f;

    private ReorderableList sourceList;
    private Vector2 scrollPosition;

    [MenuItem("Window/Stage Control/Mesh UV Grid Combiner")]
    public static void ShowWindow()
    {
        GetWindow<MeshUvGridCombinerWindow>("Mesh UV Grid Combiner");
    }

    private void OnEnable()
    {
        BuildSourceList();
    }

    private void OnGUI()
    {
        if (sourceList == null)
            BuildSourceList();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Source Mesh Objects", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Add scene GameObjects that contain MeshFilter + MeshRenderer. The generated mesh uses each object's current scene transform.", MessageType.Info);

        sourceList.DoLayoutList();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Selected Mesh Objects"))
                AddSelectedObjects();

            if (GUILayout.Button("Remove Nulls / Duplicates"))
                CleanupSourceObjects();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        LightstripMaterial = (Material)EditorGUILayout.ObjectField("Lightstrip Material", LightstripMaterial, typeof(Material), false);
        outputAssetPath = EditorGUILayout.TextField("Asset Path", outputAssetPath);
       

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Browse...", GUILayout.Width(100)))
                BrowseOutputPath();
        }

        float maxPadding = (CellSize * 0.5f) - 0.0001f;
        uvPadding = EditorGUILayout.Slider("UV Padding", uvPadding, 0f, maxPadding);

        EditorGUILayout.Space(14);

        GUI.enabled = sourceObjects.Count > 0;
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generate", GUILayout.Height(44)))
            GenerateCombinedMesh();

        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    private void BuildSourceList()
    {
        sourceList = new ReorderableList(sourceObjects, typeof(GameObject), true, true, true, true);
        sourceList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Mesh GameObjects");
        sourceList.onAddCallback = _ => sourceObjects.Add(null);
        sourceList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
        sourceList.drawElementCallback = (rect, index, active, focused) =>
        {
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            sourceObjects[index] = (GameObject)EditorGUI.ObjectField(rect, sourceObjects[index], typeof(GameObject), true);
        };
    }

    private void AddSelectedObjects()
    {
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (selected == null || sourceObjects.Contains(selected))
                continue;

            sourceObjects.Add(selected);
        }
    }

    private void CleanupSourceObjects()
    {
        HashSet<GameObject> seen = new HashSet<GameObject>();
        for (int i = sourceObjects.Count - 1; i >= 0; i--)
        {
            GameObject source = sourceObjects[i];
            if (source == null || !seen.Add(source))
                sourceObjects.RemoveAt(i);
        }
    }

    private void BrowseOutputPath()
    {
        string currentPath = string.IsNullOrWhiteSpace(outputAssetPath) ? DefaultAssetPath : outputAssetPath;
        string currentDirectory = Path.GetDirectoryName(currentPath)?.Replace('\\', '/') ?? "Assets";
        string fileName = Path.GetFileNameWithoutExtension(currentPath);

        string selectedPath = EditorUtility.SaveFilePanelInProject(
            "Save Combined Mesh",
            string.IsNullOrWhiteSpace(fileName) ? "CombinedMesh" : fileName,
            "asset",
            "Choose where to save the generated mesh asset.",
            currentDirectory);

        if (!string.IsNullOrWhiteSpace(selectedPath))
            outputAssetPath = selectedPath;
    }

    private void GenerateCombinedMesh()
    {
        if (!TryValidateOutputPath(out string validatedPath))
            return;

        List<SourceMeshEntry> validSources = CollectValidSources();
        if (validSources.Count == 0)
        {
            EditorUtility.DisplayDialog("Mesh UV Grid Combiner", "No valid MeshFilter + MeshRenderer source objects were found.", "OK");
            return;
        }

        try
        {
            Mesh combinedMesh = BuildCombinedMesh(validSources, out Vector3 instancePosition);
            Mesh savedMesh = SaveMeshAsset(combinedMesh, validatedPath);
            CreateSceneInstance(savedMesh, validSources, instancePosition);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Mesh UV Grid Combiner failed: {exception}");
            EditorUtility.DisplayDialog("Mesh UV Grid Combiner", "Failed to generate combined mesh. Check the Console for details.", "OK");
        }
    }

    private bool TryValidateOutputPath(out string validatedPath)
    {
        validatedPath = string.IsNullOrWhiteSpace(outputAssetPath) ? DefaultAssetPath : outputAssetPath.Trim();
        validatedPath = validatedPath.Replace('\\', '/');

        if (!validatedPath.StartsWith("Assets/", StringComparison.Ordinal) && validatedPath != "Assets")
        {
            EditorUtility.DisplayDialog("Invalid Output Path", "The mesh asset must be saved inside the project's Assets folder.", "OK");
            return false;
        }

        if (validatedPath == "Assets")
            validatedPath = DefaultAssetPath;

        if (Path.GetExtension(validatedPath).ToLowerInvariant() != ".asset")
            validatedPath = Path.ChangeExtension(validatedPath, ".asset").Replace('\\', '/');

        outputAssetPath = validatedPath;
        return true;
    }

    private List<SourceMeshEntry> CollectValidSources()
    {
        List<SourceMeshEntry> validSources = new List<SourceMeshEntry>();

        for (int i = 0; i < sourceObjects.Count; i++)
        {
            GameObject sourceObject = sourceObjects[i];
            if (sourceObject == null)
            {
                Debug.LogWarning($"Mesh UV Grid Combiner: Source element {i} is null and was skipped.");
                continue;
            }

            MeshFilter meshFilter = sourceObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = sourceObject.GetComponent<MeshRenderer>();

            if (meshFilter == null || meshRenderer == null)
            {
                Debug.LogWarning($"Mesh UV Grid Combiner: '{sourceObject.name}' needs MeshFilter + MeshRenderer and was skipped.", sourceObject);
                continue;
            }

            Mesh sourceMesh = meshFilter.sharedMesh;
            if (sourceMesh == null)
            {
                Debug.LogWarning($"Mesh UV Grid Combiner: '{sourceObject.name}' has no sharedMesh and was skipped.", sourceObject);
                continue;
            }

            if (!sourceMesh.isReadable)
            {
                Debug.LogWarning($"Mesh UV Grid Combiner: '{sourceObject.name}' uses mesh '{sourceMesh.name}', but Read/Write is disabled. It was skipped.", sourceObject);
                continue;
            }

            validSources.Add(new SourceMeshEntry(meshFilter, meshRenderer, sourceMesh));
        }

        return validSources;
    }

    private Mesh BuildCombinedMesh(List<SourceMeshEntry> validSources, out Vector3 instancePosition)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector4> tangents = new List<Vector4>();
        List<Color> colors = new List<Color>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv1 = new List<Vector2>();
        List<int> triangles = new List<int>();

        bool shouldRecalculateNormals = false;
        bool shouldRecalculateTangents = false;
        bool warnedAboutGridOverflow = false;

        for (int sourceIndex = 0; sourceIndex < validSources.Count; sourceIndex++)
        {
            if (!warnedAboutGridOverflow && sourceIndex >= GridCellCount)
            {
                warnedAboutGridOverflow = true;
                Debug.LogWarning($"Mesh UV Grid Combiner: More than {GridCellCount} meshes were supplied. UV1 cells past the {GridSize} x {GridSize} grid will exceed the 0-1 UV range.");
            }

            SourceMeshEntry source = validSources[sourceIndex];
            Mesh sourceMesh = source.Mesh;
            int vertexOffset = vertices.Count;
            int sourceVertexCount = sourceMesh.vertexCount;

            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector3[] sourceNormals = sourceMesh.normals;
            Vector4[] sourceTangents = sourceMesh.tangents;
            Color[] sourceColors = sourceMesh.colors;
            Vector2[] sourceUv0 = sourceMesh.uv;

            Matrix4x4 localToWorld = source.MeshFilter.transform.localToWorldMatrix;
            Matrix4x4 normalMatrix = localToWorld.inverse.transpose;
            Bounds localBounds = sourceMesh.bounds;
            bool flipsWinding = localToWorld.determinant < 0f;

            bool hasNormals = sourceNormals != null && sourceNormals.Length == sourceVertexCount;
            bool hasTangents = sourceTangents != null && sourceTangents.Length == sourceVertexCount;
            bool hasColors = sourceColors != null && sourceColors.Length == sourceVertexCount;
            bool hasUv0 = sourceUv0 != null && sourceUv0.Length == sourceVertexCount;

            if (!hasNormals)
                shouldRecalculateNormals = true;

            if (!hasTangents)
                shouldRecalculateTangents = true;

            for (int vertexIndex = 0; vertexIndex < sourceVertexCount; vertexIndex++)
            {
                Vector3 sourceVertex = sourceVertices[vertexIndex];
                vertices.Add(localToWorld.MultiplyPoint3x4(sourceVertex));

                normals.Add(hasNormals
                    ? normalMatrix.MultiplyVector(sourceNormals[vertexIndex]).normalized
                    : Vector3.up);

                if (hasTangents)
                {
                    Vector4 sourceTangent = sourceTangents[vertexIndex];
                    Vector3 transformedTangent = localToWorld.MultiplyVector(new Vector3(sourceTangent.x, sourceTangent.y, sourceTangent.z)).normalized;
                    tangents.Add(new Vector4(transformedTangent.x, transformedTangent.y, transformedTangent.z, flipsWinding ? -sourceTangent.w : sourceTangent.w));
                }
                else
                {
                    tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                }

                colors.Add(hasColors ? sourceColors[vertexIndex] : Color.white);

                Vector2 baseUv = hasUv0 ? sourceUv0[vertexIndex] : GeneratePlanarUv(sourceVertex, localBounds);
                uv0.Add(baseUv);
                uv1.Add(RemapToGridCell(baseUv, sourceIndex));
            }

            for (int subMeshIndex = 0; subMeshIndex < sourceMesh.subMeshCount; subMeshIndex++)
            {
                int[] indices = sourceMesh.GetIndices(subMeshIndex);
                MeshTopology topology = sourceMesh.GetTopology(subMeshIndex);

                for (int index = 0; index < indices.Length; index++)
                    indices[index] += vertexOffset;

                AddSubMeshTriangles(source.MeshFilter, sourceMesh, subMeshIndex, indices, topology, flipsWinding, triangles);
            }
        }

        instancePosition = CenterVerticesAroundPivot(vertices);

        Mesh combinedMesh = new Mesh
        {
            name = Path.GetFileNameWithoutExtension(outputAssetPath)
        };

        if (vertices.Count > 65535)
            combinedMesh.indexFormat = IndexFormat.UInt32;

        combinedMesh.SetVertices(vertices);
        combinedMesh.SetNormals(normals);
        combinedMesh.SetTangents(tangents);
        combinedMesh.SetColors(colors);
        combinedMesh.SetUVs(0, uv0);
        combinedMesh.SetUVs(1, uv1);
        combinedMesh.SetTriangles(triangles, 0, false);

        combinedMesh.RecalculateBounds();

        if (shouldRecalculateNormals)
            combinedMesh.RecalculateNormals();

        if (shouldRecalculateTangents)
            combinedMesh.RecalculateTangents();

        return combinedMesh;
    }

    private static Vector3 CenterVerticesAroundPivot(List<Vector3> vertices)
    {
        if (vertices.Count == 0)
            return Vector3.zero;

        Bounds bounds = new Bounds(vertices[0], Vector3.zero);
        for (int i = 1; i < vertices.Count; i++)
            bounds.Encapsulate(vertices[i]);

        Vector3 center = bounds.center;
        for (int i = 0; i < vertices.Count; i++)
            vertices[i] -= center;

        return center;
    }

    private static void AddSubMeshTriangles(
        MeshFilter meshFilter,
        Mesh sourceMesh,
        int subMeshIndex,
        int[] indices,
        MeshTopology topology,
        bool flipsWinding,
        List<int> triangles)
    {
        if (topology == MeshTopology.Triangles)
        {
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                triangles.Add(indices[i]);
                triangles.Add(flipsWinding ? indices[i + 2] : indices[i + 1]);
                triangles.Add(flipsWinding ? indices[i + 1] : indices[i + 2]);
            }

            return;
        }

        if (topology == MeshTopology.Quads)
        {
            for (int i = 0; i + 3 < indices.Length; i += 4)
            {
                AddTriangle(triangles, indices[i], indices[i + 1], indices[i + 2], flipsWinding);
                AddTriangle(triangles, indices[i], indices[i + 2], indices[i + 3], flipsWinding);
            }

            return;
        }

        Debug.LogWarning($"Mesh UV Grid Combiner: '{meshFilter.name}' mesh '{sourceMesh.name}' submesh {subMeshIndex} uses {topology}, so it was skipped because the combined output uses one triangle submesh.", meshFilter);
    }

    private static void AddTriangle(List<int> triangles, int a, int b, int c, bool flipsWinding)
    {
        triangles.Add(a);
        triangles.Add(flipsWinding ? c : b);
        triangles.Add(flipsWinding ? b : c);
    }

    private Vector2 RemapToGridCell(Vector2 sourceUv, int sourceIndex)
    {
        int gridX = sourceIndex % GridSize;
        int gridY = sourceIndex / GridSize;

        float safePadding = Mathf.Clamp(uvPadding, 0f, (CellSize * 0.5f) - 0.0001f);
        float usableSize = CellSize - (safePadding * 2f);

        float clampedU = Mathf.Clamp01(sourceUv.x);
        float clampedV = Mathf.Clamp01(sourceUv.y);

        return new Vector2(
            (gridX * CellSize) + safePadding + (clampedU * usableSize),
            (gridY * CellSize) + safePadding + (clampedV * usableSize));
    }

    private static Vector2 GeneratePlanarUv(Vector3 vertex, Bounds bounds)
    {
        Vector3 size = bounds.size;

        int axisU = 0;
        int axisV = 1;

        if (size.z >= size.x && size.z >= size.y)
        {
            axisU = size.x >= size.y ? 0 : 1;
            axisV = 2;
        }
        else if (size.y >= size.x && size.y >= size.z)
        {
            axisU = size.x >= size.z ? 0 : 2;
            axisV = 1;
        }

        return new Vector2(
            NormalizeAxis(GetAxis(vertex, axisU), GetAxis(bounds.min, axisU), GetAxis(bounds.max, axisU)),
            NormalizeAxis(GetAxis(vertex, axisV), GetAxis(bounds.min, axisV), GetAxis(bounds.max, axisV)));
    }

    private static float NormalizeAxis(float value, float min, float max)
    {
        float length = max - min;
        if (Mathf.Abs(length) < 0.00001f)
            return 0.5f;

        return Mathf.Clamp01((value - min) / length);
    }

    private static float GetAxis(Vector3 value, int axis)
    {
        switch (axis)
        {
            case 0:
                return value.x;
            case 1:
                return value.y;
            default:
                return value.z;
        }
    }

    private Mesh SaveMeshAsset(Mesh mesh, string assetPath)
    {
        string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            CreateAssetFolder(directory);

        string finalPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
        AssetDatabase.CreateAsset(mesh, finalPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(finalPath);
        Selection.activeObject = savedMesh;
        EditorGUIUtility.PingObject(savedMesh);

        Debug.Log($"Mesh UV Grid Combiner: Generated combined mesh asset at '{finalPath}' with {mesh.vertexCount} vertices and one submesh.", savedMesh);
        return savedMesh;
    }

    private void CreateSceneInstance(Mesh mesh, List<SourceMeshEntry> validSources, Vector3 instancePosition)
    {
        if (mesh == null)
            return;

        GameObject instance = new GameObject(mesh.name);
        instance.transform.position = instancePosition;

        MeshFilter meshFilter = instance.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = instance.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = GetMaterialForGeneratedObject(validSources);

        Undo.RegisterCreatedObjectUndo(instance, "Create Combined Mesh Instance");
        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);
    }

    private Material GetMaterialForGeneratedObject(List<SourceMeshEntry> validSources)
    {
        if (LightstripMaterial != null)
            return LightstripMaterial;

        foreach (SourceMeshEntry source in validSources)
        {
            if (source.MeshRenderer != null && source.MeshRenderer.sharedMaterial != null)
                return source.MeshRenderer.sharedMaterial;
        }

        return null;
    }

    private static void CreateAssetFolder(string directory)
    {
        string[] parts = directory.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private readonly struct SourceMeshEntry
    {
        public readonly MeshFilter MeshFilter;
        public readonly MeshRenderer MeshRenderer;
        public readonly Mesh Mesh;

        public SourceMeshEntry(MeshFilter meshFilter, MeshRenderer meshRenderer, Mesh mesh)
        {
            MeshFilter = meshFilter;
            MeshRenderer = meshRenderer;
            Mesh = mesh;
        }
    }
}
#endif
