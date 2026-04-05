using System.Collections.Generic;
using UnityEngine;

public class SwitchCamera : MonoBehaviour
{
    public List<GameObject> Cameras;

    void Update()
    {
        for (int i = 0; i < Cameras.Count; i++)
        {
            // 按鍵從 1 開始對應，但索引從 0 開始，因此需要減 1
            if (Input.GetKeyDown((i + 1).ToString()))
            {
                SwitchToCamera(i);
            }
        }
    }

    void SwitchToCamera(int index)
    {
        for (int i = 0; i < Cameras.Count; i++)
        {
            Cameras[i].SetActive(i == index);
        }
    }
}
