using Unity.Entities;

namespace CoasterForge {
    public struct LateralForceKeyframe : IBufferElementData {
        public Keyframe Value;

        public static implicit operator Keyframe(LateralForceKeyframe keyframe) => keyframe.Value;
        public static implicit operator LateralForceKeyframe(Keyframe keyframe) => new() { Value = keyframe };
    }
}
