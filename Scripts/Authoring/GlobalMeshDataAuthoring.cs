using UnityEngine;
using Unity.Entities;

namespace CoasterForge {
    public class GlobalMeshDataAuthoring : MonoBehaviour {
        public ComputeShader TrackMeshCompute;
        public Material DuplicationMaterial;
        public Material ExtrusionMaterial;
        public Mesh DuplicationMesh;

        private class Baker : Baker<GlobalMeshDataAuthoring> {
            public override void Bake(GlobalMeshDataAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponentObject(entity, new GlobalMeshData {
                    Compute = authoring.TrackMeshCompute,
                    DuplicationMat = authoring.DuplicationMaterial,
                    ExtrusionMat = authoring.ExtrusionMaterial,
                    DuplicationMesh = authoring.DuplicationMesh,
                });
            }
        }
    }
}
