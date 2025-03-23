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
        public bool ShouldUpdateFunctions = false;
        public bool Debug = false;

        private List<NodeMono> _nodesMono = new();
        private NativeArray<Node> _nodes;
        private NativeArray<Node> _prevNodes;
        private NativeArray<float> _desiredNormalForces;
        private NativeArray<float> _desiredLateralForces;
        private NativeArray<float> _desiredRollSpeeds;
        private JobHandle _jobHandle;
        private int _nodeCount;
        private int _refinement;
        private bool _initialized = false;

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
            for (int i = 0; i < _nodeCount; i++) {
                var node = Node.Default;
                _nodes[i] = node;

                if (Debug) {
                    var nodeMono = new UnityEngine.GameObject("Node").AddComponent<NodeMono>();
                    nodeMono.transform.SetParent(transform);
                    _nodesMono.Add(nodeMono);
                }
            }
            UpdateFunctions();
            _refinement = 0;
            _initialized = true;
        }

        private void Dispose() {
            if (!_initialized) return;
            _nodes.Dispose();
            _prevNodes.Dispose();
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
            _jobHandle.Complete();
            Dispose();
        }

        private void Update() {
            _jobHandle.Complete();

            int nodeCount = (int)(HZ * Duration);
            if (nodeCount != _nodeCount) {
                Initialize();
            }
            else if (ShouldUpdateFunctions) {
                UpdateFunctions();
                _refinement = 0;
            }

            _prevNodes.CopyFrom(_nodes);

            if (Debug) {
                for (int i = 0; i < _nodeCount; i++) {
                    _nodesMono[i].Node = _nodes[i];
                    _nodesMono[i].transform.position = _nodes[i].Position;
                }
            }

            _jobHandle = new BuildJob {
                Nodes = _nodes,
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
            public int Refinement;

            [ReadOnly]
            public bool FixedVelocity;

            public void Execute() {
                UpdateAnchor();

                const int fineNodesPerFrame = 1000;
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
                    quaternion rollQuat = quaternion.AxisAngle(node.Direction, math.radians(deltaRoll));
                    node.Lateral = math.normalize(math.mul(rollQuat, node.Lateral));
                    node.Normal = math.normalize(math.cross(node.Direction, node.Lateral));

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
            }

            private void UpdateAnchor() {
                Node anchor = Nodes[0];
                anchor.Velocity = 10f;
                anchor.Energy = 0.5f * anchor.Velocity * anchor.Velocity + G * anchor.GetHeartPosition(CENTER).y;
                Nodes[0] = anchor;
            }
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
            }
        }
    }
}
