using Unity.Entities;

namespace CoasterForge {
    public struct Cart : IComponentData {
        public Entity Section;
        public float Position;
    }
}
