using MoreMountains.Feedbacks;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Stage Controller/Timeline VFX Seeded MMF Trigger")]
public class TimelineVFXSeededMMFTrigger : MonoBehaviour
{
    private const float DuplicateCallGuardSeconds = 0.05f;

    public MMF_Player mmfPlayer;
    public TimelineVFXScrubber scrubber;

    private int lastPlayFrame = -1;
    private float lastPlayRealtime = -999f;

    private void Reset()
    {
        mmfPlayer = GetComponent<MMF_Player>();
        scrubber = GetComponent<TimelineVFXScrubber>();
    }

    public void PlayWithSeed(int seed)
    {
        if (IsDuplicatePlayCall())
            return;

        MarkPlayCall();

        if (scrubber != null)
            scrubber.SetSeed(seed);

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
