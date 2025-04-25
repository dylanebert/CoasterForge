using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static CoasterForge.Constants;

namespace CoasterForge.UI {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class NodeGraphControlSystem : SystemBase {
        private Dictionary<Entity, NodeGraphNode> _nodeMap = new();
        private NodeGraphView _view;

        private EntityQuery _nodeQuery;

        protected override void OnCreate() {
            _nodeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<NodeAspect>()
                .WithAll<UIPosition>()
                .Build(EntityManager);
        }

        protected override void OnStartRunning() {
            UndoManager.Initialize(DeserializeGraph, SerializeGraph);

            var root = UIService.Instance.UIDocument.rootVisualElement;
            _view = root.Q<NodeGraphView>();
            _view.AddNodeRequested += OnAddNodeRequested;
            _view.RemoveNodeRequested += OnRemoveNodeRequested;
            _view.MoveNodesRequested += OnMoveNodesRequested;
        }

        protected override void OnStopRunning() {
            _view.AddNodeRequested -= OnAddNodeRequested;
            _view.RemoveNodeRequested -= OnRemoveNodeRequested;
            _view.MoveNodesRequested -= OnMoveNodesRequested;
        }

        protected override void OnUpdate() {
            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            var positions = _nodeQuery.ToComponentDataArray<UIPosition>(Allocator.Temp);
            for (int i = 0; i < nodes.Length; i++) {
                var node = nodes[i];
                var position = positions[i];

                if (!_nodeMap.TryGetValue(node, out var uiNode)) {
                    uiNode = _view.AddNode(node, position);
                    _nodeMap[node] = uiNode;
                }

                uiNode.SetPosition(position);
            }

            var toRemove = new NativeList<Entity>(Allocator.Temp);
            foreach (var node in _nodeMap.Keys) {
                if (!nodes.Contains(node)) {
                    var uiNode = _nodeMap[node];
                    _view.RemoveNode(uiNode);
                    toRemove.Add(node);
                }
            }
            foreach (var node in toRemove) {
                _nodeMap.Remove(node);
            }
            toRemove.Dispose();

            nodes.Dispose();
            positions.Dispose();
        }

        private void OnAddNodeRequested(Vector2 position) {
            UndoManager.Record();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var node = ecb.CreateEntity();

            ecb.AddComponent<Node>(node);
            ecb.AddComponent<Dirty>(node);
            ecb.AddBuffer<Point>(node);

            ecb.AddComponent(node, new Section {
                DurationType = DurationType.Time,
                Duration = 1f,
                FixedVelocity = false,
            });
            ecb.AddComponent<UIPosition>(node, position);

            ecb.AddBuffer<RollSpeedKeyframe>(node);
            ecb.AddBuffer<NormalForceKeyframe>(node);
            ecb.AddBuffer<LateralForceKeyframe>(node);

            ecb.AddBuffer<InputPortReference>(node);
            var inputPort = ecb.CreateEntity();
            ecb.AddComponent<Dirty>(inputPort, true);
            var inputPoint = PointData.Default;
            inputPoint.Velocity = 10f;
            inputPoint.Energy = 0.5f * inputPoint.Velocity * inputPoint.Velocity + G * inputPoint.GetHeartPosition(CENTER).y;
            ecb.AddComponent<PointPort>(inputPort, inputPoint);
            ecb.AppendToBuffer<InputPortReference>(node, inputPort);
            ecb.SetName(inputPort, "Input Port");

            ecb.AddBuffer<OutputPortReference>(node);
            var outputPort = ecb.CreateEntity();
            ecb.AddComponent<Dirty>(outputPort);
            ecb.AddComponent<PointPort>(outputPort, PointData.Default);
            ecb.AppendToBuffer<OutputPortReference>(node, outputPort);
            ecb.SetName(outputPort, "Output Port");

            ecb.SetName(node, "New Section");

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void OnRemoveNodeRequested(NodeGraphNode node) {
            UndoManager.Record();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entity = node.Entity;
            var inputPortBuffer = SystemAPI.GetBuffer<InputPortReference>(entity);
            var outputPortBuffer = SystemAPI.GetBuffer<OutputPortReference>(entity);
            foreach (var port in inputPortBuffer) {
                ecb.DestroyEntity(port);
            }
            foreach (var port in outputPortBuffer) {
                ecb.DestroyEntity(port);
            }
            ecb.DestroyEntity(entity);
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void OnMoveNodesRequested(List<NodeGraphNode> nodes, float2 delta) {
            foreach (var node in nodes) {
                var entity = node.Entity;
                ref var position = ref SystemAPI.GetComponentRW<UIPosition>(entity).ValueRW;
                position.Value += delta;
            }
        }

        private string SerializeGraph() {
            var nodes = new List<SerializedNode>();
            foreach (var nodeEntity in _nodeMap.Keys) {
                var section = SystemAPI.GetComponent<Section>(nodeEntity);
                var position = SystemAPI.GetComponent<UIPosition>(nodeEntity);
                var rollSpeedKeyframeBuffer = SystemAPI.GetBuffer<RollSpeedKeyframe>(nodeEntity);
                var normalForceKeyframeBuffer = SystemAPI.GetBuffer<NormalForceKeyframe>(nodeEntity);
                var lateralForceKeyframeBuffer = SystemAPI.GetBuffer<LateralForceKeyframe>(nodeEntity);
                var inputPortBuffer = SystemAPI.GetBuffer<InputPortReference>(nodeEntity);
                var outputPortBuffer = SystemAPI.GetBuffer<OutputPortReference>(nodeEntity);

                var rollSpeedKeyframes = new List<Keyframe>();
                foreach (var keyframe in rollSpeedKeyframeBuffer) {
                    rollSpeedKeyframes.Add(keyframe);
                }

                var normalForceKeyframes = new List<Keyframe>();
                foreach (var keyframe in normalForceKeyframeBuffer) {
                    normalForceKeyframes.Add(keyframe);
                }

                var lateralForceKeyframes = new List<Keyframe>();
                foreach (var keyframe in lateralForceKeyframeBuffer) {
                    lateralForceKeyframes.Add(keyframe);
                }

                var inputPorts = new List<SerializedPort>();
                foreach (var port in inputPortBuffer) {
                    var point = SystemAPI.GetComponent<PointPort>(port);
                    inputPorts.Add(new SerializedPort {
                        Point = point
                    });
                }

                var outputPorts = new List<SerializedPort>();
                foreach (var port in outputPortBuffer) {
                    var point = SystemAPI.GetComponent<PointPort>(port);
                    outputPorts.Add(new SerializedPort {
                        Point = point,
                    });
                }

                nodes.Add(new SerializedNode {
                    Section = section,
                    Position = position,
                    RollSpeedKeyframes = rollSpeedKeyframes,
                    NormalForceKeyframes = normalForceKeyframes,
                    LateralForceKeyframes = lateralForceKeyframes,
                    InputPorts = inputPorts,
                    OutputPorts = outputPorts,
                });
            }

            var serializedGraph = new SerializedGraph {
                Nodes = nodes,
            };

            return JsonUtility.ToJson(serializedGraph);
        }

        private void DeserializeGraph(string json) {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<Node>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
            }
            foreach (var (_, entity) in SystemAPI.Query<PointPort>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
            }
            foreach (var (_, entity) in SystemAPI.Query<Connection>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
            }
            _nodeMap.Clear();
            _view.ClearNodes();
            ecb.Playback(EntityManager);
            ecb.Dispose();

            var serializedGraph = JsonUtility.FromJson<SerializedGraph>(json);

            ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var node in serializedGraph.Nodes) {
                var entity = ecb.CreateEntity();

                ecb.AddComponent<Node>(entity);
                ecb.AddComponent<Dirty>(entity);
                ecb.AddBuffer<Point>(entity);

                ecb.AddComponent(entity, node.Section);
                ecb.AddComponent(entity, node.Position);

                ecb.AddBuffer<RollSpeedKeyframe>(entity);
                foreach (var keyframe in node.RollSpeedKeyframes) {
                    ecb.AppendToBuffer(entity, new RollSpeedKeyframe { Value = keyframe });
                }

                ecb.AddBuffer<NormalForceKeyframe>(entity);
                foreach (var keyframe in node.NormalForceKeyframes) {
                    ecb.AppendToBuffer(entity, new NormalForceKeyframe { Value = keyframe });
                }

                ecb.AddBuffer<LateralForceKeyframe>(entity);
                foreach (var keyframe in node.LateralForceKeyframes) {
                    ecb.AppendToBuffer(entity, new LateralForceKeyframe { Value = keyframe });
                }

                ecb.AddBuffer<InputPortReference>(entity);
                foreach (var port in node.InputPorts) {
                    var portEntity = ecb.CreateEntity();
                    ecb.AddComponent<Dirty>(portEntity, true);
                    ecb.AddComponent<PointPort>(portEntity, port.Point);
                    ecb.AppendToBuffer<InputPortReference>(entity, portEntity);
                    ecb.SetName(portEntity, "Input Port");
                }

                ecb.AddBuffer<OutputPortReference>(entity);
                foreach (var port in node.OutputPorts) {
                    var portEntity = ecb.CreateEntity();
                    ecb.AddComponent<Dirty>(portEntity);
                    ecb.AddComponent<PointPort>(portEntity, port.Point);
                    ecb.AppendToBuffer<OutputPortReference>(entity, portEntity);
                    ecb.SetName(portEntity, "Output Port");
                }

                ecb.SetName(entity, "New Section");
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
