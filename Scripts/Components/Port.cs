using Unity.Entities;

namespace CoasterForge {
    public struct Port : IComponentData {
        public PortType Value;

        public static implicit operator PortType(Port port) => port.Value;
        public static implicit operator Port(PortType value) => new() { Value = value };
    }
}
