using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static CoasterForge.Constants;
using Unity.Entities;

namespace CoasterForge {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class MeshSystem : SystemBase {
        private GraphicsBuffer _crossSectionVerticesBuffer;
        private GraphicsBuffer _crossSectionUVsBuffer;
        private GraphicsBuffer _crossSectionNormalsBuffer;
        private GraphicsBuffer _crossSectionTriangulationBuffer;

        private Bounds _worldBounds;

        protected override void OnCreate() {
            RequireForUpdate<GlobalMeshData>();
            RequireForUpdate<MeshData>();
        }

        protected override void OnStartRunning() {
            var globalData = SystemAPI.ManagedAPI.GetSingleton<GlobalMeshData>();

            Extensions.ComputeRailCrossSection(out var vertices, out var uvs, out var normals, out var indices);

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

            _worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
        }

        protected override void OnStopRunning() {
            _crossSectionVerticesBuffer?.Dispose();
            _crossSectionUVsBuffer?.Dispose();
            _crossSectionNormalsBuffer?.Dispose();
            _crossSectionTriangulationBuffer?.Dispose();
        }

        protected override void OnUpdate() {
            var globalData = SystemAPI.ManagedAPI.GetSingleton<GlobalMeshData>();

            foreach (var data in SystemAPI.Query<MeshData>()) {
                if (data.CurrentBuffers == null) {
                    data.CurrentBuffers = new MeshComputeData();
                    data.CurrentBuffers.Initialize(
                        1,
                        _crossSectionVerticesBuffer,
                        _crossSectionUVsBuffer,
                        _crossSectionTriangulationBuffer,
                        globalData.DuplicationMesh
                    );
                }

                if (data.ComputeFence == null) {
                    Rebuild(globalData, data);
                }

                if (data.ComputeFence != null && data.ComputeFence.Value.done) {
                    if (data.NextBuffers != null) {
                        (data.CurrentBuffers, data.NextBuffers) = (data.NextBuffers, data.CurrentBuffers);
                    }
                    data.ComputeFence = null;
                }

                var duplicationParams = new RenderParams(globalData.DuplicationMat) {
                    worldBounds = _worldBounds,
                    matProps = data.CurrentBuffers.DuplicationMatProps
                };

                var extrusionParams = new RenderParams(globalData.ExtrusionMat) {
                    worldBounds = _worldBounds,
                    matProps = data.CurrentBuffers.ExtrusionMatProps
                };

                Graphics.RenderMeshIndirect(
                    duplicationParams,
                    globalData.DuplicationMesh,
                    data.CurrentBuffers.DuplicationBuffer,
                    1
                );

                Graphics.RenderPrimitives(
                    extrusionParams,
                    MeshTopology.Triangles,
                    data.CurrentBuffers.ExtrusionIndicesBuffer.count,
                    1
                );
            }
        }

        private void Rebuild(in GlobalMeshData globalData, MeshData data) {
            var points = SystemAPI.GetBuffer<Point>(data.Entity);
            if (points.Length == 0) return;

            if (data.NextBuffers == null || data.NextBuffers.PointsBuffer.count != points.Length) {
                data.NextBuffers?.Dispose();
                data.NextBuffers = new MeshComputeData();
                data.NextBuffers.Initialize(
                    points.Length,
                    _crossSectionVerticesBuffer,
                    _crossSectionUVsBuffer,
                    _crossSectionTriangulationBuffer,
                    globalData.DuplicationMesh
                );
            }

            data.NextBuffers.PointsBuffer.SetData(points.AsNativeArray(), 0, 0, points.Length);

            int kernel = globalData.Compute.FindKernel("CSMain");

            globalData.Compute.SetBuffer(kernel, "_CrossSectionVertices", _crossSectionVerticesBuffer);
            globalData.Compute.SetBuffer(kernel, "_CrossSectionUVs", _crossSectionUVsBuffer);
            globalData.Compute.SetBuffer(kernel, "_CrossSectionNormals", _crossSectionNormalsBuffer);
            globalData.Compute.SetBuffer(kernel, "_CrossSectionTriangulation", _crossSectionTriangulationBuffer);

            globalData.Compute.SetBuffer(kernel, "_Points", data.NextBuffers.PointsBuffer);
            globalData.Compute.SetBuffer(kernel, "_Matrices", data.NextBuffers.MatricesBuffer);
            globalData.Compute.SetBuffer(kernel, "_DuplicationPoints", data.NextBuffers.DuplicationPointsBuffer);
            globalData.Compute.SetBuffer(kernel, "_ExtrusionVertices", data.NextBuffers.ExtrusionVerticesBuffer);
            globalData.Compute.SetBuffer(kernel, "_ExtrusionNormals", data.NextBuffers.ExtrusionNormalsBuffer);
            globalData.Compute.SetBuffer(kernel, "_ExtrusionIndices", data.NextBuffers.ExtrusionIndicesBuffer);

            globalData.Compute.SetFloat("_Heart", HEART);
            globalData.Compute.SetFloat("_PointCount", points.Length);
            globalData.Compute.SetFloat("_TieSpacing", TIE_SPACING);

            globalData.Compute.GetKernelThreadGroupSizes(kernel, out uint threadGroupSize, out _, out _);
            int threadGroups = (int)math.ceil(points.Length / (float)threadGroupSize);

            globalData.Compute.Dispatch(kernel, threadGroups, 1, 1);

            data.ComputeFence = AsyncGPUReadback.Request(data.NextBuffers.PointsBuffer);
        }
    }
}
