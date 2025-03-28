using UnityEditor;
using UnityEngine;

namespace CoasterForge.Editor {
    [CustomEditor(typeof(Track))]
    public class TrackEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawPropertiesExcluding(serializedObject,
                "NormalForceCurve", "LateralForceCurve", "RollSpeedCurve",
                "NormalForceCurveEditor", "LateralForceCurveEditor", "RollSpeedCurveEditor"
            );

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("NormalForceCurve"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("LateralForceCurve"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RollSpeedCurve"));
            if (EditorGUI.EndChangeCheck()) {
                var track = target as Track;
                track.MarkDirty();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Curves");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("NormalForceCurveEditor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("LateralForceCurveEditor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RollSpeedCurveEditor"));

            if (GUILayout.Button("Update Editor Curves")) {
                var track = target as Track;
                track.UpdateEditorCurves();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
