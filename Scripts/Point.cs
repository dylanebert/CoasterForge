using Unity.Entities;

namespace CoasterForge {
    public struct Point : IBufferElementData {
        public PointData Value;

        public static implicit operator PointData(Point point) => point.Value;
        public static implicit operator Point(PointData data) => new() { Value = data };
    }
}
