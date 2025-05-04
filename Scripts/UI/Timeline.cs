using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    [UxmlElement]
    public partial class Timeline : VisualElement {
        private static readonly Color s_BackgroundColor = new(0.2f, 0.2f, 0.2f);

        private Label _tip;

        public Timeline() {
            style.position = Position.Absolute;
            style.left = 0;
            style.bottom = 0;
            style.right = 0;
            style.top = 0;
            style.backgroundColor = s_BackgroundColor;

            _tip = new Label("Select a track section to edit") {
                style = {
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 0,
                    bottom = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    color = new Color(0.6f, 0.6f, 0.6f),
                }
            };
            Add(_tip);
        }

        public void SetSelectedSection(SectionData data) {
            _tip.style.display = DisplayStyle.None;
        }

        public void ClearSelectedSection() {
            _tip.style.display = DisplayStyle.Flex;
        }
    }
}
