using Unity.Entities;

namespace CoasterForge {
    public struct PathPort : IBufferElementData { 
        public PointData Value;

        public static implicit operator PointData(PathPort port) => port.Value;
        public static implicit operator PathPort(PointData value) => new() { Value = value };
    }
}
