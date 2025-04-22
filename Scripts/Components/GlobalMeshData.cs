using UnityEngine;
using Unity.Entities;

namespace CoasterForge {
    public class GlobalMeshData : IComponentData {
        public ComputeShader Compute;
        public Material DuplicationMat;
        public Material ExtrusionMat;
        public Mesh DuplicationMesh;
    }
}
