using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class NodeGraphNode : VisualElement {
        private static readonly Color s_BackgroundColor = new(0.25f, 0.25f, 0.25f);
        private static readonly Color s_HoverOutlineColor = new(0.2f, 0.5f, 0.9f, 0.5f);
        private static readonly Color s_SelectedOutlineColor = new(0.2f, 0.5f, 0.9f);

        private NodeGraphView _view;
        private Entity _entity;
        private Vector2 _dragStart;
        private Vector2 _mouseStart;
        private bool _hovered;
        private bool _selected;
        private bool _dragging;
        private bool _moved;

        public Entity Entity => _entity;

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

        public NodeGraphNode(NodeGraphView view, Entity entity) {
            _view = view;
            _entity = entity;

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

            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        public void Select() {
            _selected = true;
            SetBorderColor(s_SelectedOutlineColor);
        }

        public void Deselect() {
            _selected = false;
            if (!_hovered || _view.BoxSelecting) {
                SetBorderColor(Color.clear);
            }
            else {
                SetBorderColor(s_HoverOutlineColor);
            }
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            if (_view.BoxSelecting) return;
            _hovered = true;
            if (!_selected) {
                SetBorderColor(s_HoverOutlineColor);
            }
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            if (_view.BoxSelecting) return;
            _hovered = false;
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
                        _view.InvokeRemoveNodeRequest(this);
                    });
                });
            }
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_dragging) return;

            var delta = evt.mousePosition - _mouseStart;
            float zoom = parent?.transform.scale.x ?? 1f;
            Vector2 desiredPosition = _dragStart + delta / zoom;
            Vector2 snappedPosition = _view.SnapNodePosition(this, desiredPosition);
            Vector2 movementDelta = snappedPosition - new Vector2(style.left.value.value, style.top.value.value);

            if (!_moved && movementDelta.sqrMagnitude > 1e-3f) {
                UndoManager.Record();
                _moved = true;
            }

            style.left = snappedPosition.x;
            style.top = snappedPosition.y;

            _view.MoveSelectedNodes(movementDelta);

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
    }
}
