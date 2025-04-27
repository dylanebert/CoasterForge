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
            var root = element.panel.visualTree.Q<TemplateContainer>();
            Vector2 worldPos = element.LocalToWorld(position);
            Vector2 rootPos = root.WorldToLocal(worldPos);

            var menu = new ContextMenu();
            menu.style.left = rootPos.x;
            menu.style.top = rootPos.y;

            configureMenu(menu);
            root.Add(menu);

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
                    root.Remove(menu);
                    root.UnregisterCallback((EventCallback<MouseDownEvent>)OnMouseDown, TrickleDown.TrickleDown);
                }
            }
            root.RegisterCallback((EventCallback<MouseDownEvent>)OnMouseDown, TrickleDown.TrickleDown);
        }
    }
}
