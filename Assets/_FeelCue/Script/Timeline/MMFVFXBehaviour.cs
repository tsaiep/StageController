using UnityEngine.Playables;
using UnityEngine.Scripting.APIUpdating;

[System.Serializable]
[MovedFrom(false, null, null, "SeededMMFVFXBehaviour")]
public class MMFVFXBehaviour : PlayableBehaviour
{
    [System.NonSerialized] public bool triggered;
    [System.NonSerialized] public bool wasActive;
}
