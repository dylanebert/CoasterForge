using Unity.Mathematics;
using UnityEngine;
using static CoasterForge.Constants;
using Node = CoasterForge.Track.Node;
using System.Runtime.InteropServices;

namespace CoasterForge {
    public class TrackMesh : MonoBehaviour {
        public Track Track;
        public ComputeShader TrackMeshCompute;
        public Material InstancedMaterial;
        public Mesh InstancedMesh;
        public int LowerLayers = 7;
        public int UpperLayers = 2;

        private ComputeBuffer _nodesBuffer;
        private ComputeBuffer _matricesBuffer;
        private ComputeBuffer _tiesBuffer;
        private GraphicsBuffer _commandBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] _commandData;
        private bool _buffersInitialized;

        private int _lastSolvedResolution;
        private bool _needsRebuild;

        private void Start() {
            InitializeBuffers(1);
        }

        private void InitializeBuffers(int nodeCount) {
            DisposeBuffers();

            int nodeStride = Marshal.SizeOf<Node>();
            _nodesBuffer = new ComputeBuffer(math.max(1, nodeCount), nodeStride);

            _matricesBuffer = new ComputeBuffer(nodeCount, 16 * sizeof(float));
            _tiesBuffer = new ComputeBuffer(nodeCount, sizeof(uint));
            _commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);

            _commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            _commandData[0].indexCountPerInstance = InstancedMesh.GetIndexCount(0);
            _commandData[0].instanceCount = (uint)nodeCount;
            _commandBuffer.SetData(_commandData);

            InstancedMaterial.SetBuffer("_Matrices", _matricesBuffer);
            InstancedMaterial.SetBuffer("_Ties", _tiesBuffer);
            InstancedMaterial.SetInt("_NodeCount", nodeCount);

            _buffersInitialized = true;
        }

        private void DisposeBuffers() {
            _nodesBuffer?.Dispose();
            _matricesBuffer?.Dispose();
            _tiesBuffer?.Dispose();
            _commandBuffer?.Dispose();
        }

        private void OnDestroy() {
            DisposeBuffers();
        }

        private void Update() {
            if (Track.SolvedResolution != _lastSolvedResolution) {
                _lastSolvedResolution = Track.SolvedResolution;
                if (_lastSolvedResolution > 0) {
                    RequestRebuild();
                }
            }

            if (_needsRebuild) {
                BuildGPU();
                _needsRebuild = false;
            }

            if (_buffersInitialized) {
                RenderParams rp = new(InstancedMaterial) {
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f),
                    matProps = new MaterialPropertyBlock()
                };

                Graphics.RenderMeshIndirect(
                    rp,
                    InstancedMesh,
                    _commandBuffer,
                    1
                );
            }
        }

        public void RequestRebuild() {
            _needsRebuild = true;
        }

        private void BuildGPU() {
            var nodes = Track.Nodes;

            if (!_buffersInitialized || _nodesBuffer.count < nodes.Length) {
                InitializeBuffers(nodes.Length);
            }

            _nodesBuffer.SetData(nodes);

            int kernel = TrackMeshCompute.FindKernel("CSMain");
            TrackMeshCompute.SetBuffer(kernel, "_Nodes", _nodesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_Matrices", _matricesBuffer);
            TrackMeshCompute.SetBuffer(kernel, "_Ties", _tiesBuffer);
            TrackMeshCompute.SetFloat("_Heart", HEART);

            TrackMeshCompute.GetKernelThreadGroupSizes(kernel, out uint threadGroupSize, out _, out _);
            int threadGroups = (int)math.ceil(nodes.Length / (float)threadGroupSize);

            TrackMeshCompute.Dispatch(kernel, threadGroups, 1, 1);
        }
    }
}
