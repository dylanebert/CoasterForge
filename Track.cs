using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static CoasterForge.Constants;

namespace CoasterForge {
    public class Track : UnityEngine.MonoBehaviour {
        public UnityEngine.AnimationCurve NormalForceCurve;
        public UnityEngine.AnimationCurve LateralForceCurve;
        public UnityEngine.AnimationCurve RollSpeedCurve;
        public float Duration = 10f;
        public bool FixedVelocity = false;

        private NativeArray<Node> _nodes;
        private NativeArray<Node> _prevNodes;
        private NativeArray<float> _desiredNormalForces;
        private NativeArray<float> _desiredLateralForces;
        private NativeArray<float> _desiredRollSpeeds;
        private NativeReference<int> _solvedResolutionReference;
        private JobHandle _jobHandle;
        private int _nodeCount;
        private int _refinement;
        private int _lastSolvedResolution = -1;
        private bool _dirty;
        private bool _initialized;

        public NativeArray<Node> Nodes => _prevNodes;
        public int NodeCount => _nodeCount;
        public int SolvedResolution => _lastSolvedResolution;

        private void Initialize() {
            if (_initialized) {
                Dispose();
            }

            _nodeCount = (int)(HZ * Duration);
            _nodes = new NativeArray<Node>(_nodeCount, Allocator.Persistent);
            _prevNodes = new NativeArray<Node>(_nodeCount, Allocator.Persistent);
            _desiredNormalForces = new NativeArray<float>(_nodeCount, Allocator.Persistent);
            _desiredLateralForces = new NativeArray<float>(_nodeCount, Allocator.Persistent);
            _desiredRollSpeeds = new NativeArray<float>(_nodeCount, Allocator.Persistent);
            _solvedResolutionReference = new NativeReference<int>(1, Allocator.Persistent);
            for (int i = 0; i < _nodeCount; i++) {
                var node = Node.Default;
                _nodes[i] = node;
            }
            UpdateFunctions();
            _initialized = true;
            _refinement = 0;
            _solvedResolutionReference.Value = -1;
            _lastSolvedResolution = -1;
        }

        private void Dispose() {
            if (!_initialized) return;
            _nodes.Dispose();
            _prevNodes.Dispose();
            _desiredNormalForces.Dispose();
            _desiredLateralForces.Dispose();
            _desiredRollSpeeds.Dispose();
            _solvedResolutionReference.Dispose();
            _initialized = false;
        }

        private void Start() {
            Initialize();
        }

        private void OnDestroy() {
            _jobHandle.Complete();
            Dispose();
        }

        private void Update() {
            _jobHandle.Complete();

            int nodeCount = (int)(HZ * Duration);
            if (nodeCount != _nodeCount) {
                Initialize();
                return;
            }

            if (_dirty && _lastSolvedResolution > 0) {
                UpdateFunctions();
                _refinement = 0;
                _solvedResolutionReference.Value = -1;
                _lastSolvedResolution = -1;
                _dirty = false;
                return;
            }

            _prevNodes.CopyFrom(_nodes);
            _lastSolvedResolution = _solvedResolutionReference.Value;

            _jobHandle = new BuildJob {
                Nodes = _nodes,
                SolvedResolution = _solvedResolutionReference,
                DesiredNormalForces = _desiredNormalForces,
                DesiredLateralForces = _desiredLateralForces,
                DesiredRollSpeeds = _desiredRollSpeeds,
                Refinement = _refinement,
                FixedVelocity = FixedVelocity,
            }.Schedule();

            _refinement++;
        }

        private void UpdateFunctions() {
            for (int i = 0; i < _nodeCount; i++) {
                _desiredNormalForces[i] = NormalForceCurve.Evaluate(i / (_nodeCount - 1f));
                _desiredLateralForces[i] = LateralForceCurve.Evaluate(i / (_nodeCount - 1f));
                _desiredRollSpeeds[i] = RollSpeedCurve.Evaluate(i / (_nodeCount - 1f));
            }
        }

        public void MarkDirty() {
            _dirty = true;
        }

        private void OnDrawGizmos() {
            for (int i = 0; i < _prevNodes.Length; i++) {
                if (i % 100 != 0) continue;
                var node = _prevNodes[i];
                float3 position = node.Position;
                float3 direction = node.Direction;
                UnityEngine.Gizmos.color = UnityEngine.Color.red;
                UnityEngine.Gizmos.DrawSphere(position, 0.1f);
                UnityEngine.Gizmos.DrawLine(position, position + direction);

                float3 heartPosition = node.GetHeartPosition(HEART);
                float3 heartDirection = node.GetHeartDirection(HEART);
                UnityEngine.Gizmos.color = UnityEngine.Color.yellow;
                UnityEngine.Gizmos.DrawSphere(heartPosition, 0.1f);
                UnityEngine.Gizmos.DrawLine(heartPosition, heartPosition + heartDirection);

                float3 heartLateral = node.GetHeartLateral(HEART);
                UnityEngine.Gizmos.color = UnityEngine.Color.blue;
                UnityEngine.Gizmos.DrawLine(heartPosition, heartPosition + heartLateral);
            }
        }

        [BurstCompile]
        private struct BuildJob : IJob {
            public NativeArray<Node> Nodes;

            [WriteOnly]
            public NativeReference<int> SolvedResolution;

            [ReadOnly]
            public NativeArray<float> DesiredNormalForces;

            [ReadOnly]
            public NativeArray<float> DesiredLateralForces;

            [ReadOnly]
            public NativeArray<float> DesiredRollSpeeds;

            [ReadOnly]
            public int Refinement;

            [ReadOnly]
            public bool FixedVelocity;

            public void Execute() {
                UpdateAnchor();

                const int fineNodesPerFrame = 10000;
                int maxK = 1;
                int levels = (int)math.floor(math.log2(Nodes.Length / (float)fineNodesPerFrame));
                if (levels > 0) maxK = 1 << levels;

                int groupIndex = 0;
                int remaining = Refinement;
                int groupLevels = 1;
                while (remaining >= groupLevels) {
                    remaining -= groupLevels;
                    groupIndex++;
                    groupLevels *= 2;
                }

                int k = maxK >> groupIndex;
                if (k < 1) k = 1;

                int nodesPerGroup = (int)math.ceil(Nodes.Length / (float)groupLevels);
                int start = nodesPerGroup * remaining;
                int end = math.min(start + nodesPerGroup, Nodes.Length);
                float hz = HZ / k;

                for (int i = start + k; i < end; i += k) {
                    if (Refinement > 0 && (i - start) % (k * 2) == 0) continue;

                    Node prev = Nodes[i - k];
                    Node node = prev;

                    // Assign target constraints values
                    node.NormalForce = DesiredNormalForces[i];
                    node.LateralForce = DesiredLateralForces[i];
                    node.RollSpeed = DesiredRollSpeeds[i];

                    // Compute force vectors needed to achieve target forces
                    float3 forceVec = -node.NormalForce * prev.Normal - node.LateralForce * prev.Lateral + math.down();
                    float normalForce = -math.dot(forceVec, prev.Normal) * G;
                    float lateralForce = -math.dot(forceVec, prev.Lateral) * G;

                    float estimatedVelocity = math.abs(prev.HeartDistanceFromLast) < EPSILON ? prev.Velocity : prev.HeartDistanceFromLast * hz;
                    if (math.abs(estimatedVelocity) < EPSILON) estimatedVelocity = EPSILON;
                    if (math.abs(prev.Velocity) < EPSILON) prev.Velocity = EPSILON;

                    // Compute curvature needed to match force vectors
                    node.Direction = math.mul(
                        math.mul(
                            quaternion.AxisAngle(prev.Lateral, normalForce / estimatedVelocity / hz),
                            quaternion.AxisAngle(prev.Normal, -lateralForce / prev.Velocity / hz)
                        ),
                        prev.Direction
                    );
                    node.Lateral = math.mul(
                        quaternion.AxisAngle(prev.Normal, -lateralForce / prev.Velocity / hz),
                        prev.Lateral
                    );
                    node.Normal = math.normalize(math.cross(node.Direction, node.Lateral));
                    node.Position += node.Direction * (node.Velocity / (2f * hz))
                        + prev.Direction * (node.Velocity / (2f * hz))
                        + (prev.GetHeartPosition(HEART) - node.GetHeartPosition(HEART));

                    // Apply roll
                    float deltaRoll = node.RollSpeed / hz;
                    quaternion rollQuat = quaternion.AxisAngle(node.Direction, math.radians(-deltaRoll));
                    node.Lateral = math.normalize(math.mul(rollQuat, node.Lateral));
                    node.Normal = math.normalize(math.cross(node.Direction, node.Lateral));
                    node.Roll = math.degrees(math.atan2(node.Lateral.y, -node.Normal.y));

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
                        node.Energy -= node.Velocity * node.Velocity * node.Velocity * RESISTANCE / hz;
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
                            + lateralAngle * node.Lateral * node.Velocity * hz / G
                            + normalAngle * node.Normal * node.HeartDistanceFromLast * hz * hz / G;
                    }
                    node.NormalForce = -math.dot(forceVec, node.Normal);
                    node.LateralForce = -math.dot(forceVec, node.Lateral);

                    Nodes[i] = node;
                }

                for (int i = start; i < end; i++) {
                    if ((i - start) % k == 0) continue;

                    int coarseIndex = i / k * k;
                    int nextCoarseIndex = math.min(coarseIndex + k, Nodes.Length - 1);
                    float t = (i - coarseIndex) / (float)k;

                    Node prev = Nodes[coarseIndex];
                    Node next = Nodes[nextCoarseIndex];
                    Node node = prev;

                    node.Position = math.lerp(prev.Position, next.Position, t);

                    Nodes[i] = node;
                }

                if (end == Nodes.Length) {
                    SolvedResolution.Value = k;
                }
            }

            private void UpdateAnchor() {
                Node anchor = Nodes[0];
                anchor.Velocity = 10f;
                anchor.Energy = 0.5f * anchor.Velocity * anchor.Velocity + G * anchor.GetHeartPosition(CENTER).y;
                Nodes[0] = anchor;
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
            };

            public override string ToString() {
                System.Text.StringBuilder sb = new();
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
                return sb.ToString();
            }
        }
    }
}
