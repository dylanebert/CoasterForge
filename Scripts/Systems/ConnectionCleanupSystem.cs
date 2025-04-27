using Unity.Entities;
using Unity.Collections;

namespace CoasterForge {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ConnectionCleanupSystem : SystemBase {
        protected override void OnUpdate() {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (connection, entity) in SystemAPI.Query<Connection>().WithEntityAccess()) {
                if (!EntityManager.Exists(connection.SourcePort) || !EntityManager.Exists(connection.TargetPort)) {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
