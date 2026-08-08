using UnityEngine;
using System.Collections.Generic;

// =========================================================================
// 母劇本基礎：統一運鏡資產 (必須獨佔 CameraProfileSO.cs 檔名)
// =========================================================================
public abstract class CameraProfileSO : ScriptableObject
{
    [Header("--- 分類標籤管理 ---")]
    public List<CameraTagSO> tags = new List<CameraTagSO>();

    [Header("--- 0. Lens 物理特寫 ---")]
    public AnimationCurve fovCurve = AnimationCurve.Linear(0f, 60f, 1f, 60f);
}