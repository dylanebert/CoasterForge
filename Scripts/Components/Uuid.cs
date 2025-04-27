using Unity.Entities;

namespace CoasterForge {
    public struct Uuid : IComponentData {
        public uint Value;

        public static implicit operator uint(Uuid id) => id.Value;
        public static implicit operator Uuid(uint value) => new() { Value = value };
    }
}
