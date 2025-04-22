using Unity.Entities;

namespace CoasterForge {
    public struct YawSpeedKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(YawSpeedKeyframe keyframe) => keyframe.Value;
        public static implicit operator YawSpeedKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}
