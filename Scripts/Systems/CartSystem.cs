using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static CoasterForge.Constants;

namespace CoasterForge {
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct CartSystem : ISystem {
        private BufferLookup<OutputPortReference> _outputPortLookup;
        private BufferLookup<Point> _pointLookup;

        private EntityQuery _connectionQuery;
        private EntityQuery _nodeQuery;

        public void OnCreate(ref SystemState state) {
            _outputPortLookup = SystemAPI.GetBufferLookup<OutputPortReference>(true);
            _pointLookup = SystemAPI.GetBufferLookup<Point>(true);

            _connectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Connection>()
                .Build(state.EntityManager);
            _nodeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<NodeAspect>()
                .Build(state.EntityManager);
        }

        public void OnUpdate(ref SystemState state) {
            _outputPortLookup.Update(ref state);
            _pointLookup.Update(ref state);

            var connections = _connectionQuery.ToComponentDataArray<Connection>(Allocator.Temp);
            var connectionMap = new NativeParallelMultiHashMap<Entity, Entity>(connections.Length * 8, Allocator.TempJob);
            foreach (var connection in connections) {
                connectionMap.Add(connection.SourcePort, connection.TargetPort);
            }
            connections.Dispose();

            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            var nodeMap = new NativeHashMap<Entity, Entity>(nodes.Length, Allocator.TempJob);
            foreach (var nodeEntity in nodes) {
                var node = SystemAPI.GetAspect<NodeAspect>(nodeEntity);
                foreach (var inputPort in node.InputPorts) {
                    nodeMap.Add(inputPort, nodeEntity);
                }
            }
            nodes.Dispose();

            state.Dependency = new Job {
                ConnectionMap = connectionMap,
                NodeMap = nodeMap,
                OutputPortLookup = _outputPortLookup,
                PointLookup = _pointLookup,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = connectionMap.Dispose(state.Dependency);
            state.Dependency = nodeMap.Dispose(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            [ReadOnly]
            public NativeParallelMultiHashMap<Entity, Entity> ConnectionMap;

            [ReadOnly]
            public NativeHashMap<Entity, Entity> NodeMap;

            [ReadOnly]
            public BufferLookup<OutputPortReference> OutputPortLookup;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            [ReadOnly]
            public float DeltaTime;

            public void Execute(ref Cart cart, ref LocalTransform transform) {
                var points = PointLookup[cart.Section];
                if (points.Length < 2) return;

                cart.Position += DeltaTime * HZ;
                if (cart.Position > points.Length - 1) {
                    if (GetNextSection(cart.Section, out Entity nextSection)) {
                        float overshoot = cart.Position - (points.Length - 1);
                        cart.Section = nextSection;
                        cart.Position = overshoot;
                        points = PointLookup[cart.Section];
                    }
                    else {
                        cart.Section = cart.Root;
                        cart.Position = 1f;
                        points = PointLookup[cart.Section];
                    }
                }

                if (points.Length < 2) return;

                cart.Position = math.clamp(cart.Position, 0f, points.Length - 1f);
                int index = (int)math.floor(cart.Position);
                float t = cart.Position - index;

                if (index >= points.Length - 1) {
                    index = points.Length - 2;
                    t = 1f;
                }

                float3 position = GetSmoothPosition(ref points, index, t);
                float3 direction = GetSmoothHeartDirection(ref points, index, t);
                float3 lateral = GetSmoothHeartLateral(ref points, index, t);
                float3 normal = math.normalize(math.cross(direction, lateral));
                quaternion rotation = quaternion.LookRotation(direction, -normal);

                transform.Position = position;
                transform.Rotation = rotation;
            }

            private bool GetNextSection(Entity section, out Entity nextSection) {
                nextSection = Entity.Null;

                var outputPortBuffer = OutputPortLookup[section];
                if (outputPortBuffer.Length != 1) {
                    throw new System.NotImplementedException("Section has multiple output ports");
                }

                var outputPort = outputPortBuffer[0];
                var connections = ConnectionMap.GetValuesForKey(outputPort.Value);
                foreach (var connection in connections) {
                    nextSection = NodeMap[connection];
                    return true;
                }

                return false;
            }

            private float3 GetSmoothPosition(ref DynamicBuffer<Point> points, int index, float t) {
                return math.lerp(
                    points[index].Value.GetHeartPosition(HEART),
                    points[index + 1].Value.GetHeartPosition(HEART),
                    t
                );
            }

            private float3 GetSmoothHeartDirection(ref DynamicBuffer<Point> points, int index, float t) {
                return math.normalize(math.lerp(
                    points[index].Value.GetHeartDirection(HEART),
                    points[index + 1].Value.GetHeartDirection(HEART),
                    t
                ));
            }

            private float3 GetSmoothHeartLateral(ref DynamicBuffer<Point> points, int index, float t) {
                return math.normalize(math.lerp(
                    points[index].Value.GetHeartLateral(HEART),
                    points[index + 1].Value.GetHeartLateral(HEART),
                    t
                ));
            }
        }
    }
}
