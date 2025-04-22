using Unity.Entities;

namespace CoasterForge {
    public struct Section : IComponentData {
        public DurationType DurationType;
        public float Duration;
        public bool FixedVelocity;
    }
}
