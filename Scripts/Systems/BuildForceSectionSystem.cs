using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static CoasterForge.Constants;

namespace CoasterForge {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildForceSectionSystem : ISystem {
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

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ForceSectionAspect section) {
                if (!section.Dirty) return;

                section.Points.Clear();
                section.Points.Add(section.Anchor);

                if (section.DurationType == DurationType.Time) {
                    BuildForceTimeSection(section);
                }
                else {
                    BuildForceDistanceSection(section);
                }

                if (section.OutputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(section.OutputPorts[0], out var anchorPort)) {
                    anchorPort.Value = section.Points[^1].Value;
                    Ecb.SetComponent(chunkIndex, section.OutputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildForceSectionSystem: No anchor port found");
                }

                foreach (var port in section.OutputPorts) {
                    Ecb.SetComponent<Dirty>(chunkIndex, port, true);
                }

                section.Dirty = false;
            }

            private void BuildForceTimeSection(ForceSectionAspect section) {
                int pointCount = (int)(HZ * section.Duration);
                for (int i = 1; i < pointCount; i++) {
                    PointData prev = section.Points[i - 1];
                    PointData curr = prev;

                    // Assign target constraints values
                    float t = i / HZ;
                    curr.RollSpeed = section.RollSpeedKeyframes.Evaluate(t);
                    curr.NormalForce = section.NormalForceKeyframes.Evaluate(t);
                    curr.LateralForce = section.LateralForceKeyframes.Evaluate(t);

                    UpdateForcePoint(section, ref curr, ref prev);
                    section.Points.Add(curr);

                    if (curr.Velocity < EPSILON) {
                        UnityEngine.Debug.LogWarning("Velocity is too low");
                        break;
                    }
                }
            }

            private void BuildForceDistanceSection(ForceSectionAspect section) {
                float endLength = section.Points[0].Value.TotalLength + section.Duration;
                while (section.Points[^1].Value.TotalLength < endLength) {
                    PointData prev = section.Points[^1];
                    PointData curr = prev;

                    float d = prev.TotalLength + prev.Velocity / HZ;
                    curr.RollSpeed = section.RollSpeedKeyframes.Evaluate(d);
                    curr.NormalForce = section.NormalForceKeyframes.Evaluate(d);
                    curr.LateralForce = section.LateralForceKeyframes.Evaluate(d);

                    UpdateForcePoint(section, ref curr, ref prev);
                    section.Points.Add(curr);

                    if (curr.Velocity < EPSILON) {
                        UnityEngine.Debug.LogWarning("Velocity is too low");
                        break;
                    }
                }
            }

            private void UpdateForcePoint(ForceSectionAspect section, ref PointData curr, ref PointData prev) {
                // Compute force vectors needed to achieve target forces
                float3 forceVec = -curr.NormalForce * prev.Normal - curr.LateralForce * prev.Lateral + math.down();
                float normalForce = -math.dot(forceVec, prev.Normal) * G;
                float lateralForce = -math.dot(forceVec, prev.Lateral) * G;

                float estimatedVelocity = math.abs(prev.HeartDistanceFromLast) < EPSILON ? prev.Velocity : prev.HeartDistanceFromLast * HZ;
                if (math.abs(estimatedVelocity) < EPSILON) estimatedVelocity = EPSILON;
                if (math.abs(prev.Velocity) < EPSILON) prev.Velocity = EPSILON;

                // Compute curvature needed to match force vectors
                curr.Direction = math.mul(
                    math.mul(
                        quaternion.AxisAngle(prev.Lateral, normalForce / estimatedVelocity / HZ),
                        quaternion.AxisAngle(prev.Normal, -lateralForce / prev.Velocity / HZ)
                    ),
                    prev.Direction
                );
                curr.Lateral = math.mul(
                    quaternion.AxisAngle(prev.Normal, -lateralForce / prev.Velocity / HZ),
                    prev.Lateral
                );
                curr.Normal = math.normalize(math.cross(curr.Direction, curr.Lateral));
                curr.Position += curr.Direction * (curr.Velocity / (2f * HZ))
                    + prev.Direction * (curr.Velocity / (2f * HZ))
                    + (prev.GetHeartPosition(HEART) - curr.GetHeartPosition(HEART));

                // Apply roll
                float deltaRoll;
                if (section.DurationType == DurationType.Time) {
                    deltaRoll = curr.RollSpeed / HZ;
                }
                else {
                    deltaRoll = curr.RollSpeed * (prev.Velocity / HZ);
                }
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
