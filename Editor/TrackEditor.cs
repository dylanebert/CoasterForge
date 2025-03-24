using UnityEditor;

namespace CoasterForge.Editor {
    [CustomEditor(typeof(Track))]
    public class TrackEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawPropertiesExcluding(serializedObject, "NormalForceCurve", "LateralForceCurve", "RollSpeedCurve");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("NormalForceCurve"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("LateralForceCurve"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RollSpeedCurve"));
            if (EditorGUI.EndChangeCheck()) {
                var track = target as Track;
                track.MarkDirty();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
