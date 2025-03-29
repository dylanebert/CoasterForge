using Unity.Mathematics;
using static CoasterForge.Constants;
using Node = CoasterForge.Track.Node;
using Keyframe = CoasterForge.Track.Keyframe;
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

        public static void Evaluate(
            this NativeArray<Keyframe> keyframes,
            float time,
            out float normalForce,
            out float lateralForce,
            out float rollSpeed) {
            if (keyframes.Length == 0) {
                normalForce = 0f;
                lateralForce = 0f;
                rollSpeed = 0f;
                return;
            }
            if (keyframes.Length == 1) {
                normalForce = keyframes[0].NormalForce;
                lateralForce = keyframes[0].LateralForce;
                rollSpeed = keyframes[0].RollSpeed;
                return;
            }

            int nextIndex = 0;
            while (nextIndex < keyframes.Length && keyframes[nextIndex].Time < time) {
                nextIndex++;
            }

            if (nextIndex == 0) {
                normalForce = keyframes[0].NormalForce;
                lateralForce = keyframes[0].LateralForce;
                rollSpeed = keyframes[0].RollSpeed;
                return;
            }
            if (nextIndex == keyframes.Length) {
                normalForce = keyframes[^1].NormalForce;
                lateralForce = keyframes[^1].LateralForce;
                rollSpeed = keyframes[^1].RollSpeed;
                return;
            }

            Keyframe k1 = keyframes[nextIndex - 1];
            Keyframe k2 = keyframes[nextIndex];

            float t = (time - k1.Time) / (k2.Time - k1.Time);

            normalForce = k1.NormalForce + (k2.NormalForce - k1.NormalForce) * (1 - math.cos(t * math.PI)) * 0.5f;
            lateralForce = k1.LateralForce + (k2.LateralForce - k1.LateralForce) * (1 - math.cos(t * math.PI)) * 0.5f;
            rollSpeed = k1.RollSpeed + (k2.RollSpeed - k1.RollSpeed) * (1 - math.cos(t * math.PI)) * 0.5f;
        }
    }
}
