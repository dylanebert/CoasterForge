using UnityEngine;
using Unity.Entities;

namespace CoasterForge {
    public class SectionMeshDataAuthoring : MonoBehaviour {
        public SectionAuthoring Section;

        private class Baker : Baker<SectionMeshDataAuthoring> {
            public override void Bake(SectionMeshDataAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                if (authoring.Section == null) {
                    Debug.LogWarning("SectionMeshDataAuthoring: Section is null");
                    return;
                }

                var sectionEntity = GetEntity(authoring.Section, TransformUsageFlags.Dynamic);
                AddComponentObject(entity, new SectionMeshData {
                    Section = sectionEntity
                });
            }
        }
    }
}
