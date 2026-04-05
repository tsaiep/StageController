using UnityEngine;

public class SLMUnit : MonoBehaviour
{
    [Header("實體元件")]
    public Transform panTransform;
    public Transform tiltTransform;
    public Light targetLight; // 腳本會自動同步此 Light 與 VLB

    [Header("單元演出設定")]
    public bool invertPan = false;
    public bool invertTilt = false;
    [Tooltip("動作位移")] public float motionOffset = 0f;

    [HideInInspector] public float curPan, curTilt, velPan, velTilt;
}