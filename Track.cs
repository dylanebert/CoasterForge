using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CoasterForge.Constants;

namespace CoasterForge {
    public class Track : MonoBehaviour {
        public List<Keyframe> NormalForceCurve;
        public List<Keyframe> LateralForceCurve;
        public List<Keyframe> RollSpeedCurve;
        public float Duration = 10f;
        public bool FixedVelocity;

#if UNITY_EDITOR
        public AnimationCurve NormalForceCurveEditor;
        public AnimationCurve LateralForceCurveEditor;
        public AnimationCurve RollSpeedCurveEditor;
#endif

        private NativeArray<Node> _nodes;
        private NativeArray<Node> _prevNodes;
        private NativeArray<float> _normalForces;
        private NativeArray<float> _lateralForces;
        private NativeArray<float> _rollSpeeds;
        private JobHandle _jobHandle;
        private int _nodeCount;
        private int _refinement;
        private int _solvedK = -1;
        private bool _dirty;
        private bool _initialized;

        public NativeArray<Node> Nodes => _prevNodes;
        public int NodeCount => _nodeCount;
        public int SolvedK => _solvedK;

        private void Initialize() {
            if (_initialized) {
                Dispose();
            }

            _nodeCount = (int)(HZ * Duration);
            _nodes = new NativeArray<Node>(_nodeCount, Allocator.Persistent);
            _prevNodes = new NativeArray<Node>(_nodeCount, Allocator.Persistent);
            _normalForces = new NativeArray<float>(_nodeCount, Allocator.Persistent);
            _lateralForces = new NativeArray<float>(_nodeCount, Allocator.Persistent);
            _rollSpeeds = new NativeArray<float>(_nodeCount, Allocator.Persistent);
            for (int i = 0; i < _nodeCount; i++) {
                var node = Node.Default;
                _nodes[i] = node;
            }
            _initialized = true;
            _dirty = true;
        }

        private void Dispose() {
            if (!_initialized) return;
            _nodes.Dispose();
            _prevNodes.Dispose();
            _normalForces.Dispose();
            _lateralForces.Dispose();
            _rollSpeeds.Dispose();
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
            Build();
        }

        private void UpdateFunctions() {
            var normalForceCurve = new NativeList<Keyframe>(NormalForceCurve.Count, Allocator.TempJob);
            var lateralForceCurve = new NativeList<Keyframe>(LateralForceCurve.Count, Allocator.TempJob);
            var rollSpeedCurve = new NativeList<Keyframe>(RollSpeedCurve.Count, Allocator.TempJob);
            for (int i = 0; i < NormalForceCurve.Count; i++) {
                normalForceCurve.Add(NormalForceCurve[i]);
            }
            for (int i = 0; i < LateralForceCurve.Count; i++) {
                lateralForceCurve.Add(LateralForceCurve[i]);
            }
            for (int i = 0; i < RollSpeedCurve.Count; i++) {
                rollSpeedCurve.Add(RollSpeedCurve[i]);
            }
            new UpdateFunctionsJob {
                NormalForces = _normalForces,
                LateralForces = _lateralForces,
                RollSpeeds = _rollSpeeds,
                NormalForceCurve = normalForceCurve,
                LateralForceCurve = lateralForceCurve,
                RollSpeedCurve = rollSpeedCurve,
            }.Schedule(_nodeCount, _nodeCount / 16).Complete();
            normalForceCurve.Dispose();
            lateralForceCurve.Dispose();
            rollSpeedCurve.Dispose();
        }

#if UNITY_EDITOR
        public void UpdateEditorCurves() {
            NormalForceCurveEditor.ClearKeys();
            LateralForceCurveEditor.ClearKeys();
            RollSpeedCurveEditor.ClearKeys();
            int nodeCount = (int)(HZ * Duration);
            for (int i = 0; i < nodeCount; i += 100) {
                float t = i / HZ;
                float normalForce = NormalForceCurve.Evaluate(t);
                float lateralForce = LateralForceCurve.Evaluate(t);
                float rollSpeed = RollSpeedCurve.Evaluate(t);
                NormalForceCurveEditor.AddKey(t, normalForce);
                LateralForceCurveEditor.AddKey(t, lateralForce);
                RollSpeedCurveEditor.AddKey(t, rollSpeed);
            }
        }
#endif

        public void MarkDirty() {
            _dirty = true;
        }

        public void Build() {
            _jobHandle.Complete();

            if (Duration < 0.01f) {
                Debug.LogWarning("Duration is too short");
                return;
            }

            int nodeCount = (int)(HZ * Duration);
            if (nodeCount != _nodeCount) {
                Initialize();
            }

            if (_dirty) {
                UpdateFunctions();
                _refinement = 0;
                _solvedK = -1;
                _dirty = false;
            }

            _prevNodes.CopyFrom(_nodes);

            (int start, int end, int k, float hz) = NextChunk();

            _jobHandle = new BuildJob {
                Nodes = _nodes,
                NormalForces = _normalForces,
                LateralForces = _lateralForces,
                RollSpeeds = _rollSpeeds,
                StartIndex = start,
                EndIndex = end,
                K = k,
                HZ = hz,
                FixedVelocity = FixedVelocity,
            }.Schedule();
        }

        private (int, int, int, float) NextChunk() {
            var refinement = new NativeReference<int>(Allocator.TempJob) { Value = _refinement };
            var solvedK = new NativeReference<int>(Allocator.TempJob) { Value = _solvedK };

            var startReference = new NativeReference<int>(Allocator.TempJob);
            var endReference = new NativeReference<int>(Allocator.TempJob);
            var kReference = new NativeReference<int>(Allocator.TempJob);
            var hzReference = new NativeReference<float>(Allocator.TempJob);

            new ComputeChunkJob {
                Refinement = refinement,
                SolvedK = solvedK,
                Start = startReference,
                End = endReference,
                K = kReference,
                HZ = hzReference,
                NodeCount = _nodeCount,
            }.Schedule().Complete();

            _refinement = refinement.Value;
            _solvedK = solvedK.Value;

            int start = startReference.Value;
            int end = endReference.Value;
            int k = kReference.Value;
            float hz = hzReference.Value;

            refinement.Dispose();
            solvedK.Dispose();

            startReference.Dispose();
            endReference.Dispose();
            kReference.Dispose();
            hzReference.Dispose();

            return (start, end, k, hz);
        }

        private void OnDrawGizmos() {
            for (int i = 0; i < _prevNodes.Length; i++) {
                if (i % 100 != 0) continue;
                var node = _prevNodes[i];
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
        private struct UpdateFunctionsJob : IJobParallelFor {
            [WriteOnly]
            public NativeArray<float> NormalForces;

            [WriteOnly]
            public NativeArray<float> LateralForces;

            [WriteOnly]
            public NativeArray<float> RollSpeeds;

            [ReadOnly]
            public NativeList<Keyframe> NormalForceCurve;

            [ReadOnly]
            public NativeList<Keyframe> LateralForceCurve;

            [ReadOnly]
            public NativeList<Keyframe> RollSpeedCurve;

            public void Execute(int index) {
                float t = index / HZ;
                NormalForces[index] = NormalForceCurve.Evaluate(t);
                LateralForces[index] = LateralForceCurve.Evaluate(t);
                RollSpeeds[index] = RollSpeedCurve.Evaluate(t);
            }
        }

        [BurstCompile]
        private struct ComputeChunkJob : IJob {
            public NativeReference<int> Refinement;
            public NativeReference<int> SolvedK;

            [WriteOnly]
            public NativeReference<int> Start;

            [WriteOnly]
            public NativeReference<int> End;

            [WriteOnly]
            public NativeReference<int> K;

            [WriteOnly]
            public NativeReference<float> HZ;

            [ReadOnly]
            public int NodeCount;

            public void Execute() {
                const int fineNodesPerFrame = 10000;

                int refinement = Refinement.Value;
                int maxK = 1;
                int levels = (int)math.floor(math.log2(NodeCount / (float)fineNodesPerFrame));
                bool solved = false;
                if (levels > 0) {
                    maxK = 1 << levels;
                    int min = (1 << levels) - 1;
                    int max = (1 << (levels + 1)) - 1;
                    solved = refinement >= max;
                    if (refinement > min) {
                        refinement = min + (refinement - min) % (max - min);
                    }
                }
                else {
                    solved = refinement >= 1;
                }

                int groupIndex = 0;
                int remaining = refinement;
                int groupLevels = 1;
                while (remaining >= groupLevels) {
                    remaining -= groupLevels;
                    groupIndex++;
                    groupLevels *= 2;
                }

                int k = maxK >> groupIndex;
                k = math.max(k, 1);
                int solvedK = solved ? 1 : k * 2;

                SolvedK.Value = SolvedK.Value < 0 ? solvedK : math.min(solvedK, SolvedK.Value);
                Refinement.Value++;

                int nodesPerGroup = (int)math.ceil(NodeCount / (float)groupLevels);
                int start = nodesPerGroup * remaining;
                int end = math.min(start + nodesPerGroup, NodeCount);
                start = math.max(0, start - k);
                float hz = Constants.HZ / k;

                Start.Value = start;
                End.Value = end;
                K.Value = k;
                HZ.Value = hz;
            }
        }

        [BurstCompile]
        private struct BuildJob : IJob {
            public NativeArray<Node> Nodes;

            [ReadOnly]
            public NativeArray<float> NormalForces;

            [ReadOnly]
            public NativeArray<float> LateralForces;

            [ReadOnly]
            public NativeArray<float> RollSpeeds;

            [ReadOnly]
            public int StartIndex;

            [ReadOnly]
            public int EndIndex;

            [ReadOnly]
            public int K;

            [ReadOnly]
            public float HZ;

            [ReadOnly]
            public bool FixedVelocity;

            public void Execute() {
                UpdateAnchor();

                for (int i = StartIndex + K; i < EndIndex; i += K) {
                    Node prev = Nodes[i - K];
                    Node node = prev;

                    // Assign target constraints values
                    node.NormalForce = NormalForces[i];
                    node.LateralForce = LateralForces[i];
                    node.RollSpeed = RollSpeeds[i];

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
                    float deltaRoll = node.RollSpeed / HZ;
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

                    Nodes[i] = node;
                }

                for (int i = StartIndex; i < EndIndex; i++) {
                    if ((i - StartIndex) % K == 0) continue;

                    int coarseIndex = i / K * K;
                    int nextCoarseIndex = math.min(coarseIndex + K, Nodes.Length - 1);
                    float t = (i - coarseIndex) / (float)K;

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
                sb.AppendLine($"TieDistance: {TieDistance}");
                return sb.ToString();
            }
        }

        [System.Serializable]
        public struct Keyframe {
            public float Time;
            public float Value;
        }
    }
}
