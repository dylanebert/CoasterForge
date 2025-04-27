using Unity.Entities;
using UnityEngine;

namespace CoasterForge {
    public class CartAuthoring : MonoBehaviour {
        public GameObject Section;

        private class Baker : Baker<CartAuthoring> {
            public override void Bake(CartAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                if (authoring.Section == null) {
                    Debug.LogWarning("CartAuthoring: Section is null");
                    return;
                }
                var sectionEntity = GetEntity(authoring.Section, TransformUsageFlags.None);
                AddComponent(entity, new Cart {
                    Root = sectionEntity,
                    Section = sectionEntity,
                    Position = 1f
                });
            }
        }
    }
}
