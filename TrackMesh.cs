using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static CoasterForge.Constants;
using Node = CoasterForge.Track.Node;

namespace CoasterForge {
    public class TrackMesh : UnityEngine.MonoBehaviour {
        public Track Track;
        public UnityEngine.Material TopperMaterial;
        public UnityEngine.Material TrackMaterial;
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

        private UnityEngine.Mesh[] _trackMeshes;
        private UnityEngine.Mesh _tieMesh;
        private NativeArray<UnityEngine.Matrix4x4> _tieMatrices;
        private int _lastSolvedResolution;
        private bool _needsRebuild;

        private void Start() {
            _tieMatrices = new NativeArray<UnityEngine.Matrix4x4>(0, Allocator.Persistent);

            _trackMeshes = new UnityEngine.Mesh[2];
            _trackMeshes[0] = new UnityEngine.Mesh {
                name = "Topper",
            };
            _trackMeshes[1] = new UnityEngine.Mesh {
                name = "Track",
            };

            var topper = new UnityEngine.GameObject("Topper");
            var topperMeshFilter = topper.AddComponent<UnityEngine.MeshFilter>();
            topper.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial = TopperMaterial;
            topperMeshFilter.mesh = _trackMeshes[0];

            var track = new UnityEngine.GameObject("Track");
            var trackMeshFilter = track.AddComponent<UnityEngine.MeshFilter>();
            track.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial = TrackMaterial;
            trackMeshFilter.mesh = _trackMeshes[1];

            var tieMeshVertices = new NativeArray<float3>(6 * 4, Allocator.Temp);
            var tieMeshTriangles = new NativeArray<ushort>(6 * 6, Allocator.Temp);

            float3 ubl = new(-TieWidth / 2f, 0f, 0f);
            float3 ubr = new(TieWidth / 2f, 0f, 0f);
            float3 utl = new(-TieWidth / 2f, 0f, TieThickness);
            float3 utr = new(TieWidth / 2f, 0f, TieThickness);

            float3 lbl = new(-TieWidth / 2f, -TieThickness, 0f);
            float3 lbr = new(TieWidth / 2f, -TieThickness, 0f);
            float3 ltl = new(-TieWidth / 2f, -TieThickness, TieThickness);
            float3 ltr = new(TieWidth / 2f, -TieThickness, TieThickness);

            int vertexIndex = 0;
            int triangleIndex = 0;

            void AddQuad(float3 bl, float3 br, float3 tl, float3 tr) {
                tieMeshVertices[vertexIndex] = bl;
                tieMeshVertices[vertexIndex + 1] = br;
                tieMeshVertices[vertexIndex + 2] = tl;
                tieMeshVertices[vertexIndex + 3] = tr;

                ushort bli = (ushort)vertexIndex;
                ushort bri = (ushort)(vertexIndex + 1);
                ushort tli = (ushort)(vertexIndex + 2);
                ushort tri = (ushort)(vertexIndex + 3);

                tieMeshTriangles[triangleIndex] = bli;
                tieMeshTriangles[triangleIndex + 1] = tli;
                tieMeshTriangles[triangleIndex + 2] = tri;

                tieMeshTriangles[triangleIndex + 3] = bli;
                tieMeshTriangles[triangleIndex + 4] = tri;
                tieMeshTriangles[triangleIndex + 5] = bri;

                vertexIndex += 4;
                triangleIndex += 6;
            }

            AddQuad(ubl, ubr, utl, utr); // Top
            AddQuad(ltl, ltr, lbl, lbr); // Bottom
            AddQuad(ltl, lbl, utl, ubl); // Left
            AddQuad(utr, ubr, ltr, lbr); // Right
            AddQuad(lbl, lbr, ubl, ubr); // Front
            AddQuad(utl, utr, ltl, ltr); // Back

            _tieMesh = new UnityEngine.Mesh {
                name = "Ties",
                indexFormat = IndexFormat.UInt16,
            };
            _tieMesh.SetVertices(tieMeshVertices);
            _tieMesh.SetIndices(tieMeshTriangles, UnityEngine.MeshTopology.Triangles, 0);

            _tieMesh.RecalculateNormals();
            _tieMesh.RecalculateBounds();

            tieMeshVertices.Dispose();
            tieMeshTriangles.Dispose();
        }

        private void OnDestroy() {
            _tieMatrices.Dispose();
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

            if (_tieMatrices.Length > 0) {
                UnityEngine.RenderParams rp = new(TrackMaterial);
                UnityEngine.Graphics.RenderMeshInstanced(rp, _tieMesh, 0, _tieMatrices);
            }
        }

        public void RequestRebuild() {
            _needsRebuild = true;
        }

        private void Build() {
            _tieMatrices.Dispose();

            var nodes = Track.Nodes;

            var trackNodes = new NativeList<Node>(Allocator.TempJob);
            var tieNodes = new NativeList<Node>(Allocator.TempJob);
            new CopyNodesJob {
                TrackNodes = trackNodes,
                TieNodes = tieNodes,
                Nodes = nodes,
                Resolution = Resolution,
            }.Schedule().Complete();

            var meshDataArray = UnityEngine.Mesh.AllocateWritableMeshData(2);

            int nodeCount = trackNodes.Length - 1;

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

            new BuildTrackMeshJob {
                MeshDataArray = meshDataArray,
                TrackNodes = trackNodes.AsArray(),
                StartOffset = StartOffset,
                TrackGauge = TrackGauge,
                TopperWidth = TopperWidth,
                TopperThickness = TopperThickness,
                TwoByTenWidth = TwoByTenWidth,
                TwoByTenThickness = TwoByTenThickness,
                UpperLayersGauge = UpperLayersGauge,
                LowerLayersGauge = LowerLayersGauge,
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

            UnityEngine.Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, _trackMeshes);

            _trackMeshes[0].RecalculateBounds();
            _trackMeshes[1].RecalculateBounds();

            trackNodes.Dispose();

            _tieMatrices = new NativeArray<UnityEngine.Matrix4x4>(tieNodes.Length, Allocator.Persistent);
            new BuildTiesJob {
                Matrices = _tieMatrices,
                TieNodes = tieNodes.AsArray(),
                StartOffset = StartOffset,
                TopperThickness = TopperThickness,
                TwoByTenThickness = TwoByTenThickness,
                TieThickness = TieThickness,
                UpperLayers = UpperLayers,
                LowerLayers = LowerLayers,
            }.Schedule(tieNodes.Length, tieNodes.Length / 64).Complete();

            tieNodes.Dispose();
        }

        [BurstCompile]
        private struct CopyNodesJob : IJob {
            [WriteOnly]
            public NativeList<Node> TrackNodes;

            [WriteOnly]
            public NativeList<Node> TieNodes;

            [ReadOnly]
            public NativeArray<Node> Nodes;

            [ReadOnly]
            public int Resolution;

            public void Execute() {
                float nodeDistance = 0.7f / Resolution;
                float distFromLast = nodeDistance;
                int count = 0;
                for (int i = 0; i < Nodes.Length - 1; i++) {
                    var node = Nodes[i];
                    distFromLast += node.DistanceFromLast;
                    if (distFromLast >= nodeDistance) {
                        distFromLast -= nodeDistance;
                        if (count % Resolution == 0) {
                            TieNodes.Add(node);
                        }
                        TrackNodes.Add(node);
                        count++;
                    }
                }
            }
        }

        [BurstCompile]
        private struct BuildTrackMeshJob : IJobParallelFor {
            [NativeDisableParallelForRestriction]
            public UnityEngine.Mesh.MeshDataArray MeshDataArray;

            [ReadOnly]
            public NativeArray<Node> TrackNodes;

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
                var topperMeshData = MeshDataArray[0];
                var vertices = topperMeshData.GetVertexData<float3>(0);
                var normals = topperMeshData.GetVertexData<float3>(1);
                var uvs = topperMeshData.GetVertexData<float2>(2);
                var triangles = topperMeshData.GetIndexData<uint>();

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
                int end = math.min(start + BatchSize, TrackNodes.Length - 1);
                for (int i = start; i < end; i++) {
                    var prev = TrackNodes[i];
                    var current = TrackNodes[i + 1];

                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, topperLeftBL, topperLeftBR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, topperLeftBR, topperLeftTL);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, topperLeftTL, topperLeftTR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, topperLeftTR, topperLeftBL);

                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, topperRightBL, topperRightBR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, topperRightBR, topperRightTL);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, topperRightTL, topperRightTR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, topperRightTR, topperRightBL);
                }

                if (batchIndex == BatchCount - 1) {
                    Node first = TrackNodes[0];
                    Node last = TrackNodes[^1];

                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, first, topperLeftBL, topperLeftTR, topperLeftBR, topperLeftTL);
                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, first, topperRightBL, topperRightTR, topperRightBR, topperRightTL);
                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, last, topperLeftBL, topperLeftBR, topperLeftTR, topperLeftTL);
                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, last, topperRightBL, topperRightBR, topperRightTR, topperRightTL);
                }
            }

            private void BuildTrack(int batchIndex) {
                var trackMeshData = MeshDataArray[1];
                var vertices = trackMeshData.GetVertexData<float3>(0);
                var normals = trackMeshData.GetVertexData<float3>(1);
                var uvs = trackMeshData.GetVertexData<float2>(2);
                var triangles = trackMeshData.GetIndexData<uint>();

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
                int end = math.min(start + BatchSize, TrackNodes.Length - 1);
                for (int i = start; i < end; i++) {
                    var prev = TrackNodes[i];
                    var current = TrackNodes[i + 1];

                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersLeftBL, upperLayersLeftBR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersLeftBR, upperLayersLeftTL);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersLeftTL, upperLayersLeftTR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersLeftTR, upperLayersLeftBL);

                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersRightBL, upperLayersRightBR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersRightBR, upperLayersRightTL);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersRightTL, upperLayersRightTR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, upperLayersRightTR, upperLayersRightBL);

                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersLeftBL, lowerLayersLeftBR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersLeftBR, lowerLayersLeftTL);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersLeftTL, lowerLayersLeftTR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersLeftTR, lowerLayersLeftBL);

                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersRightBL, lowerLayersRightBR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersRightBR, lowerLayersRightTL);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersRightTL, lowerLayersRightTR);
                    AddLongitudinalQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, prev, current, lowerLayersRightTR, lowerLayersRightBL);
                }

                if (batchIndex == BatchCount - 1) {
                    Node first = TrackNodes[0];
                    Node last = TrackNodes[^1];

                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, first, upperLayersLeftBL, upperLayersLeftTR, upperLayersLeftBR, upperLayersLeftTL);
                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, first, upperLayersRightBL, upperLayersRightTR, upperLayersRightBR, upperLayersRightTL);
                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, last, upperLayersLeftBL, upperLayersLeftBR, upperLayersLeftTR, upperLayersLeftTL);
                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, last, upperLayersRightBL, upperLayersRightBR, upperLayersRightTR, upperLayersRightTL);

                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, first, lowerLayersLeftBL, lowerLayersLeftTR, lowerLayersLeftBR, lowerLayersLeftTL);
                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, first, lowerLayersRightBL, lowerLayersRightTR, lowerLayersRightBR, lowerLayersRightTL);
                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, last, lowerLayersLeftBL, lowerLayersLeftBR, lowerLayersLeftTR, lowerLayersLeftTL);
                    AddTransverseQuad(ref vertices, ref normals, ref uvs, ref triangles, ref vertexIndex, ref triangleIndex, last, lowerLayersRightBL, lowerLayersRightBR, lowerLayersRightTR, lowerLayersRightTL);
                }
            }

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

        [BurstCompile]
        private struct BuildTiesJob : IJobParallelFor {
            [WriteOnly]
            public NativeArray<UnityEngine.Matrix4x4> Matrices;

            [ReadOnly]
            public NativeArray<Node> TieNodes;

            [ReadOnly]
            public float StartOffset;

            [ReadOnly]
            public float TopperThickness;

            [ReadOnly]
            public float TwoByTenThickness;

            [ReadOnly]
            public float TieThickness;

            [ReadOnly]
            public int UpperLayers;

            [ReadOnly]
            public int LowerLayers;

            public void Execute(int index) {
                var node = TieNodes[index];
                float heart = StartOffset - HEART - TopperThickness - TwoByTenThickness * (UpperLayers + LowerLayers) - TieThickness;
                float3 position = node.GetHeartPosition(-heart);

                float3 forward = node.GetHeartDirection(-heart);
                float3 lateral = node.GetHeartLateral(-heart);
                float3 up = math.normalize(math.cross(forward, lateral));
                quaternion rotation = quaternion.LookRotation(forward, up);

                Matrices[index] = UnityEngine.Matrix4x4.TRS(position, rotation, UnityEngine.Vector3.one);
            }
        }
    }
}
