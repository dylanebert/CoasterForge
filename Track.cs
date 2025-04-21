using System.Collections.Generic;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CoasterForge.Constants;

namespace CoasterForge {
    public class Track : MonoBehaviour {
        public List<Function> Functions;
        public bool FixedVelocity;
        public bool Autobuild;

#if UNITY_EDITOR
        public AnimationCurve NormalForceCurveEditor;
        public AnimationCurve LateralForceCurveEditor;
        public AnimationCurve RollSpeedCurveEditor;
#endif

        private NativeArray<Node> _nodesRW;
        private NativeArray<Node> _nodesRO;
        private NativeArray<Keyframe> _curve;

        private NativeReference<int> _nodeCountRW;
        private NativeReference<int> _nodeCountRO;

        private JobHandle _jobHandle;
        private int _capacity;
        private bool _dirty;
        private bool _initialized;

        public NativeArray<Node> Nodes => _nodesRO;
        public int NodeCount => _nodeCountRO.Value;

        private void Initialize() {
            if (_initialized) {
                Dispose();
            }

            while (_capacity < _nodeCountRO.Value) {
                _capacity *= 2;
            }

            _nodesRW = new NativeArray<Node>(_capacity, Allocator.Persistent);
            _nodesRO = new NativeArray<Node>(_capacity, Allocator.Persistent);
            _curve = new NativeArray<Keyframe>(_capacity, Allocator.Persistent);

            new InitializeJob {
                Nodes = _nodesRW,
            }.Schedule(_capacity, _capacity / 16).Complete();

            _initialized = true;
            _dirty = true;
        }

        private void Dispose() {
            if (!_initialized) return;
            _nodesRW.Dispose();
            _nodesRO.Dispose();
            _curve.Dispose();
            _initialized = false;
        }

        private void Start() {
            _nodeCountRW = new NativeReference<int>(Allocator.Persistent);
            _nodeCountRO = new NativeReference<int>(Allocator.Persistent);

            if (Functions.Count == 0) {
                Functions.Add(Function.Default);
            }

            float duration = GetDuration();
            _nodeCountRW.Value = (int)(HZ * duration);
            _capacity = 65536;
            Initialize();
        }

        private void OnDestroy() {
            _jobHandle.Complete();
            Dispose();

            _nodeCountRW.Dispose();
            _nodeCountRO.Dispose();
        }

        private void Update() {
            if (Autobuild) {
                Build();
            }
        }

#if UNITY_EDITOR
        public void UpdateEditorCurves() {
            NormalForceCurveEditor.ClearKeys();
            LateralForceCurveEditor.ClearKeys();
            RollSpeedCurveEditor.ClearKeys();

            UpdateFunctionStarts();

            var functions = new NativeArray<Function>(Functions.Count, Allocator.TempJob);
            for (int i = 0; i < Functions.Count; i++) {
                functions[i] = Functions[i];
            }

            float duration = GetDuration();
            int nodeCount = (int)(HZ * duration);
            var nodeCountRef = new NativeReference<int>(Allocator.TempJob) { Value = nodeCount };
            var curve = new NativeArray<Keyframe>(nodeCount, Allocator.TempJob);

            new ComputeCurveJob {
                Curve = curve,
                Functions = functions,
                NodeCount = nodeCountRef,
            }.Run();

            for (int i = 0; i < nodeCount; i += 50) {
                var point = curve[i];

                var normalKey = new UnityEngine.Keyframe(point.Time, point.NormalForce) { weightedMode = WeightedMode.None };
                var lateralKey = new UnityEngine.Keyframe(point.Time, point.LateralForce) { weightedMode = WeightedMode.None };
                var rollKey = new UnityEngine.Keyframe(point.Time, point.RollSpeed) { weightedMode = WeightedMode.None };

                NormalForceCurveEditor.AddKey(normalKey);
                LateralForceCurveEditor.AddKey(lateralKey);
                RollSpeedCurveEditor.AddKey(rollKey);
            }

            functions.Dispose();
            nodeCountRef.Dispose();
            curve.Dispose();
        }
#endif

        public void MarkDirty() {
            _dirty = true;
        }

        public JobHandle Build(bool force = false) {
            _jobHandle.Complete();

            if (!force) {
                new SafeCopyJob {
                    NodesRO = _nodesRO,
                    NodesRW = _nodesRW,
                }.Schedule(_nodeCountRW.Value, _nodeCountRW.Value / 16).Complete();
                _nodeCountRO.Value = _nodeCountRW.Value;
            }

            float duration = GetDuration();

            if (duration < 0.01f) {
                return default;
            }

            _nodeCountRW.Value = (int)(HZ * duration);
            if (_nodeCountRW.Value > _capacity) {
                Initialize();
            }

            if (_dirty || force) {
                UpdateFunctionStarts();

                var functions = new NativeArray<Function>(Functions.Count, Allocator.TempJob);
                for (int i = 0; i < Functions.Count; i++) {
                    functions[i] = Functions[i];
                }
                _jobHandle = new ComputeCurveJob {
                    Curve = _curve,
                    Functions = functions,
                    NodeCount = _nodeCountRW,
                }.Schedule(_jobHandle);
                _jobHandle = functions.Dispose(_jobHandle);

                _dirty = false;
            }

            _jobHandle = new BuildJob {
                Nodes = _nodesRW,
                Curve = _curve,
                FixedVelocity = FixedVelocity,
            }.Schedule(_jobHandle);

            if (force) {
                _jobHandle = new CopyJob {
                    NodesRO = _nodesRO,
                    NodeCountRO = _nodeCountRO,
                    NodesRW = _nodesRW,
                    NodeCountRW = _nodeCountRW,
                }.Schedule(_jobHandle);
            }

            return _jobHandle;
        }

        public float GetDuration() {
            float duration = 0f;
            for (int i = 0; i < Functions.Count; i++) {
                duration += Functions[i].Duration;
            }
            return duration;
        }

        private void UpdateFunctionStarts() {
            for (int i = 1; i < Functions.Count; i++) {
                var prevFunc = Functions[i - 1];
                var func = Functions[i];

                func.StartNormalForce = prevFunc.GetEndNormalForce();
                func.StartLateralForce = prevFunc.GetEndLateralForce();
                func.StartRollSpeed = prevFunc.GetEndRollSpeed();

                Functions[i] = func;
            }
        }

        private void OnDrawGizmos() {
            if (!_initialized) return;
            int nodeCount = _nodeCountRO.Value;
            for (int i = 0; i < nodeCount; i++) {
                if (i % 100 != 0) continue;
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
        private struct InitializeJob : IJobParallelFor {
            [WriteOnly]
            public NativeArray<Node> Nodes;

            public void Execute(int index) {
                Nodes[index] = Node.Default;
            }
        }

        [BurstCompile]
        private struct CopyJob : IJob {
            [WriteOnly]
            public NativeArray<Node> NodesRO;

            [WriteOnly]
            public NativeReference<int> NodeCountRO;

            [ReadOnly]
            public NativeArray<Node> NodesRW;

            [ReadOnly]
            public NativeReference<int> NodeCountRW;

            public void Execute() {
                int nodeCount = NodeCountRW.Value;
                for (int i = 0; i < nodeCount; i++) {
                    NodesRO[i] = NodesRW[i];
                }
                NodeCountRO.Value = NodeCountRW.Value;
            }
        }

        [BurstCompile]
        private struct SafeCopyJob : IJobParallelFor {
            [WriteOnly]
            public NativeArray<Node> NodesRO;

            [ReadOnly]
            public NativeArray<Node> NodesRW;

            public void Execute(int index) {
                NodesRO[index] = NodesRW[index];
            }
        }

        [BurstCompile]
        private struct ComputeCurveJob : IJob {
            [WriteOnly]
            public NativeArray<Keyframe> Curve;

            [ReadOnly]
            public NativeArray<Function> Functions;

            [ReadOnly]
            public NativeReference<int> NodeCount;

            public void Execute() {
                float normalForce = Functions[0].StartNormalForce;
                float lateralForce = Functions[0].StartLateralForce;
                float rollSpeed = Functions[0].StartRollSpeed;

                float totalTime = 0f;
                int currentFunction = 0;

                for (int i = 0; i < NodeCount.Value; i++) {
                    float t = i / HZ;

                    while (currentFunction < Functions.Length - 1 &&
                        totalTime + Functions[currentFunction].Duration < t) {
                        totalTime += Functions[currentFunction].Duration;
                        currentFunction++;
                    }

                    if (currentFunction < Functions.Length) {
                        var func = Functions[currentFunction];
                        float segmentT = (t - totalTime) / func.Duration;

                        float normalT = ComputeInterpolation(segmentT);
                        normalForce = func.StartNormalForce + func.NormalForceAmplitude * normalT;

                        float lateralT = ComputeInterpolation(segmentT);
                        lateralForce = func.StartLateralForce + func.LateralForceAmplitude * lateralT;

                        float rollT = ComputeInterpolation(segmentT);
                        rollSpeed = func.StartRollSpeed + func.RollSpeedAmplitude * rollT;
                    }

                    Curve[i] = new Keyframe {
                        Time = t,
                        NormalForce = normalForce,
                        LateralForce = lateralForce,
                        RollSpeed = rollSpeed,
                    };
                }
            }

            private float ComputeInterpolation(float t) {
                return t * t * (3f - 2f * t);
            }
        }

        [BurstCompile]
        private struct BuildJob : IJob {
            public NativeArray<Node> Nodes;

            [ReadOnly]
            public NativeArray<Keyframe> Curve;

            [ReadOnly]
            public bool FixedVelocity;

            public void Execute() {
                UpdateAnchor();

                for (int i = 1; i < Nodes.Length; i++) {
                    Node prev = Nodes[i - 1];
                    Node node = prev;

                    // Assign target constraints values
                    Keyframe keyframe = Curve[i];
                    node.NormalForce = keyframe.NormalForce;
                    node.LateralForce = keyframe.LateralForce;
                    node.RollSpeed = keyframe.RollSpeed * 100f;

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
        public struct Function {
            public float Duration;

            [Header("Normal Force")]
            public float StartNormalForce;
            public float NormalForceAmplitude;

            [Header("Lateral Force")]
            public float StartLateralForce;
            public float LateralForceAmplitude;

            [Header("Roll Speed")]
            public float StartRollSpeed;
            public float RollSpeedAmplitude;

            public static Function Default => new() {
                Duration = 0.1f,
                StartNormalForce = 1f,
                NormalForceAmplitude = 0f,
                StartLateralForce = 0f,
                LateralForceAmplitude = 0f,
                StartRollSpeed = 0f,
                RollSpeedAmplitude = 0f,
            };

            public float GetMaxNormalForce() {
                return math.max(StartNormalForce, StartNormalForce + NormalForceAmplitude);
            }

            public float GetMinNormalForce() {
                return math.min(StartNormalForce, StartNormalForce + NormalForceAmplitude);
            }

            public float GetMaxLateralForce() {
                return math.max(StartLateralForce, StartLateralForce + LateralForceAmplitude);
            }

            public float GetMinLateralForce() {
                return math.min(StartLateralForce, StartLateralForce + LateralForceAmplitude);
            }

            public float GetMaxRollSpeed() {
                return math.max(StartRollSpeed, StartRollSpeed + RollSpeedAmplitude);
            }

            public float GetMinRollSpeed() {
                return math.min(StartRollSpeed, StartRollSpeed + RollSpeedAmplitude);
            }

            public float GetEndNormalForce() {
                return StartNormalForce + NormalForceAmplitude;
            }

            public float GetEndLateralForce() {
                return StartLateralForce + LateralForceAmplitude;
            }

            public float GetEndRollSpeed() {
                return StartRollSpeed + RollSpeedAmplitude;
            }

            public override string ToString() {
                StringBuilder sb = new();
                sb.AppendLine($"Duration: {Duration}");
                sb.AppendLine($"NormalForceAmplitude: {NormalForceAmplitude}");
                sb.AppendLine($"LateralForceAmplitude: {LateralForceAmplitude}");
                sb.AppendLine($"RollSpeedAmplitude: {RollSpeedAmplitude}");
                return sb.ToString();
            }
        }

        public struct Keyframe {
            public float Time;
            public float NormalForce;
            public float LateralForce;
            public float RollSpeed;
        }
    }
}
