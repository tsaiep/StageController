using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.VFX;

[ExecuteAlways]
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[AddComponentMenu("Stage Controller/Timeline VFX Scrubber")]
public class TimelineVFXScrubber : MonoBehaviour
{
    [Header("References")]
    public PlayableDirector director;
    public List<VisualEffect> vfxList = new List<VisualEffect>();

    [Header("Simulation")]
    public float targetFPS = 60f;
    public uint seed = 12345;
    public bool simulateInEditMode = true;

    private const double UninitializedTime = double.NaN;
    private const double TriggerTimeEpsilon = 1e-5;
    private const double TriggerMergeWindow = 0.05;
    private const double SeekThreshold = 0.1;
    private const bool SampleTimelinePropertiesDuringRebuild = true;

    private readonly List<double> triggerTimelineTimes = new List<double>();
    [FormerlySerializedAs("vfx")]
    [SerializeField, HideInInspector] private VisualEffect legacyVfx;
    [SerializeField, HideInInspector] private PlayableDirector autoAssignedDirector;
    [SerializeField, HideInInspector] private bool directorManuallyOverridden;

    private double lastSimulatedTimelineTime = UninitializedTime;
    private bool activeSession;
    private bool isSamplingTimeline;
    private bool isRebuilding;
    private bool vfxIsResetOrHidden;

    private float FixedStep => 1f / Mathf.Max(1f, targetFPS);

    private void Reset()
    {
        vfxList = new List<VisualEffect>();
        VisualEffect localVfx = GetComponent<VisualEffect>();
        if (localVfx != null)
            vfxList.Add(localVfx);

        directorManuallyOverridden = false;
        AutoAssignDirector();
    }

    private void OnEnable()
    {
        EnsureReferences();
        PrepareVFXForManagedSimulation();
        triggerTimelineTimes.Clear();
        activeSession = false;
        lastSimulatedTimelineTime = UninitializedTime;

        if (CanSimulateNow())
            ResetOrHideVFX(GetCurrentTimelineTimeOrZero());
    }

    private void OnValidate()
    {
        targetFPS = Mathf.Max(1f, targetFPS);

        EnsureVFXReferences();

        ResolveDirectorReference();
        triggerTimelineTimes.Sort();
    }

    private void LateUpdate()
    {
        Tick();
    }

    public void PlayManagedBurst()
    {
        if (isSamplingTimeline || isRebuilding)
            return;

        EnsureReferences();
        if (director == null)
            return;

        double signalTimelineTime = director.time;
        if (HasSimulatedPast(signalTimelineTime))
            RemoveTriggersAtOrAfter(signalTimelineTime - TriggerTimeEpsilon);

        if (activeSession &&
            triggerTimelineTimes.Count > 0 &&
            signalTimelineTime < GetSessionStartTime() - TriggerMergeWindow)
        {
            ClearRuntimeSession(signalTimelineTime, true);
        }

        activeSession = true;
        bool addedTriggerTime = AddTriggerTime(signalTimelineTime);

        bool shouldRebuild = ShouldRebuildForSignal(signalTimelineTime);
        if (!addedTriggerTime && !shouldRebuild)
            return;

        if (shouldRebuild)
        {
            RebuildToCurrentTimelineTime();
            return;
        }

        PrepareVFXForManagedSimulation();
        vfxIsResetOrHidden = false;
        SendManagedPlayEvent();
    }

    [ContextMenu("Clear Managed Bursts")]
    public void ClearManagedBursts()
    {
        ClearRuntimeSession(GetCurrentTimelineTimeOrZero(), true);
    }

    private void ClearRuntimeSession(double targetTimelineTime, bool resetVFX)
    {
        triggerTimelineTimes.Clear();
        activeSession = false;
        lastSimulatedTimelineTime = UninitializedTime;

        if (resetVFX)
            ResetOrHideVFX(targetTimelineTime);
    }

    public void RebuildToCurrentTimelineTime()
    {
        if (!CanSimulateNow() || director == null)
            return;

        RebuildTo(director.time, SampleTimelinePropertiesDuringRebuild);
    }

    public void RebuildTo(double targetTimelineTime)
    {
        if (!CanSimulateNow())
            return;

        RebuildTo(targetTimelineTime, SampleTimelinePropertiesDuringRebuild);
    }

    private void Tick()
    {
        if (isRebuilding || isSamplingTimeline || !CanSimulateNow())
            return;

        PrepareVFXForManagedSimulation();

        if (!activeSession)
        {
            ResetOrHideVFX(director.time);
            return;
        }

        double currentTimelineTime = director.time;
        if (triggerTimelineTimes.Count == 0)
        {
            ClearRuntimeSession(currentTimelineTime, true);
            return;
        }

        bool isRewind = !double.IsNaN(lastSimulatedTimelineTime) &&
                        currentTimelineTime < lastSimulatedTimelineTime;
        if (isRewind)
        {
            RemoveTriggersAtOrAfter(currentTimelineTime - TriggerTimeEpsilon);
            if (triggerTimelineTimes.Count == 0)
            {
                ClearRuntimeSession(currentTimelineTime, true);
                return;
            }
        }

        double sessionStartTime = GetSessionStartTime();
        if (currentTimelineTime < sessionStartTime - TriggerMergeWindow)
        {
            ClearRuntimeSession(currentTimelineTime, true);
            return;
        }

        double localTime = currentTimelineTime - sessionStartTime;
        if (localTime < 0.0)
        {
            ResetOrHideVFX(currentTimelineTime);
            return;
        }

        if (vfxIsResetOrHidden || double.IsNaN(lastSimulatedTimelineTime))
        {
            RebuildTo(currentTimelineTime, SampleTimelinePropertiesDuringRebuild);
            return;
        }

        double timelineDelta = currentTimelineTime - lastSimulatedTimelineTime;
        if (timelineDelta < 0.0 || timelineDelta > SeekThreshold)
        {
            RebuildTo(currentTimelineTime, SampleTimelinePropertiesDuringRebuild);
            return;
        }

        if (timelineDelta <= 0.0)
            return;

        bool sampleForwardTimeline = SampleTimelinePropertiesDuringRebuild && director.state != PlayState.Playing;
        SimulateForward(timelineDelta, currentTimelineTime, sampleForwardTimeline);
    }

    private bool CanSimulateNow()
    {
        if (!Application.isPlaying && !simulateInEditMode)
            return false;

        EnsureReferences();
        return director != null && HasAnyVFX();
    }

    private double GetCurrentTimelineTimeOrZero()
    {
        return director != null ? director.time : 0.0;
    }

    private void EnsureReferences()
    {
        EnsureVFXReferences();
        ResolveDirectorReference();
    }

    private void EnsureVFXReferences()
    {
        if (vfxList == null)
            vfxList = new List<VisualEffect>();

        if (legacyVfx != null)
        {
            if (!vfxList.Contains(legacyVfx))
                vfxList.Add(legacyVfx);

            legacyVfx = null;
        }

        if (HasAnyVFX())
            return;

        VisualEffect localVfx = GetComponent<VisualEffect>();
        if (localVfx != null)
            vfxList.Add(localVfx);
    }

    private bool HasAnyVFX()
    {
        if (vfxList == null)
            return false;

        for (int i = 0; i < vfxList.Count; i++)
        {
            if (vfxList[i] != null)
                return true;
        }

        return false;
    }

    private void ResolveDirectorReference()
    {
        if (directorManuallyOverridden)
        {
            if (director != null)
                return;

            directorManuallyOverridden = false;
        }

        if (autoAssignedDirector != null &&
            director != null &&
            director != autoAssignedDirector)
        {
            directorManuallyOverridden = true;
            return;
        }

        if (director == null)
            directorManuallyOverridden = false;

        if (!directorManuallyOverridden)
            AutoAssignDirector();
    }

    private void AutoAssignDirector()
    {
        autoAssignedDirector = FindDefaultPlayableDirector();
        director = autoAssignedDirector;
    }

    private static PlayableDirector FindDefaultPlayableDirector()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            Array.Sort(roots, CompareRootSiblingIndex);

            for (int i = 0; i < roots.Length; i++)
            {
                PlayableDirector directorInRoot = FindFirstDirectorInHierarchy(roots[i].transform);
                if (directorInRoot != null)
                    return directorInRoot;
            }
        }

        return null;
    }

    private static int CompareRootSiblingIndex(GameObject a, GameObject b)
    {
        return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
    }

    private static PlayableDirector FindFirstDirectorInHierarchy(Transform root)
    {
        PlayableDirector directorOnThisObject = root.GetComponent<PlayableDirector>();
        if (directorOnThisObject != null)
            return directorOnThisObject;

        for (int i = 0; i < root.childCount; i++)
        {
            PlayableDirector directorInChild = FindFirstDirectorInHierarchy(root.GetChild(i));
            if (directorInChild != null)
                return directorInChild;
        }

        return null;
    }

    private void PrepareVFXForManagedSimulation()
    {
        if (vfxList == null)
            return;

        for (int i = 0; i < vfxList.Count; i++)
        {
            VisualEffect currentVfx = vfxList[i];
            if (currentVfx == null)
                continue;

            currentVfx.pause = true;
            currentVfx.resetSeedOnPlay = false;
            currentVfx.startSeed = seed;
        }
    }

    private void ResetOrHideVFX(double targetTimelineTime)
    {
        if (!HasAnyVFX())
            return;

        if (vfxIsResetOrHidden)
        {
            lastSimulatedTimelineTime = targetTimelineTime;
            return;
        }

        PrepareVFXForManagedSimulation();

        for (int i = 0; i < vfxList.Count; i++)
        {
            VisualEffect currentVfx = vfxList[i];
            if (currentVfx == null)
                continue;

            currentVfx.Stop();
            currentVfx.Reinit();
            currentVfx.Stop();
        }

        vfxIsResetOrHidden = true;
        lastSimulatedTimelineTime = targetTimelineTime;
    }

    private void RebuildTo(double targetTimelineTime, bool sampleTimelineProperties)
    {
        if (!HasAnyVFX())
            return;

        isRebuilding = true;

        try
        {
            double sessionStartTime = GetSessionStartTime();
            double localTime = targetTimelineTime - sessionStartTime;
            if (!activeSession || localTime < 0.0)
            {
                ResetOrHideVFX(targetTimelineTime);
                return;
            }

            PrepareVFXForManagedSimulation();

            for (int i = 0; i < vfxList.Count; i++)
            {
                VisualEffect currentVfx = vfxList[i];
                if (currentVfx == null)
                    continue;

                currentVfx.Reinit();
                currentVfx.pause = true;
            }

            vfxIsResetOrHidden = false;
            SimulateFromZero(sessionStartTime, localTime, sampleTimelineProperties);
            lastSimulatedTimelineTime = targetTimelineTime;
        }
        finally
        {
            isRebuilding = false;
        }
    }

    private void SimulateFromZero(double sessionStartTime, double targetLocalTime, bool sampleTimelineProperties)
    {
        double simulated = 0.0;
        double step = FixedStep;
        int nextTriggerIndex = GetNextTriggerIndexAfter(sessionStartTime);

        using (new DirectorTimeSampler(this, sampleTimelineProperties))
        {
            while (simulated < targetLocalTime)
            {
                double nextLocalTime = Math.Min(targetLocalTime, simulated + step);
                double nextTriggerLocalTime;

                if (TryGetNextTriggerLocalTime(sessionStartTime, simulated, nextLocalTime, nextTriggerIndex, out nextTriggerLocalTime))
                {
                    SimulateSegment(sessionStartTime, simulated, nextTriggerLocalTime);
                    simulated = nextTriggerLocalTime;
                    SendManagedPlayEvent();
                    nextTriggerIndex++;
                    continue;
                }

                SimulateSegment(sessionStartTime, simulated, nextLocalTime);
                simulated = nextLocalTime;
            }
        }
    }

    private void SimulateSegment(double sessionStartTime, double fromLocalTime, double toLocalTime)
    {
        double delta = toLocalTime - fromLocalTime;
        if (delta <= 0.0)
            return;

        SampleTimelineAt(sessionStartTime + toLocalTime);
        SimulateAllVFX((float)delta);
    }

    private bool TryGetNextTriggerLocalTime(
        double sessionStartTime,
        double currentLocalTime,
        double maxLocalTime,
        int nextTriggerIndex,
        out double nextTriggerLocalTime)
    {
        nextTriggerLocalTime = 0.0;

        if (nextTriggerIndex < 0 || nextTriggerIndex >= triggerTimelineTimes.Count)
            return false;

        nextTriggerLocalTime = triggerTimelineTimes[nextTriggerIndex] - sessionStartTime;
        return nextTriggerLocalTime > currentLocalTime + TriggerTimeEpsilon &&
               nextTriggerLocalTime <= maxLocalTime + TriggerTimeEpsilon;
    }

    private int GetNextTriggerIndexAfter(double sessionStartTime)
    {
        for (int i = 0; i < triggerTimelineTimes.Count; i++)
        {
            if (triggerTimelineTimes[i] > sessionStartTime + TriggerTimeEpsilon)
                return i;
        }

        return triggerTimelineTimes.Count;
    }

    private void SimulateForward(double timelineDelta, double targetTimelineTime, bool sampleTimelineProperties)
    {
        double simulated = 0.0;
        double step = FixedStep;
        double startTime = lastSimulatedTimelineTime;

        using (new DirectorTimeSampler(this, sampleTimelineProperties))
        {
            while (simulated + step <= timelineDelta)
            {
                double stepTimelineTime = startTime + simulated + step;
                SampleTimelineAt(stepTimelineTime);
                SimulateAllVFX((float)step);
                simulated += step;
            }

            double remainder = timelineDelta - simulated;
            if (remainder > 0.0)
            {
                SampleTimelineAt(targetTimelineTime);
                SimulateAllVFX((float)remainder);
            }
        }

        lastSimulatedTimelineTime = targetTimelineTime;
    }

    private double GetSessionStartTime()
    {
        return triggerTimelineTimes.Count > 0 ? triggerTimelineTimes[0] : 0.0;
    }

    private bool HasSimulatedPast(double timelineTime)
    {
        return !double.IsNaN(lastSimulatedTimelineTime) &&
               timelineTime < lastSimulatedTimelineTime - TriggerTimeEpsilon;
    }

    private bool ShouldRebuildForSignal(double signalTimelineTime)
    {
        if (vfxIsResetOrHidden || double.IsNaN(lastSimulatedTimelineTime))
            return true;

        double delta = signalTimelineTime - lastSimulatedTimelineTime;
        return delta < 0.0 || delta > SeekThreshold;
    }

    private bool AddTriggerTime(double triggerTime)
    {
        for (int i = 0; i < triggerTimelineTimes.Count; i++)
        {
            if (Math.Abs(triggerTimelineTimes[i] - triggerTime) <= TriggerMergeWindow)
                return false;

            if (triggerTime < triggerTimelineTimes[i])
            {
                triggerTimelineTimes.Insert(i, triggerTime);
                return true;
            }
        }

        triggerTimelineTimes.Add(triggerTime);
        return true;
    }

    private void RemoveTriggersAtOrAfter(double timelineTime)
    {
        for (int i = triggerTimelineTimes.Count - 1; i >= 0; i--)
        {
            if (triggerTimelineTimes[i] >= timelineTime)
                triggerTimelineTimes.RemoveAt(i);
        }
    }

    private void SendManagedPlayEvent()
    {
        if (vfxList == null)
            return;

        for (int i = 0; i < vfxList.Count; i++)
        {
            VisualEffect currentVfx = vfxList[i];
            if (currentVfx == null)
                continue;

            currentVfx.SendEvent(VisualEffectAsset.PlayEventName);
        }
    }

    private void SimulateAllVFX(float deltaTime)
    {
        if (vfxList == null)
            return;

        for (int i = 0; i < vfxList.Count; i++)
        {
            VisualEffect currentVfx = vfxList[i];
            if (currentVfx == null)
                continue;

            currentVfx.Simulate(deltaTime, 1u);
        }
    }

    private void SampleTimelineAt(double timelineTime)
    {
        if (!isSamplingTimeline || director == null)
            return;

        director.time = timelineTime;
        director.Evaluate();
    }

    private readonly struct DirectorTimeSampler : IDisposable
    {
        private readonly TimelineVFXScrubber owner;
        private readonly bool enabled;
        private readonly double originalTime;

        public DirectorTimeSampler(TimelineVFXScrubber owner, bool enabled)
        {
            this.owner = owner;
            this.enabled = enabled && owner.director != null;
            originalTime = this.enabled ? owner.director.time : 0.0;

            if (this.enabled)
                owner.isSamplingTimeline = true;
        }

        public void Dispose()
        {
            if (!enabled)
                return;

            owner.director.time = originalTime;
            owner.director.Evaluate();
            owner.isSamplingTimeline = false;
        }
    }
}
