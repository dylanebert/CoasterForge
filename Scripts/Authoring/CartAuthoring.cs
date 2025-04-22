using Unity.Entities;
using UnityEngine;

namespace CoasterForge {
    public class CartAuthoring : MonoBehaviour {
        public SectionAuthoring Section;

        private class Baker : Baker<CartAuthoring> {
            public override void Bake(CartAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                if (authoring.Section == null) {
                    Debug.LogWarning("CartAuthoring: Section is null");
                    return;
                }
                var sectionEntity = GetEntity(authoring.Section.gameObject, TransformUsageFlags.None);
                AddComponent(entity, new Cart { Section = sectionEntity, Position = 1f });
            }
        }
    }
}
