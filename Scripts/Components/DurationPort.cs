using Unity.Entities;

namespace CoasterForge {
    public struct DurationPort : IComponentData {
        public float Value;

        public static implicit operator float(DurationPort port) => port.Value;
        public static implicit operator DurationPort(float value) => new() { Value = value };
    }
}
