#if UNITY_EDITOR
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static CoasterForge.Constants;

namespace CoasterForge {
    public partial class DrawSectionGizmosSystem : SystemBase {
        public static DrawSectionGizmosSystem Instance { get; private set; }

        protected override void OnCreate() {
            Instance = this;
        }

        protected override void OnDestroy() {
            Instance = null;
        }

        public void Draw() {
            foreach (var nodeBuffer in SystemAPI.Query<DynamicBuffer<Node>>()) {
                for (int i = 0; i < nodeBuffer.Length; i++) {
                    if (i % 10 != 0) continue;
                    var node = nodeBuffer[i];
                    float3 position = node.Position;
                    float3 direction = node.Direction;
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(position, 0.1f);
                    Gizmos.DrawLine(position, position + direction);

                    float3 heartPosition = node.GetHeartPosition(HEART);
                    float3 heartDirection = node.GetHeartDirection(HEART);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(heartPosition, 0.1f);
                    Gizmos.DrawLine(heartPosition, heartPosition + heartDirection);

                    float3 heartLateral = node.GetHeartLateral(HEART);
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(heartPosition, heartPosition + heartLateral);
                }
            }
        }

        protected override void OnUpdate() { }
    }
}
#endif
