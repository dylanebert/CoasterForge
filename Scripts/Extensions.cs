using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static CoasterForge.Constants;

namespace CoasterForge {
    public static class Extensions {
        public static void ComputeRailCrossSection(
            out NativeArray<float3> vertices,
            out NativeArray<float2> uvs,
            out NativeArray<float3> normals,
            out NativeArray<uint> indices
        ) {
            var leftRailVertices = new NativeArray<float3>(12, Allocator.Temp) {
                [0] = new float3(-.656f, 0f, 0f),
                [1] = new float3(-.656f, .266f, 0f),
                [2] = new float3(-.6005f, .266f, 0f),
                [3] = new float3(-.6005f, .342f, 0f),
                [4] = new float3(-.59825f, .342f, 0f),
                [5] = new float3(-.59825f, .353f, 0f),
                [6] = new float3(-.48825f, .353f, 0f),
                [7] = new float3(-.48825f, .342f, 0f),
                [8] = new float3(-.4005f, .342f, 0f),
                [9] = new float3(-.4005f, .266f, 0f),
                [10] = new float3(-.456f, .266f, 0f),
                [11] = new float3(-.456f, 0f, 0f),
            };
            var leftRailUVs = new NativeArray<float2>(12, Allocator.Temp);
            for (int i = 0; i < 12; i++) {
                if (i >= 4 && i < 7) {
                    leftRailUVs[i] = new float2(0.25f, 0.5f);
                }
                else {
                    leftRailUVs[i] = new float2(0.75f, 0.5f);
                }
            }

            var rightRailVertices = new NativeArray<float3>(12, Allocator.Temp);
            var rightRailUVs = new NativeArray<float2>(12, Allocator.Temp);
            for (int i = 0; i < leftRailVertices.Length; i++) {
                rightRailVertices[leftRailVertices.Length - i - 1] = leftRailVertices[i] * new float3(-1f, 1f, 1f);
                rightRailUVs[leftRailVertices.Length - i - 1] = leftRailUVs[i];
            }

            var edges = new NativeList<Edge>(Allocator.Temp);
            for (int i = 0; i < leftRailVertices.Length; i++) {
                edges.Add(new Edge {
                    A = leftRailVertices[i],
                    B = leftRailVertices[(i + 1) % leftRailVertices.Length],
                    UV = leftRailUVs[i]
                });
            }
            for (int i = 0; i < rightRailVertices.Length; i++) {
                edges.Add(new Edge {
                    A = rightRailVertices[i],
                    B = rightRailVertices[(i + 1) % rightRailVertices.Length],
                    UV = rightRailUVs[i]
                });
            }
            leftRailVertices.Dispose();
            rightRailVertices.Dispose();

            int edgeCount = edges.Length;
            int vertexCount = edgeCount * 4;
            int indexCount = edgeCount * 6;

            vertices = new NativeArray<float3>(vertexCount, Allocator.Temp);
            uvs = new NativeArray<float2>(vertexCount, Allocator.Temp);
            normals = new NativeArray<float3>(vertexCount, Allocator.Temp);
            indices = new NativeArray<uint>(indexCount, Allocator.Temp);

            for (int i = 0; i < edgeCount; i++) {
                float3 a = edges[i].A;
                float3 b = edges[i].B;
                float3 c = a + math.forward();
                float3 d = b + math.forward();

                float3 normal = math.normalize(math.cross(b - a, math.back()));

                int ai = i * 2;
                int bi = ai + 1;
                int ci = ai + edgeCount * 2;
                int di = bi + edgeCount * 2;

                vertices[ai] = a;
                vertices[bi] = b;
                vertices[ci] = c;
                vertices[di] = d;

                uvs[ai] = edges[i].UV;
                uvs[bi] = edges[i].UV;
                uvs[ci] = edges[i].UV;
                uvs[di] = edges[i].UV;

                normals[ai] = normal;
                normals[bi] = normal;
                normals[ci] = normal;
                normals[di] = normal;

                indices[i * 6] = (uint)ai;
                indices[i * 6 + 1] = (uint)ci;
                indices[i * 6 + 2] = (uint)di;
                indices[i * 6 + 3] = (uint)ai;
                indices[i * 6 + 4] = (uint)di;
                indices[i * 6 + 5] = (uint)bi;
            }
        }

        struct Edge {
            public float3 A;
            public float3 B;
            public float2 UV;
        }

        public static float ComputeEnergy(this PointData p) {
            return 0.5f * p.Velocity * p.Velocity + G * p.GetHeartPosition(CENTER).y;
        }

        public static float GetPitch(this PointData p) {
            float magnitude = math.sqrt(p.Direction.x * p.Direction.x + p.Direction.z * p.Direction.z);
            return math.degrees(math.atan2(p.Direction.y, magnitude));
        }

        public static float GetYaw(this PointData p) {
            return math.degrees(math.atan2(-p.Direction.x, -p.Direction.z));
        }

        public static float3 GetHeartPosition(this PointData p, float heart) {
            return p.Position + p.Normal * heart;
        }

        public static float3 GetRelativePosition(this PointData p, float3 position) {
            return p.Position
                - position.y * p.Normal
                + position.x * p.GetHeartLateral(position.y)
                + position.z * p.GetHeartDirection(position.y);
        }

        public static float3 GetHeartDirection(this PointData p, float heart) {
            float dist;
            if (p.AngleFromLast < 1e-3f) {
                dist = p.HeartDistanceFromLast;
            }
            else {
                dist = p.Velocity / HZ;
            }
            float rollSpeed = dist > 0f ? p.RollSpeed / HZ / dist : 0f;
            if (float.IsNaN(rollSpeed)) {
                rollSpeed = 0f;
            }
            float3 deviation = p.Lateral * math.radians(rollSpeed * heart);
            return math.normalize(p.Direction + deviation);
        }

        public static float3 GetHeartLateral(this PointData p, float heart) {
            float dist;
            if (p.AngleFromLast < 1e-3f) {
                dist = p.HeartDistanceFromLast;
            }
            else {
                dist = p.Velocity / HZ;
            }
            float rollSpeed = dist > 0f ? p.RollSpeed / HZ / dist : 0f;
            if (float.IsNaN(rollSpeed)) {
                rollSpeed = 0f;
            }
            float3 deviation = -p.Direction * math.radians(rollSpeed * heart);
            return math.normalize(p.Lateral + deviation);
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

        public static float Evaluate(this DynamicBuffer<RollSpeedKeyframe> keyframes, float t) {
            if (keyframes.Length == 0) return 0f;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

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

        public static float Evaluate(this DynamicBuffer<NormalForceKeyframe> keyframes, float t) {
            if (keyframes.Length == 0) return 0f;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

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

        public static float Evaluate(this DynamicBuffer<LateralForceKeyframe> keyframes, float t) {
            if (keyframes.Length == 0) return 0f;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

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

        public static float Evaluate(this DynamicBuffer<PitchSpeedKeyframe> keyframes, float t) {
            if (keyframes.Length == 0) return 0f;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

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

        public static float Evaluate(this DynamicBuffer<YawSpeedKeyframe> keyframes, float t) {
            if (keyframes.Length == 0) return 0f;
            if (t <= keyframes[0].Value.Time) return keyframes[0].Value.Value;

            int i = 0;
            while (i < keyframes.Length - 1 && t > keyframes[i + 1].Value.Time) {
                i++;
            }

            if (i >= keyframes.Length - 1) return keyframes[^1].Value.Value;

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
