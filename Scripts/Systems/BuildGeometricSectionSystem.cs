using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static CoasterForge.Constants;

namespace CoasterForge {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildGeometricSectionSystem : ISystem {
        private ComponentLookup<AnchorPort> _anchorPortLookup;

        public void OnCreate(ref SystemState state) {
            _anchorPortLookup = SystemAPI.GetComponentLookup<AnchorPort>(true);
        }

        public void OnUpdate(ref SystemState state) {
            _anchorPortLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            new Job {
                Ecb = ecb.AsParallelWriter(),
                AnchorPortLookup = _anchorPortLookup,
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public ComponentLookup<AnchorPort> AnchorPortLookup;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, GeometricSectionAspect section) {
                if (!section.Dirty) return;

                section.Points.Clear();
                section.Points.Add(section.Anchor);

                if (section.DurationType == DurationType.Time) {
                    BuildGeometricTimeSection(section);
                }
                else {
                    BuildGeometricDistanceSection(section);
                }

                if (section.OutputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(section.OutputPorts[0], out var anchorPort)) {
                    anchorPort.Value = section.Points[^1].Value;
                    Ecb.SetComponent(chunkIndex, section.OutputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildGeometricSectionSystem: No anchor port found");
                }

                foreach (var port in section.OutputPorts) {
                    Ecb.SetComponent<Dirty>(chunkIndex, port, true);
                }

                section.Dirty = false;
            }

            private void BuildGeometricTimeSection(GeometricSectionAspect section) {
                int pointCount = (int)(HZ * section.Duration);
                for (int i = 1; i < pointCount; i++) {
                    PointData prev = section.Points[i - 1];
                    PointData curr = prev;

                    float t = i / HZ;
                    float rollSpeed = section.RollSpeedKeyframes.Evaluate(t);
                    float pitchChangeRate = section.PitchSpeedKeyframes.Evaluate(t);
                    float yawChangeRate = section.YawSpeedKeyframes.Evaluate(t);

                    float deltaTime = 1f / HZ;
                    float deltaRoll = rollSpeed * deltaTime;
                    float deltaPitch = pitchChangeRate * deltaTime;
                    float deltaYaw = yawChangeRate * deltaTime;

                    UpdateGeometricPoint(section, ref curr, ref prev, deltaRoll, deltaPitch, deltaYaw);
                    section.Points.Add(curr);

                    if (curr.Velocity < EPSILON) {
                        UnityEngine.Debug.LogWarning("Velocity is too low");
                        break;
                    }
                }
            }

            private void BuildGeometricDistanceSection(GeometricSectionAspect section) {
                float endLength = section.Points[0].Value.TotalLength + section.Duration;
                while (section.Points[^1].Value.TotalLength < endLength) {
                    PointData prev = section.Points[^1];
                    PointData curr = prev;

                    float d = section.Points[^1].Value.TotalLength;
                    float rollSpeedPerMeter = section.RollSpeedKeyframes.Evaluate(d);
                    float pitchChangeRatePerMeter = section.PitchSpeedKeyframes.Evaluate(d);
                    float yawChangeRatePerMeter = section.YawSpeedKeyframes.Evaluate(d);

                    float deltaTime = 1f / HZ;
                    float deltaRoll = rollSpeedPerMeter * prev.Velocity * deltaTime;
                    float deltaPitch = pitchChangeRatePerMeter * prev.Velocity * deltaTime;
                    float deltaYaw = yawChangeRatePerMeter * prev.Velocity * deltaTime;

                    UpdateGeometricPoint(section, ref curr, ref prev, deltaRoll, deltaPitch, deltaYaw);
                    section.Points.Add(curr);

                    if (curr.Velocity < EPSILON) {
                        UnityEngine.Debug.LogWarning("Velocity is too low");
                        break;
                    }
                }
            }

            private void UpdateGeometricPoint(
                GeometricSectionAspect section,
                ref PointData curr,
                ref PointData prev,
                float deltaRoll,
                float deltaPitch,
                float deltaYaw
            ) {
                curr.Direction = math.mul(
                    quaternion.Euler(math.radians(deltaPitch), math.radians(deltaYaw), 0f),
                    prev.Direction
                );
                curr.Lateral = math.mul(
                    quaternion.Euler(0f, math.radians(deltaYaw), 0f),
                    prev.Lateral
                );
                curr.Normal = math.normalize(math.cross(curr.Direction, curr.Lateral));
                curr.Position += curr.Direction * (curr.Velocity / (2f * HZ))
                    + prev.Direction * (curr.Velocity / (2f * HZ))
                    + (prev.GetHeartPosition(HEART) - curr.GetHeartPosition(HEART));

                quaternion rollQuat = quaternion.AxisAngle(curr.Direction, math.radians(-deltaRoll));
                curr.Lateral = math.normalize(math.mul(rollQuat, curr.Lateral));
                curr.Normal = math.normalize(math.cross(curr.Direction, curr.Lateral));
                curr.Roll = math.degrees(math.atan2(curr.Lateral.y, -curr.Normal.y));
                curr.Roll = (curr.Roll + 540) % 360 - 180;

                // Compute point metrics
                curr.DistanceFromLast = math.distance(curr.GetHeartPosition(HEART), prev.GetHeartPosition(HEART));
                curr.TotalLength += curr.DistanceFromLast;
                curr.HeartDistanceFromLast = math.distance(curr.Position, prev.Position);
                curr.TotalHeartLength += curr.HeartDistanceFromLast;

                // Update energy and velocity
                float pe = G * (curr.GetHeartPosition(CENTER).y + curr.TotalLength * FRICTION);
                if (section.FixedVelocity) {
                    curr.Velocity = 10f;
                    curr.Energy = 0.5f * curr.Velocity * curr.Velocity + pe;
                }
                else {
                    curr.Energy -= curr.Velocity * curr.Velocity * curr.Velocity * RESISTANCE / HZ;
                    curr.Velocity = math.sqrt(2f * math.max(0, curr.Energy - pe));
                }

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
        }
    }
}
