// useful resources about shader stripping:
// https://docs.unity3d.com/ScriptReference/Build.IPreprocessShaders.OnProcessShader.html
// https://github.com/lujian101/ShaderVariantCollector
// https://blog.unity.com/technology/stripping-scriptable-shader-variants
// see also URP's ShaderPreprocessor.cs

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

using ShaderVariantLogLevel = UnityEngine.Rendering.Universal.ShaderVariantLogLevel;

namespace NiloToon.NiloToonURP
{
    // this class will try to match URP 12.1.10's ShaderPreprocessor.cs
    class NiloToonEditorShaderStripping : IPreprocessShaders
    {
        private static readonly System.Diagnostics.Stopwatch m_stripTimer = new System.Diagnostics.Stopwatch();

        LocalKeyword _NILOTOON_RECEIVE_URP_SHADOWMAPPING;
        LocalKeyword _MAIN_LIGHT_SHADOWS_CASCADE;

        LocalKeyword _ISFACE;
        LocalKeyword _FACE_MASK_ON;
        LocalKeyword _FACE_SHADOW_GRADIENTMAP;

        LocalKeyword _NILOTOON_RECEIVE_SELF_SHADOW;
        LocalKeyword _SHADOWS_SOFT;
        LocalKeyword _NILOTOON_SELFSHADOW_INTENSITY_MAP;

        LocalKeyword _RAMP_LIGHTING;
        LocalKeyword _RAMP_LIGHTING_SAMPLE_UVY_TEX;

        LocalKeyword _RAMP_SPECULAR;
        LocalKeyword _RAMP_SPECULAR_SAMPLE_UVY_TEX;

        LocalKeyword _SPECULARHIGHLIGHTS;
        LocalKeyword _SPECULARHIGHLIGHTS_TEX_TINT;

        Shader NiloToonCharacter_shader = Shader.Find("Universal Render Pipeline/NiloToon/NiloToon_Character");
        Shader NiloToonCharacterSticker_Additive_shader = Shader.Find("Universal Render Pipeline/NiloToon/NiloToon_Character Sticker(Additive)");
        Shader NiloToonCharacterSticker_Multiply_shader = Shader.Find("Universal Render Pipeline/NiloToon/NiloToon_Character Sticker(Multiply)");

        int m_TotalVariantsInputCount;
        int m_TotalVariantsOutputCount;

        // Multiple callback may be implemented.
        // The first one executed is the one where callbackOrder is returning the smallest number.
        public int callbackOrder { get { return 1; } } // URP's ShaderPreprocessor is 0, we set to 1 to execute after URP's stripping logic

        bool IsNiloToonCharacterRelatedShaders(Shader shader)
        {
            return
                shader == NiloToonCharacter_shader ||
                shader == NiloToonCharacterSticker_Additive_shader ||
                shader == NiloToonCharacterSticker_Multiply_shader;
        }
        LocalKeyword TryGetLocalKeyword(Shader shader, string name)
        {
            return shader.keywordSpace.FindKeyword(name);
        }

        void InitializeLocalShaderKeywords(Shader shader)
        {
            _NILOTOON_RECEIVE_URP_SHADOWMAPPING = TryGetLocalKeyword(shader, "_NILOTOON_RECEIVE_URP_SHADOWMAPPING"); // if NOT on, can strip the following
            _MAIN_LIGHT_SHADOWS_CASCADE = TryGetLocalKeyword(shader, "_MAIN_LIGHT_SHADOWS_CASCADE");

            _ISFACE = TryGetLocalKeyword(shader, "_ISFACE"); // if NOT on, can strip the following
            _FACE_MASK_ON = TryGetLocalKeyword(shader, "_FACE_MASK_ON");
            _FACE_SHADOW_GRADIENTMAP = TryGetLocalKeyword(shader, "_FACE_SHADOW_GRADIENTMAP");

            _NILOTOON_RECEIVE_SELF_SHADOW = TryGetLocalKeyword(shader, "_NILOTOON_RECEIVE_SELF_SHADOW"); // if NOT on, can strip the following
            _SHADOWS_SOFT = TryGetLocalKeyword(shader, "_SHADOWS_SOFT");
            _NILOTOON_SELFSHADOW_INTENSITY_MAP = TryGetLocalKeyword(shader, "_NILOTOON_SELFSHADOW_INTENSITY_MAP");

            _RAMP_LIGHTING = TryGetLocalKeyword(shader, "_RAMP_LIGHTING"); // if NOT on, can strip the following
            _RAMP_LIGHTING_SAMPLE_UVY_TEX = TryGetLocalKeyword(shader, "_RAMP_LIGHTING_SAMPLE_UVY_TEX");

            _RAMP_SPECULAR = TryGetLocalKeyword(shader, "_RAMP_SPECULAR"); // if NOT on, can strip the following
            _RAMP_SPECULAR_SAMPLE_UVY_TEX = TryGetLocalKeyword(shader, "_RAMP_SPECULAR_SAMPLE_UVY_TEX");

            _SPECULARHIGHLIGHTS = TryGetLocalKeyword(shader, "_SPECULARHIGHLIGHTS"); // if NOT on, can strip the following
            _SPECULARHIGHLIGHTS_TEX_TINT = TryGetLocalKeyword(shader, "_SPECULARHIGHLIGHTS_TEX_TINT");
        }
        bool StripInvalidVariants(ShaderCompilerData compilerData)
        {
            var currentKeywordSet = compilerData.shaderKeywordSet;

            // since URP updated their stripping, we should not remove any URP's keyword anymore, it may remove a valid variant
            /*
            // strip invalid _MAIN_LIGHT_SHADOWS_CASCADE
            if (!currentKeywordSet.IsEnabled(_NILOTOON_RECEIVE_URP_SHADOWMAPPING))
            {
                if (currentKeywordSet.IsEnabled(_MAIN_LIGHT_SHADOWS_CASCADE))
                {
                    return true;
                }
            }
            */

            // strip invalid _NILOTOON_SELFSHADOW_INTENSITY_MAP
            if (!currentKeywordSet.IsEnabled(_NILOTOON_RECEIVE_SELF_SHADOW))
            {
                if (currentKeywordSet.IsEnabled(_NILOTOON_SELFSHADOW_INTENSITY_MAP))
                    return true;
            }

            // [It seems that we can't strip a local keyword based on local keyword..?]
            // In Unity2021.3's build, this section will incorrectly strip some keyword(e.g. _MATCAP_ADD _MATCAP_BLEND _RAMP_LIGHTING_SAMPLE_UVY_TEX _SKIN_MASK_ON _SPECULARHIGHLIGHTS),
            // we now disable this section for "Unity 2021.3 or later" temporary until bug is fixed.
            /*
            // strip invalid _FACE_MASK_ON & _FACE_SHADOW_GRADIENTMAP
            if (!currentKeywordSet.IsEnabled(_ISFACE))
            {
                if (currentKeywordSet.IsEnabled(_FACE_MASK_ON))
                {
                    return true;
                }

                if (currentKeywordSet.IsEnabled(_FACE_SHADOW_GRADIENTMAP))
                {
                    return true;
                }
            }

            // strip invalid _RAMP_LIGHTING_SAMPLE_UVY_TEX
            if (!currentKeywordSet.IsEnabled(_RAMP_LIGHTING))
            {
                if (currentKeywordSet.IsEnabled(_RAMP_LIGHTING_SAMPLE_UVY_TEX))
                {
                    return true;
                }
            }

            // strip invalid _RAMP_SPECULAR_SAMPLE_UVY_TEX
            if (!currentKeywordSet.IsEnabled(_RAMP_SPECULAR))
            {
                if (currentKeywordSet.IsEnabled(_RAMP_SPECULAR_SAMPLE_UVY_TEX))
                {
                    return true;
                }
            }

            // strip invalid _SPECULARHIGHLIGHTS_TEX_TINT
            if (!currentKeywordSet.IsEnabled(_SPECULARHIGHLIGHTS))
            {
                if (currentKeywordSet.IsEnabled(_SPECULARHIGHLIGHTS_TEX_TINT))
                {
                    return true;
                }
            }
            */

            return false;
        }

#if !UNITY_2023_1_OR_NEWER
        void LogShaderVariants(Shader shader, ShaderSnippetData snippetData, ShaderVariantLogLevel logLevel, int prevVariantsCount, int currVariantsCount, double stripTimeMs)
        {
            if (logLevel == ShaderVariantLogLevel.AllShaders || shader.name.Contains("Universal Render Pipeline"))
            {
                float percentageCurrent = (float)currVariantsCount / (float)prevVariantsCount * 100f;
                float percentageTotal = (float)m_TotalVariantsOutputCount / (float)m_TotalVariantsInputCount * 100f;

                string result = string.Format("STRIPPING: {0} ({1} pass) ({2}) -" +
                    " Remaining shader variants = {3}/{4} = {5}% - Total = {6}/{7} = {8}% TimeMs={9}",
                    shader.name, snippetData.passName, snippetData.shaderType.ToString(), currVariantsCount,
                    prevVariantsCount, percentageCurrent, m_TotalVariantsOutputCount, m_TotalVariantsInputCount,
                    percentageTotal, stripTimeMs);
                Debug.Log(result);
            }
        }
#endif

        public void OnProcessShader(Shader shader, ShaderSnippetData snippetData, IList<ShaderCompilerData> compilerDataList)
        {
            if (!IsNiloToonCharacterRelatedShaders(shader))
                return;

            UniversalRenderPipelineAsset urpAsset = UniversalRenderPipeline.asset;
            if (urpAsset == null || compilerDataList == null || compilerDataList.Count == 0)
                return;

            NiloToonShaderStrippingSettingSO.Settings targetResultSetting = GetNiloToonStrippingTargetResult();
            List<ShaderKeyword> haveToStripList = GetStripKeywordList(shader, targetResultSetting);

            m_stripTimer.Start();

            InitializeLocalShaderKeywords(shader);

            int prevVariantCount = compilerDataList.Count;
            var inputShaderVariantCount = compilerDataList.Count;
            for (int i = 0; i < inputShaderVariantCount;)
            {
                bool removeInput = false;

                var currentCompilerData = compilerDataList[i];
                var currentKeywordSet = currentCompilerData.shaderKeywordSet;

                // haveToStripList keywords
                foreach (var ignoreKeyword in haveToStripList)
                {
                    if (currentKeywordSet.IsEnabled(ignoreKeyword))
                    {
                        removeInput = true;
                        break;
                    }
                }

                if (StripInvalidVariants(currentCompilerData))
                    removeInput = true;

                // Remove at swap back
                if (removeInput)
                    compilerDataList[i] = compilerDataList[--inputShaderVariantCount];
                else
                    ++i;
            }

            if (compilerDataList is List<ShaderCompilerData> inputDataList)
                inputDataList.RemoveRange(inputShaderVariantCount, inputDataList.Count - inputShaderVariantCount);
            else
            {
                for (int i = compilerDataList.Count - 1; i >= inputShaderVariantCount; --i)
                    compilerDataList.RemoveAt(i);
            }

            m_stripTimer.Stop();
            double stripTimeMs = m_stripTimer.Elapsed.TotalMilliseconds;
            m_stripTimer.Reset();

#if !UNITY_2023_1_OR_NEWER
            if (urpAsset.shaderVariantLogLevel != ShaderVariantLogLevel.Disabled)
            {
                m_TotalVariantsInputCount += prevVariantCount;
                m_TotalVariantsOutputCount += compilerDataList.Count;
                LogShaderVariants(shader, snippetData, urpAsset.shaderVariantLogLevel, prevVariantCount, compilerDataList.Count, stripTimeMs);
            }
#endif
        }

        // use targetResultSetting to add keywords that we want to strip to our ignore list
        private static List<ShaderKeyword> GetStripKeywordList(Shader shader,
            NiloToonShaderStrippingSettingSO.Settings targetResultSetting)
        {
            List<ShaderKeyword> haveToStripList = new List<ShaderKeyword>();

            // all Global keywords
            if (!targetResultSetting.include_NILOTOON_DEBUG_SHADING)
            {
                haveToStripList.Add(new ShaderKeyword("_NILOTOON_DEBUG_SHADING"));
            }

            if (!targetResultSetting.include_NILOTOON_FORCE_MINIMUM_SHADER)
            {
                haveToStripList.Add(new ShaderKeyword("_NILOTOON_FORCE_MINIMUM_SHADER"));
            }

            if (!targetResultSetting.include_NILOTOON_GLOBAL_ENABLE_SCREENSPACE_OUTLINE)
            {
                haveToStripList.Add(new ShaderKeyword("_NILOTOON_GLOBAL_ENABLE_SCREENSPACE_OUTLINE"));
            }

            if (!targetResultSetting.include_NILOTOON_GLOBAL_ENABLE_SCREENSPACE_OUTLINE_V2)
            {
                haveToStripList.Add(new ShaderKeyword("_NILOTOON_GLOBAL_ENABLE_SCREENSPACE_OUTLINE_V2"));
            }

            if (!targetResultSetting.include_NILOTOON_RECEIVE_URP_SHADOWMAPPING)
            {
                haveToStripList.Add(new ShaderKeyword("_NILOTOON_RECEIVE_URP_SHADOWMAPPING"));
            }

            if (!targetResultSetting.include_NILOTOON_RECEIVE_SELF_SHADOW)
            {
                haveToStripList.Add(new ShaderKeyword("_NILOTOON_RECEIVE_SELF_SHADOW"));
            }

            // since URP updated their stripping, we should not remove any URP's keyword anymore, it may remove a valid variant
            /*
            if (!targetResultSetting.include_MAIN_LIGHT_SHADOWS_CASCADE)
            {
                haveToStripList.Add(new ShaderKeyword("_MAIN_LIGHT_SHADOWS_CASCADE"));
            }
            */

            // since URP updated their stripping, we should not remove any URP's keyword anymore, it may remove a valid variant
            // After URP added Forward+, we can't strip this anymore,
            // Build will crash when Forward+ is active and we strip "_ADDITIONAL_LIGHT_SHADOWS"
            /*
#if !UNITY_2022_2_OR_NEWER
            if (!targetResultSetting.include_ADDITIONAL_LIGHT_SHADOWS)
            {
                haveToStripList.Add(new ShaderKeyword("_ADDITIONAL_LIGHT_SHADOWS"));
            }
#endif
            */

            // all Local Keywords need to be initialized with the shader
            if (!targetResultSetting.include_NILOTOON_DITHER_FADEOUT)
            {
                haveToStripList.Add(new ShaderKeyword(shader, "_NILOTOON_DITHER_FADEOUT"));
            }

            if (!targetResultSetting.include_NILOTOON_DISSOLVE)
            {
                haveToStripList.Add(new ShaderKeyword(shader, "_NILOTOON_DISSOLVE"));
            }

            if (!targetResultSetting.include_NILOTOON_PERCHARACTER_BASEMAP_OVERRIDE)
            {
                haveToStripList.Add(new ShaderKeyword(shader, "_NILOTOON_PERCHARACTER_BASEMAP_OVERRIDE"));
            }

            //----------------------------------------------------------------------------------------------------------------------------------
            // [strip Fog]
            // https://docs.unity3d.com/6000.2/Documentation/Manual/urp/shader-stripping-fog.html
            // We forced dynamic_branch for fog in NiloToonCharacter.shader for Unity6.1 or higher, so no need to strip them now.
            //haveToStripList.Add(new ShaderKeyword(shader, "FOG_EXP"));
            //haveToStripList.Add(new ShaderKeyword(shader, "FOG_EXP2"));
            //haveToStripList.Add(new ShaderKeyword(shader, "FOG_LINEAR"));

            // [strip XR]
            // By default, Unity adds this set of keywords to all graphics shader programs:
            // - STEREO_INSTANCING_ON
            // - STEREO_MULTIVIEW_ON
            // - STEREO_CUBEMAP_RENDER_ON
            // - UNITY_SINGLE_PASS_STEREO
            // we by default strip these keywords since most users are not using NiloToon for XR.
            // XR user should enable these keyword manually
            // https://docs.unity3d.com/6000.2/Documentation/Manual/shader-keywords-default.html
            // Check if building for any XR platform
            bool isXREnabled = false; // IsXRBuildTarget(); // WIP
            
            // Only strip VR/XR keywords if XR is NOT enabled
            if (!isXREnabled)
            {
            if (!targetResultSetting.include_STEREO_INSTANCING_ON)
            {
                haveToStripList.Add(new ShaderKeyword(shader, "STEREO_INSTANCING_ON")); // safe to strip
            }
            if (!targetResultSetting.include_STEREO_MULTIVIEW_ON)
            {
                haveToStripList.Add(new ShaderKeyword(shader, "STEREO_MULTIVIEW_ON")); // safe to strip
            }
            if (!targetResultSetting.include_STEREO_CUBEMAP_RENDER_ON)
            {
                haveToStripList.Add(new ShaderKeyword(shader, "STEREO_CUBEMAP_RENDER_ON")); // safe to strip
            }
            if (!targetResultSetting.include_UNITY_SINGLE_PASS_STEREO)
            {
                haveToStripList.Add(new ShaderKeyword(shader, "UNITY_SINGLE_PASS_STEREO")); // safe to strip
            }
                // Other possible XR keyword to strip:
                // "_XR_OCCLUSION_MESH"
                // "UNITY_STEREO_INSTANCING_ENABLED"
            }

            //haveToStripList.Add(new ShaderKeyword(shader, "_RECEIVE_URP_SHADOW")); // confirm not safe to strip a local material keyword
            //haveToStripList.Add(new ShaderKeyword(shader, "_SCREENSPACE_OUTLINE")); // confirm not safe to strip a local material keyword
            
            // [URP multi_compile]
            //haveToStripList.Add(new ShaderKeyword(shader, "_MAIN_LIGHT_SHADOWS_CASCADE")); // safe to strip?
            //haveToStripList.Add(new ShaderKeyword(shader, "...")); // there are a lot of other keyword
            //----------------------------------------------------------------------------------------------------------------------------------
            return haveToStripList;
        }

        private static NiloToonShaderStrippingSettingSO.Settings GetNiloToonStrippingTargetResult()
        {
            // we are going to fill in targetResultSetting by a correct setting of current platform
            NiloToonShaderStrippingSettingSO.Settings targetResultSetting;

            NiloToonShaderStrippingSettingSO perPlatformUserStrippingSetting;
            
            // (1) first try to get Scriptable Object(SO) from project settings
            perPlatformUserStrippingSetting = NiloToonProjectSettings.Instance.shaderStrippingSetting;
                
            // (2) if null, get Scriptable Object(SO) from active forward renderer's NiloToonAllInOneRendererFeature's shaderStrippingSettingSO slot
            if (perPlatformUserStrippingSetting == null)
            {
                perPlatformUserStrippingSetting = NiloToonAllInOneRendererFeature.Instance?.settings?.shaderStrippingSettingSO; // NiloToonAllInOneRendererFeature.Instance is null when build using console command
            }

            // (3) if we still can't get any SO (user didn't assign it in project settings and active forward renderer's NiloToonAllInOneRendererFeature's shaderStrippingSettingSO slot)
            // spawn a temp SO for this function only, which contains default values
            if (perPlatformUserStrippingSetting == null)
            {
                perPlatformUserStrippingSetting = ScriptableObject.CreateInstance<NiloToonShaderStrippingSettingSO>();
            }

            // assign default setting first
            targetResultSetting = perPlatformUserStrippingSetting.DefaultSettings;

            // then assign per platform overrides
#if UNITY_ANDROID
            if (perPlatformUserStrippingSetting.ShouldOverrideSettingForAndroid)
                targetResultSetting = perPlatformUserStrippingSetting.AndroidSettings;
#endif
#if UNITY_IOS
            if(perPlatformUserStrippingSetting.ShouldOverrideSettingForIOS)
                targetResultSetting = perPlatformUserStrippingSetting.iOSSettings;
#endif
#if UNITY_WEBGL
            if(perPlatformUserStrippingSetting.ShouldOverrideSettingForWebGL)
                targetResultSetting = perPlatformUserStrippingSetting.WebGLSettings;
#endif
            return targetResultSetting;
        }
        
        /////////////////////////////////////////////////////////////
        // XR helper functions
        ////////////////////////////////////////////////////////////
        private static bool IsXRBuildTarget()
        {
            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            // Only check for known XR platforms
            if (buildTargetGroup != BuildTargetGroup.Android &&
                buildTargetGroup != BuildTargetGroup.Standalone &&
                buildTargetGroup != BuildTargetGroup.iOS &&
                buildTargetGroup != BuildTargetGroup.WSA &&
                buildTargetGroup != BuildTargetGroup.PS4 &&
                buildTargetGroup != BuildTargetGroup.PS5)
            {
                return false;
            }

            // Try modern XR Plugin Management first
            if (CheckXRPluginManagement(buildTargetGroup))
            {
                return true;
            }

            // Fallback to legacy VR API
            return CheckLegacyVRSupport(buildTargetGroup);
        }

        private static bool CheckXRPluginManagement(BuildTargetGroup buildTargetGroup)
        {
            try
            {
                var xrGeneralSettingsType = System.Type.GetType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management.Editor");
                if (xrGeneralSettingsType == null) return false;

                var method = xrGeneralSettingsType.GetMethod("XRGeneralSettingsForBuildTarget", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (method == null) return false;

                var xrGeneralSettings = method.Invoke(null, new object[] { buildTargetGroup });
                if (xrGeneralSettings == null) return false;

                var managerProperty = xrGeneralSettings.GetType().GetProperty("Manager");
                var manager = managerProperty?.GetValue(xrGeneralSettings);
                if (manager == null) return false;

                var loadersProperty = manager.GetType().GetProperty("activeLoaders");
                var loaders = loadersProperty?.GetValue(manager) as System.Collections.IList;
                
                return loaders != null && loaders.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckLegacyVRSupport(BuildTargetGroup buildTargetGroup)
        {
            // Unity removed legacy VR module in Unity6.4~6.5, so we can't check anymore, the API PlayerSettings.GetVirtualRealitySupported has been removed in Unity6.4~6.5
#if UNITY_6000_4_OR_NEWER
            return false;
#else
            try
            {
        #pragma warning disable CS0618
                return PlayerSettings.GetVirtualRealitySupported(buildTargetGroup);
        #pragma warning restore CS0618
            }
            catch
            {
                return false;
            }
#endif
        }
    }
}
