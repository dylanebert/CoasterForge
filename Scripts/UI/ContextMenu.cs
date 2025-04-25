using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge {
    public class ContextMenu : VisualElement {
        private static readonly Color s_BackgroundColor = new(0.3f, 0.3f, 0.3f);
        private static readonly Color s_HoverColor = new(0.35f, 0.35f, 0.35f);

        public ContextMenu() {
            style.position = Position.Absolute;
            style.backgroundColor = s_BackgroundColor;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;

            style.minWidth = 128f;

            style.borderTopLeftRadius = 3f;
            style.borderTopRightRadius = 3f;
            style.borderBottomLeftRadius = 3f;
            style.borderBottomRightRadius = 3f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopWidth = 1f;
            style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
            style.borderRightColor = new Color(0.1f, 0.1f, 0.1f);
            style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);

            style.opacity = 0f;

            style.transitionProperty = new List<StylePropertyName> { "opacity" };
            style.transitionDuration = new List<TimeValue> { new(150, TimeUnit.Millisecond) };
            style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            schedule.Execute(() => {
                style.opacity = 1f;
            });
        }

        public void AddItem(string text, Action action) {
            var button = new Button(() => {
                action?.Invoke();
                parent?.Remove(this);
            }) {
                text = text,
                style = {
                    backgroundColor = StyleKeyword.None,
                    borderTopWidth = 0f,
                    borderBottomWidth = 0f,
                    borderLeftWidth = 0f,
                    borderRightWidth = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 10f,
                    paddingRight = 10f,
                    paddingTop = 5f,
                    paddingBottom = 5f,
                    width = Length.Percent(100f),
                    unityTextAlign = TextAnchor.MiddleLeft,
                }
            };

            button.RegisterCallback<MouseEnterEvent>((evt) => {
                button.style.backgroundColor = s_HoverColor;
            });
            button.RegisterCallback<MouseLeaveEvent>((evt) => {
                button.style.backgroundColor = StyleKeyword.None;
            });

            Add(button);
        }
    }
}
