using Unity.Mathematics;
using static CoasterForge.Constants;
using Node = CoasterForge.Track.Node;
using Keyframe = CoasterForge.Track.Keyframe;
using System.Collections.Generic;
using Unity.Collections;

namespace CoasterForge {
    public static class Extensions {
        public static float GetPitch(this Node node) {
            float magnitude = math.sqrt(node.Direction.x * node.Direction.x + node.Direction.z * node.Direction.z);
            return math.degrees(math.atan2(node.Direction.y, magnitude));
        }

        public static float GetYaw(this Node node) {
            return math.degrees(math.atan2(-node.Direction.x, -node.Direction.z));
        }

        public static float3 GetHeartPosition(this Node node, float heart) {
            return node.Position + node.Normal * heart;
        }

        public static float3 GetRelativePosition(this Node node, float3 position) {
            return node.Position
                - position.y * node.Normal
                + position.x * node.GetHeartLateral(position.y)
                + position.z * node.GetHeartDirection(position.y);
        }

        public static float3 GetHeartDirection(this Node node, float heart) {
            float dist;
            if (node.AngleFromLast < 1e-3f) {
                dist = node.HeartDistanceFromLast;
            }
            else {
                dist = node.Velocity / HZ;
            }
            float rollSpeed = dist > 0f ? node.RollSpeed / HZ / dist : 0f;
            if (float.IsNaN(rollSpeed)) {
                rollSpeed = 0f;
            }
            float3 deviation = node.Lateral * math.radians(rollSpeed * heart);
            return math.normalize(node.Direction + deviation);
        }

        public static float3 GetHeartLateral(this Node node, float heart) {
            float dist;
            if (node.AngleFromLast < 1e-3f) {
                dist = node.HeartDistanceFromLast;
            }
            else {
                dist = node.Velocity / HZ;
            }
            float rollSpeed = dist > 0f ? node.RollSpeed / HZ / dist : 0f;
            if (float.IsNaN(rollSpeed)) {
                rollSpeed = 0f;
            }
            float3 deviation = -node.Direction * math.radians(rollSpeed * heart);
            return math.normalize(node.Lateral + deviation);
        }

        public static float Evaluate(this NativeList<Keyframe> curve, float time) {
            if (curve.Length == 0) return 0f;
            if (curve.Length == 1) return curve[0].Value;

            int nextIndex = 0;
            while (nextIndex < curve.Length && curve[nextIndex].Time < time) {
                nextIndex++;
            }

            if (nextIndex == 0) return curve[0].Value;
            if (nextIndex == curve.Length) return curve[^1].Value;

            Keyframe k1 = curve[nextIndex - 1];
            Keyframe k2 = curve[nextIndex];

            float t = (time - k1.Time) / (k2.Time - k1.Time);

            return k1.Value + (k2.Value - k1.Value) * (1 - math.cos(t * math.PI)) * 0.5f;
        }

        public static float Evaluate(this List<Keyframe> curve, float time) {
            if (curve.Count == 0) return 0f;
            if (curve.Count == 1) return curve[0].Value;

            int nextIndex = 0;
            while (nextIndex < curve.Count && curve[nextIndex].Time < time) {
                nextIndex++;
            }

            if (nextIndex == 0) return curve[0].Value;
            if (nextIndex == curve.Count) return curve[^1].Value;

            Keyframe k1 = curve[nextIndex - 1];
            Keyframe k2 = curve[nextIndex];

            float t = (time - k1.Time) / (k2.Time - k1.Time);

            return k1.Value + (k2.Value - k1.Value) * (1 - math.cos(t * math.PI)) * 0.5f;
        }
    }
}
