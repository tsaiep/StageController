using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace NiloToon.NiloToonURP
{
    public enum VolumeMode
    {
        Global,
        Local
    }

    [RequireComponent(typeof(Volume))]
    [ExecuteAlways]
    public class NiloToonVolumePresetPicker : MonoBehaviour
    {
        public int _currentIndex = -1;

        [Range(0, 1)]
        public float weight = 1f;
        public VolumeMode mode = VolumeMode.Global;
        public int priority = -1;
        
        public List<VolumeProfile> volumeProfiles = new List<VolumeProfile>();

        private Volume _volume;
        private Volume volume
        {
            get
            {
                if (!_volume)
                {
                    _volume = GetComponent<Volume>();
                }
                
                if (!_volume)
                {
                    _volume = gameObject.AddComponent<Volume>();
                    return _volume;
                }
                
                return _volume;
            }
        }
        public int currentIndex
        {
            get { return _currentIndex; }
            set
            {
                _currentIndex = value;
                AssignVolumeProfileByIndex(_currentIndex);
            }
        }

        public void EnablePrevious()
        {
            AssignVolumeProfileByIndex(currentIndex - 1);
        }

        public void EnableNext()
        {
            AssignVolumeProfileByIndex(currentIndex + 1);
        }

        private void AssignVolumeProfileByIndex(int index)
        {
            // Check if volumeProfiles is null or empty
            if (volumeProfiles == null || volumeProfiles.Count == 0)
            {
                Debug.LogWarning("Volume profiles list is null or empty");
                return;
            }

            // Clamp index to valid range
            if (index >= volumeProfiles.Count) index = 0;
            if (index < 0) index = volumeProfiles.Count - 1;

            // Check if volume component exists
            if (volume == null)
            {
                Debug.LogWarning("Volume component is null");
                return;
            }

            // Check if the profile at index is not null
            if (volumeProfiles[index] == null)
            {
                Debug.LogWarning($"Volume profile at index {index} is null");
                return;
            }

            volume.sharedProfile = volumeProfiles[index];
            volume.weight = weight;
            volume.priority = priority;
            volume.isGlobal = mode == VolumeMode.Global;
    
            _currentIndex = index;
        }

        // Use OnValidate to update changes during edit time
        private void OnValidate()
        {
            // IMPORTANT: Prevent execution during build process to avoid NullReferenceException
            // The Volume component may not be properly initialized during build time,
            // causing volume.priority assignment to fail. This check ensures OnValidate
            // only runs in editor when not building.
#if UNITY_EDITOR
            if (UnityEditor.BuildPipeline.isBuildingPlayer)
                return;
#endif
            AssignVolumeProfileByIndex(_currentIndex);
        }

        private void OnEnable()
        {
            AssignVolumeProfileByIndex(_currentIndex);
        }
    }


}