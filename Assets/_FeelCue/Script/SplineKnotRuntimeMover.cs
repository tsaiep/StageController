using UnityEngine;

public class SplineKnotRuntimeMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private int selectedChildIndex;
    [SerializeField] private bool syncFirstNestedChildActiveOnSelect = true;

    private Transform[] firstLevelChildren = new Transform[0];
    private Vector3 moveDirection;

    public int SelectedChildIndex => selectedChildIndex;
    public Transform SelectedChild
    {
        get
        {
            if (!HasSelectableChild())
            {
                return null;
            }

            return firstLevelChildren[selectedChildIndex];
        }
    }

    private void Awake()
    {
        CacheFirstLevelChildren();

        if (syncFirstNestedChildActiveOnSelect)
        {
            SyncFirstNestedChildActiveState();
        }
    }

    private void Update()
    {
        Transform selectedChild = SelectedChild;
        if (selectedChild == null || moveDirection == Vector3.zero)
        {
            return;
        }

        selectedChild.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    public void CacheFirstLevelChildren()
    {
        firstLevelChildren = new Transform[transform.childCount];

        for (int i = 0; i < firstLevelChildren.Length; i++)
        {
            firstLevelChildren[i] = transform.GetChild(i);
        }

        selectedChildIndex = ClampChildIndex(selectedChildIndex);
    }

    public void SelectChildByIndex(int index)
    {
        if (firstLevelChildren.Length == 0)
        {
            Debug.LogWarning($"{nameof(SplineKnotRuntimeMover)} on {name} has no first-level children to select.", this);
            selectedChildIndex = 0;
            return;
        }

        selectedChildIndex = ClampChildIndex(index);

        if (syncFirstNestedChildActiveOnSelect)
        {
            SyncFirstNestedChildActiveState();
        }
    }

    public void SelectNextChild()
    {
        SelectChildByIndex(selectedChildIndex + 1);
    }

    public void SelectPreviousChild()
    {
        SelectChildByIndex(selectedChildIndex - 1);
    }

    public void MoveSelectedChild(string axisDirection)
    {
        StartMoving(axisDirection);
    }

    public void StartMoving(string axisDirection)
    {
        if (IsStopCommand(axisDirection))
        {
            StopMoving();
            return;
        }

        if (!TryGetDirection(axisDirection, out Vector3 direction))
        {
            Debug.LogWarning($"{nameof(SplineKnotRuntimeMover)} expected X, +X, -X, Y, +Y, -Y, Z, +Z, -Z, Stop, or 0, but received \"{axisDirection}\".", this);
            return;
        }

        if (!HasSelectableChild())
        {
            Debug.LogWarning($"{nameof(SplineKnotRuntimeMover)} on {name} has no selected first-level child to move.", this);
            return;
        }

        moveDirection = direction;
    }

    public void OnMovePressed(string axisDirection)
    {
        StartMoving(axisDirection);
    }

    public void OnMoveReleased()
    {
        StopMoving();
    }

    public void ActivateFirstNestedChildOfSelectedChild()
    {
        SyncFirstNestedChildActiveState();
    }

    public void SyncFirstNestedChildActiveState()
    {
        Transform selectedChild = SelectedChild;
        if (selectedChild == null)
        {
            Debug.LogWarning($"{nameof(SplineKnotRuntimeMover)} on {name} has no selected first-level child.", this);
            return;
        }

        for (int i = 0; i < firstLevelChildren.Length; i++)
        {
            Transform child = firstLevelChildren[i];
            if (child == null || child.childCount == 0)
            {
                continue;
            }

            child.GetChild(0).gameObject.SetActive(i == selectedChildIndex);
        }
    }

    public void StopMoving()
    {
        moveDirection = Vector3.zero;
    }

    public void SetMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0f, speed);
    }

    private bool HasSelectableChild()
    {
        return firstLevelChildren.Length > 0
            && selectedChildIndex >= 0
            && selectedChildIndex < firstLevelChildren.Length
            && firstLevelChildren[selectedChildIndex] != null;
    }

    private int ClampChildIndex(int index)
    {
        if (firstLevelChildren.Length == 0)
        {
            return 0;
        }

        return Mathf.Clamp(index, 0, firstLevelChildren.Length - 1);
    }

    private bool TryGetDirection(string axisDirection, out Vector3 direction)
    {
        direction = Vector3.zero;

        if (string.IsNullOrWhiteSpace(axisDirection))
        {
            return false;
        }

        switch (axisDirection.Trim().ToUpperInvariant())
        {
            case "X":
            case "+X":
                direction = Vector3.right;
                return true;
            case "-X":
                direction = Vector3.left;
                return true;
            case "Y":
            case "+Y":
                direction = Vector3.up;
                return true;
            case "-Y":
                direction = Vector3.down;
                return true;
            case "Z":
            case "+Z":
                direction = Vector3.forward;
                return true;
            case "-Z":
                direction = Vector3.back;
                return true;
            default:
                return false;
        }
    }

    private bool IsStopCommand(string axisDirection)
    {
        if (string.IsNullOrWhiteSpace(axisDirection))
        {
            return false;
        }

        string command = axisDirection.Trim().ToUpperInvariant();
        return command == "STOP" || command == "0";
    }
}
