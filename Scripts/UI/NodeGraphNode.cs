using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge {
    public class NodeGraphNode : VisualElement {
        private static readonly Color s_BackgroundColor = new(0.25f, 0.25f, 0.25f);
        private static readonly Color s_HoverOutlineColor = new(0.2f, 0.5f, 0.9f, 0.5f);
        private static readonly Color s_SelectedOutlineColor = new(0.2f, 0.5f, 0.9f);

        private Vector2 _dragStart;
        private Vector2 _mouseStart;
        private bool _hovered;
        private bool _selected;
        private bool _dragging;

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

        public NodeGraphNode() {
            style.width = 150f;
            style.height = 100f;
            style.position = Position.Absolute;
            style.backgroundColor = s_BackgroundColor;

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

            style.transitionProperty = new List<StylePropertyName> {
                "border-color"
            };
            style.transitionDuration = new List<TimeValue> {
                new(150, TimeUnit.Millisecond)
            };
            style.transitionTimingFunction = new List<EasingFunction> {
                EasingMode.EaseOutCubic
            };

            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        public void Select() {
            SetBorderColor(s_SelectedOutlineColor);
        }

        public void Deselect() {
            if (!_hovered) {
                SetBorderColor(Color.clear);
            }
            else {
                SetBorderColor(s_HoverOutlineColor);
            }
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            _hovered = true;
            if (!_selected) {
                SetBorderColor(s_HoverOutlineColor);
            }
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            _hovered = false;
            if (!_selected) {
                SetBorderColor(Color.clear);
            }
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0) {
                _dragging = true;
                _dragStart = new Vector2(style.left.value.value, style.top.value.value);
                _mouseStart = evt.mousePosition;
                this.CaptureMouse();
                evt.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_dragging) return;

            var delta = evt.mousePosition - _mouseStart;
            float zoom = parent?.transform.scale.x ?? 1f;
            delta /= zoom;
            style.left = _dragStart.x + delta.x;
            style.top = _dragStart.y + delta.y;
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (evt.button == 0) {
                _dragging = false;
                this.ReleaseMouse();
                evt.StopPropagation();
            }
        }

        private void SetBorderColor(Color color) {
            schedule.Execute(() => {
                style.borderTopColor = color;
                style.borderBottomColor = color;
                style.borderLeftColor = color;
                style.borderRightColor = color;
            });
        }
    }
}
