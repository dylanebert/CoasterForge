using Unity.Entities;

namespace CoasterForge {
    public struct PitchSpeedKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(PitchSpeedKeyframe keyframe) => keyframe.Value;
        public static implicit operator PitchSpeedKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}
