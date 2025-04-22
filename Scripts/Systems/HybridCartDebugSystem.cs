#if UNITY_EDITOR
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CoasterForge {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class HybridCartDebugSystem : SystemBase {
        private EntityQuery _query;

        protected override void OnCreate() {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Cart, LocalTransform>()
                .WithNone<HybridTransform>()
                .Build(EntityManager);

            RequireForUpdate(_query);
        }

        protected override void OnUpdate() {
            var carts = _query.ToEntityArray(Allocator.Temp);
            if (carts.Length > 1) {
                Debug.LogError("HybridTransformSystem: Multiple carts found");
            }

            var cart = carts[0];
            var cartGO = GameObject.FindGameObjectWithTag("Cart");

            if (cartGO == null) {
                Debug.LogError("HybridTransformSystem: Cart not found");
                return;
            }

            EntityManager.AddComponentObject(cart, new HybridTransform { Value = cartGO.transform });
        }
    }
}
#endif
