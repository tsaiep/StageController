using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class RendererBoundsExpander : MonoBehaviour
{
    [Tooltip("增加 Bounds 的總尺寸，會平均擴張到正負兩側。")]
    [SerializeField]
    private Vector3 boundsExpansion = Vector3.zero;

    [Tooltip("Offset applied to the Renderer local bounds center.")]
    [SerializeField]
    private Vector3 boundsCenterOffset = Vector3.zero;

    [SerializeField]
    private bool drawGizmo = true;

    private Renderer targetRenderer;

    private void OnEnable()
    {
        ApplyBounds();
    }

    private void OnValidate()
    {
        boundsExpansion.x = Mathf.Max(0f, boundsExpansion.x);
        boundsExpansion.y = Mathf.Max(0f, boundsExpansion.y);
        boundsExpansion.z = Mathf.Max(0f, boundsExpansion.z);

        ApplyBounds();
    }

    private void OnDisable()
    {
        if (targetRenderer != null)
            targetRenderer.ResetLocalBounds();
    }

    [ContextMenu("Apply Bounds")]
    public void ApplyBounds()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        if (targetRenderer == null)
            return;

        // 先清除先前的覆寫，避免每次 OnValidate 都重複累加。
        targetRenderer.ResetLocalBounds();

        Bounds bounds = targetRenderer.localBounds;
        bounds.center += boundsCenterOffset;
        bounds.size += boundsExpansion;

        targetRenderer.localBounds = bounds;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmo)
            return;

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        if (targetRenderer == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(
            targetRenderer.localBounds.center,
            targetRenderer.localBounds.size
        );

        Gizmos.matrix = previousMatrix;
    }
#endif
}
