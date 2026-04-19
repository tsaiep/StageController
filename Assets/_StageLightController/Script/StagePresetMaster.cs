using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class StagePresetMaster : MonoBehaviour
{
    [System.Serializable]
    public class LightGroupData
    {
        public string groupName = "New Light Group";
        public StageLightArranger arranger;
        public bool isExpanded = true;
    }

    [Header("Light Groups")]
    public List<LightGroupData> lightGroups = new List<LightGroupData>();

    // Legacy sync and locking logic has been removed.
}
