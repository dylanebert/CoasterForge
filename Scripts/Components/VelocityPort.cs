using Unity.Entities;

namespace CoasterForge {
    public struct VelocityPort : IComponentData {
        public float Value;

        public static implicit operator float(VelocityPort port) => port.Value;
        public static implicit operator VelocityPort(float value) => new() { Value = value };
    }
}
