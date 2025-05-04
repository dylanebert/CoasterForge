using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class AnchorThumbControl : VisualElement {
        private static readonly Color s_HoverOutlineColor = new(0.2f, 0.5f, 0.9f, 0.3f);

        private InputThumb _thumb;
        private bool _mouseCaptured;

        public AnchorThumbControl(InputThumb view) {
            _thumb = view;

            style.position = Position.Absolute;
            style.left = -2f;
            style.right = -2f;
            style.top = -2f;
            style.bottom = -2f;
            style.borderBottomWidth = 2f;
            style.borderTopWidth = 2f;
            style.borderLeftWidth = 2f;
            style.borderRightWidth = 2f;
            style.borderBottomColor = Color.clear;
            style.borderTopColor = Color.clear;
            style.borderLeftColor = Color.clear;
            style.borderRightColor = Color.clear;
            style.backgroundColor = Color.clear;

            _thumb.Port.Node.View.RegisterCallback<MouseCaptureEvent>(OnMouseCapture, TrickleDown.TrickleDown);
            _thumb.Port.Node.View.RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOut, TrickleDown.TrickleDown);
            RegisterCallback<MouseOverEvent>(OnMouseOver);
            RegisterCallback<MouseOutEvent>(OnMouseOut);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<ClickEvent>(OnClick);
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
            if (_mouseCaptured) return;
            SetBorderColor(s_HoverOutlineColor);
            evt?.StopPropagation();
        }

        private void OnMouseOut(MouseOutEvent evt) {
            if (_mouseCaptured) return;
            SetBorderColor(Color.clear);
            evt?.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 1) {
                this.ShowContextMenu(evt.localMousePosition, menu => {
                    menu.AddItem("Promote", () => {
                        _thumb.Port.Node.View.InvokePromoteRequest(_thumb.Port);
                    });
                });
            }
        }

        private void OnClick(ClickEvent evt) {
            if (evt.clickCount == 2) {
                _thumb.Port.Node.View.InvokePromoteRequest(_thumb.Port);
                evt.StopPropagation();
            }
        }

        private void SetBorderColor(Color color) {
            style.borderBottomColor = color;
            style.borderTopColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
        }
    }
}
