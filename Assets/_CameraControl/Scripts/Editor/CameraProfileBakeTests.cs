using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using Object = UnityEngine.Object;

// No NUnit dependency: runnable from the menu in the project's existing editor.
public static class CameraProfileBakeTests
{
    const string Trigger = "Temp/CameraProfileBake.run";
    const string Report = "Reports/CameraProfileBakeRegression.txt";

    [InitializeOnLoadMethod]
    static void RunRequestedTests()
    {
        if (!File.Exists(Trigger)) return;
        File.Delete(Trigger);
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Camera System/Run Bake Regression Tests")]
    public static void Run()
    {
        var results = new List<string>();
        int failed = 0;
        Action<string, Action> test = (name, action) =>
        {
            try { action(); results.Add("PASS " + name); }
            catch (Exception e) { ++failed; results.Add("FAIL " + name + "\n" + e); }
        };
        foreach (var kind in new[] { CameraProfileWorkflow.BakeProfileType.General, CameraProfileWorkflow.BakeProfileType.Tracking })
        {
            test(kind + " / moving, rotating, scaled target + reverse/seek", () => TestPose(kind));
            test(kind + " / clip lengths 2s and 8s => 61 keys", () => TestClip(kind));
            test(kind + " / Timeline offsets + trim + speed + moving target", () => TestTimeline(kind, false));
            test(kind + " / Timeline Rotation Composer", () => TestTimeline(kind, true));
            test(kind + " / standard and legacy parameter blend equivalence", () => TestStandardBlend(kind));
        }
        test("Scene playback / old asset compatibility", TestPlaybackCompatibility);
        test("Tracking / standard scene bake and reverse/seek verification", TestStandardTracking);
        test("General / standard verification rejects unseeded pose drift", TestStandardGeneralRejection);
        foreach (CameraProfileWorkflow.BakeProfileType kind in Enum.GetValues(typeof(CameraProfileWorkflow.BakeProfileType)))
            test(kind + " / legacy procedural bake and debake", () => TestProcedural(kind));
        string report = DateTime.Now.ToString("s") + "\n" + string.Join("\n", results) + "\nFailures: " + failed;
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, report);
        if (failed == 0) Debug.Log(report); else Debug.LogError(report);
    }

    sealed class Fixture : IDisposable
    {
        public readonly Scene scene = EditorSceneManager.NewPreviewScene();
        public readonly List<Object> assets = new List<Object>();
        public T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); assets.Add(a); return a; }
        public GameObject Go(string name)
        {
            var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go;
        }
        public void Dispose()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var a in assets.AsEnumerable().Reverse()) if (a != null) Object.DestroyImmediate(a);
        }
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Keys(CameraProfileSO p)
    {
        foreach (var f in p.GetType().GetFields().Where(f => f.FieldType == typeof(AnimationCurve)))
        {
            var c = (AnimationCurve)f.GetValue(p);
            Check(c.length == 61 && c[0].time == 0 && c[c.length - 1].time == 1, f.Name + " must have 61 normalized keys");
        }
    }
    static CameraProfileBakeUtility.PoseSample Decode(CameraProfileSO p, float u, Transform target)
    {
        Func<string, float> curve = name => ((AnimationCurve)p.GetType().GetField(name).GetValue(p)).Evaluate(u);
        Func<string, Vector3> vector = name => new Vector3(curve(name + "XCurve"), curve(name + "YCurve"), curve(name + "ZCurve"));
        var aim = target.position + target.rotation * vector("rotTargetOffset");
        Vector3 position, direction;
        if (p is GeneralProfileSO)
        {
            var anchor = target.position + target.rotation * vector("posTargetOffset");
            direction = (aim - anchor).normalized;
            position = anchor - direction * curve("posDistanceCurve");
        }
        else { position = target.position + vector("followOffset"); direction = (aim - position).normalized; }
        var rotation = Quaternion.LookRotation(direction, Vector3.up);
        float dutch = p.dutchCurve != null ? p.dutchCurve.Evaluate(u) : 0f;
        return new CameraProfileBakeUtility.PoseSample
        {
            position = position, rotation = rotation * Quaternion.AngleAxis(dutch, Vector3.forward), dutch = dutch
        };
    }
    static void Compare(CameraProfileSO p, float u, Transform target, Transform camera, CinemachineCamera source = null)
    {
        var pose = Decode(p, u, target);
        Check(Vector3.Distance(pose.position, camera.position) < .003f,
            "u=" + u + " position: baked=" + pose.position.ToString("F4") + " source=" + camera.position.ToString("F4"));
        Quaternion expected = camera.rotation * Quaternion.AngleAxis(source != null ? source.Lens.Dutch : 0f, Vector3.forward);
        Check(Quaternion.Angle(pose.rotation, expected) < .08f,
            "u=" + u + " rotation: baked=" + pose.rotation.eulerAngles + " source=" + expected.eulerAngles);
    }
    static void TestPose(CameraProfileWorkflow.BakeProfileType kind)
    {
        using (var f = new Fixture())
        {
            CameraProfileSO p = kind == CameraProfileWorkflow.BakeProfileType.General ? (CameraProfileSO)f.Asset<GeneralProfileSO>() : f.Asset<TrackingProfileSO>();
            p.scenePoseBaked = true;
            foreach (var field in p.GetType().GetFields().Where(x => x.FieldType == typeof(AnimationCurve))) field.SetValue(p, new AnimationCurve());
            var poses = new List<CameraProfileBakeUtility.PoseSample>();
            for (int i = 0; i <= 120; ++i)
            {
                float u = i / 120f;
                var pose = new CameraProfileBakeUtility.PoseSample
                {
                    time = u, fov = 40 + 30 * u, aspect = 16f / 9,
                    position = new Vector3(Mathf.Sin(u * 6) * 8, u * 4, Mathf.Cos(u * 6) * 8),
                    rotation = Quaternion.Euler(20 * u, 350 * u, 0), dutch = 170 + 40 * u,
                    targetPosition = new Vector3(3 * u, 1, -2), targetRotation = Quaternion.Euler(10 * u, 120 * u, 30 * u)
                };
                poses.Add(pose); if (i % 2 == 0) CameraProfileBakeUtility.EncodePose(p, pose);
            }
            CameraProfileBakeUtility.Linearize(p); Keys(p);
            Check(Mathf.Abs(p.dutchCurve.Evaluate(.5f) - 190) < .001f, "Dutch must unwrap across +180 degrees");
            CameraProfileBakeUtility.Verify(p, poses, f.scene);
        }
    }
    static AnimationClip Clip(Fixture f, float length)
    {
        var clip = new AnimationClip { name = "Bake test " + length, frameRate = 60 };
        f.assets.Add(clip);
        Action<string, float, float> curve = (name, a, b) => AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), name), AnimationCurve.Linear(0, a, length, b));
        curve("m_LocalPosition.x", 0, length); curve("m_LocalPosition.y", 1, 2);
        curve("m_LocalPosition.z", -6, -5);
        Quaternion roll = Quaternion.Euler(0, 0, 25);
        curve("m_LocalRotation.x", roll.x, roll.x); curve("m_LocalRotation.y", roll.y, roll.y);
        curve("m_LocalRotation.z", roll.z, roll.z); curve("m_LocalRotation.w", roll.w, roll.w);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(CinemachineCamera), "Lens.FieldOfView"),
            AnimationCurve.Linear(0, 60, length, 80));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(CinemachineCamera), "Lens.Dutch"),
            AnimationCurve.Linear(0, 10, length, 20));
        return clip;
    }
    static CameraProfileWorkflow Workflow(Fixture f, CameraProfileWorkflow.BakeProfileType kind)
    {
        var camera = f.Go("Test source").AddComponent<CinemachineCamera>(); camera.enabled = false;
        camera.Follow = f.Go("Test target").transform;
        camera.Follow.SetPositionAndRotation(new Vector3(3, 2, 1), Quaternion.Euler(0, 37, 0));
        camera.Follow.localScale = new Vector3(2, .7f, 1.3f);
        var w = camera.gameObject.AddComponent<CameraProfileWorkflow>();
        // Existing regression cases explicitly exercise the original bake contract.
        w.scenePlaybackMode = CameraProfileWorkflow.ScenePlaybackMode.DeterministicPose;
        w.bakeSourceMode = CameraProfileWorkflow.BakeSourceMode.SceneTransform;
        w.bakeProfileType = kind;
        return w;
    }
    static void TestClip(CameraProfileWorkflow.BakeProfileType kind)
    {
        using (var f = new Fixture())
        {
            var w = Workflow(f, kind);
            foreach (float duration in new[] { 2f, 8f })
            {
                w.sourceAnimationClip = Clip(f, duration);
                var p = CameraProfileBakeUtility.Build(w, out _); f.assets.Add(p); Keys(p);
                Check(Mathf.Abs(p.fovCurve.Evaluate(.5f) - 70) < .001f, "Animated source FOV");
                foreach (float u in new[] { 0, .5f, 1 })
                {
                    w.sourceAnimationClip.SampleAnimation(w.gameObject, duration * u);
                    Compare(p, u, w.GetComponent<CinemachineCamera>().Follow, w.transform, w.GetComponent<CinemachineCamera>());
                }
            }
        }
    }
    static void TestTimeline(CameraProfileWorkflow.BakeProfileType kind, bool composer)
    {
        using (var f = new Fixture())
        {
            var w = Workflow(f, kind); var camera = w.GetComponent<CinemachineCamera>();
            var parent = f.Go("Camera parent").transform;
            parent.SetPositionAndRotation(new Vector3(2, 1, -3), Quaternion.Euler(0, 40, 0));
            camera.transform.SetParent(parent, false);
            if (composer)
            {
                var rot = camera.gameObject.AddComponent<CinemachineRotationComposer>();
                rot.Damping = new Vector2(.3f, .5f); rot.TargetOffset = new Vector3(.2f, 1, 0);
                rot.Composition.ScreenPosition = new Vector2(.1f, -.1f);
                camera.LookAt = camera.Follow;
            }
            var director = f.Go("Test director").AddComponent<PlayableDirector>(); director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            var timeline = f.Asset<TimelineAsset>(); director.playableAsset = timeline;
            var track = timeline.CreateTrack<AnimationTrack>(null, "Camera"); f.assets.Add(track);
            track.trackOffset = TrackOffset.ApplyTransformOffsets;
            track.position = new Vector3(-1, 0, 2); track.rotation = Quaternion.Euler(0, -20, 0);
            var animator = camera.gameObject.AddComponent<Animator>(); animator.applyRootMotion = true;
            director.SetGenericBinding(track, animator);
            var clip = track.CreateClip<AnimationPlayableAsset>(); var asset = (AnimationPlayableAsset)clip.asset; f.assets.Add(asset);
            asset.clip = Clip(f, 4); asset.removeStartOffset = false;
            asset.position = new Vector3(2, 1, 3); asset.rotation = Quaternion.Euler(0, 30, 0);
            clip.start = 10; clip.duration = 1; clip.clipIn = .5; clip.timeScale = 1.5;
            typeof(TimelineClip).GetProperty(nameof(TimelineClip.postExtrapolationMode)).SetValue(clip, TimelineClip.ClipExtrapolation.Hold);
            var other = track.CreateClip<AnimationPlayableAsset>(); f.assets.Add(other.asset);
            ((AnimationPlayableAsset)other.asset).clip = asset.clip; other.start = 20; other.duration = 4;
            var targetTrack = timeline.CreateTrack<AnimationTrack>(null, "Target"); f.assets.Add(targetTrack);
            targetTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
            var targetAnimator = camera.Follow.gameObject.AddComponent<Animator>();
            targetAnimator.applyRootMotion = true;
            director.SetGenericBinding(targetTrack, targetAnimator);
            var targetClip = targetTrack.CreateClip<AnimationPlayableAsset>(); f.assets.Add(targetClip.asset);
            ((AnimationPlayableAsset)targetClip.asset).clip = Clip(f, 15); targetClip.start = 0; targetClip.duration = 15;
            ((AnimationPlayableAsset)targetClip.asset).removeStartOffset = false;
            w.sceneAnimationSource = CameraProfileWorkflow.SceneAnimationSource.PlayableDirector;
            w.scenePlayableDirector = director; w.selectedTimelineClip = asset;
            var originalPosition = camera.transform.position; var originalTime = director.time;
            var p = CameraProfileBakeUtility.Build(w, out string report); f.assets.Add(p); Keys(p);
            Check(report.Contains("1s →"), "Must bake selected 1s clip, not 24s timeline");
            Check(director.time == originalTime && camera.transform.position == originalPosition, "Baking mutated source scene");
            // A headless editor has no output Brain and defaults to aspect 1.
            // Use the same explicit lens override for native source evaluation.
            var output = f.Go("Source comparison lens").AddComponent<Camera>();
            output.enabled = false; output.aspect = w.fallbackAspect;
            camera.gameObject.AddComponent<CameraProfileBakeTestLens>().Source = output;
            director.RebuildGraph();
            for (int i = 0; i <= 120; ++i)
            {
                float u = i / 120f;
                director.time = 10 + u; director.Evaluate();
                camera.InternalUpdateCameraState(Vector3.up, i == 0 ? -1 : 1f / 120);
                if (i % 2 == 0) Compare(p, u, camera.Follow, camera.transform, camera);
            }
            director.Stop();
        }
    }

    static void TestProcedural(CameraProfileWorkflow.BakeProfileType kind)
    {
        using (var f = new Fixture())
        {
            var w = Workflow(f, kind);
            w.bakeSourceMode = CameraProfileWorkflow.BakeSourceMode.ProceduralAnimationClip;
            var rot = w.gameObject.AddComponent<CinemachineRotationComposer>(); rot.Damping = new Vector2(.3f, .7f);
            Type body;
            string property;
            if (kind == CameraProfileWorkflow.BakeProfileType.General)
            {
                body = typeof(CinemachinePositionComposer); property = "CameraDistance";
                w.gameObject.AddComponent<CinemachinePositionComposer>().Damping = new Vector3(.1f, .2f, .3f);
            }
            else if (kind == CameraProfileWorkflow.BakeProfileType.Tracking)
            {
                body = typeof(CinemachineFollow); property = "FollowOffset.x";
                w.gameObject.AddComponent<CinemachineFollow>();
            }
            else { body = typeof(CinemachineSplineDolly); property = "m_SplineSettings.Position"; w.gameObject.AddComponent<CinemachineSplineDolly>(); }
            var clip = new AnimationClip { name = "Procedural regression" }; f.assets.Add(clip);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", body, property), AnimationCurve.Linear(0, 2, 4, 6));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(CinemachineCamera), "Lens.Dutch"),
                AnimationCurve.Linear(0, 170, 4, 210));
            w.sourceAnimationClip = clip;
            var p = CameraProfileBakeUtility.Build(w, out _); f.assets.Add(p); Keys(p);
            Check(!p.scenePoseBaked, "Legacy procedural profile must not use scene playback");
            Check(Mathf.Abs(p.dutchCurve.Evaluate(.5f) - 190) < .001f, "Procedural Dutch sampling");
            var restored = CameraProfileBakeUtility.CreateAnimationClip(p, 60); f.assets.Add(restored);
            foreach (var binding in AnimationUtility.GetCurveBindings(restored))
                Check(AnimationUtility.GetEditorCurve(restored, binding).length == 61, "Debake normalized divisions");
            Check(restored.length == 1, "Debake normalized duration");
            restored.SampleAnimation(w.gameObject, .5f);
            Check(Mathf.Abs(rot.Damping.y - .7f) < .0001f, "Debake damping preserved");
            Check(Mathf.Abs(w.GetComponent<CinemachineCamera>().Lens.Dutch - 190) < .001f, "Debake Dutch preserved");
            float result = kind == CameraProfileWorkflow.BakeProfileType.General ? w.GetComponent<CinemachinePositionComposer>().CameraDistance
                : kind == CameraProfileWorkflow.BakeProfileType.Tracking ? w.GetComponent<CinemachineFollow>().FollowOffset.x
                : w.GetComponent<CinemachineSplineDolly>().SplineSettings.Position;
            Check(Mathf.Abs(result - 4) < .001f, "Procedural normalized sampling");
            if (kind != CameraProfileWorkflow.BakeProfileType.Dolly)
            {
                p.scenePoseBaked = true;
                var sceneRestored = CameraProfileBakeUtility.CreateAnimationClip(p, 60); f.assets.Add(sceneRestored);
                Check(sceneRestored.length == 1, "Scene bake debake normalized duration");
                sceneRestored.SampleAnimation(w.gameObject, .5f);
                if (kind == CameraProfileWorkflow.BakeProfileType.Tracking)
                {
                    var follow = w.GetComponent<CinemachineFollow>();
                    Check(follow.TrackerSettings.BindingMode == Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace,
                        "Scene Tracking debake uses WorldSpace binding; actual=" + follow.TrackerSettings.BindingMode);
                    Check(follow.TrackerSettings.RotationDamping == Vector3.zero && Mathf.Approximately(follow.TrackerSettings.QuaternionDamping, 0f),
                        "Scene Tracking debake clears target rotation damping");
                }
            }
            p.scenePoseBaked = false;
            p.dutchCurve = null; // Simulate an asset serialized before dutchCurve existed.
            var legacy = CameraProfileBakeUtility.CreateAnimationClip(p, 60); f.assets.Add(legacy);
            var dutchBinding = AnimationUtility.GetCurveBindings(legacy).Single(b => b.propertyName == "Lens.Dutch");
            Check(Mathf.Abs(AnimationUtility.GetEditorCurve(legacy, dutchBinding).Evaluate(.5f)) < .0001f,
                "Missing legacy Dutch curve defaults to zero");
        }
    }


    static void TestPlaybackCompatibility()
    {
        using (var f = new Fixture())
        {
            var profile = f.Asset<GeneralProfileSO>();
            // Editor JSON wraps ScriptableObjects in a MonoBehaviour root.
            EditorJsonUtility.FromJsonOverwrite("{\"MonoBehaviour\":{\"scenePoseBaked\":true}}", profile);
            Check(profile.RequiresBakedPoseInitialization, "Existing Scene assets must retain initialization");
            profile.standardScenePlayback = true;
            Check(!profile.RequiresBakedPoseInitialization, "Standard Scene assets use ordinary playback");
            profile.scenePoseBaked = false;
            Check(!profile.RequiresBakedPoseInitialization, "Ordinary assets never need scene initialization");
        }
    }

    static void TestStandardTracking()
    {
        using (var f = new Fixture())
        {
            var w = Workflow(f, CameraProfileWorkflow.BakeProfileType.Tracking);
            w.scenePlaybackMode = CameraProfileWorkflow.ScenePlaybackMode.StandardParameters;
            w.sourceAnimationClip = Clip(f, 4);
            var profile = CameraProfileBakeUtility.Build(w, out string report);
            f.assets.Add(profile);
            Check(profile.scenePoseBaked && profile.standardScenePlayback, "Standard bake mode was not retained");
            Check(!profile.RequiresBakedPoseInitialization, "Standard bake must not silently fall back");
            Check(report.Contains("Standard Parameters"), "Report must identify the verified solver");
            Keys(profile);
        }
    }

    static void TestStandardGeneralRejection()
    {
        using (var f = new Fixture())
        {
            var w = Workflow(f, CameraProfileWorkflow.BakeProfileType.General);
            w.scenePlaybackMode = CameraProfileWorkflow.ScenePlaybackMode.StandardParameters;
            w.sourceAnimationClip = Clip(f, 4);
            var existing = f.Asset<GeneralProfileSO>();
            w.targetProfileSO = existing;
            string before = EditorJsonUtility.ToJson(existing);
            bool rejected = false;
            try
            {
                var profile = CameraProfileBakeUtility.Build(w, out _);
                f.assets.Add(profile);
            }
            catch (InvalidOperationException e)
            {
                rejected = e.Message.Contains("Standard Parameters") && e.Message.Contains("未寫入 SO");
                if (rejected) Debug.Log("Expected Standard General rejection:\n" + e.Message);
            }
            Check(rejected, "Unseeded General must report pose error instead of silently enabling initialization");
            Check(EditorJsonUtility.ToJson(existing) == before, "Failed verification changed the existing asset");
        }
    }

    // Exercise the actual mixer application and Cinemachine pipeline. The control
    // has identical curves but no Scene flags: equality demonstrates that blending
    // uses ordinary SO semantics, without assuming a particular camera trajectory.
    static void TestStandardBlend(CameraProfileWorkflow.BakeProfileType kind)
    {
        using (var f = new Fixture())
        {
            bool general = kind == CameraProfileWorkflow.BakeProfileType.General;
            CameraProfileSO baked = general ? (CameraProfileSO)f.Asset<GeneralProfileSO>() : f.Asset<TrackingProfileSO>();
            CameraProfileSO legacy = general ? (CameraProfileSO)f.Asset<GeneralProfileSO>() : f.Asset<TrackingProfileSO>();
            foreach (var field in baked.GetType().GetFields().Where(x => x.FieldType == typeof(AnimationCurve)))
                field.SetValue(baked, new AnimationCurve());
            CameraProfileBakeUtility.EncodePose(baked, new CameraProfileBakeUtility.PoseSample
            {
                time = 0, position = new Vector3(2, 3, -8), rotation = Quaternion.Euler(12, 27, 0),
                targetPosition = Vector3.zero, targetRotation = Quaternion.identity, fov = 55, dutch = 25
            });
            baked.scenePoseBaked = true;
            baked.standardScenePlayback = true;
            var control = Object.Instantiate(baked); f.assets.Add(control);
            control.scenePoseBaked = false; control.standardScenePlayback = false;
            var target = f.Go("Blend target").transform;
            var secondTarget = f.Go("Other blend target").transform;
            secondTarget.SetPositionAndRotation(new Vector3(4, 2, 3), Quaternion.Euler(0, 75, 0));
            var a = BlendCamera(f, general); var b = BlendCamera(f, general);
            var mixerA = new CameraProfileMixer(); var mixerB = new CameraProfileMixer();
            try
            {
                foreach (float dt in new[] { 1f / 24, 1f / 60, 1f / 120, -1f })
                {
                    a.ForceCameraPosition(new Vector3(-6, 5, -12), Quaternion.Euler(15, 30, 0));
                    b.ForceCameraPosition(new Vector3(-6, 5, -12), Quaternion.Euler(15, 30, 0));
                    a.PreviousStateIsValid = b.PreviousStateIsValid = false;
                    foreach (float weight in new[] { 0f, .001f, .25f, .5f, .75f, .999f, 1f, .5f, 0f })
                    {
                        target.SetPositionAndRotation(new Vector3(weight, 1, -weight), Quaternion.Euler(0, 70 * weight, 0));
                        ApplyTestBlend(mixerA, a, baked, legacy, target, secondTarget, weight, general);
                        ApplyTestBlend(mixerB, b, control, legacy, target, secondTarget, weight, general);
                        a.InternalUpdateCameraState(Vector3.up, dt);
                        b.InternalUpdateCameraState(Vector3.up, dt);
                        Check(Vector3.Distance(a.State.GetFinalPosition(), b.State.GetFinalPosition()) < .0001f,
                            "Standard/ordinary position differs at weight=" + weight + " dt=" + dt);
                        Check(Quaternion.Angle(a.State.GetFinalOrientation(), b.State.GetFinalOrientation()) < .02f,
                            "Standard/ordinary rotation differs at weight=" + weight + " dt=" + dt);
                        Check(Mathf.Abs(a.State.Lens.FieldOfView - b.State.Lens.FieldOfView) < .0001f, "FOV differs");
                        var extension = a.GetComponent<CameraProfileBakedPlayback>();
                        Check(extension == null || !extension.Active, "Standard blend enabled scene initialization");
                        float actualDamping = general ? a.GetComponent<CinemachinePositionComposer>().Damping.x
                            : a.GetComponent<CinemachineFollow>().TrackerSettings.PositionDamping.x;
                        Check(Mathf.Abs(actualDamping - (1 - weight)) < .0001f, "Legacy damping was not blended normally");
                    }
                }
            }
            finally
            {
                mixerA.OnPlayableDestroy(Playable.Null);
                mixerB.OnPlayableDestroy(Playable.Null);
            }
        }
    }

    static CinemachineCamera BlendCamera(Fixture f, bool general)
    {
        var camera = f.Go("Parameter blend verification").AddComponent<CinemachineCamera>();
        camera.enabled = false;
        var rot = camera.gameObject.AddComponent<CinemachineRotationComposer>();
        rot.Composition.DeadZone.Enabled = rot.Composition.HardLimits.Enabled = rot.Lookahead.Enabled = false;
        if (general)
        {
            var pos = camera.gameObject.AddComponent<CinemachinePositionComposer>();
            pos.Composition.DeadZone.Enabled = pos.Composition.HardLimits.Enabled = pos.Lookahead.Enabled = false;
            pos.DeadZoneDepth = 0;
        }
        else camera.gameObject.AddComponent<CinemachineFollow>().TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
        return camera;
    }

    static void ApplyTestBlend(CameraProfileMixer mixer, CinemachineCamera camera, CameraProfileSO first,
        CameraProfileSO second, Transform target, Transform secondTarget, float firstWeight, bool general)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        if (firstWeight == 0 || firstWeight == 1)
        {
            var profile = firstWeight == 1 ? first : second;
            var chosenTarget = firstWeight == 1 ? target : secondTarget;
            typeof(CameraProfileMixer).GetMethod(general ? "ApplyGeneralProfile" : "ApplyTrackingProfile", flags)
                .Invoke(mixer, general ? new object[] { camera, profile, chosenTarget, null, .5f, false }
                    : new object[] { camera, profile, chosenTarget, null, .5f });
            return;
        }
        var inputType = typeof(CameraProfileMixer).GetNestedType("CameraProfileInput", BindingFlags.NonPublic);
        var inputs = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(inputType));
        for (int i = 0; i < 2; ++i)
        {
            object input = Activator.CreateInstance(inputType);
            inputType.GetField("Weight").SetValue(input, i == 0 ? firstWeight : 1 - firstWeight);
            inputType.GetField("NormalizedTime").SetValue(input, .5f);
            inputType.GetField("Profile").SetValue(input, i == 0 ? first : second);
            inputType.GetField("Target").SetValue(input, i == 0 ? target : secondTarget);
            inputs.Add(input);
        }
        typeof(CameraProfileMixer).GetMethod(general ? "ApplyBlendedGeneralProfile" : "ApplyBlendedTrackingProfile", flags)
            .Invoke(mixer, general ? new object[] { camera, inputs, false } : new object[] { camera, inputs });
    }
}

// Test-only lens control keeps source comparison independent of Game View size.
[ExecuteAlways]
public sealed class CameraProfileBakeTestLens : CinemachineExtension
{
    public Camera Source;
    public override void PrePipelineMutateCameraStateCallback(CinemachineVirtualCameraBase camera, ref CameraState state, float deltaTime)
    {
        if (Source != null) state.Lens.PullInheritedPropertiesFromCamera(Source);
    }
    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase camera,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime) { }
}
