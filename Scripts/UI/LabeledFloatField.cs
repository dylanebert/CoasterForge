using Unity.Mathematics;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class LabeledFloatField : VisualElement {
        private InputThumb _thumb;
        private Label _label;
        private float _prevX;
        private float _sensitivity;
        private float _minValue;
        private float _maxValue;
        private bool _dragging;

        public FloatField Field { get; private set; }
        public bool Dragging => _dragging;

        public LabeledFloatField(
            InputThumb thumb,
            string text,
            float sensitivity = 0.01f,
            float minValue = float.MinValue,
            float maxValue = float.MaxValue
        ) {
            _thumb = thumb;
            _sensitivity = sensitivity;
            _minValue = minValue;
            _maxValue = maxValue;

            style.position = Position.Relative;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;

            var dummy = new VisualElement {
                style = {
                    position = Position.Relative,
                    minWidth = 3f,
                }
            };
            Add(dummy);

            _label = new Label(text) {
                style = {
                    cursor = UIService.SlideArrowCursor
                }
            };
            dummy.Add(_label);

            Field = new FloatField {
                style = {
                    width = 30f,
                    marginLeft = 4f,
                    marginRight = 4f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 1f,
                    paddingBottom = 1f
                }
            };
            Add(Field);

            Field.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            _label.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _label.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _label.RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt) {
            Field.Q<TextElement>().style.cursor = UIService.TextCursor;
            Field.UnregisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        private void OnMouseDown(MouseDownEvent evt) {
            _dragging = true;
            _label.CaptureMouse();
            _prevX = evt.mousePosition.x;
            _thumb.SetEditing(true);
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_dragging) return;
            float deltaX = evt.mousePosition.x - _prevX;
            _prevX = evt.mousePosition.x;
            float value = Field.value;
            value += deltaX * _sensitivity;
            value = math.round(value * 100f) / 100f;
            value = math.clamp(value, _minValue, _maxValue);
            Field.value = value;
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (!_dragging) return;
            _dragging = false;
            _label.ReleaseMouse();
            _thumb.SetEditing(false);
            evt.StopPropagation();
        }

        public void Clamp() {
            float value = Field.value;
            if (value >= _minValue && value <= _maxValue) return;
            Field.value = math.clamp(value, _minValue, _maxValue);
        }
    }
}
