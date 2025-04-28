using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class PortInputView : VisualElement {
        private static readonly Color s_BackgroundColor = new(0.25f, 0.25f, 0.25f);
        private static readonly Color s_PortColor = new(0.8f, 0.7f, 0.2f);
        private static readonly Color s_CircleBackgroundColor = new(0.2f, 0.2f, 0.2f);

        private NodeGraphPort _port;
        private FloatField _xField;
        private FloatField _yField;
        private FloatField _zField;
        private InputViewEdge _edge;
        private float _lastX, _lastY, _lastZ;
        private bool _editing;

        public PortInputView(NodeGraphPort port) {
            _port = port;

            style.position = Position.Absolute;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Stretch;
            style.height = 22f;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.marginLeft = 0f;
            style.marginRight = 48f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.backgroundColor = s_BackgroundColor;

            if (_port.Data.Type == PortType.Point) {
                var container = new VisualElement {
                    style = {
                        position = Position.Relative,
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        paddingLeft = 8f,
                        paddingRight = 0f,
                        paddingTop = 0f,
                        paddingBottom = 0f,
                        marginLeft = 0f,
                        marginRight = 0f,
                        marginTop = 0f,
                        marginBottom = 0f,
                        backgroundColor = Color.clear
                    }
                };
                container.Add(new Label("Anchor"));
                Add(container);
            }
            else if (_port.Data.Type == PortType.Float3) {
                var container = new VisualElement {
                    style = {
                        position = Position.Relative,
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        paddingLeft = 8f,
                        paddingRight = 0f,
                        paddingTop = 0f,
                        paddingBottom = 0f,
                        marginLeft = 0f,
                        marginRight = 0f,
                        marginTop = 0f,
                        marginBottom = 0f,
                        backgroundColor = Color.clear
                    }
                };
                Add(container);

                var dummy = new VisualElement {
                    style = {
                        position = Position.Relative,
                        minWidth = 3f,
                    }
                };
                dummy.Add(new Label("X"));
                container.Add(dummy);

                _xField = new FloatField {
                    style = {
                        minWidth = 30f,
                        marginLeft = 4f,
                        marginRight = 4f,
                        marginTop = 1f,
                        marginBottom = 1f,
                        paddingLeft = 0f,
                        paddingRight = 0f,
                        paddingTop = 1f,
                        paddingBottom = 2f,
                    }
                };
                container.Add(_xField);

                dummy = new VisualElement {
                    style = {
                        position = Position.Relative,
                        minWidth = 3f
                    }
                };
                dummy.Add(new Label("Y"));
                container.Add(dummy);

                _yField = new FloatField {
                    style = {
                        minWidth = 30f,
                        marginLeft = 4f,
                        marginRight = 4f,
                        marginTop = 1f,
                        marginBottom = 1f,
                        paddingLeft = 0f,
                        paddingRight = 0f,
                        paddingTop = 1f,
                        paddingBottom = 2f,
                    }
                };
                container.Add(_yField);

                dummy = new VisualElement {
                    style = {
                        position = Position.Relative,
                        minWidth = 3f
                    }
                };
                dummy.Add(new Label("Z"));
                container.Add(dummy);

                _zField = new FloatField {
                    style = {
                        minWidth = 30f,
                        marginLeft = 4f,
                        marginRight = 4f,
                        marginTop = 1f,
                        marginBottom = 1f,
                        paddingLeft = 0f,
                        paddingRight = 0f,
                        paddingTop = 1f,
                        paddingBottom = 2f,
                    }
                };
                container.Add(_zField);

                _xField.RegisterCallback<FocusInEvent>(OnFocusIn);
                _yField.RegisterCallback<FocusInEvent>(OnFocusIn);
                _zField.RegisterCallback<FocusInEvent>(OnFocusIn);
                _xField.RegisterCallback<FocusOutEvent>(OnFocusOut);
                _yField.RegisterCallback<FocusOutEvent>(OnFocusOut);
                _zField.RegisterCallback<FocusOutEvent>(OnFocusOut);
            }

            var connector = new VisualElement {
                style = {
                    position = Position.Relative,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    width = 16f,
                }
            };
            Add(connector);

            var circle = new VisualElement {
                style = {
                    position = Position.Relative,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
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
                    backgroundColor = s_CircleBackgroundColor
                }
            };
            connector.Add(circle);

            var cap = new VisualElement {
                style = {
                    position = Position.Relative,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    width = 4f,
                    height = 4f,
                    backgroundColor = s_PortColor,
                    borderTopLeftRadius = 4f,
                    borderTopRightRadius = 4f,
                    borderBottomLeftRadius = 4f,
                    borderBottomRightRadius = 4f
                }
            };
            circle.Add(cap);

            _edge = new InputViewEdge(_port.Connector);
            cap.Add(_edge);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) {
            style.right = _port.resolvedStyle.width * 0.5f;
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            evt.StopPropagation();
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            evt.StopPropagation();
        }

        private void OnFocusIn(FocusInEvent evt) {
            _editing = true;
        }

        private void OnFocusOut(FocusOutEvent evt) {
            if (evt.target != _xField && evt.target != _yField && evt.target != _zField) {
                _editing = false;

                if (_xField.value != _lastX || _yField.value != _lastY || _zField.value != _lastZ) {
                    PointData data = PointData.Create();
                    data.Position = new float3(_xField.value, _yField.value, _zField.value);
                    _port.Node.View.InvokeAnchorChangeRequest(_port.Node, data);

                    _lastX = _xField.value;
                    _lastY = _yField.value;
                    _lastZ = _zField.value;
                }
            }
        }

        public void SetData(PointData data) {
            if (_editing) return;

            _xField.value = data.Position.x;
            _yField.value = data.Position.y;
            _zField.value = data.Position.z;

            _lastX = data.Position.x;
            _lastY = data.Position.y;
            _lastZ = data.Position.z;
        }
    }
}
