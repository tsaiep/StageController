// Add [Revertible] to a field in any MonoBehaviour / ScriptableRendererFeature / ScriptableRenderPass script,
// then a revert button will show if the current value is not the default value

// * Only works for basic field type like float & bool, will not work for:
// - reference type field
// - enum field

// * You must place the [Revertible] ABOVE:
// - unity attributes like [Range(0,1)]
// - Nilo attributes like [RangeOverrideDisplayName("name",0,1)], [ColorUsageOverrideDisplayName("color", false, true)]
//--------------------------------------------------------
// This is correct place for [Revertible]

//[Revertible]
//[RangeOverrideDisplayName("     A", 0, 1)]
//public float settingA = 1;

//[Revertible]
//[Range(0, 1)]
//public float settingA = 1;
//--------------------------------------------------------
// This is wrong place for [Revertible]

//[RangeOverrideDisplayName("     A", 0, 1)]
//[Revertible]
//public float settingA = 1;

//[Range(0, 1)]
//[Revertible]
//public float settingA = 1;
//--------------------------------------------------------
using UnityEngine;

[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
public class RevertibleAttribute : PropertyAttribute
{
    public RevertibleAttribute()
    {
    }
}