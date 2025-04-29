using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class InputThumb : VisualElement {
        private static readonly Color s_BackgroundColor = new(0.25f, 0.25f, 0.25f);
        private static readonly Color s_PortColor = new(0.8f, 0.7f, 0.2f);
        private static readonly Color s_CircleBackgroundColor = new(0.2f, 0.2f, 0.2f);

        private NodeGraphPort _port;
        private LabeledFloatField _fieldA;
        private LabeledFloatField _fieldB;
        private LabeledFloatField _fieldC;
        private InputViewEdge _edge;
        private float _lastA, _lastB, _lastC;
        private bool _editing;
        private bool _changed;
        private bool _locked;

        public NodeGraphPort Port => _port;

        public InputThumb(NodeGraphPort port) {
            _port = port;

            style.position = Position.Absolute;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Stretch;
            style.height = 21f;
            style.right = 0f;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.marginLeft = 0f;
            style.marginRight = 32f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.backgroundColor = s_BackgroundColor;

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

            if (_port.Type == PortType.Anchor) {
                container.Add(new Label("Anchor"));
            }
            else if (_port.Type == PortType.Position) {
                _fieldA = new LabeledFloatField(this, "X");
                _fieldB = new LabeledFloatField(this, "Y");
                _fieldC = new LabeledFloatField(this, "Z");

                container.Add(_fieldA);
                container.Add(_fieldB);
                container.Add(_fieldC);
            }
            else if (_port.Type == PortType.Duration) {
                _fieldA = new LabeledFloatField(this, "X");

                container.Add(_fieldA);
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

            if (_port.Type == PortType.Anchor) {
                Add(new AnchorThumbControl(this));
            }

            _fieldA?.RegisterCallback<FocusInEvent>(OnFocusIn);
            _fieldB?.RegisterCallback<FocusInEvent>(OnFocusIn);
            _fieldC?.RegisterCallback<FocusInEvent>(OnFocusIn);
            _fieldA?.RegisterCallback<FocusOutEvent>(OnFocusOut);
            _fieldB?.RegisterCallback<FocusOutEvent>(OnFocusOut);
            _fieldC?.RegisterCallback<FocusOutEvent>(OnFocusOut);
            _fieldA?.Field.RegisterValueChangedCallback(OnValueChanged);
            _fieldB?.Field.RegisterValueChangedCallback(OnValueChanged);
            _fieldC?.Field.RegisterValueChangedCallback(OnValueChanged);

            RegisterCallback<MouseOverEvent>(OnMouseOver);
            RegisterCallback<MouseOutEvent>(OnMouseOut);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        private void OnMouseOver(MouseOverEvent evt) {
            evt.StopPropagation();
        }

        private void OnMouseOut(MouseOutEvent evt) {
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            evt.StopPropagation();
        }

        private void OnFocusIn(FocusInEvent evt) {
            SetEditing(true);
        }

        private void OnFocusOut(FocusOutEvent evt) {
            SetEditing(false);
        }

        private void OnValueChanged(ChangeEvent<float> evt) {
            if (_locked) return;

            bool changed = (_fieldA != null && _fieldA.Field.value != _lastA)
                || (_fieldB != null && _fieldB.Field.value != _lastB)
                || (_fieldC != null && _fieldC.Field.value != _lastC);

            if (!changed) return;

            if (changed && !_changed) {
                UndoManager.Record();
                _changed = true;
            }

            switch (_port.Type) {
                case PortType.Position:
                    float3 position = new(_fieldA.Field.value, _fieldB.Field.value, _fieldC.Field.value);
                    _port.Node.View.InvokePortChangeRequest(_port, position);
                    break;
                case PortType.Duration:
                    float duration = _fieldA.Field.value;
                    _port.Node.View.InvokePortChangeRequest(_port, duration);
                    break;
            }

            _lastA = _fieldA?.Field.value ?? 0f;
            _lastB = _fieldB?.Field.value ?? 0f;
            _lastC = _fieldC?.Field.value ?? 0f;
        }

        public void SetData(object data) {
            if (_editing) return;

            _locked = true;

            switch (_port.Type) {
                case PortType.Position:
                    float3 position = (float3)data;
                    _lastA = position.x;
                    _lastB = position.y;
                    _lastC = position.z;
                    _fieldA.Field.value = position.x;
                    _fieldB.Field.value = position.y;
                    _fieldC.Field.value = position.z;
                    break;
                case PortType.Duration:
                    float duration = (float)data;
                    _lastA = duration;
                    _fieldA.Field.value = duration;
                    break;
            }

            _locked = false;
            OnValueChanged(null);
        }

        public void SetEditing(bool editing) {
            _editing = editing;
            _changed = false;

            _lastA = _fieldA?.Field.value ?? 0f;
            _lastB = _fieldB?.Field.value ?? 0f;
            _lastC = _fieldC?.Field.value ?? 0f;
        }
    }
}
