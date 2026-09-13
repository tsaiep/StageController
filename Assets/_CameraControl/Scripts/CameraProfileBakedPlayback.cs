using UnityEngine;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

// Scene bakes use the existing offsets as two target-local anchors.  This extension
// supplies a deterministic initial state before the normal Composer pipeline, so
// the result does not depend on the previous camera, playback FPS or seeking order.
[AddComponentMenu("")]
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class CameraProfileBakedPlayback : CinemachineExtension
{
    [System.NonSerialized] public bool Active;
    [System.NonSerialized] public bool General;
    CinemachinePositionComposer positionComposer;
    CinemachineRotationComposer rotationComposer;
    CinemachineFollow follow;
    ScreenComposerSettings positionComposition, rotationComposition;
    LookaheadSettings positionLookahead, rotationLookahead;
    Vector3 positionDamping;
    Vector2 rotationDamping;
    TrackerSettings tracker;
    float deadZoneDepth;
    bool positionCenter, rotationCenter, settingsTemporarilyChanged;

    public static CameraProfileBakedPlayback Configure(CinemachineCamera camera, bool active, bool general)
    {
        var extension = camera.GetComponent<CameraProfileBakedPlayback>();
        if (extension == null && active)
        {
            extension = camera.gameObject.AddComponent<CameraProfileBakedPlayback>();
            extension.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
        }
        if (extension != null)
        {
            if (extension.Active != active)
                camera.PreviousStateIsValid = false;
            extension.Active = active;
            extension.General = general;
        }
        return extension;
    }

    public override void PrePipelineMutateCameraStateCallback(
        CinemachineVirtualCameraBase vcam, ref CameraState state, float deltaTime)
    {
        RestoreSettings();
        if (!Active || !(vcam is CinemachineCamera camera) || camera.Follow == null || camera.LookAt == null)
            return;
        rotationComposer = camera.GetComponent<CinemachineRotationComposer>();
        positionComposer = General ? camera.GetComponent<CinemachinePositionComposer>() : null;
        follow = General ? null : camera.GetComponent<CinemachineFollow>();
        if (rotationComposer == null || (General ? positionComposer == null : follow == null))
            return;

        Vector3 aim = camera.LookAt.position + camera.LookAt.rotation * rotationComposer.TargetOffset;
        Vector3 position, direction;
        if (General)
        {
            Vector3 anchor = camera.Follow.position + camera.Follow.rotation * positionComposer.TargetOffset;
            direction = aim - anchor;
            if (direction.sqrMagnitude < 0.000001f) return;
            position = anchor - direction.normalized * positionComposer.CameraDistance;
        }
        else
        {
            position = camera.Follow.position + follow.FollowOffset; // baked Tracking uses WorldSpace
            direction = aim - position;
            if (direction.sqrMagnitude < 0.000001f) return;
        }
        state.RawPosition = position;
        state.RawOrientation = Quaternion.LookRotation(direction, state.ReferenceUp);
        camera.PreviousStateIsValid = false;

        // Only override settings during this evaluation; restore even when leaving a
        // baked clip for an ordinary profile.  Never persist changes to the camera rig.
        rotationComposition = rotationComposer.Composition;
        rotationLookahead = rotationComposer.Lookahead;
        rotationDamping = rotationComposer.Damping;
        rotationCenter = rotationComposer.CenterOnActivate;
        rotationComposer.Composition.DeadZone.Enabled = false;
        rotationComposer.Composition.HardLimits.Enabled = false;
        rotationComposer.Lookahead.Enabled = false;
        rotationComposer.Damping = Vector2.zero;
        rotationComposer.CenterOnActivate = true;
        if (positionComposer != null)
        {
            positionComposition = positionComposer.Composition;
            positionLookahead = positionComposer.Lookahead;
            positionDamping = positionComposer.Damping;
            positionCenter = positionComposer.CenterOnActivate;
            deadZoneDepth = positionComposer.DeadZoneDepth;
            positionComposer.Composition.DeadZone.Enabled = false;
            positionComposer.Composition.HardLimits.Enabled = false;
            positionComposer.Lookahead.Enabled = false;
            positionComposer.Damping = Vector3.zero;
            positionComposer.DeadZoneDepth = 0;
            positionComposer.CenterOnActivate = true;
        }
        if (follow != null)
        {
            tracker = follow.TrackerSettings;
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = Vector3.zero;
            follow.TrackerSettings.RotationDamping = Vector3.zero;
            follow.TrackerSettings.QuaternionDamping = 0;
        }
        settingsTemporarilyChanged = true;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Finalize) RestoreSettings();
    }

    void RestoreSettings()
    {
        if (!settingsTemporarilyChanged) return;
        settingsTemporarilyChanged = false;
        if (rotationComposer != null)
        {
            rotationComposer.Composition = rotationComposition;
            rotationComposer.Lookahead = rotationLookahead;
            rotationComposer.Damping = rotationDamping;
            rotationComposer.CenterOnActivate = rotationCenter;
        }
        if (positionComposer != null)
        {
            positionComposer.Composition = positionComposition;
            positionComposer.Lookahead = positionLookahead;
            positionComposer.Damping = positionDamping;
            positionComposer.DeadZoneDepth = deadZoneDepth;
            positionComposer.CenterOnActivate = positionCenter;
        }
        if (follow != null) follow.TrackerSettings = tracker;
    }

    protected override void OnDestroy()
    {
        RestoreSettings();
        base.OnDestroy();
    }
}
