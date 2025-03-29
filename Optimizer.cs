using System.Text;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CoasterForge {
    public class Optimizer : MonoBehaviour {
        private const int NUM_TRACKS = 6; // 6 gradients

        public Track Track;
        public Transform ControlPoint;
        public float LearningRate = 0.001f;
        public float GradientClipThreshold = 1f;
        public float Epsilon = 0.001f;
        public float LossThreshold = 0.001f;
        public float Beta1 = 0.9f;
        public float Beta2 = 0.999f;
        public float EpsilonAdam = 1e-8f;
        public float NoiseScale = 0.01f;
        public float LearningRateDecay = 0.995f;

        private Track[] _tracks;
        private float _learningRate;
        private float _mt, _vt;
        private float _mnF, _vnF;
        private float _mrS, _vrS;

        private float3 _previousControlPointPosition;
        private int _t;

        private void Start() {
            _tracks = new Track[NUM_TRACKS];
            for (int i = 0; i < NUM_TRACKS; i++) {
                _tracks[i] = Instantiate(Track, transform);
            }

            Initialize();
        }

        private void OnDestroy() {
            foreach (var track in _tracks) {
                Destroy(track.gameObject);
            }
        }

        private void Initialize() {
            Track.Keyframes[^1] = Track.Keyframe.Default;
            _learningRate = LearningRate;
            _mt = _vt = 0f;
            _mnF = _vnF = 0f;
            _mrS = _vrS = 0f;
            _t = 0;
            _previousControlPointPosition = ControlPoint.position;
        }

        private void Update() {
            CheckReinitialize();

            var gradient = ComputeGradient();

            var keyframe = Track.Keyframes[^1];
            _t++;
            _learningRate *= LearningRateDecay;

            UpdateParameter(ref keyframe.Time, gradient.Time, ref _mt, ref _vt, _learningRate);
            UpdateParameter(ref keyframe.NormalForce, gradient.NormalForce, ref _mnF, ref _vnF, _learningRate);
            UpdateParameter(ref keyframe.RollSpeed, gradient.RollSpeed, ref _mrS, ref _vrS, _learningRate);

            Track.Keyframes[^1] = keyframe;
            Track.MarkDirty();
        }

        private void CheckReinitialize() {
            if (math.distancesq(_previousControlPointPosition, ControlPoint.position) > 1f) {
                Initialize();
            }
        }

        public void LogLoss() {
            ComputeLoss(Track, ControlPoint.position, true);
        }

        private void UpdateParameter(ref float param, float gradient, ref float m, ref float v, float rate) {
            gradient = math.clamp(gradient, -GradientClipThreshold, GradientClipThreshold);
            m = Beta1 * m + (1 - Beta1) * gradient;
            v = Beta2 * v + (1 - Beta2) * gradient * gradient;
            float m_hat = m / (1 - math.pow(Beta1, _t));
            float v_hat = v / (1 - math.pow(Beta2, _t));
            param -= rate * m_hat / (math.sqrt(v_hat) + EpsilonAdam);
        }

        private Gradient ComputeGradient() {
            var originalKeyframe = Track.Keyframes[^1];

            var timePlusKeyframe = originalKeyframe;
            timePlusKeyframe.Time += Epsilon;
            _tracks[0].Keyframes[^1] = timePlusKeyframe;

            var timeMinusKeyframe = originalKeyframe;
            timeMinusKeyframe.Time -= Epsilon;
            _tracks[1].Keyframes[^1] = timeMinusKeyframe;

            var normalForcePlusKeyframe = originalKeyframe;
            normalForcePlusKeyframe.NormalForce += Epsilon;
            _tracks[2].Keyframes[^1] = normalForcePlusKeyframe;

            var normalForceMinusKeyframe = originalKeyframe;
            normalForceMinusKeyframe.NormalForce -= Epsilon;
            _tracks[3].Keyframes[^1] = normalForceMinusKeyframe;

            var rollSpeedPlusKeyframe = originalKeyframe;
            rollSpeedPlusKeyframe.RollSpeed += Epsilon;
            _tracks[4].Keyframes[^1] = rollSpeedPlusKeyframe;

            var rollSpeedMinusKeyframe = originalKeyframe;
            rollSpeedMinusKeyframe.RollSpeed -= Epsilon;
            _tracks[5].Keyframes[^1] = rollSpeedMinusKeyframe;

            Simulate().Complete();

            float timeLossPlus = ComputeLoss(_tracks[0], ControlPoint.position);
            float timeLossMinus = ComputeLoss(_tracks[1], ControlPoint.position);

            float normalForceLossPlus = ComputeLoss(_tracks[2], ControlPoint.position);
            float normalForceLossMinus = ComputeLoss(_tracks[3], ControlPoint.position);

            float rollSpeedLossPlus = ComputeLoss(_tracks[4], ControlPoint.position);
            float rollSpeedLossMinus = ComputeLoss(_tracks[5], ControlPoint.position);

            var gradient = new Gradient {
                Time = (timeLossPlus - timeLossMinus) / (2 * Epsilon),
                NormalForce = (normalForceLossPlus - normalForceLossMinus) / (2 * Epsilon),
                RollSpeed = (rollSpeedLossPlus - rollSpeedLossMinus) / (2 * Epsilon),
            };

            return gradient;
        }

        private float ComputeLoss(Track track, float3 controlPoint, bool log = false) {
            int nodeCount = track.NodeCount;
            var nodes = track.Nodes;

            float positionLoss = math.distancesq(nodes[nodeCount - 1].Position, controlPoint);

            if (log) {
                StringBuilder sb = new();
                sb.AppendLine($"positionLoss: {positionLoss}");
                Debug.Log(sb.ToString());
            }

            return positionLoss;
        }

        private JobHandle Simulate() {
            /* 
            Compute track nodes from duration, normal forces, and roll speeds
            Nodes are indexed by time, one node every 0.001s
            The nodes pass through the rider center, not the track
            The term "heart" refers to an offset from the rider center, e.g. the actual track
            Each node contains:
            Position: World space [x, y, z] in meters
            Direction: Node forward vector
            Lateral: Node right vector
            Normal: Node up vector
            Roll: Banking in degrees
            Velocity: m/s
            Energy: Potential + kinetic
            NormalForce: gs
            LateralForce: gs
            RollSpeed: deg/s
            DistanceFromLast: Distance from position to previous position
            HeartDistanceFromLast: Distance from track position to previous track position
            AngleFromLast: Combined angle from previous in degrees
            PitchFromLast: Pitch from previous in degrees
            YawFromLast: Yaw from previous in degrees
            RollSpeed: deg/s
            TotalLength: Cumulative length of the nodes
            TotalHeartLength: Cumulative length of the actual track
            */
            NativeArray<JobHandle> jobHandles = new(_tracks.Length, Allocator.Temp);
            for (int i = 0; i < _tracks.Length; i++) {
                jobHandles[i] = _tracks[i].Build(true);
            }
            var combinedHandle = JobHandle.CombineDependencies(jobHandles);
            jobHandles.Dispose();
            return combinedHandle;
        }

        private struct Gradient {
            public float Time;
            public float NormalForce;
            public float RollSpeed;
        }
    }
}
