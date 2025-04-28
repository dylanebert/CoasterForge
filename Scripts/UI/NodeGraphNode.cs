using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class NodeGraphNode : VisualElement {
        private static readonly Color s_DividerColor = new(0.2f, 0.2f, 0.2f);
        private static readonly Color s_BackgroundColor = new(0.25f, 0.25f, 0.25f, 0.8f);
        private static readonly Color s_HeaderColor = new(0.3f, 0.3f, 0.3f, 0.8f);

        private static readonly Color s_HoverOutlineColor = new(0.2f, 0.5f, 0.9f, 0.3f);
        private static readonly Color s_SelectedOutlineColor = new(0.2f, 0.5f, 0.9f);

        private NodeGraphView _view;
        private Entity _entity;
        private NodeType _type;

        private List<NodeGraphPort> _inputs = new();
        private List<NodeGraphPort> _outputs = new();
        private VisualElement _header;
        private VisualElement _headerDivider;
        private VisualElement _contents;
        private VisualElement _inputsContainer;
        private VisualElement _portsDivider;
        private VisualElement _outputsContainer;
        private VisualElement _footerDivider;
        private VisualElement _footer;
        private Vector2 _dragStart;
        private Vector2 _mouseStart;
        private bool _hovering;
        private bool _selected;
        private bool _dragging;
        private bool _moved;
        private bool _mouseCaptured;

        public NodeGraphView View => _view;
        public Entity Entity => _entity;
        public List<NodeGraphPort> Inputs => _inputs;
        public List<NodeGraphPort> Outputs => _outputs;

        public bool Selected {
            get => _selected;
            set {
                _selected = value;
                if (_selected) {
                    Select();
                }
                else {
                    Deselect();
                }
            }
        }

        public NodeGraphNode(
            NodeGraphView view,
            string name,
            Entity entity,
            NodeType type,
            List<PortData> inputPorts,
            List<PortData> outputPorts
        ) {
            _view = view;
            _entity = entity;
            _type = type;

            style.position = Position.Absolute;
            style.backgroundColor = Color.clear;
            style.flexGrow = 1f;
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Stretch;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.paddingLeft = 2f;
            style.paddingRight = 2f;
            style.paddingTop = 2f;
            style.paddingBottom = 2f;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;
            style.borderTopWidth = 2f;
            style.borderBottomWidth = 2f;
            style.borderLeftWidth = 2f;
            style.borderRightWidth = 2f;
            style.borderTopColor = Color.clear;
            style.borderBottomColor = Color.clear;
            style.borderLeftColor = Color.clear;
            style.borderRightColor = Color.clear;

            _header = new VisualElement {
                style = {
                    position = Position.Relative,
                    flexGrow = 1f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    backgroundColor = s_HeaderColor
                }
            };
            _header.Add(new Label(name) {
                style = {
                    marginLeft = 6f,
                    marginRight = 6f,
                    marginTop = 6f,
                    marginBottom = 6f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f
                }
            });
            Add(_header);

            _headerDivider = new VisualElement {
                style = {
                    position = Position.Relative,
                    height = 1f,
                    flexGrow = 1f,
                    backgroundColor = s_DividerColor,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f
                }
            };
            Add(_headerDivider);

            _contents = new VisualElement {
                style = {
                    position = Position.Relative,
                    flexGrow = 1f,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Stretch,
                    justifyContent = Justify.FlexEnd,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                }
            };
            Add(_contents);

            _inputsContainer = new VisualElement {
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Column,
                    justifyContent = Justify.FlexStart,
                    alignItems = Align.FlexStart,
                    backgroundColor = s_HeaderColor,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 4f,
                    paddingBottom = 4f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f
                }
            };
            _contents.Add(_inputsContainer);

            _portsDivider = new VisualElement {
                style = {
                    position = Position.Relative,
                    width = 1f,
                    backgroundColor = s_DividerColor,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f
                }
            };
            _contents.Add(_portsDivider);

            _outputsContainer = new VisualElement {
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Column,
                    justifyContent = Justify.FlexEnd,
                    alignItems = Align.FlexStart,
                    backgroundColor = s_BackgroundColor,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 4f,
                    paddingBottom = 4f,
                    marginLeft = 0f,
                    marginRight = 0f,
                }
            };
            _contents.Add(_outputsContainer);

            _footerDivider = new VisualElement {
                style = {
                    position = Position.Relative,
                    height = 1f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    backgroundColor = s_DividerColor
                }
            };
            Add(_footerDivider);

            _footer = new VisualElement {
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Column,
                    alignItems = Align.Stretch,
                    minHeight = 12f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    backgroundColor = s_HeaderColor
                }
            };
            Add(_footer);

            UpdateInputPorts(inputPorts);
            UpdateOutputPorts(outputPorts);

            _view.RegisterCallback<MouseCaptureEvent>(OnMouseCapture, TrickleDown.TrickleDown);
            _view.RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOut, TrickleDown.TrickleDown);
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        private void UpdateInputPorts(List<PortData> inputPorts) {
            _inputs.Clear();
            _inputsContainer.Clear();

            for (int i = 0; i < inputPorts.Count; i++) {
                var port = inputPorts[i];
                var uiPort = new NodeGraphPort(_view, this, port, port.Name.ToString());
                _inputsContainer.Add(uiPort);
                _inputs.Add(uiPort);
            }
        }

        private void UpdateOutputPorts(List<PortData> outputPorts) {
            _outputs.Clear();
            _outputsContainer.Clear();

            for (int i = 0; i < outputPorts.Count; i++) {
                var port = outputPorts[i];
                var uiPort = new NodeGraphPort(_view, this, port, port.Name.ToString());
                _outputsContainer.Add(uiPort);
                _outputs.Add(uiPort);
            }
        }

        public void Select() {
            _selected = true;
            SetBorderColor(s_SelectedOutlineColor);
        }

        public void Deselect() {
            _selected = false;
            if (!_hovering || _view.BoxSelecting) {
                SetBorderColor(Color.clear);
            }
            else {
                SetBorderColor(s_HoverOutlineColor);
            }
        }

        private void OnMouseCapture(MouseCaptureEvent evt) {
            if (evt.relatedTarget != this) {
                OnMouseLeave(null);
                _mouseCaptured = true;
            }
        }

        private void OnMouseCaptureOut(MouseCaptureOutEvent evt) {
            if (evt.relatedTarget != this) {
                _mouseCaptured = false;
            }
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            if (_view.BoxSelecting || _mouseCaptured) return;
            _hovering = true;
            if (!_selected) {
                SetBorderColor(s_HoverOutlineColor);
            }
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            if (_view.BoxSelecting || _mouseCaptured) return;
            _hovering = false;
            if (!_selected) {
                SetBorderColor(Color.clear);
            }
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0 || evt.button == 1) {
                if (evt.shiftKey) {
                    if (_selected) {
                        _view.DeselectNode(this);
                    }
                    else {
                        _view.SelectNode(this, true);
                    }
                }
                else if (!_selected) {
                    _view.SelectNode(this, false);
                }
            }

            if (evt.button == 0) {
                _dragging = true;
                _dragStart = new Vector2(style.left.value.value, style.top.value.value);
                _mouseStart = evt.mousePosition;
                _moved = false;

                this.CaptureMouse();
                evt.StopPropagation();
            }

            if (evt.button == 1) {
                this.ShowContextMenu(evt.localMousePosition, menu => {
                    menu.AddItem("Delete", () => {
                        _view.InvokeRemoveSelectedRequest();
                    });
                });
            }
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_dragging) return;

            var delta = evt.mousePosition - _mouseStart;
            Vector2 desiredPosition = _dragStart + delta / _view.Zoom;
            Vector2 snappedPosition = _view.SnapNodePosition(this, desiredPosition);
            Vector2 movementDelta = snappedPosition - new Vector2(style.left.value.value, style.top.value.value);

            if (!_moved && movementDelta.sqrMagnitude > 1e-3f) {
                UndoManager.Record();
                _moved = true;
            }

            style.left = snappedPosition.x;
            style.top = snappedPosition.y;

            _view.InvokeMoveNodesRequest(movementDelta);

            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (evt.button == 0) {
                _dragging = false;
                _view.ClearGuides();
                this.ReleaseMouse();
                evt.StopPropagation();
            }
        }

        private void SetBorderColor(Color color) {
            style.borderTopColor = color;
            style.borderBottomColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
        }

        public void SetPosition(Vector2 position) {
            style.left = position.x;
            style.top = position.y;
        }

        public void SetAnchor(PointData anchor) {
            if (_type != NodeType.Anchor) {
                Debug.LogError("SetAnchor called on non-anchor node");
                return;
            }

            _inputs[0].SetData(anchor);
        }
    }
}
