using System;

namespace CoasterForge.UI {
    [Serializable]
    public class SerializedPort {
        public uint Id;
        public string Name;
        public PortType Type;
        public PointData Data;
    }
}
