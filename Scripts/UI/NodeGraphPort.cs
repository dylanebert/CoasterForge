using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class NodeGraphPort : VisualElement {
        private static NodeGraphPort s_DraggedPort = null;
        private static NodeGraphPort s_HoveredPort = null;

        private static readonly Color s_PortColor = new(0.8f, 0.7f, 0.2f);

        private NodeGraphView _view;
        private NodeGraphNode _node;
        private Entity _entity;
        private VisualElement _connector;
        private VisualElement _circle;
        private VisualElement _cap;
        private Label _label;
        private Edge _dragEdge;
        private bool _isInput;
        private bool _isConnected;

        public NodeGraphNode Node => _node;
        public Entity Entity => _entity;
        public bool IsInput => _isInput;

        public NodeGraphPort(NodeGraphView view, NodeGraphNode node, Entity entity, bool isInput) {
            _view = view;
            _node = node;
            _entity = entity;
            _isInput = isInput;

            style.position = Position.Relative;
            style.flexGrow = 1f;
            style.flexDirection = isInput ? FlexDirection.Row : FlexDirection.RowReverse;
            style.justifyContent = Justify.FlexStart;
            style.alignItems = Align.Center;
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

            _label = new Label(isInput ? "Input" : "Output") {
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

            _connector.RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            _connector.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            _connector.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _connector.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _connector.RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        private void UpdateCapDisplay() {
            if (_isConnected && s_DraggedPort != this) {
                _cap.style.display = DisplayStyle.Flex;
            }
            else if (s_DraggedPort == this || s_HoveredPort == this) {
                _cap.style.display = DisplayStyle.Flex;
            }
            else {
                _cap.style.display = DisplayStyle.None;
            }
        }

        public void SetConnected(bool connected) {
            _isConnected = connected;
            UpdateCapDisplay();
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            s_HoveredPort = this;
            UpdateCapDisplay();
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            if (s_HoveredPort == this) {
                s_HoveredPort = null;
            }
            UpdateCapDisplay();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button != 0) return;

            s_DraggedPort = this;
            _dragEdge = new Edge(_view, Entity.Null, this);
            _view.ConnectionsLayer.Add(_dragEdge);
            _dragEdge.SetDragEnd(evt.mousePosition);
            _connector.CaptureMouse();
            evt.StopPropagation();

            SetConnectorColor(Color.clear);
            UpdateCapDisplay();
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (s_DraggedPort != this || evt.button != 0) return;

            _dragEdge.SetDragEnd(evt.mousePosition);
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (s_DraggedPort != this || evt.button != 0) return;

            if (s_HoveredPort != null) {
                NodeGraphPort source = s_DraggedPort.IsInput ? s_HoveredPort : s_DraggedPort;
                NodeGraphPort target = s_DraggedPort.IsInput ? s_DraggedPort : s_HoveredPort;
                _view.InvokeConnectionRequest(source, target);
            }

            s_DraggedPort = null;
            _view.ConnectionsLayer.Remove(_dragEdge);
            _dragEdge = null;
            _connector.ReleaseMouse();
            evt.StopPropagation();

            SetConnectorColor(s_PortColor);
            UpdateCapDisplay();
        }

        private void SetConnectorColor(Color color) {
            _circle.style.borderLeftColor = color;
            _circle.style.borderRightColor = color;
            _circle.style.borderTopColor = color;
            _circle.style.borderBottomColor = color;
        }
    }
}
