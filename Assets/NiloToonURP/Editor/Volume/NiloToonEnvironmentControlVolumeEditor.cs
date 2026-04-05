using System.Collections.Generic;
using NiloToon.NiloToonURP;
using UnityEditor;
using UnityEditor.Rendering;

namespace NiloToon.NiloToonURP
{
    [CanEditMultipleObjects]
#if UNITY_2022_2_OR_NEWER
    [CustomEditor(typeof(NiloToonEnvironmentControlVolume))]
#else
    [VolumeComponentEditor(typeof(NiloToonEnvironmentControlVolume))]
#endif
    public class NiloToonEnvironmentControlVolumeEditor : NiloToonVolumeComponentEditor<NiloToonEnvironmentControlVolume>
    {
        // Override GetHelpBoxContent to provide specific help box content
        protected override List<string> GetHelpBoxContent()
        {
            List<string> messages = new List<string>();

            messages.Add("- For extra color control of NiloToon_Environment shader\n" +
                         "- Or for debug which material is using NiloToon_Environment shader" );
            messages.Add(NonPostProcess_NotAffectPerformance_Message);
            
            return messages;
        }
    }
}