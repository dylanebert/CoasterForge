using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class NodeGraphControlSystem : SystemBase {
        private readonly Dictionary<NodeType, string> _names = new() {
            { NodeType.ForceSection, "Force Section" },
            { NodeType.GeometricSection, "Geometric Section" },
            { NodeType.Anchor, "Anchor" },
        };

        private Dictionary<Entity, NodeGraphNode> _nodeMap = new();
        private Dictionary<Entity, Edge> _edgeMap = new();
        private List<PortData> _inputPortData = new();
        private List<PortData> _outputPortData = new();
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
                .WithAll<AnchorPort, Uuid>()
                .Build(EntityManager);
        }

        protected override void OnStartRunning() {
            UndoManager.Initialize(DeserializeGraph, SerializeGraph);

            var root = UIService.Instance.UIDocument.rootVisualElement;
            _view = root.Q<NodeGraphView>();
            _view.AddNodeRequested += OnAddNodeRequested;
            _view.AddConnectedNodeRequested += OnAddConnectedNodeRequested;
            _view.RemoveSelectedRequested += OnRemoveSelectedRequested;
            _view.MoveNodesRequested += OnMoveNodesRequested;
            _view.ConnectionRequested += OnConnectionRequested;
            _view.PortChangeRequested += OnPortChangeRequested;
            _view.PromoteRequested += OnPromoteRequested;
            _view.DurationTypeChangeRequested += OnDurationTypeChangeRequested;
        }

        protected override void OnStopRunning() {
            _view.AddNodeRequested -= OnAddNodeRequested;
            _view.AddConnectedNodeRequested -= OnAddConnectedNodeRequested;
            _view.RemoveSelectedRequested -= OnRemoveSelectedRequested;
            _view.MoveNodesRequested -= OnMoveNodesRequested;
            _view.ConnectionRequested -= OnConnectionRequested;
            _view.PortChangeRequested -= OnPortChangeRequested;
            _view.PromoteRequested -= OnPromoteRequested;
            _view.DurationTypeChangeRequested -= OnDurationTypeChangeRequested;
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
                Entity entity = nodes[i];
                NodeType nodeType = SystemAPI.GetComponent<Node>(entity);
                float2 uiPosition = positions[i];
                var inputPorts = SystemAPI.GetBuffer<InputPortReference>(entity);
                var outputPorts = SystemAPI.GetBuffer<OutputPortReference>(entity);
                _inputPortData.Clear();
                _outputPortData.Clear();
                foreach (var port in inputPorts) {
                    string name = SystemAPI.GetComponent<Name>(port);
                    PortType portType = SystemAPI.GetComponent<Port>(port);
                    object data = default;
                    if (portType == PortType.Anchor) {
                        data = SystemAPI.GetComponent<AnchorPort>(port).Value;
                    }
                    else if (portType == PortType.Duration) {
                        data = SystemAPI.GetComponent<DurationPort>(port).Value;
                    }
                    else if (portType == PortType.Position) {
                        data = SystemAPI.GetComponent<PositionPort>(port).Value;
                    }
                    else if (portType == PortType.Roll) {
                        data = SystemAPI.GetComponent<RollPort>(port).Value;
                    }
                    else if (portType == PortType.Pitch) {
                        data = SystemAPI.GetComponent<PitchPort>(port).Value;
                    }
                    else if (portType == PortType.Yaw) {
                        data = SystemAPI.GetComponent<YawPort>(port).Value;
                    }
                    else if (portType == PortType.Velocity) {
                        data = SystemAPI.GetComponent<VelocityPort>(port).Value;
                    }
                    else {
                        throw new NotImplementedException();
                    }
                    _inputPortData.Add(new PortData {
                        Name = name,
                        Entity = port,
                        Type = portType,
                        Data = data,
                        IsInput = true,
                    });
                }
                foreach (var port in outputPorts) {
                    string name = SystemAPI.GetComponent<Name>(port);
                    PortType portType = SystemAPI.GetComponent<Port>(port);
                    object data = default;
                    if (portType == PortType.Anchor) {
                        data = SystemAPI.GetComponent<AnchorPort>(port).Value;
                    }
                    else {
                        throw new NotImplementedException();
                    }
                    _outputPortData.Add(new PortData {
                        Name = name,
                        Entity = port,
                        Type = portType,
                        Data = data,
                        IsInput = false,
                    });
                }

                if (!_nodeMap.TryGetValue(entity, out var uiNode)) {
                    string name = SystemAPI.GetComponent<Name>(entity).ToString();
                    NodeType type = SystemAPI.GetComponent<Node>(entity);
                    uiNode = _view.AddNode(name, entity, type, uiPosition, _inputPortData, _outputPortData);
                    _nodeMap[entity] = uiNode;
                }

                uiNode.SetPosition(uiPosition);

                for (int j = 0; j < _inputPortData.Count; j++) {
                    uiNode.SetPortData(j, _inputPortData[j].Data);
                }

                if (SystemAPI.HasComponent<Duration>(entity)) {
                    var duration = SystemAPI.GetComponent<Duration>(entity);
                    uiNode.SetDurationType(duration.Type);
                }
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

        private Entity AddNode(float2 position, NodeType nodeType) {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var node = EntityManager.CreateEntity();

            string name = _names[nodeType];
            ecb.AddComponent<Name>(node, name);
            ecb.AddComponent<Node>(node, nodeType);
            ecb.AddComponent<UIPosition>(node, position);
            ecb.AddComponent<Dirty>(node);
            ecb.SetName(node, name);

            ecb.AddBuffer<InputPortReference>(node);
            if (nodeType == NodeType.Anchor) {
                var positionPort = ecb.CreateEntity();
                name = "Position";
                ecb.AddComponent<Name>(positionPort, name);
                ecb.AddComponent<Port>(positionPort, PortType.Position);
                ecb.AddComponent(positionPort, Uuid.Create());
                ecb.AddComponent<Dirty>(positionPort, true);
                ecb.AddComponent<PositionPort>(positionPort, float3.zero);
                ecb.AppendToBuffer<InputPortReference>(node, positionPort);
                ecb.SetName(positionPort, name);

                var rollPort = ecb.CreateEntity();
                name = "Roll";
                ecb.AddComponent<Name>(rollPort, name);
                ecb.AddComponent<Port>(rollPort, PortType.Roll);
                ecb.AddComponent(rollPort, Uuid.Create());
                ecb.AddComponent<Dirty>(rollPort, true);
                ecb.AddComponent<RollPort>(rollPort, 0f);
                ecb.AppendToBuffer<InputPortReference>(node, rollPort);
                ecb.SetName(rollPort, name);

                var pitchPort = ecb.CreateEntity();
                name = "Pitch";
                ecb.AddComponent<Name>(pitchPort, name);
                ecb.AddComponent<Port>(pitchPort, PortType.Pitch);
                ecb.AddComponent(pitchPort, Uuid.Create());
                ecb.AddComponent<Dirty>(pitchPort, true);
                ecb.AddComponent<PitchPort>(pitchPort, 0f);
                ecb.AppendToBuffer<InputPortReference>(node, pitchPort);
                ecb.SetName(pitchPort, name);

                var yawPort = ecb.CreateEntity();
                name = "Yaw";
                ecb.AddComponent<Name>(yawPort, name);
                ecb.AddComponent<Port>(yawPort, PortType.Yaw);
                ecb.AddComponent(yawPort, Uuid.Create());
                ecb.AddComponent<Dirty>(yawPort, true);
                ecb.AddComponent<YawPort>(yawPort, 0f);
                ecb.AppendToBuffer<InputPortReference>(node, yawPort);
                ecb.SetName(yawPort, name);

                var velocityPort = ecb.CreateEntity();
                name = "Velocity";
                ecb.AddComponent<Name>(velocityPort, name);
                ecb.AddComponent<Port>(velocityPort, PortType.Velocity);
                ecb.AddComponent(velocityPort, Uuid.Create());
                ecb.AddComponent<Dirty>(velocityPort, true);
                ecb.AddComponent<VelocityPort>(velocityPort, 10f);
                ecb.AppendToBuffer<InputPortReference>(node, velocityPort);
                ecb.SetName(velocityPort, name);
            }
            else if (nodeType == NodeType.ForceSection || nodeType == NodeType.GeometricSection) {
                var inputPort = ecb.CreateEntity();
                name = "Input";
                ecb.AddComponent<Name>(inputPort, name);
                ecb.AddComponent<Port>(inputPort, PortType.Anchor);
                ecb.AddComponent(inputPort, Uuid.Create());
                ecb.AddComponent<Dirty>(inputPort, true);
                ecb.AddComponent<AnchorPort>(inputPort, PointData.Create());
                ecb.AppendToBuffer<InputPortReference>(node, inputPort);
                ecb.SetName(inputPort, name);

                var durationPort = ecb.CreateEntity();
                name = "Duration";
                ecb.AddComponent<Name>(durationPort, name);
                ecb.AddComponent<Port>(durationPort, PortType.Duration);
                ecb.AddComponent(durationPort, Uuid.Create());
                ecb.AddComponent<Dirty>(durationPort, true);
                ecb.AddComponent<DurationPort>(durationPort, 1f);
                ecb.AppendToBuffer<InputPortReference>(node, durationPort);
                ecb.SetName(durationPort, name);
            }

            PointData anchor = PointData.Create();
            ecb.AddComponent(node, new Anchor {
                Value = anchor,
            });

            ecb.AddBuffer<OutputPortReference>(node);
            var outputPort = ecb.CreateEntity();
            name = "Output";
            ecb.AddComponent<Name>(outputPort, name);
            ecb.AddComponent<Port>(outputPort, PortType.Anchor);
            ecb.AddComponent(outputPort, Uuid.Create());
            ecb.AddComponent<Dirty>(outputPort);
            ecb.AddComponent<AnchorPort>(outputPort, anchor);
            ecb.AppendToBuffer<OutputPortReference>(node, outputPort);
            ecb.SetName(outputPort, name);

            if (nodeType == NodeType.ForceSection || nodeType == NodeType.GeometricSection) {
                SectionType sectionType = nodeType switch {
                    NodeType.ForceSection => SectionType.Force,
                    NodeType.GeometricSection => SectionType.Geometric,
                    _ => throw new ArgumentOutOfRangeException()
                };

                ecb.AddComponent(node, new Duration {
                    Type = DurationType.Time,
                    Value = 1f,
                });
                ecb.AddComponent(node, new FixedVelocity {
                    Value = false,
                });
                ecb.AddBuffer<Point>(node);
                ecb.AddBuffer<RollSpeedKeyframe>(node);

                if (sectionType == SectionType.Force) {
                    ecb.AddBuffer<NormalForceKeyframe>(node);
                    ecb.AddBuffer<LateralForceKeyframe>(node);
                }
                else {
                    ecb.AddBuffer<PitchSpeedKeyframe>(node);
                    ecb.AddBuffer<YawSpeedKeyframe>(node);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            return node;
        }

        private void OnAddNodeRequested(Vector2 position, NodeType nodeType) {
            UndoManager.Record();

            AddNode(position, nodeType);
        }

        private void OnAddConnectedNodeRequested(NodeGraphPort source, Vector2 position, NodeType nodeType) {
            UndoManager.Record();

            var node = AddNode(position, nodeType);
            Entity sourceEntity = Entity.Null;
            Entity targetEntity = Entity.Null;
            if (source.IsInput) {
                var outputs = SystemAPI.GetBuffer<OutputPortReference>(node);
                sourceEntity = outputs[0].Value;
                targetEntity = source.Entity;
            }
            else {
                var inputs = SystemAPI.GetBuffer<InputPortReference>(node);
                sourceEntity = source.Entity;
                targetEntity = inputs[0].Value;
            }
            AddConnection(sourceEntity, targetEntity);
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

        private void AddConnection(Entity source, Entity target) {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var connections = _connectionQuery.ToEntityArray(Allocator.Temp);
            var toRemove = new NativeHashSet<Entity>(connections.Length, Allocator.Temp);
            foreach (var existingEntity in connections) {
                var existing = SystemAPI.GetComponent<Connection>(existingEntity);
                if (existing.SourcePort == source
                    || existing.TargetPort == source
                    || existing.SourcePort == target
                    || existing.TargetPort == target) {
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
                SourcePort = source,
                TargetPort = target,
            });
            ecb.SetName(connection, "Connection");

            ecb.SetComponent<Dirty>(source, true);

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void OnConnectionRequested(NodeGraphPort source, NodeGraphPort target) {
            UndoManager.Record();

            AddConnection(source.Entity, target.Entity);
        }

        private void OnPortChangeRequested(NodeGraphPort port, object data) {
            if (!port.IsInput) {
                throw new NotImplementedException("Only input ports can be changed");
            }

            var entity = port.Entity;

            switch (port.Type) {
                case PortType.Duration:
                    ref var duration = ref SystemAPI.GetComponentRW<DurationPort>(entity).ValueRW;
                    duration.Value = (float)data;
                    break;
                case PortType.Position:
                    ref var position = ref SystemAPI.GetComponentRW<PositionPort>(entity).ValueRW;
                    position = (float3)data;
                    break;
                case PortType.Roll:
                    ref var roll = ref SystemAPI.GetComponentRW<RollPort>(entity).ValueRW;
                    roll.Value = (float)data;
                    break;
                case PortType.Pitch:
                    ref var pitch = ref SystemAPI.GetComponentRW<PitchPort>(entity).ValueRW;
                    pitch.Value = (float)data;
                    break;
                case PortType.Yaw:
                    ref var yaw = ref SystemAPI.GetComponentRW<YawPort>(entity).ValueRW;
                    yaw.Value = (float)data;
                    break;
                case PortType.Velocity:
                    ref var velocity = ref SystemAPI.GetComponentRW<VelocityPort>(entity).ValueRW;
                    velocity.Value = (float)data;
                    break;
                default:
                    throw new NotImplementedException();
            }

            ref var dirty = ref SystemAPI.GetComponentRW<Dirty>(entity).ValueRW;
            dirty = true;
        }

        private void OnPromoteRequested(NodeGraphPort port) {
            if (!port.IsInput || port.Type != PortType.Anchor) {
                throw new NotImplementedException("Only input anchor ports can be promoted");
            }

            UndoManager.Record();

            var targetNode = port.Node.Entity;
            float2 uiPosition = SystemAPI.GetComponent<UIPosition>(targetNode);

            float2 sourcePosition = uiPosition + new float2(-256f, 0f);
            var node = AddNode(sourcePosition, NodeType.Anchor);

            var sourcePort = SystemAPI.GetBuffer<OutputPortReference>(node)[0];
            var targetPort = port.Entity;
            AddConnection(sourcePort, targetPort);

            PointData anchor = SystemAPI.GetComponent<AnchorPort>(targetPort);
            ref var nodeAnchor = ref SystemAPI.GetComponentRW<Anchor>(node).ValueRW;
            nodeAnchor.Value = anchor;

            var anchorInputBuffer = SystemAPI.GetBuffer<InputPortReference>(node);

            ref var positionPort = ref SystemAPI.GetComponentRW<PositionPort>(anchorInputBuffer[0].Value).ValueRW;
            positionPort.Value = anchor.Position;

            ref var rollPort = ref SystemAPI.GetComponentRW<RollPort>(anchorInputBuffer[1].Value).ValueRW;
            rollPort.Value = anchor.Roll;

            ref var pitchPort = ref SystemAPI.GetComponentRW<PitchPort>(anchorInputBuffer[2].Value).ValueRW;
            pitchPort.Value = anchor.GetPitch();

            ref var yawPort = ref SystemAPI.GetComponentRW<YawPort>(anchorInputBuffer[3].Value).ValueRW;
            yawPort.Value = anchor.GetYaw();

            ref var velocityPort = ref SystemAPI.GetComponentRW<VelocityPort>(anchorInputBuffer[4].Value).ValueRW;
            velocityPort.Value = anchor.Velocity;
        }

        private void OnDurationTypeChangeRequested(NodeGraphNode node, DurationType durationType) {
            UndoManager.Record();

            ref var duration = ref SystemAPI.GetComponentRW<Duration>(node.Entity).ValueRW;
            duration.Type = durationType;

            ref var dirty = ref SystemAPI.GetComponentRW<Dirty>(node.Entity).ValueRW;
            dirty = true;
        }

        private string SerializeGraph() {
            var nodes = new List<SerializedNode>();
            var edges = new List<SerializedEdge>();
            foreach (var nodeEntity in _nodeMap.Keys) {
                var name = SystemAPI.GetComponent<Name>(nodeEntity);
                NodeType type = SystemAPI.GetComponent<Node>(nodeEntity);
                float2 uiPosition = SystemAPI.GetComponent<UIPosition>(nodeEntity);

                var inputPortBuffer = SystemAPI.GetBuffer<InputPortReference>(nodeEntity);
                var outputPortBuffer = SystemAPI.GetBuffer<OutputPortReference>(nodeEntity);

                var inputPorts = new List<SerializedPort>();
                foreach (var port in inputPortBuffer) {
                    uint uuid = SystemAPI.GetComponent<Uuid>(port);
                    string portName = SystemAPI.GetComponent<Name>(port);
                    PortType portType = SystemAPI.GetComponent<Port>(port);
                    SerializedPort serializedPort = new() {
                        Id = uuid,
                        Name = portName,
                        Type = portType,
                    };
                    if (portType == PortType.Anchor) {
                        serializedPort.PointData = SystemAPI.GetComponent<AnchorPort>(port).Value;
                    }
                    else if (portType == PortType.Duration) {
                        serializedPort.FloatData = SystemAPI.GetComponent<DurationPort>(port).Value;
                    }
                    else if (portType == PortType.Position) {
                        serializedPort.Float3Data = SystemAPI.GetComponent<PositionPort>(port).Value;
                    }
                    else if (portType == PortType.Roll) {
                        serializedPort.FloatData = SystemAPI.GetComponent<RollPort>(port).Value;
                    }
                    else if (portType == PortType.Pitch) {
                        serializedPort.FloatData = SystemAPI.GetComponent<PitchPort>(port).Value;
                    }
                    else if (portType == PortType.Yaw) {
                        serializedPort.FloatData = SystemAPI.GetComponent<YawPort>(port).Value;
                    }
                    else if (portType == PortType.Velocity) {
                        serializedPort.FloatData = SystemAPI.GetComponent<VelocityPort>(port).Value;
                    }
                    else {
                        throw new NotImplementedException();
                    }
                    inputPorts.Add(serializedPort);
                }

                var outputPorts = new List<SerializedPort>();
                foreach (var port in outputPortBuffer) {
                    uint uuid = SystemAPI.GetComponent<Uuid>(port);
                    string portName = SystemAPI.GetComponent<Name>(port);
                    PortType portType = SystemAPI.GetComponent<Port>(port);
                    SerializedPort serializedPort = new() {
                        Id = uuid,
                        Name = portName,
                        Type = portType,
                    };
                    if (portType == PortType.Anchor) {
                        serializedPort.PointData = SystemAPI.GetComponent<AnchorPort>(port).Value;
                    }
                    else if (portType == PortType.Duration) {
                        serializedPort.FloatData = SystemAPI.GetComponent<DurationPort>(port).Value;
                    }
                    else if (portType == PortType.Position) {
                        serializedPort.Float3Data = SystemAPI.GetComponent<PositionPort>(port).Value;
                    }
                    else if (portType == PortType.Roll) {
                        serializedPort.FloatData = SystemAPI.GetComponent<RollPort>(port).Value;
                    }
                    else if (portType == PortType.Pitch) {
                        serializedPort.FloatData = SystemAPI.GetComponent<PitchPort>(port).Value;
                    }
                    else if (portType == PortType.Yaw) {
                        serializedPort.FloatData = SystemAPI.GetComponent<YawPort>(port).Value;
                    }
                    else if (portType == PortType.Velocity) {
                        serializedPort.FloatData = SystemAPI.GetComponent<VelocityPort>(port).Value;
                    }
                    else {
                        throw new NotImplementedException();
                    }
                    outputPorts.Add(serializedPort);
                }

                Anchor anchor = SystemAPI.GetComponent<Anchor>(nodeEntity);

                Duration duration = type switch {
                    NodeType.ForceSection or NodeType.GeometricSection => SystemAPI.GetComponent<Duration>(nodeEntity),
                    _ => default,
                };
                FixedVelocity fixedVelocity = type switch {
                    NodeType.ForceSection or NodeType.GeometricSection => SystemAPI.GetComponent<FixedVelocity>(nodeEntity),
                    _ => default,
                };
                DynamicBuffer<RollSpeedKeyframe>? rollSpeedKeyframeBuffer = type switch {
                    NodeType.ForceSection or NodeType.GeometricSection => SystemAPI.GetBuffer<RollSpeedKeyframe>(nodeEntity),
                    _ => null,
                };
                DynamicBuffer<NormalForceKeyframe>? normalForceKeyframeBuffer = type switch {
                    NodeType.ForceSection => SystemAPI.GetBuffer<NormalForceKeyframe>(nodeEntity),
                    _ => null,
                };
                DynamicBuffer<LateralForceKeyframe>? lateralForceKeyframeBuffer = type switch {
                    NodeType.ForceSection => SystemAPI.GetBuffer<LateralForceKeyframe>(nodeEntity),
                    _ => null,
                };
                DynamicBuffer<PitchSpeedKeyframe>? pitchSpeedKeyframeBuffer = type switch {
                    NodeType.GeometricSection => SystemAPI.GetBuffer<PitchSpeedKeyframe>(nodeEntity),
                    _ => null,
                };
                DynamicBuffer<YawSpeedKeyframe>? yawSpeedKeyframeBuffer = type switch {
                    NodeType.GeometricSection => SystemAPI.GetBuffer<YawSpeedKeyframe>(nodeEntity),
                    _ => null,
                };

                List<Keyframe> rollSpeedKeyframes = new();
                if (rollSpeedKeyframeBuffer != null) {
                    foreach (var keyframe in rollSpeedKeyframeBuffer) {
                        rollSpeedKeyframes.Add(keyframe);
                    }
                }

                List<Keyframe> normalForceKeyframes = new();
                if (normalForceKeyframeBuffer != null) {
                    foreach (var keyframe in normalForceKeyframeBuffer) {
                        normalForceKeyframes.Add(keyframe);
                    }
                }

                List<Keyframe> lateralForceKeyframes = new();
                if (lateralForceKeyframeBuffer != null) {
                    foreach (var keyframe in lateralForceKeyframeBuffer) {
                        lateralForceKeyframes.Add(keyframe);
                    }
                }

                List<Keyframe> pitchSpeedKeyframes = new();
                if (pitchSpeedKeyframeBuffer != null) {
                    foreach (var keyframe in pitchSpeedKeyframeBuffer) {
                        pitchSpeedKeyframes.Add(keyframe);
                    }
                }

                List<Keyframe> yawSpeedKeyframes = new();
                if (yawSpeedKeyframeBuffer != null) {
                    foreach (var keyframe in yawSpeedKeyframeBuffer) {
                        yawSpeedKeyframes.Add(keyframe);
                    }
                }

                nodes.Add(new SerializedNode {
                    Name = name,
                    Type = type,
                    Position = uiPosition,
                    InputPorts = inputPorts,
                    OutputPorts = outputPorts,
                    Anchor = anchor,
                    Duration = duration,
                    FixedVelocity = fixedVelocity,
                    RollSpeedKeyframes = rollSpeedKeyframes,
                    NormalForceKeyframes = normalForceKeyframes,
                    LateralForceKeyframes = lateralForceKeyframes,
                    PitchSpeedKeyframes = pitchSpeedKeyframes,
                    YawSpeedKeyframes = yawSpeedKeyframes,
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
            foreach (var (_, entity) in SystemAPI.Query<AnchorPort>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
            }
            foreach (var (_, entity) in SystemAPI.Query<Connection>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
            }
            _nodeMap.Clear();
            _edgeMap.Clear();
            _view.ClearGraph();
            ecb.Playback(EntityManager);
            ecb.Dispose();

            var serializedGraph = JsonUtility.FromJson<SerializedGraph>(json);

            ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var node in serializedGraph.Nodes) {
                var entity = ecb.CreateEntity();

                ecb.AddComponent(entity, node.Name);
                ecb.AddComponent<Node>(entity, node.Type);
                ecb.AddComponent(entity, node.Position);
                ecb.AddComponent<Dirty>(entity);

                ecb.AddBuffer<InputPortReference>(entity);
                foreach (var port in node.InputPorts) {
                    var portEntity = ecb.CreateEntity();
                    ecb.AddComponent<Port>(portEntity, port.Type);
                    ecb.AddComponent<Name>(portEntity, port.Name);
                    ecb.AddComponent<Uuid>(portEntity, port.Id);
                    ecb.AddComponent<Dirty>(portEntity, true);
                    if (port.Type == PortType.Anchor) {
                        ecb.AddComponent<AnchorPort>(portEntity, port.PointData);
                    }
                    else if (port.Type == PortType.Duration) {
                        ecb.AddComponent<DurationPort>(portEntity, port.FloatData);
                    }
                    else if (port.Type == PortType.Position) {
                        ecb.AddComponent<PositionPort>(portEntity, port.Float3Data);
                    }
                    else if (port.Type == PortType.Roll) {
                        ecb.AddComponent<RollPort>(portEntity, port.FloatData);
                    }
                    else if (port.Type == PortType.Pitch) {
                        ecb.AddComponent<PitchPort>(portEntity, port.FloatData);
                    }
                    else if (port.Type == PortType.Yaw) {
                        ecb.AddComponent<YawPort>(portEntity, port.FloatData);
                    }
                    else if (port.Type == PortType.Velocity) {
                        ecb.AddComponent<VelocityPort>(portEntity, port.FloatData);
                    }
                    ecb.AppendToBuffer<InputPortReference>(entity, portEntity);
                    ecb.SetName(portEntity, port.Name);
                }

                ecb.AddComponent<Anchor>(entity, node.Anchor);

                ecb.AddBuffer<OutputPortReference>(entity);
                foreach (var port in node.OutputPorts) {
                    var portEntity = ecb.CreateEntity();
                    ecb.AddComponent<Port>(portEntity, port.Type);
                    ecb.AddComponent<Name>(portEntity, port.Name);
                    ecb.AddComponent<Uuid>(portEntity, port.Id);
                    ecb.AddComponent<Dirty>(portEntity);
                    if (port.Type == PortType.Anchor) {
                        ecb.AddComponent<AnchorPort>(portEntity, port.PointData);
                    }
                    else if (port.Type == PortType.Duration) {
                        ecb.AddComponent<DurationPort>(portEntity, port.FloatData);
                    }
                    else if (port.Type == PortType.Position) {
                        ecb.AddComponent<PositionPort>(portEntity, port.Float3Data);
                    }
                    else if (port.Type == PortType.Roll) {
                        ecb.AddComponent<RollPort>(portEntity, port.FloatData);
                    }
                    else if (port.Type == PortType.Pitch) {
                        ecb.AddComponent<PitchPort>(portEntity, port.FloatData);
                    }
                    else if (port.Type == PortType.Yaw) {
                        ecb.AddComponent<YawPort>(portEntity, port.FloatData);
                    }
                    else if (port.Type == PortType.Velocity) {
                        ecb.AddComponent<VelocityPort>(portEntity, port.FloatData);
                    }
                    ecb.AppendToBuffer<OutputPortReference>(entity, portEntity);
                    ecb.SetName(portEntity, port.Name);
                }

                if (node.Type == NodeType.ForceSection || node.Type == NodeType.GeometricSection) {
                    ecb.AddComponent(entity, node.Duration);
                    ecb.AddComponent(entity, node.FixedVelocity);
                    ecb.AddBuffer<Point>(entity);

                    ecb.AddBuffer<RollSpeedKeyframe>(entity);
                    foreach (var keyframe in node.RollSpeedKeyframes) {
                        ecb.AppendToBuffer(entity, new RollSpeedKeyframe { Value = keyframe });
                    }

                    if (node.Type == NodeType.ForceSection) {
                        ecb.AddBuffer<NormalForceKeyframe>(entity);
                        foreach (var keyframe in node.NormalForceKeyframes) {
                            ecb.AppendToBuffer(entity, new NormalForceKeyframe { Value = keyframe });
                        }

                        ecb.AddBuffer<LateralForceKeyframe>(entity);
                        foreach (var keyframe in node.LateralForceKeyframes) {
                            ecb.AppendToBuffer(entity, new LateralForceKeyframe { Value = keyframe });
                        }
                    }
                    else if (node.Type == NodeType.GeometricSection) {
                        ecb.AddBuffer<PitchSpeedKeyframe>(entity);
                        foreach (var keyframe in node.PitchSpeedKeyframes) {
                            ecb.AppendToBuffer(entity, new PitchSpeedKeyframe { Value = keyframe });
                        }

                        ecb.AddBuffer<YawSpeedKeyframe>(entity);
                        foreach (var keyframe in node.YawSpeedKeyframes) {
                            ecb.AppendToBuffer(entity, new YawSpeedKeyframe { Value = keyframe });
                        }
                    }
                }

                ecb.SetName(entity, node.Name);
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
