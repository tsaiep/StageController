using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[DisallowMultipleComponent]
[AddComponentMenu("Stage Controller/Timeline MMF VFX Trigger")]
[MovedFrom(false, null, null, "TimelineVFXSeededMMFTrigger")]
public class TimelineMMFVFXTrigger : MonoBehaviour
{
    private const float DuplicateCallGuardSeconds = 0.05f;

    public MMF_Player mmfPlayer;

    private int lastPlayFrame = -1;
    private float lastPlayRealtime = -999f;

    private void Reset()
    {
        mmfPlayer = GetComponent<MMF_Player>();
    }

    public void Play()
    {
        if (IsDuplicatePlayCall())
            return;

        MarkPlayCall();

        if (mmfPlayer != null)
            mmfPlayer.PlayFeedbacks();
    }

    private bool IsDuplicatePlayCall()
    {
        if (lastPlayFrame == Time.frameCount)
            return true;

        return Time.realtimeSinceStartup - lastPlayRealtime <= DuplicateCallGuardSeconds;
    }

    private void MarkPlayCall()
    {
        lastPlayFrame = Time.frameCount;
        lastPlayRealtime = Time.realtimeSinceStartup;
    }
}
