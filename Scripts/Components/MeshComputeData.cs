using UnityEngine;
using System.Runtime.InteropServices;
using System;

namespace CoasterForge {
    public class MeshComputeData : IDisposable {
        public ComputeBuffer PointsBuffer;
        public ComputeBuffer MatricesBuffer;
        public ComputeBuffer DuplicationPointsBuffer;
        public ComputeBuffer ExtrusionVerticesBuffer;
        public ComputeBuffer ExtrusionNormalsBuffer;
        public ComputeBuffer ExtrusionIndicesBuffer;
        public GraphicsBuffer DuplicationBuffer;
        public MaterialPropertyBlock DuplicationMatProps;
        public MaterialPropertyBlock ExtrusionMatProps;

        private GraphicsBuffer _crossSectionVerticesBuffer;
        private GraphicsBuffer _crossSectionUVsBuffer;
        private GraphicsBuffer _crossSectionTriangulationBuffer;

        public void Initialize(
            int count,
            GraphicsBuffer crossSectionVerticesBuffer,
            GraphicsBuffer crossSectionUVsBuffer,
            GraphicsBuffer crossSectionTriangulationBuffer,
            Mesh duplicationMesh
        ) {
            Dispose();

            _crossSectionVerticesBuffer = crossSectionVerticesBuffer;
            _crossSectionUVsBuffer = crossSectionUVsBuffer;
            _crossSectionTriangulationBuffer = crossSectionTriangulationBuffer;

            DuplicationMatProps = new MaterialPropertyBlock();
            ExtrusionMatProps = new MaterialPropertyBlock();

            PointsBuffer = new ComputeBuffer(count, Marshal.SizeOf<Point>());
            MatricesBuffer = new ComputeBuffer(count, 16 * sizeof(float));
            DuplicationPointsBuffer = new ComputeBuffer(count, sizeof(uint));
            ExtrusionVerticesBuffer = new ComputeBuffer(count * _crossSectionVerticesBuffer.count, sizeof(float) * 3);
            ExtrusionNormalsBuffer = new ComputeBuffer(count * _crossSectionVerticesBuffer.count, sizeof(float) * 3);
            ExtrusionIndicesBuffer = new ComputeBuffer(count * _crossSectionTriangulationBuffer.count, sizeof(uint));
            DuplicationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);

            var duplicationData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            duplicationData[0].indexCountPerInstance = duplicationMesh.GetIndexCount(0);
            duplicationData[0].instanceCount = (uint)count;
            DuplicationBuffer.SetData(duplicationData);

            BindToMaterials();
        }

        public void BindToMaterials() {
            DuplicationMatProps.SetBuffer("_Matrices", MatricesBuffer);
            DuplicationMatProps.SetBuffer("_DuplicationPoints", DuplicationPointsBuffer);
            DuplicationMatProps.SetInt("_PointCount", PointsBuffer.count);

            ExtrusionMatProps.SetBuffer("_Vertices", ExtrusionVerticesBuffer);
            ExtrusionMatProps.SetBuffer("_UVs", _crossSectionUVsBuffer);
            ExtrusionMatProps.SetBuffer("_Normals", ExtrusionNormalsBuffer);
            ExtrusionMatProps.SetBuffer("_Triangles", ExtrusionIndicesBuffer);
            ExtrusionMatProps.SetInt("_UVCount", _crossSectionUVsBuffer.count);
        }

        public void Dispose() {
            PointsBuffer?.Dispose();
            MatricesBuffer?.Dispose();
            DuplicationPointsBuffer?.Dispose();
            ExtrusionVerticesBuffer?.Dispose();
            ExtrusionNormalsBuffer?.Dispose();
            ExtrusionIndicesBuffer?.Dispose();
            DuplicationBuffer?.Dispose();
        }
    }
}
