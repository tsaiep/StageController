using UnityEngine.Playables;

[System.Serializable]
public class SeededMMFVFXBehaviour : PlayableBehaviour
{
    public int seed = 12345;

    [System.NonSerialized] public bool triggered;
}
