using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


namespace CoasterForge {
    using static Constants;

    public class TestMono : UnityEngine.MonoBehaviour {
        public UnityEngine.AnimationCurve NormalForceCurve;
        public UnityEngine.AnimationCurve LateralForceCurve;
        public UnityEngine.AnimationCurve RollSpeedCurve;
        public float Duration = 10f;
        public bool FixedVelocity = false;

        private List<NodeMono> _nodesMono = new();
        private NativeArray<Node> _nodes;
        private NativeArray<float> _desiredNormalForces;
        private NativeArray<float> _desiredLateralForces;
        private NativeArray<float> _desiredRollSpeeds;
        private int _nodeCount;
        private bool _initialized = false;

        private void Initialize() {
            if (_initialized) {
                Dispose();
            }

            _nodeCount = (int)(HZ * Duration);
            _nodes = new NativeArray<Node>(_nodeCount, Allocator.Persistent);
            _desiredNormalForces = new NativeArray<float>(_nodeCount, Allocator.Persistent);
            _desiredLateralForces = new NativeArray<float>(_nodeCount, Allocator.Persistent);
            _desiredRollSpeeds = new NativeArray<float>(_nodeCount, Allocator.Persistent);
            for (int i = 0; i < _nodeCount; i++) {
                var node = Node.Default;
                _nodes[i] = node;
                var nodeMono = new UnityEngine.GameObject("Node").AddComponent<NodeMono>();
                nodeMono.transform.SetParent(transform);
                _nodesMono.Add(nodeMono);
            }
            _initialized = true;
        }

        private void Dispose() {
            if (!_initialized) return;
            _nodes.Dispose();
            _desiredNormalForces.Dispose();
            _desiredLateralForces.Dispose();
            _desiredRollSpeeds.Dispose();
            foreach (var nodeMono in _nodesMono) {
                Destroy(nodeMono.gameObject);
            }
            _nodesMono.Clear();
            _initialized = false;
        }

        private void Start() {
            Initialize();
        }

        private void OnDestroy() {
            Dispose();
        }

        private void Update() {
            int nodeCount = (int)(HZ * Duration);
            if (nodeCount != _nodeCount) {
                Initialize();
            }

            for (int i = 0; i < _nodeCount; i++) {
                _desiredNormalForces[i] = NormalForceCurve.Evaluate(i / (_nodeCount - 1f));
                _desiredLateralForces[i] = LateralForceCurve.Evaluate(i / (_nodeCount - 1f));
                _desiredRollSpeeds[i] = RollSpeedCurve.Evaluate(i / (_nodeCount - 1f));
            }

            Build();
        }

        private void Build() {
            for (int i = 0; i < _nodeCount; i++) {
                _nodesMono[i].Node = _nodes[i];
            }

            new BuildJob {
                Nodes = _nodes,
                DesiredNormalForces = _desiredNormalForces,
                DesiredLateralForces = _desiredLateralForces,
                DesiredRollSpeeds = _desiredRollSpeeds,
                FixedVelocity = FixedVelocity,
            }.Schedule().Complete();
        }

        [BurstCompile]
        private struct BuildJob : IJob {
            public NativeArray<Node> Nodes;

            [ReadOnly]
            public NativeArray<float> DesiredNormalForces;

            [ReadOnly]
            public NativeArray<float> DesiredLateralForces;

            [ReadOnly]
            public NativeArray<float> DesiredRollSpeeds;

            [ReadOnly]
            public bool FixedVelocity;

            public void Execute() {
                Node anchor = Nodes[0];
                anchor.Velocity = 10f;
                anchor.Energy = 0.5f * anchor.Velocity * anchor.Velocity + G * GetHeartPosition(anchor, 0.9f).y;
                Nodes[0] = anchor;

                for (int i = 1; i < Nodes.Length; i++) {
                    Node prev = Nodes[i - 1];
                    Node node = prev;

                    // Assign target constraints values
                    node.NormalForce = DesiredNormalForces[i];
                    node.LateralForce = DesiredLateralForces[i];
                    node.RollSpeed = DesiredRollSpeeds[i];

                    // Compute force vectors needed to achieve target forces
                    float3 forceVec = -node.NormalForce * prev.Normal - node.LateralForce * prev.Lateral + math.down();
                    float normalForce = -math.dot(forceVec, prev.Normal) * G;
                    float lateralForce = -math.dot(forceVec, prev.Lateral) * G;
                    float estimatedVelocity = math.abs(prev.HeartDistanceFromLast) < EPSILON ? prev.Velocity : prev.HeartDistanceFromLast * HZ;

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
                        + (GetHeartPosition(prev) - GetHeartPosition(node));

                    // Apply roll
                    float deltaRoll = node.RollSpeed / HZ;
                    quaternion rollQuat = quaternion.AxisAngle(node.Direction, math.radians(deltaRoll));
                    node.Lateral = math.normalize(math.mul(rollQuat, node.Lateral));
                    node.Normal = math.normalize(math.cross(node.Direction, node.Lateral));

                    // Compute node metrics
                    node.DistanceFromLast = math.distance(GetHeartPosition(node), GetHeartPosition(prev));
                    node.TotalLength += node.DistanceFromLast;
                    node.HeartDistanceFromLast = math.distance(node.Position, prev.Position);
                    node.TotalHeartLength += node.HeartDistanceFromLast;

                    float3 diff = node.Direction - prev.Direction;
                    if (math.length(diff) < EPSILON) {
                        node.PitchFromLast = 0f;
                        node.YawFromLast = 0f;
                    }
                    else {
                        node.PitchFromLast = (GetPitch(node) - GetPitch(prev) + 540) % 360 - 180;
                        node.YawFromLast = (GetYaw(node) - GetYaw(prev) + 540) % 360 - 180;
                    }
                    float yawScaleFactor = math.cos(math.abs(math.radians(GetPitch(node))));
                    node.AngleFromLast = math.sqrt(yawScaleFactor * yawScaleFactor * node.YawFromLast * node.YawFromLast + node.PitchFromLast * node.PitchFromLast);

                    float pe = G * (GetHeartPosition(node, 0.9f).y + node.TotalLength * FRICTION);
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

                    Nodes[i] = node;
                }
            }

            private float GetPitch(Node node) {
                float magnitude = math.sqrt(node.Direction.x * node.Direction.x + node.Direction.z * node.Direction.z);
                return math.degrees(math.atan2(node.Direction.y, magnitude));
            }

            private float GetYaw(Node node) {
                return math.degrees(math.atan2(-node.Direction.x, -node.Direction.z));
            }

            private float3 GetHeartPosition(Node node, float multiplier = 1f) {
                return node.Position + node.Normal * HEART * multiplier;
            }
        }

        private void OnDrawGizmos() {
            UnityEngine.Gizmos.color = UnityEngine.Color.red;
            for (int i = 0; i < _nodes.Length; i++) {
                if (i % 100 != 0) continue;
                var node = _nodes[i];
                UnityEngine.Gizmos.DrawSphere(node.Position, 0.1f);
                UnityEngine.Gizmos.DrawLine(node.Position, node.Position + node.Direction);
            }
        }
    }
}
