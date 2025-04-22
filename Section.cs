using System.Collections.Generic;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CoasterForge.Constants;

namespace CoasterForge {
    public class Section : MonoBehaviour {
        public List<Keyframe> RollSpeedKeyframes;
        public List<Keyframe> NormalForceKeyframes;
        public List<Keyframe> LateralForceKeyframes;
        public float Duration;
        public DurationType DurationType;
        public SectionType SectionType;
        public bool FixedVelocity;
        public bool Autobuild;

#if UNITY_EDITOR
        public AnimationCurve RollSpeedCurveEditor;
        public AnimationCurve NormalForceCurveEditor;
        public AnimationCurve LateralForceCurveEditor;
#endif

        private NativeList<Node> _nodesRW;
        private NativeList<Node> _nodesRO;

        private JobHandle _jobHandle;

        public NativeList<Node> Nodes => _nodesRO;
        public int NodeCount => _nodesRO.Length;

        private void Start() {
            _nodesRW = new NativeList<Node>(65536, Allocator.Persistent);
            _nodesRO = new NativeList<Node>(65536, Allocator.Persistent);
        }

        private void OnDestroy() {
            _jobHandle.Complete();
            _nodesRW.Dispose();
            _nodesRO.Dispose();
        }

        private void Update() {
            if (Autobuild) {
                Build();
            }
        }

#if UNITY_EDITOR
        public void UpdateEditorCurves() {
            RollSpeedCurveEditor.ClearKeys();
            NormalForceCurveEditor.ClearKeys();
            LateralForceCurveEditor.ClearKeys();

            int nodeCount = (int)(HZ * Duration);
            var nodeCountRef = new NativeReference<int>(Allocator.TempJob) { Value = nodeCount };

            var rollSpeedKeyframes = new NativeArray<Keyframe>(RollSpeedKeyframes.Count, Allocator.TempJob);
            var normalForceKeyframes = new NativeArray<Keyframe>(NormalForceKeyframes.Count, Allocator.TempJob);
            var lateralForceKeyframes = new NativeArray<Keyframe>(LateralForceKeyframes.Count, Allocator.TempJob);

            for (int i = 0; i < RollSpeedKeyframes.Count; i++) {
                rollSpeedKeyframes[i] = RollSpeedKeyframes[i];
            }
            for (int i = 0; i < NormalForceKeyframes.Count; i++) {
                normalForceKeyframes[i] = NormalForceKeyframes[i];
            }
            for (int i = 0; i < LateralForceKeyframes.Count; i++) {
                lateralForceKeyframes[i] = LateralForceKeyframes[i];
            }

            for (int i = 0; i < nodeCount; i++) {
                float t = i / HZ;
                float rollSpeed = rollSpeedKeyframes.Evaluate(t);
                float normalForce = normalForceKeyframes.Evaluate(t);
                float lateralForce = lateralForceKeyframes.Evaluate(t);

                var rollKey = new UnityEngine.Keyframe(t, rollSpeed) { weightedMode = WeightedMode.None };
                var normalKey = new UnityEngine.Keyframe(t, normalForce) { weightedMode = WeightedMode.None };
                var lateralKey = new UnityEngine.Keyframe(t, lateralForce) { weightedMode = WeightedMode.None };

                RollSpeedCurveEditor.AddKey(rollKey);
                NormalForceCurveEditor.AddKey(normalKey);
                LateralForceCurveEditor.AddKey(lateralKey);
            }

            rollSpeedKeyframes.Dispose();
            normalForceKeyframes.Dispose();
            lateralForceKeyframes.Dispose();
            nodeCountRef.Dispose();
        }
#endif

        public JobHandle Build(bool force = false) {
            if (!force && !_jobHandle.IsCompleted) {
                return default;
            }

            _jobHandle.Complete();

            if (!force) {
                new CopyJob {
                    NodesRO = _nodesRO,
                    NodesRW = _nodesRW,
                }.Run();
            }

            if (Duration < 0.01f) {
                return default;
            }

            var rollSpeedKeyframes = new NativeArray<Keyframe>(RollSpeedKeyframes.Count, Allocator.TempJob);
            var normalForceKeyframes = new NativeArray<Keyframe>(NormalForceKeyframes.Count, Allocator.TempJob);
            var lateralForceKeyframes = new NativeArray<Keyframe>(LateralForceKeyframes.Count, Allocator.TempJob);

            for (int i = 0; i < RollSpeedKeyframes.Count; i++) {
                rollSpeedKeyframes[i] = RollSpeedKeyframes[i];
            }
            for (int i = 0; i < NormalForceKeyframes.Count; i++) {
                normalForceKeyframes[i] = NormalForceKeyframes[i];
            }
            for (int i = 0; i < LateralForceKeyframes.Count; i++) {
                lateralForceKeyframes[i] = LateralForceKeyframes[i];
            }

            _jobHandle = new BuildJob {
                Nodes = _nodesRW,
                RollSpeedKeyframes = rollSpeedKeyframes,
                NormalForceKeyframes = normalForceKeyframes,
                LateralForceKeyframes = lateralForceKeyframes,
                Duration = Duration,
                DurationType = DurationType,
                SectionType = SectionType,
                FixedVelocity = FixedVelocity,
            }.Schedule(_jobHandle);

            _jobHandle = rollSpeedKeyframes.Dispose(_jobHandle);
            _jobHandle = normalForceKeyframes.Dispose(_jobHandle);
            _jobHandle = lateralForceKeyframes.Dispose(_jobHandle);

            if (force) {
                _jobHandle = new CopyJob {
                    NodesRO = _nodesRO,
                    NodesRW = _nodesRW,
                }.Schedule(_jobHandle);
            }

            return _jobHandle;
        }

        private void OnDrawGizmos() {
            if (!_nodesRO.IsCreated) return;
            int nodeCount = _nodesRO.Length;
            for (int i = 0; i < nodeCount; i++) {
                if (i % 10 != 0) continue;
                var node = _nodesRO[i];
                float3 position = node.Position;
                float3 direction = node.Direction;
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(position, 0.1f);
                Gizmos.DrawLine(position, position + direction);

                float3 heartPosition = node.GetHeartPosition(HEART);
                float3 heartDirection = node.GetHeartDirection(HEART);
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(heartPosition, 0.1f);
                Gizmos.DrawLine(heartPosition, heartPosition + heartDirection);

                float3 heartLateral = node.GetHeartLateral(HEART);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(heartPosition, heartPosition + heartLateral);
            }
        }

        [BurstCompile]
        private struct BuildJob : IJob {
            public NativeList<Node> Nodes;

            [ReadOnly]
            public NativeArray<Keyframe> RollSpeedKeyframes;

            [ReadOnly]
            public NativeArray<Keyframe> NormalForceKeyframes;

            [ReadOnly]
            public NativeArray<Keyframe> LateralForceKeyframes;

            [ReadOnly]
            public float Duration;

            [ReadOnly]
            public DurationType DurationType;

            [ReadOnly]
            public SectionType SectionType;

            [ReadOnly]
            public bool FixedVelocity;

            public void Execute() {
                Nodes.Clear();

                Node anchor = Node.Default;
                anchor.Velocity = 10f;
                anchor.Energy = 0.5f * anchor.Velocity * anchor.Velocity + G * anchor.GetHeartPosition(CENTER).y;
                Nodes.Add(anchor);

                switch (SectionType) {
                    case SectionType.Geometric:
                        switch (DurationType) {
                            case DurationType.Time:
                                BuildGeometricTimeSection();
                                break;
                            case DurationType.Distance:
                                BuildGeometricDistanceSection();
                                break;
                        }
                        break;
                    case SectionType.Force:
                        switch (DurationType) {
                            case DurationType.Time:
                                BuildForceTimeSection();
                                break;
                            case DurationType.Distance:
                                BuildForceDistanceSection();
                                break;
                        }
                        break;
                }
            }

            private void BuildGeometricTimeSection() {
                int nodeCount = (int)(HZ * Duration);
                for (int i = 1; i < nodeCount; i++) {
                    Node prev = Nodes[i - 1];
                    Node node = prev;

                    float t = i / HZ;
                    float rollSpeedPerMeter = RollSpeedKeyframes.Evaluate(t);
                    float pitchChangeRatePerMeter = NormalForceKeyframes.Evaluate(t);
                    float yawChangeRatePerMeter = LateralForceKeyframes.Evaluate(t);

                    float deltaTime = 1f / HZ;
                    float rollSpeedPerSecond = rollSpeedPerMeter * prev.Velocity;
                    float pitchChangePerSecond = pitchChangeRatePerMeter * prev.Velocity;
                    float yawChangePerSecond = yawChangeRatePerMeter * prev.Velocity;

                    float rollSpeed = rollSpeedPerSecond * deltaTime;
                    float pitchChange = pitchChangePerSecond * deltaTime;
                    float yawChange = yawChangePerSecond * deltaTime;

                    UpdateGeometricNode(ref node, ref prev, pitchChange, yawChange, rollSpeed);
                    Nodes.Add(node);
                }
            }

            private void BuildGeometricDistanceSection() {
                while (Nodes[^1].TotalLength < Duration) {
                    Node prev = Nodes[^1];
                    Node node = prev;

                    float t = Nodes[^1].TotalLength / Duration;
                    float rollSpeed = RollSpeedKeyframes.Evaluate(t);
                    float pitchChangeRate = LateralForceKeyframes.Evaluate(t);
                    float yawChangeRate = NormalForceKeyframes.Evaluate(t);

                    float deltaLength = 1f / HZ;
                    float pitchChange = pitchChangeRate * deltaLength;
                    float yawChange = yawChangeRate * deltaLength;

                    UpdateGeometricNode(ref node, ref prev, pitchChange, yawChange, rollSpeed);
                    Nodes.Add(node);
                }
            }

            private void UpdateGeometricNode(ref Node node, ref Node prev, float pitchChange, float yawChange, float deltaRoll) {
                node.Direction = math.mul(
                    quaternion.Euler(math.radians(pitchChange), math.radians(yawChange), 0f),
                    prev.Direction
                );
                node.Lateral = math.mul(
                    quaternion.Euler(0f, math.radians(yawChange), 0f),
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
                if (FixedVelocity) {
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

            private void BuildForceTimeSection() {
                int nodeCount = (int)(HZ * Duration);
                for (int i = 1; i < nodeCount; i++) {
                    Node prev = Nodes[i - 1];
                    Node node = prev;

                    // Assign target constraints values
                    node.RollSpeed = RollSpeedKeyframes.Evaluate(i / HZ);
                    node.NormalForce = NormalForceKeyframes.Evaluate(i / HZ);
                    node.LateralForce = LateralForceKeyframes.Evaluate(i / HZ);

                    UpdateForceNode(ref node, ref prev);
                    Nodes.Add(node);
                }
            }

            private void BuildForceDistanceSection() {
                while (Nodes[^1].TotalLength < Duration) {
                    Node prev = Nodes[^1];
                    Node node = prev;

                    float estimatedDistance = prev.TotalLength + prev.Velocity / HZ;

                    node.RollSpeed = RollSpeedKeyframes.Evaluate(estimatedDistance);
                    node.NormalForce = NormalForceKeyframes.Evaluate(estimatedDistance);
                    node.LateralForce = LateralForceKeyframes.Evaluate(estimatedDistance);

                    UpdateForceNode(ref node, ref prev);
                    Nodes.Add(node);
                }
            }

            private void UpdateForceNode(ref Node node, ref Node prev) {
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
                if (DurationType == DurationType.Time) {
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
                if (FixedVelocity) {
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

        [BurstCompile]
        private struct CopyJob : IJob {
            [WriteOnly]
            public NativeList<Node> NodesRO;

            [ReadOnly]
            public NativeList<Node> NodesRW;

            public void Execute() {
                NodesRO.Clear();
                NodesRO.ResizeUninitialized(NodesRW.Length);
                for (int i = 0; i < NodesRW.Length; i++) {
                    NodesRO[i] = NodesRW[i];
                }
            }
        }

        public struct Node {
            public float3 Position;
            public float3 Direction;
            public float3 Lateral;
            public float3 Normal;
            public float Roll;
            public float Velocity;
            public float Energy;
            public float NormalForce;
            public float LateralForce;
            public float DistanceFromLast;
            public float HeartDistanceFromLast;
            public float AngleFromLast;
            public float PitchFromLast;
            public float YawFromLast;
            public float RollSpeed;
            public float TotalLength;
            public float TotalHeartLength;
            public float TieDistance;

            public static Node Default => new() {
                Position = float3.zero,
                Direction = math.back(),
                Lateral = math.right(),
                Normal = math.down(),
                Roll = 0f,
                Velocity = 0f,
                Energy = 0f,
                NormalForce = 1f,
                LateralForce = 0f,
                DistanceFromLast = 0f,
                HeartDistanceFromLast = 0f,
                AngleFromLast = 0f,
                PitchFromLast = 0f,
                YawFromLast = 0f,
                RollSpeed = 0f,
                TotalLength = 0f,
                TotalHeartLength = 0f,
                TieDistance = 0f,
            };

            public override string ToString() {
                StringBuilder sb = new();
                sb.AppendLine($"Position: {Position}");
                sb.AppendLine($"Direction: {Direction}");
                sb.AppendLine($"Lateral: {Lateral}");
                sb.AppendLine($"Normal: {Normal}");
                sb.AppendLine($"Roll: {Roll}");
                sb.AppendLine($"Velocity: {Velocity}");
                sb.AppendLine($"Energy: {Energy}");
                sb.AppendLine($"NormalForce: {NormalForce}");
                sb.AppendLine($"LateralForce: {LateralForce}");
                sb.AppendLine($"DistanceFromLast: {DistanceFromLast}");
                sb.AppendLine($"HeartDistanceFromLast: {HeartDistanceFromLast}");
                sb.AppendLine($"AngleFromLast: {AngleFromLast}");
                sb.AppendLine($"PitchFromLast: {PitchFromLast}");
                sb.AppendLine($"YawFromLast: {YawFromLast}");
                sb.AppendLine($"RollSpeed: {RollSpeed}");
                sb.AppendLine($"TotalLength: {TotalLength}");
                sb.AppendLine($"TotalHeartLength: {TotalHeartLength}");
                sb.AppendLine($"TieDistance: {TieDistance}");
                return sb.ToString();
            }
        }

        [System.Serializable]
        public struct Keyframe {
            public float Time;
            public float Value;
            public InterpolationType InInterpolation;
            public InterpolationType OutInterpolation;

            public float InTangent;
            public float OutTangent;
            public float InWeight;
            public float OutWeight;

            public enum InterpolationType {
                Constant,
                Linear,
                Ease,
            }

            public static Keyframe Default => new() {
                Time = 0f,
                Value = 0f,
                InInterpolation = InterpolationType.Ease,
                OutInterpolation = InterpolationType.Ease,
                InTangent = 0f,
                OutTangent = 0f,
                InWeight = 1 / 3f,
                OutWeight = 1 / 3f,
            };
        }
    }
}
