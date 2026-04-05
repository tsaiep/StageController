using System.Collections.Generic;
using UnityEngine;

public class SwitchObejectCircle : MonoBehaviour
{
    [System.Serializable]
    public class ObjStyleGroup
    {
        public List<GameObject> objects; // 這個髮型對應的所有物件
    }

    public List<ObjStyleGroup> objStyles = new List<ObjStyleGroup>(); // 髮型組列表
    public KeyCode switchKey = KeyCode.Z; // 切換按鍵
    private int currentIndex = 0; // 當前髮型索引

    void Start()
    {
        // 確保所有髮型的物件都關閉，然後啟用第一組物件
        DisableAllStyles();
        if (objStyles.Count > 0 && objStyles[0].objects.Count > 0)
        {
            EnableObjects(objStyles[0].objects);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(switchKey))
        {
            SwitchStyle();
        }
    }

    void SwitchStyle()
    {
        if (objStyles.Count == 0) return;

        // 關閉當前髮型的所有物件
        DisableObjects(objStyles[currentIndex].objects);

        // 更新索引（循環變換）
        currentIndex = (currentIndex + 1) % objStyles.Count;

        // 啟用新的髮型的所有物件
        EnableObjects(objStyles[currentIndex].objects);
    }

    void DisableAllStyles()
    {
        foreach (var style in objStyles)
        {
            DisableObjects(style.objects);
        }
    }

    void DisableObjects(List<GameObject> objects)
    {
        foreach (var obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }

    void EnableObjects(List<GameObject> objects)
    {
        foreach (var obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
    }
}
