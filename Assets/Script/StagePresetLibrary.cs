using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "箇砞籖家狾畐", menuName = "縊北/箇砞籖家狾畐")]
public class StagePresetLibrary : ScriptableObject
{
    [System.Serializable]
    public class PresetEntry
    {
        public string presetName;     // 家狾嘿 (: IdolCute, ClimaxRed)
        public GameObject prefab;     // 癸莱本Τ StagePresetMaster  Prefab
    }

    public List<PresetEntry> presets = new List<PresetEntry>();

    // е硉琩т Prefab よ猭
    public GameObject GetPrefab(string name)
    {
        var entry = presets.Find(x => x.presetName == name);
        return entry?.prefab;
    }
}