using Unity.Mathematics;
using UnityEngine;
using static CoasterForge.Constants;
using Node = CoasterForge.Section.Node;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

namespace CoasterForge {
    public class TrackMesh : MonoBehaviour {
        public Section Track;
        public ComputeShader TrackMeshCompute;
        public Material DuplicationMaterial;
        public Material ExtrusionMaterial;
        public Mesh DuplicationMesh;

        private GraphicsBuffer _crossSectionVerticesBuffer;
        private GraphicsBuffer _crossSectionUVsBuffer;
        private GraphicsBuffer _crossSectionNormalsBuffer;
        private GraphicsBuffer _crossSectionTriangulationBuffer;

        private AsyncGPUReadbackRequest? _computeFence;
        private ComputeData _currentBuffers;
        private ComputeData _nextBuffers;
        private RenderParams _duplicationParams;
        private RenderParams _extrusionParams;

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

            _currentBuffers = new ComputeData();
            _currentBuffers.Initialize(
                1,
                _crossSectionVerticesBuffer,
                _crossSectionUVsBuffer,
                _crossSectionTriangulationBuffer,
                DuplicationMesh,
                DuplicationMaterial,
                _extrusionParams.matProps
            );
            _nextBuffers = null;
        }

        private void ComputeCrossSection() {
            Utils.ComputeRailCrossSection(out var vertices, out var uvs, out var normals, out var indices);

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
        }

        private void OnDestroy() {
            _currentBuffers.Dispose();
            _nextBuffers?.Dispose();
            _crossSectionVerticesBuffer?.Dispose();
            _crossSectionUVsBuffer?.Dispose();
            _crossSectionNormalsBuffer?.Dispose();
            _crossSectionTriangulationBuffer?.Dispose();
        }

        private void Update() {
            bool shouldRebuild = _computeFence == null && Track.NodeCount > 0;

            if (shouldRebuild) {
                Build();
            }

            if (_computeFence != null && _computeFence.Value.done) {
                if (_nextBuffers != null) {
                    (_currentBuffers, _nextBuffers) = (_nextBuffers, _currentBuffers);
                }
                _computeFence = null;
            }

            Graphics.RenderMeshIndirect(
                _duplicationParams,
                DuplicationMesh,
                _currentBuffers.DuplicationBuffer,
                1
            );

            Graphics.RenderPrimitives(
                _extrusionParams,
                MeshTopology.Triangles,
                _currentBuffers.ExtrusionIndicesBuffer.count,
                1
            );
        }

        private void Build() {
            int nodeCount = Track.NodeCount;
            var nodes = Track.Nodes;

            if (_nextBuffers == null || _nextBuffers.NodesBuffer.count != nodeCount) {
                _nextBuffers?.Dispose();
                _nextBuffers = new ComputeData();
                _nextBuffers.Initialize(
                    nodeCount,
                    _crossSectionVerticesBuffer,
                    _crossSectionUVsBuffer,
                    _crossSectionTriangulationBuffer,
                    DuplicationMesh,
                    DuplicationMaterial,
                    _extrusionParams.matProps
                );
            }

            _nextBuffers.NodesBuffer.SetData(nodes.AsArray(), 0, 0, nodeCount);

            int kernel = TrackMeshCompute.FindKernel("CSMain");

            TrackMeshCompute.SetBuffer(kernel, "_CrossSectionVertices", _crossSectionVerticesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_CrossSectionUVs", _crossSectionUVsBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_CrossSectionNormals", _crossSectionNormalsBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_CrossSectionTriangulation", _crossSectionTriangulationBuffer);

            TrackMeshCompute.SetBuffer(kernel, "_Nodes", _nextBuffers.NodesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_Matrices", _nextBuffers.MatricesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_DuplicationNodes", _nextBuffers.DuplicationNodesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_ExtrusionVertices", _nextBuffers.ExtrusionVerticesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_ExtrusionNormals", _nextBuffers.ExtrusionNormalsBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_ExtrusionIndices", _nextBuffers.ExtrusionIndicesBuffer);

            TrackMeshCompute.SetFloat("_Heart", HEART);
            TrackMeshCompute.SetFloat("_NodeCount", nodeCount);

            TrackMeshCompute.GetKernelThreadGroupSizes(kernel, out uint threadGroupSize, out _, out _);
            int threadGroups = (int)math.ceil(nodeCount / (float)threadGroupSize);

            TrackMeshCompute.Dispatch(kernel, threadGroups, 1, 1);

            _computeFence = AsyncGPUReadback.Request(_nextBuffers.NodesBuffer);
        }

        public class ComputeData {
            public ComputeBuffer NodesBuffer;
            public ComputeBuffer MatricesBuffer;
            public ComputeBuffer DuplicationNodesBuffer;
            public ComputeBuffer ExtrusionVerticesBuffer;
            public ComputeBuffer ExtrusionNormalsBuffer;
            public ComputeBuffer ExtrusionIndicesBuffer;
            public GraphicsBuffer DuplicationBuffer;

            private GraphicsBuffer _crossSectionVerticesBuffer;
            private GraphicsBuffer _crossSectionUVsBuffer;
            private GraphicsBuffer _crossSectionTriangulationBuffer;
            private Material _duplicationMaterial;
            private MaterialPropertyBlock _extrusionMatProps;

            public void Initialize(
                int nodeCount,
                GraphicsBuffer crossSectionVerticesBuffer,
                GraphicsBuffer crossSectionUVsBuffer,
                GraphicsBuffer crossSectionTriangulationBuffer,
                Mesh duplicationMesh,
                Material duplicationMaterial,
                MaterialPropertyBlock extrusionMatProps
            ) {
                Dispose();

                _crossSectionVerticesBuffer = crossSectionVerticesBuffer;
                _crossSectionUVsBuffer = crossSectionUVsBuffer;
                _crossSectionTriangulationBuffer = crossSectionTriangulationBuffer;
                _duplicationMaterial = duplicationMaterial;
                _extrusionMatProps = extrusionMatProps;

                NodesBuffer = new ComputeBuffer(nodeCount, Marshal.SizeOf<Node>());
                MatricesBuffer = new ComputeBuffer(nodeCount, 16 * sizeof(float));
                DuplicationNodesBuffer = new ComputeBuffer(nodeCount, sizeof(uint));
                ExtrusionVerticesBuffer = new ComputeBuffer(nodeCount * _crossSectionVerticesBuffer.count, sizeof(float) * 3);
                ExtrusionNormalsBuffer = new ComputeBuffer(nodeCount * _crossSectionVerticesBuffer.count, sizeof(float) * 3);
                ExtrusionIndicesBuffer = new ComputeBuffer(nodeCount * _crossSectionTriangulationBuffer.count, sizeof(uint));
                DuplicationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);

                var duplicationData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
                duplicationData[0].indexCountPerInstance = duplicationMesh.GetIndexCount(0);
                duplicationData[0].instanceCount = (uint)nodeCount;
                DuplicationBuffer.SetData(duplicationData);

                BindToMaterials();
            }

            public void BindToMaterials() {
                _duplicationMaterial.SetBuffer("_Matrices", MatricesBuffer);
                _duplicationMaterial.SetBuffer("_DuplicationNodes", DuplicationNodesBuffer);
                _duplicationMaterial.SetInt("_NodeCount", NodesBuffer.count);

                _extrusionMatProps.SetBuffer("_Vertices", ExtrusionVerticesBuffer);
                _extrusionMatProps.SetBuffer("_UVs", _crossSectionUVsBuffer);
                _extrusionMatProps.SetBuffer("_Normals", ExtrusionNormalsBuffer);
                _extrusionMatProps.SetBuffer("_Triangles", ExtrusionIndicesBuffer);
                _extrusionMatProps.SetInt("_UVCount", _crossSectionUVsBuffer.count);
            }

            public void Dispose() {
                NodesBuffer?.Dispose();
                MatricesBuffer?.Dispose();
                DuplicationNodesBuffer?.Dispose();
                ExtrusionVerticesBuffer?.Dispose();
                ExtrusionNormalsBuffer?.Dispose();
                ExtrusionIndicesBuffer?.Dispose();
                DuplicationBuffer?.Dispose();
            }
        }


    }
}
