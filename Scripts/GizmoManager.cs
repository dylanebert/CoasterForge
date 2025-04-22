#if UNITY_EDITOR
using UnityEngine;

namespace CoasterForge {
    public class GizmoManager : MonoBehaviour {
        private void OnDrawGizmos() {
            DrawSectionGizmosSystem.Instance?.Draw();
        }
    }
}
#endif
