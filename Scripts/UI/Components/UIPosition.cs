using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CoasterForge.UI {
    [System.Serializable]
    public struct UIPosition : IComponentData {
        public float2 Value;

        public static implicit operator Vector2(UIPosition position) => new(position.Value.x, position.Value.y);
        public static implicit operator UIPosition(Vector2 position) => new() { Value = new float2(position.x, position.y) };
    }
}
