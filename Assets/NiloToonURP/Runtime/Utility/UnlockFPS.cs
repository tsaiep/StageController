using UnityEngine;

namespace NiloToon.NiloToonURP.MiscUtil
{
    public class UnlockFPS : MonoBehaviour
    {
        [Range(0,1000)]
        public int targetFPS = 60;
        
        void Start()
        {
            Application.targetFrameRate = targetFPS;
        }
    }
}