using UnityEngine;

namespace CoasterForge {
    public class RuntimeDebug : MonoBehaviour {
        public Optimizer Optimizer;

        private void Update() {
            if (Input.GetKeyDown(KeyCode.F1)) {
                Optimizer.AddControlPoint();
            }
            else if (Input.GetKeyDown(KeyCode.F2)) {
                Optimizer.RemoveControlPoint();
            }
        }
    }
}
