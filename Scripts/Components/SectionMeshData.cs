using UnityEngine.Rendering;
using Unity.Entities;
using System;

namespace CoasterForge {
    public class SectionMeshData : IComponentData, IDisposable {
        public Entity Section;

        public AsyncGPUReadbackRequest? ComputeFence;
        public SectionMeshComputeData CurrentBuffers;
        public SectionMeshComputeData NextBuffers;

        public void Dispose() {
            CurrentBuffers?.Dispose();
            NextBuffers?.Dispose();
        }
    }
}
