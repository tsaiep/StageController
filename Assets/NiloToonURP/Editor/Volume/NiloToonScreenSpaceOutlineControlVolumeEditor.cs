using System.Collections.Generic;
using NiloToon.NiloToonURP;
using UnityEditor;
using UnityEditor.Rendering;

namespace NiloToon.NiloToonURP
{
    [CanEditMultipleObjects]
#if UNITY_2022_2_OR_NEWER
    [CustomEditor(typeof(NiloToonScreenSpaceOutlineControlVolume))]
#else
    [VolumeComponentEditor(typeof(NiloToonScreenSpaceOutlineControlVolume))]
#endif
    public class NiloToonScreenSpaceOutlineControlEditor : NiloToonVolumeComponentEditor<NiloToonScreenSpaceOutlineControlVolume>
    {
        // Override GetHelpBoxContent to provide specific help box content
        protected override List<string> GetHelpBoxContent()
        {
            List<string> messages = new List<string>();
            
            messages.Add(
                "- For producing NiloToon Character or Environment's screen space outline\n" +
                "- Requires enabling 'Enable ScreenSpace Outline' in NiloToonAllInOne renderer feature\n" +
                "- Requires temporal AA like TAA/STP/DLSS/XeSS/FSR... to produce stable result");
            
            messages.Add(NonPostProcess_MayAffectPerformance_Message);
            
            return messages;
        }
    }
}