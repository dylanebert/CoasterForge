using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using static CoasterForge.Constants;

namespace CoasterForge {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildForceSectionSystem : ISystem {
        public void OnUpdate(ref SystemState state) {
            new Job().ScheduleParallel();
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            public void Execute(ForceSectionAspect section) {
                section.Nodes.Clear();

                Node anchor = Node.Default;
                anchor.Velocity = 10f;
                anchor.Energy = 0.5f * anchor.Velocity * anchor.Velocity + G * anchor.GetHeartPosition(CENTER).y;
                section.Nodes.Add(anchor);

                if (section.DurationType == DurationType.Time) {
                    BuildForceTimeSection(section);
                }
                else {
                    BuildForceDistanceSection(section);
                }
            }

            private void BuildForceTimeSection(ForceSectionAspect section) {
                int nodeCount = (int)(HZ * section.Duration);
                for (int i = 1; i < nodeCount; i++) {
                    Node prev = section.Nodes[i - 1];
                    Node node = prev;

                    // Assign target constraints values
                    float t = i / HZ;
                    node.RollSpeed = section.RollSpeedKeyframes.Evaluate(t);
                    node.NormalForce = section.NormalForceKeyframes.Evaluate(t);
                    node.LateralForce = section.LateralForceKeyframes.Evaluate(t);

                    UpdateForceNode(section, ref node, ref prev);
                    section.Nodes.Add(node);
                }
            }

            private void BuildForceDistanceSection(ForceSectionAspect section) {
                while (section.Nodes[^1].TotalLength < section.Duration) {
                    Node prev = section.Nodes[^1];
                    Node node = prev;

                    float d = prev.TotalLength + prev.Velocity / HZ;
                    node.RollSpeed = section.RollSpeedKeyframes.Evaluate(d);
                    node.NormalForce = section.NormalForceKeyframes.Evaluate(d);
                    node.LateralForce = section.LateralForceKeyframes.Evaluate(d);

                    UpdateForceNode(section, ref node, ref prev);
                    section.Nodes.Add(node);
                }
            }

            private void UpdateForceNode(ForceSectionAspect section, ref Node node, ref Node prev) {
                // Compute force vectors needed to achieve target forces
                float3 forceVec = -node.NormalForce * prev.Normal - node.LateralForce * prev.Lateral + math.down();
                float normalForce = -math.dot(forceVec, prev.Normal) * G;
                float lateralForce = -math.dot(forceVec, prev.Lateral) * G;

                float estimatedVelocity = math.abs(prev.HeartDistanceFromLast) < EPSILON ? prev.Velocity : prev.HeartDistanceFromLast * HZ;
                if (math.abs(estimatedVelocity) < EPSILON) estimatedVelocity = EPSILON;
                if (math.abs(prev.Velocity) < EPSILON) prev.Velocity = EPSILON;

                // Compute curvature needed to match force vectors
                node.Direction = math.mul(
                    math.mul(
                        quaternion.AxisAngle(prev.Lateral, normalForce / estimatedVelocity / HZ),
                        quaternion.AxisAngle(prev.Normal, -lateralForce / prev.Velocity / HZ)
                    ),
                    prev.Direction
                );
                node.Lateral = math.mul(
                    quaternion.AxisAngle(prev.Normal, -lateralForce / prev.Velocity / HZ),
                    prev.Lateral
                );
                node.Normal = math.normalize(math.cross(node.Direction, node.Lateral));
                node.Position += node.Direction * (node.Velocity / (2f * HZ))
                    + prev.Direction * (node.Velocity / (2f * HZ))
                    + (prev.GetHeartPosition(HEART) - node.GetHeartPosition(HEART));

                // Apply roll
                float deltaRoll;
                if (section.DurationType == DurationType.Time) {
                    deltaRoll = node.RollSpeed / HZ;
                }
                else {
                    deltaRoll = node.RollSpeed * (prev.Velocity / HZ);
                }
                quaternion rollQuat = quaternion.AxisAngle(node.Direction, math.radians(-deltaRoll));
                node.Lateral = math.normalize(math.mul(rollQuat, node.Lateral));
                node.Normal = math.normalize(math.cross(node.Direction, node.Lateral));
                node.Roll = math.degrees(math.atan2(node.Lateral.y, -node.Normal.y));
                node.Roll = (node.Roll + 540) % 360 - 180;

                // Compute node metrics
                node.DistanceFromLast = math.distance(node.GetHeartPosition(HEART), prev.GetHeartPosition(HEART));
                node.TotalLength += node.DistanceFromLast;
                node.HeartDistanceFromLast = math.distance(node.Position, prev.Position);
                node.TotalHeartLength += node.HeartDistanceFromLast;

                float3 diff = node.Direction - prev.Direction;
                if (math.length(diff) < EPSILON) {
                    node.PitchFromLast = 0f;
                    node.YawFromLast = 0f;
                }
                else {
                    node.PitchFromLast = (node.GetPitch() - prev.GetPitch() + 540) % 360 - 180;
                    node.YawFromLast = (node.GetYaw() - prev.GetYaw() + 540) % 360 - 180;
                }
                float yawScaleFactor = math.cos(math.abs(math.radians(node.GetPitch())));
                node.AngleFromLast = math.sqrt(yawScaleFactor * yawScaleFactor * node.YawFromLast * node.YawFromLast + node.PitchFromLast * node.PitchFromLast);

                float pe = G * (node.GetHeartPosition(CENTER).y + node.TotalLength * FRICTION);
                if (section.FixedVelocity) {
                    node.Velocity = 10f;
                    node.Energy = 0.5f * node.Velocity * node.Velocity + pe;
                }
                else {
                    node.Energy -= node.Velocity * node.Velocity * node.Velocity * RESISTANCE / HZ;
                    node.Velocity = math.sqrt(2f * math.max(0, node.Energy - pe));
                }

                // Compute actual forces
                if (math.abs(node.AngleFromLast) < EPSILON) {
                    forceVec = math.up();
                }
                else {
                    float cosRoll = math.cos(math.radians(node.Roll));
                    float sinRoll = math.sin(math.radians(node.Roll));
                    float normalAngle = math.radians(-node.PitchFromLast * cosRoll
                        - yawScaleFactor * node.YawFromLast * sinRoll);
                    float lateralAngle = math.radians(node.PitchFromLast * sinRoll
                        - yawScaleFactor * node.YawFromLast * cosRoll);
                    forceVec = math.up()
                        + lateralAngle * node.Lateral * node.Velocity * HZ / G
                        + normalAngle * node.Normal * node.HeartDistanceFromLast * HZ * HZ / G;
                }
                node.NormalForce = -math.dot(forceVec, node.Normal);
                node.LateralForce = -math.dot(forceVec, node.Lateral);

                if (node.TieDistance > TIE_SPACING) {
                    node.TieDistance = 0f;
                }
                else {
                    node.TieDistance += node.DistanceFromLast;
                }
            }
        }
    }
}
