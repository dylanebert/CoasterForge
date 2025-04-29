using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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
            PropagateAnchors(ref state);
            PropagateInputPorts(ref state);
            PropagateConnections(ref state);
        }

        [BurstCompile]
        private void PropagateAnchors(ref SystemState state) {
            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var nodeEntity in nodes) {
                var node = SystemAPI.GetAspect<NodeAspect>(nodeEntity);
                if (node.Type != NodeType.Anchor || !node.Dirty) continue;

                var outputPort = SystemAPI.GetBuffer<OutputPortReference>(nodeEntity)[0];
                ref Dirty dirty = ref SystemAPI.GetComponentRW<Dirty>(outputPort).ValueRW;

                ref AnchorPort port = ref SystemAPI.GetComponentRW<AnchorPort>(outputPort).ValueRW;
                port.Value = SystemAPI.GetComponent<Anchor>(nodeEntity);

                node.Dirty = false;
                dirty = true;
            }
            nodes.Dispose();
        }

        [BurstCompile]
        private void PropagateInputPorts(ref SystemState state) {
            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var nodeEntity in nodes) {
                var node = SystemAPI.GetAspect<NodeAspect>(nodeEntity);

                for (int i = 0; i < node.InputPorts.Length; i++) {
                    var inputPort = node.InputPorts[i];
                    ref Dirty inputPortDirty = ref SystemAPI.GetComponentRW<Dirty>(inputPort).ValueRW;
                    if (!inputPortDirty) continue;
                    PortType type = SystemAPI.GetComponent<Port>(inputPort);
                    ref Anchor anchor = ref SystemAPI.GetComponentRW<Anchor>(nodeEntity).ValueRW;

                    if (type == PortType.Anchor) {
                        anchor.Value = SystemAPI.GetComponent<AnchorPort>(inputPort);
                    }
                    else if (type == PortType.Duration) {
                        float duration = SystemAPI.GetComponent<DurationPort>(inputPort);
                        ref var durationComponent = ref SystemAPI.GetComponentRW<Duration>(nodeEntity).ValueRW;
                        durationComponent.Value = duration;
                    }
                    else if (type == PortType.Position) {
                        float3 position = SystemAPI.GetComponent<PositionPort>(inputPort);
                        anchor.Value.SetPosition(position);
                    }
                    else if (type == PortType.Roll) {
                        float roll = SystemAPI.GetComponent<RollPort>(inputPort);
                        anchor.Value.SetRoll(roll);
                    }
                    else if (type == PortType.Pitch) {
                        float pitch = SystemAPI.GetComponent<PitchPort>(inputPort);
                        anchor.Value.SetPitch(pitch);
                    }
                    else if (type == PortType.Yaw) {
                        float yaw = SystemAPI.GetComponent<YawPort>(inputPort);
                        anchor.Value.SetYaw(yaw);
                    }
                    else if (type == PortType.Velocity) {
                        float velocity = SystemAPI.GetComponent<VelocityPort>(inputPort);
                        anchor.Value.SetVelocity(velocity);
                    }
                    else {
                        throw new System.NotImplementedException($"Unknown input port type: {type}");
                    }

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

            if (SystemAPI.HasComponent<AnchorPort>(sourcePort) && SystemAPI.HasComponent<AnchorPort>(targetPort)) {
                AnchorPort sourcePointPort = SystemAPI.GetComponent<AnchorPort>(sourcePort);
                ref AnchorPort targetPointPort = ref SystemAPI.GetComponentRW<AnchorPort>(targetPort).ValueRW;
                targetPointPort.Value = sourcePointPort.Value;
            }
            else {
                UnityEngine.Debug.LogWarning("Unknown propagation");
            }

            targetPortDirty = true;
        }
    }
}
