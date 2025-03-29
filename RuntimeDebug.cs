using UnityEngine;
using UnityEngine.InputSystem;

namespace CoasterForge {
    public class RuntimeDebug : MonoBehaviour {
        public Track Track;

        private void Update() {
            if (Keyboard.current.f1Key.wasPressedThisFrame) {
                var keyframe = Track.Keyframes[^1];
                keyframe.Time += 0.1f;
                Track.Keyframes[^1] = keyframe;
                Track.MarkDirty();
            }
            else if (Keyboard.current.f2Key.wasPressedThisFrame) {
                var keyframe = Track.Keyframes[^1];
                keyframe.Time -= 0.1f;
                Track.Keyframes[^1] = keyframe;
                Track.MarkDirty();
            }
            else if (Keyboard.current.f3Key.isPressed) {
                var keyframe = Track.Keyframes[^1];
                keyframe.NormalForce += 0.1f;
                Track.Keyframes[^1] = keyframe;
                Track.MarkDirty();
            }
            else if (Keyboard.current.f4Key.isPressed) {
                var keyframe = Track.Keyframes[^1];
                keyframe.NormalForce -= 0.1f;
                Track.Keyframes[^1] = keyframe;
                Track.MarkDirty();
            }
            else if (Keyboard.current.f5Key.wasPressedThisFrame) {
                Track.MarkDirty();
            }
        }
    }
}
