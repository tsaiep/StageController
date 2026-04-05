using System.Collections.Generic;
using NiloToon.NiloToonURP;
using UnityEditor;
using UnityEditor.Rendering;

namespace NiloToon.NiloToonURP
{
    [CanEditMultipleObjects]
#if UNITY_2022_2_OR_NEWER
    [CustomEditor(typeof(NiloToonBloomVolume))]
#else
    [VolumeComponentEditor(typeof(NiloToonBloomVolume))]
#endif
    public class NiloToonBloomVolumeEditor : NiloToonVolumeComponentEditor<NiloToonBloomVolume>
    {
        // Override GetHelpBoxContent to provide specific help box content
        protected override List<string> GetHelpBoxContent()
        {
            List<string> messages = new List<string>();

            messages.Add("- Similar to URP's Bloom but with more controls\n" +
                         "- Great for preventing character become over bloom\n" +
                         "- Allows different setting for Character & non-Character pixels\n" +
                         "- Can supplement or replace URP's Bloom\n" +
                         "* This Bloom is slower than URP's Bloom (GPU), only recommended for PC/Console");
            
            messages.Add(IsPostProcessMessage);
            
            return messages;
        }
    }
}