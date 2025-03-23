using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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

        private NativeList<float3> _topperVertices;
        private NativeList<float3> _topperNormals;
        private NativeList<float2> _topperUVs;
        private NativeList<int> _topperTriangles;

        private NativeList<float3> _trackVertices;
        private NativeList<float3> _trackNormals;
        private NativeList<float2> _trackUVs;
        private NativeList<int> _trackTriangles;

        private UnityEngine.Mesh _topperMesh;
        private UnityEngine.Mesh _trackMesh;

        private void Start() {
            _topperVertices = new NativeList<float3>(Allocator.Persistent);
            _topperNormals = new NativeList<float3>(Allocator.Persistent);
            _topperUVs = new NativeList<float2>(Allocator.Persistent);
            _topperTriangles = new NativeList<int>(Allocator.Persistent);
            _topperMesh = new UnityEngine.Mesh {
                name = "Topper",
            };
            TopperMeshFilter.mesh = _topperMesh;

            _trackVertices = new NativeList<float3>(Allocator.Persistent);
            _trackNormals = new NativeList<float3>(Allocator.Persistent);
            _trackUVs = new NativeList<float2>(Allocator.Persistent);
            _trackTriangles = new NativeList<int>(Allocator.Persistent);
            _trackMesh = new UnityEngine.Mesh {
                name = "Track",
            };
            TrackMeshFilter.mesh = _trackMesh;
        }

        private void OnDestroy() {
            _topperVertices.Dispose();
            _topperNormals.Dispose();
            _topperUVs.Dispose();
            _topperTriangles.Dispose();

            _trackVertices.Dispose();
            _trackNormals.Dispose();
            _trackUVs.Dispose();
            _trackTriangles.Dispose();
        }

        public void Build() {
            _topperVertices.Clear();
            _topperNormals.Clear();
            _topperUVs.Clear();
            _topperTriangles.Clear();

            _trackVertices.Clear();
            _trackNormals.Clear();
            _trackUVs.Clear();
            _trackTriangles.Clear();

            var trackNodes = Track.Nodes;
            var nodes = new NativeList<Node>(Allocator.TempJob);

            float nodeDistance = 0.7f / Resolution;
            float distFromLast = nodeDistance;
            for (int i = 0; i < trackNodes.Length - 1; i++) {
                var node = trackNodes[i];
                distFromLast += node.DistanceFromLast;
                if (distFromLast >= nodeDistance) {
                    distFromLast -= nodeDistance;
                    nodes.Add(node);
                }
            }

            var jobHandle = new JobHandle();
            jobHandle = new BuildJob {
                Vertices = _topperVertices,
                Normals = _topperNormals,
                UVs = _topperUVs,
                Triangles = _topperTriangles,
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
                BuildType = BuildType.Topper,
            }.Schedule(jobHandle);
            jobHandle = new BuildJob {
                Vertices = _trackVertices,
                Normals = _trackNormals,
                UVs = _trackUVs,
                Triangles = _trackTriangles,
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
                BuildType = BuildType.Track,
            }.Schedule(jobHandle);
            jobHandle.Complete();

            _topperMesh.SetVertices(_topperVertices.AsArray());
            _topperMesh.SetNormals(_topperNormals.AsArray());
            _topperMesh.SetUVs(0, _topperUVs.AsArray());
            _topperMesh.SetIndices(_topperTriangles.AsArray(), UnityEngine.MeshTopology.Triangles, 0);

            _trackMesh.SetVertices(_trackVertices.AsArray());
            _trackMesh.SetNormals(_trackNormals.AsArray());
            _trackMesh.SetUVs(0, _trackUVs.AsArray());
            _trackMesh.SetIndices(_trackTriangles.AsArray(), UnityEngine.MeshTopology.Triangles, 0);

            nodes.Dispose();
        }

        [BurstCompile]
        private struct BuildJob : IJob {
            [WriteOnly]
            public NativeList<float3> Vertices;

            [WriteOnly]
            public NativeList<float3> Normals;

            [WriteOnly]
            public NativeList<float2> UVs;

            [WriteOnly]
            public NativeList<int> Triangles;

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
            public BuildType BuildType;

            private int _vertexIndex;

            public void Execute() {
                _vertexIndex = 0;

                switch (BuildType) {
                    case BuildType.Track:
                        BuildTrack();
                        break;
                    case BuildType.Topper:
                        BuildTopper();
                        break;
                    default:
                        throw new System.ArgumentException("Invalid build type");
                }
            }

            private void BuildTrack() {
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

                for (int i = 0; i < Nodes.Length - 1; i++) {
                    var prev = Nodes[i];
                    var current = Nodes[i + 1];

                    AppendQuad(prev, current, upperLayersLeftBL, upperLayersLeftBR);
                    AppendQuad(prev, current, upperLayersLeftBR, upperLayersLeftTL);
                    AppendQuad(prev, current, upperLayersLeftTL, upperLayersLeftTR);
                    AppendQuad(prev, current, upperLayersLeftTR, upperLayersLeftBL);

                    AppendQuad(prev, current, upperLayersRightBL, upperLayersRightBR);
                    AppendQuad(prev, current, upperLayersRightBR, upperLayersRightTL);
                    AppendQuad(prev, current, upperLayersRightTL, upperLayersRightTR);
                    AppendQuad(prev, current, upperLayersRightTR, upperLayersRightBL);

                    AppendQuad(prev, current, lowerLayersLeftBL, lowerLayersLeftBR);
                    AppendQuad(prev, current, lowerLayersLeftBR, lowerLayersLeftTL);
                    AppendQuad(prev, current, lowerLayersLeftTL, lowerLayersLeftTR);
                    AppendQuad(prev, current, lowerLayersLeftTR, lowerLayersLeftBL);

                    AppendQuad(prev, current, lowerLayersRightBL, lowerLayersRightBR);
                    AppendQuad(prev, current, lowerLayersRightBR, lowerLayersRightTL);
                    AppendQuad(prev, current, lowerLayersRightTL, lowerLayersRightTR);
                    AppendQuad(prev, current, lowerLayersRightTR, lowerLayersRightBL);

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

                        AppendQuad(prev, tieTopBL, tieTopTL, tieTopBR, tieTopTR); // Top
                        AppendQuad(prev, tieBottomTL, tieBottomBL, tieBottomTR, tieBottomBR); //Bottom
                        AppendQuad(prev, tieTopBL, tieTopBR, tieBottomBL, tieBottomBR); // Front
                        AppendQuad(prev, tieBottomTL, tieBottomTR, tieTopTL, tieTopTR); // Back
                        AppendQuad(prev, tieTopTL, tieTopBL, tieBottomTL, tieBottomBL); // Left
                        AppendQuad(prev, tieBottomTR, tieBottomBR, tieTopTR, tieTopBR); // Right
                    }
                }

                Node first = Nodes[0];
                Node last = Nodes[^1];

                AppendQuad(first, upperLayersLeftBL, upperLayersLeftTR, upperLayersLeftBR, upperLayersLeftTL);
                AppendQuad(first, upperLayersRightBL, upperLayersRightTR, upperLayersRightBR, upperLayersRightTL);
                AppendQuad(last, upperLayersLeftBL, upperLayersLeftBR, upperLayersLeftTR, upperLayersLeftTL);
                AppendQuad(last, upperLayersRightBL, upperLayersRightBR, upperLayersRightTR, upperLayersRightTL);

                AppendQuad(first, lowerLayersLeftBL, lowerLayersLeftTR, lowerLayersLeftBR, lowerLayersLeftTL);
                AppendQuad(first, lowerLayersRightBL, lowerLayersRightTR, lowerLayersRightBR, lowerLayersRightTL);
                AppendQuad(last, lowerLayersLeftBL, lowerLayersLeftBR, lowerLayersLeftTR, lowerLayersLeftTL);
                AppendQuad(last, lowerLayersRightBL, lowerLayersRightBR, lowerLayersRightTR, lowerLayersRightTL);
            }

            private void BuildTopper() {
                float heart = StartOffset - HEART;
                var topperLeftBL = new float3(-TrackGauge / 2f - TopperWidth / 2f, heart - TopperThickness, 0);
                var topperLeftBR = new float3(-TrackGauge / 2f + TopperWidth / 2f, heart - TopperThickness, 0);
                var topperLeftTL = new float3(-TrackGauge / 2f + TopperWidth / 2f, heart, 0);
                var topperLeftTR = new float3(-TrackGauge / 2f - TopperWidth / 2f, heart, 0);
                var topperRightBL = new float3(TrackGauge / 2f - TopperWidth / 2f, heart - TopperThickness, 0);
                var topperRightBR = new float3(TrackGauge / 2f + TopperWidth / 2f, heart - TopperThickness, 0);
                var topperRightTL = new float3(TrackGauge / 2f + TopperWidth / 2f, heart, 0);
                var topperRightTR = new float3(TrackGauge / 2f - TopperWidth / 2f, heart, 0);

                for (int i = 0; i < Nodes.Length - 1; i++) {
                    var prev = Nodes[i];
                    var current = Nodes[i + 1];

                    AppendQuad(prev, current, topperLeftBL, topperLeftBR);
                    AppendQuad(prev, current, topperLeftBR, topperLeftTL);
                    AppendQuad(prev, current, topperLeftTL, topperLeftTR);
                    AppendQuad(prev, current, topperLeftTR, topperLeftBL);

                    AppendQuad(prev, current, topperRightBL, topperRightBR);
                    AppendQuad(prev, current, topperRightBR, topperRightTL);
                    AppendQuad(prev, current, topperRightTL, topperRightTR);
                    AppendQuad(prev, current, topperRightTR, topperRightBL);
                }

                Node first = Nodes[0];
                Node last = Nodes[^1];

                AppendQuad(first, topperLeftBL, topperLeftTR, topperLeftBR, topperLeftTL);
                AppendQuad(first, topperRightBL, topperRightTR, topperRightBR, topperRightTL);
                AppendQuad(last, topperLeftBL, topperLeftBR, topperLeftTR, topperLeftTL);
                AppendQuad(last, topperRightBL, topperRightBR, topperRightTR, topperRightTL);
            }

            public void AppendQuad(Node prev, Node curr, float3 l, float3 r, float2? uv = null) {
                float3 bl = prev.GetRelativePosition(l);
                float3 br = prev.GetRelativePosition(r);
                float3 tl = curr.GetRelativePosition(l);
                float3 tr = curr.GetRelativePosition(r);
                AppendQuad(bl, br, tl, tr, uv);
            }

            public void AppendQuad(Node node, float3 bl, float3 br, float3 tl, float3 tr, float2? uv = null) {
                bl = node.GetRelativePosition(bl);
                br = node.GetRelativePosition(br);
                tl = node.GetRelativePosition(tl);
                tr = node.GetRelativePosition(tr);
                AppendQuad(bl, br, tl, tr, uv);
            }

            public void AppendQuad(float3 bl, float3 br, float3 tl, float3 tr, float2? uv = null) {
                Vertices.Add(bl);
                Vertices.Add(br);
                Vertices.Add(tl);
                Vertices.Add(tr);

                float3 normal = math.normalize(math.cross(tl - bl, br - bl));
                Normals.Add(normal);
                Normals.Add(normal);
                Normals.Add(normal);
                Normals.Add(normal);

                uv ??= new float2(1f, 1f);
                UVs.Add(new float2(0f, 0f));
                UVs.Add(new float2(uv.Value.x, 0f));
                UVs.Add(new float2(0f, uv.Value.y));
                UVs.Add(new float2(uv.Value.x, uv.Value.y));

                int bli = _vertexIndex;
                int bri = _vertexIndex + 1;
                int tli = _vertexIndex + 2;
                int tri = _vertexIndex + 3;
                _vertexIndex += 4;

                Triangles.Add(bli);
                Triangles.Add(tli);
                Triangles.Add(tri);

                Triangles.Add(bli);
                Triangles.Add(tri);
                Triangles.Add(bri);
            }
        }

        public enum BuildType {
            Track,
            Topper,
        }
    }
}
