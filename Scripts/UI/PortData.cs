using Unity.Entities;

namespace CoasterForge.UI {
    public struct PortData {
        public string Name;
        public Entity Entity;
        public PortType Type;
        public object Data;
        public bool IsInput;
    }
}
