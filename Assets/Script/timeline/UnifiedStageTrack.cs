using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.2f, 0.7f, 0.3f)]
[TrackBindingType(typeof(UnifiedStageController))]
[TrackClipType(typeof(UnifiedStageClip))]
[DisplayName("舞台整合控制軌道")]
public class UnifiedStageTrack : TrackAsset
{
    // 核心修正：加入此標籤能讓 Timeline 知道這個軌道包含可錄製的 Clip 屬性
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        // 確保這行存在，它會連結 Mixer 邏輯
        return ScriptPlayable<UnifiedStageMixer>.Create(graph, inputCount);
    }

    // 關鍵：這會強迫 Timeline 在軌道上顯示「紅點錄製按鈕」
    // 它告訴系統我們允許把 Animation Data 儲存在 Clip 裡面
    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        // 這裡不需要寫邏輯，但存在這個 override 會增加系統對錄製的識別
        base.GatherProperties(director, driver);
    }
}