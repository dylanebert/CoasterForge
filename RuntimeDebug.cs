using UnityEngine;
using UnityEngine.InputSystem;

namespace CoasterForge {
    public class RuntimeDebug : MonoBehaviour {
        public Track Track;

        private void Update() {
            if (Keyboard.current.f5Key.wasPressedThisFrame) {
                Track.MarkDirty();
            }
        }

    }
}
