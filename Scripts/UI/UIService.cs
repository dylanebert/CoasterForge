using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class UIService : MonoBehaviour {
        public UIDocument UIDocument;

        public static UIService Instance { get; private set; }
        public static UnityEngine.UIElements.Cursor SlideArrowCursor { get; private set; }

        private void Awake() {
            Instance = this;

            var slideArrow = Resources.Load<Texture2D>("SlideArrow");
            var cursor = new UnityEngine.UIElements.Cursor {
                texture = slideArrow,
                hotspot = new Vector2(16, 16)
            };
            SlideArrowCursor = cursor;
        }
    }
}
