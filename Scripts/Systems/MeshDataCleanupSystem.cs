using Unity.Entities;
using Unity.Collections;

namespace CoasterForge {
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class MeshDataCleanupSystem : SystemBase {
        protected override void OnUpdate() {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (meshData, entity) in SystemAPI.Query<MeshData>().WithEntityAccess()) {
                if (!EntityManager.Exists(meshData.Entity)) {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
