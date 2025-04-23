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
            foreach (var pointBuffer in SystemAPI.Query<DynamicBuffer<Point>>()) {
                for (int i = 0; i < pointBuffer.Length; i++) {
                    if (i % 10 != 0) continue;
                    PointData p = pointBuffer[i];
                    float3 position = p.Position;
                    float3 direction = p.Direction;
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(position, 0.1f);
                    Gizmos.DrawLine(position, position + direction);

                    float3 heartPosition = p.GetHeartPosition(HEART);
                    float3 heartDirection = p.GetHeartDirection(HEART);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(heartPosition, 0.1f);
                    Gizmos.DrawLine(heartPosition, heartPosition + heartDirection);

                    float3 heartLateral = p.GetHeartLateral(HEART);
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(heartPosition, heartPosition + heartLateral);
                }
            }
        }

        protected override void OnUpdate() { }
    }
}
#endif
