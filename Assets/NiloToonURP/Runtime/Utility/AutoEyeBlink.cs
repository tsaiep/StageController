using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NiloToon.NiloToonURP.MiscUtil
{
    [Serializable]
    public class RendererBlendshapes
    {
        [SerializeField]
        public SkinnedMeshRenderer renderer;
        
        [SerializeField]
        public List<string> blendshapeNames = new List<string>();
        
        [SerializeField]
        [HideInInspector]
        public List<int> blendshapeIndices = new List<int>();
        
        [SerializeField]
        public List<float> blendshapeMaxValues = new List<float>();

        public RendererBlendshapes()
        {
            blendshapeNames = new List<string>();
            blendshapeIndices = new List<int>();
            blendshapeMaxValues = new List<float>();
        }
    }

    public class AutoEyeBlink : MonoBehaviour
    {
        [SerializeField]
        [Header("Targets")]
        public List<RendererBlendshapes> rendererTargets = new List<RendererBlendshapes>();
        
        [SerializeField]
        [Header("Timing Settings")]
        public float minCoolDown = 1f;
        [SerializeField]
        public float maxCoolDown = 3f;
        [SerializeField]
        public float minCloseAnimTime = 0.12f;
        [SerializeField]
        public float maxCloseAnimTime = 0.3f;
        [SerializeField]
        public float minHoldTime = 0.1f;
        [SerializeField]
        public float maxHoldTime = 0.2f;

        private void OnValidate()
        {
            foreach (var target in rendererTargets)
            {
                if (target.renderer != null && target.renderer.sharedMesh != null)
                {
                    target.blendshapeIndices.Clear();
                    foreach (var name in target.blendshapeNames)
                    {
                        target.blendshapeIndices.Add(target.renderer.sharedMesh.GetBlendShapeIndex(name));
                    }
                }
            }
        }

        private void Start()
        {
            rendererTargets.RemoveAll(target => target.renderer == null || target.blendshapeNames.Count == 0);

            if (rendererTargets.Count > 0)
            {
                StartCoroutine(EyeBlinkLoop());
            }
            else
            {
                Debug.LogWarning("No valid blendshape targets set for AutoEyeBlink");
            }
        }

        private IEnumerator EyeBlinkLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(minCoolDown, maxCoolDown));

                float closeTime = UnityEngine.Random.Range(minCloseAnimTime, maxCloseAnimTime);
                
                yield return StartCoroutine(AnimateBlendshapes(0f, 100f, closeTime * 0.5f));
                yield return new WaitForSeconds(UnityEngine.Random.Range(minHoldTime, maxHoldTime));
                yield return StartCoroutine(AnimateBlendshapes(100f, 0f, closeTime * 0.5f));
            }
        }

        private IEnumerator AnimateBlendshapes(float startValue, float endValue, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float currentValue = Mathf.Lerp(startValue, endValue, normalizedTime);

                foreach (var target in rendererTargets)
                {
                    if (target.renderer == null || target.renderer.sharedMesh == null) continue;

                    for (int i = 0; i < target.blendshapeIndices.Count; i++)
                    {
                        if (target.blendshapeIndices[i] != -1)
                        {
                            float maxValue = (target.blendshapeMaxValues.Count > i) ? target.blendshapeMaxValues[i] : 1f;
                            target.renderer.SetBlendShapeWeight(target.blendshapeIndices[i], currentValue * maxValue);
                        }
                    }
                }

                yield return null;
            }

            foreach (var target in rendererTargets)
            {
                if (target.renderer == null || target.renderer.sharedMesh == null) continue;

                for (int i = 0; i < target.blendshapeIndices.Count; i++)
                {
                    if (target.blendshapeIndices[i] != -1)
                    {
                        float maxValue = (target.blendshapeMaxValues.Count > i) ? target.blendshapeMaxValues[i] : 1f;
                        target.renderer.SetBlendShapeWeight(target.blendshapeIndices[i], endValue * maxValue);
                    }
                }
            }
        }

#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(AutoEyeBlink))]
        public class AutoEyeBlinkEditor : UnityEditor.Editor
        {
            private static class Styles
            {
                public static readonly GUIStyle headerStyle;
                public static readonly GUIStyle sectionStyle;
                public static readonly GUIStyle groupStyle;
                
                static Styles()
                {
                    headerStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
                    headerStyle.fontSize = 14;
                    headerStyle.margin = new RectOffset(4, 4, 12, 8);

                    sectionStyle = new GUIStyle(UnityEngine.GUI.skin.box);
                    sectionStyle.padding = new RectOffset(12, 12, 12, 12);
                    sectionStyle.margin = new RectOffset(0, 0, 8, 8);

                    groupStyle = new GUIStyle(UnityEditor.EditorStyles.helpBox);
                    groupStyle.padding = new RectOffset(8, 8, 8, 8);
                    groupStyle.margin = new RectOffset(0, 0, 4, 4);
                }
            }

            private readonly Dictionary<string, string> tooltips = new Dictionary<string, string>
            {
                { "minCoolDown", "Minimum time to wait between blinks" },
                { "maxCoolDown", "Maximum time to wait between blinks" },
                { "minCloseAnimTime", "Minimum duration of the blink animation" },
                { "maxCloseAnimTime", "Maximum duration of the blink animation" },
                { "minHoldTime", "Minimum time to keep eyes closed" },
                { "maxHoldTime", "Maximum time to keep eyes closed" }
            };

            public override void OnInspectorGUI()
            {
                serializedObject.Update();
                AutoEyeBlink script = (AutoEyeBlink)target;

                // Timing Settings
                UnityEditor.EditorGUILayout.Space(4);
                using (new UnityEditor.EditorGUILayout.VerticalScope(Styles.sectionStyle))
                {
                    UnityEditor.EditorGUILayout.LabelField("Timing Settings", Styles.headerStyle);
                    
                    using (new UnityEditor.EditorGUILayout.VerticalScope(Styles.groupStyle))
                    {
                        DrawFloatField("Min Cool Down", "minCoolDown", 0f, 10f);
                        DrawFloatField("Max Cool Down", "maxCoolDown", 0f, 10f);
                        DrawFloatField("Min Close Anim Time", "minCloseAnimTime", 0f, 1f);
                        DrawFloatField("Max Close Anim Time", "maxCloseAnimTime", 0f, 1f);
                        DrawFloatField("Min Hold Time", "minHoldTime", 0f, 1f);
                        DrawFloatField("Max Hold Time", "maxHoldTime", 0f, 1f);
                    }
                    
                    ValidateTimings(script);
                }

                // Renderer Targets
                UnityEditor.EditorGUILayout.Space(4);
                using (new UnityEditor.EditorGUILayout.VerticalScope(Styles.sectionStyle))
                {
                    UnityEditor.EditorGUILayout.LabelField("Renderer Targets", Styles.headerStyle);
                    
                    SerializedProperty rendererTargetsProperty = serializedObject.FindProperty("rendererTargets");

                    for (int i = 0; i < script.rendererTargets.Count; i++)
                    {
                        using (new UnityEditor.EditorGUILayout.VerticalScope(Styles.groupStyle))
                        {
                            var target = script.rendererTargets[i];
                            SerializedProperty rendererTargetProperty = rendererTargetsProperty.GetArrayElementAtIndex(i);
                            SerializedProperty rendererProperty = rendererTargetProperty.FindPropertyRelative("renderer");

                            var rect = UnityEditor.EditorGUILayout.GetControlRect();
                            UnityEditor.EditorGUI.BeginProperty(rect, new GUIContent("Renderer"), rendererProperty);
                            
                            using (var check = new UnityEditor.EditorGUI.ChangeCheckScope())
                            {
                                var newRenderer = (SkinnedMeshRenderer)UnityEditor.EditorGUI.ObjectField(
                                    rect, "Renderer", target.renderer, typeof(SkinnedMeshRenderer), true);
                                
                                if (check.changed)
                                {
                                    UnityEditor.Undo.RecordObject(script, "Change Renderer");
                                    target.renderer = newRenderer;
                                    target.blendshapeNames.Clear();
                                    target.blendshapeIndices.Clear();
                                    target.blendshapeMaxValues.Clear();
                                    UnityEditor.EditorUtility.SetDirty(script);
                                }
                            }
                            
                            UnityEditor.EditorGUI.EndProperty();

                            if (target.renderer != null && target.renderer.sharedMesh != null)
                            {
                                DrawBlendshapeSection(target, target.renderer.sharedMesh, script, i);
                            }
                            else
                            {
                                UnityEditor.EditorGUILayout.HelpBox("Please assign a SkinnedMeshRenderer with a valid mesh", UnityEditor.MessageType.Info);
                            }

                            if (GUILayout.Button("Remove Renderer", GUILayout.Width(120)))
                            {
                                UnityEditor.Undo.RecordObject(script, "Remove Renderer");
                                script.rendererTargets.RemoveAt(i);
                                UnityEditor.EditorUtility.SetDirty(script);
                                i--;
                            }
                        }
                        UnityEditor.EditorGUILayout.Space(2);
                    }

                    UnityEditor.EditorGUILayout.Space(4);
                    if (GUILayout.Button("Add Renderer", GUILayout.Height(24)))
                    {
                        UnityEditor.Undo.RecordObject(script, "Add Renderer");
                        script.rendererTargets.Add(new RendererBlendshapes());
                        UnityEditor.EditorUtility.SetDirty(script);
                    }
                }

                serializedObject.ApplyModifiedProperties();
            }

            private void DrawFloatField(string label, string propertyName, float min, float max)
            {
                var prop = serializedObject.FindProperty(propertyName);
                var rect = UnityEditor.EditorGUILayout.GetControlRect();
                
                UnityEditor.EditorGUI.BeginProperty(rect, new GUIContent(label, tooltips[propertyName]), prop);
                float newValue = UnityEditor.EditorGUI.Slider(rect, label, prop.floatValue, min, max);
                if (newValue != prop.floatValue)
                {
                    prop.floatValue = newValue;
                }
                UnityEditor.EditorGUI.EndProperty();
            }

            private void ValidateTimings(AutoEyeBlink script)
            {
                if (script.minCoolDown > script.maxCoolDown)
                    UnityEditor.EditorGUILayout.HelpBox("Min Cool Down should be less than Max Cool Down", UnityEditor.MessageType.Warning);
                
                if (script.minCloseAnimTime > script.maxCloseAnimTime)
                    UnityEditor.EditorGUILayout.HelpBox("Min Close Anim Time should be less than Max Close Anim Time", UnityEditor.MessageType.Warning);
                
                if (script.minHoldTime > script.maxHoldTime)
                    UnityEditor.EditorGUILayout.HelpBox("Min Hold Time should be less than Max Hold Time", UnityEditor.MessageType.Warning);
            }

           private void DrawBlendshapeSection(RendererBlendshapes target, UnityEngine.Mesh mesh, AutoEyeBlink script, int targetIndex)
            {
                var blendshapeNames = new List<string>();
                for (int j = 0; j < mesh.blendShapeCount; j++)
                {
                    blendshapeNames.Add(mesh.GetBlendShapeName(j));
                }

                if (blendshapeNames.Count == 0)
                {
                    UnityEditor.EditorGUILayout.HelpBox("This mesh has no blendshapes", UnityEditor.MessageType.Info);
                    return;
                }

                EditorGUILayout.LabelField("Blendshapes", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                
                for (int j = 0; j < target.blendshapeNames.Count; j++)
                {
                    while (target.blendshapeMaxValues.Count <= j)
                        target.blendshapeMaxValues.Add(1f);

                    EditorGUILayout.BeginHorizontal();
                    {
                        // Label
                        EditorGUILayout.LabelField($"Shape {j + 1}", EditorStyles.miniLabel, GUILayout.Width(60));

                        // Dropdown
                        int currentIndex = blendshapeNames.IndexOf(target.blendshapeNames[j]);
                        if (currentIndex == -1) currentIndex = 0;

                        using (var check = new EditorGUI.ChangeCheckScope())
                        {
                            var dropdownStyle = new GUIStyle(EditorStyles.miniPullDown)
                            {
                                fixedWidth = 120,  // Increased width to show full name
                                alignment = TextAnchor.MiddleLeft,
                                clipping = TextClipping.Clip
                            };

                            // Create the popup content with the actual blendshape names
                            int newIndex = EditorGUILayout.Popup(currentIndex, 
                                blendshapeNames.ToArray(), 
                                dropdownStyle);

                            if (check.changed)
                            {
                                Undo.RecordObject(script, "Change Blendshape");
                                target.blendshapeNames[j] = blendshapeNames[newIndex];
                                EditorUtility.SetDirty(script);
                            }
                        }

                        // Slider
                        using (var check = new EditorGUI.ChangeCheckScope())
                        {
                            float newValue = EditorGUILayout.Slider(target.blendshapeMaxValues[j], 0f, 1f, GUILayout.ExpandWidth(true));
                            if (check.changed)
                            {
                                Undo.RecordObject(script, "Change Max Value");
                                target.blendshapeMaxValues[j] = newValue;
                                EditorUtility.SetDirty(script);
                            }
                        }

                        GUILayout.Space(4); // Add small space before the X button

                        // Remove button with dark theme style
                        var buttonStyle = new GUIStyle(EditorStyles.miniButton)
                        {
                            fixedWidth = 18,
                            fixedHeight = 16,
                            padding = new RectOffset(0, 0, 0, 0),
                            margin = new RectOffset(0, 0, 1, 0),
                            alignment = TextAnchor.MiddleCenter,
                            normal = new GUIStyleState
                            {
                                background = EditorGUIUtility.whiteTexture,
                                textColor = new Color(0.7f, 0.7f, 0.7f)
                            }
                        };

                        if (GUILayout.Button("X", buttonStyle))
                        {
                            Undo.RecordObject(script, "Remove Blendshape");
                            target.blendshapeNames.RemoveAt(j);
                            target.blendshapeMaxValues.RemoveAt(j);
                            EditorUtility.SetDirty(script);
                            j--;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);

                // Add Blendshape button
                var addButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    normal = new GUIStyleState { textColor = new Color(0.7f, 0.7f, 0.7f) }
                };

                if (GUILayout.Button("Add Blendshape", addButtonStyle))
                {
                    if (blendshapeNames.Count > 0)
                    {
                        Undo.RecordObject(script, "Add Blendshape");
                        target.blendshapeNames.Add(blendshapeNames[0]);
                        target.blendshapeMaxValues.Add(1f);
                        EditorUtility.SetDirty(script);
                    }
                }
            }
        }
#endif
    }
}