using Unity.Entities;

namespace CoasterForge {
    public struct PitchPort : IComponentData {
        public float Value;

        public static implicit operator float(PitchPort port) => port.Value;
        public static implicit operator PitchPort(float value) => new() { Value = value };
    }
}
