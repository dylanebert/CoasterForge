using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class UIService : MonoBehaviour {
        public UIDocument UIDocument;

        public static UIService Instance { get; private set; }

        private void Awake() {
            Instance = this;
        }
    }
}
