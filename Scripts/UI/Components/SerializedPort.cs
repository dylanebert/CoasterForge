using System;
using Unity.Mathematics;

namespace CoasterForge.UI {
    [Serializable]
    public class SerializedPort {
        public uint Id;
        public string Name;
        public PortType Type;
        public PointData PointData;
        public float3 Float3Data;
        public float FloatData;
    }
}
