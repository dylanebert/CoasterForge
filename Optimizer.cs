using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace CoasterForge {
    public class Optimizer : MonoBehaviour {
        public Track Track;
        public Transform ControlPoint;
        public float LearningRate = 0.001f;

        private void Update() {
            if (Track.SolvedK <= 0) return;

            // Adjustable parameters
            // Duration d (in seconds)
            // Normal force curve (in gs, list of keyframes)
            // Roll speed curve (in deg/s, list of keyframes)
            // Each keyframe has normalized time t [0, 1] and value
            float d0 = Track.Duration;
            List<Track.Keyframe> nF0 = Track.NormalForceCurve;
            List<Track.Keyframe> rS0 = Track.RollSpeedCurve;

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
            Simulate();

            var nodes = Track.Nodes;
            var results = new NativeArray<ForwardResult>(nodes.Length, Allocator.TempJob);
            new ForwardJob {
                Results = results,
                Nodes = nodes,
            }.Schedule().Complete();

            Simulate();
        }

        private void Simulate() {
            Track.MarkDirty();
            Track.Build();
        }

        [BurstCompile]
        private struct ForwardJob : IJob {
            [WriteOnly]
            public NativeArray<ForwardResult> Results;

            [ReadOnly]
            public NativeArray<Track.Node> Nodes;

            public void Execute() {
                for (int i = 0; i < Nodes.Length; i++) {
                    // Forward pass
                }
            }
        }

        private struct ForwardResult {

        }
    }
}
