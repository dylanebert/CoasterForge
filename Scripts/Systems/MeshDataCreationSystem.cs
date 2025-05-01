using Unity.Entities;
using Unity.Collections;

namespace CoasterForge {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MeshDataCreationSystem : SystemBase {
        private EntityQuery _query;

        protected override void OnCreate() {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Point>()
                .WithNone<HasMeshDataTag>()
                .Build(EntityManager);

            RequireForUpdate(_query);
        }

        protected override void OnUpdate() {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var sections = _query.ToEntityArray(Allocator.Temp);
            foreach (var section in sections) {
                var meshDataEntity = ecb.CreateEntity();
                ecb.AddComponent(meshDataEntity, new MeshData {
                    Entity = section
                });
                ecb.AddComponent<HasMeshDataTag>(section);
                ecb.SetName(meshDataEntity, "MeshData");
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
