using Unity.Entities;

namespace CoasterForge {
    [System.Serializable]
    public struct FixedVelocity : IComponentData {
        public bool Value;

        public static implicit operator bool(FixedVelocity fixedVelocity) => fixedVelocity.Value;
        public static implicit operator FixedVelocity(bool value) => new() { Value = value };
    }
}
