using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static CoasterForge.Constants;

namespace CoasterForge {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildCopyPathSystem : ISystem {
        private ComponentLookup<AnchorPort> _anchorPortLookup;
        private BufferLookup<PathPort> _pathPortLookup;

        public void OnCreate(ref SystemState state) {
            _anchorPortLookup = SystemAPI.GetComponentLookup<AnchorPort>(true);
            _pathPortLookup = SystemAPI.GetBufferLookup<PathPort>(true);
        }

        public void OnUpdate(ref SystemState state) {
            _anchorPortLookup.Update(ref state);
            _pathPortLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            new Job {
                Ecb = ecb.AsParallelWriter(),
                AnchorPortLookup = _anchorPortLookup,
                PathPortLookup = _pathPortLookup,
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public ComponentLookup<AnchorPort> AnchorPortLookup;

            [ReadOnly]
            public BufferLookup<PathPort> PathPortLookup;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, CopyPathAspect section) {
                if (!section.Dirty) return;

                if (section.InputPorts.Length < 2
                    || !PathPortLookup.TryGetBuffer(section.InputPorts[1], out var pathBuffer)) {
                    UnityEngine.Debug.LogError("BuildCopyPathSystem: No path port found");
                    return;
                }

                if (pathBuffer.Length < 2) return;

                section.Points.Clear();
                section.Points.Add(section.Anchor);

                PointData pathStart = pathBuffer[0];
                PointData anchor = section.Anchor;

                float3x3 pathBasis = new(pathStart.Lateral, pathStart.Normal, pathStart.Direction);
                float3x3 anchorBasis = new(anchor.Lateral, anchor.Normal, anchor.Direction);
                float3x3 rotation = math.mul(anchorBasis, math.transpose(pathBasis));
                float3 translation = anchor.Position - math.mul(rotation, pathStart.Position);
                float4x4 transform = new(
                    new float4(rotation.c0, 0f),
                    new float4(rotation.c1, 0f),
                    new float4(rotation.c2, 0f),
                    new float4(translation, 1f)
                );

                float distance = pathBuffer[0].Value.TotalLength;
                float endDistance = pathBuffer[^1].Value.TotalLength;
                int index = 0;
                int iter = 0;
                while (distance < endDistance) {
                    if (iter++ > 10000) {
                        UnityEngine.Debug.LogError("BuildCopyPathSystem: Exceeded max iterations");
                        break;
                    }

                    PointData prev = section.Points[^1];
                    PointData curr = prev;

                    UpdateCopyPoint(section, ref curr, prev, ref pathBuffer, ref index, ref distance, transform, rotation);

                    section.Points.Add(curr);

                    if (curr.Velocity < EPSILON) {
                        UnityEngine.Debug.LogWarning("Velocity is too low");
                        break;
                    }
                }

                if (section.OutputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(section.OutputPorts[0], out var anchorPort)) {
                    anchorPort.Value = section.Points[^1].Value;
                    Ecb.SetComponent(chunkIndex, section.OutputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildCopyPathSystem: No anchor port found");
                }

                foreach (var port in section.OutputPorts) {
                    Ecb.SetComponent<Dirty>(chunkIndex, port, true);
                }

                section.Dirty = false;
            }

            private void UpdateCopyPoint(
                CopyPathAspect section,
                ref PointData curr,
                in PointData prev,
                ref DynamicBuffer<PathPort> pathBuffer,
                ref int index,
                ref float distance,
                float4x4 transform,
                float3x3 rotation
            ) {
                float desiredDistance = distance + prev.Velocity / HZ;
                var (start, end, t) = Project(ref pathBuffer, ref index, desiredDistance);

                if (t == -1f) {
                    curr.Position = end.Position;
                    curr.Direction = end.Direction;
                    curr.Lateral = end.Lateral;
                    curr.Normal = end.Normal;
                    distance = pathBuffer[^1].Value.TotalLength;
                }
                else {
                    curr.Position = math.lerp(start.Position, end.Position, t);
                    curr.Direction = math.normalize(math.lerp(start.Direction, end.Direction, t));
                    float roll = math.lerp(start.Roll, end.Roll, t);
                    float yaw = curr.GetYaw();

                    curr.Lateral = math.mul(quaternion.Euler(0f, math.radians(yaw), 0f), math.right());

                    quaternion rollQuat = quaternion.AxisAngle(curr.Direction, math.radians(-roll));
                    curr.Lateral = math.normalize(math.mul(rollQuat, curr.Lateral));
                }

                curr.Position = math.transform(transform, curr.Position);
                curr.Direction = math.mul(rotation, curr.Direction);
                curr.Lateral = math.mul(rotation, curr.Lateral);
                curr.Normal = math.normalize(math.cross(curr.Direction, curr.Lateral));

                // Compute point metrics
                curr.DistanceFromLast = math.distance(curr.GetHeartPosition(HEART), prev.GetHeartPosition(HEART));
                curr.TotalLength += curr.DistanceFromLast;
                distance += curr.DistanceFromLast;
                curr.HeartDistanceFromLast = math.distance(curr.Position, prev.Position);
                curr.TotalHeartLength += curr.HeartDistanceFromLast;

                // Compute orientation changes
                float3 diff = curr.Direction - prev.Direction;
                if (math.length(diff) < EPSILON) {
                    curr.PitchFromLast = 0f;
                    curr.YawFromLast = 0f;
                }
                else {
                    curr.PitchFromLast = (curr.GetPitch() - prev.GetPitch() + 540) % 360 - 180;
                    curr.YawFromLast = (curr.GetYaw() - prev.GetYaw() + 540) % 360 - 180;
                }
                float yawScaleFactor = math.cos(math.abs(math.radians(curr.GetPitch())));
                curr.AngleFromLast = math.sqrt(yawScaleFactor * yawScaleFactor * curr.YawFromLast * curr.YawFromLast + curr.PitchFromLast * curr.PitchFromLast);

                // Update energy and velocity
                float pe = G * (curr.GetHeartPosition(CENTER).y + curr.TotalLength * FRICTION);
                curr.Energy -= curr.Velocity * curr.Velocity * curr.Velocity * RESISTANCE / HZ;
                curr.Velocity = math.sqrt(2f * math.max(0, curr.Energy - pe));

                // Compute actual forces
                float3 forceVec;
                if (math.abs(curr.AngleFromLast) < EPSILON) {
                    forceVec = math.up();
                }
                else {
                    float cosRoll = math.cos(math.radians(curr.Roll));
                    float sinRoll = math.sin(math.radians(curr.Roll));
                    float normalAngle = math.radians(-curr.PitchFromLast * cosRoll
                        - yawScaleFactor * curr.YawFromLast * sinRoll);
                    float lateralAngle = math.radians(curr.PitchFromLast * sinRoll
                        - yawScaleFactor * curr.YawFromLast * cosRoll);
                    forceVec = math.up()
                        + lateralAngle * curr.Lateral * curr.Velocity * HZ / G
                        + normalAngle * curr.Normal * curr.HeartDistanceFromLast * HZ * HZ / G;
                }
                curr.NormalForce = -math.dot(forceVec, curr.Normal);
                curr.LateralForce = -math.dot(forceVec, curr.Lateral);

                if (curr.TieDistance > TIE_SPACING) {
                    curr.TieDistance = 0f;
                }
                else {
                    curr.TieDistance += curr.DistanceFromLast;
                }
            }

            private (PointData, PointData, float) Project(
                ref DynamicBuffer<PathPort> pathBuffer,
                ref int index,
                float distance
            ) {
                PointData start;
                PointData end;

                if (distance >= pathBuffer[^1].Value.TotalLength) {
                    index = pathBuffer.Length - 1;
                    end = pathBuffer[^1].Value;
                    return (end, end, -1f);
                }

                for (int i = index; i < pathBuffer.Length - 1; i++) {
                    if (pathBuffer[i + 1].Value.TotalLength >= distance) {
                        index = i;
                        break;
                    }
                }

                start = pathBuffer[index].Value;
                end = pathBuffer[index + 1].Value;

                float t = (distance - start.TotalLength) / (end.TotalLength - start.TotalLength);
                t = math.saturate(t);

                return (start, end, t);
            }
        }
    }
}
