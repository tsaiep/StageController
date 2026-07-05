using MoreMountains.Feedbacks;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Stage Controller/Timeline VFX Seeded MMF Trigger")]
public class TimelineVFXSeededMMFTrigger : MonoBehaviour
{
    public MMF_Player mmfPlayer;
    public TimelineVFXScrubber scrubber;

    private void Reset()
    {
        mmfPlayer = GetComponent<MMF_Player>();
        scrubber = GetComponent<TimelineVFXScrubber>();
    }

    public void PlayWithSeed(int seed)
    {
        if (scrubber != null)
            scrubber.SetSeed(seed);

        if (mmfPlayer != null)
            mmfPlayer.PlayFeedbacks();
    }
}
