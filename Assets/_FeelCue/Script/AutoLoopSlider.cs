using UnityEngine;
using UnityEngine.UI;

public class AutoLoopSlider : MonoBehaviour
{
    [SerializeField] private Slider targetSlider;
    [SerializeField] private bool autoControl = true;
    [SerializeField] private float loopDuration = 1f;
    [SerializeField] private float intervalDuration = 0f;
    [SerializeField] private bool syncFromSliderWhenStarted = true;

    private float progress;
    private float intervalTimer;
    private bool waitingForInterval;

    public bool AutoControl => autoControl;
    public float Progress => progress;

    private void Awake()
    {
        if (targetSlider == null)
        {
            targetSlider = GetComponent<Slider>();
        }

        SyncProgressFromSlider();
    }

    private void Update()
    {
        if (!autoControl || targetSlider == null)
        {
            return;
        }

        if (waitingForInterval)
        {
            intervalTimer += Time.deltaTime;
            ApplyProgressToSlider();

            if (intervalTimer < intervalDuration)
            {
                return;
            }

            intervalTimer = 0f;
            waitingForInterval = false;
            progress = 0f;
        }

        progress += Time.deltaTime / loopDuration;

        if (progress >= 1f)
        {
            progress = 1f;
            waitingForInterval = true;
            intervalTimer = 0f;
        }

        ApplyProgressToSlider();
    }

    public void SetAutoControl(bool isOn)
    {
        autoControl = isOn;

        if (autoControl && syncFromSliderWhenStarted)
        {
            SyncProgressFromSlider();
        }

        if (!autoControl)
        {
            waitingForInterval = false;
            intervalTimer = 0f;
        }
    }

    public void StartAutoControl()
    {
        SetAutoControl(true);
    }

    public void StopAutoControl()
    {
        SetAutoControl(false);
    }

    public void ToggleAutoControl()
    {
        SetAutoControl(!autoControl);
    }

    public void SetLoopDuration(float duration)
    {
        loopDuration = Mathf.Max(0.01f, duration);
    }

    public void SetIntervalDuration(float duration)
    {
        intervalDuration = Mathf.Max(0f, duration);
    }

    public void ResetProgressToZero()
    {
        progress = 0f;
        waitingForInterval = false;
        intervalTimer = 0f;
        ApplyProgressToSlider();
    }

    public void SyncProgressFromSlider()
    {
        if (targetSlider == null)
        {
            progress = 0f;
            return;
        }

        progress = Mathf.InverseLerp(targetSlider.minValue, targetSlider.maxValue, targetSlider.value);
        waitingForInterval = progress >= 1f;
        intervalTimer = 0f;
    }

    private void ApplyProgressToSlider()
    {
        if (targetSlider == null)
        {
            return;
        }

        targetSlider.value = Mathf.Lerp(targetSlider.minValue, targetSlider.maxValue, progress);
    }

    private void OnValidate()
    {
        loopDuration = Mathf.Max(0.01f, loopDuration);
        intervalDuration = Mathf.Max(0f, intervalDuration);
    }
}
