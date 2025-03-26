using Unity.Mathematics;
using Unity.Collections;

namespace CoasterForge {
    public static class Utils {
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

        public struct Edge {
            public float3 A;
            public float3 B;
            public float2 UV;
        }
    }
}
