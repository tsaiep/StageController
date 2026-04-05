using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class StagePresetMaster : MonoBehaviour
{
    [System.Serializable]
    public class LightGroupData
    {
        public string groupName = "新燈光群組";
        public StageLightArranger arranger;
        public bool isExpanded = true;
    }

    [Header("燈光群組清單")]
    public List<LightGroupData> lightGroups = new List<LightGroupData>();

    // 所有同步與鎖定邏輯已移除
}