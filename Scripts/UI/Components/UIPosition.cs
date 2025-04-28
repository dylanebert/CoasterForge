using Unity.Entities;
using Unity.Mathematics;

namespace CoasterForge.UI {
    [System.Serializable]
    public struct UIPosition : IComponentData {
        public float2 Value;

        public static implicit operator float2(UIPosition position) => new(position.Value.x, position.Value.y);
        public static implicit operator UIPosition(float2 position) => new() { Value = position };
    }
}
