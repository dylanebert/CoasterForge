using Unity.Entities;
using UnityEngine;

namespace CoasterForge {
    public class HybridTransform : IComponentData {
        public Transform Value;

        public static implicit operator Transform(HybridTransform transform) => transform.Value;
        public static implicit operator HybridTransform(Transform transform) => new() { Value = transform };
    }
}
