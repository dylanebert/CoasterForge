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

        private NativeList<Node> _nodes;

        private UnityEngine.Mesh[] _meshes;
        private int _trackNodeCount;
        private bool _needsRebuild;
        private bool _initialized;

        private void Initialize() {
            if (_initialized) {
                Dispose();
            }

            _nodes = new NativeList<Node>(Allocator.Persistent);

            var trackNodes = Track.Nodes;
            _trackNodeCount = trackNodes.Length;
            float nodeDistance = 0.7f / Resolution;
            float distFromLast = nodeDistance;
            for (int i = 0; i < trackNodes.Length - 1; i++) {
                var node = trackNodes[i];
                distFromLast += node.DistanceFromLast;
                if (distFromLast >= nodeDistance) {
                    distFromLast -= nodeDistance;
                    _nodes.Add(node);
                }
            }

            _initialized = true;
        }

        private void Dispose() {
            if (!_initialized) return;
            _nodes.Dispose();
            _initialized = false;
        }

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

        private void OnDestroy() {
            Dispose();
        }

        private void Update() {
            if (_needsRebuild) {
                Build();
                _needsRebuild = false;
            }
        }

        public void RequestRebuild() {
            _needsRebuild = true;
        }

        private void Build() {
            if (!_initialized || Track.Nodes.Length != _trackNodeCount) {
                Initialize();
            }

            var meshDataArray = UnityEngine.Mesh.AllocateWritableMeshData(2);

            int nodeCount = _nodes.Length - 1;

            int topperQuadCount = nodeCount * 8 + 4;
            int topperVertexCount = topperQuadCount * 4;
            int topperTriangleCount = topperQuadCount * 6;

            int trackTieCount = nodeCount / Resolution;
            int trackQuadCount = nodeCount * 16 + trackTieCount * 6 + 8;
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
                IndexFormat.UInt16
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
                IndexFormat.UInt16
            );

            new BuildJob {
                MeshDataArray = meshDataArray,
                Nodes = _nodes.AsArray(),
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
            }.Schedule(2, 1).Complete();

            UnityEngine.Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, _meshes);

            _meshes[0].RecalculateBounds();
            _meshes[1].RecalculateBounds();
        }

        [BurstCompile]
        private struct BuildJob : IJobParallelFor {
            [NativeDisableParallelForRestriction]
            public UnityEngine.Mesh.MeshDataArray MeshDataArray;

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

            public void Execute(int index) {
                if (index == 0) {
                    BuildTopper();
                }
                else {
                    BuildTrack();
                }
            }

            private void BuildTopper() {
                var topperMeshData = MeshDataArray[0];
                var vertices = topperMeshData.GetVertexData<float3>(0);
                var normals = topperMeshData.GetVertexData<float3>(1);
                var uvs = topperMeshData.GetVertexData<float2>(2);
                var triangles = topperMeshData.GetIndexData<ushort>();

                int vertexIndex = 0;
                int triangleIndex = 0;

                float heart = StartOffset - HEART;
                var topperLeftBL = new float3(-TrackGauge / 2f - TopperWidth / 2f, heart - TopperThickness, 0);
                var topperLeftBR = new float3(-TrackGauge / 2f + TopperWidth / 2f, heart - TopperThickness, 0);
                var topperLeftTL = new float3(-TrackGauge / 2f + TopperWidth / 2f, heart, 0);
                var topperLeftTR = new float3(-TrackGauge / 2f - TopperWidth / 2f, heart, 0);
                var topperRightBL = new float3(TrackGauge / 2f - TopperWidth / 2f, heart - TopperThickness, 0);
                var topperRightBR = new float3(TrackGauge / 2f + TopperWidth / 2f, heart - TopperThickness, 0);
                var topperRightTL = new float3(TrackGauge / 2f + TopperWidth / 2f, heart, 0);
                var topperRightTR = new float3(TrackGauge / 2f - TopperWidth / 2f, heart, 0);

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

                    ushort bli = (ushort)vertexIndex;
                    ushort bri = (ushort)(vertexIndex + 1);
                    ushort tli = (ushort)(vertexIndex + 2);
                    ushort tri = (ushort)(vertexIndex + 3);

                    triangles[triangleIndex] = bli;
                    triangles[triangleIndex + 1] = tli;
                    triangles[triangleIndex + 2] = tri;

                    triangles[triangleIndex + 3] = bli;
                    triangles[triangleIndex + 4] = tri;
                    triangles[triangleIndex + 5] = bri;

                    vertexIndex += 4;
                    triangleIndex += 6;
                }

                void AddLongitudinalQuad(Node prev, Node curr, float3 l, float3 r) {
                    float3 bl = prev.GetRelativePosition(l);
                    float3 br = prev.GetRelativePosition(r);
                    float3 tl = curr.GetRelativePosition(l);
                    float3 tr = curr.GetRelativePosition(r);

                    AddQuad(bl, br, tl, tr);
                }

                void AddTransverseQuad(Node node, float3 bl, float3 br, float3 tl, float3 tr) {
                    float3 bl2 = node.GetRelativePosition(bl);
                    float3 br2 = node.GetRelativePosition(br);
                    float3 tl2 = node.GetRelativePosition(tl);
                    float3 tr2 = node.GetRelativePosition(tr);

                    AddQuad(bl2, br2, tl2, tr2);
                }

                for (int i = 0; i < Nodes.Length - 1; i++) {
                    var prev = Nodes[i];
                    var current = Nodes[i + 1];

                    AddLongitudinalQuad(prev, current, topperLeftBL, topperLeftBR);
                    AddLongitudinalQuad(prev, current, topperLeftBR, topperLeftTL);
                    AddLongitudinalQuad(prev, current, topperLeftTL, topperLeftTR);
                    AddLongitudinalQuad(prev, current, topperLeftTR, topperLeftBL);

                    AddLongitudinalQuad(prev, current, topperRightBL, topperRightBR);
                    AddLongitudinalQuad(prev, current, topperRightBR, topperRightTL);
                    AddLongitudinalQuad(prev, current, topperRightTL, topperRightTR);
                    AddLongitudinalQuad(prev, current, topperRightTR, topperRightBL);
                }

                Node first = Nodes[0];
                Node last = Nodes[^1];

                AddTransverseQuad(first, topperLeftBL, topperLeftTR, topperLeftBR, topperLeftTL);
                AddTransverseQuad(first, topperRightBL, topperRightTR, topperRightBR, topperRightTL);
                AddTransverseQuad(last, topperLeftBL, topperLeftBR, topperLeftTR, topperLeftTL);
                AddTransverseQuad(last, topperRightBL, topperRightBR, topperRightTR, topperRightTL);

                topperMeshData.subMeshCount = 1;
                topperMeshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndex));
            }

            private void BuildTrack() {
                var trackMeshData = MeshDataArray[1];
                var vertices = trackMeshData.GetVertexData<float3>(0);
                var normals = trackMeshData.GetVertexData<float3>(1);
                var uvs = trackMeshData.GetVertexData<float2>(2);
                var triangles = trackMeshData.GetIndexData<ushort>();

                int vertexIndex = 0;
                int triangleIndex = 0;

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

                    ushort bli = (ushort)vertexIndex;
                    ushort bri = (ushort)(vertexIndex + 1);
                    ushort tli = (ushort)(vertexIndex + 2);
                    ushort tri = (ushort)(vertexIndex + 3);

                    triangles[triangleIndex] = bli;
                    triangles[triangleIndex + 1] = tli;
                    triangles[triangleIndex + 2] = tri;

                    triangles[triangleIndex + 3] = bli;
                    triangles[triangleIndex + 4] = tri;
                    triangles[triangleIndex + 5] = bri;

                    vertexIndex += 4;
                    triangleIndex += 6;
                }

                void AddLongitudinalQuad(Node prev, Node curr, float3 l, float3 r) {
                    float3 bl = prev.GetRelativePosition(l);
                    float3 br = prev.GetRelativePosition(r);
                    float3 tl = curr.GetRelativePosition(l);
                    float3 tr = curr.GetRelativePosition(r);

                    AddQuad(bl, br, tl, tr);
                }

                void AddTransverseQuad(Node node, float3 bl, float3 br, float3 tl, float3 tr) {
                    float3 bl2 = node.GetRelativePosition(bl);
                    float3 br2 = node.GetRelativePosition(br);
                    float3 tl2 = node.GetRelativePosition(tl);
                    float3 tr2 = node.GetRelativePosition(tr);

                    AddQuad(bl2, br2, tl2, tr2);
                }

                for (int i = 0; i < Nodes.Length - 1; i++) {
                    var prev = Nodes[i];
                    var current = Nodes[i + 1];

                    AddLongitudinalQuad(prev, current, upperLayersLeftBL, upperLayersLeftBR);
                    AddLongitudinalQuad(prev, current, upperLayersLeftBR, upperLayersLeftTL);
                    AddLongitudinalQuad(prev, current, upperLayersLeftTL, upperLayersLeftTR);
                    AddLongitudinalQuad(prev, current, upperLayersLeftTR, upperLayersLeftBL);

                    AddLongitudinalQuad(prev, current, upperLayersRightBL, upperLayersRightBR);
                    AddLongitudinalQuad(prev, current, upperLayersRightBR, upperLayersRightTL);
                    AddLongitudinalQuad(prev, current, upperLayersRightTL, upperLayersRightTR);
                    AddLongitudinalQuad(prev, current, upperLayersRightTR, upperLayersRightBL);

                    AddLongitudinalQuad(prev, current, lowerLayersLeftBL, lowerLayersLeftBR);
                    AddLongitudinalQuad(prev, current, lowerLayersLeftBR, lowerLayersLeftTL);
                    AddLongitudinalQuad(prev, current, lowerLayersLeftTL, lowerLayersLeftTR);
                    AddLongitudinalQuad(prev, current, lowerLayersLeftTR, lowerLayersLeftBL);

                    AddLongitudinalQuad(prev, current, lowerLayersRightBL, lowerLayersRightBR);
                    AddLongitudinalQuad(prev, current, lowerLayersRightBR, lowerLayersRightTL);
                    AddLongitudinalQuad(prev, current, lowerLayersRightTL, lowerLayersRightTR);
                    AddLongitudinalQuad(prev, current, lowerLayersRightTR, lowerLayersRightBL);

                    // Ties
                    if (i % Resolution == 0) {
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

                        AddTransverseQuad(prev, tieTopBL, tieTopTL, tieTopBR, tieTopTR); // Top
                        AddTransverseQuad(prev, tieBottomTL, tieBottomBL, tieBottomTR, tieBottomBR); //Bottom
                        AddTransverseQuad(prev, tieTopBL, tieTopBR, tieBottomBL, tieBottomBR); // Front
                        AddTransverseQuad(prev, tieBottomTL, tieBottomTR, tieTopTL, tieTopTR); // Back
                        AddTransverseQuad(prev, tieTopTL, tieTopBL, tieBottomTL, tieBottomBL); // Left
                        AddTransverseQuad(prev, tieBottomTR, tieBottomBR, tieTopTR, tieTopBR); // Right
                    }
                }

                Node first = Nodes[0];
                Node last = Nodes[^1];

                AddTransverseQuad(first, upperLayersLeftBL, upperLayersLeftTR, upperLayersLeftBR, upperLayersLeftTL);
                AddTransverseQuad(first, upperLayersRightBL, upperLayersRightTR, upperLayersRightBR, upperLayersRightTL);
                AddTransverseQuad(last, upperLayersLeftBL, upperLayersLeftBR, upperLayersLeftTR, upperLayersLeftTL);
                AddTransverseQuad(last, upperLayersRightBL, upperLayersRightBR, upperLayersRightTR, upperLayersRightTL);

                AddTransverseQuad(first, lowerLayersLeftBL, lowerLayersLeftTR, lowerLayersLeftBR, lowerLayersLeftTL);
                AddTransverseQuad(first, lowerLayersRightBL, lowerLayersRightTR, lowerLayersRightBR, lowerLayersRightTL);
                AddTransverseQuad(last, lowerLayersLeftBL, lowerLayersLeftBR, lowerLayersLeftTR, lowerLayersLeftTL);
                AddTransverseQuad(last, lowerLayersRightBL, lowerLayersRightBR, lowerLayersRightTR, lowerLayersRightTL);

                trackMeshData.subMeshCount = 1;
                trackMeshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndex));
            }
        }
    }
}
