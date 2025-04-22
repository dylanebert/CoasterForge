using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using static CoasterForge.Constants;

namespace CoasterForge {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildGeometricSectionSystem : ISystem {
        public void OnUpdate(ref SystemState state) {
            new Job().ScheduleParallel();
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            public void Execute(GeometricSectionAspect section) {
                section.Nodes.Clear();

                Node anchor = Node.Default;
                anchor.Velocity = 10f;
                anchor.Energy = 0.5f * anchor.Velocity * anchor.Velocity + G * anchor.GetHeartPosition(CENTER).y;
                section.Nodes.Add(anchor);

                if (section.DurationType == DurationType.Time) {
                    BuildGeometricTimeSection(section);
                }
                else {
                    BuildGeometricDistanceSection(section);
                }
            }

            private void BuildGeometricTimeSection(GeometricSectionAspect section) {
                int nodeCount = (int)(HZ * section.Duration);
                for (int i = 1; i < nodeCount; i++) {
                    Node prev = section.Nodes[i - 1];
                    Node node = prev;

                    float t = i / HZ;
                    float rollSpeed = section.RollSpeedKeyframes.Evaluate(t);
                    float pitchChangeRate = section.PitchSpeedKeyframes.Evaluate(t);
                    float yawChangeRate = section.YawSpeedKeyframes.Evaluate(t);

                    float deltaTime = 1f / HZ;
                    float deltaRoll = rollSpeed * deltaTime;
                    float deltaPitch = pitchChangeRate * deltaTime;
                    float deltaYaw = yawChangeRate * deltaTime;

                    UpdateGeometricNode(section, ref node, ref prev, deltaRoll, deltaPitch, deltaYaw);
                    section.Nodes.Add(node);
                }
            }

            private void BuildGeometricDistanceSection(GeometricSectionAspect section) {
                while (section.Nodes[^1].TotalLength < section.Duration) {
                    Node prev = section.Nodes[^1];
                    Node node = prev;

                    float d = section.Nodes[^1].TotalLength;
                    float rollSpeedPerMeter = section.RollSpeedKeyframes.Evaluate(d);
                    float pitchChangeRatePerMeter = section.PitchSpeedKeyframes.Evaluate(d);
                    float yawChangeRatePerMeter = section.YawSpeedKeyframes.Evaluate(d);

                    float deltaTime = 1f / HZ;
                    float deltaRoll = rollSpeedPerMeter * prev.Velocity * deltaTime;
                    float deltaPitch = pitchChangeRatePerMeter * prev.Velocity * deltaTime;
                    float deltaYaw = yawChangeRatePerMeter * prev.Velocity * deltaTime;

                    UpdateGeometricNode(section, ref node, ref prev, deltaRoll, deltaPitch, deltaYaw);
                    section.Nodes.Add(node);
                }
            }

            private void UpdateGeometricNode(
                GeometricSectionAspect section,
                ref Node node,
                ref Node prev,
                float deltaRoll,
                float deltaPitch,
                float deltaYaw
            ) {
                node.Direction = math.mul(
                    quaternion.Euler(math.radians(deltaPitch), math.radians(deltaYaw), 0f),
                    prev.Direction
                );
                node.Lateral = math.mul(
                    quaternion.Euler(0f, math.radians(deltaYaw), 0f),
                    prev.Lateral
                );
                node.Normal = math.normalize(math.cross(node.Direction, node.Lateral));
                node.Position += node.Direction * (node.Velocity / (2f * HZ))
                    + prev.Direction * (node.Velocity / (2f * HZ))
                    + (prev.GetHeartPosition(HEART) - node.GetHeartPosition(HEART));

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

                // Update energy and velocity
                float pe = G * (node.GetHeartPosition(CENTER).y + node.TotalLength * FRICTION);
                if (section.FixedVelocity) {
                    node.Velocity = 10f;
                    node.Energy = 0.5f * node.Velocity * node.Velocity + pe;
                }
                else {
                    node.Energy -= node.Velocity * node.Velocity * node.Velocity * RESISTANCE / HZ;
                    node.Velocity = math.sqrt(2f * math.max(0, node.Energy - pe));
                }

                // Compute orientation changes
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

                // Compute actual forces
                float3 forceVec;
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
