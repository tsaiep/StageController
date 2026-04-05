#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

namespace NiloToon.NiloToonURP
{
    public enum OutlineBakeMethod
    {
        Unity,
        NiloToon
    }

    [System.Serializable]
    public class NiloToonProjectSettingsData
    {
        public OutlineBakeMethod outlineBakeMethod = OutlineBakeMethod.Unity;
        public string shaderStrippingSettingGUID = "";
    }

    public class NiloToonProjectSettings : ScriptableObject
    {
        private const string SettingsFileName = "NiloToonProjectSettings.json";

        [SerializeField]
        private OutlineBakeMethod _outlineBakeMethod = OutlineBakeMethod.Unity;

        [SerializeField] 
        private string _shaderStrippingSettingGUID = "";

        public OutlineBakeMethod outlineBakeMethod
        {
            get => _outlineBakeMethod;
            set
            {
                if (_outlineBakeMethod != value)
                {
                    _outlineBakeMethod = value;
                    SaveSettings();
                }
            }
        }

        public NiloToonShaderStrippingSettingSO shaderStrippingSetting
        {
            get
            {
                if (string.IsNullOrEmpty(_shaderStrippingSettingGUID))
                    return null;
                
                string path = AssetDatabase.GUIDToAssetPath(_shaderStrippingSettingGUID);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning($"[NiloToon] Shader stripping setting GUID '{_shaderStrippingSettingGUID}' has no valid asset path!");
                    return null;
                }
                
                var asset = AssetDatabase.LoadAssetAtPath<NiloToonShaderStrippingSettingSO>(path);
                
                if (asset == null)
                {
                    Debug.LogWarning($"[NiloToon] Failed to load shader stripping setting from path: {path}");
                }
                
                return asset;
            }
            set
            {
                string newGUID = "";
                if (value != null)
                {
                    string path = AssetDatabase.GetAssetPath(value);
                    newGUID = AssetDatabase.AssetPathToGUID(path);
                }
                
                if (_shaderStrippingSettingGUID != newGUID)
                {
                    _shaderStrippingSettingGUID = newGUID;
                    SaveSettings();
                }
            }
        }

        private static NiloToonProjectSettings instance;

        public static NiloToonProjectSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = CreateInstance<NiloToonProjectSettings>();
                    instance.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    LoadSettings();
                }
                return instance;
            }
        }

        public static void SaveSettings()
        {
            if (instance == null) return;

            var data = new NiloToonProjectSettingsData
            {
                outlineBakeMethod = instance._outlineBakeMethod,
                shaderStrippingSettingGUID = instance._shaderStrippingSettingGUID
            };

            string json = JsonUtility.ToJson(data, true);
            string path = GetSettingsPath();
            
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            try
            {
                File.WriteAllText(path, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save NiloToon settings: {e.Message}");
            }
        }

        private static void LoadSettings()
        {
            string path = GetSettingsPath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<NiloToonProjectSettingsData>(json);
                    
                    if (data != null)
                    {
                        instance._outlineBakeMethod = data.outlineBakeMethod;
                        instance._shaderStrippingSettingGUID = data.shaderStrippingSettingGUID;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to load NiloToon settings: {e.Message}");
                    SaveSettings();
                }
            }
            else
            {
                SaveSettings();
            }
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(Application.dataPath, "..", "ProjectSettings", SettingsFileName);
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(Instance);
        }
    }
}
#endif