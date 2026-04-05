using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace NiloToon.NiloToonURP
{
    public class NiloToonProjectSettingsProvider : SettingsProvider
    {
        private SerializedObject settings;
        private const string SettingsPath = "Project/NiloToon";

        public NiloToonProjectSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) {}

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new NiloToonProjectSettingsProvider(SettingsPath, SettingsScope.Project);
            provider.keywords = new HashSet<string>(new[] { "NiloToon", "Model", "Outline", "Bake", "Method", "Shader", "Stripping" });
            return provider;
        }

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            settings = NiloToonProjectSettings.GetSerializedSettings();
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.LabelField("Outline Bake method", EditorStyles.boldLabel);

            // if we don't add this, settings will be null after "revert scene change" clicked
            if (settings == null || settings.targetObject == null)
            {
                settings = NiloToonProjectSettings.GetSerializedSettings();
            }
                
            if (settings == null || settings.targetObject == null)
            {
                EditorGUILayout.HelpBox("NiloToon settings are not available. Please check the console for errors.", MessageType.Error);
                return;
            }

            settings.Update();

            SerializedProperty outlineBakeMethodProp = settings.FindProperty("_outlineBakeMethod");

            if (outlineBakeMethodProp == null)
            {
                EditorGUILayout.HelpBox("NiloToon settings(_outlineBakeMethod) are not available. Please check the console for errors.", MessageType.Error);
                return;
            }

            OutlineBakeMethod currentBakeMethod = (OutlineBakeMethod)outlineBakeMethodProp.enumValueIndex;

            // HelpBox
            string helpBoxMessage;
            bool isParallelImportEnabled = EditorSettings.refreshImportMode == AssetDatabase.RefreshImportMode.OutOfProcessPerQueue;
            if (isParallelImportEnabled)
            {
                helpBoxMessage = "Parallel Import enabled, 'Outline Bake Method' is ignored, 'NiloToon' is used instead.\n" +
                                 "Activated method = 'NiloToon' - faster to reimport, Parallel Import supported. It has a slightly different Classic Outline result than 'Unity'";
            }
            else
            {
                if (currentBakeMethod == OutlineBakeMethod.Unity)
                {
                    helpBoxMessage = "Activated method = 'Unity' - slower to reimport, Parallel Import not supported, will fallback to 'NiloToon' if Parallel Import is enabled. It is the default option and produce the most robust Classic Outline result";
                }
                else
                {
                    helpBoxMessage = "Activated method = 'NiloToon' - faster to reimport, Parallel Import supported. It has a slightly different Classic Outline result than 'Unity'";
                }
            }
            EditorGUILayout.HelpBox(helpBoxMessage, MessageType.Info);

            // Outline Bake Method
            EditorGUILayout.PropertyField(outlineBakeMethodProp, new GUIContent("Outline Bake Method"));

            //--------------------------------------------------------------------------------------------------------------------
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Shader stripping at Build Time", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox("When assigned, NiloToon shader stripping at build time will use this setting.\n- higher priority than NiloToon renderer feature's Shader Stripping Setting\n- Required for build using command line", MessageType.Info);
            EditorGUILayout.HelpBox("If your build doesn't reflect the change in this setting, it is due to shader cache from previous build doesn't invalidate, either:\n- Restart UnityEditor\n- or assign the same setting to the active NiloToon renderer feature's Shader Stripping Setting\nthen build again.", MessageType.Info);
            
            // Shader Stripping Setting - ObjectField
            EditorGUI.BeginChangeCheck();
            var currentSetting = NiloToonProjectSettings.Instance.shaderStrippingSetting;
            var newSetting = (NiloToonShaderStrippingSettingSO)EditorGUILayout.ObjectField(
                "Shader Stripping Setting",
                currentSetting,
                typeof(NiloToonShaderStrippingSettingSO),
                false
            );
            
            if (EditorGUI.EndChangeCheck())
            {
                NiloToonProjectSettings.Instance.shaderStrippingSetting = newSetting;
            }

            if (settings.ApplyModifiedProperties())
            {
                NiloToonProjectSettings.SaveSettings();

                if (currentBakeMethod != NiloToonProjectSettings.Instance.outlineBakeMethod)
                {
                    bool userWantToReimport = EditorUtility.DisplayDialog(
                        "Reimport Assets",
                        "Outline Bake Method changed. Reimport affected assets to ensure Outline data(UV8) is updated correctly?",
                        "Yes",
                        "No"
                    );

                    if (userWantToReimport)
                    {
                        NiloToonEditor_ReimportAllAssetFilteredByLabel.ReFixAll();
                    }
                }
            }
        }
    }
}