using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
#endif

namespace Runtime.CameraSystem
{
    public class CameraSystemMaster : MonoBehaviour
    {
        private const float TimedAlphaActiveOverrideMaximum = 0.9999f;

        //[Header("--- General Cameras ---")]
        [Tooltip("General 運鏡使用的 A Camera。")]
        public CinemachineCamera generalCamera;

        [Tooltip("General 運鏡使用的 B Camera。請複製 General A 的 Cinemachine 組件設定。")]
        public CinemachineCamera generalCameraB;

        //[Header("--- Tracking Camera ---")]
        [Tooltip("Tracking 運鏡使用的 Camera。")]
        public CinemachineCamera trackingCamera;

        //[Header("--- Dolly Camera ---")]
        [Tooltip("Dolly 運鏡使用的 Camera。")]
        public CinemachineCamera dollyCamera;

        //[Header("--- Priority Settings ---")]
        public int livePriority = 100;
        public int inactivePriority = 0;

        //[Header("--- Storyboard RT Cross Fade ---")]
        [Tooltip("Storyboard cross fade 用的離屏 Unity Camera。這台 Camera 需要掛 CinemachineBrain，並輸出到 RenderTexture。")]
        public Camera crossFadeRenderCamera;

        [Tooltip("crossFadeRenderCamera 上的 CinemachineBrain。建議 Channel Mask 設為 Channel01。")]
        public CinemachineBrain crossFadeRenderBrain;

        [Tooltip("可手動指定 RenderTexture。若留空，Play Mode 會依照畫面大小自動建立並釋放。")]
        public RenderTexture crossFadeRenderTexture;

        [Tooltip("離屏 cross fade vcams 使用的 Cinemachine Output Channel。Main Camera Brain 應排除此 channel。")]
        public OutputChannels crossFadeOutputChannel = OutputChannels.Channel01;

        [Tooltip("General Profile 在離屏 RenderTexture 中使用的 CinemachineCamera。")]
        public CinemachineCamera crossFadeGeneralCamera;

        [Tooltip("Tracking Profile 在離屏 RenderTexture 中使用的 CinemachineCamera。")]
        public CinemachineCamera crossFadeTrackingCamera;

        [Tooltip("Dolly Profile 在離屏 RenderTexture 中使用的 CinemachineCamera。")]
        public CinemachineCamera crossFadeDollyCamera;

        [Tooltip("主畫面 cross fade 專用的 muted CinemachineCamera。這台 Camera 只能承載 CinemachineStoryboard，不可與任何運鏡 Camera 共用。")]
        public CinemachineCamera crossFadeStoryboardCamera;

        [Tooltip("自動建立 RenderTexture 時使用目前 Game View 大小。")]
        public bool autoResizeCrossFadeRenderTexture = true;

        [Min(16)]
        public int fallbackCrossFadeTextureWidth = 1920;

        [Min(16)]
        public int fallbackCrossFadeTextureHeight = 1080;

        [Tooltip("Storyboard canvas sorting order。若 UI 需要蓋在轉場上，UI canvas sorting order 要更高。")]
        public int storyboardSortingOrder = 0;

        [SerializeField]
        [Tooltip("主輸出 Unity Camera 上的 per-camera blur state。可使用建立組件工具自動補齊。")]
        private CameraBlurState mainCameraBlurState;

        [SerializeField]
        [Tooltip("Cross Fade Render Camera 上的 per-camera blur state。可使用建立組件工具自動補齊。")]
        private CameraBlurState crossFadeRenderBlurState;

        private RenderTexture _ownedCrossFadeRenderTexture;
        private int _ownedCrossFadeRenderTextureWidth;
        private int _ownedCrossFadeRenderTextureHeight;
        private bool _hasLoggedCrossFadeChannelWarning;
        private bool _hasLoggedInvalidCrossFadeRig;
        private bool _hasLoggedInvalidCrossFadeBlurSetup;
        private CinemachineBrain _crossFadeOverrideBrain;
        private int _crossFadeCameraOverrideId = -1;
        private bool _isEditorCrossFadePreviewActive;
        private int _editorCrossFadePreviewFrame;

        public bool IsStoryboardCrossFadeActive =>
            _crossFadeCameraOverrideId > 0;

        public bool IsEditorCrossFadePreviewActive =>
            _isEditorCrossFadePreviewActive;

        private void Awake()
        {
            if (generalCamera == null && trackingCamera == null && dollyCamera == null)
            {
                Debug.LogError(
                    $"[{nameof(CameraSystemMaster)}] 沒有指定任何 Cinemachine Camera，Camera Profile 系統無法運作。",
                    this
                );
            }

            if (generalCamera != null && generalCameraB == null)
            {
                Debug.LogWarning(
                    $"[{nameof(CameraSystemMaster)}] General Camera B 尚未指定。General → General 連續 Clip 會退回單台 General Camera，可能仍會看到旋轉殘留。",
                    this
                );
            }
        }

        private void OnDisable()
        {
            ClearAllDirectionalCameraOffsets();
            ClearStoryboardCrossFade();
            ReleaseOwnedCrossFadeRenderTexture();
        }

        public CinemachineCamera GetGeneralCamera(bool useB)
        {
            if (useB && generalCameraB != null)
                return generalCameraB;

            return generalCamera;
        }

        public void SetOnlyThisCameraLive(CinemachineCamera liveCamera)
        {
            SetCameraPriority(generalCamera, liveCamera);
            SetCameraPriority(generalCameraB, liveCamera);
            SetCameraPriority(trackingCamera, liveCamera);
            SetCameraPriority(dollyCamera, liveCamera);
        }

        public void DisableAllCameras()
        {
            SetCameraPriority(generalCamera, null);
            SetCameraPriority(generalCameraB, null);
            SetCameraPriority(trackingCamera, null);
            SetCameraPriority(dollyCamera, null);
        }

        public CinemachineCamera GetCrossFadeGeneralCamera()
        {
            return crossFadeGeneralCamera;
        }

        public CinemachineCamera GetCrossFadeTrackingCamera()
        {
            return crossFadeTrackingCamera;
        }

        public CinemachineCamera GetCrossFadeDollyCamera()
        {
            return crossFadeDollyCamera;
        }

        public bool TrySetDirectionalCameraOffset(
            CinemachineCamera camera,
            Vector3 localOffset)
        {
            if (camera == null)
                return false;

            CinemachineCameraOffset cameraOffset =
                camera.GetComponent<CinemachineCameraOffset>();

            if (cameraOffset == null)
            {
#if UNITY_EDITOR
                cameraOffset = !Application.isPlaying
                    ? Undo.AddComponent<CinemachineCameraOffset>(camera.gameObject)
                    : camera.gameObject.AddComponent<CinemachineCameraOffset>();
#else
                cameraOffset = camera.gameObject.AddComponent<CinemachineCameraOffset>();
#endif
            }

            if (cameraOffset == null)
                return false;

            cameraOffset.enabled = true;
            cameraOffset.ApplyAfter = CinemachineCore.Stage.Aim;
            cameraOffset.PreserveComposition = false;
            cameraOffset.Offset = localOffset;
            return true;
        }

        public void ClearDirectionalCameraOffset(CinemachineCamera camera)
        {
            if (camera == null)
                return;

            CinemachineCameraOffset cameraOffset =
                camera.GetComponent<CinemachineCameraOffset>();

            if (cameraOffset != null)
            {
                cameraOffset.Offset = Vector3.zero;
            }
        }

        public void ClearAllDirectionalCameraOffsets()
        {
            ClearDirectionalCameraOffset(generalCamera);
            ClearDirectionalCameraOffset(generalCameraB);
            ClearDirectionalCameraOffset(trackingCamera);
            ClearDirectionalCameraOffset(dollyCamera);
            ClearDirectionalCameraOffset(crossFadeGeneralCamera);
            ClearDirectionalCameraOffset(crossFadeTrackingCamera);
            ClearDirectionalCameraOffset(crossFadeDollyCamera);
        }

        public void SetOnlyThisCrossFadeCameraLive(CinemachineCamera liveCamera)
        {
            SetCameraPriority(crossFadeGeneralCamera, liveCamera);
            SetCameraPriority(crossFadeTrackingCamera, liveCamera);
            SetCameraPriority(crossFadeDollyCamera, liveCamera);
        }

        /// <summary>
        /// 將同一個 blur intensity 套到主輸出與離屏輸出。兩張畫面
        /// 使用相同 kernel，經 Storyboard alpha 合成後才會像整體畫面一起模糊。
        /// Blur rig 缺失時只停用模糊，不影響既有 Cross Fade。
        /// </summary>
        public bool TrySetCrossFadeBlurIntensity(float intensity)
        {
            return TrySetCrossFadeBlurIntensity(
                intensity,
                intensity > CameraBlurPass.MinimumIntensity ? 1f : 0f
            );
        }

        /// <summary>
        /// intensity 控制 Kawase kernel 半徑，blendWeight 控制原圖與模糊圖
        /// 的最終合成，讓低強度時能從完全清楚連續過渡。
        /// </summary>
        public bool TrySetCrossFadeBlurIntensity(
            float intensity,
            float blendWeight)
        {
            intensity = Mathf.Max(0f, intensity);
            blendWeight = Mathf.Clamp01(blendWeight);

            ResolveCameraBlurStates();

            if (intensity <= CameraBlurPass.MinimumIntensity ||
                blendWeight <= CameraBlurPass.MinimumIntensity)
            {
                ClearCrossFadeBlur();
                return true;
            }

            if (mainCameraBlurState == null ||
                crossFadeRenderBlurState == null ||
                !mainCameraBlurState.isActiveAndEnabled ||
                !crossFadeRenderBlurState.isActiveAndEnabled)
            {
                ClearCrossFadeBlur();

                if (!_hasLoggedInvalidCrossFadeBlurSetup)
                {
                    Debug.LogWarning(
                        $"[{nameof(CameraSystemMaster)}] Cross Fade Blur 缺少啟用中的 CameraBlurState；本次轉場退化為一般 Cross Fade。請在 Edit Mode 執行建立組件。",
                        this
                    );
                    _hasLoggedInvalidCrossFadeBlurSetup = true;
                }

                return false;
            }

            mainCameraBlurState.SetBlur(intensity, blendWeight);
            crossFadeRenderBlurState.SetBlur(intensity, blendWeight);
            _hasLoggedInvalidCrossFadeBlurSetup = false;
            return true;
        }

        public void ClearCrossFadeBlur()
        {
            ResolveCameraBlurStates();
            mainCameraBlurState?.Clear();
            crossFadeRenderBlurState?.Clear();
        }

        private void ResolveCameraBlurStates()
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null &&
                (mainCameraBlurState == null ||
                 mainCameraBlurState.gameObject != mainCamera.gameObject))
            {
                mainCameraBlurState =
                    mainCamera.GetComponent<CameraBlurState>();
            }

            if (crossFadeRenderCamera != null &&
                (crossFadeRenderBlurState == null ||
                 crossFadeRenderBlurState.gameObject !=
                    crossFadeRenderCamera.gameObject))
            {
                crossFadeRenderBlurState =
                    crossFadeRenderCamera.GetComponent<CameraBlurState>();
            }
        }

        public bool TrySetStoryboardCrossFade(
            CinemachineCamera baseCamera,
            CinemachineCamera renderTextureCamera,
            float alpha,
            float deltaTime)
        {
            return TrySetStoryboardCrossFadeInternal(
                baseCamera,
                renderTextureCamera,
                alpha,
                alpha,
                false,
                deltaTime
            );
        }

        /// <summary>
        /// Cross Fade Blur 的 timed-alpha 路徑。rawAlpha 維持 Timeline overlap
        /// 的生命週期，displayAlpha 只決定 Storyboard RT 的實際顯示透明度。
        /// </summary>
        public bool TrySetStoryboardCrossFadeTimedAlpha(
            CinemachineCamera baseCamera,
            CinemachineCamera renderTextureCamera,
            float rawAlpha,
            float displayAlpha,
            float deltaTime)
        {
            return TrySetStoryboardCrossFadeInternal(
                baseCamera,
                renderTextureCamera,
                rawAlpha,
                displayAlpha,
                true,
                deltaTime
            );
        }

        private bool TrySetStoryboardCrossFadeInternal(
            CinemachineCamera baseCamera,
            CinemachineCamera renderTextureCamera,
            float rawAlpha,
            float displayAlpha,
            bool keepTimedOverrideInProgress,
            float deltaTime)
        {
            if (!TryGetCrossFadeRuntimeComponents(
                baseCamera,
                renderTextureCamera,
                out CinemachineBrain mainBrain,
                out CinemachineStoryboard storyboard))
            {
                return false;
            }

            RenderTexture texture = GetOrCreateCrossFadeRenderTexture();

            if (texture == null)
                return LogInvalidCrossFadeRig("無法建立或取得 RenderTexture。");

            ConfigureCrossFadeRenderRig();

            crossFadeRenderCamera.targetTexture = texture;
            crossFadeRenderCamera.enabled = true;
            crossFadeRenderBrain.enabled = true;

            SetOnlyThisCrossFadeCameraLive(renderTextureCamera);

            CalculateStoryboardAlphaControl(
                rawAlpha,
                displayAlpha,
                keepTimedOverrideInProgress,
                out float overrideAlpha,
                out float storyboardAlpha
            );

            ConfigureStoryboardOverlay(storyboard, texture, storyboardAlpha);
            SetStoryboardCameraOverride(
                mainBrain,
                baseCamera,
                overrideAlpha,
                deltaTime
            );

            _hasLoggedInvalidCrossFadeRig = false;

            WarnIfCrossFadeChannelCanDriveMainBrain();

            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 在非 Play Mode 中立即評估兩套 camera、更新離屏 RT，並刷新主 Brain 的
        /// Storyboard overlay。此流程不會執行 runtime 的 camera 角色交換。
        /// </summary>
        public bool TrySetStoryboardCrossFadePreview(
            CinemachineCamera baseCamera,
            CinemachineCamera renderTextureCamera,
            float alpha,
            float deltaTime)
        {
            return TrySetStoryboardCrossFadePreviewInternal(
                baseCamera,
                renderTextureCamera,
                alpha,
                alpha,
                false,
                deltaTime
            );
        }

        public bool TrySetStoryboardCrossFadePreviewTimedAlpha(
            CinemachineCamera baseCamera,
            CinemachineCamera renderTextureCamera,
            float rawAlpha,
            float displayAlpha,
            float deltaTime)
        {
            return TrySetStoryboardCrossFadePreviewInternal(
                baseCamera,
                renderTextureCamera,
                rawAlpha,
                displayAlpha,
                true,
                deltaTime
            );
        }

        private bool TrySetStoryboardCrossFadePreviewInternal(
            CinemachineCamera baseCamera,
            CinemachineCamera renderTextureCamera,
            float rawAlpha,
            float displayAlpha,
            bool keepTimedOverrideInProgress,
            float deltaTime)
        {
            if (Application.isPlaying)
            {
                return TrySetStoryboardCrossFadeInternal(
                    baseCamera,
                    renderTextureCamera,
                    rawAlpha,
                    displayAlpha,
                    keepTimedOverrideInProgress,
                    deltaTime
                );
            }

            if (!TryGetCrossFadeRuntimeComponents(
                baseCamera,
                renderTextureCamera,
                out CinemachineBrain mainBrain,
                out CinemachineStoryboard storyboard))
            {
                return false;
            }

            RenderTexture texture = GetOrCreateCrossFadeRenderTexture();

            if (texture == null)
                return LogInvalidCrossFadeRig("無法建立或取得 Editor preview RenderTexture。");

            ConfigureCrossFadeRenderRig();

            crossFadeRenderCamera.targetTexture = texture;
            crossFadeRenderCamera.enabled = true;
            crossFadeRenderBrain.enabled = true;

            SetOnlyThisCameraLive(baseCamera);
            SetOnlyThisCrossFadeCameraLive(renderTextureCamera);

            float previewDeltaTime = deltaTime > 0f ? deltaTime : -1f;

            if (previewDeltaTime < 0f)
            {
                baseCamera.PreviousStateIsValid = false;
                renderTextureCamera.PreviousStateIsValid = false;
            }

            ManualUpdateBrain(crossFadeRenderBrain, previewDeltaTime);
            RenderCrossFadePreview(texture);

            CalculateStoryboardAlphaControl(
                rawAlpha,
                displayAlpha,
                keepTimedOverrideInProgress,
                out float overrideAlpha,
                out float storyboardAlpha
            );

            ConfigureStoryboardOverlay(storyboard, texture, storyboardAlpha);
            SetStoryboardCameraOverride(
                mainBrain,
                baseCamera,
                overrideAlpha,
                previewDeltaTime
            );
            ManualUpdateBrain(mainBrain, previewDeltaTime);

            _isEditorCrossFadePreviewActive = true;
            _hasLoggedInvalidCrossFadeRig = false;
            WarnIfCrossFadeChannelCanDriveMainBrain();
            EditorApplication.QueuePlayerLoopUpdate();

            return true;
        }

        /// <summary>
        /// Editor Timeline 離開 crossfade 後，讓主 Brain 直接輸出指定 camera，
        /// 不沿用 overlap 期間的 root blend 或 damping history。
        /// </summary>
        public void RefreshEditorCameraPreview(CinemachineCamera activeCamera)
        {
            if (Application.isPlaying || activeCamera == null)
                return;

            SetOnlyThisCameraLive(activeCamera);
            activeCamera.PreviousStateIsValid = false;

            CinemachineBrain mainBrain = GetMainBrain();

            if (mainBrain != null && mainBrain != crossFadeRenderBrain)
            {
                mainBrain.ResetState();
                ManualUpdateBrain(mainBrain, -1f);
            }
            else
            {
                activeCamera.InternalUpdateCameraState(Vector3.up, -1f);
            }

            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void ManualUpdateBrain(CinemachineBrain brain, float deltaTime)
        {
            if (brain == null)
                return;

            CinemachineBrain.UpdateMethods previousUpdateMethod =
                brain.UpdateMethod;

            try
            {
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                brain.ManualUpdate(++_editorCrossFadePreviewFrame, deltaTime);
            }
            finally
            {
                brain.UpdateMethod = previousUpdateMethod;
            }
        }

        private void RenderCrossFadePreview(RenderTexture destination)
        {
            if (crossFadeRenderCamera == null || destination == null)
                return;

            if (RenderPipelineManager.currentPipeline != null)
            {
                RenderPipeline.StandardRequest request =
                    new RenderPipeline.StandardRequest
                    {
                        destination = destination,
                        mipLevel = 0,
                        slice = 0,
                        face = CubemapFace.Unknown
                    };

                RenderPipeline.SubmitRenderRequest(
                    crossFadeRenderCamera,
                    request
                );
            }
            else
            {
                crossFadeRenderCamera.Render();
            }
        }
#endif

        /// <summary>
        /// 只更新主 Brain 上的 Storyboard override，不重新啟動離屏 Camera/Brain。
        /// 用於 RT camera 已升格到主輸出後，讓最後一張 RT 暫時遮住角色交換。
        /// </summary>
        public bool TrySetStoryboardOverlayOverride(
            CinemachineCamera baseCamera,
            float alpha,
            float deltaTime)
        {
            CinemachineBrain mainBrain = GetMainBrain();
            CinemachineStoryboard storyboard = crossFadeStoryboardCamera != null
                ? crossFadeStoryboardCamera.GetComponent<CinemachineStoryboard>()
                : null;
            RenderTexture texture = GetActiveCrossFadeRenderTexture();

            if (baseCamera == null)
                return LogInvalidCrossFadeRig("找不到 Storyboard override 的 base CinemachineCamera。");

            if (mainBrain == null || mainBrain == crossFadeRenderBrain)
                return LogInvalidCrossFadeRig("找不到 MainCamera 上獨立的 CinemachineBrain。");

            if (crossFadeStoryboardCamera == null || storyboard == null)
                return LogInvalidCrossFadeRig("缺少專用 Cross Fade Storyboard Camera 或 CinemachineStoryboard。");

            if (texture == null)
                return LogInvalidCrossFadeRig("交接期間找不到已渲染的 RenderTexture。");

            if (crossFadeStoryboardCamera == baseCamera ||
                IsProfileCamera(crossFadeStoryboardCamera))
            {
                return LogInvalidCrossFadeRig("Cross Fade Storyboard Camera 必須是獨立物件，不可與任何運鏡 Camera 共用。");
            }

            ConfigureStoryboardOverlay(storyboard, texture);
            SetStoryboardCameraOverride(mainBrain, baseCamera, alpha, deltaTime);
            _hasLoggedInvalidCrossFadeRig = false;
            return true;
        }

        /// <summary>
        /// 將目前由離屏 Brain 驅動的 camera 直接升格成主輸出 camera。
        /// 交換的是 camera 角色，不複製 CameraState，因此 Composer/Dolly 的內部
        /// damping 歷史也會完整保留。
        /// </summary>
        public bool TryPromoteCrossFadeCamera(
            CinemachineCamera renderedCamera,
            CinemachineCamera handoffSlotCamera,
            out CinemachineCamera promotedCamera)
        {
            promotedCamera = null;

            if (renderedCamera == null || handoffSlotCamera == null)
                return LogInvalidCrossFadeRig("升格時找不到 transition camera 或主 camera slot。");

            CinemachineBrain mainBrain = GetMainBrain();

            if (mainBrain == null || mainBrain == crossFadeRenderBrain)
                return LogInvalidCrossFadeRig("升格前找不到 MainCamera 上獨立的 CinemachineBrain。");

            CinemachineCamera demotedCamera;

            if (renderedCamera == crossFadeGeneralCamera)
            {
                if (handoffSlotCamera == generalCamera)
                {
                    demotedCamera = generalCamera;
                    generalCamera = renderedCamera;
                    crossFadeGeneralCamera = demotedCamera;
                }
                else if (handoffSlotCamera == generalCameraB)
                {
                    demotedCamera = generalCameraB;
                    generalCameraB = renderedCamera;
                    crossFadeGeneralCamera = demotedCamera;
                }
                else
                {
                    return LogInvalidCrossFadeRig("General transition camera 與 handoff slot 類型不相符。");
                }
            }
            else if (renderedCamera == crossFadeTrackingCamera &&
                handoffSlotCamera == trackingCamera)
            {
                demotedCamera = trackingCamera;
                trackingCamera = renderedCamera;
                crossFadeTrackingCamera = demotedCamera;
            }
            else if (renderedCamera == crossFadeDollyCamera &&
                handoffSlotCamera == dollyCamera)
            {
                demotedCamera = dollyCamera;
                dollyCamera = renderedCamera;
                crossFadeDollyCamera = demotedCamera;
            }
            else
            {
                return LogInvalidCrossFadeRig("Transition camera 與 handoff slot 類型不相符，無法交換角色。");
            }

            // 先凍結最後一張 RT。接下來的幾幀只使用 Storyboard overlay，
            // 不可讓 offscreen Brain 再驅動已經升格的 camera。
            if (crossFadeRenderCamera != null)
                crossFadeRenderCamera.enabled = false;

            if (crossFadeRenderBrain != null)
                crossFadeRenderBrain.enabled = false;

            promotedCamera = renderedCamera;
            promotedCamera.OutputChannel = OutputChannels.Default;
            promotedCamera.enabled = true;

            ConfigureCrossFadeCamera(demotedCamera);
            SetOnlyThisCrossFadeCameraLive(null);
            SetOnlyThisCameraLive(promotedCamera);

            // 清掉 root camera 原本從 Clip1 產生的 blend；promotedCamera 自己的
            // pipeline 狀態不會被清掉，所以位置/旋轉 damping 仍延續 Clip2。
            mainBrain.ResetState();
            _hasLoggedInvalidCrossFadeRig = false;
            return true;
        }

        public void ClearStoryboardCrossFade()
        {
            ClearCrossFadeBlur();

            bool releaseEditorPreviewTexture =
                !Application.isPlaying && _isEditorCrossFadePreviewActive;

            if (_crossFadeOverrideBrain != null &&
                _crossFadeCameraOverrideId > 0)
            {
                _crossFadeOverrideBrain.ReleaseCameraOverride(
                    _crossFadeCameraOverrideId
                );
            }

            _crossFadeCameraOverrideId = -1;
            _crossFadeOverrideBrain = null;

            if (crossFadeStoryboardCamera != null)
            {
                CinemachineStoryboard storyboard =
                    crossFadeStoryboardCamera.GetComponent<CinemachineStoryboard>();

                if (storyboard != null)
                {
                    storyboard.Alpha = 1f;
                    storyboard.ShowImage = false;
                    storyboard.Image = null;
                }

                crossFadeStoryboardCamera.Priority.Value =
                    GetStoryboardInactivePriority();
            }

            SetOnlyThisCrossFadeCameraLive(null);

            if (crossFadeRenderCamera != null)
            {
                crossFadeRenderCamera.enabled = false;
            }

            if (crossFadeRenderBrain != null)
            {
                crossFadeRenderBrain.enabled = false;
            }

            _isEditorCrossFadePreviewActive = false;

            if (releaseEditorPreviewTexture)
            {
                ReleaseOwnedCrossFadeRenderTexture();
            }
        }

        public void ReportCrossFadeSetupFailure(string message)
        {
            LogInvalidCrossFadeRig(message);
        }

        private bool TryGetCrossFadeRuntimeComponents(
            CinemachineCamera baseCamera,
            CinemachineCamera renderTextureCamera,
            out CinemachineBrain mainBrain,
            out CinemachineStoryboard storyboard)
        {
            mainBrain = GetMainBrain();
            storyboard = crossFadeStoryboardCamera != null
                ? crossFadeStoryboardCamera.GetComponent<CinemachineStoryboard>()
                : null;

            if (baseCamera == null)
                return LogInvalidCrossFadeRig("找不到 outgoing/base CinemachineCamera。");

            if (renderTextureCamera == null)
                return LogInvalidCrossFadeRig("找不到 incoming RenderTexture CinemachineCamera。");

            if (crossFadeRenderCamera == null || crossFadeRenderBrain == null)
                return LogInvalidCrossFadeRig("Cross Fade Render Camera 或 Render Brain 尚未設定。");

            if (crossFadeStoryboardCamera == null || storyboard == null)
                return LogInvalidCrossFadeRig("缺少專用 Cross Fade Storyboard Camera 或 CinemachineStoryboard。請在 Edit Mode 執行自動設置。");

            if (crossFadeStoryboardCamera == baseCamera ||
                crossFadeStoryboardCamera == renderTextureCamera ||
                IsProfileCamera(crossFadeStoryboardCamera))
            {
                return LogInvalidCrossFadeRig("Cross Fade Storyboard Camera 必須是獨立物件，不可與任何運鏡 Camera 共用。");
            }

            if (mainBrain == null || mainBrain == crossFadeRenderBrain)
                return LogInvalidCrossFadeRig("找不到 MainCamera 上獨立的 CinemachineBrain。");

            return true;
        }

        private bool LogInvalidCrossFadeRig(string message)
        {
            if (!_hasLoggedInvalidCrossFadeRig)
            {
                Debug.LogError(
                    $"[{nameof(CameraSystemMaster)}] Storyboard RT Cross Fade 無法啟動：{message} Crossfade 將在 clip 邊界使用 hard cut，不會退回參數混合。",
                    this
                );
                _hasLoggedInvalidCrossFadeRig = true;
            }

            return false;
        }

        private bool IsProfileCamera(CinemachineCamera camera)
        {
            return camera == generalCamera ||
                camera == generalCameraB ||
                camera == trackingCamera ||
                camera == dollyCamera ||
                camera == crossFadeGeneralCamera ||
                camera == crossFadeTrackingCamera ||
                camera == crossFadeDollyCamera;
        }

        private int GetStoryboardInactivePriority()
        {
            return inactivePriority > int.MinValue
                ? inactivePriority - 1
                : int.MinValue;
        }

        private CinemachineBrain GetMainBrain()
        {
            return Camera.main != null
                ? Camera.main.GetComponent<CinemachineBrain>()
                : null;
        }

        private void SetCameraPriority(CinemachineCamera camera, CinemachineCamera liveCamera)
        {
            if (camera == null)
                return;

            camera.Priority.Value = camera == liveCamera
                ? livePriority
                : inactivePriority;
        }

        private void ConfigureCrossFadeRenderRig()
        {
            if (crossFadeRenderBrain != null)
            {
                crossFadeRenderBrain.ChannelMask = crossFadeOutputChannel;
                crossFadeRenderBrain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
            }

            ConfigureCrossFadeCamera(crossFadeGeneralCamera);
            ConfigureCrossFadeCamera(crossFadeTrackingCamera);
            ConfigureCrossFadeCamera(crossFadeDollyCamera);

            if (crossFadeRenderCamera != null && Camera.main != null)
            {
                crossFadeRenderCamera.depth = Camera.main.depth - 1f;
            }
        }

        private void ConfigureCrossFadeCamera(CinemachineCamera camera)
        {
            if (camera == null)
                return;

            camera.OutputChannel = crossFadeOutputChannel;
            camera.Priority.Value = inactivePriority;
        }

        private static void CalculateStoryboardAlphaControl(
            float rawAlpha,
            float displayAlpha,
            bool keepTimedOverrideInProgress,
            out float overrideAlpha,
            out float storyboardAlpha)
        {
            float raw = Mathf.Clamp01(rawAlpha);
            float display = Mathf.Clamp01(displayAlpha);

            if (!keepTimedOverrideInProgress)
            {
                overrideAlpha = display;
                storyboardAlpha = 1f;
                return;
            }

            // CinemachineStoryboard 的最終透明度是 Storyboard.Alpha 與
            // override blend weight 的乘積。carrier 至少涵蓋 raw/display，
            // 讓 Storyboard.Alpha 可維持在 0..1；overlap 尚未真正結束前
            // 不允許 carrier 精確到 1，避免 Cinemachine 提前完成 override。
            overrideAlpha = Mathf.Max(raw, display);

            if (raw < 1f)
            {
                overrideAlpha = Mathf.Min(
                    overrideAlpha,
                    TimedAlphaActiveOverrideMaximum
                );
            }

            storyboardAlpha = overrideAlpha > Mathf.Epsilon
                ? Mathf.Clamp01(display / overrideAlpha)
                : 0f;
        }

        private void ConfigureStoryboardOverlay(
            CinemachineStoryboard storyboard,
            RenderTexture texture,
            float alpha = 1f)
        {
            crossFadeStoryboardCamera.OutputChannel = OutputChannels.Default;
            crossFadeStoryboardCamera.Priority.Value = GetStoryboardInactivePriority();
            crossFadeStoryboardCamera.enabled = true;

            storyboard.enabled = true;
            storyboard.ShowImage = true;
            storyboard.Image = texture;
            storyboard.Alpha = Mathf.Clamp01(alpha);
            storyboard.Aspect = CinemachineStoryboard.FillStrategy.CropImageToFit;
            storyboard.Center = Vector2.zero;
            storyboard.Rotation = Vector3.zero;
            storyboard.Scale = Vector2.one;
            storyboard.SyncScale = true;
            storyboard.MuteCamera = true;
            storyboard.SplitView = 0f;
            storyboard.RenderMode = CinemachineStoryboard.StoryboardRenderMode.ScreenSpaceOverlay;
            storyboard.SortingOrder = storyboardSortingOrder;
        }

        private void SetStoryboardCameraOverride(
            CinemachineBrain mainBrain,
            CinemachineCamera baseCamera,
            float alpha,
            float deltaTime)
        {
            if (_crossFadeOverrideBrain != null &&
                _crossFadeOverrideBrain != mainBrain &&
                _crossFadeCameraOverrideId > 0)
            {
                _crossFadeOverrideBrain.ReleaseCameraOverride(
                    _crossFadeCameraOverrideId
                );
                _crossFadeCameraOverrideId = -1;
            }

            _crossFadeOverrideBrain = mainBrain;
            _crossFadeCameraOverrideId = mainBrain.SetCameraOverride(
                _crossFadeCameraOverrideId,
                int.MaxValue,
                baseCamera,
                crossFadeStoryboardCamera,
                Mathf.Clamp01(alpha),
                deltaTime > 0f ? deltaTime : -1f
            );
        }

        private RenderTexture GetActiveCrossFadeRenderTexture()
        {
            if (crossFadeRenderCamera != null &&
                crossFadeRenderCamera.targetTexture != null)
            {
                return crossFadeRenderCamera.targetTexture;
            }

            if (crossFadeRenderTexture != null)
                return crossFadeRenderTexture;

            return _ownedCrossFadeRenderTexture;
        }

        private RenderTexture GetOrCreateCrossFadeRenderTexture()
        {
            if (crossFadeRenderTexture != null)
                return crossFadeRenderTexture;

            int width = fallbackCrossFadeTextureWidth;
            int height = fallbackCrossFadeTextureHeight;

            if (autoResizeCrossFadeRenderTexture)
            {
                width = Mathf.Max(16, Screen.width);
                height = Mathf.Max(16, Screen.height);
            }

            if (_ownedCrossFadeRenderTexture != null &&
                _ownedCrossFadeRenderTextureWidth == width &&
                _ownedCrossFadeRenderTextureHeight == height)
            {
                return _ownedCrossFadeRenderTexture;
            }

            ReleaseOwnedCrossFadeRenderTexture();

            _ownedCrossFadeRenderTexture = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.Default
            )
            {
                name = "CameraProfileCrossFadeRT"
            };

            _ownedCrossFadeRenderTexture.Create();
            _ownedCrossFadeRenderTextureWidth = width;
            _ownedCrossFadeRenderTextureHeight = height;

            return _ownedCrossFadeRenderTexture;
        }

        private void ReleaseOwnedCrossFadeRenderTexture()
        {
            if (_ownedCrossFadeRenderTexture == null)
                return;

            if (crossFadeRenderCamera != null &&
                crossFadeRenderCamera.targetTexture == _ownedCrossFadeRenderTexture)
            {
                crossFadeRenderCamera.targetTexture = null;
            }

            _ownedCrossFadeRenderTexture.Release();

            if (Application.isPlaying)
            {
                Destroy(_ownedCrossFadeRenderTexture);
            }
            else
            {
                DestroyImmediate(_ownedCrossFadeRenderTexture);
            }

            _ownedCrossFadeRenderTexture = null;
            _ownedCrossFadeRenderTextureWidth = 0;
            _ownedCrossFadeRenderTextureHeight = 0;
        }

        private void WarnIfCrossFadeChannelCanDriveMainBrain()
        {
            if (_hasLoggedCrossFadeChannelWarning || crossFadeRenderBrain == null)
                return;

            CinemachineBrain[] brains = UnityEngine.Object.FindObjectsByType<CinemachineBrain>(
                FindObjectsSortMode.None
            );

            foreach (CinemachineBrain brain in brains)
            {
                if (brain == null || brain == crossFadeRenderBrain)
                    continue;

                if ((brain.ChannelMask & crossFadeOutputChannel) == 0)
                    continue;

                Debug.LogWarning(
                    $"[{nameof(CameraSystemMaster)}] Storyboard RT Cross Fade 使用 {crossFadeOutputChannel}，但 {brain.name} 的 Channel Mask 也包含這個 channel。請將主 Camera Brain 的 Channel Mask 改成只吃 Default，避免離屏 transition camera 被主畫面選中。",
                    brain
                );
                _hasLoggedCrossFadeChannelWarning = true;
                return;
            }
        }

#if UNITY_EDITOR
        private enum CameraRigKind
        {
            General,
            Tracking,
            Dolly
        }

        private enum DebugSeverity
        {
            Warning,
            Error
        }

        private readonly struct CameraSlot
        {
            public readonly string Label;
            public readonly string FieldName;
            public readonly CameraRigKind Kind;
            public readonly bool Required;
            public readonly CinemachineCamera Camera;

            public CameraSlot(
                string label,
                string fieldName,
                CameraRigKind kind,
                bool required,
                CinemachineCamera camera)
            {
                Label = label;
                FieldName = fieldName;
                Kind = kind;
                Required = required;
                Camera = camera;
            }
        }

        private readonly struct DebugIssue
        {
            public readonly DebugSeverity Severity;
            public readonly string Message;
            public readonly Object Context;

            public DebugIssue(DebugSeverity severity, string message, Object context)
            {
                Severity = severity;
                Message = message;
                Context = context;
            }
        }

        public void DebugValidateCameraSetup()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning(
                    $"[{nameof(CameraSystemMaster)}] Camera setup 檢查請在 Edit Mode 執行，避免 runtime transition 狀態導致誤判。",
                    this
                );
                return;
            }

            List<DebugIssue> issues = CollectCameraSetupIssues();

            if (issues.Count == 0)
            {
                Debug.Log(
                    $"[{nameof(CameraSystemMaster)}] Camera setup 檢查完成：主 Camera、Storyboard RT Cross Fade 與 Camera Blur rig 設定正常。",
                    this
                );
                return;
            }

            foreach (DebugIssue issue in issues)
            {
                switch (issue.Severity)
                {
                    case DebugSeverity.Error:
                        Debug.LogError(
                            $"[{nameof(CameraSystemMaster)}] {issue.Message}",
                            issue.Context != null ? issue.Context : this
                        );
                        break;

                    case DebugSeverity.Warning:
                        Debug.LogWarning(
                            $"[{nameof(CameraSystemMaster)}] {issue.Message}",
                            issue.Context != null ? issue.Context : this
                        );
                        break;

                    default:
                        Debug.Log(
                            $"[{nameof(CameraSystemMaster)}] {issue.Message}",
                            issue.Context != null ? issue.Context : this
                        );
                        break;
                }
            }
        }

        public void DebugAutoFixCameraSetup()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning(
                    $"[{nameof(CameraSystemMaster)}] Camera setup 自動修復只能在 Edit Mode 執行。",
                    this
                );
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Auto Fix Camera System Master Setup");

            Undo.RecordObject(this, "Auto Fix Camera System Master Setup");

            if (!IsValidCrossFadeOutputChannel(crossFadeOutputChannel))
            {
                crossFadeOutputChannel = OutputChannels.Channel01;
            }

            GameObject mainOutputObject = ResolveMainOutputObject(
                out bool mainCameraAmbiguous
            );
            Camera mainOutputCamera = null;

            if (!mainCameraAmbiguous)
            {
                mainOutputCamera = EnsureMainOutputCamera(mainOutputObject);
            }
            else
            {
                Debug.LogError(
                    $"[{nameof(CameraSystemMaster)}] 場景中有多個 MainCamera 或多個可能的主 CinemachineBrain。" +
                    "已略過主攝影機建立與修復；請先保留唯一候選後再執行一次。",
                    this
                );
            }

            if (generalCamera == null)
            {
                generalCamera = CreateCameraRig("CinemachineCamera_General_A");
            }

            if (generalCameraB == null)
            {
                generalCameraB = CreateCameraRig("CinemachineCamera_General_B");
            }

            if (trackingCamera == null)
            {
                trackingCamera = CreateCameraRig("CinemachineCamera_Tracking");
            }

            if (dollyCamera == null)
            {
                dollyCamera = CreateCameraRig("CinemachineCamera_Dolly");
            }

            EditorUtility.SetDirty(this);

            FixGeneralCamera(generalCamera, null);
            FixGeneralCamera(generalCameraB, generalCamera);
            FixTrackingCamera(trackingCamera);
            FixDollyCamera(dollyCamera);
            FixPrimaryVirtualCameraChannel(generalCamera);
            FixPrimaryVirtualCameraChannel(generalCameraB);
            FixPrimaryVirtualCameraChannel(trackingCamera);
            FixPrimaryVirtualCameraChannel(dollyCamera);

            EnsureCrossFadeRenderRig(mainOutputCamera);
            EnsureCrossFadeVirtualCameras();
            EnsureCrossFadeStoryboardCamera();

            CinemachineBrain mainBrain = mainOutputCamera != null
                ? mainOutputCamera.GetComponent<CinemachineBrain>()
                : null;

            if (mainBrain != null)
            {
                FixMainBrainChannelMask(mainBrain);
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }

            Debug.Log(
                $"[{nameof(CameraSystemMaster)}] 已自動建立/補上主 Camera、Storyboard RT Cross Fade 與 Camera Blur rig。請重新按一次檢查確認細節。",
                this
            );
        }

        private List<DebugIssue> CollectCameraSetupIssues()
        {
            List<DebugIssue> issues = new List<DebugIssue>();

            if (livePriority <= inactivePriority)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"Priority 設定錯誤：livePriority ({livePriority}) 必須大於 inactivePriority ({inactivePriority})。",
                    this
                ));
            }

            foreach (CameraSlot slot in GetCameraSlots())
            {
                ValidateCameraSlot(slot, issues);
            }

            ValidateGeneralCameraPair(issues);
            ValidateDistinctCameraAssignments(issues);
            ValidatePrimaryVirtualCameraChannels(issues);
            ValidateCrossFadeSetup(issues);

            return issues;
        }

        internal void DebugGetCameraSetupIssueCounts(
            out int errorCount,
            out int warningCount)
        {
            errorCount = 0;
            warningCount = 0;

            foreach (DebugIssue issue in CollectCameraSetupIssues())
            {
                if (issue.Severity == DebugSeverity.Error)
                    errorCount++;
                else if (issue.Severity == DebugSeverity.Warning)
                    warningCount++;
            }
        }

        private CameraSlot[] GetCameraSlots()
        {
            return new[]
            {
                new CameraSlot("General Camera A", nameof(generalCamera), CameraRigKind.General, true, generalCamera),
                new CameraSlot("General Camera B", nameof(generalCameraB), CameraRigKind.General, false, generalCameraB),
                new CameraSlot("Tracking Camera", nameof(trackingCamera), CameraRigKind.Tracking, true, trackingCamera),
                new CameraSlot("Dolly Camera", nameof(dollyCamera), CameraRigKind.Dolly, true, dollyCamera),
                new CameraSlot("Cross Fade General Camera", nameof(crossFadeGeneralCamera), CameraRigKind.General, true, crossFadeGeneralCamera),
                new CameraSlot("Cross Fade Tracking Camera", nameof(crossFadeTrackingCamera), CameraRigKind.Tracking, true, crossFadeTrackingCamera),
                new CameraSlot("Cross Fade Dolly Camera", nameof(crossFadeDollyCamera), CameraRigKind.Dolly, true, crossFadeDollyCamera)
            };
        }

        private void ValidateCameraSlot(CameraSlot slot, List<DebugIssue> issues)
        {
            if (slot.Camera == null)
            {
                issues.Add(new DebugIssue(
                    slot.Required ? DebugSeverity.Error : DebugSeverity.Warning,
                    $"{slot.Label} ({slot.FieldName}) 尚未綁定 CinemachineCamera。",
                    this
                ));
                return;
            }

            if (!slot.Camera.gameObject.activeInHierarchy)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    $"{slot.Label} 綁定的物件未啟用，Timeline 可能無法切到這台 camera。",
                    slot.Camera
                ));
            }

            if (!slot.Camera.enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"{slot.Label} 的 CinemachineCamera component 被停用。",
                    slot.Camera
                ));
            }

            if (!slot.Camera.Priority.Enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    $"{slot.Label} 的 Priority 尚未啟用。CameraSystemMaster 會用 priority 切換 live camera。",
                    slot.Camera
                ));
            }

            ValidateRequiredComponent<CinemachineRotationComposer>(
                slot,
                issues,
                "Rotation Composer"
            );

            if (slot.Kind != CameraRigKind.Dolly)
            {
                ValidateRequiredComponent<CinemachineCameraOffset>(
                    slot,
                    issues,
                    "Directional Camera Offset"
                );
            }

            switch (slot.Kind)
            {
                case CameraRigKind.General:
                    ValidateRequiredComponent<CinemachinePositionComposer>(
                        slot,
                        issues,
                        "Position Composer"
                    );
                    ValidateConflictingBody<CinemachinePositionComposer>(
                        slot,
                        issues
                    );
                    break;

                case CameraRigKind.Tracking:
                    ValidateRequiredComponent<CinemachineFollow>(
                        slot,
                        issues,
                        "Cinemachine Follow"
                    );
                    ValidateConflictingBody<CinemachineFollow>(
                        slot,
                        issues
                    );
                    break;

                case CameraRigKind.Dolly:
                    ValidateRequiredComponent<CinemachineSplineDolly>(
                        slot,
                        issues,
                        "Spline Dolly"
                    );

                    ValidateConflictingBody<CinemachineSplineDolly>(
                        slot,
                        issues
                    );
                    break;
            }
        }

        private T ValidateRequiredComponent<T>(
            CameraSlot slot,
            List<DebugIssue> issues,
            string displayName) where T : Behaviour
        {
            T component = slot.Camera.GetComponent<T>();

            if (component == null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"{slot.Label} 缺少必要組件：{displayName}。",
                    slot.Camera
                ));
                return null;
            }

            if (!component.enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"{slot.Label} 的必要組件 {displayName} 被停用。",
                    component
                ));
            }

            return component;
        }

        private void ValidateConflictingBody<TExpected>(
            CameraSlot slot,
            List<DebugIssue> issues) where TExpected : CinemachineComponentBase
        {
            CinemachineComponentBase[] components =
                slot.Camera.GetComponents<CinemachineComponentBase>();

            foreach (CinemachineComponentBase component in components)
            {
                if (component == null ||
                    component.Stage != CinemachineCore.Stage.Body ||
                    component is TExpected ||
                    !component.enabled)
                {
                    continue;
                }

                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    $"{slot.Label} 有額外啟用的 Body 組件：{component.GetType().Name}。同一台 CinemachineCamera 同時啟用多個 Body 組件時，只會有一個被 pipeline 採用，可能不是預期設定。",
                    component
                ));
            }
        }

        private void ValidateGeneralCameraPair(List<DebugIssue> issues)
        {
            if (generalCamera == null || generalCameraB == null)
                return;

            if (generalCamera == generalCameraB)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "General Camera A 與 B 指到同一台 CinemachineCamera。General -> General 連續 clip 需要兩台不同 camera。",
                    generalCamera
                ));
                return;
            }

        }

        private void ValidateDistinctCameraAssignments(List<DebugIssue> issues)
        {
            CameraSlot[] slots = GetCameraSlots();

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Camera == null)
                    continue;

                for (int j = i + 1; j < slots.Length; j++)
                {
                    if (slots[j].Camera == null ||
                        slots[i].Camera != slots[j].Camera)
                    {
                        continue;
                    }

                    issues.Add(new DebugIssue(
                        DebugSeverity.Error,
                        $"{slots[i].Label} 與 {slots[j].Label} 指到同一台 CinemachineCamera。每個 camera slot 都必須使用不同物件。",
                        slots[i].Camera
                    ));
                }
            }

            if (crossFadeStoryboardCamera == null)
                return;

            foreach (CameraSlot slot in slots)
            {
                if (slot.Camera == null ||
                    slot.Camera != crossFadeStoryboardCamera)
                {
                    continue;
                }

                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"Cross Fade Storyboard Camera 與 {slot.Label} 指到同一台 CinemachineCamera。Storyboard 必須使用獨立物件。",
                    crossFadeStoryboardCamera
                ));
            }
        }

        private void ValidatePrimaryVirtualCameraChannels(List<DebugIssue> issues)
        {
            ValidatePrimaryVirtualCameraChannel(
                "General Camera A",
                generalCamera,
                issues
            );
            ValidatePrimaryVirtualCameraChannel(
                "General Camera B",
                generalCameraB,
                issues
            );
            ValidatePrimaryVirtualCameraChannel(
                "Tracking Camera",
                trackingCamera,
                issues
            );
            ValidatePrimaryVirtualCameraChannel(
                "Dolly Camera",
                dollyCamera,
                issues
            );
        }

        private static void ValidatePrimaryVirtualCameraChannel(
            string label,
            CinemachineCamera camera,
            List<DebugIssue> issues)
        {
            if (camera == null || camera.OutputChannel == OutputChannels.Default)
                return;

            issues.Add(new DebugIssue(
                DebugSeverity.Error,
                $"{label} 的 Output Channel 必須為 Default，才能由 MainCamera Brain 驅動。",
                camera
            ));
        }

        private void ValidateCrossFadeSetup(List<DebugIssue> issues)
        {
            bool validChannel = IsValidCrossFadeOutputChannel(crossFadeOutputChannel);

            if (!validChannel)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"Cross Fade Output Channel ({crossFadeOutputChannel}) 必須是單一、非 Default 的 Cinemachine channel。",
                    this
                ));
            }

            if (crossFadeRenderCamera == null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"Cross Fade Render Camera ({nameof(crossFadeRenderCamera)}) 尚未綁定 Unity Camera。",
                    this
                ));
            }

            if (crossFadeRenderBrain == null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"Cross Fade Render Brain ({nameof(crossFadeRenderBrain)}) 尚未綁定 CinemachineBrain。",
                    this
                ));
            }

            if (crossFadeStoryboardCamera == null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"Cross Fade Storyboard Camera ({nameof(crossFadeStoryboardCamera)}) 尚未綁定專用 CinemachineCamera。",
                    this
                ));
            }

            if (crossFadeRenderCamera != null)
            {
                ValidateCrossFadeRenderCamera(issues);
            }

            if (crossFadeRenderBrain != null)
            {
                ValidateCrossFadeRenderBrain(validChannel, issues);
            }

            if (crossFadeStoryboardCamera != null)
            {
                ValidateCrossFadeStoryboardCamera(issues);
            }

            if (crossFadeRenderCamera != null &&
                crossFadeRenderBrain != null &&
                crossFadeRenderCamera.gameObject != crossFadeRenderBrain.gameObject)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "Cross Fade Render Camera 與 CinemachineBrain 必須在同一個 GameObject 上。",
                    crossFadeRenderCamera
                ));
            }

            ValidateCrossFadeVirtualCameraChannel(
                "Cross Fade General Camera",
                crossFadeGeneralCamera,
                validChannel,
                issues
            );
            ValidateCrossFadeVirtualCameraChannel(
                "Cross Fade Tracking Camera",
                crossFadeTrackingCamera,
                validChannel,
                issues
            );
            ValidateCrossFadeVirtualCameraChannel(
                "Cross Fade Dolly Camera",
                crossFadeDollyCamera,
                validChannel,
                issues
            );

            GameObject mainOutputObject = ResolveMainOutputObject(out bool ambiguous);

            if (ambiguous)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "場景中有多個 MainCamera 或多個可能的主 CinemachineBrain，無法安全確定 crossfade 主輸出 camera。",
                    this
                ));
                return;
            }

            if (mainOutputObject == null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "找不到可用的 MainCamera。可按「建立與補上組件」自動建立主輸出 Camera。",
                    this
                ));
                return;
            }

            if (!mainOutputObject.CompareTag("MainCamera"))
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"主輸出 Camera ({mainOutputObject.name}) 未設定 MainCamera tag，runtime 的 Camera.main 無法取得它。",
                    mainOutputObject
                ));
            }

            if (!mainOutputObject.activeInHierarchy)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"MainCamera ({mainOutputObject.name}) 的 GameObject 未啟用。",
                    mainOutputObject
                ));
            }

            Camera mainOutputCamera = mainOutputObject.GetComponent<Camera>();

            if (mainOutputCamera == null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"MainCamera ({mainOutputObject.name}) 缺少 Camera component。",
                    mainOutputObject
                ));
                return;
            }

            if (!mainOutputCamera.enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"MainCamera ({mainOutputCamera.name}) 的 Camera component 被停用。",
                    mainOutputCamera
                ));
            }

            if (mainOutputCamera.targetTexture != null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"MainCamera ({mainOutputCamera.name}) 的 Target Texture 必須為空，才能輸出到主畫面。",
                    mainOutputCamera
                ));
            }

            if (GetAdditionalCameraData(mainOutputCamera) == null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"MainCamera ({mainOutputCamera.name}) 缺少 URP Universal Additional Camera Data。",
                    mainOutputCamera
                ));
            }

            CinemachineBrain mainBrain =
                mainOutputCamera.GetComponent<CinemachineBrain>();

            if (mainBrain == null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"MainCamera ({mainOutputCamera.name}) 缺少 CinemachineBrain。",
                    mainOutputCamera
                ));
                return;
            }

            if (!mainBrain.enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"MainCamera ({mainOutputCamera.name}) 的 CinemachineBrain 被停用。",
                    mainBrain
                ));
            }

            if (crossFadeRenderCamera == mainOutputCamera ||
                crossFadeRenderBrain == mainBrain)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "Cross Fade Render Camera/Brain 不能與 MainCamera 共用同一組件。",
                    mainOutputCamera
                ));
            }

            if ((mainBrain.ChannelMask & OutputChannels.Default) == 0)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"MainCamera Brain ({mainBrain.name}) 的 Channel Mask 未包含 Default。",
                    mainBrain
                ));
            }

            if (validChannel &&
                (mainBrain.ChannelMask & crossFadeOutputChannel) != 0)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"MainCamera Brain ({mainBrain.name}) 的 Channel Mask 仍包含 crossfade channel {crossFadeOutputChannel}。",
                    mainBrain
                ));
            }

            if (crossFadeRenderCamera != null &&
                !HasMatchingStableRenderCameraSettings(
                    mainOutputCamera,
                    crossFadeRenderCamera))
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "Cross Fade Render Camera 的主要渲染設定與 MainCamera 不一致。建議按自動修復重新同步。",
                    crossFadeRenderCamera
                ));
            }

            Component mainAdditionalData = GetAdditionalCameraData(mainOutputCamera);
            Component crossFadeAdditionalData =
                GetAdditionalCameraData(crossFadeRenderCamera);

            if (mainAdditionalData != null && crossFadeAdditionalData == null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "Cross Fade Render Camera 缺少與 MainCamera 相同的 URP Additional Camera Data。",
                    crossFadeRenderCamera
                ));
            }

            ValidateCrossFadeBlurSetup(mainOutputCamera, issues);

            if (!validChannel)
                return;

            CinemachineBrain[] brains = Object.FindObjectsByType<CinemachineBrain>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (CinemachineBrain brain in brains)
            {
                if (brain == null ||
                    brain == crossFadeRenderBrain ||
                    brain == mainBrain ||
                    (brain.ChannelMask & crossFadeOutputChannel) == 0)
                {
                    continue;
                }

                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    $"CinemachineBrain ({brain.name}) 也包含 crossfade channel {crossFadeOutputChannel}，可能會誤選 transition camera。",
                    brain
                ));
            }
        }

        private void ValidateCrossFadeBlurSetup(
            Camera mainOutputCamera,
            List<DebugIssue> issues)
        {
            CameraBlurState expectedMainState = mainOutputCamera != null
                ? mainOutputCamera.GetComponent<CameraBlurState>()
                : null;

            if (expectedMainState == null ||
                mainCameraBlurState != expectedMainState)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "Main Camera 缺少或尚未綁定 CameraBlurState。請執行建立組件。",
                    mainOutputCamera != null
                        ? (Object)mainOutputCamera
                        : this
                ));
            }
            else if (!expectedMainState.enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "Main Camera 的 CameraBlurState 必須保持啟用。",
                    expectedMainState
                ));
            }

            CameraBlurState expectedCrossFadeState =
                crossFadeRenderCamera != null
                    ? crossFadeRenderCamera.GetComponent<CameraBlurState>()
                    : null;

            if (expectedCrossFadeState == null ||
                crossFadeRenderBlurState != expectedCrossFadeState)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "Cross Fade Render Camera 缺少或尚未綁定 CameraBlurState。請執行建立組件。",
                    crossFadeRenderCamera != null
                        ? (Object)crossFadeRenderCamera
                        : this
                ));
            }
            else if (!expectedCrossFadeState.enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "Cross Fade Render Camera 的 CameraBlurState 必須保持啟用。",
                    expectedCrossFadeState
                ));
            }

            HashSet<ScriptableRendererData> rendererDataSet =
                new HashSet<ScriptableRendererData>();

            AddRendererData(
                GraphicsSettings.currentRenderPipeline as
                    UniversalRenderPipelineAsset,
                rendererDataSet
            );

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                AddRendererData(
                    QualitySettings.GetRenderPipelineAssetAt(i) as
                        UniversalRenderPipelineAsset,
                    rendererDataSet
                );
            }

            foreach (ScriptableRendererData rendererData in rendererDataSet)
            {
                if (rendererData == null)
                    continue;

                if (!rendererData.TryGetRendererFeature(
                    out CameraBlurRendererFeature feature))
                {
                    issues.Add(new DebugIssue(
                        DebugSeverity.Error,
                        $"URP Renderer ({rendererData.name}) 缺少 CameraBlurRendererFeature。",
                        rendererData
                    ));
                }
                else if (feature != null && !feature.isActive)
                {
                    issues.Add(new DebugIssue(
                        DebugSeverity.Error,
                        $"URP Renderer ({rendererData.name}) 的 CameraBlurRendererFeature 已停用。",
                        feature
                    ));
                }
            }
        }

        private static void AddRendererData(
            UniversalRenderPipelineAsset pipelineAsset,
            HashSet<ScriptableRendererData> rendererDataSet)
        {
            if (pipelineAsset == null)
                return;

            foreach (ScriptableRendererData rendererData in
                pipelineAsset.rendererDataList)
            {
                if (rendererData != null)
                    rendererDataSet.Add(rendererData);
            }
        }

        private void ValidateCrossFadeRenderCamera(List<DebugIssue> issues)
        {
            if (!crossFadeRenderCamera.gameObject.activeInHierarchy)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "Cross Fade Render Camera 的 GameObject 未啟用，runtime 無法開啟離屏渲染。",
                    crossFadeRenderCamera
                ));
            }

            if (!Application.isPlaying &&
                !_isEditorCrossFadePreviewActive &&
                crossFadeRenderCamera.enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "Cross Fade Render Camera 在 Edit Mode 下應預先停用，避免未進入 transition 就重複渲染。",
                    crossFadeRenderCamera
                ));
            }

            if (crossFadeRenderCamera.GetComponent<AudioListener>() != null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "Cross Fade Render Camera 不應掛 AudioListener，否則會與 MainCamera 重複。",
                    crossFadeRenderCamera
                ));
            }

            if (!IsStoryboardCrossFadeActive &&
                crossFadeRenderCamera.targetTexture != crossFadeRenderTexture)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "Cross Fade Render Camera 的 Target Texture 與 CameraSystemMaster.crossFadeRenderTexture 不一致。",
                    crossFadeRenderCamera
                ));
            }

            Camera mainOutputCamera = ResolveMainOutputCamera(out bool ambiguous);

            if (!ambiguous && mainOutputCamera != null &&
                !Mathf.Approximately(
                    crossFadeRenderCamera.depth,
                    mainOutputCamera.depth - 1f))
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "Cross Fade Render Camera 的 Depth 應為 MainCamera.depth - 1，確保 RenderTexture 在 Storyboard 之前更新。",
                    crossFadeRenderCamera
                ));
            }
        }

        private void ValidateCrossFadeRenderBrain(
            bool validChannel,
            List<DebugIssue> issues)
        {
            if (!Application.isPlaying &&
                !_isEditorCrossFadePreviewActive &&
                crossFadeRenderBrain.enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "Cross Fade Render Brain 在 Edit Mode 下應預先停用，runtime 會在 transition 開始時啟用。",
                    crossFadeRenderBrain
                ));
            }

            if (validChannel &&
                crossFadeRenderBrain.ChannelMask != crossFadeOutputChannel)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"Cross Fade Render Brain 的 Channel Mask 必須只包含 {crossFadeOutputChannel}。",
                    crossFadeRenderBrain
                ));
            }

            if (crossFadeRenderBrain.UpdateMethod !=
                CinemachineBrain.UpdateMethods.SmartUpdate)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "Cross Fade Render Brain 的 Update Method 應為 Smart Update。",
                    crossFadeRenderBrain
                ));
            }

            if (crossFadeRenderBrain.DefaultBlend.Style !=
                    CinemachineBlendDefinition.Styles.Cut ||
                !Mathf.Approximately(crossFadeRenderBrain.DefaultBlend.Time, 0f))
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "Cross Fade Render Brain 的 Default Blend 應為 Cut / 0，避免 RenderTexture 內再發生第二次 camera blend。",
                    crossFadeRenderBrain
                ));
            }
        }

        private void ValidateCrossFadeVirtualCameraChannel(
            string label,
            CinemachineCamera camera,
            bool validChannel,
            List<DebugIssue> issues)
        {
            if (camera == null || !validChannel)
                return;

            if (camera.OutputChannel != crossFadeOutputChannel)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"{label} 的 Output Channel 必須為 {crossFadeOutputChannel}。",
                    camera
                ));
            }

            if (!IsStoryboardCrossFadeActive &&
                camera.Priority.Value != inactivePriority)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    $"{label} 的初始 Priority 應為 inactivePriority ({inactivePriority})。",
                    camera
                ));
            }
        }

        private void ValidateCrossFadeStoryboardCamera(
            List<DebugIssue> issues)
        {
            CinemachineCamera camera = crossFadeStoryboardCamera;

            if (!camera.gameObject.activeInHierarchy || !camera.enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "Cross Fade Storyboard Camera 必須保持啟用，才能由 Main Brain camera override 驅動。",
                    camera
                ));
            }

            if (camera.OutputChannel != OutputChannels.Default)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "Cross Fade Storyboard Camera 的 Output Channel 必須為 Default。",
                    camera
                ));
            }

            if (camera.Priority.Value != GetStoryboardInactivePriority())
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    $"Cross Fade Storyboard Camera 的初始 Priority 應為 {GetStoryboardInactivePriority()}，避免它在非轉場期間被 Main Brain 選中。",
                    camera
                ));
            }

            CinemachineStoryboard storyboard =
                camera.GetComponent<CinemachineStoryboard>();

            if (storyboard == null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "Cross Fade Storyboard Camera 缺少 CinemachineStoryboard。",
                    camera
                ));
                return;
            }

            if (!storyboard.enabled || !storyboard.MuteCamera)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "Cross Fade Storyboard Camera 的 CinemachineStoryboard 必須啟用 Mute Camera。",
                    storyboard
                ));
            }

            if (!Mathf.Approximately(storyboard.Alpha, 1f))
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "Cross Fade Storyboard 的 Alpha 應固定為 1；畫面權重由 Main Brain camera override 控制。",
                    storyboard
                ));
            }

            if (!Application.isPlaying &&
                !_isEditorCrossFadePreviewActive &&
                storyboard.ShowImage)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "Cross Fade Storyboard 在 Edit Mode 下應關閉 Show Image，runtime 會在轉場開始時啟用。",
                    storyboard
                ));
            }
        }

        private static bool HasMatchingStableRenderCameraSettings(
            Camera source,
            Camera target)
        {
            if (source == null || target == null)
                return false;

            return source.clearFlags == target.clearFlags &&
                source.backgroundColor == target.backgroundColor &&
                source.cullingMask == target.cullingMask &&
                source.renderingPath == target.renderingPath &&
                source.allowHDR == target.allowHDR &&
                source.allowMSAA == target.allowMSAA &&
                source.useOcclusionCulling == target.useOcclusionCulling;
        }

        private static Component GetAdditionalCameraData(Camera camera)
        {
            if (camera == null)
                return null;

            Component[] components = camera.GetComponents<Component>();

            foreach (Component component in components)
            {
                if (component != null &&
                    component.GetType().FullName ==
                        "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData")
                {
                    return component;
                }
            }

            return null;
        }

        private static bool IsValidCrossFadeOutputChannel(OutputChannels channel)
        {
            uint value = (uint)channel;
            uint defaultValue = (uint)OutputChannels.Default;

            return value != 0 &&
                (value & defaultValue) == 0 &&
                (value & (value - 1)) == 0;
        }

        private GameObject ResolveMainOutputObject(out bool ambiguous)
        {
            ambiguous = false;

            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            List<GameObject> taggedMainObjects = new List<GameObject>();

            foreach (Transform candidate in transforms)
            {
                if (candidate == null ||
                    !candidate.gameObject.CompareTag("MainCamera"))
                {
                    continue;
                }

                taggedMainObjects.Add(candidate.gameObject);
            }

            if (taggedMainObjects.Count == 1)
                return taggedMainObjects[0];

            if (taggedMainObjects.Count > 1)
            {
                ambiguous = true;
                return null;
            }

            Camera[] cameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            List<Camera> brainCameras = new List<Camera>();

            foreach (Camera camera in cameras)
            {
                if (camera == null ||
                    camera == crossFadeRenderCamera ||
                    camera == crossFadeRenderBrain?.GetComponent<Camera>() ||
                    camera.GetComponent<CinemachineBrain>() == null)
                {
                    continue;
                }

                brainCameras.Add(camera);
            }

            if (brainCameras.Count == 1)
                return brainCameras[0].gameObject;

            ambiguous = brainCameras.Count > 1;
            return null;
        }

        private Camera ResolveMainOutputCamera(out bool ambiguous)
        {
            GameObject mainOutputObject = ResolveMainOutputObject(out ambiguous);
            return mainOutputObject != null
                ? mainOutputObject.GetComponent<Camera>()
                : null;
        }

        private Camera EnsureMainOutputCamera(GameObject mainOutputObject)
        {
            bool createdObject = mainOutputObject == null;

            if (createdObject)
            {
                mainOutputObject = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(
                    mainOutputObject,
                    "Create Main Camera"
                );
                mainOutputObject.transform.position = new Vector3(0f, 0f, -10f);
                mainOutputObject.transform.rotation = Quaternion.identity;
                mainOutputObject.transform.localScale = Vector3.one;
            }

            Undo.RecordObject(mainOutputObject, "Configure Main Camera");

            if (!mainOutputObject.activeSelf)
                mainOutputObject.SetActive(true);

            if (!mainOutputObject.CompareTag("MainCamera"))
                mainOutputObject.tag = "MainCamera";

            Camera mainCamera = mainOutputObject.GetComponent<Camera>();

            if (mainCamera == null)
                mainCamera = Undo.AddComponent<Camera>(mainOutputObject);

            Undo.RecordObject(mainCamera, "Configure Main Camera");
            mainCamera.enabled = true;
            mainCamera.targetTexture = null;

            CinemachineBrain mainBrain =
                mainOutputObject.GetComponent<CinemachineBrain>();

            if (mainBrain == null)
                mainBrain = Undo.AddComponent<CinemachineBrain>(mainOutputObject);

            Undo.RecordObject(mainBrain, "Configure Main Camera Brain");
            mainBrain.enabled = true;

            EnsureUniversalAdditionalCameraData(mainCamera);
            mainCameraBlurState = EnsureCameraBlurState(mainCamera);

            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            bool hasNonCrossFadeListener = false;

            foreach (AudioListener listener in listeners)
            {
                bool isSeparateCrossFadeListener =
                    crossFadeRenderCamera != null &&
                    crossFadeRenderCamera.gameObject != mainOutputObject &&
                    listener != null &&
                    listener.gameObject == crossFadeRenderCamera.gameObject;

                if (listener == null || isSeparateCrossFadeListener)
                {
                    continue;
                }

                hasNonCrossFadeListener = true;
                break;
            }

            if (!hasNonCrossFadeListener)
                Undo.AddComponent<AudioListener>(mainOutputObject);

            EditorUtility.SetDirty(mainOutputObject);
            EditorUtility.SetDirty(mainCamera);
            EditorUtility.SetDirty(mainBrain);

            return mainCamera;
        }

        private static void EnsureUniversalAdditionalCameraData(Camera camera)
        {
            if (camera == null || GetAdditionalCameraData(camera) != null)
                return;

            Type additionalDataType = null;

            foreach (System.Reflection.Assembly assembly in
                AppDomain.CurrentDomain.GetAssemblies())
            {
                additionalDataType = assembly.GetType(
                    "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData",
                    false
                );

                if (additionalDataType != null)
                    break;
            }

            if (additionalDataType == null ||
                !typeof(Component).IsAssignableFrom(additionalDataType))
            {
                Debug.LogError(
                    $"[{nameof(CameraSystemMaster)}] 找不到 URP UniversalAdditionalCameraData 類型，無法補齊 Main Camera 的 URP 設定。",
                    camera
                );
                return;
            }

            Component additionalData = Undo.AddComponent(
                camera.gameObject,
                additionalDataType
            );

            if (additionalData != null)
                EditorUtility.SetDirty(additionalData);
        }

        private static CameraBlurState EnsureCameraBlurState(Camera camera)
        {
            if (camera == null)
                return null;

            CameraBlurState state = camera.GetComponent<CameraBlurState>();

            if (state == null)
                state = Undo.AddComponent<CameraBlurState>(camera.gameObject);

            if (state != null)
            {
                state.Clear();
                EditorUtility.SetDirty(state);
            }

            return state;
        }

        private void EnsureCrossFadeRenderRig(Camera mainOutputCamera)
        {
            CinemachineBrain mainBrain = mainOutputCamera != null
                ? mainOutputCamera.GetComponent<CinemachineBrain>()
                : null;
            GameObject renderObject = null;

            if (crossFadeRenderCamera != null &&
                crossFadeRenderCamera != mainOutputCamera)
            {
                renderObject = crossFadeRenderCamera.gameObject;
            }
            else if (crossFadeRenderBrain != null &&
                crossFadeRenderBrain != mainBrain)
            {
                renderObject = crossFadeRenderBrain.gameObject;
            }

            if (renderObject == null)
            {
                renderObject = new GameObject("CrossFade_RenderCamera");
                Undo.RegisterCreatedObjectUndo(
                    renderObject,
                    "Create Cross Fade Render Camera"
                );
                renderObject.transform.SetParent(transform, false);
                renderObject.transform.localPosition = Vector3.zero;
                renderObject.transform.localRotation = Quaternion.identity;
                renderObject.transform.localScale = Vector3.one;
            }

            Undo.RecordObject(renderObject, "Configure Cross Fade Render Camera");

            if (!renderObject.activeSelf)
            {
                renderObject.SetActive(true);
            }

            Camera renderCamera = renderObject.GetComponent<Camera>();

            if (renderCamera == null)
            {
                renderCamera = Undo.AddComponent<Camera>(renderObject);
            }

            crossFadeRenderBlurState = EnsureCameraBlurState(renderCamera);

            CinemachineBrain renderBrain =
                renderObject.GetComponent<CinemachineBrain>();

            if (renderBrain == null)
            {
                renderBrain = Undo.AddComponent<CinemachineBrain>(renderObject);
            }

            if (mainOutputCamera != null && mainOutputCamera != renderCamera)
            {
                Undo.RecordObject(renderCamera, "Copy Main Camera Render Settings");
                EditorUtility.CopySerialized(mainOutputCamera, renderCamera);
                CopyAdditionalCameraData(mainOutputCamera, renderCamera);
            }
            else
            {
                Undo.RecordObject(renderCamera, "Configure Cross Fade Render Camera");
            }

            renderCamera.targetTexture = crossFadeRenderTexture;
            renderCamera.depth = mainOutputCamera != null
                ? mainOutputCamera.depth - 1f
                : -2f;
            renderCamera.enabled = false;

            AudioListener audioListener =
                renderObject.GetComponent<AudioListener>();

            if (audioListener != null)
            {
                Undo.DestroyObjectImmediate(audioListener);
            }

            Undo.RecordObject(renderBrain, "Configure Cross Fade Render Brain");
            renderBrain.ChannelMask = crossFadeOutputChannel;
            renderBrain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
            renderBrain.BlendUpdateMethod =
                CinemachineBrain.BrainUpdateMethods.LateUpdate;
            renderBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.Cut,
                0f
            );
            renderBrain.enabled = false;

            crossFadeRenderCamera = renderCamera;
            crossFadeRenderBrain = renderBrain;

            EditorUtility.SetDirty(renderCamera);
            EditorUtility.SetDirty(renderBrain);
            EditorUtility.SetDirty(this);
        }

        private static void CopyAdditionalCameraData(
            Camera sourceCamera,
            Camera targetCamera)
        {
            Component source = GetAdditionalCameraData(sourceCamera);

            if (source == null || targetCamera == null)
                return;

            Component target = targetCamera.GetComponent(source.GetType());

            if (target == null)
            {
                target = Undo.AddComponent(
                    targetCamera.gameObject,
                    source.GetType()
                );
            }

            if (target == null)
                return;

            Undo.RecordObject(target, "Copy Additional Camera Data");
            EditorUtility.CopySerialized(source, target);
            EditorUtility.SetDirty(target);
        }

        private void EnsureCrossFadeVirtualCameras()
        {
            if (!CanReuseCrossFadeCamera(crossFadeGeneralCamera))
            {
                crossFadeGeneralCamera =
                    CreateCameraRig("CinemachineCamera_CrossFade_General");
            }

            if (!CanReuseCrossFadeCamera(
                crossFadeTrackingCamera,
                crossFadeGeneralCamera))
            {
                crossFadeTrackingCamera =
                    CreateCameraRig("CinemachineCamera_CrossFade_Tracking");
            }

            if (!CanReuseCrossFadeCamera(
                crossFadeDollyCamera,
                crossFadeGeneralCamera,
                crossFadeTrackingCamera))
            {
                crossFadeDollyCamera =
                    CreateCameraRig("CinemachineCamera_CrossFade_Dolly");
            }

            FixGeneralCamera(crossFadeGeneralCamera, generalCamera);
            FixTrackingCamera(crossFadeTrackingCamera, trackingCamera);
            FixDollyCamera(crossFadeDollyCamera, dollyCamera);

            FixCrossFadeVirtualCamera(crossFadeGeneralCamera);
            FixCrossFadeVirtualCamera(crossFadeTrackingCamera);
            FixCrossFadeVirtualCamera(crossFadeDollyCamera);

            EditorUtility.SetDirty(this);
        }

        private void EnsureCrossFadeStoryboardCamera()
        {
            if (!CanReuseCrossFadeStoryboardCamera(crossFadeStoryboardCamera))
            {
                crossFadeStoryboardCamera =
                    CreateCameraRig("CinemachineCamera_CrossFade_Storyboard");
            }

            CinemachineCamera camera = crossFadeStoryboardCamera;
            CinemachineStoryboard storyboard =
                EnsureComponent<CinemachineStoryboard>(camera);

            Undo.RecordObject(camera, "Configure Cross Fade Storyboard Camera");
            camera.OutputChannel = OutputChannels.Default;
            camera.Priority.Enabled = true;
            camera.Priority.Value = GetStoryboardInactivePriority();
            camera.enabled = true;

            if (!camera.gameObject.activeSelf)
            {
                Undo.RecordObject(
                    camera.gameObject,
                    "Enable Cross Fade Storyboard Camera"
                );
                camera.gameObject.SetActive(true);
            }

            Undo.RecordObject(storyboard, "Configure Cross Fade Storyboard");
            storyboard.enabled = true;
            storyboard.ShowImage = false;
            storyboard.Image = crossFadeRenderTexture;
            storyboard.Alpha = 1f;
            storyboard.Aspect = CinemachineStoryboard.FillStrategy.CropImageToFit;
            storyboard.Center = Vector2.zero;
            storyboard.Rotation = Vector3.zero;
            storyboard.Scale = Vector2.one;
            storyboard.SyncScale = true;
            storyboard.MuteCamera = true;
            storyboard.SplitView = 0f;
            storyboard.RenderMode =
                CinemachineStoryboard.StoryboardRenderMode.ScreenSpaceOverlay;
            storyboard.SortingOrder = storyboardSortingOrder;

            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(storyboard);
            EditorUtility.SetDirty(this);
        }

        private bool CanReuseCrossFadeStoryboardCamera(
            CinemachineCamera candidate)
        {
            return candidate != null && !IsProfileCamera(candidate);
        }

        private bool CanReuseCrossFadeCamera(
            CinemachineCamera candidate,
            CinemachineCamera otherCrossFadeCameraA = null,
            CinemachineCamera otherCrossFadeCameraB = null)
        {
            if (candidate == null ||
                candidate == generalCamera ||
                candidate == generalCameraB ||
                candidate == trackingCamera ||
                candidate == dollyCamera ||
                candidate == crossFadeStoryboardCamera ||
                candidate == otherCrossFadeCameraA ||
                candidate == otherCrossFadeCameraB)
            {
                return false;
            }

            return true;
        }

        private void FixCrossFadeVirtualCamera(CinemachineCamera camera)
        {
            if (camera == null)
                return;

            Undo.RecordObject(camera, "Configure Cross Fade Cinemachine Camera");
            camera.OutputChannel = crossFadeOutputChannel;
            camera.Priority.Enabled = true;
            camera.Priority.Value = inactivePriority;
            camera.enabled = true;

            if (!camera.gameObject.activeSelf)
            {
                Undo.RecordObject(
                    camera.gameObject,
                    "Enable Cross Fade Cinemachine Camera"
                );
                camera.gameObject.SetActive(true);
            }

            EditorUtility.SetDirty(camera);
        }

        private void FixMainBrainChannelMask(CinemachineBrain mainBrain)
        {
            if (mainBrain == null ||
                !IsValidCrossFadeOutputChannel(crossFadeOutputChannel))
            {
                return;
            }

            Undo.RecordObject(mainBrain, "Configure Main Camera Brain Channels");

            uint mask = (uint)mainBrain.ChannelMask;
            mask |= (uint)OutputChannels.Default;
            mask &= ~(uint)crossFadeOutputChannel;
            mainBrain.ChannelMask = (OutputChannels)mask;

            EditorUtility.SetDirty(mainBrain);
        }

        private static void FixPrimaryVirtualCameraChannel(
            CinemachineCamera camera)
        {
            if (camera == null)
                return;

            Undo.RecordObject(camera, "Configure Main Cinemachine Camera Channel");
            camera.OutputChannel = OutputChannels.Default;
            EditorUtility.SetDirty(camera);
        }

        private CinemachineCamera CreateCameraRig(string objectName)
        {
            GameObject cameraObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(cameraObject, $"Create {objectName}");

            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;
            cameraObject.transform.localScale = Vector3.one;

            CinemachineCamera camera =
                Undo.AddComponent<CinemachineCamera>(cameraObject);

            camera.Priority.Value = inactivePriority;

            return camera;
        }

        private void FixGeneralCamera(
            CinemachineCamera camera,
            CinemachineCamera copyFrom)
        {
            if (camera == null)
                return;

            FixCommonCameraSettings(camera);

            CinemachinePositionComposer position =
                EnsureComponent<CinemachinePositionComposer>(camera);
            CinemachineRotationComposer rotation =
                EnsureComponent<CinemachineRotationComposer>(camera);
            CinemachineCameraOffset directionalOffset =
                EnsureComponent<CinemachineCameraOffset>(camera);

            DisableConflictingBodyComponents<CinemachinePositionComposer>(camera);
            ConfigureDirectionalCameraOffset(directionalOffset);

            if (copyFrom != null)
            {
                CopyComponentSettings(
                    copyFrom.GetComponent<CinemachinePositionComposer>(),
                    position
                );
                CopyComponentSettings(
                    copyFrom.GetComponent<CinemachineRotationComposer>(),
                    rotation
                );
                CopyCameraCoreSettings(copyFrom, camera);
            }
            else
            {
                Undo.RecordObject(position, "Configure General Camera Position Composer");
                position.CameraDistance = Mathf.Max(0.01f, position.CameraDistance);
                position.Damping = Vector3.one;

                Undo.RecordObject(rotation, "Configure General Camera Rotation Composer");
                rotation.Damping = Vector2.one;
            }

            EditorUtility.SetDirty(position);
            EditorUtility.SetDirty(rotation);
            EditorUtility.SetDirty(directionalOffset);
        }

        private void FixTrackingCamera(
            CinemachineCamera camera,
            CinemachineCamera copyFrom = null)
        {
            if (camera == null)
                return;

            FixCommonCameraSettings(camera);

            CinemachineFollow follow =
                EnsureComponent<CinemachineFollow>(camera);
            CinemachineRotationComposer rotation =
                EnsureComponent<CinemachineRotationComposer>(camera);
            CinemachineCameraOffset directionalOffset =
                EnsureComponent<CinemachineCameraOffset>(camera);

            DisableConflictingBodyComponents<CinemachineFollow>(camera);
            ConfigureDirectionalCameraOffset(directionalOffset);

            if (copyFrom != null)
            {
                CopyComponentSettings(
                    copyFrom.GetComponent<CinemachineFollow>(),
                    follow
                );
                CopyComponentSettings(
                    copyFrom.GetComponent<CinemachineRotationComposer>(),
                    rotation
                );
                CopyCameraCoreSettings(copyFrom, camera);
            }
            else
            {
                Undo.RecordObject(follow, "Configure Tracking Camera Follow");

                if (follow.FollowOffset == Vector3.zero)
                {
                    follow.FollowOffset = new Vector3(0f, 0.1f, 1f);
                }

                Undo.RecordObject(rotation, "Configure Tracking Camera Rotation Composer");
                rotation.Damping = Vector2.one;
            }

            EditorUtility.SetDirty(follow);
            EditorUtility.SetDirty(rotation);
            EditorUtility.SetDirty(directionalOffset);
        }

        private static void ConfigureDirectionalCameraOffset(
            CinemachineCameraOffset cameraOffset)
        {
            if (cameraOffset == null)
                return;

            cameraOffset.Offset = Vector3.zero;
            cameraOffset.ApplyAfter = CinemachineCore.Stage.Aim;
            cameraOffset.PreserveComposition = false;
        }

        private void FixDollyCamera(
            CinemachineCamera camera,
            CinemachineCamera copyFrom = null)
        {
            if (camera == null)
                return;

            FixCommonCameraSettings(camera);

            CinemachineSplineDolly dolly =
                EnsureComponent<CinemachineSplineDolly>(camera);
            CinemachineRotationComposer rotation =
                EnsureComponent<CinemachineRotationComposer>(camera);

            DisableConflictingBodyComponents<CinemachineSplineDolly>(camera);

            if (copyFrom != null)
            {
                CopyComponentSettings(
                    copyFrom.GetComponent<CinemachineSplineDolly>(),
                    dolly
                );
                CopyComponentSettings(
                    copyFrom.GetComponent<CinemachineRotationComposer>(),
                    rotation
                );
                CopyCameraCoreSettings(copyFrom, camera);
            }
            else
            {
                Undo.RecordObject(dolly, "Configure Dolly Camera Spline Dolly");
                dolly.PositionUnits = UnityEngine.Splines.PathIndexUnit.Normalized;
                dolly.CameraRotation = CinemachineSplineDolly.RotationMode.Default;

                Undo.RecordObject(rotation, "Configure Dolly Camera Rotation Composer");
                rotation.TargetOffset = new Vector3(0f, 1f, 0f);
                rotation.Damping = Vector2.zero;
            }

            EditorUtility.SetDirty(dolly);
            EditorUtility.SetDirty(rotation);
        }

        private void FixCommonCameraSettings(CinemachineCamera camera)
        {
            Undo.RecordObject(camera.gameObject, "Configure Cinemachine Camera GameObject");
            Undo.RecordObject(camera, "Configure Cinemachine Camera");

            if (!camera.gameObject.activeSelf)
            {
                camera.gameObject.SetActive(true);
            }

            camera.enabled = true;
            camera.Priority.Value = inactivePriority;

            if (camera.Lens.FieldOfView < 10f || camera.Lens.FieldOfView > 120f)
            {
                camera.Lens.FieldOfView = Mathf.Clamp(camera.Lens.FieldOfView, 10f, 120f);
            }

            EditorUtility.SetDirty(camera);
        }

        private T EnsureComponent<T>(CinemachineCamera camera) where T : Behaviour
        {
            T component = camera.GetComponent<T>();

            if (component == null)
            {
                component = Undo.AddComponent<T>(camera.gameObject);
            }

            Undo.RecordObject(component, $"Configure {typeof(T).Name}");
            component.enabled = true;

            return component;
        }

        private void DisableConflictingBodyComponents<TExpected>(
            CinemachineCamera camera) where TExpected : CinemachineComponentBase
        {
            CinemachineComponentBase[] components =
                camera.GetComponents<CinemachineComponentBase>();

            foreach (CinemachineComponentBase component in components)
            {
                if (component == null ||
                    component.Stage != CinemachineCore.Stage.Body ||
                    component is TExpected ||
                    !component.enabled)
                {
                    continue;
                }

                Undo.RecordObject(component, "Disable Conflicting Cinemachine Body Component");
                component.enabled = false;
                EditorUtility.SetDirty(component);
            }
        }

        private void CopyCameraCoreSettings(
            CinemachineCamera source,
            CinemachineCamera target)
        {
            if (source == null || target == null)
                return;

            Undo.RecordObject(target, "Copy Cinemachine Camera Settings");
            target.Lens = source.Lens;
            target.OutputChannel = source.OutputChannel;
            target.StandbyUpdate = source.StandbyUpdate;
            target.BlendHint = source.BlendHint;
            target.Target = source.Target;
            target.Priority.Value = inactivePriority;
            EditorUtility.SetDirty(target);
        }

        private void CopyComponentSettings<T>(T source, T target) where T : Component
        {
            if (source == null || target == null)
                return;

            Undo.RecordObject(target, $"Copy {typeof(T).Name} Settings");
            EditorUtility.CopySerialized(source, target);

            if (target is Behaviour behaviour)
            {
                behaviour.enabled = true;
            }

            EditorUtility.SetDirty(target);
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CameraSystemMaster))]
    public class CameraSystemMasterEditor : Editor
    {
        private SerializedProperty _generalCamera;
        private SerializedProperty _generalCameraB;
        private SerializedProperty _trackingCamera;
        private SerializedProperty _dollyCamera;
        private SerializedProperty _livePriority;
        private SerializedProperty _inactivePriority;
        private SerializedProperty _crossFadeRenderCamera;
        private SerializedProperty _crossFadeRenderBrain;
        private SerializedProperty _crossFadeRenderTexture;
        private SerializedProperty _crossFadeOutputChannel;
        private SerializedProperty _crossFadeGeneralCamera;
        private SerializedProperty _crossFadeTrackingCamera;
        private SerializedProperty _crossFadeDollyCamera;
        private SerializedProperty _crossFadeStoryboardCamera;
        private SerializedProperty _autoResizeCrossFadeRenderTexture;
        private SerializedProperty _fallbackCrossFadeTextureWidth;
        private SerializedProperty _fallbackCrossFadeTextureHeight;
        private SerializedProperty _storyboardSortingOrder;
        private SerializedProperty _mainCameraBlurState;
        private SerializedProperty _crossFadeRenderBlurState;

        private bool _showGeneratedRig;
        private bool _showRenderTextureSettings;
        private int _setupErrorCount;
        private int _setupWarningCount;
        private double _nextStatusRefreshTime;

        private void OnEnable()
        {
            _generalCamera = serializedObject.FindProperty("generalCamera");
            _generalCameraB = serializedObject.FindProperty("generalCameraB");
            _trackingCamera = serializedObject.FindProperty("trackingCamera");
            _dollyCamera = serializedObject.FindProperty("dollyCamera");
            _livePriority = serializedObject.FindProperty("livePriority");
            _inactivePriority = serializedObject.FindProperty("inactivePriority");
            _crossFadeRenderCamera = serializedObject.FindProperty("crossFadeRenderCamera");
            _crossFadeRenderBrain = serializedObject.FindProperty("crossFadeRenderBrain");
            _crossFadeRenderTexture = serializedObject.FindProperty("crossFadeRenderTexture");
            _crossFadeOutputChannel = serializedObject.FindProperty("crossFadeOutputChannel");
            _crossFadeGeneralCamera = serializedObject.FindProperty("crossFadeGeneralCamera");
            _crossFadeTrackingCamera = serializedObject.FindProperty("crossFadeTrackingCamera");
            _crossFadeDollyCamera = serializedObject.FindProperty("crossFadeDollyCamera");
            _crossFadeStoryboardCamera = serializedObject.FindProperty("crossFadeStoryboardCamera");
            _autoResizeCrossFadeRenderTexture = serializedObject.FindProperty("autoResizeCrossFadeRenderTexture");
            _fallbackCrossFadeTextureWidth = serializedObject.FindProperty("fallbackCrossFadeTextureWidth");
            _fallbackCrossFadeTextureHeight = serializedObject.FindProperty("fallbackCrossFadeTextureHeight");
            _storyboardSortingOrder = serializedObject.FindProperty("storyboardSortingOrder");
            _mainCameraBlurState = serializedObject.FindProperty("mainCameraBlurState");
            _crossFadeRenderBlurState = serializedObject.FindProperty("crossFadeRenderBlurState");

            EditorApplication.hierarchyChanged += RequestStatusRefresh;
            Undo.undoRedoPerformed += RequestStatusRefresh;
            RefreshSetupStatus(true);
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= RequestStatusRefresh;
            Undo.undoRedoPerformed -= RequestStatusRefresh;
        }

        public override void OnInspectorGUI()
        {
            CameraSystemMaster master = target as CameraSystemMaster;

            if (master == null)
                return;

            serializedObject.Update();
            RefreshSetupStatus(false);

            DrawScriptField(master);
            DrawSetupStatus(master);
            DrawSetupTools(master);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("主要運鏡相機", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_generalCamera, new GUIContent("General Camera A"));
            EditorGUILayout.PropertyField(_generalCameraB, new GUIContent("General Camera B"));
            EditorGUILayout.PropertyField(_trackingCamera, new GUIContent("Tracking Camera"));
            EditorGUILayout.PropertyField(_dollyCamera, new GUIContent("Dolly Camera"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Priority", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_livePriority, new GUIContent("Live Priority"));
            EditorGUILayout.PropertyField(_inactivePriority, new GUIContent("Inactive Priority"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Crossfade", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _crossFadeOutputChannel,
                new GUIContent("Output Channel")
            );
            EditorGUILayout.PropertyField(
                _storyboardSortingOrder,
                new GUIContent("Storyboard Sorting Order")
            );
            _showGeneratedRig = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showGeneratedRig,
                "生成的 Crossfade Rig 引用"
            );

            if (_showGeneratedRig)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    _crossFadeRenderCamera,
                    new GUIContent("Render Camera")
                );
                EditorGUILayout.PropertyField(
                    _crossFadeRenderBrain,
                    new GUIContent("Render Brain")
                );
                EditorGUILayout.PropertyField(
                    _crossFadeGeneralCamera,
                    new GUIContent("General Transition Camera")
                );
                EditorGUILayout.PropertyField(
                    _crossFadeTrackingCamera,
                    new GUIContent("Tracking Transition Camera")
                );
                EditorGUILayout.PropertyField(
                    _crossFadeDollyCamera,
                    new GUIContent("Dolly Transition Camera")
                );
                EditorGUILayout.PropertyField(
                    _crossFadeStoryboardCamera,
                    new GUIContent("Storyboard Camera")
                );
                EditorGUILayout.PropertyField(
                    _mainCameraBlurState,
                    new GUIContent("Main Camera Blur State")
                );
                EditorGUILayout.PropertyField(
                    _crossFadeRenderBlurState,
                    new GUIContent("Render Camera Blur State")
                );
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            _showRenderTextureSettings = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showRenderTextureSettings,
                "RenderTexture 與顯示設定"
            );

            if (_showRenderTextureSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    _crossFadeRenderTexture,
                    new GUIContent("Render Texture (Optional)")
                );
                EditorGUILayout.PropertyField(
                    _autoResizeCrossFadeRenderTexture,
                    new GUIContent("Auto Resize")
                );

                if (!_autoResizeCrossFadeRenderTexture.boolValue)
                {
                    EditorGUILayout.PropertyField(
                        _fallbackCrossFadeTextureWidth,
                        new GUIContent("Fallback Width")
                    );
                    EditorGUILayout.PropertyField(
                        _fallbackCrossFadeTextureHeight,
                        new GUIContent("Fallback Height")
                    );
                }

                if (_crossFadeRenderTexture.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "未指定 RenderTexture 時，系統會在 crossfade 開始時動態建立並在結束後釋放。",
                        MessageType.Info
                    );
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            if (serializedObject.ApplyModifiedProperties())
            {
                RequestStatusRefresh();
                RefreshSetupStatus(true);
            }
        }

        private static void DrawScriptField(CameraSystemMaster master)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Script",
                    MonoScript.FromMonoBehaviour(master),
                    typeof(MonoScript),
                    false
                );
            }
        }

        private void DrawSetupStatus(CameraSystemMaster master)
        {
            EditorGUILayout.Space(6f);

            if (master.IsEditorCrossFadePreviewActive)
            {
                EditorGUILayout.HelpBox(
                    "Editor Timeline Crossfade 預覽中",
                    MessageType.Info
                );
            }
            else if (Application.isPlaying && master.IsStoryboardCrossFadeActive)
            {
                EditorGUILayout.HelpBox(
                    "Runtime Crossfade 執行中",
                    MessageType.Info
                );
            }

            if (_setupErrorCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"設定尚未完成：{_setupErrorCount} 個錯誤、{_setupWarningCount} 個警告。按「檢查設定」查看細節。",
                    MessageType.Error
                );
            }
            else if (_setupWarningCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"基本設定可使用，但仍有 {_setupWarningCount} 個建議項目。按「檢查設定」查看細節。",
                    MessageType.Warning
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "主 Camera、Storyboard RT Crossfade 與 Camera Blur rig 設定正常。",
                    MessageType.Info
                );
            }
        }

        private void DrawSetupTools(CameraSystemMaster master)
        {
            EditorGUILayout.Space(3f);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Camera setup tools 請在 Edit Mode 使用。",
                    MessageType.Info
                );
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("建立組件", GUILayout.Height(32f)))
                    {
                        master.DebugAutoFixCameraSetup();
                        serializedObject.Update();
                        RefreshSetupStatus(true);
                    }

                    if (GUILayout.Button("檢查設定", GUILayout.Height(32f)))
                    {
                        master.DebugValidateCameraSetup();
                        RefreshSetupStatus(true);
                    }
                }
            }
        }

        private void RequestStatusRefresh()
        {
            _nextStatusRefreshTime = 0d;
            Repaint();
        }

        private void RefreshSetupStatus(bool force)
        {
            if (!force && EditorApplication.timeSinceStartup < _nextStatusRefreshTime)
                return;

            CameraSystemMaster master = target as CameraSystemMaster;

            if (master == null)
                return;

            master.DebugGetCameraSetupIssueCounts(
                out _setupErrorCount,
                out _setupWarningCount
            );
            _nextStatusRefreshTime = EditorApplication.timeSinceStartup + 1d;
        }
    }
#endif
}
