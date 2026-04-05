using System.Collections.Generic;
using UnityEngine;

public class KeyCodeControlBlendShape : MonoBehaviour
{
    public UpdateMethod updateMethod = UpdateMethod.LateUpdate;
    public List<KeyCodeData> keyCodeControlsList = new List<KeyCodeData>();

    public enum UpdateMethod
    {
        Update,
        LateUpdate,
    }

    [System.Serializable]
    public class KeyCodeData
    {
        [Header("Expression Name")]
        public string name;
        
        [Header("Keyboard Activate")]
        public KeyCode keyCode;

        [Header("Lock ARKit")]
        public bool lockEyeARKit = false;
        public bool lockMouthARKit = false;
        public bool lockCheekARKit = false;

        [Header("Defines BlendShapes")]
        public List<SkinnedMeshRendererData> renderers = new List<SkinnedMeshRendererData>();

        // internal
        internal bool isKeyCodePressing;
        internal bool isKeyCodeJustReleased;
    }

    [System.Serializable]
    public class SkinnedMeshRendererData
    {
        public SkinnedMeshRenderer renderer = null;

        [Header("Defines BlendShapes")]
        public List<BlendShapeData> blendShapes = new List<BlendShapeData>();
    }
    [System.Serializable]
    public class BlendShapeData
    {
        public string name = "";
        [Range(0, 100)]
        public float weight = 100;
    }

    static readonly string[] ARKitEyeBanList = new string[] {
        "cheekSquintRight",
        "cheekSquintLeft",
        "eyeBlinkLeft",
        "eyeBlinkRight",
        "eyeLookDownLeft",
        "eyeLookDownRight",
        "eyeLookInLeft",
        "eyeLookInRight",
        "eyeLookOutLeft",
        "eyeLookOutRight",
        "eyeLookUpLeft",
        "eyeLookUpRight",
        "eyeSquintLeft",
        "eyeSquintRight",
        "eyeWideLeft",
        "eyeWideRight",
    };
    static readonly string[] ARKitMouthBanList = new string[] {
        "jawForward",
        "jawLeft",
        "jawOpen",
        "jawRight",
        "mouthClose",
        "mouthDimpleLeft",
        "mouthDimpleRight",
        "mouthFrownRight",
        "mouthFrownLeft",
        "mouthFunnel",
        "mouthLeft",
        "mouthLowerDownLeft",
        "mouthLowerDownRight",
        "mouthPucker",
        "mouthPressLeft",
        "mouthPressRight",
        "mouthRight",
        "mouthRollUpper",
        "mouthRollLower",
        "mouthShrugUpper",
        "mouthShrugLower",
        "mouthSmileLeft",
        "mouthSmileRight",
        "mouthStretchRight",
        "mouthStretchLeft",
        "mouthUpperUpLeft",
        "mouthUpperUpRight",
        "tongueOut",
    };
    static readonly string[] ARKitCheekBanList = new string[] {
        "cheekPuff",
        "noseSneerLeft",
        "noseSneerRight",
    };

    void Update()
    {
        UpdateKeyState();

        if (updateMethod == UpdateMethod.Update)
        {
            Execute();
        }
    }
    void LateUpdate()
    {
        if (updateMethod == UpdateMethod.LateUpdate)
        {
            Execute();
        }
    }

    void UpdateKeyState()
    {
        foreach (var keyCodeData in keyCodeControlsList)
        {
            keyCodeData.isKeyCodePressing = Input.GetKey(keyCodeData.keyCode);
            keyCodeData.isKeyCodeJustReleased = Input.GetKeyUp(keyCodeData.keyCode);
        }
    }
    void Execute()
    {
        foreach (var keyCodeData in keyCodeControlsList)
        {
            if (!keyCodeData.isKeyCodePressing && !keyCodeData.isKeyCodeJustReleased) continue;

            foreach (var rendererData in keyCodeData.renderers)
            {
                // safe check
                if (rendererData == null) continue;
                SkinnedMeshRenderer renderer = rendererData.renderer;
                if (!renderer) continue;

                // lock list
                DisableARKitBlendShape(keyCodeData, rendererData);

                // apply
                foreach (var blendShapeData in rendererData.blendShapes)
                {
                    if (blendShapeData == null) continue;

                    // real work
                    string targetBlendShapeName = blendShapeData.name;
                    int BSindex = renderer.sharedMesh.GetBlendShapeIndex(targetBlendShapeName);
                    if (BSindex == -1)
                    {
                        Debug.LogWarning($"BlendShape ({targetBlendShapeName}) not found, skip execute.", this);
                        continue;
                    }

                    renderer.SetBlendShapeWeight(BSindex, keyCodeData.isKeyCodeJustReleased ? 0 : blendShapeData.weight);
                }
            }
        }
    }


    void BatchLockBlendShapeNames(SkinnedMeshRenderer renderer, string[] banList)
    {
        foreach (var blendShapeName in banList)
        {
            int blendshapeIndex = renderer.sharedMesh.GetBlendShapeIndex(blendShapeName);
            if (blendshapeIndex != -1)
            {
                renderer.SetBlendShapeWeight(blendshapeIndex, 0);
            }
        }
    }

    void DisableARKitBlendShape(KeyCodeData keyCodeData, SkinnedMeshRendererData rendererData)
    {
        if (keyCodeData.lockEyeARKit)
        {
            BatchLockBlendShapeNames(rendererData.renderer, ARKitEyeBanList);
        }
        if (keyCodeData.lockMouthARKit)
        {
            BatchLockBlendShapeNames(rendererData.renderer, ARKitMouthBanList);
        }
        if (keyCodeData.lockCheekARKit)
        {
            BatchLockBlendShapeNames(rendererData.renderer, ARKitCheekBanList);
        }
    }

}
