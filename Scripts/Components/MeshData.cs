using UnityEngine.Rendering;
using Unity.Entities;
using System;

namespace CoasterForge {
    public class MeshData : IComponentData, IDisposable {
        public Entity Entity;

        public AsyncGPUReadbackRequest? ComputeFence;
        public MeshComputeData CurrentBuffers;
        public MeshComputeData NextBuffers;

        public void Dispose() {
            CurrentBuffers?.Dispose();
            NextBuffers?.Dispose();
        }
    }
}
