using System.Text;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CoasterForge.ControlPoint;
using static CoasterForge.Track;

namespace CoasterForge {
    public class Optimizer : MonoBehaviour {
        private const int N_GRADIENTS = 3 * 2;
        private const int N_INITIALIZATIONS = 1;
        private const int N_TRACKS = N_GRADIENTS * N_INITIALIZATIONS;

        private const float LEARNING_RATE = 0.1f; // Initial learning rate
        private const float MIN_LEARNING_RATE = 1e-4f; // Minimum learning rate
        private const float EPSILON = 0.001f; // Gradient step size
        private const float BETA1 = 0.9f; // Exponential decay rate for first moment
        private const float BETA2 = 0.999f; // Exponential decay rate for second moment
        private const float EPSILON_ADAM = 1e-8f; // Small constant to prevent division by zero
        private const float LEARNING_RATE_DECAY = 0.995f; // Decay rate for learning rate
        private const float DECAY_THRESHOLD = 0.001f; // Minimum loss increase to apply decay

        public Track Track;
        public ControlPoint ControlPoint;

        private Track[] _tracks;
        private Function[] _functions;
        private Gradient[] _gradients;
        private float[] _losses;

        private float _learningRate;
        private float _loss;
        private float _mt, _vt;
        private float _mnF, _vnF;
        private float _mrS, _vrS;

        private int _t;

        private void Start() {
            _tracks = new Track[N_TRACKS];
            _functions = new Function[N_INITIALIZATIONS];
            _gradients = new Gradient[N_INITIALIZATIONS];
            _losses = new float[N_INITIALIZATIONS];

            for (int i = 0; i < N_TRACKS; i++) {
                int initialization = i / N_GRADIENTS;
                int gradient = i % N_GRADIENTS;
                _tracks[i] = Instantiate(Track, transform);
                _tracks[i].name = $"Initialization {initialization} Gradient {gradient}";
                _tracks[i].Autobuild = false;
            }

            Initialize();
        }

        private void OnDestroy() {
            foreach (var track in _tracks) {
                Destroy(track.gameObject);
            }
        }

        public void AddControlPoint() {
            var newFunction = Function.Default;

            Track.Functions.Add(newFunction);
            for (int i = 0; i < N_TRACKS; i++) {
                _tracks[i].Functions[^1] = Track.Functions[^2];
                _tracks[i].Functions.Add(newFunction);
            }

            Initialize();
        }

        public void RemoveControlPoint() {
            if (Track.Functions.Count <= 1) {
                Debug.LogWarning("Cannot remove last control point");
                return;
            }

            Track.Functions.RemoveAt(Track.Functions.Count - 1);
            for (int i = 0; i < N_TRACKS; i++) {
                _tracks[i].Functions.RemoveAt(_tracks[i].Functions.Count - 1);
            }

            ResetControlPoint();
            Initialize();
        }

        private void ResetControlPoint() {
            Track.Build(true).Complete();

            var node = Track.Nodes[Track.NodeCount - 1];
            ControlPoint.transform.position = node.Position;
        }

        private void Initialize() {
            for (int i = 0; i < N_INITIALIZATIONS; i++) {
                _functions[i] = Function.Default;
                _losses[i] = float.MaxValue;
            }
            _learningRate = LEARNING_RATE;
            _loss = float.MaxValue;
            _mt = _vt = 0f;
            _mnF = _vnF = 0f;
            _mrS = _vrS = 0f;
            _t = 0;
        }

        private void Update() {
            if (ControlPoint.Dirty) {
                Initialize();
                ControlPoint.Dirty = false;
            }

            ComputeGradients();

            float loss = 0f;
            float bestLoss = float.MaxValue;
            int bestIndex = 0;

            _t++;

            for (int i = 0; i < N_INITIALIZATIONS; i++) {
                if (ControlPoint.Mode == ConstraintMode.Duration) {
                    _functions[i].Duration = ControlPoint.TargetDuration;
                }
                else {
                    UpdateParameter(ref _functions[i].Duration, _gradients[i].Duration, ref _mt, ref _vt, _learningRate, ActivationType.Log);
                }

                UpdateParameter(ref _functions[i].NormalForceAmplitude, _gradients[i].NormalForceAmplitude, ref _mnF, ref _vnF, _learningRate, ActivationType.Linear);
                UpdateParameter(ref _functions[i].RollSpeedAmplitude, _gradients[i].RollSpeedAmplitude, ref _mrS, ref _vrS, _learningRate, ActivationType.Linear);

                if (_gradients[i].Loss < bestLoss) {
                    bestLoss = _gradients[i].Loss;
                    bestIndex = i;
                }

                loss += _gradients[i].Loss;
            }

            loss /= N_INITIALIZATIONS;

            bool shouldDecay = loss > _loss + DECAY_THRESHOLD;
            _loss = math.min(_loss, loss);

            Debug.Log(_learningRate);

            if (shouldDecay) {
                _learningRate *= LEARNING_RATE_DECAY;
                _learningRate = math.max(_learningRate, MIN_LEARNING_RATE);
            }

            Track.Functions[^1] = _functions[bestIndex];
            Track.MarkDirty();
        }

        private void UpdateParameter(ref float param, float gradient, ref float m, ref float v, float lr, ActivationType activationType) {
            float param_hat = activationType switch {
                ActivationType.Log => Log(param),
                ActivationType.Sigmoid => InverseSigmoid(param),
                _ => param
            };
            m = BETA1 * m + (1 - BETA1) * gradient;
            v = BETA2 * v + (1 - BETA2) * gradient * gradient;
            float m_hat = m / (1 - math.pow(BETA1, _t));
            float v_hat = v / (1 - math.pow(BETA2, _t));
            param_hat -= lr * m_hat / (math.sqrt(v_hat) + EPSILON_ADAM);
            param = activationType switch {
                ActivationType.Log => InverseLog(param_hat),
                ActivationType.Sigmoid => Sigmoid(param_hat),
                _ => param_hat
            };
        }

        private void ComputeGradients() {
            for (int i = 0; i < N_INITIALIZATIONS; i++) {
                var originalFunction = _functions[i];

                // Duration (log)
                float logDuration = Log(originalFunction.Duration);

                var durationPlusFunction = originalFunction;
                durationPlusFunction.Duration = InverseLog(logDuration + EPSILON);
                _tracks[i * N_GRADIENTS + 0].Functions[^1] = durationPlusFunction;

                var durationMinusFunction = originalFunction;
                durationMinusFunction.Duration = InverseLog(logDuration - EPSILON);
                _tracks[i * N_GRADIENTS + 1].Functions[^1] = durationMinusFunction;

                // Normal Force Amplitude
                var normalForceAmplitudePlusFunction = originalFunction;
                normalForceAmplitudePlusFunction.NormalForceAmplitude += EPSILON;
                _tracks[i * N_GRADIENTS + 2].Functions[^1] = normalForceAmplitudePlusFunction;

                var normalForceAmplitudeMinusFunction = originalFunction;
                normalForceAmplitudeMinusFunction.NormalForceAmplitude -= EPSILON;
                _tracks[i * N_GRADIENTS + 3].Functions[^1] = normalForceAmplitudeMinusFunction;

                // Roll Speed Amplitude
                var rollSpeedAmplitudePlusFunction = originalFunction;
                rollSpeedAmplitudePlusFunction.RollSpeedAmplitude += EPSILON;
                _tracks[i * N_GRADIENTS + 4].Functions[^1] = rollSpeedAmplitudePlusFunction;

                var rollSpeedAmplitudeMinusFunction = originalFunction;
                rollSpeedAmplitudeMinusFunction.RollSpeedAmplitude -= EPSILON;
                _tracks[i * N_GRADIENTS + 5].Functions[^1] = rollSpeedAmplitudeMinusFunction;
            }

            Simulate().Complete();

            for (int i = 0; i < N_INITIALIZATIONS; i++) {
                Debug.Log($"Initialization {i}");
                float durationLossPlus = ComputeLoss(_tracks[i * N_GRADIENTS + 0], true);
                float durationLossMinus = ComputeLoss(_tracks[i * N_GRADIENTS + 1]);

                float normalForceAmplitudeLossPlus = ComputeLoss(_tracks[i * N_GRADIENTS + 2]);
                float normalForceAmplitudeLossMinus = ComputeLoss(_tracks[i * N_GRADIENTS + 3]);

                float rollSpeedAmplitudeLossPlus = ComputeLoss(_tracks[i * N_GRADIENTS + 4]);
                float rollSpeedAmplitudeLossMinus = ComputeLoss(_tracks[i * N_GRADIENTS + 5]);

                float minLoss = math.min(math.min(math.min(math.min(math.min(
                    durationLossPlus, durationLossMinus),
                    normalForceAmplitudeLossPlus),
                    normalForceAmplitudeLossMinus),
                    rollSpeedAmplitudeLossPlus),
                    rollSpeedAmplitudeLossMinus);

                _gradients[i] = new Gradient {
                    Loss = minLoss,
                    Duration = (durationLossPlus - durationLossMinus) / (2 * EPSILON),
                    NormalForceAmplitude = (normalForceAmplitudeLossPlus - normalForceAmplitudeLossMinus) / (2 * EPSILON),
                    RollSpeedAmplitude = (rollSpeedAmplitudeLossPlus - rollSpeedAmplitudeLossMinus) / (2 * EPSILON),
                };
            }
        }

        private float Log(float x) {
            return math.log(x);
        }

        private float InverseLog(float x) {
            return math.exp(x);
        }

        private float Sigmoid(float x) {
            return 1f / (1f + math.exp(-x));
        }

        private float InverseSigmoid(float x) {
            return math.log(x / (1f - x));
        }

        private float Angle(float a, float b) {
            float diff = (a - b + 180f) % 360f - 180f;
            return math.abs(diff);
        }

        private float ComputeLoss(Track track, bool log = false) {
            int nodeCount = track.NodeCount;
            var nodes = track.Nodes;

            var lastFunction = track.Functions[^1];
            var lastNode = nodes[nodeCount - 1];

            int functionNodeCount = (int)(lastFunction.Duration * Constants.HZ);
            int startIndex = math.max(0, nodeCount - functionNodeCount);
            var firstNode = nodes[startIndex];

            float totalLoss = 0f;

            // Constraint-based loss
            float targetPositionLoss, targetRollLoss, targetNormalForceLoss, targetPitchLoss, targetYawLoss;
            targetPositionLoss = ControlPoint.Mode switch {
                ConstraintMode.Position => math.distancesq(lastNode.Position, ControlPoint.transform.position),
                _ => 0f,
            };
            targetRollLoss = ControlPoint.ConstrainRoll ? Angle(lastNode.Roll, ControlPoint.Roll) : 0f;
            targetNormalForceLoss = ControlPoint.ConstrainNormalForce ? math.abs(ControlPoint.NormalForce - lastNode.NormalForce) : 0f;
            targetPitchLoss = ControlPoint.ConstrainPitch ? Angle(ControlPoint.Pitch, lastNode.GetPitch()) : 0f;
            targetYawLoss = ControlPoint.ConstrainYaw ? Angle(ControlPoint.Yaw, lastNode.GetYaw()) : 0f;

            targetPositionLoss *= 1f;
            targetNormalForceLoss *= 10f;
            targetRollLoss *= 1f;
            targetPitchLoss *= 1f;
            targetYawLoss *= 1f;

            totalLoss += targetPositionLoss
                + targetNormalForceLoss
                + targetRollLoss
                + targetPitchLoss
                + targetYawLoss;

            // Heuristic loss
            float durationLoss, normalForceLoss, rollSpeedLoss;
            float extremeNormalForceLoss, extremeRollSpeedLoss;
            durationLoss = ControlPoint.Mode switch {
                ConstraintMode.Duration => 0f,
                _ => lastFunction.Duration,
            };

            float dv = lastFunction.NormalForceAmplitude;
            float dt = lastFunction.Duration;
            normalForceLoss = math.abs(dv / dt);

            dv = lastFunction.RollSpeedAmplitude;
            dt = lastFunction.Duration;
            rollSpeedLoss = math.abs(dv / dt);

            (float min, float max) = (-1f, 6f);
            float minNormalForce = lastFunction.GetMinNormalForce();
            float maxNormalForce = lastFunction.GetMaxNormalForce();
            if (minNormalForce < min) {
                extremeNormalForceLoss = min - minNormalForce;
            }
            else if (maxNormalForce > max) {
                extremeNormalForceLoss = maxNormalForce - max;
            }
            else {
                extremeNormalForceLoss = 0f;
            }

            (min, max) = (-10f, 10f);
            float minRollSpeed = lastFunction.GetMinRollSpeed();
            float maxRollSpeed = lastFunction.GetMaxRollSpeed();
            if (minRollSpeed < min) {
                extremeRollSpeedLoss = min - minRollSpeed;
            }
            else if (maxRollSpeed > max) {
                extremeRollSpeedLoss = max - maxRollSpeed;
            }
            else {
                extremeRollSpeedLoss = 0f;
            }

            durationLoss *= 0.1f;
            normalForceLoss *= 0.1f;
            rollSpeedLoss *= 0.1f;
            extremeNormalForceLoss *= 10f;
            extremeRollSpeedLoss *= 10f;

            totalLoss += durationLoss
                + normalForceLoss
                + rollSpeedLoss
                + extremeNormalForceLoss
                + extremeRollSpeedLoss;

            if (log) {
                StringBuilder sb = new();
                sb.AppendLine($"Total Loss: {totalLoss}");
                sb.AppendLine($"Position Loss: {targetPositionLoss}");
                sb.AppendLine($"Duration Loss: {durationLoss}");
                sb.AppendLine($"Target Normal Force Loss: {targetNormalForceLoss}");
                sb.AppendLine($"Target Roll Loss: {targetRollLoss}");
                sb.AppendLine($"Target Pitch Loss: {targetPitchLoss}");
                sb.AppendLine($"Target Yaw Loss: {targetYawLoss}");
                sb.AppendLine($"Normal Force Loss: {normalForceLoss}");
                sb.AppendLine($"Roll Speed Loss: {rollSpeedLoss}");
                sb.AppendLine($"Extreme Normal Force Loss: {extremeNormalForceLoss}");
                sb.AppendLine($"Extreme Roll Speed Loss: {extremeRollSpeedLoss}");
                sb.AppendLine($"Normal Force: {lastNode.NormalForce}");
                sb.AppendLine($"Roll: {lastNode.Roll}");
                sb.AppendLine($"Pitch: {lastNode.GetPitch()}");
                sb.AppendLine($"Yaw: {lastNode.GetYaw()}");
                Debug.Log(sb.ToString());
            }

            return totalLoss;
        }

        private JobHandle Simulate() {
            NativeArray<JobHandle> jobHandles = new(_tracks.Length, Allocator.Temp);
            for (int i = 0; i < _tracks.Length; i++) {
                jobHandles[i] = _tracks[i].Build(true);
            }
            var combinedHandle = JobHandle.CombineDependencies(jobHandles);
            jobHandles.Dispose();
            return combinedHandle;
        }

        private enum ActivationType {
            Linear,
            Log,
            Sigmoid,
        }

        private struct Gradient {
            public float Loss;
            public float Duration;
            public float NormalForceAmplitude;
            public float RollSpeedAmplitude;

            public override string ToString() {
                StringBuilder sb = new();
                sb.AppendLine($"Loss: {Loss}");
                sb.AppendLine($"Duration: {Duration}");
                sb.AppendLine($"NormalForceAmplitude: {NormalForceAmplitude}");
                sb.AppendLine($"RollSpeedAmplitude: {RollSpeedAmplitude}");
                return sb.ToString();
            }
        }
    }
}
