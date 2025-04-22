using UnityEngine;
using System.Runtime.InteropServices;
using System;

namespace CoasterForge {
    public class SectionMeshComputeData : IDisposable {
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
