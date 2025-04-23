using Unity.Entities;

namespace CoasterForge {
    public struct PointPort : IComponentData {
        public PointData Value;

        public static implicit operator PointData(PointPort port) => port.Value;
        public static implicit operator PointPort(PointData value) => new() { Value = value };
    }
}
