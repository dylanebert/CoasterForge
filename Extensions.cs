using Unity.Collections;
using Unity.Mathematics;
using static CoasterForge.Constants;
using Node = CoasterForge.Section.Node;
using Keyframe = CoasterForge.Section.Keyframe;
using InterpolationType = CoasterForge.Section.Keyframe.InterpolationType;

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

        public static float Evaluate(this NativeArray<Keyframe> keyframes, float t) {
            if (keyframes.Length == 0) return 0f;
            if (t <= keyframes[0].Time) return keyframes[0].Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value;

            Keyframe start = keyframes[i];
            Keyframe end = keyframes[i + 1];

            if (start.OutInterpolation == InterpolationType.Constant) {
                return start.Value;
            }
            if (end.InInterpolation == InterpolationType.Constant) {
                return end.Value;
            }

            float segmentT = (t - start.Time) / (end.Time - start.Time);
            var interpolationType = GetMaxInterpolation(start.OutInterpolation, end.InInterpolation);

            switch (interpolationType) {
                case InterpolationType.Linear:
                    return math.lerp(start.Value, end.Value, segmentT);
                case InterpolationType.Ease:
                    float oneMinusT = 1f - segmentT;
                    float timeSquared = segmentT * segmentT;
                    float timeCubed = timeSquared * segmentT;
                    float oneMinusTSquared = oneMinusT * oneMinusT;
                    float oneMinusTCubed = oneMinusTSquared * oneMinusT;

                    float dt = end.Time - start.Time;
                    float p0 = start.Value;
                    float p1 = p0 + (start.OutTangent * dt * start.OutWeight);
                    float p3 = end.Value;
                    float p2 = p3 - (end.InTangent * dt * end.InWeight);

                    return oneMinusTCubed * p0
                        + 3f * oneMinusTSquared * segmentT * p1
                        + 3f * oneMinusT * timeSquared * p2
                        + timeCubed * p3;
                default:
                    return start.Value;
            }
        }

        private static InterpolationType GetMaxInterpolation(
            InterpolationType a,
            InterpolationType b
        ) {
            return (InterpolationType)math.max((int)a, (int)b);
        }
    }
}
