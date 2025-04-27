using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public static class Extensions {
        public static void ShowContextMenu(
            this VisualElement element,
            Vector2 position,
            Action<ContextMenu> configureMenu
        ) {
            var menu = new ContextMenu();
            menu.style.left = position.x;
            menu.style.top = position.y;

            configureMenu(menu);
            element.Add(menu);

            var root = element.panel.visualTree;

            void OnMouseDown(MouseDownEvent evt) {
                bool inMenu = false;
                VisualElement target = evt.target as VisualElement;
                while (target != null) {
                    if (target == menu) {
                        inMenu = true;
                        break;
                    }
                    target = target.parent;
                }

                if (!inMenu && menu.parent != null) {
                    element.Remove(menu);
                    root.UnregisterCallback((EventCallback<MouseDownEvent>)OnMouseDown, TrickleDown.TrickleDown);
                }
            }
            root.RegisterCallback((EventCallback<MouseDownEvent>)OnMouseDown, TrickleDown.TrickleDown);
        }
    }
}
