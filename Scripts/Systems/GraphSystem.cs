using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace CoasterForge {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct GraphSystem : ISystem {
        private EntityQuery _nodeQuery;
        private EntityQuery _connectionQuery;

        public void OnCreate(ref SystemState state) {
            _nodeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<NodeAspect>()
                .Build(state.EntityManager);
            _connectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Connection>()
                .Build(state.EntityManager);

            state.RequireForUpdate(_nodeQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            PropagateInputPorts(ref state);
            PropagateConnections(ref state);
        }

        [BurstCompile]
        private void PropagateInputPorts(ref SystemState state) {
            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var nodeEntity in nodes) {
                var node = SystemAPI.GetAspect<NodeAspect>(nodeEntity);

                foreach (var inputPort in node.InputPorts) {
                    ref Dirty inputPortDirty = ref SystemAPI.GetComponentRW<Dirty>(inputPort).ValueRW;
                    if (!inputPortDirty) continue;
                    inputPortDirty = false;
                    node.Dirty = true;
                }
            }

            nodes.Dispose();
        }

        [BurstCompile]
        private void PropagateConnections(ref SystemState state) {
            var connections = _connectionQuery.ToComponentDataArray<Connection>(Allocator.Temp);
            var map = new NativeParallelMultiHashMap<Entity, Entity>(connections.Length, Allocator.Temp);
            foreach (var connection in connections) {
                map.Add(connection.SourcePort, connection.TargetPort);
            }
            connections.Dispose();

            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var nodeEntity in nodes) {
                var node = SystemAPI.GetAspect<NodeAspect>(nodeEntity);
                foreach (var sourcePort in node.OutputPorts) {
                    ref Dirty sourcePortDirty = ref SystemAPI.GetComponentRW<Dirty>(sourcePort).ValueRW;
                    if (!sourcePortDirty || !map.ContainsKey(sourcePort)) continue;

                    foreach (var targetPort in map.GetValuesForKey(sourcePort)) {
                        PropagateConnection(ref state, sourcePort, targetPort);
                    }

                    sourcePortDirty = false;
                }
            }

            map.Dispose();
            nodes.Dispose();
        }

        private void PropagateConnection(ref SystemState state, Entity sourcePort, Entity targetPort) {
            ref Dirty targetPortDirty = ref SystemAPI.GetComponentRW<Dirty>(targetPort).ValueRW;

            if (SystemAPI.HasComponent<PointPort>(sourcePort) && SystemAPI.HasComponent<PointPort>(targetPort)) {
                PointPort sourcePointPort = SystemAPI.GetComponent<PointPort>(sourcePort);
                ref PointPort targetPointPort = ref SystemAPI.GetComponentRW<PointPort>(targetPort).ValueRW;
                targetPointPort.Value = sourcePointPort.Value;
            }
            else {
                UnityEngine.Debug.LogWarning("Unknown propagation");
            }

            targetPortDirty = true;
        }
    }
}
