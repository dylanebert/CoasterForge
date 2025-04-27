using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static CoasterForge.Constants;

namespace CoasterForge.UI {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class NodeGraphControlSystem : SystemBase {
        private Dictionary<Entity, NodeGraphNode> _nodeMap = new();
        private Dictionary<Entity, Edge> _edgeMap = new();
        private NodeGraphView _view;

        private EntityQuery _nodeQuery;
        private EntityQuery _connectionQuery;
        private EntityQuery _portQuery;

        protected override void OnCreate() {
            _nodeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<NodeAspect>()
                .WithAll<UIPosition>()
                .Build(EntityManager);
            _connectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<ConnectionAspect>()
                .Build(EntityManager);
            _portQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PointPort, Uuid>()
                .Build(EntityManager);
        }

        protected override void OnStartRunning() {
            UndoManager.Initialize(DeserializeGraph, SerializeGraph);

            var root = UIService.Instance.UIDocument.rootVisualElement;
            _view = root.Q<NodeGraphView>();
            _view.AddNodeRequested += OnAddNodeRequested;
            _view.RemoveSelectedRequested += OnRemoveSelectedRequested;
            _view.MoveNodesRequested += OnMoveNodesRequested;
            _view.ConnectionRequested += OnConnectionRequested;
        }

        protected override void OnStopRunning() {
            _view.AddNodeRequested -= OnAddNodeRequested;
            _view.RemoveSelectedRequested -= OnRemoveSelectedRequested;
            _view.MoveNodesRequested -= OnMoveNodesRequested;
            _view.ConnectionRequested -= OnConnectionRequested;
        }

        protected override void OnUpdate() {
            if (Keyboard.current.deleteKey.wasPressedThisFrame) {
                OnRemoveSelectedRequested();
            }

            UpdateNodes();
            UpdateEdges();
        }

        private void UpdateNodes() {
            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            var positions = _nodeQuery.ToComponentDataArray<UIPosition>(Allocator.Temp);
            for (int i = 0; i < nodes.Length; i++) {
                var node = nodes[i];
                var position = positions[i];
                var inputPorts = SystemAPI.GetBuffer<InputPortReference>(node);
                var outputPorts = SystemAPI.GetBuffer<OutputPortReference>(node);

                if (!_nodeMap.TryGetValue(node, out var uiNode)) {
                    uiNode = _view.AddNode(node, position, inputPorts, outputPorts);
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

        private void UpdateEdges() {
            var connections = _connectionQuery.ToEntityArray(Allocator.Temp);
            foreach (var connection in connections) {
                if (!_edgeMap.TryGetValue(connection, out var edge)) {
                    var source = FindSourcePort(connection);
                    var target = FindTargetPort(connection);
                    edge = _view.AddEdge(connection, source, target);
                    _edgeMap[connection] = edge;
                }
            }

            var toRemove = new NativeList<Entity>(Allocator.Temp);
            foreach (var connection in _edgeMap.Keys) {
                if (!connections.Contains(connection)) {
                    var edge = _edgeMap[connection];
                    _view.RemoveEdge(edge);
                    toRemove.Add(connection);
                }
            }
            foreach (var connection in toRemove) {
                _edgeMap.Remove(connection);
            }
            toRemove.Dispose();

            connections.Dispose();
        }

        private NodeGraphPort FindSourcePort(Entity connectionEntity) {
            var connection = SystemAPI.GetComponent<Connection>(connectionEntity);
            foreach (var node in _nodeMap.Values) {
                foreach (var port in node.Outputs) {
                    if (port.Entity == connection.SourcePort) {
                        return port;
                    }
                }
            }

            Debug.LogError("Failed to find source port");
            return null;
        }

        private NodeGraphPort FindTargetPort(Entity connectionEntity) {
            var connection = SystemAPI.GetComponent<Connection>(connectionEntity);
            foreach (var node in _nodeMap.Values) {
                foreach (var port in node.Inputs) {
                    if (port.Entity == connection.TargetPort) {
                        return port;
                    }
                }
            }

            Debug.LogError("Failed to find target port");
            return null;
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
            uint uuid = (uint)Guid.NewGuid().GetHashCode();
            ecb.AddComponent<Uuid>(inputPort, uuid);
            var inputPoint = PointData.Default;
            inputPoint.Velocity = 10f;
            inputPoint.Energy = 0.5f * inputPoint.Velocity * inputPoint.Velocity + G * inputPoint.GetHeartPosition(CENTER).y;
            ecb.AddComponent<PointPort>(inputPort, inputPoint);
            ecb.AppendToBuffer<InputPortReference>(node, inputPort);
            ecb.SetName(inputPort, "Input Port");

            ecb.AddBuffer<OutputPortReference>(node);
            var outputPort = ecb.CreateEntity();
            ecb.AddComponent<Dirty>(outputPort);
            uuid = (uint)Guid.NewGuid().GetHashCode();
            ecb.AddComponent<Uuid>(outputPort, uuid);
            ecb.AddComponent<PointPort>(outputPort, PointData.Default);
            ecb.AppendToBuffer<OutputPortReference>(node, outputPort);
            ecb.SetName(outputPort, "Output Port");

            ecb.SetName(node, "New Section");

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void OnRemoveSelectedRequested() {
            UndoManager.Record();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var node in _view.SelectedNodes) {
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
            }
            foreach (var edge in _view.SelectedEdges) {
                var entity = edge.Entity;
                ecb.DestroyEntity(entity);
            }
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

        private void OnConnectionRequested(NodeGraphPort source, NodeGraphPort target) {
            UndoManager.Record();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var connections = _connectionQuery.ToEntityArray(Allocator.Temp);
            var toRemove = new NativeHashSet<Entity>(connections.Length, Allocator.Temp);
            foreach (var existingEntity in connections) {
                var existing = SystemAPI.GetComponent<Connection>(existingEntity);
                if (existing.SourcePort == source.Entity
                    || existing.TargetPort == source.Entity
                    || existing.SourcePort == target.Entity
                    || existing.TargetPort == target.Entity) {
                    toRemove.Add(existingEntity);
                }
            }
            connections.Dispose();

            foreach (var existingEntity in toRemove) {
                ecb.DestroyEntity(existingEntity);
            }
            toRemove.Dispose();

            var connection = ecb.CreateEntity();
            ecb.AddComponent<Dirty>(connection);
            ecb.AddComponent(connection, new Connection {
                SourcePort = source.Entity,
                TargetPort = target.Entity,
            });
            ecb.SetName(connection, "Connection");

            ecb.SetComponent<Dirty>(source.Entity, true);

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private string SerializeGraph() {
            var nodes = new List<SerializedNode>();
            var edges = new List<SerializedEdge>();
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
                    var uuid = SystemAPI.GetComponent<Uuid>(port);
                    var point = SystemAPI.GetComponent<PointPort>(port);
                    inputPorts.Add(new SerializedPort {
                        Id = uuid,
                        Point = point,
                    });
                }

                var outputPorts = new List<SerializedPort>();
                foreach (var port in outputPortBuffer) {
                    var uuid = SystemAPI.GetComponent<Uuid>(port);
                    var point = SystemAPI.GetComponent<PointPort>(port);
                    outputPorts.Add(new SerializedPort {
                        Id = uuid,
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

            foreach (var edgeEntity in _edgeMap.Keys) {
                var connection = SystemAPI.GetComponent<Connection>(edgeEntity);
                var source = SystemAPI.GetComponent<Uuid>(connection.SourcePort);
                var target = SystemAPI.GetComponent<Uuid>(connection.TargetPort);
                edges.Add(new SerializedEdge {
                    SourceId = source,
                    TargetId = target,
                });
            }

            var serializedGraph = new SerializedGraph {
                Nodes = nodes,
                Edges = edges,
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
            _edgeMap.Clear();
            _view.ClearEdges();
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
                    ecb.AddComponent<Uuid>(portEntity, port.Id);
                    ecb.AddComponent<PointPort>(portEntity, port.Point);
                    ecb.AppendToBuffer<InputPortReference>(entity, portEntity);
                    ecb.SetName(portEntity, "Input Port");
                }

                ecb.AddBuffer<OutputPortReference>(entity);
                foreach (var port in node.OutputPorts) {
                    var portEntity = ecb.CreateEntity();
                    ecb.AddComponent<Dirty>(portEntity);
                    ecb.AddComponent<Uuid>(portEntity, port.Id);
                    ecb.AddComponent<PointPort>(portEntity, port.Point);
                    ecb.AppendToBuffer<OutputPortReference>(entity, portEntity);
                    ecb.SetName(portEntity, "Output Port");
                }

                ecb.SetName(entity, "New Section");
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            var ports = _portQuery.ToEntityArray(Allocator.Temp);
            var portMap = new NativeHashMap<uint, Entity>(ports.Length, Allocator.Temp);
            foreach (var port in ports) {
                var uuid = SystemAPI.GetComponent<Uuid>(port);
                portMap[uuid] = port;
            }
            ports.Dispose();

            ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var edge in serializedGraph.Edges) {
                var source = portMap[edge.SourceId];
                var target = portMap[edge.TargetId];
                var connection = ecb.CreateEntity();
                ecb.AddComponent<Dirty>(connection);
                ecb.AddComponent(connection, new Connection {
                    SourcePort = source,
                    TargetPort = target,
                });
                ecb.SetName(connection, "Connection");
            }
            ecb.Playback(EntityManager);
            ecb.Dispose();

            portMap.Dispose();
        }
    }
}
