using Unity.Entities;

namespace CoasterForge {
    [System.Serializable]
    public struct Duration : IComponentData {
        public DurationType Type;
        public float Value;

        public static implicit operator float(Duration duration) => duration.Value;
        public static implicit operator Duration(float value) => new() { Value = value };
    }
}
