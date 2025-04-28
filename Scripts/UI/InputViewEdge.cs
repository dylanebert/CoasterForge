using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class InputViewEdge : VisualElement {
        private static readonly Color s_EdgeColor = new(0.8f, 0.7f, 0.2f);

        private VisualElement _target;

        public InputViewEdge(VisualElement target) {
            _target = target;

            style.width = 0f;
            style.height = 0f;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx) {
            Vector2 start = Vector2.zero;

            Vector2 end = _target.worldBound.center;
            end = this.WorldToLocal(end) + new Vector2(-4f, 0f);

            var painter = ctx.painter2D;
            painter.lineWidth = 2f;
            painter.strokeColor = s_EdgeColor;

            painter.BeginPath();
            painter.MoveTo(start);
            painter.LineTo(end);
            painter.Stroke();
        }
    }
}
