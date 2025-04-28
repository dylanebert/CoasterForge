using Unity.Entities;

namespace CoasterForge.UI
{
    public struct PortData
    {
        public string Name;
        public Entity Entity;
        public PortType Type;
        public PointData Data;
        public bool IsInput;
    }
}
