using System.Collections.Generic;
using UnityEngine;

public class SwitchObjectOnOff : MonoBehaviour
{
    [System.Serializable]
    public class KeyObjectGroup
    {
        public KeyCode key;                 // 按鍵
        public List<GameObject> objects;   // 對應的物件清單
    }

    public List<KeyObjectGroup> keyObjectGroups = new List<KeyObjectGroup>();

    void Update()
    {
        foreach (var group in keyObjectGroups)
        {
            if (Input.GetKeyDown(group.key))
            {
                // 切換當前按鍵的物件群組
                ToggleObjects(group.objects);
            }
        }
    }

    // 切換指定的物件清單
    void ToggleObjects(List<GameObject> objects)
    {
        foreach (var obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(!obj.activeSelf);
            }
        }
    }
}
