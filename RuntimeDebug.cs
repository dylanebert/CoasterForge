using UnityEngine;
using UnityEngine.InputSystem;

namespace CoasterForge {
    public class RuntimeDebug : MonoBehaviour {
        public Track Track;

        private void Update() {
            if (Keyboard.current.f1Key.wasPressedThisFrame) {
                Track.Duration += 0.1f;
                Track.MarkDirty();
            }
            else if (Keyboard.current.f2Key.wasPressedThisFrame) {
                Track.Duration -= 0.1f;
                Track.MarkDirty();
            }
            else if (Keyboard.current.f3Key.isPressed) {
                var keyframe = Track.NormalForceCurve[2];
                keyframe.Time += Time.deltaTime;
                Track.NormalForceCurve[2] = keyframe;
                Track.MarkDirty();
            }
            else if (Keyboard.current.f4Key.isPressed) {
                var keyframe = Track.NormalForceCurve[2];
                keyframe.Time -= Time.deltaTime;
                Track.NormalForceCurve[2] = keyframe;
                Track.MarkDirty();
            }
            else if (Keyboard.current.f5Key.wasPressedThisFrame) {
                Track.MarkDirty();
            }
        }
    }
}
