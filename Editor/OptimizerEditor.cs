using UnityEditor;
using UnityEngine;

namespace CoasterForge.Editor {
    [CustomEditor(typeof(Optimizer))]
    public class OptimizerEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            if (GUILayout.Button("Log Loss")) {
                var optimizer = target as Optimizer;
                optimizer.LogLoss();
            }
        }
    }
}
