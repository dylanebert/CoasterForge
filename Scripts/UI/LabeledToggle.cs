using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class LabeledToggle : VisualElement {
        private NodeGraphNode _node;
        private Toggle _toggle;

        public Toggle Toggle => _toggle;

        public LabeledToggle(NodeGraphNode node, string text, bool value) {
            _node = node;

            style.position = Position.Relative;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 8f;
            style.paddingRight = 0f;
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

            _toggle = new Toggle {
                value = value,
                style = {
                    position = Position.Relative,
                    width = 16f,
                    height = 16f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 4f,
                    marginRight = 4f,
                    marginTop = 4f,
                    marginBottom = 4f,
                },
            };
            Add(_toggle);
        }

        public void SetValue(bool value) {
            if (_toggle.value == value) return;

            _toggle.value = value;
        }
    }
}
