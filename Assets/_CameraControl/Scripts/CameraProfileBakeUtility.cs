#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using Object = UnityEngine.Object;

public static class CameraProfileBakeUtility
{
    const float PositionTolerance = 0.003f;
    const float RotationTolerance = 0.08f;

    public static TimelineClip[] GetTimelineClips(PlayableDirector director)
    {
        if (director == null || !(director.playableAsset is TimelineAsset timeline)) return Array.Empty<TimelineClip>();
        return AllTracks(timeline.GetRootTracks()).OfType<AnimationTrack>()
            .SelectMany(t => t.GetClips()).Where(c => c.asset is AnimationPlayableAsset a && a.clip != null).ToArray();
    }

    static IEnumerable<TrackAsset> AllTracks(IEnumerable<TrackAsset> tracks)
    {
        foreach (var t in tracks)
        {
            yield return t;
            foreach (var child in AllTracks(t.GetChildTracks())) yield return child;
        }
    }

    public static CameraProfileSO Build(CameraProfileWorkflow w, out string report)
    {
        report = "";
        if (Application.isPlaying) throw new InvalidOperationException("請離開 Play Mode 後烘焙。");
        if (w.sampleRate < 2 || w.sampleRate > 120) throw new InvalidOperationException("Normalized Divisions 必須在 2–120 之間。");
        bool scene = w.bakeSourceMode == CameraProfileWorkflow.BakeSourceMode.SceneTransform;
        bool timelineMode = scene && w.sceneAnimationSource == CameraProfileWorkflow.SceneAnimationSource.PlayableDirector;
        var selected = timelineMode ? GetTimelineClips(w.scenePlayableDirector).SingleOrDefault(c => c.asset == w.selectedTimelineClip) : null;
        if (timelineMode && selected == null) throw new InvalidOperationException("請指定 Director，並在 Target Timeline Clip 選擇單一片段。");
        if (!timelineMode && w.sourceAnimationClip == null) throw new InvalidOperationException("請指定 Source Animation Clip。");
        if (scene && w.bakeProfileType == CameraProfileWorkflow.BakeProfileType.Dolly)
            throw new InvalidOperationException("Scene Transform 支援 General／Tracking；Dolly 請用 Procedural Animation Clip。");

        Transform binding = timelineMode ? BindingTransform(w.scenePlayableDirector, selected.GetParentTrack()) : null;
        if (timelineMode && binding == null) throw new InvalidOperationException("選定 Animation Track 尚未綁定場景 Animator。");
        if (timelineMode && selected.GetParentTrack().parent is AnimationTrack)
            throw new InvalidOperationException("相機來源請放在獨立 Animation Track；目前不接受 Override Track 作為單一來源。");
        Transform source = scene ? w.animatedCameraTransform : w.transform;
        if (source == null && binding != null)
        {
            if (binding.GetComponent<CinemachineCamera>() != null || binding.GetComponent<Camera>() != null) source = binding;
            else
            {
                var cameras = binding.GetComponentsInChildren<CinemachineCamera>(true);
                if (cameras.Length == 1) source = cameras[0].transform;
                else throw new InvalidOperationException("Track 綁定階層無法唯一辨識相機，請指定 Source Camera。");
            }
        }
        if (source == null) source = w.transform;
        if (timelineMode && source != binding && !source.IsChildOf(binding))
            throw new InvalidOperationException("Source Camera 不在所選 Animation Track 的綁定階層下。");

        var sourceVcam = source.GetComponent<CinemachineCamera>();
        var workflowVcam = w.GetComponent<CinemachineCamera>();
        Transform target = w.sceneTrackingTarget != null ? w.sceneTrackingTarget
            : (sourceVcam != null ? sourceVcam.Follow : workflowVcam.Follow);
        if (scene && target == null) throw new InvalidOperationException("找不到 Playback Target。請指定或設定來源 Cinemachine Camera 的 Follow。");
        if (scene && (target == source || target.IsChildOf(source)))
            throw new InvalidOperationException("Playback Target 不能是相機本身或相機的子物件。");

        double duration = timelineMode ? selected.duration : w.sourceAnimationClip.length;
        if (duration <= 0 || double.IsNaN(duration) || double.IsInfinity(duration))
            throw new InvalidOperationException("來源片段必須有有效長度。");
        var profile = NewProfile(w.bakeProfileType);
        try
        {
            profile.scenePoseBaked = scene;
            foreach (var f in CurveFields(profile)) f.SetValue(profile, new AnimationCurve());
            using (var sampling = new SamplingScope(w, source, sourceVcam, target, binding, selected))
            {
                if (!scene) ValidateProcedural(sampling.SourceCamera, w.bakeProfileType);
                var poses = new List<PoseSample>();
                float? previousDutch = null;
                // The number of curve keys never depends on source duration. Extra
                // integration steps reproduce source damping without adding output keys.
                double previous = 0;
                for (int i = 0; i <= w.sampleRate * (scene ? 2 : 1); ++i)
                {
                    float u = (float)i / (w.sampleRate * (scene ? 2 : 1));
                    double time = duration * u;
                    if (scene && i > 0 && sampling.HasProceduralSource)
                    {
                        for (double step = previous + 1.0 / 60; step < time - 0.0000001; step += 1.0 / 60)
                        {
                            sampling.Evaluate(step, (float)(step - previous), false, scene);
                            previous = step;
                        }
                    }
                    sampling.Evaluate(time, i == 0 ? -1 : (float)Math.Min(time - previous, 1.0 / 60), i == 0, scene);
                    previous = time;
                    if (scene)
                    {
                        var pose = sampling.ReadPose(u);
                        pose.dutch = UnwrapDegrees(previousDutch, pose.dutch);
                        previousDutch = pose.dutch;
                        ValidatePose(pose);
                        poses.Add(pose);
                        if ((i & 1) == 0) EncodePose(profile, pose);
                    }
                    else
                    {
                        float dutch = UnwrapDegrees(previousDutch, sampling.SourceCamera.Lens.Dutch);
                        previousDutch = dutch;
                        SampleProcedural(profile, sampling.SourceCamera, u, i == 0, dutch);
                    }
                    if (EditorUtility.DisplayCancelableProgressBar("Camera Profile Bake", "取樣 " + (timelineMode ? selected.displayName : w.sourceAnimationClip.name), u * 0.6f))
                        throw new OperationCanceledException("已取消；原場景與 SO 未變更。");
                }
                Linearize(profile);
                string verification = scene ? Verify(profile, poses, sampling.Scene) : "Procedural 曲線已取樣（不包含場景 Transform）。";
                report = (timelineMode ? selected.GetParentTrack().name + " / " + selected.displayName : w.sourceAnimationClip.name)
                    + "\n來源長度 " + duration.ToString("0.###") + "s → 0–1，" + (w.sampleRate + 1) + " keys / curve"
                    + "\n" + verification
                    + (scene ? "\nFOV：" + sampling.LensDescription + "\n回放 Target 需對應來源 Target；Tracking FollowOffset 使用 World Space。"
                        : "\nDamping 為第一個取樣值，SO 不支援 Damping 動畫。");
            }
            return profile;
        }
        catch { Object.DestroyImmediate(profile); throw; }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static CameraProfileSO NewProfile(CameraProfileWorkflow.BakeProfileType kind) =>
        kind == CameraProfileWorkflow.BakeProfileType.General ? ScriptableObject.CreateInstance<GeneralProfileSO>()
        : kind == CameraProfileWorkflow.BakeProfileType.Tracking ? (CameraProfileSO)ScriptableObject.CreateInstance<TrackingProfileSO>()
        : ScriptableObject.CreateInstance<DollyProfileSO>();

    static IEnumerable<FieldInfo> CurveFields(CameraProfileSO profile) =>
        profile.GetType().GetFields().Where(f => f.FieldType == typeof(AnimationCurve));

    static void Key(CameraProfileSO profile, string field, float t, float value) =>
        ((AnimationCurve)profile.GetType().GetField(field).GetValue(profile)).AddKey(t, value);
    static void VectorKeys(CameraProfileSO p, string prefix, float t, Vector3 value)
    {
        Key(p, prefix + "XCurve", t, value.x);
        Key(p, prefix + "YCurve", t, value.y);
        Key(p, prefix + "ZCurve", t, value.z);
    }

    public struct PoseSample
    {
        public float time, fov, aspect, dutch;
        public Vector3 position, targetPosition;
        public Quaternion rotation, targetRotation;
    }

    static void ValidatePose(PoseSample p)
    {
        if (!Finite(p.position.x) || !Finite(p.position.y) || !Finite(p.position.z)
            || !Finite(p.targetPosition.x) || !Finite(p.targetPosition.y) || !Finite(p.targetPosition.z)
            || !Finite(p.fov) || p.fov < 10 || p.fov > 120 || !Finite(p.aspect) || p.aspect <= 0 || !Finite(p.dutch))
            throw new InvalidOperationException("在 u=" + p.time + " 的 Pose／FOV 無效；回放端 FOV 支援 10–120 度。");
        Vector3 forward = p.rotation * Vector3.forward;
        if (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.9999f)
            throw new InvalidOperationException("u=" + p.time + " 鏡頭接近完全垂直，Rotation Composer 存在奇異點。");
    }
    static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    static float UnwrapDegrees(float? previous, float value) =>
        previous.HasValue ? previous.Value + Mathf.DeltaAngle(previous.Value, value) : value;

    // Use target rotation, not InverseTransformPoint: Cinemachine TargetOffset
    // ignores target scale. Two distinct anchors encode direction without new pose curves.
    public static void EncodePose(CameraProfileSO profile, PoseSample p)
    {
        float distance = Mathf.Max(0.1f, Vector3.Distance(p.position, p.targetPosition));
        Vector3 forward = p.rotation * Vector3.forward;
        Quaternion inverse = Quaternion.Inverse(p.targetRotation);
        Vector3 aim = p.position + forward * (distance * 2);
        Key(profile, "fovCurve", p.time, p.fov);
        Key(profile, "dutchCurve", p.time, p.dutch);
        VectorKeys(profile, "rotTargetOffset", p.time, inverse * (aim - p.targetPosition));
        Key(profile, "rotScreenXCurve", p.time, 0);
        Key(profile, "rotScreenYCurve", p.time, 0);
        if (profile is GeneralProfileSO g)
        {
            Vector3 anchor = p.position + forward * distance;
            VectorKeys(g, "posTargetOffset", p.time, inverse * (anchor - p.targetPosition));
            Key(g, "posDistanceCurve", p.time, distance);
            Key(g, "posScreenXCurve", p.time, 0);
            Key(g, "posScreenYCurve", p.time, 0);
            g.posDampingX = g.posDampingY = g.posDampingZ = g.rotDampingX = g.rotDampingY = 0;
        }
        else if (profile is TrackingProfileSO tr)
        {
            VectorKeys(tr, "followOffset", p.time, p.position - p.targetPosition);
            tr.dampingX = tr.dampingY = tr.dampingZ = tr.rotDampingX = tr.rotDampingY = 0;
        }
    }

    static void ValidateProcedural(CinemachineCamera vcam, CameraProfileWorkflow.BakeProfileType kind)
    {
        if (vcam == null || vcam.GetComponent<CinemachineRotationComposer>() == null
            || (kind == CameraProfileWorkflow.BakeProfileType.General && vcam.GetComponent<CinemachinePositionComposer>() == null)
            || (kind == CameraProfileWorkflow.BakeProfileType.Tracking && vcam.GetComponent<CinemachineFollow>() == null)
            || (kind == CameraProfileWorkflow.BakeProfileType.Dolly && vcam.GetComponent<CinemachineSplineDolly>() == null))
            throw new InvalidOperationException("Procedural 模式需要相符的 Position 元件及 Rotation Composer。");
    }
    static void SampleProcedural(CameraProfileSO p, CinemachineCamera camera, float t, bool first, float dutch)
    {
        var rot = camera.GetComponent<CinemachineRotationComposer>();
        Key(p, "fovCurve", t, camera.Lens.FieldOfView);
        Key(p, "dutchCurve", t, dutch);
        VectorKeys(p, "rotTargetOffset", t, rot.TargetOffset);
        Key(p, "rotScreenXCurve", t, rot.Composition.ScreenPosition.x);
        Key(p, "rotScreenYCurve", t, rot.Composition.ScreenPosition.y);
        if (p is GeneralProfileSO g)
        {
            var pos = camera.GetComponent<CinemachinePositionComposer>();
            Key(p, "posDistanceCurve", t, pos.CameraDistance);
            VectorKeys(p, "posTargetOffset", t, pos.TargetOffset);
            Key(p, "posScreenXCurve", t, pos.Composition.ScreenPosition.x);
            Key(p, "posScreenYCurve", t, pos.Composition.ScreenPosition.y);
            if (first)
            {
                g.posDampingX = pos.Damping.x; g.posDampingY = pos.Damping.y; g.posDampingZ = pos.Damping.z;
                g.rotDampingX = rot.Damping.x; g.rotDampingY = rot.Damping.y;
            }
        }
        else if (p is TrackingProfileSO tr)
        {
            var follow = camera.GetComponent<CinemachineFollow>();
            VectorKeys(p, "followOffset", t, follow.FollowOffset);
            if (first)
            {
                tr.dampingX = follow.TrackerSettings.PositionDamping.x;
                tr.dampingY = follow.TrackerSettings.PositionDamping.y;
                tr.dampingZ = follow.TrackerSettings.PositionDamping.z;
                tr.rotDampingX = rot.Damping.x; tr.rotDampingY = rot.Damping.y;
            }
        }
        else if (p is DollyProfileSO d)
        {
            var dolly = camera.GetComponent<CinemachineSplineDolly>();
            Key(p, "splinePositionCurve", t, dolly.SplineSettings.Position);
            if (first)
            {
                d.positionUnits = dolly.SplineSettings.Units;
                d.rotDampingX = rot.Damping.x; d.rotDampingY = rot.Damping.y;
            }
        }
    }

    public static void Linearize(CameraProfileSO p)
    {
        foreach (var field in CurveFields(p))
        {
            var curve = (AnimationCurve)field.GetValue(p);
            for (int i = 0; i < curve.length; ++i)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
        }
    }

    // Exercise the same private application methods as the real Custom Track,
    // then the real Cinemachine pipeline, including reverse/random seeks.
    public static string Verify(CameraProfileSO profile, IList<PoseSample> poses, Scene scene)
    {
        var go = new GameObject("Bake verification camera");
        SceneManager.MoveGameObjectToScene(go, scene);
        var target = new GameObject("Bake verification target");
        SceneManager.MoveGameObjectToScene(target, scene);
        var mixer = new CameraProfileMixer();
        try
        {
            var camera = go.AddComponent<CinemachineCamera>();
            camera.enabled = false;
            go.AddComponent<CinemachineRotationComposer>();
            bool general = profile is GeneralProfileSO;
            if (general) go.AddComponent<CinemachinePositionComposer>(); else go.AddComponent<CinemachineFollow>();
            var apply = typeof(CameraProfileMixer).GetMethod(general ? "ApplyGeneralProfile" : "ApplyTrackingProfile",
                BindingFlags.Instance | BindingFlags.NonPublic);
            float maxPosition = 0, maxRotation = 0, midPosition = 0, midRotation = 0;
            string firstError = "";
            for (int pass = 0; pass < 3; ++pass)
            {
                for (int n = 0; n < poses.Count; ++n)
                {
                    int index = pass == 0 ? n : pass == 1 ? poses.Count - 1 - n : (n % 2 == 0 ? n / 2 : poses.Count - 1 - n / 2);
                    var p = poses[index];
                    target.transform.SetPositionAndRotation(p.targetPosition, p.targetRotation);
                    // Targets may be scaled in production; offsets must ignore this scale.
                    target.transform.localScale = new Vector3(2, 0.7f, 1.3f);
                    if (general) apply.Invoke(mixer, new object[] { camera, profile, target.transform, null, p.time, false });
                    else apply.Invoke(mixer, new object[] { camera, profile, target.transform, null, p.time });
                    camera.InternalUpdateCameraState(Vector3.up, pass == 0 ? 1f / 60 : pass == 1 ? 1f / 24 : -1);
                    float posError = Vector3.Distance(camera.State.GetFinalPosition(), p.position);
                    Quaternion expectedRotation = p.rotation * Quaternion.AngleAxis(p.dutch, Vector3.forward);
                    float rotError = Quaternion.Angle(camera.State.GetFinalOrientation(), expectedRotation);
                    if (firstError.Length == 0 && (index & 1) == 0 && (posError > PositionTolerance || rotError > RotationTolerance))
                        firstError = "\nu=" + p.time + " pass=" + pass + " expected=" + p.position.ToString("F4") + "/" + expectedRotation.eulerAngles
                            + " actual=" + camera.State.GetFinalPosition().ToString("F4") + "/" + camera.State.GetFinalOrientation().eulerAngles
                            + " aim=" + camera.State.ReferenceLookAt.ToString("F4");
                    if ((index & 1) == 0)
                    {
                        maxPosition = Mathf.Max(maxPosition, posError); maxRotation = Mathf.Max(maxRotation, rotError);
                    }
                    else { midPosition = Mathf.Max(midPosition, posError); midRotation = Mathf.Max(midRotation, rotError); }
                    if (!Finite(posError) || !Finite(rotError))
                        throw new InvalidOperationException("回放驗證出現非有限值。");
                }
            }
            if (maxPosition > PositionTolerance || maxRotation > RotationTolerance)
                throw new InvalidOperationException("Custom Track 回放驗證未通過；未寫入 SO。\n位置誤差 "
                    + maxPosition.ToString("0.00000") + "m，角度誤差 " + maxRotation.ToString("0.000") + "°" + firstError);
            return "Custom Track 正播／倒播／跳時間驗證通過。\n鍵位置最大誤差 " + maxPosition.ToString("0.00000")
                + "m / " + maxRotation.ToString("0.000") + "°\n鍵間插值最大誤差 " + midPosition.ToString("0.00000")
                + "m / " + midRotation.ToString("0.000") + "°"
                + (midPosition > PositionTolerance || midRotation > RotationTolerance ? "（可提高等分數改善）" : "");
        }
        finally
        {
            mixer.OnPlayableDestroy(Playable.Null);
            Object.DestroyImmediate(go); Object.DestroyImmediate(target);
        }
    }

    public static AnimationClip CreateAnimationClip(CameraProfileSO profile, int divisions)
    {
        if (profile == null) throw new InvalidOperationException("請先指定 Profile。");
        if (divisions < 2 || divisions > 120) throw new InvalidOperationException("Normalized Divisions 必須在 2–120。");
        var clip = new AnimationClip { frameRate = divisions };
        foreach (var field in CurveFields(profile))
        {
            string name = field.Name;
            Type type;
            string property;
            if (name == "fovCurve") { type = typeof(CinemachineCamera); property = "Lens.FieldOfView"; }
            else if (name == "dutchCurve") { type = typeof(CinemachineCamera); property = "Lens.Dutch"; }
            else if (name == "posDistanceCurve") { type = typeof(CinemachinePositionComposer); property = "CameraDistance"; }
            else if (name == "splinePositionCurve") { type = typeof(CinemachineSplineDolly); property = "m_SplineSettings.Position"; }
            else
            {
                bool pos = name.StartsWith("pos");
                bool follow = name.StartsWith("follow");
                type = pos ? typeof(CinemachinePositionComposer) : follow ? typeof(CinemachineFollow) : typeof(CinemachineRotationComposer);
                string axis = name.Substring(name.Length - 6, 1).ToLowerInvariant();
                property = (follow ? "FollowOffset." : name.Contains("Screen") ? "Composition.ScreenPosition." : "TargetOffset.") + axis;
            }
            var source = (AnimationCurve)field.GetValue(profile) ?? AnimationCurve.Linear(0, 0, 1, 0);
            var curve = new AnimationCurve();
            for (int i = 0; i <= divisions; ++i) { float t = (float)i / divisions; curve.AddKey(t, source.Evaluate(t)); }
            for (int i = 0; i < curve.length; ++i)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", type, property), curve);
        }
        void Constant(Type type, string property, float value)
        {
            var keys = new Keyframe[divisions + 1];
            for (int i = 0; i <= divisions; ++i) keys[i] = new Keyframe((float)i / divisions, value);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", type, property), new AnimationCurve(keys));
        }
        void ConstantDiscrete(Type type, string property, int value)
        {
            var keys = new Keyframe[divisions + 1];
            for (int i = 0; i <= divisions; ++i) keys[i] = new Keyframe((float)i / divisions, value);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.DiscreteCurve("", type, property), new AnimationCurve(keys));
        }
        float rx, ry;
        if (profile is GeneralProfileSO g)
        {
            Constant(typeof(CinemachinePositionComposer), "Damping.x", g.posDampingX);
            Constant(typeof(CinemachinePositionComposer), "Damping.y", g.posDampingY);
            Constant(typeof(CinemachinePositionComposer), "Damping.z", g.posDampingZ);
            rx = g.rotDampingX; ry = g.rotDampingY;
        }
        else if (profile is TrackingProfileSO tr)
        {
            Constant(typeof(CinemachineFollow), "TrackerSettings.PositionDamping.x", tr.dampingX);
            Constant(typeof(CinemachineFollow), "TrackerSettings.PositionDamping.y", tr.dampingY);
            Constant(typeof(CinemachineFollow), "TrackerSettings.PositionDamping.z", tr.dampingZ);
            if (profile.scenePoseBaked)
            {
                ConstantDiscrete(typeof(CinemachineFollow), "TrackerSettings.BindingMode", 4); // BindingMode.WorldSpace
                Constant(typeof(CinemachineFollow), "TrackerSettings.RotationDamping.x", 0f);
                Constant(typeof(CinemachineFollow), "TrackerSettings.RotationDamping.y", 0f);
                Constant(typeof(CinemachineFollow), "TrackerSettings.RotationDamping.z", 0f);
                Constant(typeof(CinemachineFollow), "TrackerSettings.QuaternionDamping", 0f);
            }
            rx = tr.rotDampingX; ry = tr.rotDampingY;
        }
        else
        {
            var d = (DollyProfileSO)profile;
            Constant(typeof(CinemachineSplineDolly), "m_SplineSettings.Units", (float)d.positionUnits);
            rx = d.rotDampingX; ry = d.rotDampingY;
        }
        Constant(typeof(CinemachineRotationComposer), "Damping.x", rx);
        Constant(typeof(CinemachineRotationComposer), "Damping.y", ry);
        return clip;
    }

    static Transform BindingTransform(PlayableDirector director, TrackAsset track)
    {
        Object binding = director.GetGenericBinding(track);
        while (binding == null && track.parent is TrackAsset parent)
        { track = parent; binding = director.GetGenericBinding(track); }
        return binding is GameObject go ? go.transform : binding is Component c ? c.transform : null;
    }

    // Only transforms, animators and camera components are cloned. Signals, audio,
    // custom tracks and gameplay scripts never run, and the live scene isn't sampled.
    sealed class SamplingScope : IDisposable
    {
        public Scene Scene { get; private set; }
        public CinemachineCamera SourceCamera { get; private set; }
        public bool HasProceduralSource => SourceCamera != null
            && SourceCamera.GetComponents<CinemachineComponentBase>().Any(c => c.enabled);
        public string LensDescription { get; private set; }
        readonly CameraProfileWorkflow workflow;
        readonly Dictionary<Transform, Transform> transforms = new Dictionary<Transform, Transform>();
        readonly List<Object> assets = new List<Object>();
        readonly Dictionary<Transform, (Vector3 p, Quaternion r, Vector3 s)> defaults = new Dictionary<Transform, (Vector3, Quaternion, Vector3)>();
        Transform poseTransform, playbackTarget, clipRoot;
        Camera unityCamera;
        CinemachineCamera lensVcam;
        PlayableDirector director;
        TimelineClip selected;
        float aspect;
        bool scene;
        public SamplingScope(CameraProfileWorkflow w, Transform source, CinemachineCamera vcam, Transform target,
            Transform binding, TimelineClip selectedClip)
        {
            workflow = w;
            selected = selectedClip;
            scene = w.bakeSourceMode == CameraProfileWorkflow.BakeSourceMode.SceneTransform;
            Scene = EditorSceneManager.NewPreviewScene();
            try
            {
                poseTransform = CloneHierarchy(source);
                if (target != null) playbackTarget = CloneHierarchy(target);
                if (vcam != null) SourceCamera = CopyCamera(vcam);
                lensVcam = SourceCamera;
                if (lensVcam == null && source.GetComponent<Camera>() == null && w.sceneLensCamera == null)
                    lensVcam = CopyCamera(w.GetComponent<CinemachineCamera>());
                var sourceUnityCamera = w.sceneLensCamera != null ? w.sceneLensCamera : source.GetComponent<Camera>();
                if (sourceUnityCamera != null)
                {
                    var tr = CloneHierarchy(sourceUnityCamera.transform);
                    unityCamera = tr.gameObject.AddComponent<Camera>();
                    EditorUtility.CopySerialized(sourceUnityCamera, unityCamera);
                    unityCamera.enabled = false;
                }
                var brain = CinemachineCore.FindPotentialTargetBrain(vcam != null ? vcam : w.GetComponent<CinemachineCamera>());
                if (scene && brain != null && Vector3.Angle(brain.DefaultWorldUp, Vector3.up) > 0.01f)
                    throw new InvalidOperationException("目前場景反解支援 World Up = Y，請移除自訂 World Up。");
                aspect = sourceUnityCamera != null ? sourceUnityCamera.aspect
                    : brain != null && brain.OutputCamera != null ? brain.OutputCamera.aspect : w.fallbackAspect;
                if (scene && SourceCamera != null)
                {
                    // PullStateFromVirtualCamera otherwise picks any live Brain even
                    // for our isolated camera. Lock source output mode/aspect explicitly.
                    var lensObject = new GameObject("Isolated source lens");
                    SceneManager.MoveGameObjectToScene(lensObject, Scene);
                    var lensSource = lensObject.AddComponent<Camera>();
                    var output = sourceUnityCamera != null ? sourceUnityCamera : brain != null ? brain.OutputCamera : null;
                    if (output != null) EditorUtility.CopySerialized(output, lensSource);
                    lensSource.enabled = false;
                    lensSource.aspect = aspect;
                    var lensOverride = SourceCamera.gameObject.AddComponent<CameraProfileBakeLensOverride>();
                    lensOverride.Source = lensSource;
                }
                LensDescription = vcam != null ? vcam.name + " / Cinemachine Lens"
                    : sourceUnityCamera != null ? sourceUnityCamera.name + " / Unity Camera" : w.name + " / Cinemachine Lens";
                if (selected != null) CreateTimeline(binding);
                else
                {
                    var root = scene && w.sceneAnimationRoot != null ? w.sceneAnimationRoot : source;
                    if (source != root && !source.IsChildOf(root))
                        throw new InvalidOperationException("Source Camera 必須位於 Animation Root 階層內。");
                    clipRoot = CloneHierarchy(root);
                    EnsureAnimatedComponents(root, w.sourceAnimationClip);
                }
                foreach (var t in transforms.Values)
                    defaults[t] = (t.localPosition, t.localRotation, t.localScale);
            }
            catch { Dispose(); throw; }
        }

        Transform Map(Transform source)
        {
            if (transforms.TryGetValue(source, out var existing)) return existing;
            var go = new GameObject(source.name);
            SceneManager.MoveGameObjectToScene(go, Scene);
            var clone = go.transform;
            transforms[source] = clone;
            if (source.parent != null) clone.SetParent(Map(source.parent), false);
            clone.localPosition = source.localPosition; clone.localRotation = source.localRotation; clone.localScale = source.localScale;
            return clone;
        }
        Transform CloneHierarchy(Transform source)
        {
            var clone = Map(source);
            foreach (Transform child in source) CloneHierarchy(child);
            return clone;
        }
        CinemachineCamera CopyCamera(CinemachineCamera original)
        {
            if (scene && original.GetComponents<CinemachineExtension>().Any(e => e.enabled && !(e is CameraProfileBakedPlayback)))
                throw new InvalidOperationException("來源包含 Cinemachine Extension；隔離取樣不會執行場景碰撞／自訂擴充。請先停用擴充或錄成 Transform Clip。");
            Transform clone = CloneHierarchy(original.transform);
            var result = clone.GetComponent<CinemachineCamera>();
            if (result != null) return result;
            result = clone.gameObject.AddComponent<CinemachineCamera>();
            EditorUtility.CopySerialized(original, result);
            result.enabled = false;
            result.Follow = original.Follow != null ? CloneHierarchy(original.Follow) : null;
            result.LookAt = original.LookAt != null ? CloneHierarchy(original.LookAt) : result.Follow;
            foreach (var component in original.GetComponents<CinemachineComponentBase>())
            {
                if (scene && component.enabled && component.Stage == CinemachineCore.Stage.Aim
                    && !(component is CinemachineRotationComposer))
                    throw new InvalidOperationException("來源 Rotation Control 目前支援 None／Rotation Composer。");
                if (scene && component.enabled && component.Stage == CinemachineCore.Stage.Noise)
                    throw new InvalidOperationException("請暫時關閉來源 Noise 後烘焙。");
                var copy = clone.gameObject.AddComponent(component.GetType());
                EditorUtility.CopySerialized(component, copy);
            }
            return result;
        }
        void EnsureAnimatedComponents(Transform originalRoot, AnimationClip clip)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type == typeof(Transform)) continue;
                var obj = AnimationUtility.GetAnimatedObject(originalRoot.gameObject, binding) as Component;
                if (obj == null) continue;
                var clone = Map(obj.transform);
                if (clone.GetComponent(binding.type) != null) continue;
                if (obj is CinemachineCamera cm) CopyCamera(cm);
                else if (obj is Camera || obj is CinemachineComponentBase)
                {
                    var copy = clone.gameObject.AddComponent(binding.type);
                    EditorUtility.CopySerialized(obj, copy);
                    if (copy is Camera cam) cam.enabled = false;
                }
            }
        }

        void CreateTimeline(Transform cameraBinding)
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            assets.Add(timeline);
            var directorObject = new GameObject("Isolated animation bake");
            SceneManager.MoveGameObjectToScene(directorObject, Scene);
            director = directorObject.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.playableAsset = timeline;
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = Math.Max(selected.end + 1, 1);

            var originals = AllTracks(((TimelineAsset)workflow.scenePlayableDirector.playableAsset).GetRootTracks()).OfType<AnimationTrack>().ToArray();
            var tracks = new Dictionary<AnimationTrack, AnimationTrack>();
            foreach (var original in originals)
            {
                bool chosen = original == selected.GetParentTrack();
                if (!chosen && original.mutedInHierarchy) continue;
                var originalBinding = BindingTransform(workflow.scenePlayableDirector, original);
                if (originalBinding == null) continue;
                if (!chosen && originalBinding == cameraBinding) continue;
                var animatorTransform = CloneHierarchy(originalBinding);
                var animator = animatorTransform.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = animatorTransform.gameObject.AddComponent<Animator>();
                    var sourceAnimator = originalBinding.GetComponent<Animator>();
                    if (sourceAnimator != null) animator.avatar = sourceAnimator.avatar;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.applyRootMotion = true;
                }
                AnimationTrack parent = null;
                if (original.parent is AnimationTrack originalParent && !tracks.TryGetValue(originalParent, out parent)) continue;
                var track = timeline.CreateTrack<AnimationTrack>(parent, original.name);
                assets.Add(track);
                tracks[original] = track;
                track.avatarMask = original.avatarMask;
                track.applyAvatarMask = original.applyAvatarMask;
                track.trackOffset = TrackOffset.ApplyTransformOffsets;
                track.position = original.position; track.rotation = original.rotation;
                bool sceneOffsets = original.trackOffset == TrackOffset.ApplySceneOffsets;
#pragma warning disable 618
                sceneOffsets |= original.trackOffset == TrackOffset.Auto && originalBinding.GetComponent<Animator>()?.runtimeAnimatorController != null;
#pragma warning restore 618
                if (sceneOffsets)
                {
                    track.position = originalBinding.localPosition;
                    track.rotation = originalBinding.localRotation;
                    // Timeline keeps the pre-preview scene offsets here. Reading them
                    // avoids accidentally baking the current preview pose a second time.
                    if (AnimationMode.InAnimationMode())
                    {
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        var pos = typeof(AnimationTrack).GetField("m_SceneOffsetPosition", flags);
                        var rot = typeof(AnimationTrack).GetField("m_SceneOffsetRotation", flags);
                        if (pos != null && rot != null)
                        {
                            track.position = (Vector3)pos.GetValue(original);
                            track.rotation = Quaternion.Euler((Vector3)rot.GetValue(original));
                        }
                    }
                }
                if (parent == null) director.SetGenericBinding(track, animator);
                var clipList = chosen ? new[] { selected } : original.GetClips().ToArray();
                foreach (var clip in clipList)
                {
                    if (!(clip.asset is AnimationPlayableAsset a) || a.clip == null) continue;
                    EnsureAnimatedComponents(originalBinding, a.clip);
                    var copy = track.CreateClip<AnimationPlayableAsset>();
                    var asset = (AnimationPlayableAsset)copy.asset;
                    assets.Add(asset);
                    EditorUtility.CopySerialized(a, asset);
                    copy.displayName = clip.displayName;
                    copy.start = clip.start; copy.duration = clip.duration;
                    copy.clipIn = clip.clipIn; copy.timeScale = clip.timeScale;
                    // Timeline exposes these setters internally; do not modify the source asset.
                    typeof(TimelineClip).GetProperty(nameof(TimelineClip.preExtrapolationMode)).SetValue(copy,
                        chosen ? TimelineClip.ClipExtrapolation.None : clip.preExtrapolationMode);
                    typeof(TimelineClip).GetProperty(nameof(TimelineClip.postExtrapolationMode)).SetValue(copy,
                        chosen ? TimelineClip.ClipExtrapolation.Hold : clip.postExtrapolationMode);
                    copy.easeInDuration = chosen ? 0 : clip.easeInDuration;
                    copy.easeOutDuration = chosen ? 0 : clip.easeOutDuration;
                    if (!chosen)
                    {
                        copy.mixInCurve = new AnimationCurve(clip.mixInCurve.keys);
                        copy.mixOutCurve = new AnimationCurve(clip.mixOutCurve.keys);
                    }
                }
                if (!original.inClipMode && original.infiniteClip != null)
                {
                    EnsureAnimatedComponents(originalBinding, original.infiniteClip);
                    var serialized = new SerializedObject(track);
                    serialized.FindProperty("m_InfiniteClip").objectReferenceValue = original.infiniteClip;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    track.infiniteClipOffsetPosition = original.infiniteClipOffsetPosition;
                    track.infiniteClipOffsetRotation = original.infiniteClipOffsetRotation;
                }
            }
            director.RebuildGraph();
        }

        public void Evaluate(double localTime, float dt, bool first, bool evaluateCamera)
        {
            foreach (var item in defaults)
            {
                item.Key.localPosition = item.Value.p;
                item.Key.localRotation = item.Value.r;
                item.Key.localScale = item.Value.s;
            }
            if (director != null) { director.time = selected.start + localTime; director.Evaluate(); }
            else workflow.sourceAnimationClip.SampleAnimation(clipRoot.gameObject, (float)localTime);
            if (evaluateCamera && SourceCamera != null)
                SourceCamera.InternalUpdateCameraState(Vector3.up, first ? -1 : dt);
        }

        public PoseSample ReadPose(float u)
        {
            float fov;
            var lens = SourceCamera != null ? SourceCamera.State.Lens : lensVcam != null ? lensVcam.Lens : LensSettings.Default;
            if (lensVcam != null)
            {
                if (lens.Orthographic || lens.IsPhysicalCamera)
                    throw new InvalidOperationException("目前 SO 支援一般透視 FOV，不支援 Orthographic／Physical Camera。");
                fov = lens.FieldOfView;
            }
            else
            {
                if (unityCamera == null) throw new InvalidOperationException("找不到來源鏡頭。");
                if (unityCamera.orthographic || unityCamera.usePhysicalProperties)
                    throw new InvalidOperationException("請使用一般 Perspective Camera。");
                fov = unityCamera.fieldOfView;
            }
            Vector3 position = SourceCamera != null ? SourceCamera.State.GetFinalPosition() : poseTransform.position;
            Quaternion finalRotation = SourceCamera != null ? SourceCamera.State.GetFinalOrientation() : poseTransform.rotation;
            Vector3 forward = finalRotation * Vector3.forward;
            Quaternion levelRotation = Quaternion.LookRotation(forward, Vector3.up);
            float dutch = Vector3.SignedAngle(levelRotation * Vector3.up, finalRotation * Vector3.up, forward);
            return new PoseSample
            {
                time = u, fov = fov, aspect = aspect, dutch = dutch,
                position = position, rotation = levelRotation,
                targetPosition = playbackTarget.position, targetRotation = playbackTarget.rotation
            };
        }
        public void Dispose()
        {
            if (director != null) director.Stop();
            if (Scene.IsValid()) EditorSceneManager.ClosePreviewScene(Scene);
            for (int i = assets.Count - 1; i >= 0; --i) if (assets[i] != null) Object.DestroyImmediate(assets[i]);
        }
    }
}

// Editor-only, attached only to the isolated sampling camera, never saved in a scene.
[ExecuteAlways]
sealed class CameraProfileBakeLensOverride : CinemachineExtension
{
    public Camera Source;
    public override void PrePipelineMutateCameraStateCallback(CinemachineVirtualCameraBase camera, ref CameraState state, float deltaTime)
    {
        if (Source != null) state.Lens.PullInheritedPropertiesFromCamera(Source);
    }
    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase camera,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime) { }
}
#endif
