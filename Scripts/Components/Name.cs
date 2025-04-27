using Unity.Collections;
using Unity.Entities;

namespace CoasterForge {
    [System.Serializable]
    public struct Name : IComponentData {
        public FixedString64Bytes Value;

        public override string ToString() => Value.ToString();

        public static implicit operator FixedString64Bytes(Name name) => name.Value;
        public static implicit operator Name(FixedString64Bytes value) => new() { Value = value };

        public static implicit operator string(Name name) => name.Value.ToString();
        public static implicit operator Name(string value) => new() { Value = value };
    }
}
