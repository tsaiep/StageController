using System;
using System.Collections.Generic;
using System.Reflection;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

[AddComponentMenu("Stage Controller/Audio Analyzer Group Control")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public class AudioAnalyzerGroupControl : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool scriptsActive = true;

    [Header("Fade In / Out")]
    [Min(0f)] [SerializeField] private float returnDuration = 0.5f;
    [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Debug")]
    [SerializeField, MMReadOnly] private int mmfPlayerCount;
    [SerializeField, MMReadOnly] private int floatControllerCount;
    [SerializeField, MMReadOnly] private int vfxControllerCount;
    [SerializeField, MMReadOnly] private int shaderControllerCount;

    private readonly List<MMFPlayerState> _mmfPlayers = new List<MMFPlayerState>();
    private readonly List<FloatControllerState> _floatControllers = new List<FloatControllerState>();
    private readonly List<VFXControllerState> _vfxControllers = new List<VFXControllerState>();
    private readonly List<ShaderControllerState> _shaderControllers = new List<ShaderControllerState>();

    private bool _initialized;
    private bool _lastAppliedState;
    private bool _stateDirty;
    private bool _hierarchyDirty;

    public bool ScriptsActive
    {
        get => scriptsActive;
        set => SetScriptsActive(value);
    }

    [Serializable]
    private class MMFPlayerState
    {
        public MMF_Player Component;
        public bool RestoreEnabled;
        public bool RestoreCanPlay;
    }

    [Serializable]
    private class FloatControllerState
    {
        public FloatController Component;
        public bool RestoreEnabled;
        public bool RestoreRevertSetting;
        public bool InitialValueCaptured;
        public float InitialValue;
        public float ReturnStartValue;
        public float ReturnElapsed;
        public bool Returning;
        public float FadeInStartValue;
        public float FadeInElapsed;
        public bool FadingIn;
    }

    [Serializable]
    private class VFXControllerState
    {
        public AudioAnalyzerVFXController Component;
        public bool RestoreEnabled;
    }

    [Serializable]
    private class ShaderControllerState
    {
        public AudioAnalyzerShaderController Component;
        public bool RestoreEnabled;
    }

    private void Awake()
    {
        RefreshTargets();
        ApplyState(scriptsActive, true);
    }

    private void Update()
    {
        if (_hierarchyDirty)
        {
            RefreshTargets();
            _hierarchyDirty = false;

            if (!scriptsActive)
            {
                CaptureAndDisableNewTargets();
            }
        }

        if (_stateDirty || scriptsActive != _lastAppliedState)
        {
            ApplyState(scriptsActive, false);
        }

        if (!scriptsActive)
        {
            UpdateFloatReturns();
            EnforceDisabledState();
        }
        else
        {
            UpdateFloatFadeIns();
        }
    }

    private void OnValidate()
    {
        returnDuration = Mathf.Max(0f, returnDuration);
        _stateDirty = true;
    }

    private void OnTransformChildrenChanged()
    {
        _hierarchyDirty = true;
    }

    [ContextMenu("Refresh Targets")]
    public void RefreshTargets()
    {
        // Explicitly include components on this GameObject as well as all descendants.
        MergeMMFPlayers(GetComponents<MMF_Player>());
        MergeFloatControllers(GetComponents<FloatController>());
        MergeVFXControllers(GetComponents<AudioAnalyzerVFXController>());
        MergeShaderControllers(GetComponents<AudioAnalyzerShaderController>());

        MergeMMFPlayers(GetComponentsInChildren<MMF_Player>(true));
        MergeFloatControllers(GetComponentsInChildren<FloatController>(true));
        MergeVFXControllers(GetComponentsInChildren<AudioAnalyzerVFXController>(true));
        MergeShaderControllers(GetComponentsInChildren<AudioAnalyzerShaderController>(true));

        RemoveMissingTargets();
        UpdateDebugCounts();
    }

    [ContextMenu("Recapture Initial Values")]
    public void RecaptureInitialValues()
    {
        foreach (FloatControllerState state in _floatControllers)
        {
            if (state.Component != null && TryGetControlledFloat(state.Component, out float value))
            {
                state.InitialValue = value;
                state.InitialValueCaptured = true;
            }
        }

        foreach (VFXControllerState state in _vfxControllers)
        {
            if (state.Component != null)
            {
                state.Component.RecaptureInitialValues();
            }
        }

        foreach (ShaderControllerState state in _shaderControllers)
        {
            if (state.Component != null)
            {
                state.Component.RecaptureInitialValues();
            }
        }
    }

    public void SetScriptsActive(bool value)
    {
        scriptsActive = value;
        if (!_initialized)
        {
            return;
        }

        ApplyState(value, false);
    }

    public void EnableScripts()
    {
        SetScriptsActive(true);
    }

    public void DisableScripts()
    {
        SetScriptsActive(false);
    }

    private void ApplyState(bool active, bool force)
    {
        if (!force && _initialized && active == _lastAppliedState)
        {
            _stateDirty = false;
            return;
        }

        RefreshTargets();

        if (!_initialized && active)
        {
            _lastAppliedState = true;
            _initialized = true;
            _stateDirty = false;
            return;
        }

        if (active)
        {
            RestoreTargets();
        }
        else
        {
            CaptureAndDisableTargets();
        }

        _lastAppliedState = active;
        _initialized = true;
        _stateDirty = false;
    }

    private void CaptureAndDisableTargets()
    {
        foreach (MMFPlayerState state in _mmfPlayers)
        {
            MMF_Player player = state.Component;
            if (player == null)
            {
                continue;
            }

            state.RestoreEnabled = player.enabled;
            state.RestoreCanPlay = player.CanPlay;
            player.CanPlay = false;
            if (player.IsPlaying)
            {
                player.StopFeedbacks();
            }
            player.enabled = false;
        }

        foreach (FloatControllerState state in _floatControllers)
        {
            FloatController controller = state.Component;
            if (controller == null)
            {
                continue;
            }

            state.RestoreEnabled = controller.enabled;
            state.RestoreRevertSetting = controller.RevertToInitialValueAfterEnd;
            CaptureFloatInitialValue(state);
            state.FadingIn = false;

            controller.RevertToInitialValueAfterEnd = false;
            controller.enabled = false;

            if (state.RestoreEnabled && state.InitialValueCaptured &&
                TryGetControlledFloat(controller, out state.ReturnStartValue))
            {
                state.ReturnElapsed = 0f;
                state.Returning = returnDuration > 0f && controller.gameObject.activeInHierarchy;
                if (!state.Returning)
                {
                    TrySetControlledFloat(controller, state.InitialValue);
                    CompleteFloatReturn(state);
                }
            }
            else
            {
                state.Returning = false;
                controller.RevertToInitialValueAfterEnd = state.RestoreRevertSetting;
            }
        }

        foreach (VFXControllerState state in _vfxControllers)
        {
            AudioAnalyzerVFXController controller = state.Component;
            if (controller == null)
            {
                continue;
            }

            state.RestoreEnabled = controller.enabled;
            if (state.RestoreEnabled)
            {
                controller.ReturnToInitialValuesAndDisable(returnDuration, returnCurve, useUnscaledTime);
            }
            else
            {
                controller.DisableImmediately(false);
            }
        }

        foreach (ShaderControllerState state in _shaderControllers)
        {
            AudioAnalyzerShaderController controller = state.Component;
            if (controller == null)
            {
                continue;
            }

            state.RestoreEnabled = controller.enabled;
            if (state.RestoreEnabled)
            {
                controller.ReturnToInitialValuesAndDisable(returnDuration, returnCurve, useUnscaledTime);
            }
            else
            {
                controller.DisableImmediately(false);
            }
        }
    }

    private void CaptureAndDisableNewTargets()
    {
        foreach (MMFPlayerState state in _mmfPlayers)
        {
            if (state.Component != null && (state.Component.enabled || state.Component.CanPlay))
            {
                state.RestoreEnabled = state.Component.enabled;
                state.RestoreCanPlay = state.Component.CanPlay;
                state.Component.CanPlay = false;
                if (state.Component.IsPlaying)
                {
                    state.Component.StopFeedbacks();
                }
                state.Component.enabled = false;
            }
        }

        foreach (FloatControllerState state in _floatControllers)
        {
            if (state.Component == null || !state.Component.enabled)
            {
                continue;
            }

            state.RestoreEnabled = true;
            state.RestoreRevertSetting = state.Component.RevertToInitialValueAfterEnd;
            CaptureFloatInitialValue(state);
            state.FadingIn = false;
            state.Component.RevertToInitialValueAfterEnd = false;
            state.Component.enabled = false;

            if (state.InitialValueCaptured && TryGetControlledFloat(state.Component, out state.ReturnStartValue))
            {
                state.ReturnElapsed = 0f;
                state.Returning = returnDuration > 0f && state.Component.gameObject.activeInHierarchy;
                if (!state.Returning)
                {
                    TrySetControlledFloat(state.Component, state.InitialValue);
                    CompleteFloatReturn(state);
                }
            }
        }

        foreach (VFXControllerState state in _vfxControllers)
        {
            if (state.Component != null && state.Component.enabled && !state.Component.IsReturningToInitialValues)
            {
                state.RestoreEnabled = true;
                state.Component.ReturnToInitialValuesAndDisable(returnDuration, returnCurve, useUnscaledTime);
            }
        }

        foreach (ShaderControllerState state in _shaderControllers)
        {
            if (state.Component != null && state.Component.enabled && !state.Component.IsReturningToInitialValues)
            {
                state.RestoreEnabled = true;
                state.Component.ReturnToInitialValuesAndDisable(returnDuration, returnCurve, useUnscaledTime);
            }
        }
    }

    private void RestoreTargets()
    {
        foreach (MMFPlayerState state in _mmfPlayers)
        {
            if (state.Component == null)
            {
                continue;
            }

            state.Component.CanPlay = state.RestoreCanPlay;
            state.Component.enabled = state.RestoreEnabled;
        }

        foreach (FloatControllerState state in _floatControllers)
        {
            FloatController controller = state.Component;
            if (controller == null)
            {
                continue;
            }

            state.Returning = false;
            controller.RevertToInitialValueAfterEnd = state.RestoreRevertSetting;

            state.FadingIn = state.RestoreEnabled && state.InitialValueCaptured &&
                             TryGetControlledFloat(controller, out state.FadeInStartValue) &&
                             returnDuration > 0f && controller.gameObject.activeInHierarchy;
            state.FadeInElapsed = 0f;
            controller.enabled = state.RestoreEnabled;
            if (state.InitialValueCaptured)
            {
                controller.InitialValue = state.InitialValue;
            }
        }

        foreach (VFXControllerState state in _vfxControllers)
        {
            if (state.Component == null)
            {
                continue;
            }

            if (state.RestoreEnabled)
            {
                state.Component.FadeInAudioControl(returnDuration, returnCurve, useUnscaledTime);
            }
            else
            {
                state.Component.DisableImmediately(false);
            }
        }

        foreach (ShaderControllerState state in _shaderControllers)
        {
            if (state.Component == null)
            {
                continue;
            }

            if (state.RestoreEnabled)
            {
                state.Component.FadeInAudioControl(returnDuration, returnCurve, useUnscaledTime);
            }
            else
            {
                state.Component.DisableImmediately(false);
            }
        }
    }

    private void UpdateFloatReturns()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        foreach (FloatControllerState state in _floatControllers)
        {
            if (!state.Returning || state.Component == null)
            {
                continue;
            }

            state.ReturnElapsed += deltaTime;
            float normalizedTime = returnDuration <= 0f ? 1f : Mathf.Clamp01(state.ReturnElapsed / returnDuration);
            float interpolation = returnCurve == null ? normalizedTime : returnCurve.Evaluate(normalizedTime);
            float value = Mathf.LerpUnclamped(state.ReturnStartValue, state.InitialValue, interpolation);
            TrySetControlledFloat(state.Component, value);

            if (normalizedTime >= 1f)
            {
                TrySetControlledFloat(state.Component, state.InitialValue);
                CompleteFloatReturn(state);
            }
        }
    }

    private static void CompleteFloatReturn(FloatControllerState state)
    {
        state.Returning = false;
        if (state.Component != null)
        {
            state.Component.RevertToInitialValueAfterEnd = state.RestoreRevertSetting;
        }
    }

    private void UpdateFloatFadeIns()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        foreach (FloatControllerState state in _floatControllers)
        {
            if (!state.FadingIn || state.Component == null)
            {
                continue;
            }

            if (!state.Component.enabled)
            {
                state.FadingIn = false;
                continue;
            }

            state.FadeInElapsed += deltaTime;
            float normalizedTime = returnDuration <= 0f ? 1f : Mathf.Clamp01(state.FadeInElapsed / returnDuration);
            float interpolation = returnCurve == null ? normalizedTime : returnCurve.Evaluate(normalizedTime);
            float value = Mathf.LerpUnclamped(state.FadeInStartValue, state.Component.CurrentValue, interpolation);
            TrySetControlledFloat(state.Component, value);

            if (normalizedTime >= 1f)
            {
                TrySetControlledFloat(state.Component, state.Component.CurrentValue);
                state.FadingIn = false;
            }
        }
    }

    private void EnforceDisabledState()
    {
        foreach (MMFPlayerState state in _mmfPlayers)
        {
            if (state.Component == null)
            {
                continue;
            }
            state.Component.CanPlay = false;
            state.Component.enabled = false;
        }

        foreach (FloatControllerState state in _floatControllers)
        {
            if (state.Component != null && state.Component.enabled)
            {
                state.Component.RevertToInitialValueAfterEnd = false;
                state.Component.enabled = false;
            }
        }

        foreach (VFXControllerState state in _vfxControllers)
        {
            if (state.Component != null && !state.Component.IsReturningToInitialValues)
            {
                state.Component.enabled = false;
            }
        }

        foreach (ShaderControllerState state in _shaderControllers)
        {
            if (state.Component != null && !state.Component.IsReturningToInitialValues)
            {
                state.Component.enabled = false;
            }
        }
    }

    private static bool TryGetControlledFloat(FloatController controller, out float value)
    {
        value = 0f;
        if (!TryGetFloatMember(controller, out object target, out FieldInfo field, out PropertyInfo property))
        {
            return false;
        }

        try
        {
            value = field != null ? (float)field.GetValue(target) : (float)property.GetValue(target, null);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{nameof(AudioAnalyzerGroupControl)} could not read '{controller.PropertyName}' on '{controller.name}': {exception.Message}", controller);
            return false;
        }
    }

    private static bool TrySetControlledFloat(FloatController controller, float value)
    {
        if (!TryGetFloatMember(controller, out object target, out FieldInfo field, out PropertyInfo property))
        {
            return false;
        }

        try
        {
            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                property.SetValue(target, value, null);
            }
            controller.CurrentValue = value;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{nameof(AudioAnalyzerGroupControl)} could not write '{controller.PropertyName}' on '{controller.name}': {exception.Message}", controller);
            return false;
        }
    }

    private static bool TryGetFloatMember(FloatController controller, out object target, out FieldInfo field, out PropertyInfo property)
    {
        target = controller == null ? null : controller.TargetObject;
        field = null;
        property = null;

        if (target == null || string.IsNullOrEmpty(controller.PropertyName) ||
            controller.PropertyName == FloatController._undefinedString)
        {
            return false;
        }

        Type targetType = target.GetType();
        property = targetType.GetProperty(controller.PropertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.PropertyType == typeof(float) && property.CanRead && property.CanWrite)
        {
            return true;
        }

        property = null;
        field = targetType.GetField(controller.PropertyName, BindingFlags.Instance | BindingFlags.Public);
        return field != null && field.FieldType == typeof(float) && !field.IsInitOnly;
    }

    private static void CaptureFloatInitialValue(FloatControllerState state)
    {
        if (!state.InitialValueCaptured && state.Component != null &&
            TryGetControlledFloat(state.Component, out float value))
        {
            state.InitialValue = value;
            state.InitialValueCaptured = true;
        }
    }

    private void MergeMMFPlayers(MMF_Player[] components)
    {
        foreach (MMF_Player component in components)
        {
            if (!_mmfPlayers.Exists(state => state.Component == component))
            {
                _mmfPlayers.Add(new MMFPlayerState { Component = component });
            }
        }
    }

    private void MergeFloatControllers(FloatController[] components)
    {
        foreach (FloatController component in components)
        {
            if (_floatControllers.Exists(state => state.Component == component))
            {
                continue;
            }

            FloatControllerState state = new FloatControllerState { Component = component };
            CaptureFloatInitialValue(state);
            _floatControllers.Add(state);
        }
    }

    private void MergeVFXControllers(AudioAnalyzerVFXController[] components)
    {
        foreach (AudioAnalyzerVFXController component in components)
        {
            if (!_vfxControllers.Exists(state => state.Component == component))
            {
                _vfxControllers.Add(new VFXControllerState { Component = component });
            }
        }
    }

    private void MergeShaderControllers(AudioAnalyzerShaderController[] components)
    {
        foreach (AudioAnalyzerShaderController component in components)
        {
            if (!_shaderControllers.Exists(state => state.Component == component))
            {
                _shaderControllers.Add(new ShaderControllerState { Component = component });
            }
        }
    }

    private void RemoveMissingTargets()
    {
        _mmfPlayers.RemoveAll(state => state.Component == null || !IsInControlledHierarchy(state.Component.transform));
        _floatControllers.RemoveAll(state => state.Component == null || !IsInControlledHierarchy(state.Component.transform));
        _vfxControllers.RemoveAll(state => state.Component == null || !IsInControlledHierarchy(state.Component.transform));
        _shaderControllers.RemoveAll(state => state.Component == null || !IsInControlledHierarchy(state.Component.transform));
    }

    private bool IsInControlledHierarchy(Transform target)
    {
        return target == transform || target.IsChildOf(transform);
    }

    private void UpdateDebugCounts()
    {
        mmfPlayerCount = _mmfPlayers.Count;
        floatControllerCount = _floatControllers.Count;
        vfxControllerCount = _vfxControllers.Count;
        shaderControllerCount = _shaderControllers.Count;
    }
}
