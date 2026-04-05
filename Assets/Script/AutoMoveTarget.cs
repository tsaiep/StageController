using UnityEngine;

public class AutoMoveTarget : MonoBehaviour
{
    [Header("移動範圍設定")]
    [Tooltip("移動的中心點")]
    public Vector3 centerPosition;
    [Tooltip("前後移動的距離範圍")]
    public float moveDistance = 5f;
    [Tooltip("移動速度")]
    public float moveSpeed = 1f;

    [Header("移動軸向")]
    public bool 沿著Z軸移動 = true;
    public bool 沿著X軸移動 = false;

    private float timer;

    void Start()
    {
        // 如果沒有手動設定中心點，則以當前位置為準
        if (centerPosition == Vector3.zero)
        {
            centerPosition = transform.position;
        }
    }

    void Update()
    {
        // 使用正弦波計算平滑的來回位移
        timer += Time.deltaTime * moveSpeed;
        float offset = Mathf.Sin(timer) * moveDistance;

        Vector3 nextPos = centerPosition;

        if (沿著Z軸移動) nextPos.z += offset;
        if (沿著X軸移動) nextPos.x += offset;

        transform.position = nextPos;
    }

    // 在 Scene 視窗畫出移動路徑，方便調校
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 start = centerPosition;
        Vector3 end = centerPosition;

        if (沿著Z軸移動)
        {
            start.z -= moveDistance;
            end.z += moveDistance;
        }
        else if (沿著X軸移動)
        {
            start.x -= moveDistance;
            end.x += moveDistance;
        }

        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(start, 0.3f);
        Gizmos.DrawWireSphere(end, 0.3f);
    }
}