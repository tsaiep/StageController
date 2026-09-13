using UnityEngine;
using System.Collections.Generic;

// =========================================================================
// 母劇本基礎：統一運鏡資產 (必須獨佔 CameraProfileSO.cs 檔名)
// =========================================================================
public abstract class CameraProfileSO : ScriptableObject
{
    [HideInInspector] public bool scenePoseBaked;

    [Header("--- 分類標籤管理 ---")]
    public List<CameraTagSO> tags = new List<CameraTagSO>();

    [Header("--- 0. Lens 物理特寫 ---")]
    public AnimationCurve fovCurve = AnimationCurve.Linear(0f, 60f, 1f, 60f);
    [Tooltip("最終畫面繞鏡頭 Forward 軸的傾斜角度。舊 Profile 沒有此資料時視為 0。")]
    public AnimationCurve dutchCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
}
