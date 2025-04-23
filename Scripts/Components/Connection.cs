using Unity.Entities;

namespace CoasterForge {
    public struct Connection : IComponentData {
        public Entity SourcePort;
        public Entity TargetPort;
    }
}
