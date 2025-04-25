using Unity.Entities;

namespace CoasterForge {
    [System.Serializable]
    public struct Section : IComponentData {
        public DurationType DurationType;
        public float Duration;
        public bool FixedVelocity;
    }
}
