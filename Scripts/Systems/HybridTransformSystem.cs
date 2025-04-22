#if UNITY_EDITOR
using Unity.Entities;
using Unity.Transforms;

namespace CoasterForge {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class HybridTransformSystem : SystemBase {
        protected override void OnUpdate() {
            foreach (var (transform, hybridTransform) in SystemAPI.Query<LocalTransform, HybridTransform>()) {
                hybridTransform.Value.position = transform.Position;
                hybridTransform.Value.rotation = transform.Rotation;
            }
        }
    }
}
#endif
