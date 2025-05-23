using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class NodeGraphPort : VisualElement {
        private static NodeGraphPort s_DraggedPort = null;
        private static NodeGraphPort s_HoveredPort = null;

        private static readonly Color s_PortColor = new(0.8f, 0.7f, 0.2f);
        private static readonly Color s_CircleBackgroundColor = new(0.2f, 0.2f, 0.2f);

        private NodeGraphView _view;
        private NodeGraphNode _node;
        private PortData _data;

        private VisualElement _connector;
        private VisualElement _circle;
        private VisualElement _cap;
        private InputThumb _thumb;
        private Label _label;
        private Edge _dragEdge;
        private bool _isConnected;
        private bool _mouseCaptured;

        public NodeGraphNode Node => _node;
        public VisualElement Connector => _connector;
        public string Name => _data.Name;
        public Entity Entity => _data.Entity;
        public PortType Type => _data.Type;
        public object Data => _data.Data;
        public bool IsInput => _data.IsInput;

        public NodeGraphPort(
            NodeGraphView view,
            NodeGraphNode node,
            PortData data
        ) {
            _view = view;
            _node = node;
            _data = data;

            style.position = Position.Relative;
            style.flexGrow = 0f;
            style.flexDirection = data.IsInput ? FlexDirection.Row : FlexDirection.RowReverse;
            style.justifyContent = Justify.FlexStart;
            style.alignItems = Align.Center;
            style.height = 24f;
            style.paddingLeft = 4f;
            style.paddingRight = 4f;
            style.paddingBottom = 0f;
            style.paddingTop = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;

            _connector = new VisualElement {
                style = {
                    position = Position.Relative,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    width = 20f,
                    height = 20f
                }
            };
            Add(_connector);

            _circle = new VisualElement {
                style = {
                    position = Position.Relative,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    marginLeft = 4f,
                    marginRight = 4f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    width = 8f,
                    height = 8f,
                    borderTopLeftRadius = 8f,
                    borderTopRightRadius = 8f,
                    borderBottomLeftRadius = 8f,
                    borderBottomRightRadius = 8f,
                    borderLeftWidth = 1f,
                    borderRightWidth = 1f,
                    borderTopWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftColor = s_PortColor,
                    borderRightColor = s_PortColor,
                    borderTopColor = s_PortColor,
                    borderBottomColor = s_PortColor,
                    backgroundColor = s_CircleBackgroundColor
                }
            };
            _connector.Add(_circle);

            _cap = new VisualElement {
                style = {
                    width = 4f,
                    height = 4f,
                    backgroundColor = s_PortColor,
                    borderTopLeftRadius = 4f,
                    borderTopRightRadius = 4f,
                    borderBottomLeftRadius = 4f,
                    borderBottomRightRadius = 4f,
                    display = DisplayStyle.None,
                }
            };
            _circle.Add(_cap);

            _label = new Label(_data.Name) {
                style = {
                    marginLeft = 4f,
                    marginRight = 4f,
                    marginTop = 2f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                }
            };
            Add(_label);

            if (_data.IsInput) {
                _thumb = new InputThumb(this);
                _connector.Add(_thumb);
            }

            _view.RegisterCallback<MouseCaptureEvent>(OnMouseCapture, TrickleDown.TrickleDown);
            _view.RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOut, TrickleDown.TrickleDown);
            _connector.RegisterCallback<MouseOverEvent>(OnMouseOver);
            _connector.RegisterCallback<MouseOutEvent>(OnMouseOut);
            _connector.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _connector.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _connector.RegisterCallback<MouseUpEvent>(OnMouseUp);

            UpdateDisplay();
        }

        private void UpdateDisplay() {
            if (_isConnected && s_DraggedPort != this) {
                _cap.style.display = DisplayStyle.Flex;
            }
            else if (s_DraggedPort == this || s_HoveredPort == this) {
                _cap.style.display = DisplayStyle.Flex;
            }
            else {
                _cap.style.display = DisplayStyle.None;
            }

            if (_thumb != null) {
                _thumb.style.display = _isConnected ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public void SetConnected(bool connected) {
            _isConnected = connected;
            UpdateDisplay();
        }

        private void OnMouseCapture(MouseCaptureEvent evt) {
            if (evt.relatedTarget != this) {
                OnMouseOut(null);
                _mouseCaptured = true;
            }
        }

        private void OnMouseCaptureOut(MouseCaptureOutEvent evt) {
            if (evt.relatedTarget != this) {
                _mouseCaptured = false;
            }
        }

        private void OnMouseOver(MouseOverEvent evt) {
            if (_mouseCaptured && s_DraggedPort == null) return;

            if (s_DraggedPort != null && (
                s_DraggedPort?.Node == _node
                || s_DraggedPort?.IsInput == _data.IsInput
                || s_DraggedPort?.Type != _data.Type)) return;
            s_HoveredPort = this;
            UpdateDisplay();
        }

        private void OnMouseOut(MouseOutEvent evt) {
            if (_mouseCaptured && s_DraggedPort == null) return;

            if (s_HoveredPort == this) {
                s_HoveredPort = null;
            }
            UpdateDisplay();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button != 0) return;

            s_DraggedPort = this;
            s_HoveredPort = null;
            _dragEdge = new Edge(_view, Entity.Null, this);
            _view.ConnectionsLayer.Add(_dragEdge);
            _dragEdge.SetDragEnd(evt.mousePosition);
            _connector.CaptureMouse();
            evt.StopPropagation();

            SetConnectorColor(Color.clear);
            UpdateDisplay();
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (s_DraggedPort != this || evt.button != 0) return;

            _dragEdge.SetDragEnd(evt.mousePosition);
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (s_DraggedPort != this || evt.button != 0) return;

            if (s_HoveredPort != null) {
                NodeGraphPort source = IsInput ? s_HoveredPort : s_DraggedPort;
                NodeGraphPort target = IsInput ? s_DraggedPort : s_HoveredPort;
                _view.InvokeConnectionRequest(source, target);
            }
            else if (!_data.IsInput) {
                if (_data.Type == PortType.Anchor) {
                    Vector2 localPosition = evt.localMousePosition;
                    Vector2 worldPosition = _connector.LocalToWorld(localPosition);
                    Vector2 viewPosition = _view.WorldToLocal(worldPosition);
                    Vector2 contentPosition = (viewPosition - _view.Offset) / _view.Zoom;
                    _view.ShowContextMenu(viewPosition, menu => {
                        menu.AddItem("Force Section", () => {
                            _view.InvokeAddConnectedNodeRequest(this, contentPosition, NodeType.ForceSection, 0);
                        });
                        menu.AddItem("Geometric Section", () => {
                            _view.InvokeAddConnectedNodeRequest(this, contentPosition, NodeType.GeometricSection, 0);
                        });
                        menu.AddItem("Path Section", () => {
                            _view.InvokeAddConnectedNodeRequest(this, contentPosition, NodeType.PathSection, 0);
                        });
                        menu.AddItem("Reverse", () => {
                            _view.InvokeAddConnectedNodeRequest(this, contentPosition, NodeType.Reverse, 0);
                        });
                    });
                }
                else if (_data.Type == PortType.Path) {
                    Vector2 localPosition = evt.localMousePosition;
                    Vector2 worldPosition = _connector.LocalToWorld(localPosition);
                    Vector2 viewPosition = _view.WorldToLocal(worldPosition);
                    Vector2 contentPosition = (viewPosition - _view.Offset) / _view.Zoom;
                    _view.ShowContextMenu(viewPosition, menu => {
                        menu.AddItem("Path Section", () => {
                            _view.InvokeAddConnectedNodeRequest(this, contentPosition, NodeType.PathSection, 1);
                        });
                        menu.AddItem("Reverse Path", () => {
                            _view.InvokeAddConnectedNodeRequest(this, contentPosition, NodeType.ReversePath, 0);
                        });
                    });
                }
            }

            s_DraggedPort = null;
            _view.ConnectionsLayer.Remove(_dragEdge);
            _dragEdge = null;
            _connector.ReleaseMouse();
            evt.StopPropagation();

            SetConnectorColor(s_PortColor);
            UpdateDisplay();
        }

        private void SetConnectorColor(Color color) {
            _circle.style.borderLeftColor = color;
            _circle.style.borderRightColor = color;
            _circle.style.borderTopColor = color;
            _circle.style.borderBottomColor = color;
        }

        public void SetData(object data) {
            _thumb.SetData(data);
        }
    }
}
