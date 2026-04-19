using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "StagePresetLibrary", menuName = "Stage Control/Stage Preset Library")]
public class StagePresetLibrary : ScriptableObject
{
    [System.Serializable]
    public class PresetEntry
    {
        public string presetName; // Template key, for example IdolCute or ClimaxRed.
        public GameObject prefab; // Prefab that contains a StagePresetMaster component.
    }

    public List<PresetEntry> presets = new List<PresetEntry>();

    public GameObject GetPrefab(string presetName)
    {
        var entry = presets.Find(x => x.presetName == presetName);
        return entry?.prefab;
    }
}
