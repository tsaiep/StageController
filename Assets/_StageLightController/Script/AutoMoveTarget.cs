using UnityEngine;
using UnityEngine.Serialization;

public class AutoMoveTarget : MonoBehaviour
{
    [Header("Movement Range")]
    [Tooltip("Center point for the back-and-forth motion.")]
    public Vector3 centerPosition;

    [Tooltip("Maximum distance traveled from the center.")]
    public float moveDistance = 5f;

    [Tooltip("Oscillation speed.")]
    public float moveSpeed = 1f;

    [Header("Movement Axis")]
    [FormerlySerializedAs("\u6cbf\u8457Z\u8ef8\u79fb\u52d5")]
    public bool moveAlongZAxis = true;

    [FormerlySerializedAs("\u6cbf\u8457X\u8ef8\u79fb\u52d5")]
    public bool moveAlongXAxis = false;

    private float timer;

    void Start()
    {
        // If the center was not configured manually, use the current position.
        if (centerPosition == Vector3.zero)
        {
            centerPosition = transform.position;
        }
    }

    void Update()
    {
        // Use a sine wave to create smooth back-and-forth motion.
        timer += Time.deltaTime * moveSpeed;
        float offset = Mathf.Sin(timer) * moveDistance;

        Vector3 nextPos = centerPosition;

        if (moveAlongZAxis) nextPos.z += offset;
        if (moveAlongXAxis) nextPos.x += offset;

        transform.position = nextPos;
    }

    // Draw the motion path in the Scene view for easier tuning.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 start = centerPosition;
        Vector3 end = centerPosition;

        if (moveAlongZAxis)
        {
            start.z -= moveDistance;
            end.z += moveDistance;
        }
        else if (moveAlongXAxis)
        {
            start.x -= moveDistance;
            end.x += moveDistance;
        }

        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(start, 0.3f);
        Gizmos.DrawWireSphere(end, 0.3f);
    }
}
