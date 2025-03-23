using UnityEditor;
using UnityEngine;

namespace CoasterForge.Editor {
    [CustomEditor(typeof(TrackMesh))]
    public class TrackMeshEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            if (GUILayout.Button("Build")) {
                var trackMesh = target as TrackMesh;
                trackMesh.Build();
            }
        }
    }
}
