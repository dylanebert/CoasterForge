using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CoasterForge {
    public class Optimizer : MonoBehaviour {
        public Track Track;
        public Transform ControlPoint;
        public float LearningRate = 0.001f;
        public float GradientClipThreshold = 1f;
        public float Epsilon = 0.001f;
        public float Beta1 = 0.9f;
        public float Beta2 = 0.999f;
        public float EpsilonAdam = 1e-8f;

        private float m_d, m_v;
        private List<float> m_nF, v_nF;
        private List<float> m_rS, v_rS;
        private int t;

        private void Start() {
            m_d = m_v = 0f;
            m_nF = new List<float>(new float[Track.NormalForceCurve.Count]);
            m_rS = new List<float>(new float[Track.RollSpeedCurve.Count]);
            v_nF = new List<float>(new float[Track.NormalForceCurve.Count]);
            v_rS = new List<float>(new float[Track.RollSpeedCurve.Count]);
            t = 0;
        }

        private void Update() {
            if (Track.SolvedK <= 0) return;

            float d = Track.Duration;
            List<Track.Keyframe> nF = Track.NormalForceCurve;
            List<Track.Keyframe> rS = Track.RollSpeedCurve;

            float gradient_d = ComputeGradientForDuration(d, nF, rS);
            List<float> gradients_nF = ComputeGradientsForNormalForce(d, nF, rS);
            List<float> gradients_rS = ComputeGradientsForRollSpeed(d, nF, rS);

            t++;

            UpdateParameter(ref d, gradient_d, ref m_d, ref m_v);
            Track.Duration = d;

            for (int i = 0; i < nF.Count; i++) {
                float newValue = nF[i].Value - LearningRate * gradients_nF[i];
                float m_nF_i = m_nF[i];
                float v_nF_i = v_nF[i];
                UpdateParameter(ref newValue, gradients_nF[i], ref v_nF_i, ref m_nF_i);
                nF[i] = new Track.Keyframe { Time = nF[i].Time, Value = newValue };
                v_nF[i] = v_nF_i;
                m_nF[i] = m_nF_i;
            }
            Track.NormalForceCurve = nF;

            for (int i = 0; i < rS.Count; i++) {
                float newValue = rS[i].Value - LearningRate * gradients_rS[i];
                float m_rS_i = m_rS[i];
                float v_rS_i = v_rS[i];
                UpdateParameter(ref newValue, gradients_rS[i], ref v_rS_i, ref m_rS_i);
                rS[i] = new Track.Keyframe { Time = rS[i].Time, Value = newValue };
                v_rS[i] = v_rS_i;
                m_rS[i] = m_rS_i;
            }
            Track.RollSpeedCurve = rS;

            Simulate();
        }

        public void LogLoss() {
            ComputeLoss(Track.Nodes, ControlPoint.position, Track.Duration, Track.NormalForceCurve, Track.RollSpeedCurve, true);
        }

        private void UpdateParameter(ref float param, float gradient, ref float m, ref float v) {
            gradient = math.clamp(gradient, -GradientClipThreshold, GradientClipThreshold);
            m = Beta1 * m + (1 - Beta1) * gradient;
            v = Beta2 * v + (1 - Beta2) * gradient * gradient;
            float m_hat = m / (1 - math.pow(Beta1, t));
            float v_hat = v / (1 - math.pow(Beta2, t));
            param -= LearningRate * m_hat / (math.sqrt(v_hat) + EpsilonAdam);
        }

        private float ComputeGradientForDuration(float d, List<Track.Keyframe> nF, List<Track.Keyframe> rS) {
            float original_d = Track.Duration;

            Track.Duration = d + Epsilon;
            Simulate();
            float L_plus = ComputeLoss(Track.Nodes, ControlPoint.position, Track.Duration, nF, rS);

            Track.Duration = d - Epsilon;
            Simulate();
            float L_minus = ComputeLoss(Track.Nodes, ControlPoint.position, Track.Duration, nF, rS);

            return (L_plus - L_minus) / (2 * Epsilon);
        }

        private List<float> ComputeGradientsForNormalForce(float d, List<Track.Keyframe> nF, List<Track.Keyframe> rS) {
            List<float> gradients = new();
            for (int i = 0; i < nF.Count; i++) {
                var originalKeyframe = nF[i];

                Track.NormalForceCurve[i] = new Track.Keyframe {
                    Time = originalKeyframe.Time,
                    Value = originalKeyframe.Value + Epsilon,
                };

                Simulate();
                float L_plus = ComputeLoss(Track.Nodes, ControlPoint.position, Track.Duration, nF, rS);

                Track.NormalForceCurve[i] = new Track.Keyframe {
                    Time = originalKeyframe.Time,
                    Value = originalKeyframe.Value - Epsilon,
                };

                Simulate();
                float L_minus = ComputeLoss(Track.Nodes, ControlPoint.position, Track.Duration, nF, rS);

                Track.NormalForceCurve[i] = originalKeyframe;

                gradients.Add((L_plus - L_minus) / (2 * Epsilon));
            }

            return gradients;
        }

        private List<float> ComputeGradientsForRollSpeed(float d, List<Track.Keyframe> nF, List<Track.Keyframe> rS) {
            List<float> gradients = new();
            for (int i = 0; i < rS.Count; i++) {
                var originalKeyframe = rS[i];

                Track.RollSpeedCurve[i] = new Track.Keyframe {
                    Time = originalKeyframe.Time,
                    Value = originalKeyframe.Value + Epsilon,
                };

                Simulate();
                float L_plus = ComputeLoss(Track.Nodes, ControlPoint.position, Track.Duration, nF, rS);

                Track.RollSpeedCurve[i] = new Track.Keyframe {
                    Time = originalKeyframe.Time,
                    Value = originalKeyframe.Value - Epsilon,
                };

                Simulate();
                float L_minus = ComputeLoss(Track.Nodes, ControlPoint.position, Track.Duration, nF, rS);

                Track.RollSpeedCurve[i] = originalKeyframe;

                gradients.Add((L_plus - L_minus) / (2 * Epsilon));
            }

            return gradients;
        }

        private float ComputeLoss(NativeArray<Track.Node> nodes, float3 controlPoint, float d, List<Track.Keyframe> nF, List<Track.Keyframe> rS, bool log = false) {
            int nodeCount = nodes.Length;

            float positionLoss = math.distancesq(nodes[^1].Position, controlPoint);

            float normalSmoothnessLoss = 0f;
            for (int i = 1; i < nF.Count; i++) {
                float dt = nF[i].Time - nF[i - 1].Time;
                float normalDiff = (nF[i].Value - nF[i - 1].Value) / math.max(dt, 1e-3f);
                normalSmoothnessLoss += normalDiff * normalDiff;
            }

            float rollSpeedSmoothnessLoss = 0f;
            for (int i = 1; i < rS.Count; i++) {
                float dt = rS[i].Time - rS[i - 1].Time;
                float rollSpeedDiff = (rS[i].Value - rS[i - 1].Value) / math.max(dt, 1e-3f);
                rollSpeedSmoothnessLoss += rollSpeedDiff * rollSpeedDiff;
            }

            float velocityLoss = 0f;
            for (int i = 0; i < nodeCount; i++) {
                float velocity = nodes[i].Velocity;
                if (velocity < 1f) {
                    velocityLoss += (1f - velocity) * (1f - velocity);
                }
            }

            float energyLoss = 0f;
            for (int i = 0; i < nodeCount; i++) {
                float velocity = nodes[i].Velocity;
                if (velocity < 0f) {
                    energyLoss += velocity * velocity;
                }
            }

            float angleLoss = 0f;
            for (int i = 0; i < nodeCount; i++) {
                float angle = nodes[i].AngleFromLast;
                float distance = nodes[i].DistanceFromLast;
                float maxAngle = 10f * math.max(distance, 1e-3f);
                if (angle > maxAngle) {
                    angleLoss += (angle - maxAngle) * (angle - maxAngle);
                }
            }

            float extremeNormalForceLoss = 0f;
            for (int i = 0; i < nF.Count; i++) {
                float normalForce = nF[i].Value;
                const float minNormalForce = -2f;
                const float maxNormalForce = 6f;
                if (normalForce < minNormalForce || normalForce > maxNormalForce) {
                    float normalizedNormalForce = (normalForce - minNormalForce) / (maxNormalForce - minNormalForce);
                    extremeNormalForceLoss += normalizedNormalForce * normalizedNormalForce;
                }
            }

            float extremeRollSpeedLoss = 0f;
            for (int i = 0; i < rS.Count; i++) {
                float rollSpeed = rS[i].Value;
                const float minRollSpeed = -500f;
                const float maxRollSpeed = 500f;
                if (rollSpeed < minRollSpeed || rollSpeed > maxRollSpeed) {
                    float normalizedRollSpeed = (rollSpeed - minRollSpeed) / (maxRollSpeed - minRollSpeed);
                    extremeRollSpeedLoss += normalizedRollSpeed * normalizedRollSpeed;
                }
            }

            if (log) {
                StringBuilder sb = new();
                sb.AppendLine($"positionLoss: {positionLoss}");
                sb.AppendLine($"normalSmoothnessLoss: {normalSmoothnessLoss}");
                sb.AppendLine($"rollSpeedSmoothnessLoss: {rollSpeedSmoothnessLoss}");
                sb.AppendLine($"velocityLoss: {velocityLoss}");
                sb.AppendLine($"energyLoss: {energyLoss}");
                sb.AppendLine($"angleLoss: {angleLoss}");
                sb.AppendLine($"extremeNormalForceLoss: {extremeNormalForceLoss}");
                sb.AppendLine($"extremeRollSpeedLoss: {extremeRollSpeedLoss}");
                Debug.Log(sb.ToString());
            }

            return positionLoss
                + normalSmoothnessLoss
                + rollSpeedSmoothnessLoss
                + velocityLoss
                + energyLoss
                + angleLoss
                + extremeNormalForceLoss
                + extremeRollSpeedLoss;
        }

        private void Simulate() {
            // Compute track nodes from duration, normal forces, and roll speeds
            // Nodes are indexed by time, one node every 0.001s
            // The nodes pass through the rider center, not the track
            // The term "heart" refers to an offset from the rider center, e.g. the actual track
            // Each node contains:
            // Position: World space [x, y, z] in meters
            // Direction: Node forward vector
            // Lateral: Node right vector
            // Normal: Node up vector
            // Roll: Banking in degrees
            // Velocity: m/s
            // Energy: Potential + kinetic
            // NormalForce: gs
            // LateralForce: gs
            // RollSpeed: deg/s
            // DistanceFromLast: Distance from position to previous position
            // HeartDistanceFromLast: Distance from track position to previous track position
            // AngleFromLast: Combined angle from previous in degrees
            // PitchFromLast: Pitch from previous in degrees
            // YawFromLast: Yaw from previous in degrees
            // RollSpeed: deg/s
            // TotalLength: Cumulative length of the nodes
            // TotalHeartLength: Cumulative length of the actual track
            Track.MarkDirty();
            Track.Build(true);
        }
    }
}
