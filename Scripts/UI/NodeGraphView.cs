using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    [UxmlElement]
    public partial class NodeGraphView : VisualElement {
        private const float MIN_ZOOM = 0.1f;
        private const float MAX_ZOOM = 10f;
        private const float ZOOM_SPEED = 0.3f;

        private static readonly Color s_BackgroundColor = new(0.2f, 0.2f, 0.2f);
        private static readonly Color s_GuideColor = new(0.2f, 0.5f, 0.9f, 0.5f);
        private static readonly Color s_SelectionBoxColor = new(0.2f, 0.5f, 0.9f, 0.5f);

        private List<NodeGraphNode> _nodes = new();
        private List<Edge> _edges = new();
        private List<NodeGraphNode> _selectedNodes = new();
        private List<Edge> _selectedEdges = new();
        private VisualElement _content;
        private VisualElement _connectionsLayer;
        private VisualElement _nodesLayer;
        private NodeGraphNode _lastSnappedNode;
        private VisualElement _container;
        private VisualElement _horizontalGuide;
        private VisualElement _verticalGuide;
        private VisualElement _selectionBox;
        private Label _tip;
        private Vector2 _start;
        private Vector2 _offset;
        private Vector2 _selectionStart;
        private float _zoom = 1f;
        private bool _snapX;
        private bool _snapY;
        private bool _panning;
        private bool _boxSelecting;

        public List<NodeGraphNode> SelectedNodes => _selectedNodes;
        public List<Edge> SelectedEdges => _selectedEdges;
        public VisualElement ConnectionsLayer => _connectionsLayer;
        public VisualElement NodesLayer => _nodesLayer;
        public Vector2 Offset => _offset;
        public float Zoom => _zoom;
        public bool BoxSelecting => _boxSelecting;

        public event Action<Vector2, NodeType> AddNodeRequested;
        public event Action<NodeGraphPort, Vector2, NodeType> AddConnectedNodeRequested;
        public event Action RemoveSelectedRequested;
        public event Action<List<NodeGraphNode>, float2> MoveNodesRequested;
        public event Action<NodeGraphPort, NodeGraphPort> ConnectionRequested;
        public event Action<NodeGraphPort, object> PortChangeRequested;
        public event Action<NodeGraphPort> PromoteRequested;

        public NodeGraphView() {
            style.position = Position.Absolute;
            style.backgroundColor = s_BackgroundColor;
            style.left = 0;
            style.right = 0;
            style.top = 0;
            style.bottom = 0;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;
            style.overflow = Overflow.Visible;

            _tip = new Label("Right click to add node") {
                style = {
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 0,
                    bottom = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    color = new Color(0.6f, 0.6f, 0.6f),
                }
            };
            Add(_tip);

            _container = new VisualElement {
                style = {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 0,
                    overflow = Overflow.Hidden,
                }
            };
            Add(_container);

            _selectionBox = new VisualElement {
                style = {
                    position = Position.Absolute,
                    borderTopWidth = 1f,
                    borderRightWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftWidth = 1f,
                    borderTopColor = s_SelectionBoxColor,
                    borderRightColor = s_SelectionBoxColor,
                    borderBottomColor = s_SelectionBoxColor,
                    borderLeftColor = s_SelectionBoxColor,
                    display = DisplayStyle.None,
                }
            };
            Add(_selectionBox);

            _horizontalGuide = new VisualElement {
                style = {
                    position = Position.Absolute,
                    height = 1f,
                    backgroundColor = s_GuideColor,
                    display = DisplayStyle.None,
                }
            };
            _verticalGuide = new VisualElement {
                style = {
                    position = Position.Absolute,
                    width = 1f,
                    backgroundColor = s_GuideColor,
                    display = DisplayStyle.None,
                }
            };

            _container.Add(_horizontalGuide);
            _container.Add(_verticalGuide);

            _content = new VisualElement {
                style = {
                    position = Position.Absolute,
                    width = 0,
                    height = 0,
                    backgroundColor = Color.clear,
                },
                pickingMode = PickingMode.Ignore
            };
            _container.Add(_content);

            _connectionsLayer = new VisualElement {
                style = {
                    position = Position.Absolute,
                    width = 0,
                    height = 0,
                    top = 0,
                    left = 0,
                },
                pickingMode = PickingMode.Ignore
            };
            _nodesLayer = new VisualElement {
                style = {
                    position = Position.Absolute,
                    width = 0,
                    height = 0,
                    top = 0,
                    left = 0,
                },
                pickingMode = PickingMode.Ignore
            };
            _content.Add(_connectionsLayer);
            _content.Add(_nodesLayer);

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            RegisterCallback<WheelEvent>(OnWheel);
        }

        public NodeGraphNode AddNode(
            string name,
            Entity entity,
            NodeType type,
            Vector2 position,
            List<PortData> inputPorts,
            List<PortData> outputPorts
        ) {
            var node = new NodeGraphNode(this, name, entity, type, inputPorts, outputPorts);
            Vector2 contentPosition = (position - _offset) / _zoom;
            node.style.left = contentPosition.x;
            node.style.top = contentPosition.y;
            _nodesLayer.Add(node);
            _nodes.Add(node);

            _tip.style.display = DisplayStyle.None;

            return node;
        }

        public Edge AddEdge(Entity entity, NodeGraphPort source, NodeGraphPort target) {
            var edge = new Edge(this, entity, source, target);
            _edges.Add(edge);
            _connectionsLayer.Add(edge);

            source.SetConnected(true);
            target.SetConnected(true);

            return edge;
        }

        public void RemoveNode(NodeGraphNode node) {
            _nodes.Remove(node);
            _nodesLayer.Remove(node);
            _selectedNodes.Remove(node);

            _tip.style.display = _nodes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void RemoveEdge(Edge edge) {
            _edges.Remove(edge);
            _connectionsLayer.Remove(edge);
            _selectedEdges.Remove(edge);

            edge.Source.SetConnected(IsPortConnected(edge.Source));
            edge.Target.SetConnected(IsPortConnected(edge.Target));
        }

        private bool IsPortConnected(NodeGraphPort port) {
            foreach (var edge in _edges) {
                if (edge.Source == port || edge.Target == port) {
                    return true;
                }
            }
            return false;
        }

        public void SelectNode(NodeGraphNode node, bool addToSelection = false) {
            if (!addToSelection) {
                ClearSelection();
            }

            if (node != null && !_selectedNodes.Contains(node)) {
                _selectedNodes.Add(node);
                node.Selected = true;
                node.BringToFront();
            }
        }

        public void SelectEdge(Edge edge, bool addToSelection = false) {
            if (!addToSelection) {
                ClearSelection();
            }

            if (edge != null && !_selectedEdges.Contains(edge)) {
                _selectedEdges.Add(edge);
                edge.Selected = true;
            }
        }

        public void DeselectNode(NodeGraphNode node) {
            _selectedNodes.Remove(node);
            node.Selected = false;
        }

        public void DeselectEdge(Edge edge) {
            _selectedEdges.Remove(edge);
            edge.Selected = false;
        }

        public void ClearGraph() {
            foreach (NodeGraphNode node in _nodes) {
                _nodesLayer.Remove(node);
            }
            foreach (Edge edge in _edges) {
                _connectionsLayer.Remove(edge);
            }
            _nodes.Clear();
            _edges.Clear();
            _selectedNodes.Clear();
            _selectedEdges.Clear();

            _tip.style.display = DisplayStyle.Flex;
        }

        public void ClearSelection() {
            foreach (NodeGraphNode node in _selectedNodes) {
                node.Selected = false;
            }
            foreach (Edge edge in _selectedEdges) {
                edge.Selected = false;
            }
            _selectedNodes.Clear();
            _selectedEdges.Clear();
        }

        private void UpdateSelectionBox(Vector2 position) {
            if (!_boxSelecting) return;

            Vector2 contentStart = (_selectionStart - _offset) / _zoom;
            Vector2 contentEnd = (position - _offset) / _zoom;

            float left = Mathf.Min(contentStart.x, contentEnd.x);
            float top = Mathf.Min(contentStart.y, contentEnd.y);
            float width = Mathf.Abs(contentEnd.x - contentStart.x);
            float height = Mathf.Abs(contentEnd.y - contentStart.y);

            Vector2 screenPos = new Vector2(left, top) * _zoom + _offset;
            Vector2 screenSize = new Vector2(width, height) * _zoom;

            _selectionBox.style.left = screenPos.x;
            _selectionBox.style.top = screenPos.y;
            _selectionBox.style.width = screenSize.x;
            _selectionBox.style.height = screenSize.y;
        }

        private void SelectBox(Rect selectionRect) {
            Vector2 contentStart = (new Vector2(selectionRect.x, selectionRect.y) - _offset) / _zoom;
            Vector2 contentSize = selectionRect.size / _zoom;
            Rect contentSpaceRect = new(contentStart, contentSize);

            foreach (NodeGraphNode node in _nodes) {
                Vector2 nodePos = new(node.style.left.value.value, node.style.top.value.value);
                Vector2 nodeSize = new(node.resolvedStyle.width, node.resolvedStyle.height);
                Rect nodeRect = new(nodePos, nodeSize);

                if (contentSpaceRect.Overlaps(nodeRect)) {
                    SelectNode(node, true);
                }
            }

            foreach (Edge edge in _edges) {
                Vector2 edgePos = new(edge.style.left.value.value, edge.style.top.value.value);
                Vector2 edgeSize = new(edge.resolvedStyle.width, edge.resolvedStyle.height);
                Rect edgeRect = new(edgePos, edgeSize);

                if (contentSpaceRect.Overlaps(edgeRect)) {
                    SelectEdge(edge, true);
                }
            }
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0 && evt.target == _container) {
                _boxSelecting = true;
                _selectionStart = evt.localMousePosition;
                _selectionBox.style.display = DisplayStyle.Flex;
                UpdateSelectionBox(_selectionStart);

                if (!evt.shiftKey) {
                    ClearSelection();
                }

                this.CaptureMouse();
                evt.StopPropagation();
            }

            else if (evt.button == 1 && evt.target == _container) {
                Vector2 position = evt.localMousePosition;
                Vector2 contentPosition = (position - _offset) / _zoom;
                this.ShowContextMenu(position, menu => {
                    menu.AddItem("Add Anchor", () => {
                        AddNodeRequested?.Invoke(contentPosition, NodeType.Anchor);
                    });
                    menu.AddItem("Add Force Section", () => {
                        AddNodeRequested?.Invoke(contentPosition, NodeType.ForceSection);
                    });
                    menu.AddItem("Add Geometric Section", () => {
                        AddNodeRequested?.Invoke(contentPosition, NodeType.GeometricSection);
                    });
                });
            }

            if (evt.button == 2) {
                _panning = true;
                _start = evt.localMousePosition;
                this.CaptureMouse();
                evt.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (_boxSelecting) {
                UpdateSelectionBox(evt.localMousePosition);
                evt.StopPropagation();
                return;
            }

            if (_panning) {
                Vector2 delta = evt.localMousePosition - _start;
                _offset += delta;
                _content.transform.position = _offset;
                _start = evt.localMousePosition;
                evt.StopPropagation();
            }
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (evt.button == 0 && _boxSelecting) {
                _boxSelecting = false;
                _selectionBox.style.display = DisplayStyle.None;
                this.ReleaseMouse();

                float left = Mathf.Min(_selectionStart.x, evt.localMousePosition.x);
                float top = Mathf.Min(_selectionStart.y, evt.localMousePosition.y);
                float width = Mathf.Abs(evt.localMousePosition.x - _selectionStart.x);
                float height = Mathf.Abs(evt.localMousePosition.y - _selectionStart.y);

                Rect selectionRect = new(new Vector2(left, top), new Vector2(width, height));
                SelectBox(selectionRect);
                evt.StopPropagation();
            }

            if (evt.button == 2 && _panning) {
                _panning = false;
                this.ReleaseMouse();
                evt.StopPropagation();
            }
        }

        private void OnWheel(WheelEvent evt) {
            Vector2 mousePos = evt.mousePosition;
            Vector2 contentSpaceMousePos = (mousePos - _offset) / _zoom;

            float zoomDelta = -evt.delta.y * ZOOM_SPEED;
            float multiplier = zoomDelta > 0 ? 1.1f : 1f / 1.1f;
            _zoom = Mathf.Clamp(_zoom * Mathf.Pow(multiplier, Mathf.Abs(zoomDelta)), MIN_ZOOM, MAX_ZOOM);

            _content.transform.scale = new Vector3(_zoom, _zoom, 1f);
            _offset = mousePos - contentSpaceMousePos * _zoom;
            _content.transform.position = _offset;
            evt.StopPropagation();
        }

        public Vector2 SnapNodePosition(NodeGraphNode node, Vector2 desiredPosition) {
            const float thresholdScreen = 10f;
            float threshold = thresholdScreen / _zoom;

            NodeGraphNode snappedNode = null;
            float minDx = float.MaxValue;
            float minDy = float.MaxValue;
            float bestDx = 0f;
            float bestDy = 0f;

            // Dragged node's snap points
            float nodeLeft = desiredPosition.x;
            float nodeWidth = node.resolvedStyle.width;
            float nodeCenterX = nodeLeft + nodeWidth / 2f;
            float nodeTop = desiredPosition.y;
            float nodeHeight = node.resolvedStyle.height;
            float nodeCenterY = nodeTop + nodeHeight / 2f;

            foreach (NodeGraphNode other in _nodes) {
                if (other == node) continue;

                // Other node's snap points
                float otherLeft = other.style.left.value.value;
                float otherWidth = other.resolvedStyle.width;
                float otherCenterX = otherLeft + otherWidth / 2f;
                float otherTop = other.style.top.value.value;
                float otherHeight = other.resolvedStyle.height;
                float otherCenterY = otherTop + otherHeight / 2f;

                // Vertical
                float centerDx = otherCenterX - nodeCenterX;
                if (Mathf.Abs(centerDx) < minDx && Mathf.Abs(centerDx) < threshold) {
                    minDx = Mathf.Abs(centerDx);
                    bestDx = centerDx;
                    snappedNode = other;
                }

                // Horizontal
                float centerDy = otherCenterY - nodeCenterY;
                if (Mathf.Abs(centerDy) < minDy && Mathf.Abs(centerDy) < threshold) {
                    minDy = Mathf.Abs(centerDy);
                    bestDy = centerDy;
                    snappedNode = other;
                }
            }

            Vector2 snappedPosition = desiredPosition;
            _snapX = false;
            _snapY = false;
            _lastSnappedNode = snappedNode;

            if (minDx < threshold) {
                snappedPosition.x += bestDx;
                _snapX = true;
            }
            if (minDy < threshold) {
                snappedPosition.y += bestDy;
                _snapY = true;
            }

            UpdateGuides(node, snappedPosition);

            return snappedPosition;
        }

        private void UpdateGuides(NodeGraphNode node, Vector2 snappedPosition) {
            _horizontalGuide.style.display = DisplayStyle.None;
            _verticalGuide.style.display = DisplayStyle.None;

            if (_lastSnappedNode == null || (!_snapX && !_snapY)) return;

            float nodeWidth = node.resolvedStyle.width;
            float nodeHeight = node.resolvedStyle.height;
            float nodeCenterX = snappedPosition.x + nodeWidth / 2f;
            float nodeCenterY = snappedPosition.y + nodeHeight / 2f;

            float otherLeft = _lastSnappedNode.style.left.value.value;
            float otherWidth = _lastSnappedNode.resolvedStyle.width;
            float otherCenterX = otherLeft + otherWidth / 2f;
            float otherTop = _lastSnappedNode.style.top.value.value;
            float otherHeight = _lastSnappedNode.resolvedStyle.height;
            float otherCenterY = otherTop + otherHeight / 2f;

            float minX = Mathf.Min(snappedPosition.x, otherLeft) - 1000f;
            float maxX = Mathf.Max(snappedPosition.x + nodeWidth, otherLeft + otherWidth) + 1000f;
            float minY = Mathf.Min(snappedPosition.y, otherTop) - 1000f;
            float maxY = Mathf.Max(snappedPosition.y + nodeHeight, otherTop + otherHeight) + 1000f;

            float containerCenterX = nodeCenterX * _zoom + _offset.x;
            float containerCenterY = nodeCenterY * _zoom + _offset.y;
            float containerMinX = minX * _zoom + _offset.x;
            float containerMaxX = maxX * _zoom + _offset.x;
            float containerMinY = minY * _zoom + _offset.y;
            float containerMaxY = maxY * _zoom + _offset.y;

            if (_snapX) {
                _verticalGuide.style.left = containerCenterX;
                _verticalGuide.style.top = containerMinY;
                _verticalGuide.style.height = containerMaxY - containerMinY;
                _verticalGuide.style.display = DisplayStyle.Flex;
            }

            if (_snapY) {
                _horizontalGuide.style.top = containerCenterY;
                _horizontalGuide.style.left = containerMinX;
                _horizontalGuide.style.width = containerMaxX - containerMinX;
                _horizontalGuide.style.display = DisplayStyle.Flex;
            }
        }

        public void ClearGuides() {
            _horizontalGuide.style.display = DisplayStyle.None;
            _verticalGuide.style.display = DisplayStyle.None;
            _lastSnappedNode = null;
            _snapX = false;
            _snapY = false;
        }

        public void InvokeAddConnectedNodeRequest(NodeGraphPort source, Vector2 position, NodeType nodeType) {
            AddConnectedNodeRequested?.Invoke(source, position, nodeType);
        }

        public void InvokeRemoveSelectedRequest() {
            RemoveSelectedRequested?.Invoke();
        }

        public void InvokeMoveNodesRequest(float2 delta) {
            MoveNodesRequested?.Invoke(_selectedNodes, delta);
        }

        public void InvokeConnectionRequest(NodeGraphPort source, NodeGraphPort target) {
            ConnectionRequested?.Invoke(source, target);
        }

        public void InvokePortChangeRequest(NodeGraphPort port, object data) {
            PortChangeRequested?.Invoke(port, data);
        }

        public void InvokePromoteRequest(NodeGraphPort port) {
            PromoteRequested?.Invoke(port);
        }
    }
}
