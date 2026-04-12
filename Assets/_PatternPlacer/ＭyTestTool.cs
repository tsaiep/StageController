using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class MyTestTool : MonoBehaviour
{
    
#if UNITY_EDITOR
    public float MovingDistance = 1f;
    public void MyTransformFunc()
    {
        foreach (Transform child in  transform )
        {
            Undo.RecordObject(child, "MyTestTool Move Child");
            child.position += Vector3.up*MovingDistance;
        }
    }

    [CustomEditor(typeof(MyTestTool))]
    public class MyTestToolEditor : Editor
    {
        SerializedProperty MovingDistanceProp;
        void OnEnable()
        {
            MovingDistanceProp = serializedObject.FindProperty("MovingDistance");
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var script = (MyTestTool)target;
            
            EditorGUILayout.PropertyField(MovingDistanceProp, new GUIContent("Moving Distance"));
            serializedObject.ApplyModifiedProperties();
            
            if (GUILayout.Button("Generate", GUILayout.Height(28)))
                script.MyTransformFunc();
        }
    }
    
#endif
}
