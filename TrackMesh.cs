using Unity.Mathematics;
using UnityEngine;
using static CoasterForge.Constants;
using Node = CoasterForge.Track.Node;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace CoasterForge {
    public class TrackMesh : MonoBehaviour {
        public Track Track;
        public ComputeShader TrackMeshCompute;
        public Material DuplicationMaterial;
        public Material ExtrusionMaterial;
        public Mesh DuplicationMesh;

        private ComputeBuffer _nodesBuffer;
        private GraphicsBuffer _crossSectionVerticesBuffer;
        private GraphicsBuffer _crossSectionUVsBuffer;
        private GraphicsBuffer _crossSectionNormalsBuffer;
        private GraphicsBuffer _crossSectionTriangulationBuffer;

        private ComputeBuffer _matricesBuffer;
        private ComputeBuffer _duplicationNodesBuffer;
        private ComputeBuffer _extrusionVerticesBuffer;
        private ComputeBuffer _extrusionNormalsBuffer;
        private ComputeBuffer _extrusionIndicesBuffer;

        private GraphicsBuffer _duplicationBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] _duplicationData;
        private RenderParams _duplicationParams;
        private RenderParams _extrusionParams;
        private int _lastSolvedResolution;
        private bool _buffersInitialized;
        private bool _needsRebuild;

        private void Start() {
            _duplicationParams = new RenderParams(DuplicationMaterial) {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f),
                matProps = new MaterialPropertyBlock()
            };

            _extrusionParams = new RenderParams(ExtrusionMaterial) {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f),
                matProps = new MaterialPropertyBlock()
            };

            ComputeCrossSection();
            InitializeBuffers(1);
        }

        private void Extrude(
            ref NativeList<Edge> edges,
            out NativeArray<float3> vertices,
            out NativeArray<float2> uvs,
            out NativeArray<float3> normals,
            out NativeArray<uint> indices
        ) {
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

        private void ComputeCrossSection() {
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

            Extrude(ref edges, out var vertices, out var uvs, out var normals, out var indices);

            _crossSectionVerticesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertices.Length, sizeof(float) * 3);
            _crossSectionUVsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, uvs.Length, sizeof(float) * 2);
            _crossSectionNormalsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, normals.Length, sizeof(float) * 3);
            _crossSectionTriangulationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indices.Length, sizeof(uint));

            _crossSectionVerticesBuffer.SetData(vertices);
            _crossSectionUVsBuffer.SetData(uvs);
            _crossSectionNormalsBuffer.SetData(normals);
            _crossSectionTriangulationBuffer.SetData(indices);

            vertices.Dispose();
            uvs.Dispose();
            normals.Dispose();
            indices.Dispose();
            edges.Dispose();
        }

        private void InitializeBuffers(int nodeCount) {
            DisposeBuffers();

            int nodeStride = Marshal.SizeOf<Node>();
            _nodesBuffer = new ComputeBuffer(math.max(1, nodeCount), nodeStride);

            _matricesBuffer = new ComputeBuffer(nodeCount, 16 * sizeof(float));
            _duplicationNodesBuffer = new ComputeBuffer(nodeCount, sizeof(uint));
            _extrusionVerticesBuffer = new ComputeBuffer(nodeCount * _crossSectionVerticesBuffer.count, sizeof(float) * 3);
            _extrusionNormalsBuffer = new ComputeBuffer(nodeCount * _crossSectionVerticesBuffer.count, sizeof(float) * 3);
            _extrusionIndicesBuffer = new ComputeBuffer(nodeCount * _crossSectionTriangulationBuffer.count, sizeof(uint));
            _duplicationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);

            _duplicationData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            _duplicationData[0].indexCountPerInstance = DuplicationMesh.GetIndexCount(0);
            _duplicationData[0].instanceCount = (uint)nodeCount;
            _duplicationBuffer.SetData(_duplicationData);

            DuplicationMaterial.SetBuffer("_Matrices", _matricesBuffer);
            DuplicationMaterial.SetBuffer("_DuplicationNodes", _duplicationNodesBuffer);
            DuplicationMaterial.SetInt("_NodeCount", nodeCount);

            _extrusionParams.matProps.SetBuffer("_Vertices", _extrusionVerticesBuffer);
            _extrusionParams.matProps.SetBuffer("_UVs", _crossSectionUVsBuffer);
            _extrusionParams.matProps.SetBuffer("_Normals", _extrusionNormalsBuffer);
            _extrusionParams.matProps.SetBuffer("_Triangles", _extrusionIndicesBuffer);
            _extrusionParams.matProps.SetInt("_UVCount", _crossSectionUVsBuffer.count);

            _buffersInitialized = true;
        }

        private void DisposeBuffers() {
            _nodesBuffer?.Dispose();
            _extrusionVerticesBuffer?.Dispose();
            _extrusionNormalsBuffer?.Dispose();
            _extrusionIndicesBuffer?.Dispose();
            _matricesBuffer?.Dispose();
            _duplicationNodesBuffer?.Dispose();
            _duplicationBuffer?.Dispose();
        }

        private void OnDestroy() {
            DisposeBuffers();
            _crossSectionVerticesBuffer?.Dispose();
            _crossSectionUVsBuffer?.Dispose();
            _crossSectionNormalsBuffer?.Dispose();
            _crossSectionTriangulationBuffer?.Dispose();
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

            if (_buffersInitialized) {
                Graphics.RenderMeshIndirect(
                    _duplicationParams,
                    DuplicationMesh,
                    _duplicationBuffer,
                    1
                );

                Graphics.RenderPrimitives(
                    _extrusionParams,
                    MeshTopology.Triangles,
                    _extrusionIndicesBuffer.count,
                    1
                );
            }
        }

        public void RequestRebuild() {
            _needsRebuild = true;
        }

        private void Build() {
            var nodes = Track.Nodes;

            if (!_buffersInitialized || _nodesBuffer.count < nodes.Length) {
                InitializeBuffers(nodes.Length);
            }

            _nodesBuffer.SetData(nodes);

            int kernel = TrackMeshCompute.FindKernel("CSMain");

            TrackMeshCompute.SetBuffer(kernel, "_Nodes", _nodesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_CrossSectionVertices", _crossSectionVerticesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_CrossSectionUVs", _crossSectionUVsBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_CrossSectionNormals", _crossSectionNormalsBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_CrossSectionTriangulation", _crossSectionTriangulationBuffer);

            TrackMeshCompute.SetBuffer(kernel, "_Matrices", _matricesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_DuplicationNodes", _duplicationNodesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_ExtrusionVertices", _extrusionVerticesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_ExtrusionNormals", _extrusionNormalsBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_ExtrusionIndices", _extrusionIndicesBuffer);

            TrackMeshCompute.SetFloat("_Heart", HEART);

            TrackMeshCompute.GetKernelThreadGroupSizes(kernel, out uint threadGroupSize, out _, out _);
            int threadGroups = (int)math.ceil(nodes.Length / (float)threadGroupSize);

            TrackMeshCompute.Dispatch(kernel, threadGroups, 1, 1);
        }

        public struct Edge {
            public float3 A;
            public float3 B;
            public float2 UV;
        }
    }
}
