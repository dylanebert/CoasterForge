using Unity.Entities;

namespace CoasterForge {
    public struct Anchor : IComponentData {
        public PointData Value;

        public static implicit operator PointData(Anchor anchor) => anchor.Value;
        public static implicit operator Anchor(PointData point) => new() { Value = point };
    }
}
