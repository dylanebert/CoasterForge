using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static CoasterForge.Constants;

namespace CoasterForge {
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct CartSystem : ISystem {
        private BufferLookup<Node> _nodeLookup;

        public void OnCreate(ref SystemState state) {
            _nodeLookup = SystemAPI.GetBufferLookup<Node>(true);
        }

        public void OnUpdate(ref SystemState state) {
            _nodeLookup.Update(ref state);

            state.Dependency = new Job {
                NodeLookup = _nodeLookup,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            [ReadOnly]
            public BufferLookup<Node> NodeLookup;

            [ReadOnly]
            public float DeltaTime;

            public void Execute(ref Cart cart, ref LocalTransform transform) {
                var nodes = NodeLookup[cart.Section];

                cart.Position += DeltaTime * HZ;
                if (cart.Position >= nodes.Length - 2) {
                    cart.Position = 1f;
                }

                if (nodes.Length < 3) return;

                cart.Position = math.clamp(cart.Position, 1, nodes.Length - 2);
                int index = (int)math.floor(cart.Position);
                float t = cart.Position - index;

                int frontWheelIndex = GetIndex(ref nodes, index, FRONT_WHEEL_OFFSET);
                int rearWheelIndex = GetIndex(ref nodes, index, REAR_WHEEL_OFFSET);
                float3 frontWheelPosition = GetSmoothPosition(ref nodes, frontWheelIndex, t);
                float3 rearWheelPosition = GetSmoothPosition(ref nodes, rearWheelIndex, t);
                float3 position = math.lerp(frontWheelPosition, rearWheelPosition, 0.5f);

                if (math.any(math.isnan(position))) {
                    UnityEngine.Debug.Log($"{index}: {nodes[frontWheelIndex]}, {nodes[rearWheelIndex]}");
                }

                float3 direction = GetSmoothHeartDirection(ref nodes, index, t);
                float3 lateral = GetSmoothHeartLateral(ref nodes, index, t);
                float3 normal = math.normalize(math.cross(direction, lateral));
                quaternion rotation = quaternion.LookRotation(direction, -normal);

                transform.Position = position;
                transform.Rotation = rotation;
            }

            private int GetIndex(ref DynamicBuffer<Node> nodes, int index, float offset) {
                int currentIndex = index;
                float startLength = nodes[index].TotalLength;
                float targetLength = startLength + offset;
                int direction = offset > 0f ? 1 : -1;
                while (currentIndex > 0 && currentIndex < nodes.Length - 2) {
                    if ((direction > 0 && nodes[currentIndex + 1].TotalLength >= targetLength) ||
                        (direction < 0 && nodes[currentIndex - 1].TotalLength <= targetLength)) {
                        break;
                    }
                    currentIndex += direction;
                }
                return currentIndex;
            }

            private float3 GetSmoothPosition(ref DynamicBuffer<Node> nodes, int index, float t) {
                return math.lerp(
                    nodes[index].GetHeartPosition(HEART),
                    nodes[index + 1].GetHeartPosition(HEART),
                    t
                );
            }

            private float3 GetSmoothHeartDirection(ref DynamicBuffer<Node> nodes, int index, float t) {
                return math.normalize(math.lerp(
                    nodes[index].GetHeartDirection(HEART),
                    nodes[index + 1].GetHeartDirection(HEART),
                    t
                ));
            }

            private float3 GetSmoothHeartLateral(ref DynamicBuffer<Node> nodes, int index, float t) {
                return math.normalize(math.lerp(
                    nodes[index].GetHeartLateral(HEART),
                    nodes[index + 1].GetHeartLateral(HEART),
                    t
                ));
            }
        }
    }
}
