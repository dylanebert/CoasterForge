using Unity.Mathematics;
using static CoasterForge.Constants;
using Node = CoasterForge.Track.Node;
using Keyframe = CoasterForge.Track.Keyframe;
using Unity.Collections;
using UnityEngine;

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
    }
}
