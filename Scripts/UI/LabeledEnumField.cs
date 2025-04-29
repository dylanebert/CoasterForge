using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class LabeledEnumField<T> : VisualElement where T : Enum {
        private static readonly Color s_BackgroundColor = new(0.35f, 0.35f, 0.35f);
        private static readonly Color s_HoverBackgroundColor = new(0.4f, 0.4f, 0.4f);
        private static readonly Color s_ArrowColor = new(0.75f, 0.75f, 0.75f);

        private static readonly Color s_OutlineColor = new(0.2f, 0.2f, 0.2f);
        private static readonly Color s_HoverOutlineColor = new(0.2f, 0.5f, 0.9f, 0.3f);

        private NodeGraphNode _node;
        private VisualElement _field;
        private Label _valueLabel;
        private Vector2 _mouseDownPosition;
        private T _value;

        public event Action<T> ValueChanged;

        public LabeledEnumField(NodeGraphNode node, string text, T value) {
            _node = node;
            _value = value;

            style.position = Position.Relative;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 8f;
            style.paddingRight = 4f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;

            var label = new Label(text) {
                style = {
                    flexGrow = 1f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                }
            };
            Add(label);

            _field = new VisualElement() {
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.FlexStart,
                    flexGrow = 0f,
                    width = 80f,
                    height = 17f,
                    paddingLeft = 0f,
                    paddingRight = 4f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 3f,
                    marginRight = 3f,
                    marginTop = 4f,
                    marginBottom = 4f,
                    borderLeftWidth = 1f,
                    borderRightWidth = 1f,
                    borderTopWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftColor = s_OutlineColor,
                    borderRightColor = s_OutlineColor,
                    borderTopColor = s_OutlineColor,
                    borderBottomColor = s_OutlineColor,
                    borderTopLeftRadius = 3f,
                    borderTopRightRadius = 3f,
                    borderBottomLeftRadius = 3f,
                    borderBottomRightRadius = 3f,
                    backgroundColor = s_BackgroundColor,
                }
            };
            Add(_field);

            _valueLabel = new Label(_value.ToString()) {
                style = {
                    flexGrow = 1f,
                    paddingLeft = 4f,
                    paddingRight = 4f,
                    paddingTop = 0f,
                    paddingBottom = 1f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            _field.Add(_valueLabel);

            var arrow = new VisualElement() {
                style = {
                    position = Position.Relative,
                    width = 12f,
                    height = 12f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    backgroundImage = Resources.Load<Texture2D>("Dropdown"),
                    unityBackgroundImageTintColor = s_ArrowColor
                }
            };
            _field.Add(arrow);

            _field.RegisterCallback<MouseOverEvent>(OnMouseOver);
            _field.RegisterCallback<MouseOutEvent>(OnMouseOut);
            _field.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _field.RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        private void OnMouseOver(MouseOverEvent evt) {
            _field.style.backgroundColor = s_HoverBackgroundColor;
            SetBorderColor(s_HoverOutlineColor);
            evt.StopPropagation();
        }

        private void OnMouseOut(MouseOutEvent evt) {
            _field.style.backgroundColor = s_BackgroundColor;
            SetBorderColor(s_OutlineColor);
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            _mouseDownPosition = evt.localMousePosition;
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            Vector2 delta = evt.localMousePosition - _mouseDownPosition;
            const float THRESHOLD = 5f * 5f;
            if (delta.sqrMagnitude > THRESHOLD) return;

            Vector2 anchor = new(0f, _field.resolvedStyle.height);
            _field.ShowContextMenu(anchor, menu => {
                foreach (var enumValue in Enum.GetValues(typeof(T))) {
                    menu.AddItem(enumValue.ToString(), () => {
                        _value = (T)enumValue;
                        _valueLabel.text = _value.ToString();
                        ValueChanged?.Invoke(_value);
                    });
                }
            });
        }

        private void SetBorderColor(Color color) {
            _field.style.borderLeftColor = color;
            _field.style.borderRightColor = color;
            _field.style.borderTopColor = color;
            _field.style.borderBottomColor = color;
        }

        public void SetValue(T value) {
            if (_value.Equals(value)) return;

            _value = value;
            _valueLabel.text = _value.ToString();
        }
    }
}
