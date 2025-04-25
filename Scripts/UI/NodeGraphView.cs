using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge {
    [UxmlElement]
    public partial class NodeGraphView : VisualElement {
        private const float MIN_ZOOM = 0.1f;
        private const float MAX_ZOOM = 10f;
        private const float ZOOM_SPEED = 0.1f;

        private static readonly Color s_BackgroundColor = new(0.2f, 0.2f, 0.2f);

        private Label _tip;
        private VisualElement _container;
        private NodeGraphContent _content;
        private Vector2 _start;
        private Vector2 _offset;
        private float _zoom = 1f;
        private bool _panning;

        private NodeGraphNode _selectedNode;
        private List<NodeGraphNode> _nodes = new();

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

            _tip = new Label("Right click to add a node") {
                style = {
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 0,
                    bottom = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 11,
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

            _content = new NodeGraphContent();
            _container.Add(_content);

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<WheelEvent>(OnWheel);
        }

        private void ShowContextMenu(Vector2 position) {
            var menu = new ContextMenu();
            menu.style.left = position.x;
            menu.style.top = position.y;
            menu.AddItem("Add Node", () => AddNodeAtPosition(position));
            menu.AddItem("Add Node", () => AddNodeAtPosition(position));
            Add(menu);

            void OnMouseDown(MouseDownEvent evt) {
                if (menu.parent != null) {
                    Remove(menu);
                }
                UnregisterCallback((EventCallback<MouseDownEvent>)OnMouseDown);
            }
            RegisterCallback((EventCallback<MouseDownEvent>)OnMouseDown);
        }

        private void AddNodeAtPosition(Vector2 position) {
            Vector2 contentPosition = (position - _offset) / _zoom;

            var node = new NodeGraphNode();
            node.style.left = contentPosition.x;
            node.style.top = contentPosition.y;

            node.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button == 0) {
                    SelectNode(node);
                }
            });

            _content.Add(node);
            _nodes.Add(node);

            _tip.style.display = DisplayStyle.None;
        }

        private void SelectNode(NodeGraphNode node) {
            if (_selectedNode != null) {
                _selectedNode.Selected = false;
            }
            _selectedNode = node;
            if (_selectedNode != null) {
                _selectedNode.Selected = true;
                _selectedNode.BringToFront();
            }
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0) {
                if (evt.target == _container) {
                    SelectNode(null);
                }
            }
            else if (evt.button == 1) {
                ShowContextMenu(evt.localMousePosition);
            }
            else if (evt.button == 2) {
                _panning = true;
                _start = evt.localMousePosition;
                this.CaptureMouse();
                evt.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_panning) return;

            Vector2 delta = evt.localMousePosition - _start;
            _offset += delta;
            _content.transform.position = _offset;
            _start = evt.localMousePosition;
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
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
    }
}
