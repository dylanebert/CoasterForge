using UnityEditor;
using UnityEngine;

namespace CoasterForge.Editor {
    [CustomEditor(typeof(Section))]
    public class TrackEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawPropertiesExcluding(serializedObject,
                "RollSpeedCurveEditor", "NormalForceCurveEditor", "LateralForceCurveEditor"
            );

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Curves");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RollSpeedCurveEditor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("NormalForceCurveEditor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("LateralForceCurveEditor"));

            if (GUILayout.Button("Update Editor Curves")) {
                var track = target as Section;
                track.UpdateEditorCurves();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
