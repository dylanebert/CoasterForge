using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static CoasterForge.Constants;
using Node = CoasterForge.Track.Node;

namespace CoasterForge {
    public class TrackMesh : UnityEngine.MonoBehaviour {
        public Track Track;
        public UnityEngine.MeshFilter TopperMeshFilter;
        public UnityEngine.MeshFilter TrackMeshFilter;
        public float StartOffset = 0.35f;
        public float TrackGauge = 1.0865f;
        public float TopperThickness = 0.011f;
        public float TopperWidth = 0.11f;
        public float TwoByTenWidth = 0.2f;
        public float TwoByTenThickness = 0.038f;
        public float UpperLayersGauge = 1.001f;
        public float LowerLayersGauge = 1.112f;
        public float TieThickness = 0.09f;
        public float TieWidth = 1.8f;
        public int LowerLayers = 7;
        public int UpperLayers = 2;
        public int Resolution = 2;

        private UnityEngine.Mesh[] _meshes;
        private int _lastSolvedResolution;
        private bool _needsRebuild;

        private void Start() {
            _meshes = new UnityEngine.Mesh[2];
            _meshes[0] = new UnityEngine.Mesh {
                name = "Topper",
            };
            _meshes[1] = new UnityEngine.Mesh {
                name = "Track",
            };
            TopperMeshFilter.mesh = _meshes[0];
            TrackMeshFilter.mesh = _meshes[1];
        }

        private void Update() {
            if (Track.SolvedResolution != _lastSolvedResolution) {
                _lastSolvedResolution = Track.SolvedResolution;
                if (_lastSolvedResolution > 0) {
                    RequestRebuild();
                }
            }

            if (_needsRebuild) {
                Build();
                _needsRebuild = false;
            }
        }

        public void RequestRebuild() {
            _needsRebuild = true;
        }

        private void Build() {
            var trackNodes = Track.Nodes;

            var nodes = new NativeList<Node>(Allocator.TempJob);
            new CopyNodesJob {
                Nodes = nodes,
                TrackNodes = trackNodes,
                Resolution = Resolution,
            }.Schedule().Complete();

            var meshDataArray = UnityEngine.Mesh.AllocateWritableMeshData(2);

            int nodeCount = nodes.Length - 1;

            int topperQuadCount = nodeCount * 8 + 4;
            int topperVertexCount = topperQuadCount * 4;
            int topperTriangleCount = topperQuadCount * 6;

            int trackQuadCount = nodeCount * 16 + 8;
            int trackVertexCount = trackQuadCount * 4;
            int trackTriangleCount = trackQuadCount * 6;

            var topperMeshData = meshDataArray[0];
            topperMeshData.SetVertexBufferParams(
                topperVertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, dimension: 3, stream: 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, dimension: 3, stream: 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2, stream: 2)
            );
            topperMeshData.SetIndexBufferParams(
                topperTriangleCount,
                IndexFormat.UInt32
            );

            var trackMeshData = meshDataArray[1];
            trackMeshData.SetVertexBufferParams(
                trackVertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, dimension: 3, stream: 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, dimension: 3, stream: 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2, stream: 2)
            );
            trackMeshData.SetIndexBufferParams(
                trackTriangleCount,
                IndexFormat.UInt32
            );

            int batchCount = 8;
            int batchSize = (nodeCount + batchCount - 1) / batchCount;

            new BuildJob {
                TopperVertices = topperMeshData.GetVertexData<float3>(0),
                TopperNormals = topperMeshData.GetVertexData<float3>(1),
                TopperUVs = topperMeshData.GetVertexData<float2>(2),
                TopperTriangles = topperMeshData.GetIndexData<uint>(),
                TrackVertices = trackMeshData.GetVertexData<float3>(0),
                TrackNormals = trackMeshData.GetVertexData<float3>(1),
                TrackUVs = trackMeshData.GetVertexData<float2>(2),
                TrackTriangles = trackMeshData.GetIndexData<uint>(),
                Nodes = nodes.AsArray(),
                StartOffset = StartOffset,
                TrackGauge = TrackGauge,
                TopperWidth = TopperWidth,
                TopperThickness = TopperThickness,
                TwoByTenWidth = TwoByTenWidth,
                TwoByTenThickness = TwoByTenThickness,
                UpperLayersGauge = UpperLayersGauge,
                LowerLayersGauge = LowerLayersGauge,
                TieWidth = TieWidth,
                TieThickness = TieThickness,
                UpperLayers = UpperLayers,
                LowerLayers = LowerLayers,
                Resolution = Resolution,
                BatchSize = batchSize,
                BatchCount = batchCount,
            }.Schedule(batchCount * 2, 1).Complete();

            topperMeshData.subMeshCount = 1;
            topperMeshData.SetSubMesh(0, new SubMeshDescriptor(0, topperTriangleCount));

            trackMeshData.subMeshCount = 1;
            trackMeshData.SetSubMesh(0, new SubMeshDescriptor(0, trackTriangleCount));

            UnityEngine.Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, _meshes);

            _meshes[0].RecalculateBounds();
            _meshes[1].RecalculateBounds();

            nodes.Dispose();
        }

        [BurstCompile]
        private struct CopyNodesJob : IJob {
            [WriteOnly]
            public NativeList<Node> Nodes;

            [ReadOnly]
            public NativeArray<Node> TrackNodes;

            [ReadOnly]
            public int Resolution;

            public void Execute() {
                float nodeDistance = 0.7f / Resolution;
                float distFromLast = nodeDistance;
                for (int i = 0; i < TrackNodes.Length - 1; i++) {
                    var node = TrackNodes[i];
                    distFromLast += node.DistanceFromLast;
                    if (distFromLast >= nodeDistance) {
                        distFromLast -= nodeDistance;
                        Nodes.Add(node);
                    }
                }
            }
        }

        [BurstCompile]
        private struct BuildJob : IJobParallelFor {
            [WriteOnly, NativeDisableContainerSafetyRestriction]
            public NativeArray<float3> TopperVertices;

            [WriteOnly, NativeDisableContainerSafetyRestriction]
            public NativeArray<float3> TopperNormals;

            [WriteOnly, NativeDisableContainerSafetyRestriction]
            public NativeArray<float2> TopperUVs;

            [WriteOnly, NativeDisableContainerSafetyRestriction]
            public NativeArray<uint> TopperTriangles;

            [WriteOnly, NativeDisableContainerSafetyRestriction]
            public NativeArray<float3> TrackVertices;

            [WriteOnly, NativeDisableContainerSafetyRestriction]
            public NativeArray<float3> TrackNormals;

            [WriteOnly, NativeDisableContainerSafetyRestriction]
            public NativeArray<float2> TrackUVs;

            [WriteOnly, NativeDisableContainerSafetyRestriction]
            public NativeArray<uint> TrackTriangles;

            [ReadOnly]
            public NativeArray<Node> Nodes;

            [ReadOnly]
            public float StartOffset;

            [ReadOnly]
            public float TrackGauge;

            [ReadOnly]
            public float TopperWidth;

            [ReadOnly]
            public float TopperThickness;

            [ReadOnly]
            public float TwoByTenWidth;

            [ReadOnly]
            public float TwoByTenThickness;

            [ReadOnly]
            public float UpperLayersGauge;

            [ReadOnly]
            public float LowerLayersGauge;

            [ReadOnly]
            public float TieWidth;

            [ReadOnly]
            public float TieThickness;

            [ReadOnly]
            public int UpperLayers;

            [ReadOnly]
            public int LowerLayers;

            [ReadOnly]
            public int Resolution;

            [ReadOnly]
            public int BatchSize;

            [ReadOnly]
            public int BatchCount;

            public void Execute(int index) {
                int batchIndex = index / 2;
                int batchType = index % 2;
                switch (batchType) {
                    case 0:
                        BuildTopper(batchIndex);
                        break;
                    case 1:
                        BuildTrack(batchIndex);
                        break;
                    default:
                        throw new System.ArgumentOutOfRangeException(nameof(index), "Invalid job index");
                }
            }

            private void BuildTopper(int batchIndex) {
                int quadIndex = batchIndex * BatchSize * 8;
                int vertexIndex = quadIndex * 4;
                int triangleIndex = quadIndex * 6;

                float heart = StartOffset - HEART;
                var topperLeftBL = new float3(-TrackGauge / 2f - TopperWidth / 2f, heart - TopperThickness, 0);
                var topperLeftBR = new float3(-TrackGauge / 2f + TopperWidth / 2f, heart - TopperThickness, 0);
                var topperLeftTL = new float3(-TrackGauge / 2f + TopperWidth / 2f, heart, 0);
                var topperLeftTR = new float3(-TrackGauge / 2f - TopperWidth / 2f, heart, 0);
                var topperRightBL = new float3(TrackGauge / 2f - TopperWidth / 2f, heart - TopperThickness, 0);
                var topperRightBR = new float3(TrackGauge / 2f + TopperWidth / 2f, heart - TopperThickness, 0);
                var topperRightTL = new float3(TrackGauge / 2f + TopperWidth / 2f, heart, 0);
                var topperRightTR = new float3(TrackGauge / 2f - TopperWidth / 2f, heart, 0);

                int start = batchIndex * BatchSize;
                int end = math.min(start + BatchSize, Nodes.Length - 1);
                for (int i = start; i < end; i++) {
                    var prev = Nodes[i];
                    var current = Nodes[i + 1];

                    AddLongitudinalQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, prev, current, topperLeftBL, topperLeftBR);
                    AddLongitudinalQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, prev, current, topperLeftBR, topperLeftTL);
                    AddLongitudinalQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, prev, current, topperLeftTL, topperLeftTR);
                    AddLongitudinalQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, prev, current, topperLeftTR, topperLeftBL);

                    AddLongitudinalQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, prev, current, topperRightBL, topperRightBR);
                    AddLongitudinalQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, prev, current, topperRightBR, topperRightTL);
                    AddLongitudinalQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, prev, current, topperRightTL, topperRightTR);
                    AddLongitudinalQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, prev, current, topperRightTR, topperRightBL);
                }

                if (batchIndex == BatchCount - 1) {
                    Node first = Nodes[0];
                    Node last = Nodes[^1];

                    AddTransverseQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, first, topperLeftBL, topperLeftTR, topperLeftBR, topperLeftTL);
                    AddTransverseQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, first, topperRightBL, topperRightTR, topperRightBR, topperRightTL);
                    AddTransverseQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, last, topperLeftBL, topperLeftBR, topperLeftTR, topperLeftTL);
                    AddTransverseQuad(ref TopperVertices, ref TopperNormals, ref TopperUVs, ref TopperTriangles, ref vertexIndex, ref triangleIndex, last, topperRightBL, topperRightBR, topperRightTR, topperRightTL);
                }
            }

            private void BuildTrack(int batchIndex) {
                int quadIndex = batchIndex * BatchSize * 16;
                int vertexIndex = quadIndex * 4;
                int triangleIndex = quadIndex * 6;

                float heart = StartOffset - HEART;
                var upperLayersLeftBL = new float3(-UpperLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var upperLayersLeftBR = new float3(-UpperLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var upperLayersLeftTL = new float3(-UpperLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness, 0);
                var upperLayersLeftTR = new float3(-UpperLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness, 0);
                var upperLayersRightBL = new float3(UpperLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var upperLayersRightBR = new float3(UpperLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var upperLayersRightTL = new float3(UpperLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness, 0);
                var upperLayersRightTR = new float3(UpperLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness, 0);
                var lowerLayersLeftBL = new float3(-LowerLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers - TwoByTenThickness * LowerLayers, 0);
                var lowerLayersLeftBR = new float3(-LowerLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers - TwoByTenThickness * LowerLayers, 0);
                var lowerLayersLeftTL = new float3(-LowerLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var lowerLayersLeftTR = new float3(-LowerLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var lowerLayersRightBL = new float3(LowerLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers - TwoByTenThickness * LowerLayers, 0);
                var lowerLayersRightBR = new float3(LowerLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers - TwoByTenThickness * LowerLayers, 0);
                var lowerLayersRightTL = new float3(LowerLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var lowerLayersRightTR = new float3(LowerLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);

                int start = batchIndex * BatchSize;
                int end = math.min(start + BatchSize, Nodes.Length - 1);
                for (int i = start; i < end; i++) {
                    var prev = Nodes[i];
                    var current = Nodes[i + 1];

                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersLeftBL, upperLayersLeftBR);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersLeftBR, upperLayersLeftTL);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersLeftTL, upperLayersLeftTR);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersLeftTR, upperLayersLeftBL);

                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersRightBL, upperLayersRightBR);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersRightBR, upperLayersRightTL);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersRightTL, upperLayersRightTR);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersRightTR, upperLayersRightBL);

                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersLeftBL, lowerLayersLeftBR);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersLeftBR, lowerLayersLeftTL);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersLeftTL, lowerLayersLeftTR);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersLeftTR, lowerLayersLeftBL);

                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersRightBL, lowerLayersRightBR);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersRightBR, lowerLayersRightTL);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersRightTL, lowerLayersRightTR);
                    AddLongitudinalQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersRightTR, lowerLayersRightBL);
                }

                if (batchIndex == BatchCount - 1) {
                    Node first = Nodes[0];
                    Node last = Nodes[^1];

                    AddTransverseQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, first, upperLayersLeftBL, upperLayersLeftTR, upperLayersLeftBR, upperLayersLeftTL);
                    AddTransverseQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, first, upperLayersRightBL, upperLayersRightTR, upperLayersRightBR, upperLayersRightTL);
                    AddTransverseQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, last, upperLayersLeftBL, upperLayersLeftBR, upperLayersLeftTR, upperLayersLeftTL);
                    AddTransverseQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, last, upperLayersRightBL, upperLayersRightBR, upperLayersRightTR, upperLayersRightTL);

                    AddTransverseQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, first, lowerLayersLeftBL, lowerLayersLeftTR, lowerLayersLeftBR, lowerLayersLeftTL);
                    AddTransverseQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, first, lowerLayersRightBL, lowerLayersRightTR, lowerLayersRightBR, lowerLayersRightTL);
                    AddTransverseQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, last, lowerLayersLeftBL, lowerLayersLeftBR, lowerLayersLeftTR, lowerLayersLeftTL);
                    AddTransverseQuad(ref TrackVertices, ref TrackNormals, ref TrackUVs, ref TrackTriangles, ref vertexIndex, ref triangleIndex, last, lowerLayersRightBL, lowerLayersRightBR, lowerLayersRightTR, lowerLayersRightTL);
                }
            }

            /* private void BuildTies(int batchIndex) {
                var tiesMeshData = MeshDataArray[2];
                var vertices = tiesMeshData.GetVertexData<float3>(0);
                var normals = tiesMeshData.GetVertexData<float3>(1);
                var uvs = tiesMeshData.GetVertexData<float2>(2);
                var triangles = tiesMeshData.GetIndexData<uint>();

                int tieCount = (Nodes.Length + Resolution - 1) / Resolution;
                int tieBatchSize = tieCount / BatchCount;
                int quadIndex = batchIndex * tieBatchSize * 6;
                int vertexIndex = quadIndex * 4;
                int triangleIndex = quadIndex * 6;

                float heart = StartOffset - HEART;
                var upperLayersLeftBL = new float3(-UpperLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var upperLayersLeftBR = new float3(-UpperLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var upperLayersLeftTL = new float3(-UpperLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness, 0);
                var upperLayersLeftTR = new float3(-UpperLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness, 0);
                var upperLayersRightBL = new float3(UpperLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var upperLayersRightBR = new float3(UpperLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var upperLayersRightTL = new float3(UpperLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness, 0);
                var upperLayersRightTR = new float3(UpperLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness, 0);
                var lowerLayersLeftBL = new float3(-LowerLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers - TwoByTenThickness * LowerLayers, 0);
                var lowerLayersLeftBR = new float3(-LowerLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers - TwoByTenThickness * LowerLayers, 0);
                var lowerLayersLeftTL = new float3(-LowerLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var lowerLayersLeftTR = new float3(-LowerLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var lowerLayersRightBL = new float3(LowerLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers - TwoByTenThickness * LowerLayers, 0);
                var lowerLayersRightBR = new float3(LowerLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers - TwoByTenThickness * LowerLayers, 0);
                var lowerLayersRightTL = new float3(LowerLayersGauge / 2f + TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);
                var lowerLayersRightTR = new float3(LowerLayersGauge / 2f - TwoByTenWidth / 2f, heart - TopperThickness - TwoByTenThickness * UpperLayers, 0);

                void AddQuad(float3 bl, float3 br, float3 tl, float3 tr) {
                    vertices[vertexIndex] = bl;
                    vertices[vertexIndex + 1] = br;
                    vertices[vertexIndex + 2] = tl;
                    vertices[vertexIndex + 3] = tr;

                    float3 normal = math.normalize(math.cross(tl - bl, br - bl));
                    normals[vertexIndex] = normal;
                    normals[vertexIndex + 1] = normal;
                    normals[vertexIndex + 2] = normal;
                    normals[vertexIndex + 3] = normal;

                    uvs[vertexIndex] = float2.zero;
                    uvs[vertexIndex + 1] = new float2(1f, 0f);
                    uvs[vertexIndex + 2] = new float2(0f, 1f);
                    uvs[vertexIndex + 3] = new float2(1f, 1f);

                    uint bli = (uint)vertexIndex;
                    uint bri = (uint)(vertexIndex + 1);
                    uint tli = (uint)(vertexIndex + 2);
                    uint tri = (uint)(vertexIndex + 3);

                    triangles[triangleIndex] = bli;
                    triangles[triangleIndex + 1] = tli;
                    triangles[triangleIndex + 2] = tri;

                    triangles[triangleIndex + 3] = bli;
                    triangles[triangleIndex + 4] = tri;
                    triangles[triangleIndex + 5] = bri;

                    vertexIndex += 4;
                    triangleIndex += 6;
                }

                void AddTransverseQuad(Node node, float3 bl, float3 br, float3 tl, float3 tr) {
                    float3 bl2 = node.GetRelativePosition(bl);
                    float3 br2 = node.GetRelativePosition(br);
                    float3 tl2 = node.GetRelativePosition(tl);
                    float3 tr2 = node.GetRelativePosition(tr);

                    AddQuad(bl2, br2, tl2, tr2);
                }

                int start = batchIndex * tieBatchSize;
                int end = math.min(start + tieBatchSize, Nodes.Length - 1);
                for (int i = start; i < end; i += Resolution) {
                    var node = Nodes[i];

                    float topY = heart - TopperThickness - TwoByTenThickness * UpperLayers - TwoByTenThickness * LowerLayers;
                    var tieTopBL = new float3(-TieWidth / 2f, topY, 0f);
                    var tieTopBR = new float3(TieWidth / 2f, topY, 0f);
                    var tieTopTL = new float3(-TieWidth / 2f, topY, TieThickness);
                    var tieTopTR = new float3(TieWidth / 2f, topY, TieThickness);

                    float bottomY = topY - TieThickness;
                    var tieBottomBL = new float3(-TieWidth / 2f, bottomY, 0f);
                    var tieBottomBR = new float3(TieWidth / 2f, bottomY, 0f);
                    var tieBottomTL = new float3(-TieWidth / 2f, bottomY, TieThickness);
                    var tieBottomTR = new float3(TieWidth / 2f, bottomY, TieThickness);

                    AddTransverseQuad(node, tieTopBL, tieTopTL, tieTopBR, tieTopTR); // Top
                    AddTransverseQuad(node, tieBottomTL, tieBottomBL, tieBottomTR, tieBottomBR); //Bottom
                    AddTransverseQuad(node, tieTopBL, tieTopBR, tieBottomBL, tieBottomBR); // Front
                    AddTransverseQuad(node, tieBottomTL, tieBottomTR, tieTopTL, tieTopTR); // Back
                    AddTransverseQuad(node, tieTopTL, tieTopBL, tieBottomTL, tieBottomBL); // Left
                    AddTransverseQuad(node, tieBottomTR, tieBottomBR, tieTopTR, tieTopBR); // Right
                }
            } */

            private void AddQuad(
                ref NativeArray<float3> vertices,
                ref NativeArray<float3> normals,
                ref NativeArray<float2> uvs,
                ref NativeArray<uint> triangles,
                ref int vertexIndex, ref int triangleIndex,
                float3 bl, float3 br, float3 tl, float3 tr
            ) {
                vertices[vertexIndex] = bl;
                vertices[vertexIndex + 1] = br;
                vertices[vertexIndex + 2] = tl;
                vertices[vertexIndex + 3] = tr;

                float3 normal = math.normalize(math.cross(tl - bl, br - bl));
                normals[vertexIndex] = normal;
                normals[vertexIndex + 1] = normal;
                normals[vertexIndex + 2] = normal;
                normals[vertexIndex + 3] = normal;

                uvs[vertexIndex] = float2.zero;
                uvs[vertexIndex + 1] = new float2(1f, 0f);
                uvs[vertexIndex + 2] = new float2(0f, 1f);
                uvs[vertexIndex + 3] = new float2(1f, 1f);

                uint bli = (uint)vertexIndex;
                uint bri = (uint)(vertexIndex + 1);
                uint tli = (uint)(vertexIndex + 2);
                uint tri = (uint)(vertexIndex + 3);

                triangles[triangleIndex] = bli;
                triangles[triangleIndex + 1] = tli;
                triangles[triangleIndex + 2] = tri;

                triangles[triangleIndex + 3] = bli;
                triangles[triangleIndex + 4] = tri;
                triangles[triangleIndex + 5] = bri;

                vertexIndex += 4;
                triangleIndex += 6;
            }

            private void AddLongitudinalQuad(
                ref NativeArray<float3> vertices,
                ref NativeArray<float3> normals,
                ref NativeArray<float2> uvs,
                ref NativeArray<uint> triangles,
                ref int vertexIndex, ref int triangleIndex,
                Node prev, Node curr, float3 l, float3 r
            ) {
                float3 bl = prev.GetRelativePosition(l);
                float3 br = prev.GetRelativePosition(r);
                float3 tl = curr.GetRelativePosition(l);
                float3 tr = curr.GetRelativePosition(r);

                AddQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, bl, br, tl, tr);
            }

            private void AddTransverseQuad(
                ref NativeArray<float3> vertices,
                ref NativeArray<float3> normals,
                ref NativeArray<float2> uvs,
                ref NativeArray<uint> triangles,
                ref int vertexIndex, ref int triangleIndex,
                Node node, float3 bl, float3 br, float3 tl, float3 tr
            ) {
                float3 bl2 = node.GetRelativePosition(bl);
                float3 br2 = node.GetRelativePosition(br);
                float3 tl2 = node.GetRelativePosition(tl);
                float3 tr2 = node.GetRelativePosition(tr);

                AddQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, bl2, br2, tl2, tr2);
            }
        }
    }
}
