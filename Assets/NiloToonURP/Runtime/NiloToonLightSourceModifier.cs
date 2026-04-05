using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace NiloToon.NiloToonURP
{
    [ExecuteAlways]
    public class NiloToonLightSourceModifier : MonoBehaviour
    {
        internal static List<NiloToonLightSourceModifier> AllNiloToonLightModifiers = new();

        //-----------------------------------------------------------------------------
        [Header("Contribution To Main Light")] 
        [OverrideDisplayName("Color")]
        [Tooltip("Should target lights contribute to character's main light color?\n\n" +
                 "- When it is 1, target lights are main light (default result)\n" +
                 "- When it is 0, target lights are 'Additive / Rim Light' only light\n\n" +
                 "Default: 1")]
        [Revertible]
        public float contributeToMainLightColor = 1;

        [OverrideDisplayName("    Desaturate")]
        [Revertible]
        [Range(0, 1)]
        [Tooltip("Should target lights desaturate its color before contributing to character's main light color?\n\n" +
                 "- When the light color has very high saturation, you can increase the desaturation to make the character's lighting result become more natural\n\n" +
                 "Default: 0")]
        
        public float applyDesaturateWhenContributeToMainLightColor = 0;

        [OverrideDisplayName("    Back Light Occlusion (2D)")]
        [Revertible]
        [Range(0, 1)]
        [Tooltip("When a light is from the back side of the character in camera view, should that light be occluded by the character? (occlude the back light in a 2D way, without using character's normal)\n\n" +
                 "- When it is 0, a back light will completely pass through the character without any occlusion (default result)\n" +
                 "- When it is 1, a back light will be occluded by the character, reducing the main light contribution, hence producing a 'Additive / Rim Light' only lighting\n\n" +
                 "Default: 0")]
        public float backLightOcclusion2DWhenContributeToMainLightColor = 0;
        
        [OverrideDisplayName("    Back Light Occlusion (3D)")]
        [Revertible]
        [Range(0, 1)]
        [Tooltip("When a light is from the back side of the character in camera view, should that light be occluded by the character? (occlude the back light in a 3D way, using character's normal)\n\n" +
                 "- When it is 0, a back light will completely pass through the character without any occlusion (default result)\n" +
                 "- When it is 1, a back light will be occluded by the character, reducing the main light contribution, hence producing a 'Additive / Rim Light' only lighting\n\n" +
                 "Default: 0")]
        public float backLightOcclusion3DWhenContributeToMainLightColor = 0;
        
        [OverrideDisplayName("Direction")]
        [Revertible]
        [Range(0,1)]
        [Tooltip("NiloToon will weighted sum all light's direction and use the average direction as main light's direction.\n\n" +
                 "- When it is 1, target lights will be considered in the light direction weighted sum, hence affecting the result main light direction\n" +
                 "- When it is 0, target lights will not be considered in the light direction weighted sum, hence not affecting the result main light direction. Great for 'color only' lighting\n\n" +
                 "Default: 1")]
        public float contributeToMainLightDirection = 1;
        
        //-----------------------------------------------------------------------------
        [Header("Contribution To Additive/Rim Light")] 
        [OverrideDisplayName("Intensity")]
        [Tooltip("Default: 1")]
        [Revertible]
        public float contributeToAdditiveOrRimLightIntensity = 1;
        
        //-----------------------------------------------------------------------------
        [Header("Rendering Layer (Additional lights)")]
        [Tooltip("When enabled, this light will ignore Rendering Layer settings for NiloToon characters.\n\n" +
                 "Default: false")]
        [Revertible]
        public bool ignoreRenderingLayer = false;

        //-----------------------------------------------------------------------------
        [Header("Light Mask")]
        [HelpBox("- When enabled, this script will only apply to target Unity Lights in the list.\n" +
                 "- When disabled, this script will apply to all Unity Lights.")]
        [Tooltip("Default: true")]
        [OverrideDisplayName("Enabled?")]
        [Revertible]
        public bool enableTargetLightMask = true;
        
        [DisableIf("enableTargetLightMask")]
        [OverrideDisplayName("    Target Lights List")]
        //[Revertible]
        public List<Light> targetLightsMask = new();

        // Preset methods for editor
        public void ApplyDefaultPreset()
        {
            contributeToMainLightColor = 1;
            applyDesaturateWhenContributeToMainLightColor = 0;
            backLightOcclusion2DWhenContributeToMainLightColor = 0;
            backLightOcclusion3DWhenContributeToMainLightColor = 0;
            contributeToMainLightDirection = 1;
            contributeToAdditiveOrRimLightIntensity = 1;
        }

        public void ApplyColorAndRimLightOnlyPreset()
        {
            contributeToMainLightColor = 1;
            contributeToMainLightDirection = 0;
            contributeToAdditiveOrRimLightIntensity = 1;
        }
        
        public void ApplyColorOnlyPreset()
        {
            contributeToMainLightColor = 1;
            contributeToMainLightDirection = 0;
            contributeToAdditiveOrRimLightIntensity = 0;
        }

        public void ApplyRimLightOnlyPreset()
        {
            contributeToMainLightColor = 0;
            contributeToMainLightDirection = 0;
            contributeToAdditiveOrRimLightIntensity = 1;
        }

        public void ApplyNoEffectPreset()
        {
            contributeToMainLightColor = 0;
            contributeToMainLightDirection = 0;
            contributeToAdditiveOrRimLightIntensity = 0;
        }
        
        private void OnEnable()
        {
            // https://docs.unity3d.com/ScriptReference/ExecuteAlways.html
            // If a MonoBehaviour runs Play logic in Play Mode and fails to check if its GameObject is part of the playing world,
            // a Prefab being edited in Prefab Mode may incorrectly trigger logic intended only to be run as part of the game.
            if (!Application.isPlaying || Application.IsPlaying(gameObject))
            {
                AddToGlobalList();
            }
        }

        private void LateUpdate()
        {
            PerFrameAction();
        }

        private void OnDisable()
        {
            RemoveFromGlobalList();
        }

        private void OnDestroy()
        {
            RemoveFromGlobalList();
        }

        private void OnValidate()
        {
            PerFrameAction();
        }

        private void PerFrameAction()
        {
            AutoRefillLightMaskList();
            SafeGuardValues();
        }
        private void SafeGuardValues()
        {
            contributeToMainLightColor = Mathf.Max(0, contributeToMainLightColor);
            contributeToAdditiveOrRimLightIntensity = Mathf.Max(0, contributeToAdditiveOrRimLightIntensity);
        }

        private void AutoRefillLightMaskList()
        {
            // if user add this script on Light's game object or root game object, it is likely that the user want this script to affect that light or that group of child lights only,
            // so NiloToon auto this and save user some time.
            if (targetLightsMask.Count == 0)
            {
                var targetLights = GetComponentsInChildren<Light>();

                foreach (var targetLight in targetLights)
                {
                    if (targetLight)
                    {
                        if (!targetLightsMask.Contains(targetLight))
                            targetLightsMask.Add(targetLight);
                    }
                }
            }
        }

        private void AddToGlobalList()
        {
            // Lazy init
            if (AllNiloToonLightModifiers == null)
                AllNiloToonLightModifiers = new List<NiloToonLightSourceModifier>();

            AllNiloToonLightModifiers.Add(this);
        }

        private void RemoveFromGlobalList()
        {
            AllNiloToonLightModifiers?.Remove(this);
        }

        internal bool AffectsLight(Light inputLight)
        {
            if (!enableTargetLightMask) return true;
            if (targetLightsMask == null) return false;
            
            foreach (var light in targetLightsMask)
            {
                if(!light) continue;
                
                if (light == inputLight) return true;
            }

            return false;
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(NiloToonLightSourceModifier))]
    [CanEditMultipleObjects]
    public class NiloToonLightSourceModifierUI : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
            "This script controls how each Unity light source affects NiloToon character(s)\n\n" +
            "NiloToon combines all active NiloToonLightSourceModifiers for each light source in the background." +
            "The final modifier settings are then applied to the corresponding Unity Light," +
            "influencing the lighting of all NiloToon characters affected by that light.", MessageType.Info);

            // Add preset buttons section
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            
            // Calculate if we should use vertical layout based on inspector width
            float inspectorWidth = EditorGUIUtility.currentViewWidth;
            bool useVerticalLayout = inspectorWidth < 400; // Switch to vertical when less than 400 pixels
            
            // For narrow windows, use 2-column grid layout
            if (useVerticalLayout)
            {
                // First row
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset to Default", GUILayout.Height(25)))
                {
                    ApplyPreset("Default", (modifier) => modifier.ApplyDefaultPreset());
                }
                if (GUILayout.Button("Color+Rim", GUILayout.Height(25)))
                {
                    ApplyPreset("Color+Rim Light Only", (modifier) => modifier.ApplyColorAndRimLightOnlyPreset());
                }
                EditorGUILayout.EndHorizontal();
                
                // Second row
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Color Only", GUILayout.Height(25)))
                {
                    ApplyPreset("Color Only", (modifier) => modifier.ApplyColorOnlyPreset());
                }
                if (GUILayout.Button("Rim Only", GUILayout.Height(25)))
                {
                    ApplyPreset("Rim Light Only", (modifier) => modifier.ApplyRimLightOnlyPreset());
                }
                EditorGUILayout.EndHorizontal();
                
                // Third row
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("No Effect", GUILayout.Height(25)))
                {
                    ApplyPreset("No Effect", (modifier) => modifier.ApplyNoEffectPreset());
                }
                GUILayout.FlexibleSpace(); // Fill remaining space
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // Wide window - show all buttons in one row
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Reset to Default", GUILayout.Height(25)))
                {
                    ApplyPreset("Default", (modifier) => modifier.ApplyDefaultPreset());
                }
                
                if (GUILayout.Button("Color+Rim", GUILayout.Height(25)))
                {
                    ApplyPreset("Color+Rim Light Only", (modifier) => modifier.ApplyColorAndRimLightOnlyPreset());
                }
                
                if (GUILayout.Button("Color Only", GUILayout.Height(25)))
                {
                    ApplyPreset("Color Only", (modifier) => modifier.ApplyColorOnlyPreset());
                }
                
                if (GUILayout.Button("Rim Only", GUILayout.Height(25)))
                {
                    ApplyPreset("Rim Light Only", (modifier) => modifier.ApplyRimLightOnlyPreset());
                }
                
                if (GUILayout.Button("No Effect", GUILayout.Height(25)))
                {
                    ApplyPreset("No Effect", (modifier) => modifier.ApplyNoEffectPreset());
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            // Add a small help box explaining the presets
            EditorGUILayout.HelpBox(
                "• Reset to Default: Color=1, Direction=1, Rim=1\n" +
                "• Color+Rim:           Color=1, Direction=0, Rim=1\n" +
                "• Color Only:           Color=1, Direction=0, Rim=0\n" +
                "• Rim Only:              Color=0, Direction=0, Rim=1\n" +
                "• No Effect:            Color=0, Direction=0, Rim=0", 
                MessageType.None);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
            
            DrawDefaultInspector();
        }
        
        private void ApplyPreset(string presetName, System.Action<NiloToonLightSourceModifier> applyAction)
        {
            Undo.RecordObjects(targets, $"Apply {presetName} Preset");
            foreach (var t in targets)
            {
                var modifier = t as NiloToonLightSourceModifier;
                if (modifier != null)
                {
                    applyAction(modifier);
                    EditorUtility.SetDirty(modifier);
                }
            }
        }
    }
#endif  
}