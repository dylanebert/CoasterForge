#if UNITY_EDITOR
using Unity.Collections;
using Unity.Entities;

namespace CoasterForge {
    public partial class RebuildDebugSystem : SystemBase {
        public static RebuildDebugSystem Instance { get; private set; }

        public RebuildDebugSystem() {
            Instance = this;
        }

        public void Rebuild() {
            var leaves = new NativeHashSet<Entity>(1024, Allocator.Temp);
            foreach (var connection in SystemAPI.Query<Connection>()) {
                leaves.Add(connection.TargetPort);
            }

            foreach (var inputPortBuffer in SystemAPI.Query<DynamicBuffer<InputPortReference>>()) {
                foreach (var inputPort in inputPortBuffer) {
                    if (leaves.Contains(inputPort)) continue;
                    ref Dirty dirty = ref SystemAPI.GetComponentRW<Dirty>(inputPort).ValueRW;
                    dirty = true;
                }
            }

            leaves.Dispose();
        }

        protected override void OnUpdate() { }
    }
}
#endif
