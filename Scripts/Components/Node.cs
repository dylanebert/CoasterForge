using Unity.Entities;

namespace CoasterForge {
    public struct Node : IComponentData {
        public NodeType Value;

        public static implicit operator NodeType(Node node) => node.Value;
        public static implicit operator Node(NodeType type) => new() { Value = type };
    }
}
