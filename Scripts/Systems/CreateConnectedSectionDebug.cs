#if UNITY_EDITOR
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using static CoasterForge.Constants;

namespace CoasterForge {
    public partial class CreateConnectedSectionDebug : SystemBase {
        protected override void OnUpdate() {
            if (!Keyboard.current.f1Key.wasPressedThisFrame) return;

            var sections = EntityManager.CreateEntityQuery(typeof(Section)).ToEntityArray(Allocator.Temp);
            if (sections.Length != 1) {
                Debug.LogWarning("CreateConnectedSectionDebug: Expected 1 section, found " + sections.Length);
                return;
            }

            var prev = sections[0];
            var prevOutputPort = SystemAPI.GetBuffer<OutputPortReference>(prev)[0];

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var curr = ecb.CreateEntity();
            ecb.AddComponent(curr, new Section {
                DurationType = DurationType.Time,
                Duration = 1f,
                FixedVelocity = false,
            });
            ecb.AddComponent<Dirty>(curr);
            ecb.AddBuffer<Point>(curr);
            ecb.AddBuffer<RollSpeedKeyframe>(curr);
            ecb.AddBuffer<NormalForceKeyframe>(curr);
            ecb.AddBuffer<LateralForceKeyframe>(curr);
            ecb.AddComponent<Node>(curr);

            ecb.AddBuffer<InputPortReference>(curr);
            var inputPort = ecb.CreateEntity();
            var inputPoint = PointData.Default;
            inputPoint.Velocity = 10f;
            inputPoint.Energy = 0.5f * inputPoint.Velocity * inputPoint.Velocity + G * inputPoint.GetHeartPosition(CENTER).y;
            ecb.AddComponent<PointPort>(inputPort, inputPoint);
            ecb.AddComponent<Dirty>(inputPort);
            ecb.AppendToBuffer<InputPortReference>(curr, inputPort);
            ecb.SetName(inputPort, "Input Port");

            ecb.AddBuffer<OutputPortReference>(curr);
            var outputPort = ecb.CreateEntity();
            ecb.AddComponent<PointPort>(outputPort, PointData.Default);
            ecb.AddComponent<Dirty>(outputPort);
            ecb.AppendToBuffer<OutputPortReference>(curr, outputPort);
            ecb.SetName(outputPort, "Output Port");

            ecb.SetName(curr, "New Section");

            var connection = ecb.CreateEntity();
            ecb.AddComponent(connection, new Connection {
                SourcePort = prevOutputPort,
                TargetPort = inputPort,
            });
            ecb.AddComponent<Dirty>(connection);
            ecb.SetName(connection, "Connection");

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
#endif
