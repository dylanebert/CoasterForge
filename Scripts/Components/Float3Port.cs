using Unity.Entities;
using Unity.Mathematics;

namespace CoasterForge {
    public struct Float3Port : IComponentData {
        public float3 Value;

        public static implicit operator float3(Float3Port port) => port.Value;
        public static implicit operator Float3Port(float3 value) => new() { Value = value };
    }
}
