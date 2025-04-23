using Unity.Entities;

namespace CoasterForge {
    public struct Cart : IComponentData {
        public Entity Root;
        public Entity Section;
        public float Position;
    }
}
