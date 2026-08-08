using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UnifiedStageController))]
public class UnifiedStageControllerEditor : Editor
{
    // ── 各 unit 列的摺疊狀態 ──
    private bool   _showBaseSection = true;
    private bool[] _unitFoldouts;

    // ── 全部套用欄位 ──
    private Vector2 _applyAllValue = Vector2.zero;

    public override void OnInspectorGUI()
    {
        // ── 繪製原始 Inspector ──
        DrawDefaultInspector();

        UnifiedStageController ctrl = (UnifiedStageController)target;
        if (ctrl.slmUnits == null || ctrl.slmUnits.Length == 0)
            return;

        int n = ctrl.slmUnits.Length;

        // 確保 foldout 陣列大小正確
        if (_unitFoldouts == null || _unitFoldouts.Length != n)
            _unitFoldouts = new bool[n]; // 預設全部收折

        EditorGUILayout.Space(10);

        // ═══════════════════════════════════════════════════
        //  旋轉基準偏移 區塊
        // ═══════════════════════════════════════════════════
        _showBaseSection = EditorGUILayout.BeginFoldoutHeaderGroup(
            _showBaseSection, "旋轉基準偏移（Rotation Base）");

        if (_showBaseSection)
        {
            EditorGUI.indentLevel++;

            // ── 說明文字 ──
            EditorGUILayout.HelpBox(
                "每盞燈靜止時的旋轉角（±180°）。Custom Track 動畫的 0° 對應此位置。\n" +
                "修改數值將立即更新 Transform；Timeline 播放不影響此數值。",
                MessageType.Info);

            EditorGUILayout.Space(4);

            // ── 全部套用列 ──
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("批次設定", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                _applyAllValue = EditorGUILayout.Vector2Field(
                    new GUIContent("套用數值", "x = Pan Base，y = Tilt Base"),
                    _applyAllValue);

                if (GUILayout.Button("套用到全部", GUILayout.Width(90), GUILayout.Height(18)))
                {
                    var objs = new Object[n];
                    for (int i = 0; i < n; i++) objs[i] = ctrl.slmUnits[i];
                    Undo.RecordObjects(objs, "Apply Rotation Base To All");

                    foreach (var u in ctrl.slmUnits)
                    {
                        if (u == null) continue;
                        u.rotationBase = _applyAllValue;
                        u.ApplyBaseToTransforms();
                        EditorUtility.SetDirty(u);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── 逐燈列表 ──
            for (int i = 0; i < n; i++)
            {
                var unit = ctrl.slmUnits[i];
                if (unit == null)
                {
                    EditorGUILayout.LabelField($"  [{i}]", "（null）");
                    continue;
                }

                // ── 可摺疊列（Foldout，不可巢狀 BeginFoldoutHeaderGroup）──
                string label = $"[{i}]  {unit.gameObject.name}   " +
                               $"Pan={unit.rotationBase.x:F1}°  Tilt={unit.rotationBase.y:F1}°";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    _unitFoldouts[i] = EditorGUILayout.Foldout(_unitFoldouts[i], label, true);

                    if (_unitFoldouts[i])
                    {
                        EditorGUI.indentLevel++;

                        SerializedObject unitSO = new SerializedObject(unit);
                        unitSO.Update();

                        var baseProp = unitSO.FindProperty("rotationBase");

                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(
                            baseProp,
                            new GUIContent("Rotation Base (x=Pan, y=Tilt)"));
                        bool changed = EditorGUI.EndChangeCheck();

                        if (unitSO.ApplyModifiedProperties() && changed)
                        {
                            // 數值改變 → 即時同步 Transform（不依賴 OnValidate 延遲）
                            Undo.RecordObjects(
                                new Object[] { unit.panTransform, unit.tiltTransform },
                                "Set Rotation Base");
                            unit.ApplyBaseToTransforms();
                            EditorUtility.SetDirty(unit);
                        }

                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }


            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}
