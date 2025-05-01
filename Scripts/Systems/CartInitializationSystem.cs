using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace CoasterForge {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct CartInitializationSystem : ISystem {
        private EntityQuery _sectionQuery;
        private EntityQuery _cartQuery;

        public void OnCreate(ref SystemState state) {
            _sectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Point>()
                .Build(state.EntityManager);
            _cartQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Cart>()
                .Build(state.EntityManager);

            state.RequireForUpdate(_cartQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var sections = _sectionQuery.ToEntityArray(Allocator.Temp);
            if (sections.Length == 0) {
                sections.Dispose();
                return;
            }

            foreach (var cart in SystemAPI.Query<RefRW<Cart>>()) {
                if (state.EntityManager.Exists(cart.ValueRO.Root)) continue;

                cart.ValueRW.Root = sections[0];
            }

            sections.Dispose();
        }
    }
}
